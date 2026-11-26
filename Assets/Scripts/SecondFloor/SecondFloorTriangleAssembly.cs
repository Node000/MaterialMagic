using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;
using Template = SecondFloorPatternLibrary.Template;
using Variant = SecondFloorPatternLibrary.Variant;

internal static partial class SecondFloorPatternGenerator
{
    private const int TrianglePortsPerRegion = 80;
    private const int TrianglePortsPerSearch = 32;
    private const int TriangleOptionProbesPerPort = 96;
    private const int TriangleOptionsPerPort = 4;
    private const int TriangleBridgePlacements = 40;
    private const int TriangleBOffsetsPerBridge = 3;
    private const int TriangleABLayouts = 60;
    private const int TriangleCPairsPerKey = 10;
    private const int TriangleClosingProbes = 24000;

    private readonly struct TrianglePort
    {
        public readonly int StripIndex;
        public readonly Vector2Int Cell;
        public readonly Vector2Int Direction;
        public Vector2Int Outside => Cell + Direction;

        public TrianglePort(int stripIndex, Vector2Int cell, Vector2Int direction)
        {
            StripIndex = stripIndex;
            Cell = cell;
            Direction = direction;
        }

        public TrianglePort Move(Vector2Int offset) => new TrianglePort(StripIndex, Cell + offset, Direction);
    }

    private sealed class TriangleRegion
    {
        public readonly HashSet<Vector2Int> Occupied = new HashSet<Vector2Int>();
        public readonly HashSet<Vector2Int> Halo;
        public readonly List<TrianglePort> Ports = new List<TrianglePort>();

        public TriangleRegion(Region region, Random random)
        {
            foreach (var strip in region.Strips)
                Occupied.UnionWith(strip.Positions);
            Halo = TriangleHalo(Occupied);

            foreach (var strip in region.Strips)
            {
                if (strip.Template.NoLoop)
                    continue;
                bool hasPortals = false;
                foreach (var cell in strip.Variant.Cells)
                    hasPortals |= cell.IsPortal;
                foreach (var cell in strip.Variant.Cells)
                {
                    if (hasPortals && !cell.IsPortal)
                        continue;
                    var position = strip.Origin + cell.Position;
                    foreach (var direction in Directions)
                        if (!Occupied.Contains(position + direction))
                            Ports.Add(new TrianglePort(strip.Index, position, direction));
                }
            }

            TriangleShuffle(Ports, random);
            if (Ports.Count > TrianglePortsPerRegion)
                Ports.RemoveRange(TrianglePortsPerRegion, Ports.Count - TrianglePortsPerRegion);
        }
    }

    private sealed class TriangleBridgeOption
    {
        public readonly Template Template;
        public readonly Variant Variant;
        public readonly Vector2Int Anchor;
        public readonly List<TrianglePort> Exits;

        public TriangleBridgeOption(Template template, Variant variant, Vector2Int anchor, List<TrianglePort> exits)
        {
            Template = template;
            Variant = variant;
            Anchor = anchor;
            Exits = exits;
        }
    }

    private sealed class TriangleBridge
    {
        public readonly TriangleBridgeOption Option;
        public readonly TrianglePort Host;
        public readonly Vector2Int Origin;
        public readonly HashSet<Vector2Int> Occupied;
        public readonly HashSet<Vector2Int> Halo;
        public readonly List<TrianglePort> Exits;

        public TriangleBridge(TriangleBridgeOption option, TrianglePort host, Vector2Int origin,
            List<TrianglePort> exits)
        {
            Option = option;
            Host = host;
            Origin = origin;
            Exits = exits;
            Occupied = new HashSet<Vector2Int>();
            foreach (var cell in option.Variant.Cells)
                Occupied.Add(origin + cell.Position);
            Halo = TriangleHalo(Occupied);
        }
    }

    private readonly struct TriangleLanding
    {
        public readonly TriangleBridge Bridge;
        public readonly TrianglePort Exit;
        public readonly TrianglePort Target;

        public TriangleLanding(TriangleBridge bridge, TrianglePort exit, TrianglePort target)
        {
            Bridge = bridge;
            Exit = exit;
            Target = target;
        }
    }

