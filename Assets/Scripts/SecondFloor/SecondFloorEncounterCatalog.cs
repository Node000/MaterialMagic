using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

public enum SecondFloorEncounterPool
{
    Begin,
    Mid,
    Normal,
    Elite,
    Boss
}

/// <summary>A concrete first-floor formation, with its separately authored second-floor display enemy.</summary>
public sealed class SecondFloorEncounter
{
    private readonly LevelData pinnedLevel;

    public SecondFloorEncounterPool Pool { get; }
    public int DisplayEnemyId { get; }
    public int SourceLevelId => pinnedLevel.numericId;
    /// <summary>Zero-based random group index, or -1 for a fixed formation.</summary>
    public int SourceGroupIndex { get; }
    public bool IsBoss => Pool == SecondFloorEncounterPool.Boss;

    internal SecondFloorEncounter(SecondFloorEncounterPool pool, int displayEnemyId, LevelData source, int groupIndex)
    {
        Pool = pool;
        DisplayEnemyId = displayEnemyId;
        SourceGroupIndex = groupIndex;
        pinnedLevel = CloneFixedLevel(source);
        if (groupIndex >= 0)
        {
            var group = source.randomEnemyGroups[groupIndex];
            pinnedLevel.enemies = ClonePlacements(group.enemies);
            pinnedLevel.enemyIds = Array.Empty<int>();
            if (!string.IsNullOrEmpty(group.mapIconPath)) pinnedLevel.mapIconPath = group.mapIconPath;
        }
    }

    /// <summary>
    /// Returns a fresh copy with no random group left to draw. Start this exact level, bypassing the
    /// first-floor battle-count pool draw. Enemy IDs, authored placements and metadata are not scaled.
    /// </summary>
    public LevelData CreateBattleLevel() => CloneFixedLevel(pinnedLevel);

    private static LevelData CloneFixedLevel(LevelData level) => new LevelData
    {
        numericId = level.numericId,
        id = level.id,
        titleKey = level.titleKey,
        levelType = level.levelType,
        enemies = ClonePlacements(level.enemies),
        randomEnemyGroups = Array.Empty<LevelEnemyGroupData>(),
        enemyIds = level.enemyIds == null ? null : (int[])level.enemyIds.Clone(),
        rewardPoolId = level.rewardPoolId,
        eventPoolId = level.eventPoolId,
        bonusLevelId = level.bonusLevelId,
        restTextKeys = level.restTextKeys == null ? null : (string[])level.restTextKeys.Clone(),
        restHealAmount = level.restHealAmount,
        mapIconPath = level.mapIconPath
    };

    private static LevelEnemyData[] ClonePlacements(LevelEnemyData[] source)
    {
        if (source == null) return null;
        if (source.Length == 0) return Array.Empty<LevelEnemyData>();
        var result = new LevelEnemyData[source.Length];
        for (int i = 0; i < source.Length; i++)
        {
            var placement = source[i];
            if (placement != null)
                result[i] = new LevelEnemyData { enemyId = placement.enemyId, x = placement.x, y = placement.y };
        }
        return result;
    }
}

/// <summary>Second-floor-only encounter selection; does not consume run or map-generation RNG state.</summary>
public sealed class SecondFloorEncounterCatalog
{
    public const string ResourcePath = "Data/Maps/SecondFloor.encounters";
    public const int FirstFloorChapterId = 1;
    private const string DocumentKind = "second-floor-encounters";

    private readonly Dictionary<SecondFloorEncounterPool, IReadOnlyList<SecondFloorEncounter>> pools =
        new Dictionary<SecondFloorEncounterPool, IReadOnlyList<SecondFloorEncounter>>();

    private SecondFloorEncounterCatalog() { }

    public static SecondFloorEncounterCatalog LoadDefault()
    {
        var asset = Resources.Load<TextAsset>(ResourcePath);
        if (asset == null) throw new InvalidOperationException($"Missing second-floor encounter catalog '{ResourcePath}'.");
        if (!GameDataDatabase.TryGetChapterData(FirstFloorChapterId, out var chapter))
            throw new InvalidOperationException($"Missing first-floor encounter source chapter {FirstFloorChapterId}.");
        return Read(asset.text, chapter, GameDataDatabase.LevelData);
    }

