using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = System.Random;
using Template = SecondFloorPatternLibrary.Template;
using Variant = SecondFloorPatternLibrary.Variant;

/// <summary>Native three-slot layout generator. Its result is the same physical model used to read a PlotWeave snapshot.</summary>
internal static partial class SecondFloorPatternGenerator
{
    private static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
    private const int LayoutAttempts = 20;
    private const int RegionAttempts = 6;
    private const int PlacementProbes = 2000;

    internal readonly struct SlotSettings
    {
        public readonly string RegionTypeId;
        public readonly int StripCount;
        public readonly int MaxNoLoop;
        public SlotSettings(string regionTypeId, int stripCount, int maxNoLoop)
        {
            RegionTypeId = regionTypeId; StripCount = stripCount; MaxNoLoop = maxNoLoop;
        }
    }

    internal sealed class Layout
    {
        public SecondFloorFloorMap Map { get; }
        public double Resource { get; }
        public double Difficulty { get; }
        internal IReadOnlyList<Strip> Strips { get; }
        internal IReadOnlyList<Join> Joins { get; }

        internal Layout(SecondFloorFloorMap map, List<Strip> strips, List<Join> joins)
        {
            Map = map; Strips = strips.AsReadOnly(); Joins = joins.AsReadOnly();
            Resource = strips.Sum(s => s.Template.Resource);
            Difficulty = strips.Sum(s => s.Template.Difficulty);
        }
    }

    internal sealed class Strip
    {
        public readonly int Index;
        public readonly int Slot;
        public readonly Template Template;
        public readonly Variant Variant;
        public readonly Vector2Int Origin;
        public Strip(int index, int slot, Template template, Variant variant, Vector2Int origin)
        {
            Index = index; Slot = slot; Template = template; Variant = variant; Origin = origin;
        }
        public Strip Move(Vector2Int offset) => new Strip(Index, Slot, Template, Variant, Origin + offset);
        public IEnumerable<Vector2Int> Positions => Variant.Cells.Select(c => Origin + c.Position);
    }

    internal readonly struct Join
    {
        public readonly int Left, Right;
        public readonly Vector2Int From, To;
        public Join(int left, int right, Vector2Int from, Vector2Int to)
        {
            Left = left; Right = right; From = from; To = to;
        }
        public Join Move(Vector2Int offset) => new Join(Left, Right, From + offset, To + offset);
    }

    private sealed class Region
    {
        public readonly string TypeId;
        public readonly List<Strip> Strips = new List<Strip>();
        public readonly List<Join> Joins = new List<Join>();
        public Region(string typeId) => TypeId = typeId;
    }

