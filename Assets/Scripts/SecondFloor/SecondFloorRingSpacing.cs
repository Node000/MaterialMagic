using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Spacing = SecondFloorMapPools.RingSpacing;
using Variant = SecondFloorPatternLibrary.Variant;

internal static partial class SecondFloorPatternGenerator
{
    private static readonly Vector2Int[] PositiveContactDirections = { Vector2Int.right, Vector2Int.up };
    // Boundary segments use the lower coordinate and a positive normal; Along advances along the wall.
    private readonly struct SharedWall
    {
        public readonly int First, Second, Line, Along, Axis;
        public readonly Vector2Int Position;

        public SharedWall(Vector2Int a, Vector2Int b, int first, int second)
        {
            First = Math.Min(first, second);
            Second = Math.Max(first, second);
            Axis = a.x == b.x ? 1 : 0;
            Position = Vector2Int.Min(a, b);
            Line = Axis == 0 ? Position.x : Position.y;
            Along = Axis == 0 ? Position.y : Position.x;
        }

        public bool IsDoor(Join join) => Position == Vector2Int.Min(join.From, join.To) &&
            Axis == (join.From.x == join.To.x ? 1 : 0);
    }

    private static List<SharedWall> PlacementContacts(Variant variant, Vector2Int origin,
        Dictionary<Vector2Int, (Strip Strip, bool IsPort)> owners)
    {
        var contacts = new List<SharedWall>();
        foreach (var cell in variant.Cells)
        {
            var to = origin + cell.Position;
            foreach (var direction in Directions)
            {
                var from = to + direction;
                if (owners.TryGetValue(from, out var host)) contacts.Add(new SharedWall(from, to, host.Strip.Index, -1));
            }
        }
        return contacts;
    }

    private static Join[] ChooseClosure(List<Join> matches, List<SharedWall> contacts, Spacing spacing,
        Func<int, int, bool> isPairValid = null)
    {
        // Prefer opening a long shared wall rather than leaving it intact for slightly more door separation.
        Join[] chosen = null;
        double best = double.PositiveInfinity;
        int probes = 96;
        for (int i = 0; i < matches.Count; i++)
        for (int j = i + 1; j < matches.Count; j++)
        {
            var a = matches[i];
            var b = matches[j];
            if (a.Left == b.Left) continue;
            if (isPairValid != null && !isPairValid(a.Left, b.Left)) continue;
            var pair = new[] { a, b };
            double score = ContactCost(contacts, spacing, pair) - DoorSeparation(a, b, spacing);
            if (score < best)
            {
                best = score;
                chosen = pair;
            }
            if (--probes == 0) return chosen;
        }
        return chosen;
    }

    private static double ClosureBonus(Join[] joins, Spacing spacing) =>
        joins.Length == 2 ? DoorSeparation(joins[0], joins[1], spacing) : 0;

    private static double DoorSeparation(Join a, Join b, Spacing spacing)
    {
        var delta = a.To - b.To;
        int distance = Math.Abs(delta.x) + Math.Abs(delta.y);
        // Treat distance as a visual tie-breaker, not a reason to add a long shared wall.
        return Math.Min(distance, 5) * (spacing == Spacing.Spacious ? 0.65 : 0.35);
    }

    private static double ContactCost(IEnumerable<SharedWall> contacts, Spacing spacing, Join[] doors = null) =>
        ContactCost(contacts, spacing, doors, out _);

    private static double ContactCost(IEnumerable<SharedWall> contacts, Spacing spacing, Join[] doors, out int longest)
    {
        double score = 0;
        longest = 0;
        foreach (var group in contacts.Where(wall => doors == null || !doors.Any(wall.IsDoor))
                     .GroupBy(wall => (wall.First, wall.Second, wall.Axis, wall.Line)))
        {
            int previous = int.MinValue;
            int run = 0;
            foreach (int along in group.Select(wall => wall.Along).OrderBy(value => value))
            {
                run = along == previous + 1 ? run + 1 : 1;
                previous = along;
                longest = Math.Max(longest, run);
                // Adding segments to one uninterrupted wall is worse than isolated contact.
                score += spacing == Spacing.Spacious ? 2 * run - 1 : (run + 1) * 0.5;
            }
        }
        // Score the longest uninterrupted contact separately: a sparse floor can still hide a single bad loop.
        int excess = Math.Max(longest - 1, 0);
        return score + excess * excess * (spacing == Spacing.Spacious ? 16 : 8);
    }

    private static double FloorContactCost(Layout layout, Spacing spacing)
    {
        var owners = new Dictionary<Vector2Int, Strip>();
        foreach (var strip in layout.Strips)
            if (strip.Slot >= 0)
                foreach (var position in strip.Positions) owners.Add(position, strip);

        // Strip graph leaf-pruning isolates the local cycles so weak contacts on those cycles count more.
        var neighbors = layout.Strips.Select(_ => new HashSet<int>()).ToArray();
        foreach (var join in layout.Joins)
        {
            int slot = layout.Strips[join.Left].Slot;
            if (slot < 0 || layout.Strips[join.Right].Slot != slot) continue;
            neighbors[join.Left].Add(join.Right);
            neighbors[join.Right].Add(join.Left);
        }
        var queue = new Queue<int>(Enumerable.Range(0, neighbors.Length).Where(i => neighbors[i].Count == 1));
        while (queue.Count > 0)
        {
            int leaf = queue.Dequeue();
            if (neighbors[leaf].Count != 1) continue;
            int other = neighbors[leaf].First();
            neighbors[leaf].Clear();
            if (neighbors[other].Remove(leaf) && neighbors[other].Count == 1) queue.Enqueue(other);
        }

        var all = new List<SharedWall>();
        var cycle = new List<SharedWall>();
        foreach (var pair in owners)
        foreach (var direction in PositiveContactDirections)
        {
            var to = pair.Key + direction;
            var a = pair.Value;
            if (!owners.TryGetValue(to, out var b) || a.Slot != b.Slot || a.Index == b.Index ||
                layout.Map.CanStep(pair.Key, to)) continue;
            var wall = new SharedWall(pair.Key, to, a.Index, b.Index);
            all.Add(wall);
            if (neighbors[a.Index].Count > 1 && neighbors[b.Index].Count > 1) cycle.Add(wall);
        }
        double cost = ContactCost(all, spacing, null, out int longest);
        double cycleCost = ContactCost(cycle, spacing, null, out int cycleLongest);
        // Prefer fixing the worst glued section first; total contact and the loop core settle closer calls.
        return longest * (spacing == Spacing.Spacious ? 150 : 75) + cycleLongest * 25 +
            cost + cycleCost * (spacing == Spacing.Spacious ? 2 : 1);
    }
}