    private static bool TryAssemble(Region[] regions, IReadOnlyList<Template> templates,
        Dictionary<string, int> counts, Random random, Random versionRandom, out List<Strip> strips, out List<Join> joins)
    {
        strips = null;
        joins = null;
        var a = new TriangleRegion(regions[0], random);
        var b = new TriangleRegion(regions[1], random);
        var c = new TriangleRegion(regions[2], random);
        if (a.Ports.Count == 0 || b.Ports.Count == 0 || c.Ports.Count < 2)
            return false;

        var options = TriangleBridgeOptions(templates, counts);
        // Existing libraries used an unweighted shuffle of bridge placements. Keep their seeded results.
        bool weightedFamilies = false;
        foreach (var root in templates)
            weightedFamilies |= root.FamilyVersions.Count > 1;
        if (!weightedFamilies)
            TriangleShuffle(options, random);
        var optionsAB = TriangleOptionsForPair(options, regions[0].TypeId, regions[1].TypeId);
        var optionsBC = TriangleOptionsForPair(options, regions[1].TypeId, regions[2].TypeId);
        var optionsCA = TriangleOptionsForPair(options, regions[2].TypeId, regions[0].TypeId);
        if (optionsAB.Count == 0 || optionsBC.Count == 0 || optionsCA.Count == 0)
            return false;

        // Leave A fixed. Translate B onto the first bridge, then use the remaining two bridges to locate C.
        // The local template variants are never rotated as part of moving a region.
        var cPairs = TrianglePortPairs(c.Ports);
        var candidatesAB = TriangleBridgeCandidates(a, Vector2Int.zero, optionsAB, null, Vector2Int.zero, null, random, weightedFamilies);
        int layouts = 0;
        foreach (var ab in candidatesAB)
        {
            int offsetsForBridge = 0;
            var checkedOffsets = new HashSet<Vector2Int>();
            foreach (var exitAB in ab.Exits)
            {
                foreach (var portB in b.Ports)
                {
                    if (portB.Direction != -exitAB.Direction)
                        continue;
                    var bOffset = exitAB.Outside - portB.Cell;
                    if (!checkedOffsets.Add(bOffset) ||
                        !TriangleRegionFits(b, bOffset, a.Halo, null, Vector2Int.zero, ab.Occupied))
                        continue;
                    if (++layouts > TriangleABLayouts)
                        return false;

                    if (TryCloseTriangle(a, b, c, bOffset, ab, optionsBC, optionsCA, cPairs, counts, random, weightedFamilies,
                        out var bc, out var ca, out var cOffset))
                    {
                        strips = new List<Strip>();
                        joins = new List<Join>();
                        TriangleAppendRegion(regions[0], Vector2Int.zero, strips, joins);
                        TriangleAppendRegion(regions[1], bOffset, strips, joins);
                        TriangleAppendRegion(regions[2], cOffset, strips, joins);
                        TriangleAppendBridge(new TriangleLanding(ab, exitAB, portB.Move(bOffset)), strips, joins, versionRandom);
                        TriangleAppendBridge(bc, strips, joins, versionRandom);
                        TriangleAppendBridge(ca, strips, joins, versionRandom);
                        return true;
                    }

                    if (++offsetsForBridge >= TriangleBOffsetsPerBridge)
                        break;
                }
                if (offsetsForBridge >= TriangleBOffsetsPerBridge)
                    break;
            }
        }
        return false;
    }

