using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class SecondFloorWorldPoolsTests
{
    [Test]
    public void ReadsFourTierWorldPoolJsonAndGeneratesByTier()
    {
        var samplePatterns = Resources.Load<TextAsset>("Data/Maps/Patterns/Sample.patterns");
        Assert.That(samplePatterns, Is.Not.Null);

        string json = BuildFourTierJson();
        var pools = SecondFloorMapPools.Read(json);
        Assert.That(pools.Pools.Count, Is.EqualTo(4));

        for (int tier = 1; tier <= 4; tier++)
        {
            var pool = pools.GetPoolForTier(tier);
            Assert.That(pool.Tier, Is.EqualTo(tier));
            Assert.That(pool.Id, Is.EqualTo($"level-{tier}"));

            var generated = pools.GenerateForTier(tier, 100 + tier, samplePatterns);
            Assert.That(generated.Strips.Count(s => s.Slot < 0), Is.EqualTo(3));
            Assert.That(ShortestRegionCycle(generated), Is.GreaterThanOrEqualTo(4));
        }
    }

    [Test]
    public void MinCycleLengthPreventsShortThreeStripCyclesAcrossSeeds()
    {
        var samplePatterns = Resources.Load<TextAsset>("Data/Maps/Patterns/Sample.patterns");
        Assert.That(samplePatterns, Is.Not.Null);

        var doc = JObject.Parse(BuildFourTierJson());
        var entry = (JObject)doc["pools"][0]["entries"][0];
        entry["minCycleLength"] = 4;
        entry["extraLoopChance"] = 0.6;
        entry["localClustering"] = true;

        var pools = SecondFloorMapPools.Read(doc.ToString());
        foreach (int seed in new[] { 11, 23, 37, 53, 71, 89 })
        {
            var generated = pools.Generate("level-1", "tier1_standard", seed, samplePatterns);
            int girth = ShortestRegionCycle(generated);
            Assert.That(girth, Is.GreaterThanOrEqualTo(4), $"Seed {seed} produced short cycle of length {girth}");
        }
    }

    [Test]
    public void SpawnRegionSlotSupportsExplicitSlotAndRandomAcrossRegions()
    {
        var samplePatterns = Resources.Load<TextAsset>("Data/Maps/Patterns/Sample.patterns");
        Assert.That(samplePatterns, Is.Not.Null);

        foreach (var (code, expectedSlot) in new[] { ("A", 0), ("B", 1), ("C", 2) })
        {
            var doc = JObject.Parse(BuildFourTierJson());
            doc["pools"][0]["entries"][0]["spawnRegionSlot"] = code;
            var pools = SecondFloorMapPools.Read(doc.ToString());
            var generated = pools.Generate("level-1", "tier1_standard", 42, samplePatterns);
            Assert.That(generated.SpawnRegionSlot, Is.EqualTo(expectedSlot));
            var spawnStrip = generated.Strips.Single(s =>
                generated.Map.Tiles.Any(t => t.Position == generated.Map.Spawn && t.StripIds.Contains(s.Id)));
            Assert.That(spawnStrip.Slot, Is.EqualTo(expectedSlot));
        }

        var randomDoc = JObject.Parse(BuildFourTierJson());
        randomDoc["pools"][0]["entries"][0]["spawnRegionSlot"] = "random";
        var randomPools = SecondFloorMapPools.Read(randomDoc.ToString());
        var seen = new HashSet<int>();
        for (int seed = 1; seed <= 20; seed++)
        {
            var generated = randomPools.Generate("level-1", "tier1_standard", seed, samplePatterns);
            seen.Add(generated.SpawnRegionSlot);
        }
        Assert.That(seen.Count, Is.GreaterThanOrEqualTo(2));
    }

    private static string BuildFourTierJson()
    {
        var tiers = new JArray();
        for (int tier = 1; tier <= 4; tier++)
        {
            tiers.Add(new JObject
            {
                ["id"] = $"level-{tier}",
                ["name"] = $"等级 {tier}",
                ["tier"] = tier,
                ["entries"] = new JArray
                {
                    new JObject
                    {
                        ["id"] = $"tier{tier}_standard",
                        ["name"] = $"等级 {tier} 标准图",
                        ["weight"] = 1,
                        ["patternLibraryId"] = "Sample",
                        ["ringSpacing"] = "balanced",
                        ["spawnRegionSlot"] = "random",
                        ["minCycleLength"] = 4,
                        ["extraLoopChance"] = 0.4,
                        ["localClustering"] = true,
                        ["regions"] = new JArray
                        {
                            new JObject { ["regionTypeId"] = "city", ["stripMin"] = 6, ["stripMax"] = 6, ["maxNoLoop"] = 1 },
                            new JObject { ["regionTypeId"] = "factory", ["stripMin"] = 6, ["stripMax"] = 6, ["maxNoLoop"] = 1 },
                            new JObject { ["regionTypeId"] = "suburb", ["stripMin"] = 6, ["stripMax"] = 6, ["maxNoLoop"] = 1 },
                        },
                        ["budget"] = new JObject
                        {
                            ["resourceMin"] = null,
                            ["resourceMax"] = null,
                            ["difficultyMin"] = null,
                            ["difficultyMax"] = null,
                        },
                        ["progression"] = new JObject
                        {
                            ["baseLevel"] = tier,
                            ["levelPerHop"] = 0.75,
                            ["maxLevel"] = 20,
                        },
                    }
                }
            });
        }
        return new JObject
        {
            ["kind"] = "plotweave-map-pools",
            ["schemaVersion"] = 1,
            ["pools"] = tiers,
        }.ToString();
    }

    private static int ShortestRegionCycle(SecondFloorMapPools.GeneratedMap generated)
    {
        int best = int.MaxValue;
        foreach (int slot in new[] { 0, 1, 2 })
        {
            var nodes = new HashSet<int>(generated.Strips.Where(s => s.Slot == slot).Select(s => s.Index));
            var adj = nodes.ToDictionary(n => n, _ => new List<int>());
            var edges = new List<(int A, int B)>();
            foreach (var join in generated.Joins)
            {
                if (!nodes.Contains(join.Left) || !nodes.Contains(join.Right)) continue;
                adj[join.Left].Add(join.Right);
                adj[join.Right].Add(join.Left);
                edges.Add((join.Left, join.Right));
            }
            foreach (var (u, v) in edges)
            {
                var dist = new Dictionary<int, int> { [u] = 0 };
                var queue = new Queue<int>();
                queue.Enqueue(u);
                while (queue.Count > 0)
                {
                    int cur = queue.Dequeue();
                    int d = dist[cur];
                    if (d + 1 >= best) break;
                    foreach (int nxt in adj[cur])
                    {
                        if ((cur == u && nxt == v) || (cur == v && nxt == u)) continue;
                        if (nxt == v)
                        {
                            best = Math.Min(best, d + 2);
                            break;
                        }
                        if (!dist.ContainsKey(nxt))
                        {
                            dist[nxt] = d + 1;
                            queue.Enqueue(nxt);
                        }
                    }
                }
            }
        }
        return best;
    }
}