    internal static Layout Generate(SecondFloorPatternLibrary library, IReadOnlyList<SlotSettings> slots, int seed,
        int baseLevel, float levelPerHop, int maxLevel, Func<Layout, double> targetDistance = null, string mapId = null)
    {
        if (library == null) throw new ArgumentNullException(nameof(library));
        if (slots == null || slots.Count != 3) throw new ArgumentException("Expected exactly three ordered region slots (A/B/C).", nameof(slots));
        if (baseLevel < 1 || maxLevel < baseLevel || levelPerHop < 0 || float.IsNaN(levelPerHop) || float.IsInfinity(levelPerHop))
            throw new ArgumentException("Invalid floor level progression.");
        var pools = new Template[3][];
        for (int i = 0; i < 3; i++)
        {
            var setting = slots[i];
            if (string.IsNullOrWhiteSpace(setting.RegionTypeId) || setting.StripCount < 3 || setting.StripCount > 20 || setting.MaxNoLoop < 0)
                throw new ArgumentException($"Invalid settings for region slot {(char)('A' + i)}.", nameof(slots));
            pools[i] = library.Templates.Where(t => t.Role == "normal" && Capacity(t) > 0 && t.MatchesRegion(setting.RegionTypeId)).ToArray();
            if (pools[i].Sum(t => t.SpawnEligible || t.NoLoop ? 0 : Capacity(t)) < (i == 0 ? 2 : 3))
                throw new InvalidOperationException($"Region {setting.RegionTypeId} has insufficient normal patterns to make a ring.");
        }
        var spawns = pools[0].Where(t => t.SpawnEligible).ToArray();
        if (spawns.Length == 0) throw new InvalidOperationException($"Region {slots[0].RegionTypeId} has no eligible spawn pattern.");
        foreach (var template in spawns)
            if (template.Variants[0].Cells.Count(c => c.Type == "spawn") != 1)
                throw new InvalidOperationException($"Spawn pattern '{template.Id}' must contain exactly one spawn cell.");
        var bridges = library.Templates.Where(t => t.Role == "bridge" && Capacity(t) > 0).ToArray();
        for (int i = 0; i < 3; i++)
        {
            var a = slots[i].RegionTypeId;
            var b = slots[(i + 1) % 3].RegionTypeId;
            if (!bridges.Any(t => t.MatchesRegion(a) && t.MatchesRegion(b)))
                throw new InvalidOperationException($"No eligible bridge patterns for {a} ↔ {b}.");
        }

        var random = new Random(seed);
        int regionsBuilt = 0;
        int fullLayouts = 0;
        Layout closest = null;
        double closestDistance = double.PositiveInfinity;
        for (int attempt = 0; attempt < LayoutAttempts; attempt++)
        {
            var regions = new Region[3];
            var counts = new Dictionary<string, int>();
            int nextIndex = 0;
            for (int i = 0; i < 3; i++)
            {
                for (int retry = 0; retry < RegionAttempts && regions[i] == null; retry++)
                    regions[i] = TryBuildRegion(slots[i], i, nextIndex, pools[i], spawns, counts, random);
                if (regions[i] == null) break;
                foreach (var strip in regions[i].Strips) counts[strip.Template.Id] = Used(counts, strip.Template) + 1;
                nextIndex += regions[i].Strips.Count;
            }
            if (regions[2] == null) continue;
            regionsBuilt++;
            if (!TryAssemble(regions, bridges, counts, random, out var strips, out var joins)) continue;
            var layout = BuildLayout(mapId ?? $"pattern-preview-{seed}", strips, joins, library, baseLevel, levelPerHop, maxLevel);
            if (targetDistance == null) return layout;
            double distance = targetDistance(layout);
            if (distance == 0) return layout;
            if (closest == null || distance < closestDistance)
            {
                closest = layout;
                closestDistance = distance;
            }
            // Keep the slot counts/configuration fixed. Compare only complete valid maps within the existing search budget.
            if (++fullLayouts >= 8) break;
        }
        if (closest != null) return closest;
        throw new InvalidOperationException($"Cannot assemble floor for seed {seed}: {regionsBuilt}/{LayoutAttempts} attempts built all three rings, with no valid three-bridge layout. Check region pattern eligibility, limits and shapes.");
    }

    private static Region TryBuildRegion(SlotSettings settings, int slot, int startIndex, Template[] templates,
        Template[] spawnTemplates, Dictionary<string, int> used, Random random)
    {
        var region = new Region(settings.RegionTypeId);
        var count = new Dictionary<string, int>(used);
        var owners = new Dictionary<Vector2Int, (Strip Strip, bool IsPort)>();
        bool ring = false;
        int noLoopCount = 0;
        var first = WeightedOrder(slot == 0 ? spawnTemplates : templates.Where(t => !t.SpawnEligible && !t.NoLoop), random)
            .FirstOrDefault(t => Used(count, t) < Capacity(t));
        if (first == null) return null;
        Add(new Strip(startIndex, slot, first, first.Variants[random.Next(first.Variants.Count)], Vector2Int.zero));
        while (region.Strips.Count < settings.StripCount)
        {
            int left = settings.StripCount - region.Strips.Count;
            // Try to close as early as the third strip; only the final placement requires closure.
            bool tryClose = !ring && region.Strips.Count >= 2;
            var eligible = WeightedOrder(templates.Where(t => !t.SpawnEligible && Used(count, t) < Capacity(t) &&
                (!t.NoLoop || (ring && noLoopCount < settings.MaxNoLoop))), random).ToArray();
            if (tryClose && TryPlace(eligible, 2)) continue;
            if ((tryClose && left == 1) || !TryPlace(eligible, 1)) return null;
        }
        return ring ? region : null;

        void Add(Strip strip)
        {
            bool restricted = strip.Variant.Cells.Any(c => c.IsPortal);
            foreach (var cell in strip.Variant.Cells)
                owners.Add(strip.Origin + cell.Position, (strip, !restricted || cell.IsPortal));
            region.Strips.Add(strip);
            count[strip.Template.Id] = Used(count, strip.Template) + 1;
            if (strip.Template.NoLoop) noLoopCount++;
        }

        bool TryPlace(Template[] candidates, int requiredJoins)
        {
            var anchors = owners.Where(p => p.Value.IsPort && !p.Value.Strip.Template.NoLoop)
                .SelectMany(p => Directions.Where(d => !owners.ContainsKey(p.Key + d)).Select(d => p.Key + d))
                .Distinct().OrderBy(_ => random.Next()).ToArray();
            int probes = PlacementProbes;
            foreach (var template in candidates)
            foreach (var variant in template.Variants.OrderBy(_ => random.Next()))
            {
                bool restricted = variant.Cells.Any(c => c.IsPortal);
                var ports = variant.Cells.Where(c => !restricted || c.IsPortal).OrderBy(_ => random.Next()).ToArray();
                var tried = new HashSet<Vector2Int>();
                foreach (var target in anchors)
                foreach (var port in ports)
                {
                    var origin = target - port.Position;
                    if (!tried.Add(origin)) continue;
                    if (--probes < 0) return false;
                    if (variant.Cells.Any(c => owners.ContainsKey(origin + c.Position))) continue;
                    int index = startIndex + region.Strips.Count;
                    var matches = new Dictionary<int, Join>();
                    foreach (var entry in ports)
                    {
                        var to = origin + entry.Position;
                        foreach (var d in Directions)
                        {
                            var from = to + d;
                            if (owners.TryGetValue(from, out var host) && host.IsPort && !host.Strip.Template.NoLoop)
                                matches.TryAdd(host.Strip.Index, new Join(host.Strip.Index, index, from, to));
                        }
                    }
                    if (matches.Count < requiredJoins) continue;
                    Add(new Strip(index, slot, template, variant, origin));
                    region.Joins.AddRange(matches.Values.Take(requiredJoins));
                    if (requiredJoins == 2) ring = true;
                    return true;
                }
            }
            return false;
        }
    }