    /// <summary>Reads display selections and validates one entry for every battle formation in the source chapter pools.</summary>
    public static SecondFloorEncounterCatalog Read(string json, ChapterData sourceChapter, IReadOnlyDictionary<int, LevelData> levels)
    {
        if (sourceChapter == null) throw new ArgumentNullException(nameof(sourceChapter));
        if (levels == null) throw new ArgumentNullException(nameof(levels));
        var document = RuleJson.Deserialize<Document>(json);
        if (document == null || document.Kind != DocumentKind || document.SchemaVersion != 1)
            throw new FormatException("Unsupported second-floor encounter catalog schema.");

        var displayIds = new Dictionary<(int LevelId, int GroupIndex), int>();
        foreach (var entry in document.Entries)
        {
            if (entry == null || entry.LevelId < 1 || entry.GroupIndex < -1 || entry.DisplayEnemyId < 1)
                throw new FormatException("Invalid second-floor display enemy entry; expected levelId, groupIndex (-1 for fixed), and displayEnemyId.");
            var key = (entry.LevelId, entry.GroupIndex);
            if (!displayIds.TryAdd(key, entry.DisplayEnemyId))
                throw new FormatException($"Duplicate second-floor display enemy for level {entry.LevelId}, group {entry.GroupIndex}.");
        }

        var catalog = new SecondFloorEncounterCatalog();
        var usedDisplayIds = new HashSet<(int LevelId, int GroupIndex)>();
        catalog.AddPool(SecondFloorEncounterPool.Begin, sourceChapter.BeginPool, levels, displayIds, usedDisplayIds);
        catalog.AddPool(SecondFloorEncounterPool.Mid, sourceChapter.MidPool, levels, displayIds, usedDisplayIds);
        catalog.AddPool(SecondFloorEncounterPool.Normal, sourceChapter.NormalPool, levels, displayIds, usedDisplayIds);
        catalog.AddPool(SecondFloorEncounterPool.Elite, sourceChapter.ElitePool, levels, displayIds, usedDisplayIds);
        catalog.AddPool(SecondFloorEncounterPool.Boss, sourceChapter.BossPool, levels, displayIds, usedDisplayIds);
        foreach (var key in displayIds.Keys)
            if (!usedDisplayIds.Contains(key))
                throw new FormatException($"Second-floor display enemy refers to level {key.LevelId}, group {key.GroupIndex}, outside the source formations.");
        return catalog;
    }

    public IReadOnlyList<SecondFloorEncounter> GetPool(SecondFloorEncounterPool pool) => pools[pool];

    public bool TryResolve(int mapSeed, SecondFloorFloorMap.Tile tile, out SecondFloorEncounter encounter)
    {
        if (tile == null)
        {
            encounter = null;
            return false;
        }
        return TryResolve(mapSeed, tile.Position, tile.Type, tile.EnemyLevel, out encounter);
    }

    /// <summary>Only authored battle tile types produce encounters. Missing or nonpositive rank defaults to rank 1.</summary>
    public bool TryResolve(int mapSeed, Vector2Int position, string tileType, int? enemyRank, out SecondFloorEncounter encounter)
    {
        int rank = Math.Max(1, enemyRank.GetValueOrDefault());
        SecondFloorEncounterPool pool;
        uint tileKind;
        switch (tileType)
        {
            case "combat":
                pool = rank <= 2 ? SecondFloorEncounterPool.Begin : rank <= 4 ? SecondFloorEncounterPool.Mid : SecondFloorEncounterPool.Normal;
                tileKind = 1;
                break;
            case "elite":
                pool = SecondFloorEncounterPool.Elite;
                tileKind = 2;
                break;
            case "boss":
                pool = SecondFloorEncounterPool.Boss;
                tileKind = 3;
                break;
            default:
                encounter = null;
                return false;
        }
        var candidates = GetPool(pool);
        uint hash = HashTile(mapSeed, position, tileKind, rank);
        encounter = candidates[(int)(hash % (uint)candidates.Count)];
        return true;
    }

