using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class SecondFloorRingSpacingTests
{
    [Test]
    public void DefaultAndExplicitLegacyProduceTheSamePhysicalMap()
    {
        var original = Resources.Load<TextAsset>(SecondFloorMapPools.ResourcePath);
        Assert.That(original, Is.Not.Null);
        var legacy = AddMode(original.text, "legacy");
        Assert.That(legacy.GetEntry("level-1", "standard-triangle").Spacing, Is.EqualTo(SecondFloorMapPools.RingSpacing.Legacy));
        var implicitMode = SecondFloorMapPools.Read(original.text);
        foreach (int seed in new[] { 5, 37 })
            Assert.That(Signature(legacy.Generate("level-1", "standard-triangle", seed)),
                Is.EqualTo(Signature(implicitMode.Generate("level-1", "standard-triangle", seed))));
        Assert.Throws<FormatException>(() => AddMode(original.text, "unknown"));
    }

    [Test]
    public void SampleSpacingModesAreDeterministicAndReduceSharedWallsAcrossSeeds()
    {
        var original = Resources.Load<TextAsset>(SecondFloorMapPools.ResourcePath);
        Assert.That(original, Is.Not.Null);
        var modes = new[] { "legacy", "balanced", "spacious" };
        var pools = modes.Select(mode => AddMode(original.text, mode)).ToArray();
        var lengths = new int[modes.Length];
        var wallTotals = new int[modes.Length];
        var elapsed = new long[modes.Length];
        var seeds = new[] { 1, 2, 5, 17, 37, 103, 419, 701 };
        for (int m = 0; m < modes.Length; m++)
        {
            var timer = Stopwatch.StartNew();
            foreach (int seed in seeds)
            {
                var layout = pools[m].Generate("level-1", "standard-triangle", seed);
                Assert.That(layout.Strips.Count(s => s.Slot < 0), Is.EqualTo(3));
                foreach (int slot in new[] { 0, 1, 2 })
                {
                    var ids = new HashSet<int>(layout.Strips.Where(s => s.Slot == slot).Select(s => s.Index));
                    Assert.That(layout.Joins.Count(j => ids.Contains(j.Left) && ids.Contains(j.Right)), Is.EqualTo(ids.Count));
                }
                var stats = WallStats(layout.Map);
                lengths[m] += stats.Longest;
                wallTotals[m] += stats.Count;
                if (seed == 5)
                    Assert.That(Signature(layout), Is.EqualTo(Signature(pools[m].Generate("level-1", "standard-triangle", seed))));
            }
            elapsed[m] = timer.ElapsedMilliseconds;
            TestContext.WriteLine($"{modes[m]}: {seeds.Length}/{seeds.Length} generated, sum of longest wall {lengths[m]}, shared wall segments {wallTotals[m]}, {elapsed[m]} ms (includes repeat for seed 5)");
        }
        Assert.That(wallTotals[1], Is.LessThan(wallTotals[0]));
        Assert.That(wallTotals[2], Is.LessThan(wallTotals[0]));
        Assert.That(lengths[2], Is.LessThan(lengths[0]));
    }

    private static SecondFloorMapPools AddMode(string text, string mode)
    {
        var config = JObject.Parse(text);
        config["pools"][0]["entries"][0]["ringSpacing"] = mode;
        return SecondFloorMapPools.Read(config.ToString());
    }

    private static string Signature(SecondFloorMapPools.GeneratedMap map) => string.Join(";",
        map.Strips.Select(s => $"{s.TemplateId}:{s.Origin}:{s.QuarterTurns}")
            .Concat(map.Joins.Select(j => $"{j.Left}->{j.Right}:{j.From}:{j.To}")));

    private static (int Longest, int Count) WallStats(SecondFloorFloorMap map)
    {
        var tiles = map.Tiles.ToDictionary(t => t.Position);
        var contacts = new Dictionary<(string, string, int, int), List<int>>();
        foreach (var tile in tiles.Values)
        foreach (var offset in new[] { Vector2Int.right, Vector2Int.up })
        {
            var to = tile.Position + offset;
            if (!tiles.TryGetValue(to, out var other) || tile.StripIds[0] == other.StripIds[0] || map.CanStep(tile.Position, to)) continue;
            string a = tile.StripIds[0], b = other.StripIds[0];
            if (string.CompareOrdinal(a, b) > 0) (a, b) = (b, a);
            var key = (a, b, offset.x, offset.x == 1 ? tile.Position.x : tile.Position.y);
            if (!contacts.TryGetValue(key, out var line)) contacts.Add(key, line = new List<int>());
            line.Add(offset.x == 1 ? tile.Position.y : tile.Position.x);
        }
        int longest = 0;
        foreach (var line in contacts.Values)
        {
            line.Sort();
            int run = 0, previous = int.MinValue;
            foreach (int at in line)
            {
                run = at == previous + 1 ? run + 1 : 1;
                longest = Math.Max(longest, run);
                previous = at;
            }
        }
        return (longest, contacts.Values.Sum(line => line.Count));
    }
}