    private static bool TryCloseTriangle(TriangleRegion a, TriangleRegion b, TriangleRegion c, Vector2Int bOffset,
        TriangleBridge ab, List<TriangleBridgeOption> optionsBC, List<TriangleBridgeOption> optionsCA,
        Dictionary<(Vector2Int, Vector2Int, Vector2Int), List<(TrianglePort, TrianglePort)>> cPairs,
        Dictionary<string, int> counts, Random random, bool weightedFamilies, out TriangleLanding landingBC, out TriangleLanding landingCA,
        out Vector2Int cOffset)
    {
        landingBC = landingCA = default;
        cOffset = default;

        var candidatesCA = TriangleBridgeCandidates(a, Vector2Int.zero, optionsCA, b.Halo, bOffset, ab.Halo, random, weightedFamilies);
        var candidatesBC = TriangleBridgeCandidates(b, bOffset, optionsBC, a.Halo, Vector2Int.zero, ab.Halo, random, weightedFamilies);
        int probes = 0;
        foreach (var ca in candidatesCA)
        {
            foreach (var bc in candidatesBC)
            {
                if (!TriangleBridgeCapacity(ab.Option.Template, bc.Option.Template, ca.Option.Template, counts) ||
                    ca.Halo.Overlaps(bc.Occupied))
                    continue;

                foreach (var exitCA in ca.Exits)
                {
                    if (bc.Occupied.Contains(exitCA.Outside))
                        continue;
                    foreach (var exitBC in bc.Exits)
                    {
                        if (++probes > TriangleClosingProbes)
                            return false;
                        if (ca.Occupied.Contains(exitBC.Outside))
                            continue;
                        var key = (exitBC.Outside - exitCA.Outside, -exitCA.Direction, -exitBC.Direction);
                        if (!cPairs.TryGetValue(key, out var pairs))
                            continue;

                        // The matching displacement and facing directions fix C's translation and both doors.
                        foreach (var pair in pairs)
                        {
                            var offset = exitCA.Outside - pair.Item1.Cell;
                            if (!TriangleRegionFits(c, offset, a.Halo, b.Halo, bOffset, ab.Halo, bc.Occupied, ca.Occupied))
                                continue;
                            landingBC = new TriangleLanding(bc, exitBC, pair.Item2.Move(offset));
                            landingCA = new TriangleLanding(ca, exitCA, pair.Item1.Move(offset));
                            cOffset = offset;
                            return true;
                        }
                    }
                }
            }
        }
        return false;
    }

    private static List<TriangleBridgeOption> TriangleBridgeOptions(IReadOnlyList<Template> templates,
        Dictionary<string, int> counts)
    {
        var options = new List<TriangleBridgeOption>();
        foreach (var template in templates)
        {
            if (template.Role != "bridge" || !TriangleHasCapacity(template, 1, counts))
                continue;
            foreach (var variant in template.Variants)
            {
                var occupied = new HashSet<Vector2Int>();
                var portals = new List<Vector2Int>();
                var degree = new Dictionary<Vector2Int, int>();
                foreach (var cell in variant.Cells)
                {
                    occupied.Add(cell.Position);
                    degree.Add(cell.Position, 0);
                    if (cell.IsPortal)
                        portals.Add(cell.Position);
                }
                if (portals.Count == 0)
                {
                    foreach (var edge in variant.Edges)
                    {
                        ++degree[edge.A];
                        ++degree[edge.B];
                    }
                    foreach (var cell in variant.Cells)
                        if (degree[cell.Position] == 1)
                            portals.Add(cell.Position);
                }
                // If a bridge marks any portals they are mandatory; do not add an unmarked fallback.
                if (portals.Count < 2)
                    continue;

                foreach (var anchor in portals)
                {
                    var exits = new List<TrianglePort>();
                    foreach (var exit in portals)
                    {
                        if (exit == anchor)
                            continue;
                        foreach (var direction in Directions)
                            if (!occupied.Contains(exit + direction))
                                exits.Add(new TrianglePort(-1, exit, direction));
                    }
                    if (exits.Count != 0)
                        options.Add(new TriangleBridgeOption(template, variant, anchor, exits));
                }
            }
        }
        return options;
    }

    private static List<TriangleBridgeOption> TriangleOptionsForPair(List<TriangleBridgeOption> options,
        string first, string second)
    {
        var matching = new List<TriangleBridgeOption>();
        foreach (var option in options)
            if (option.Template.MatchesRegion(first) && option.Template.MatchesRegion(second))
                matching.Add(option);
        return matching;
    }