    private static Layout BuildLayout(string mapId, List<Strip> strips, List<Join> joins, SecondFloorPatternLibrary library,
        int baseLevel, float levelPerHop, int maxLevel)
    {
        var spawnStrip = strips.Single(s => s.Template.SpawnEligible);
        var spawn = spawnStrip.Origin + spawnStrip.Variant.Cells.Single(c => c.Type == "spawn").Position;
        var graph = new List<int>[strips.Count];
        for (int i = 0; i < graph.Length; i++) graph[i] = new List<int>();
        foreach (var link in joins)
        {
            graph[link.Left].Add(link.Right);
            graph[link.Right].Add(link.Left);
        }
        var distance = Enumerable.Repeat(-1, strips.Count).ToArray();
        var pending = new Queue<int>();
        distance[spawnStrip.Index] = 0;
        pending.Enqueue(spawnStrip.Index);
        while (pending.Count != 0)
        {
            int from = pending.Dequeue();
            foreach (int to in graph[from])
            {
                if (distance[to] >= 0) continue;
                distance[to] = distance[from] + 1;
                pending.Enqueue(to);
            }
        }
        if (distance.Contains(-1)) throw new InvalidOperationException("Generated strip graph is disconnected.");
        var tiles = new List<SecondFloorFloorMap.Tile>();
        var edges = new List<(Vector2Int From, Vector2Int To, SecondFloorEdgeKind Kind)>();
        foreach (var strip in strips)
        {
            var stripId = $"s{strip.Index}";
            int level = (int)Math.Min(maxLevel, Math.Floor(baseLevel + (double)levelPerHop * distance[strip.Index] + 0.5));
            foreach (var cell in strip.Variant.Cells)
            {
                int? enemyLevel = library.CellTypes[cell.Type].SupportsOffset
                    ? (int)Math.Min(maxLevel, Math.Max(1L, (long)level + cell.DifficultyOffset)) : (int?)null;
                tiles.Add(new SecondFloorFloorMap.Tile(strip.Origin + cell.Position, cell.Type, enemyLevel, new[] { stripId }));
            }
            foreach (var edge in strip.Variant.Edges)
                edges.Add((strip.Origin + edge.A, strip.Origin + edge.B, SecondFloorEdgeKind.Open));
        }
        foreach (var join in joins) edges.Add((join.From, join.To, SecondFloorEdgeKind.Door));
        return new Layout(SecondFloorFloorMap.FromLayout(mapId, spawn, tiles, edges), strips, joins);
    }

    private static int Capacity(Template t) => t.InstanceLimit;
    private static int Used(Dictionary<string, int> counts, Template t) => counts.TryGetValue(t.Id, out int count) ? count : 0;
    private static IEnumerable<Template> WeightedOrder(IEnumerable<Template> templates, Random random) =>
        templates.OrderBy(t => -Math.Log(1d - random.NextDouble()) / t.Weight);
}
