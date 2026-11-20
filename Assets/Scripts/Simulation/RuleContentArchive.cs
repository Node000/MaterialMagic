using System;
using System.Collections.Generic;

/// <summary>
/// Portable converted content, exported after Unity's JSON/SO precedence has been applied.
/// This carries values only, never ScriptableObject/Material/scene references or profile state.
/// </summary>
public sealed class RuleContentArchive
{
    public int version = 1;
    public Dictionary<int, RelicData> relics;
    public Dictionary<string, RelicPoolData> relicPools;
    public Dictionary<int, MagicData> magics;
    public Dictionary<MagicRarity, MagicRarityData> rarities;
    public Dictionary<int, EnemyData> enemies;
    public Dictionary<int, EventData> events;
    public Dictionary<int, LevelData> levels;
    public Dictionary<int, RewardPoolData> rewards;
    public Dictionary<int, BonusLevelData> bonuses;
    public Dictionary<int, ChapterData> chapters;
    public Dictionary<int, MapGenConfigData> mapConfigs;
    public Dictionary<int, EconomyConfigData> economies;
    public Dictionary<int, ShopProductPoolData> shopPools;
    public Dictionary<int, AscensionData> ascensions;
    public Dictionary<string, DifficultyUpgradeData> difficulties;
    public Dictionary<string, TagData> tags;
    public Dictionary<string, MagicModifierData> magicModifiers;
    public Dictionary<string, MaterialModifierData> materialModifiers;
    public Dictionary<string, PlayerStartConfigData> starts;
    public float summonHorizontal = 180f, summonVertical = 160f, summonRadius = 150f;
    public int summonRowSlots = 4, summonAttempts = 16;
    public Dictionary<string, RuleArrowUpgradeValue> arrowUpgrades;

    public static RuleContentArchive Capture(IRuleContentProvider source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        var result = new RuleContentArchive
        {
            relics = Copy(source.Numeric<RelicData>()), relicPools = Copy(source.Named<RelicPoolData>()),
            magics = Copy(source.Numeric<MagicData>()), rarities = Copy(source.Rarities),
            enemies = Copy(source.Numeric<EnemyData>()), events = Copy(source.Numeric<EventData>()),
            levels = Copy(source.Numeric<LevelData>()), rewards = Copy(source.Numeric<RewardPoolData>()),
            bonuses = Copy(source.Numeric<BonusLevelData>()), chapters = Copy(source.Numeric<ChapterData>()),
            mapConfigs = Copy(source.Numeric<MapGenConfigData>()), economies = Copy(source.Numeric<EconomyConfigData>()),
            shopPools = Copy(source.Numeric<ShopProductPoolData>()), ascensions = Copy(source.Numeric<AscensionData>()),
            difficulties = Copy(source.Named<DifficultyUpgradeData>()), tags = Copy(source.Named<TagData>()),
            magicModifiers = Copy(source.Named<MagicModifierData>()), materialModifiers = Copy(source.Named<MaterialModifierData>()),
            starts = Copy(source.Named<PlayerStartConfigData>()), arrowUpgrades = new Dictionary<string, RuleArrowUpgradeValue>()
        };
        if (source is IRuleConfigurationProvider config)
        {
            RuleSummonLayout layout = config.SummonLayout;
            result.summonHorizontal = layout.HorizontalSpacing; result.summonVertical = layout.VerticalSpacing;
            result.summonRadius = layout.OccupiedRadius; result.summonRowSlots = layout.SameRowSlotCount;
            result.summonAttempts = layout.MaxSearchAttempts;
            foreach (ArrowUpgradeNodeDefinition node in ArrowUpgradeSystem.Nodes)
                if (config.TryGetArrowUpgrade(node.Id, out RuleArrowUpgradeValue value)) result.arrowUpgrades.Add(node.Id, value);
        }
        return result;
    }

    public IRuleContentProvider CreateProvider()
    {
        if (version != 1) throw new NotSupportedException("Unsupported content archive version: " + version);
        return new RuleContentSnapshot(rarities, new RuleConfiguration(
            new RuleSummonLayout(summonHorizontal, summonVertical, summonRadius, summonRowSlots, summonAttempts), arrowUpgrades))
            .AddNumeric(relics).AddNamed(relicPools).AddNumeric(magics).AddNumeric(enemies).AddNumeric(events)
            .AddNumeric(levels).AddNumeric(rewards).AddNumeric(bonuses).AddNumeric(chapters).AddNumeric(mapConfigs)
            .AddNumeric(economies).AddNumeric(shopPools).AddNumeric(ascensions).AddNamed(difficulties).AddNamed(tags)
            .AddNamed(magicModifiers).AddNamed(materialModifiers).AddNamed(starts).Freeze();
    }
    public string ToJson() => RuleJson.Serialize(this, true);
    public static RuleContentArchive FromJson(string json) => RuleJson.Deserialize<RuleContentArchive>(json)
        ?? throw new ArgumentException("A content archive is required.", nameof(json));
    private static Dictionary<K, V> Copy<K, V>(IReadOnlyDictionary<K, V> source)
    {
        var result = new Dictionary<K, V>();
        foreach (var pair in source) result.Add(pair.Key, RuleJson.Clone(pair.Value));
        return result;
    }
}
