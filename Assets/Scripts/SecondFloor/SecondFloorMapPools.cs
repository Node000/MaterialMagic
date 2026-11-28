using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Random = System.Random;

/// <summary>Versioned PlotWeave recipes shared by the draft preview and the runtime floor generator.</summary>
public sealed class SecondFloorMapPools
{
    public const string ResourcePath = "Data/Maps/SecondFloor.map-pools";
    private const string PatternDirectory = "Data/Maps/Patterns/";
    private const string PatternSuffix = ".patterns";
    private static readonly Regex IdPattern = new Regex(@"\A[A-Za-z0-9_-]{1,64}\z", RegexOptions.CultureInvariant);
    private static readonly Regex HashPattern = new Regex(@"\A[0-9a-f]{64}\z", RegexOptions.CultureInvariant);

    public sealed class Pool
    {
        private readonly Dictionary<string, Entry> entriesById;
        public string Id { get; }
        public string Name { get; }
        public int Tier { get; }
        public IReadOnlyList<Entry> Entries { get; }

        internal Pool(string id, string name, List<Entry> entries, int tier = 0)
        {
            Id = id;
            Name = name;
            Tier = tier;
            Entries = entries.AsReadOnly();
            entriesById = entries.ToDictionary(entry => entry.Id, StringComparer.Ordinal);
        }

        public Entry GetEntry(string entryId)
        {
            if (entryId != null && entriesById.TryGetValue(entryId, out var entry)) return entry;
            throw new ArgumentException($"Map pool '{Id}' has no entry '{entryId}'.", nameof(entryId));
        }
    }

    public enum RingSpacing { Legacy, Balanced, Spacious }

    public sealed class Entry
    {
        public string Id { get; }
        public string Name { get; }
        public double Weight { get; }
        public string PatternLibraryId { get; }
        /// <summary>Optional SHA-256 of the exact published pattern bytes. Unpublished drafts may omit it.</summary>
        public string PatternSha256 { get; }
        public IReadOnlyList<RegionSlot> Regions { get; }
        public BudgetTargets Budget { get; }
        public LevelProgression Progression { get; }
        public RingSpacing Spacing { get; }
        /// <summary>Null means random across eligible region slots; 0/1/2 pins spawn to A/B/C.</summary>
        public int? SpawnRegionSlot { get; }
        public int MinCycleLength { get; }
        public double ExtraLoopChance { get; }
        public bool LocalClustering { get; }

        internal Entry(string id, string name, double weight, string patternLibraryId, string patternSha256,
            RegionSlot[] regions, BudgetTargets budget, LevelProgression progression, RingSpacing spacing,
            int? spawnRegionSlot = 0, int minCycleLength = 3, double extraLoopChance = 0d, bool localClustering = false)
        {
            Id = id;
            Name = name;
            Weight = weight;
            PatternLibraryId = patternLibraryId;
            PatternSha256 = patternSha256;
            Regions = Array.AsReadOnly(regions);
            Budget = budget;
            Progression = progression;
            Spacing = spacing;
            SpawnRegionSlot = spawnRegionSlot;
            MinCycleLength = Math.Max(3, minCycleLength);
            ExtraLoopChance = Math.Max(0d, Math.Min(1d, extraLoopChance));
            LocalClustering = localClustering;
        }
    }

    public readonly struct RegionSlot
    {
        public string RegionTypeId { get; }
        public int StripMin { get; }
        public int StripMax { get; }
        public int MaxNoLoop { get; }

        internal RegionSlot(string regionTypeId, int stripMin, int stripMax, int maxNoLoop)
        {
            RegionTypeId = regionTypeId;
            StripMin = stripMin;
            StripMax = stripMax;
            MaxNoLoop = maxNoLoop;
        }
    }