    private static List<TriangleBridge> TriangleBridgeCandidates(TriangleRegion host, Vector2Int hostOffset,
        List<TriangleBridgeOption> options, HashSet<Vector2Int> thirdRegionHalo, Vector2Int thirdRegionOffset,
        HashSet<Vector2Int> otherBridgeHalo, Random random, bool weightedFamilies)
    {
        var result = new List<TriangleBridge>();
        var optionOrder = new List<TriangleBridgeOption>(options);
        var byRoot = new Dictionary<Template, List<TriangleBridgeOption>>();
        var roots = new List<Template>();
        if (weightedFamilies)
            foreach (var option in options)
            {
                if (!byRoot.TryGetValue(option.Template, out var familyOptions))
                {
                    byRoot.Add(option.Template, familyOptions = new List<TriangleBridgeOption>());
                    roots.Add(option.Template);
                }
                familyOptions.Add(option);
            }
        for (int p = 0; p < host.Ports.Count && p < TrianglePortsPerSearch; p++)
        {
            var port = host.Ports[p].Move(hostOffset);
            if (TriangleBridgeBlocked(port.Outside, host, hostOffset, thirdRegionHalo, thirdRegionOffset, otherBridgeHalo))
                continue;

            if (weightedFamilies)
            {
                optionOrder.Clear();
                var rootOrder = new List<Template>(WeightedOrder(roots, random));
                foreach (var root in rootOrder)
                    TriangleShuffle(byRoot[root], random);
                // Give every family its weighted turn before considering extra rotations/anchors of any family.
                for (int rotationOrAnchor = 0; optionOrder.Count < options.Count; rotationOrAnchor++)
                    foreach (var root in rootOrder)
                        if (rotationOrAnchor < byRoot[root].Count)
                            optionOrder.Add(byRoot[root][rotationOrAnchor]);
            }
            else TriangleShuffle(optionOrder, random);
            int placedForPort = 0;
            for (int i = 0; i < optionOrder.Count && i < TriangleOptionProbesPerPort; i++)
            {
                var option = optionOrder[i];
                var origin = port.Outside - option.Anchor;
                bool fits = true;
                foreach (var cell in option.Variant.Cells)
                {
                    if (!TriangleBridgeBlocked(origin + cell.Position, host, hostOffset,
                        thirdRegionHalo, thirdRegionOffset, otherBridgeHalo))
                        continue;
                    fits = false;
                    break;
                }
                if (!fits)
                    continue;

                var exits = new List<TrianglePort>();
                foreach (var exit in option.Exits)
                {
                    var worldExit = exit.Move(origin);
                    if (!host.Halo.Contains(worldExit.Outside - hostOffset) &&
                        !TriangleBridgeBlocked(worldExit.Outside, host, hostOffset,
                            thirdRegionHalo, thirdRegionOffset, otherBridgeHalo))
                        exits.Add(worldExit);
                }
                if (exits.Count == 0)
                    continue;

                TriangleShuffle(exits, random);
                result.Add(new TriangleBridge(option, port, origin, exits));
                if (result.Count >= TriangleBridgePlacements)
                    return result;
                if (++placedForPort >= TriangleOptionsPerPort)
                    break;
            }
        }
        return result;
    }

    private static bool TriangleBridgeBlocked(Vector2Int position, TriangleRegion host, Vector2Int hostOffset,
        HashSet<Vector2Int> thirdRegionHalo, Vector2Int thirdRegionOffset, HashSet<Vector2Int> otherBridgeHalo) =>
        host.Occupied.Contains(position - hostOffset) ||
        (thirdRegionHalo != null && thirdRegionHalo.Contains(position - thirdRegionOffset)) ||
        (otherBridgeHalo != null && otherBridgeHalo.Contains(position));