    private void AddPool(SecondFloorEncounterPool pool, int[] levelIds, IReadOnlyDictionary<int, LevelData> levels,
        Dictionary<(int LevelId, int GroupIndex), int> displayIds, HashSet<(int LevelId, int GroupIndex)> usedDisplayIds)
    {
        if (levelIds == null || levelIds.Length == 0)
            throw new FormatException($"The first-floor {pool} encounter pool is empty.");
        var formations = new List<SecondFloorEncounter>();
        var seenLevels = new HashSet<int>();
        foreach (int levelId in levelIds)
        {
            if (!seenLevels.Add(levelId)) continue;
            if (!levels.TryGetValue(levelId, out var source) || source == null || source.numericId != levelId)
                throw new FormatException($"The first-floor {pool} encounter pool references missing level {levelId}.");
            if (source.levelType != LevelType.Battle &&
                (source.levelType != LevelType.Elite || pool != SecondFloorEncounterPool.Elite && pool != SecondFloorEncounterPool.Boss))
                throw new FormatException($"Level {levelId} has invalid type {source.levelType} for the first-floor {pool} encounter pool.");

            int groupCount = source.randomEnemyGroups?.Length ?? 0;
            if (groupCount == 0)
                formations.Add(CreateFormation(pool, source, -1, displayIds, usedDisplayIds));
            else
                for (int groupIndex = 0; groupIndex < groupCount; groupIndex++)
                    formations.Add(CreateFormation(pool, source, groupIndex, displayIds, usedDisplayIds));
        }
        pools.Add(pool, formations.AsReadOnly());
    }

    private static SecondFloorEncounter CreateFormation(SecondFloorEncounterPool pool, LevelData level, int groupIndex,
        Dictionary<(int LevelId, int GroupIndex), int> displayIds, HashSet<(int LevelId, int GroupIndex)> usedDisplayIds)
    {
        var key = (level.numericId, groupIndex);
        string label = $"level {level.numericId}, group {groupIndex}";
        if (!displayIds.TryGetValue(key, out int displayEnemyId))
            throw new FormatException($"Missing second-floor display enemy for {label}.");

        bool found = false;
        var placements = groupIndex >= 0 ? level.randomEnemyGroups[groupIndex]?.enemies : level.enemies;
        if (placements?.Length > 0)
        {
            foreach (var placement in placements)
            {
                if (placement == null || placement.enemyId < 1)
                    throw new FormatException($"Invalid enemy placement at {label}.");
                if (placement.enemyId == displayEnemyId) found = true;
            }
        }
        else if (groupIndex < 0 && level.enemyIds?.Length > 0)
        {
            foreach (int enemyId in level.enemyIds)
            {
                if (enemyId < 1) throw new FormatException($"Invalid enemy id at {label}.");
                if (enemyId == displayEnemyId) found = true;
            }
        }
        else throw new FormatException($"Empty first-floor encounter formation at {label}.");

        if (!found)
            throw new FormatException($"Second-floor display enemy {displayEnemyId} is not a member of {label}.");
        usedDisplayIds.Add(key);
        return new SecondFloorEncounter(pool, displayEnemyId, level, groupIndex);
    }

    private static uint HashTile(int mapSeed, Vector2Int position, uint tileKind, int rank)
    {
        // FNV-1a over five fixed-width, little-endian words, followed by an avalanche. Neither
        // platform endianness, string hashing, dictionary order, nor prior resolutions enter the key.
        unchecked
        {
            uint hash = HashWord(2166136261u, (uint)mapSeed);
            hash = HashWord(hash, (uint)position.x);
            hash = HashWord(hash, (uint)position.y);
            hash = HashWord(hash, tileKind);
            hash = HashWord(hash, (uint)rank);
            hash ^= hash >> 16;
            hash *= 0x85ebca6bu;
            hash ^= hash >> 13;
            hash *= 0xc2b2ae35u;
            return hash ^ (hash >> 16);
        }
    }

    private static uint HashWord(uint hash, uint word)
    {
        unchecked
        {
            for (int i = 0; i < 4; i++, word >>= 8) hash = (hash ^ (word & 0xffu)) * 16777619u;
            return hash;
        }
    }

    private sealed class Document
    {
        [JsonProperty("kind", Required = Required.Always)] public string Kind { get; set; }
        [JsonProperty("schemaVersion", Required = Required.Always)] public int SchemaVersion { get; set; }
        [JsonProperty("entries", Required = Required.Always)] public DisplayEntry[] Entries { get; set; }
    }

    private sealed class DisplayEntry
    {
        [JsonProperty("levelId", Required = Required.Always)] public int LevelId { get; set; }
        [JsonProperty("groupIndex", Required = Required.Always)] public int GroupIndex { get; set; }
        [JsonProperty("displayEnemyId", Required = Required.Always)] public int DisplayEnemyId { get; set; }
    }
}