    public readonly struct BudgetTargets
    {
        public double? ResourceMin { get; }
        public double? ResourceMax { get; }
        public double? DifficultyMin { get; }
        public double? DifficultyMax { get; }
        internal bool HasBounds => ResourceMin.HasValue || ResourceMax.HasValue || DifficultyMin.HasValue || DifficultyMax.HasValue;

        internal BudgetTargets(double? resourceMin, double? resourceMax, double? difficultyMin, double? difficultyMax)
        {
            ResourceMin = resourceMin;
            ResourceMax = resourceMax;
            DifficultyMin = difficultyMin;
            DifficultyMax = difficultyMax;
        }

        public bool ContainsResource(double resource) => Distance(resource, ResourceMin, ResourceMax) == 0;
        public bool ContainsDifficulty(double difficulty) => Distance(difficulty, DifficultyMin, DifficultyMax) == 0;

        internal double GetDistance(double resource, double difficulty) =>
            Distance(resource, ResourceMin, ResourceMax) + Distance(difficulty, DifficultyMin, DifficultyMax);

        private static double Distance(double value, double? min, double? max)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return double.PositiveInfinity;
            // Normalize by the target's scale and ignore only floating-point summation noise near an exact bound.
            const double relativeTolerance = 1e-14;
            if (min.HasValue && value < min.Value && (min.Value - value) / min.Value > relativeTolerance)
                return (min.Value - value) / Math.Max(1d, min.Value);
            if (max.HasValue && value > max.Value && (max.Value == 0 || (value - max.Value) / max.Value > relativeTolerance))
                return (value - max.Value) / Math.Max(1d, max.Value);
            return 0d;
        }
    }

    public readonly struct LevelProgression
    {
        public int BaseLevel { get; }
        public float LevelPerHop { get; }
        public int MaxLevel { get; }

        internal LevelProgression(int baseLevel, float levelPerHop, int maxLevel)
        {
            BaseLevel = baseLevel;
            LevelPerHop = levelPerHop;
            MaxLevel = maxLevel;
        }
    }

    public sealed class GeneratedMap
    {
        public SecondFloorFloorMap Map { get; }
        public string PoolId { get; }
        public string EntryId { get; }
        public string PatternLibraryId { get; }
        public int Seed { get; }
        public int SpawnRegionSlot { get; }
        public double Resource { get; }
        public double Difficulty { get; }
        public BudgetTargets Budget { get; }
        public bool ResourceWithinBudget => Budget.ContainsResource(Resource);
        public bool DifficultyWithinBudget => Budget.ContainsDifficulty(Difficulty);
        public bool WithinBudget => ResourceWithinBudget && DifficultyWithinBudget;
        /// <summary>The actual number of strips in each ordered region slot, excluding bridges.</summary>
        public IReadOnlyList<int> StripCounts { get; }
        public IReadOnlyList<GeneratedStrip> Strips { get; }
        public IReadOnlyList<GeneratedJoin> Joins { get; }

        internal GeneratedMap(string poolId, Entry entry, int seed, int[] stripCounts, SecondFloorPatternGenerator.Layout layout)
        {
            Map = layout.Map;
            PoolId = poolId;
            EntryId = entry.Id;
            PatternLibraryId = entry.PatternLibraryId;
            Seed = seed;
            SpawnRegionSlot = layout.SpawnRegionSlot;
            Resource = layout.Resource;
            Difficulty = layout.Difficulty;
            Budget = entry.Budget;
            StripCounts = Array.AsReadOnly(stripCounts);
            Strips = Array.AsReadOnly(layout.Strips.Select(strip => new GeneratedStrip(strip)).ToArray());
            Joins = Array.AsReadOnly(layout.Joins.Select(join => new GeneratedJoin(join)).ToArray());
        }
    }

    public readonly struct GeneratedStrip
    {
        public int Index { get; }
        public string Id => "s" + Index.ToString(CultureInfo.InvariantCulture);
        /// <summary>0/1/2 for A/B/C, or -1 for a bridge.</summary>
        public int Slot { get; }
        public string TemplateId { get; }
        /// <summary>Origin in Unity X/Z grid coordinates.</summary>
        public Vector2Int Origin { get; }
        public int QuarterTurns { get; }

        internal GeneratedStrip(SecondFloorPatternGenerator.Strip strip)
        {
            Index = strip.Index;
            Slot = strip.Slot;
            TemplateId = strip.Template.Id;
            Origin = strip.Origin;
            QuarterTurns = strip.Variant.QuarterTurns;
        }
    }

    public readonly struct GeneratedJoin
    {
        public int Left { get; }
        public int Right { get; }
        public Vector2Int From { get; }
        public Vector2Int To { get; }

        internal GeneratedJoin(SecondFloorPatternGenerator.Join join)
        {
            Left = join.Left;
            Right = join.Right;
            From = join.From;
            To = join.To;
        }
    }

    private readonly Dictionary<string, Pool> poolsById;
    public IReadOnlyList<Pool> Pools { get; }

    private SecondFloorMapPools(List<Pool> pools)
    {
        Pools = pools.AsReadOnly();
        poolsById = pools.ToDictionary(pool => pool.Id, StringComparer.Ordinal);
    }

    public static SecondFloorMapPools Read(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) throw new FormatException("Map pool document is empty.");
        try
        {
            using (var text = new StringReader(json))
            using (var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None })
            {
                var root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (reader.Read()) throw new FormatException("Unexpected content after the map pool document.");
                return ReadDocument(root);
            }
        }
        catch (JsonException ex)
        {
            throw new FormatException($"Invalid map pool JSON: {ex.Message}", ex);
        }
        catch (ArithmeticException ex)
        {
            throw new FormatException("Map pool contains a number outside the supported range.", ex);
        }
    }

    public Pool GetPool(string poolId)
    {
        if (poolId != null && poolsById.TryGetValue(poolId, out var pool)) return pool;
        throw new ArgumentException($"Unknown map pool '{poolId}'.", nameof(poolId));
    }

    /// <summary>Resolve a tier pool (1..4) by explicit tier field, 'level-{tier}' id, or ordered index.</summary>
    public Pool GetPoolForTier(int tier)
    {
        if (tier < 1) throw new ArgumentOutOfRangeException(nameof(tier), "Tier must be >= 1.");
        var byTier = Pools.FirstOrDefault(p => p.Tier == tier);
        if (byTier != null) return byTier;
        if (poolsById.TryGetValue($"level-{tier.ToString(CultureInfo.InvariantCulture)}", out var byId)) return byId;
        if (tier <= Pools.Count) return Pools[tier - 1];
        throw new ArgumentException($"No map pool found for tier {tier}.", nameof(tier));
    }

    public GeneratedMap GenerateForTier(int tier, int seed)
    {
        var pool = GetPoolForTier(tier);
        return Generate(pool.Id, seed);
    }

    public GeneratedMap GenerateForTier(int tier, int seed, TextAsset patterns)
    {
        var pool = GetPoolForTier(tier);
        return Generate(pool.Id, SelectEntry(pool.Id, seed).Id, seed, patterns);
    }

    public Entry GetEntry(string poolId, string entryId) => GetPool(poolId).GetEntry(entryId);

    /// <summary>Deterministic weighted choice among the ordered entries of the requested pool.</summary>
    public Entry SelectEntry(string poolId, int seed)
    {
        var entries = GetPool(poolId).Entries;
        // Scale before summing so all finite positive weights are accepted, even near double.MaxValue.
        double maxWeight = entries.Max(entry => entry.Weight);
        double total = entries.Sum(entry => entry.Weight / maxWeight);
        double target = new Random(seed ^ 0x504F4F4C).NextDouble() * total;
        double cumulative = 0;
        foreach (var entry in entries)
        {
            cumulative += entry.Weight / maxWeight;
            if (target < cumulative) return entry;
        }
        return entries[entries.Count - 1];
    }

    /// <summary>Game convenience overload: select a recipe by weight, then resolve its declared pattern resource.</summary>
    public GeneratedMap Generate(string poolId, int seed) => Generate(poolId, SelectEntry(poolId, seed).Id, seed);

    public GeneratedMap Generate(string poolId, string entryId, int seed)
    {
        var entry = GetEntry(poolId, entryId);
        string path = PatternDirectory + entry.PatternLibraryId + PatternSuffix;
        var patterns = Resources.Load<TextAsset>(path);
        if (!patterns) throw new InvalidOperationException($"Map pool '{poolId}' entry '{entryId}' requires pattern resource '{path}'.");
        return Generate(poolId, entry, seed, patterns);
    }

    /// <summary>Explicit recipe and pattern input for both draft previews and an Inspector-bound runtime preview.</summary>
    public GeneratedMap Generate(string poolId, string entryId, int seed, TextAsset patterns) =>
        Generate(poolId, GetEntry(poolId, entryId), seed, patterns);

    private static GeneratedMap Generate(string poolId, Entry entry, int seed, TextAsset patterns)
    {
        string expectedName = entry.PatternLibraryId + PatternSuffix;
        if (!patterns || !string.Equals(patterns.name, expectedName, StringComparison.Ordinal))
            throw new InvalidOperationException($"Map pool '{poolId}' entry '{entry.Id}' requires '{expectedName}.json'.");
        if (entry.PatternSha256 != null)
        {
            using (var sha = SHA256.Create())
            {
                string actual = BitConverter.ToString(sha.ComputeHash(patterns.bytes)).Replace("-", "").ToLowerInvariant();
                if (!string.Equals(actual, entry.PatternSha256, StringComparison.Ordinal))
                    throw new InvalidOperationException($"Pattern '{expectedName}.json' does not match the published SHA-256 for map pool '{poolId}' entry '{entry.Id}'.");
            }
        }

        var library = SecondFloorPatternLibrary.Read(patterns);
        var random = new Random(seed ^ 0x534C4F54);
        var slots = new SecondFloorPatternGenerator.SlotSettings[3];
        var counts = new int[slots.Length];
        for (int i = 0; i < slots.Length; i++)
        {
            var region = entry.Regions[i];
            counts[i] = random.Next(region.StripMin, region.StripMax + 1);
            slots[i] = new SecondFloorPatternGenerator.SlotSettings(region.RegionTypeId, counts[i], region.MaxNoLoop);
        }
        var progression = entry.Progression;
        var budget = entry.Budget;
        Func<SecondFloorPatternGenerator.Layout, double> targetDistance = null;
        if (budget.HasBounds) targetDistance = layout => budget.GetDistance(layout.Resource, layout.Difficulty);
        string mapId = $"map-pool/{poolId}/{entry.Id}/{seed.ToString(CultureInfo.InvariantCulture)}";
        var topology = new SecondFloorPatternGenerator.TopologyOptions(
            entry.SpawnRegionSlot,
            entry.MinCycleLength,
            entry.ExtraLoopChance,
            entry.LocalClustering);
        var result = SecondFloorPatternGenerator.Generate(library, slots, seed, progression.BaseLevel,
            progression.LevelPerHop, progression.MaxLevel, targetDistance, mapId, entry.Spacing, topology);
        if (double.IsInfinity(result.Resource) || double.IsNaN(result.Resource) ||
            double.IsInfinity(result.Difficulty) || double.IsNaN(result.Difficulty))
            throw new InvalidOperationException($"Map pool '{poolId}' entry '{entry.Id}' produced non-finite metrics. Check pattern weights and metrics.");
        return new GeneratedMap(poolId, entry, seed, counts, result);
    }

    private static SecondFloorMapPools ReadDocument(JObject root)
    {
        CheckFields(root, "kind", "schemaVersion", "pools");
        if (Text(root, "kind") != "plotweave-map-pools") throw Invalid(root, "kind must be 'plotweave-map-pools'.");
        Integer(root, "schemaVersion", 1, 1);
        var rawPools = ArrayField(root, "pools", 1, 64);
        var pools = new List<Pool>(rawPools.Count);
        var poolIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rawPool in rawPools)
        {
            var pool = Object(rawPool);
            CheckFields(pool, "id", "name", "tier", "entries");
            string id = Id(pool, "id");
            if (!poolIds.Add(id)) throw Invalid(pool, $"duplicate pool ID '{id}'.");
            int tier = pool.Property("tier") == null ? 0 : Integer(pool, "tier", 1, 64);
            var rawEntries = ArrayField(pool, "entries", 1, 64);
            var entries = new List<Entry>(rawEntries.Count);
            var entryIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var rawEntry in rawEntries)
            {
                var entry = ReadEntry(Object(rawEntry));
                if (!entryIds.Add(entry.Id)) throw Invalid(rawEntry, $"duplicate entry ID '{entry.Id}' in pool '{id}'.");
                entries.Add(entry);
            }
            pools.Add(new Pool(id, Text(pool, "name"), entries, tier));
        }
        return new SecondFloorMapPools(pools);
    }

    private static Entry ReadEntry(JObject entry)
    {
        CheckFields(entry, "id", "name", "weight", "patternLibraryId", "patternSha256", "regions", "budget", "progression",
            "ringSpacing", "spawnRegionSlot", "minCycleLength", "extraLoopChance", "localClustering", "topology");
        string id = Id(entry, "id");
        string patternId = Id(entry, "patternLibraryId");
        string hash = null;
        if (entry.Property("patternSha256") != null)
        {
            hash = Text(entry, "patternSha256");
            if (!HashPattern.IsMatch(hash)) throw Invalid(entry["patternSha256"], "expected 64 lowercase hexadecimal digits.");
        }

        var regions = new RegionSlot[3];
        var rawRegions = ArrayField(entry, "regions", regions.Length, regions.Length);
        for (int i = 0; i < regions.Length; i++)
        {
            var region = Object(rawRegions[i]);
            CheckFields(region, "regionTypeId", "stripMin", "stripMax", "maxNoLoop");
            int min = Integer(region, "stripMin", 3, 20);
            regions[i] = new RegionSlot(Id(region, "regionTypeId"), min, Integer(region, "stripMax", min, 20),
                Integer(region, "maxNoLoop", 0, 20));
        }

        var budget = ObjectField(entry, "budget");
        CheckFields(budget, "resourceMin", "resourceMax", "difficultyMin", "difficultyMax");
        double? resourceMin = Bound(budget, "resourceMin"), resourceMax = Bound(budget, "resourceMax");
        double? difficultyMin = Bound(budget, "difficultyMin"), difficultyMax = Bound(budget, "difficultyMax");
        if (resourceMin.HasValue && resourceMax.HasValue && resourceMin.Value > resourceMax.Value)
            throw Invalid(budget, "resourceMin must not exceed resourceMax.");
        if (difficultyMin.HasValue && difficultyMax.HasValue && difficultyMin.Value > difficultyMax.Value)
            throw Invalid(budget, "difficultyMin must not exceed difficultyMax.");

        var progression = ObjectField(entry, "progression");
        CheckFields(progression, "baseLevel", "levelPerHop", "maxLevel");
        int baseLevel = Integer(progression, "baseLevel", 1, int.MaxValue);
        double levelPerHop = Number(progression, "levelPerHop", 0);
        if (levelPerHop > 3.4028234e38) throw Invalid(progression["levelPerHop"], "value exceeds the supported range.");
        int maxLevel = Integer(progression, "maxLevel", baseLevel, int.MaxValue);
        var spacing = entry.Property("ringSpacing") == null ? RingSpacing.Legacy : Text(entry, "ringSpacing") switch
        {
            "legacy" => RingSpacing.Legacy,
            "balanced" => RingSpacing.Balanced,
            "spacious" => RingSpacing.Spacious,
            _ => throw Invalid(entry["ringSpacing"], "expected legacy, balanced or spacious.")
        };

        var topology = entry.Property("topology") != null ? ObjectField(entry, "topology") : null;
        int? spawnRegionSlot = ReadSpawnRegionSlot(entry);
        int minCycleLength = entry.Property("minCycleLength") != null
            ? Integer(entry, "minCycleLength", 3, 20)
            : (topology?.Property("minCycleLength") != null ? Integer(topology, "minCycleLength", 3, 20) : 3);
        double extraLoopChance = entry.Property("extraLoopChance") != null
            ? UnitInterval(entry, "extraLoopChance")
            : (topology?.Property("extraLoopChance") != null ? UnitInterval(topology, "extraLoopChance") : 0d);
        bool localClustering = entry.Property("localClustering") != null
            ? BooleanField(entry, "localClustering")
            : (topology?.Property("localClustering") != null && BooleanField(topology, "localClustering"));

        return new Entry(id, Text(entry, "name"), Number(entry, "weight", double.Epsilon), patternId, hash, regions,
            new BudgetTargets(resourceMin, resourceMax, difficultyMin, difficultyMax),
            new LevelProgression(baseLevel, (float)levelPerHop, maxLevel), spacing,
            spawnRegionSlot, minCycleLength, extraLoopChance, localClustering);
    }

    private static int? ReadSpawnRegionSlot(JObject entry)
    {
        var prop = entry.Property("spawnRegionSlot");
        if (prop == null) return 0; // Legacy configs without this field keep slot A as spawn.
        var token = prop.Value;
        if (token.Type == JTokenType.Null) return null;
        if (token.Type == JTokenType.Integer)
        {
            int slot = Integer(entry, "spawnRegionSlot", 0, 2);
            return slot;
        }
        if (token.Type == JTokenType.String)
        {
            string value = ((string)token)?.Trim();
            if (string.IsNullOrEmpty(value) || string.Equals(value, "random", StringComparison.OrdinalIgnoreCase))
                return null;
            if (string.Equals(value, "A", StringComparison.OrdinalIgnoreCase) || value == "0") return 0;
            if (string.Equals(value, "B", StringComparison.OrdinalIgnoreCase) || value == "1") return 1;
            if (string.Equals(value, "C", StringComparison.OrdinalIgnoreCase) || value == "2") return 2;
        }
        throw Invalid(token, "expected 'random', 'A', 'B', 'C', 0, 1, 2, or null.");
    }

    private static double UnitInterval(JObject obj, string field)
    {
        double value = Number(obj, field, 0);
        if (value > 1d) throw Invalid(obj[field], "expected a number in [0, 1].");
        return value;
    }

    private static bool BooleanField(JObject obj, string field)
    {
        var token = obj[field];
        if (token?.Type != JTokenType.Boolean)
            throw Invalid(obj, $"'{field}' must be a JSON boolean.");
        return (bool)token;
    }

    private static void CheckFields(JObject obj, params string[] names)
    {
        foreach (var field in obj.Properties())
            if (!names.Contains(field.Name, StringComparer.Ordinal)) throw Invalid(field, $"unknown field '{field.Name}'.");
    }

    private static string Id(JObject obj, string field)
    {
        string id = Text(obj, field);
        if (!IdPattern.IsMatch(id)) throw Invalid(obj[field], "expected an ID of 1–64 ASCII letters, digits, underscores or hyphens.");
        return id;
    }

    private static string Text(JObject obj, string field)
    {
        var value = obj[field];
        if (value?.Type != JTokenType.String || string.IsNullOrWhiteSpace((string)value))
            throw Invalid(obj, $"'{field}' must be a non-empty string.");
        return (string)value;
    }

    private static int Integer(JObject obj, string field, int min, int max)
    {
        double value = Number(obj, field, min);
        if (value > max || value != Math.Truncate(value)) throw Invalid(obj[field], $"expected an integer in [{min}, {max}].");
        return (int)value;
    }

    private static double Number(JObject obj, string field, double min)
    {
        var token = obj[field];
        if (token?.Type != JTokenType.Integer && token?.Type != JTokenType.Float)
            throw Invalid(obj, $"'{field}' must be a JSON number.");
        double value = (double)token;
        if (double.IsNaN(value) || double.IsInfinity(value) || value < min)
            throw Invalid(token, $"expected a finite number greater than or equal to {min}.");
        return value;
    }

    private static double? Bound(JObject obj, string field) =>
        obj[field]?.Type == JTokenType.Null ? (double?)null : Number(obj, field, 0);

    private static JArray ArrayField(JObject obj, string field, int min, int max)
    {
        if (!(obj[field] is JArray array) || array.Count < min || array.Count > max)
            throw Invalid(obj, $"'{field}' must be an array with {min}–{max} items.");
        return array;
    }

    private static JObject ObjectField(JObject obj, string field) =>
        obj[field] as JObject ?? throw Invalid(obj, $"'{field}' must be an object.");

    private static JObject Object(JToken token) => token as JObject ?? throw Invalid(token, "expected a JSON object.");

    private static FormatException Invalid(JToken token, string message) =>
        new FormatException($"Map pools {(string.IsNullOrEmpty(token.Path) ? "$" : token.Path)}: {message}");
}
