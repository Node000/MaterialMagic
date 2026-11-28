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

    internal readonly struct TopologyOptions
    {
        public readonly int? SpawnRegionSlot;
        public readonly int MinCycleLength;
        public readonly double ExtraLoopChance;
        public readonly bool LocalClustering;

        public static readonly TopologyOptions Legacy = new TopologyOptions(0, 3, 0d, false);

        public TopologyOptions(int? spawnRegionSlot, int minCycleLength, double extraLoopChance, bool localClustering)
        {
            SpawnRegionSlot = spawnRegionSlot;
            MinCycleLength = Math.Max(3, minCycleLength);
            ExtraLoopChance = Math.Max(0d, Math.Min(1d, extraLoopChance));
            LocalClustering = localClustering;
        }
    }

    internal sealed class Layout
    {
        public SecondFloorFloorMap Map { get; }
        public double Resource { get; }
        public double Difficulty { get; }
        public int SpawnRegionSlot { get; }
        internal IReadOnlyList<Strip> Strips { get; }
        internal IReadOnlyList<Join> Joins { get; }

        internal Layout(SecondFloorFloorMap map, List<Strip> strips, List<Join> joins, double resource, double difficulty, int spawnRegionSlot = 0)
        {
            Map = map; Strips = strips.AsReadOnly(); Joins = joins.AsReadOnly();
            Resource = resource;
            Difficulty = difficulty;
            SpawnRegionSlot = spawnRegionSlot;
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
        int baseLevel, float levelPerHop, int maxLevel, Func<Layout, double> targetDistance = null, string mapId = null,
        SecondFloorMapPools.RingSpacing spacing = SecondFloorMapPools.RingSpacing.Legacy) =>
        Generate(library, slots, seed, baseLevel, levelPerHop, maxLevel, targetDistance, mapId, spacing, TopologyOptions.Legacy);

    internal static Layout Generate(SecondFloorPatternLibrary library, IReadOnlyList<SlotSettings> slots, int seed,
        int baseLevel, float levelPerHop, int maxLevel, Func<Layout, double> targetDistance, string mapId,
        SecondFloorMapPools.RingSpacing spacing, TopologyOptions topology)
    {
        if (library == null) throw new ArgumentNullException(nameof(library));
        if (slots == null || slots.Count != 3) throw new ArgumentException("Expected exactly three ordered region slots (A/B/C).", nameof(slots));
        if (baseLevel < 1 || maxLevel < baseLevel || levelPerHop < 0 || float.IsNaN(levelPerHop) || float.IsInfinity(levelPerHop))
            throw new ArgumentException("Invalid floor level progression.");

        int minCycleLength = Math.Max(3, topology.MinCycleLength);
        var pools = new Template[3][];
        var spawnsBySlot = new Template[3][];
        for (int i = 0; i < 3; i++)
        {
            var setting = slots[i];
            if (string.IsNullOrWhiteSpace(setting.RegionTypeId) || setting.StripCount < 3 || setting.StripCount > 20 || setting.MaxNoLoop < 0)
                throw new ArgumentException($"Invalid settings for region slot {(char)('A' + i)}.", nameof(slots));
            pools[i] = library.FamilyRoots.Where(t => t.Role == "normal" && Capacity(t) > 0 && t.MatchesRegion(setting.RegionTypeId)).ToArray();
            spawnsBySlot[i] = pools[i].Where(t => t.SpawnEligible).ToArray();
        }

        int spawnSlot;
        if (topology.SpawnRegionSlot.HasValue)
        {
            spawnSlot = topology.SpawnRegionSlot.Value;
            if (spawnSlot < 0 || spawnSlot > 2)
                throw new ArgumentException($"Invalid spawn region slot {spawnSlot}.", nameof(topology));
        }
        else
        {
            var eligibleSpawnSlots = Enumerable.Range(0, 3).Where(i => spawnsBySlot[i].Length > 0).ToArray();
            if (eligibleSpawnSlots.Length == 0)
                throw new InvalidOperationException("No region slot has an eligible spawn pattern.");
            spawnSlot = eligibleSpawnSlots[new Random(seed ^ 0x5350574E).Next(eligibleSpawnSlots.Length)];
        }

        for (int i = 0; i < 3; i++)
        {
            if (pools[i].Sum(t => t.SpawnEligible || t.NoLoop ? 0 : Capacity(t)) < (i == spawnSlot ? 2 : 3))
                throw new InvalidOperationException($"Region {slots[i].RegionTypeId} has insufficient normal patterns to make a ring.");
        }
        var spawns = spawnsBySlot[spawnSlot];
        if (spawns.Length == 0) throw new InvalidOperationException($"Region {slots[spawnSlot].RegionTypeId} has no eligible spawn pattern.");
        foreach (var template in spawns)
            if (template.Variants[0].Cells.Count(c => c.Type == "spawn") != 1)
                throw new InvalidOperationException($"Spawn pattern '{template.Id}' must contain exactly one spawn cell.");
        var bridges = library.FamilyRoots.Where(t => t.Role == "bridge" && Capacity(t) > 0).ToArray();
        for (int i = 0; i < 3; i++)
        {
            var a = slots[i].RegionTypeId;
            var b = slots[(i + 1) % 3].RegionTypeId;
            if (!bridges.Any(t => t.MatchesRegion(a) && t.MatchesRegion(b)))
                throw new InvalidOperationException($"No eligible bridge patterns for {a} ↔ {b}.");
        }

        var normalizedTopology = new TopologyOptions(spawnSlot, minCycleLength, topology.ExtraLoopChance, topology.LocalClustering);
        var random = new Random(seed);
        // Content choices must not perturb the seeded root ordering or geometry search.
        var versionRandom = new Random(seed ^ 0x56455253);
        int regionsBuilt = 0;
        int fullLayouts = 0;
        Layout closest = null;
        double closestDistance = double.PositiveInfinity;
        double closestContact = double.PositiveInfinity;
        bool improveSpacing = spacing != SecondFloorMapPools.RingSpacing.Legacy;
        int spacingSamples = 0;
        for (int attempt = 0; attempt < LayoutAttempts; attempt++)
        {
            var regions = new Region[3];
            var counts = new Dictionary<string, int>();
            int nextIndex = 0;
            for (int i = 0; i < 3; i++)
            {
                for (int retry = 0; retry < RegionAttempts && regions[i] == null; retry++)
                    regions[i] = TryBuildRegion(slots[i], i, i == spawnSlot, nextIndex, pools[i], spawns, counts, random, versionRandom,
                        retry < RegionAttempts - 1 ? spacing : SecondFloorMapPools.RingSpacing.Legacy, normalizedTopology);
                if (regions[i] == null) break;
                foreach (var strip in regions[i].Strips) RecordUse(counts, strip.Template);
                nextIndex += regions[i].Strips.Count;
            }
            if (regions[2] == null) continue;
            regionsBuilt++;
            if (!TryAssemble(regions, bridges, counts, random, versionRandom, out var strips, out var joins)) continue;
            // Per-attempt content stream: retries must not change which types a successful layout draws.
            var contentRandom = new Random(seed ^ 0x54595045 ^ attempt);
            var layout = BuildLayout(mapId ?? $"pattern-preview-{seed}", strips, joins, library, baseLevel, levelPerHop,
                maxLevel, contentRandom, spawnSlot);
            double distance = targetDistance?.Invoke(layout) ?? 0;
            if (!improveSpacing && distance == 0) return layout;
            double contact = improveSpacing ? FloorContactCost(layout, spacing) : 0;
            if (closest == null || distance < closestDistance || (distance == closestDistance && contact < closestContact))
            {
                closest = layout;
                closestDistance = distance;
                closestContact = contact;
            }
            // Budget stays the first priority; within it compare a few complete valid layouts.
            if (improveSpacing && distance == 0 &&
                (++spacingSamples >= (spacing == SecondFloorMapPools.RingSpacing.Spacious ? 5 : 4) || contact == 0)) break;
            if (++fullLayouts >= 8) break;
        }
        if (closest != null) return closest;
        if (improveSpacing) return Generate(library, slots, seed, baseLevel, levelPerHop, maxLevel, targetDistance, mapId,
            SecondFloorMapPools.RingSpacing.Legacy, normalizedTopology);
        throw new InvalidOperationException($"Cannot assemble floor for seed {seed}: {regionsBuilt}/{LayoutAttempts} attempts built all three rings, with no valid three-bridge layout. Check region pattern eligibility, limits and shapes.");
    }

    private static Region TryBuildRegion(SlotSettings settings, int slot, bool isSpawnSlot, int startIndex, Template[] templates,
        Template[] spawnTemplates, Dictionary<string, int> used, Random random, Random versionRandom,
        SecondFloorMapPools.RingSpacing spacing, TopologyOptions topology)
    {
        var region = new Region(settings.RegionTypeId);
        var count = new Dictionary<string, int>(used);
        var owners = new Dictionary<Vector2Int, (Strip Strip, bool IsPort)>();
        bool ring = false;
        int noLoopCount = 0;
        int effectiveMinCycle = Math.Min(Math.Max(3, topology.MinCycleLength), settings.StripCount);
        bool useMinCycleCheck = effectiveMinCycle > 3;
        bool useExtraLoops = topology.ExtraLoopChance > 0d;
        bool useClustering = topology.LocalClustering && settings.StripCount >= 5;
        int clusterQuota = useClustering ? Math.Min(3, Math.Max(2, settings.StripCount * 2 / 5)) : 0;
        var clusterStrips = new HashSet<int>();
        int clusterPlaced = 0;

        var firstRoot = WeightedOrder(isSpawnSlot ? spawnTemplates : templates.Where(t => !t.SpawnEligible && !t.NoLoop), random)
            .FirstOrDefault(t => Used(count, t) < Capacity(t));
        if (firstRoot == null) return null;
        var first = SelectVersion(firstRoot, versionRandom);
        var firstVariants = PlacementVariants(first);
        Add(new Strip(startIndex, slot, first, firstVariants[random.Next(firstVariants.Count)], Vector2Int.zero));
        while (region.Strips.Count < settings.StripCount)
        {
            int left = settings.StripCount - region.Strips.Count;
            // Respect minCycleLength: cannot close a cycle of length K with fewer than K - 1 existing strips.
            bool tryClose = !ring && region.Strips.Count >= effectiveMinCycle - 1;
            var eligible = WeightedOrder(templates.Where(t => !t.SpawnEligible && Used(count, t) < Capacity(t) &&
                (!t.NoLoop || (ring && noLoopCount < settings.MaxNoLoop))), random)
                .Select(root => SelectVersion(root, versionRandom)).ToArray();
            if (tryClose && TryPlace(eligible, 2)) continue;
            if ((tryClose && left == 1) || !TryPlace(eligible, 1)) return null;
        }
        if (!ring) return null;

        // Post-placement green-line pass: open additional doors between adjacent non-noLoop strips when cycle length >= minCycleLength.
        if (useExtraLoops)
        {
            AddExtraAdjacentDoors();
        }
        return region;

        int ShortestStripPath(int fromIndex, int toIndex)
        {
            if (fromIndex == toIndex) return 0;
            var visited = new HashSet<int> { fromIndex };
            var queue = new Queue<(int Id, int Dist)>();
            queue.Enqueue((fromIndex, 0));
            while (queue.Count > 0)
            {
                var (cur, dist) = queue.Dequeue();
                foreach (var j in region.Joins)
                {
                    int nxt = j.Left == cur ? j.Right : j.Right == cur ? j.Left : -1;
                    if (nxt < 0) continue;
                    if (nxt == toIndex) return dist + 1;
                    if (visited.Add(nxt)) queue.Enqueue((nxt, dist + 1));
                }
            }
            return int.MaxValue / 4;
        }

        int StripDegree(int stripIndex)
        {
            int deg = 0;
            foreach (var j in region.Joins)
                if (j.Left == stripIndex || j.Right == stripIndex) deg++;
            return deg;
        }

        bool HasJoin(int a, int b)
        {
            foreach (var j in region.Joins)
                if ((j.Left == a && j.Right == b) || (j.Left == b && j.Right == a)) return true;
            return false;
        }

        void Add(Strip strip)
        {
            bool restricted = strip.Variant.Cells.Any(c => c.IsPortal);
            foreach (var cell in strip.Variant.Cells)
                owners.Add(strip.Origin + cell.Position, (strip, !restricted || cell.IsPortal));
            region.Strips.Add(strip);
            RecordUse(count, strip.Template);
            if (strip.Template.NoLoop) noLoopCount++;
            else if (useClustering && clusterStrips.Count == 0 && region.Strips.Count >= 2)
                clusterStrips.Add(strip.Index);
        }

        void AddExtraAdjacentDoors()
        {
            var pairDoors = new Dictionary<(int, int), List<Join>>();
            foreach (var pair in owners)
            {
                var a = pair.Value;
                if (!a.IsPort || a.Strip.Template.NoLoop) continue;
                foreach (var d in Directions)
                {
                    var neighborPos = pair.Key + d;
                    if (!owners.TryGetValue(neighborPos, out var b) || !b.IsPort || b.Strip.Template.NoLoop) continue;
                    if (a.Strip.Index >= b.Strip.Index) continue;
                    if (HasJoin(a.Strip.Index, b.Strip.Index)) continue;
                    var key = (a.Strip.Index, b.Strip.Index);
                    if (!pairDoors.TryGetValue(key, out var list)) pairDoors.Add(key, list = new List<Join>());
                    list.Add(new Join(a.Strip.Index, b.Strip.Index, pair.Key, neighborPos));
                }
            }
            var ranked = pairDoors.Keys
                .Select(k => (Key: k, Leaf: (StripDegree(k.Item1) <= 1 ? 1 : 0) + (StripDegree(k.Item2) <= 1 ? 1 : 0),
                    Dist: ShortestStripPath(k.Item1, k.Item2)))
                .Where(x => x.Dist + 1 >= effectiveMinCycle)
                .OrderByDescending(x => x.Leaf)
                .ThenByDescending(x => x.Dist)
                .ToArray();
            foreach (var item in ranked)
            {
                if (HasJoin(item.Key.Item1, item.Key.Item2)) continue;
                if (ShortestStripPath(item.Key.Item1, item.Key.Item2) + 1 < effectiveMinCycle) continue;
                if (random.NextDouble() >= topology.ExtraLoopChance) continue;
                var doors = pairDoors[item.Key];
                region.Joins.Add(doors[random.Next(doors.Count)]);
            }
        }

        bool TryPlace(Template[] candidates, int requiredJoins)
        {
            var anchors = owners.Where(p => p.Value.IsPort && !p.Value.Strip.Template.NoLoop)
                .SelectMany(p => Directions.Where(d => !owners.ContainsKey(p.Key + d)).Select(d => p.Key + d))
                .Distinct().OrderBy(_ => random.Next()).ToArray();
            int probes = PlacementProbes;
            bool inClusterPhase = useClustering && clusterStrips.Count > 0 && clusterPlaced < clusterQuota && requiredJoins == 1;
            bool improve = spacing != SecondFloorMapPools.RingSpacing.Legacy || inClusterPhase || useExtraLoops;
            int index = startIndex + region.Strips.Count;
            Func<int, int, bool> validCyclePair = useMinCycleCheck
                ? (a, b) => ShortestStripPath(a, b) + 2 >= effectiveMinCycle
                : (Func<int, int, bool>)null;

            foreach (var template in candidates)
            {
                Variant bestVariant = null;
                Vector2Int bestOrigin = default;
                Join[] bestJoins = null;
                double bestScore = double.PositiveInfinity;
                int sampled = 0;
                foreach (var variant in PlacementVariants(template).OrderBy(_ => random.Next()))
                {
                    bool restricted = variant.Cells.Any(c => c.IsPortal);
                    var ports = variant.Cells.Where(c => !restricted || c.IsPortal).OrderBy(_ => random.Next()).ToArray();
                    var tried = new HashSet<Vector2Int>();
                    foreach (var target in anchors)
                    foreach (var port in ports)
                    {
                        var origin = target - port.Position;
                        if (!tried.Add(origin)) continue;
                        if (--probes < 0) goto ChoosePlacement;
                        if (variant.Cells.Any(c => owners.ContainsKey(origin + c.Position))) continue;
                        var matches = new Dictionary<int, Join>();
                        var alternatives = (improve && requiredJoins == 2) || (useMinCycleCheck && requiredJoins == 2) ||
                            (useExtraLoops && ring && !template.NoLoop)
                            ? new List<Join>() : null;
                        foreach (var entry in ports)
                        {
                            var to = origin + entry.Position;
                            foreach (var d in Directions)
                            {
                                var from = to + d;
                                if (!owners.TryGetValue(from, out var host) || !host.IsPort || host.Strip.Template.NoLoop) continue;
                                var join = new Join(host.Strip.Index, index, from, to);
                                matches.TryAdd(host.Strip.Index, join);
                                alternatives?.Add(join);
                            }
                        }
                        if (matches.Count < requiredJoins) continue;

                        if (!improve)
                        {
                            if (requiredJoins == 2 && useMinCycleCheck)
                            {
                                var validPair = FindFirstValidPair(matches.Values.ToArray(), validCyclePair);
                                if (validPair == null) continue;
                                return Commit(variant, origin, validPair);
                            }
                            return Commit(variant, origin, matches.Values.Take(requiredJoins));
                        }

                        var contacts = PlacementContacts(variant, origin, owners);
                        Join[] joins;
                        if (requiredJoins == 2)
                        {
                            joins = ChooseClosure(alternatives, contacts, spacing, validCyclePair);
                            if (joins == null) continue;
                        }
                        else if (useExtraLoops && ring && !template.NoLoop && matches.Count >= 2 && random.NextDouble() < topology.ExtraLoopChance)
                        {
                            var extraPair = ChooseClosure(alternatives, contacts, spacing, validCyclePair);
                            joins = extraPair ?? new[] { PickSingleJoin(matches, inClusterPhase, clusterStrips) };
                        }
                        else
                        {
                            joins = new[] { PickSingleJoin(matches, inClusterPhase, clusterStrips) };
                        }

                        double score = ContactCost(contacts, spacing, joins) - ClosureBonus(joins, spacing);
                        if (joins.Length == 2 && requiredJoins == 1) score -= 18;
                        if (inClusterPhase)
                        {
                            bool joinsCluster = joins.Any(j => clusterStrips.Contains(j.Left));
                            int clusterContacts = contacts.Count(w => clusterStrips.Contains(w.First) || clusterStrips.Contains(w.Second));
                            if (joinsCluster) score -= 45 + Math.Min(clusterContacts, 4) * 6;
                            else score += 20;
                        }

                        if (score < bestScore)
                        {
                            bestScore = score;
                            bestVariant = variant;
                            bestOrigin = origin;
                            bestJoins = joins;
                        }
                        if (++sampled >= 48 || (requiredJoins == 1 && !inClusterPhase && !useExtraLoops && score == 0)) goto ChoosePlacement;
                    }
                }
            ChoosePlacement:
                if (bestVariant != null) return Commit(bestVariant, bestOrigin, bestJoins);
                if (probes < 0) return false;

                bool Commit(Variant variant, Vector2Int origin, IEnumerable<Join> joins)
                {
                    var joinArray = joins as Join[] ?? joins.ToArray();
                    var strip = new Strip(index, slot, template, variant, origin);
                    Add(strip);
                    region.Joins.AddRange(joinArray);
                    if (requiredJoins == 2 || joinArray.Length >= 2) ring = true;
                    if (inClusterPhase && !template.NoLoop && joinArray.Any(j => clusterStrips.Contains(j.Left)))
                    {
                        clusterStrips.Add(index);
                        clusterPlaced++;
                    }
                    return true;
                }
            }
            return false;
        }
    }

    private static Join[] FindFirstValidPair(Join[] joins, Func<int, int, bool> validCyclePair)
    {
        for (int i = 0; i < joins.Length; i++)
        for (int j = i + 1; j < joins.Length; j++)
            if (joins[i].Left != joins[j].Left && (validCyclePair == null || validCyclePair(joins[i].Left, joins[j].Left)))
                return new[] { joins[i], joins[j] };
        return null;
    }

    private static Join PickSingleJoin(Dictionary<int, Join> matches, bool preferCluster, HashSet<int> clusterStrips)
    {
        if (preferCluster)
            foreach (var pair in matches)
                if (clusterStrips.Contains(pair.Key)) return pair.Value;
        return matches.Values.First();
    }

    private static Layout BuildLayout(string mapId, List<Strip> strips, List<Join> joins, SecondFloorPatternLibrary library,
        int baseLevel, float levelPerHop, int maxLevel, Random contentRandom, int spawnSlot = 0)
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
        double totalResource = 0;
        double totalDifficulty = 0;
        foreach (var strip in strips)
        {
            var stripId = $"s{strip.Index}";
            int level = (int)Math.Min(maxLevel, Math.Floor(baseLevel + (double)levelPerHop * distance[strip.Index] + 0.5));
            double stripResource = 0;
            double stripDifficulty = 0;
            foreach (var cell in strip.Variant.Cells)
            {
                string actualType = PickCellType(cell, library, contentRandom);
                var type = library.CellTypes[actualType];
                stripResource += type.Resource;
                stripDifficulty += type.Difficulty;
                int? enemyLevel = type.SupportsOffset
                    ? (int)Math.Min(maxLevel, Math.Max(1L, (long)level + cell.DifficultyOffset)) : (int?)null;
                tiles.Add(new SecondFloorFloorMap.Tile(strip.Origin + cell.Position, actualType, enemyLevel, new[] { stripId }));
            }
            totalResource += strip.Template.ResourceOverride ?? stripResource;
            totalDifficulty += strip.Template.DifficultyOverride ?? stripDifficulty;
            foreach (var edge in strip.Variant.Edges)
                edges.Add((strip.Origin + edge.A, strip.Origin + edge.B, SecondFloorEdgeKind.Open));
        }
        foreach (var join in joins) edges.Add((join.From, join.To, SecondFloorEdgeKind.Door));
        var visualTypes = strips.Select(strip => ($"s{strip.Index}", strip.Template.VisualTypeId));
        return new Layout(SecondFloorFloorMap.FromLayout(mapId, spawn, tiles, edges, visualTypes), strips, joins,
            totalResource, totalDifficulty, spawnSlot);
    }

    private static string PickCellType(SecondFloorPatternLibrary.Cell cell, SecondFloorPatternLibrary library, Random random)
    {
        if (cell.TypePool == null || cell.TypePool.Count < 2)
            return cell.Type;
        double total = 0;
        foreach (var option in cell.TypePool)
            total += option.Weight;
        if (!(total > 0))
            return cell.Type;
        double roll = random.NextDouble() * total;
        foreach (var option in cell.TypePool)
        {
            roll -= option.Weight;
            if (roll < 0)
                return option.Type;
        }
        return cell.TypePool[cell.TypePool.Count - 1].Type;
    }

    private static int Capacity(Template t) => t.InstanceLimit;
    private static int Used(Dictionary<string, int> counts, Template t) => counts.TryGetValue(t.FamilyId, out int count) ? count : 0;
    private static void RecordUse(Dictionary<string, int> counts, Template t) => counts[t.FamilyId] = Used(counts, t) + 1;
    private static IEnumerable<Template> WeightedOrder(IEnumerable<Template> roots, Random random) =>
        roots.OrderBy(t => -Math.Log(1d - random.NextDouble()) / t.Weight);
    private static Template SelectVersion(Template root, Random random) => root.FamilyVersions.Count == 1
        ? root : root.FamilyVersions[random.Next(root.FamilyVersions.Count)];
    private static IReadOnlyList<Variant> PlacementVariants(Template template) => template.PlacementVariants;
}