    private static Dictionary<(Vector2Int, Vector2Int, Vector2Int), List<(TrianglePort, TrianglePort)>>
        TrianglePortPairs(List<TrianglePort> ports)
    {
        var pairs = new Dictionary<(Vector2Int, Vector2Int, Vector2Int), List<(TrianglePort, TrianglePort)>>();
        for (int i = 0; i < ports.Count; i++)
        {
            for (int j = 0; j < ports.Count; j++)
            {
                if (i == j)
                    continue;
                var a = ports[i];
                var b = ports[j];
                var key = (b.Cell - a.Cell, a.Direction, b.Direction);
                if (!pairs.TryGetValue(key, out var list))
                {
                    list = new List<(TrianglePort, TrianglePort)>();
                    pairs.Add(key, list);
                }
                if (list.Count < TriangleCPairsPerKey)
                    list.Add((a, b));
            }
        }
        return pairs;
    }

    // Padded sets forbid both overlap and wall contact. Plain occupied sets allow wall contact with
    // an endpoint region, but its two bridge doors are added explicitly rather than inferred from adjacency.
    private static bool TriangleRegionFits(TriangleRegion region, Vector2Int offset, HashSet<Vector2Int> aHalo,
        HashSet<Vector2Int> bHalo, Vector2Int bOffset, HashSet<Vector2Int> blocked,
        HashSet<Vector2Int> bridgeBC = null, HashSet<Vector2Int> bridgeCA = null)
    {
        foreach (var cell in region.Occupied)
        {
            var position = cell + offset;
            if (aHalo.Contains(position) || blocked.Contains(position) ||
                (bHalo != null && bHalo.Contains(position - bOffset)) ||
                (bridgeBC != null && bridgeBC.Contains(position)) ||
                (bridgeCA != null && bridgeCA.Contains(position)))
                return false;
        }
        return true;
    }

    private static HashSet<Vector2Int> TriangleHalo(HashSet<Vector2Int> cells)
    {
        var halo = new HashSet<Vector2Int>(cells);
        foreach (var cell in cells)
            foreach (var direction in Directions)
                halo.Add(cell + direction);
        return halo;
    }

    private static bool TriangleHasCapacity(Template template, int requested, Dictionary<string, int> counts)
    {
        return requested <= (long)Capacity(template) - Used(counts, template);
    }

    private static bool TriangleBridgeCapacity(Template ab, Template bc, Template ca, Dictionary<string, int> counts)
    {
        string familyAB = ab.FamilyId, familyBC = bc.FamilyId, familyCA = ca.FamilyId;
        int abUses = 1 + (familyAB == familyBC ? 1 : 0) + (familyAB == familyCA ? 1 : 0);
        // A distinct CA family's single-use capacity was already checked when collecting options.
        return TriangleHasCapacity(ab, abUses, counts) &&
            (familyBC == familyAB || TriangleHasCapacity(bc, familyBC == familyCA ? 2 : 1, counts));
    }

    private static void TriangleAppendRegion(Region region, Vector2Int offset, List<Strip> strips, List<Join> joins)
    {
        foreach (var strip in region.Strips)
            strips.Add(strip.Move(offset));
        foreach (var join in region.Joins)
            joins.Add(join.Move(offset));
    }

    private static void TriangleAppendBridge(TriangleLanding landing, List<Strip> strips, List<Join> joins, Random versionRandom)
    {
        var bridge = landing.Bridge;
        var template = SelectVersion(bridge.Option.Template, versionRandom);
        var variant = bridge.Option.Variant;
        if (template != bridge.Option.Template)
            foreach (var placement in PlacementVariants(template))
                if (placement.QuarterTurns == variant.QuarterTurns)
                {
                    variant = placement;
                    break;
                }
        int index = strips.Count;
        strips.Add(new Strip(index, -1, template, variant, bridge.Origin));
        joins.Add(new Join(bridge.Host.StripIndex, index, bridge.Host.Cell, bridge.Host.Outside));
        joins.Add(new Join(index, landing.Target.StripIndex, landing.Exit.Cell, landing.Target.Cell));
    }

    private static void TriangleShuffle<T>(List<T> items, Random random)
    {
        for (int i = items.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            T value = items[i];
            items[i] = items[j];
            items[j] = value;
        }
    }
}
