using System;
using System.Collections.Generic;
using UnityEngine;

public enum DifficultyUpgradeScope
{
    Global = 0,
    EnemySpecific = 1,
    BossOnly = 2,
    ChapterOnly = 3
}

public enum DifficultyUpgradeEffectType
{
    None = 0,
    EnemyMaxHealthMultiplier = 1,
    EnemyAttackIntentMultiplier = 2,
    EnemyInitialBuff = 3,
    PlayerMaxHealthDelta = 10,
    StartingDeckAddBasicDirections = 11,
    GoldGainMultiplier = 12,
    RewardMagicChoiceDelta = 13,
    ShopPriceMultiplier = 14,
    MapWidthDelta = 15,
    MapHeightDelta = 16,
    MapBlockAvailableCells = 17,
    DesignedMapMainAreaHeightDelta = 25,
    StartingMagicSlotDelta = 18,
    MagicRarityWeightMultiplier = 19,
    MapHiddenLevelChanceDelta = 20,
    MapLevelWeightMultiplier = 21,
    DesignedMapLevelCountDelta = 22,
    ChapterLengthDelta = 23,
    StartingDeckAddPlaceholder = 24
}

[Serializable]
public class DifficultyUpgradeEffectData
{
    public DifficultyUpgradeEffectType type;
    public float value = 1f;
    public int intValue;
    public BuffEnum buffType;
    public int stack;
    public MagicRarity rarity;
}

[Serializable]
public class DifficultyUpgradeData : IDataRecord
{
    public string id;
    public string nameKey;
    public string descriptionKey;
    public int cost;
    public DifficultyUpgradeScope scope;
    public int[] targetEnemyIds = Array.Empty<int>();
    public int[] chapterIds = Array.Empty<int>();
    public LevelType[] targetLevelTypes = Array.Empty<LevelType>();
    public DifficultyUpgradeEffectData[] effects = Array.Empty<DifficultyUpgradeEffectData>();
    public string handlerId;

    public string Id => id;
}

[Serializable]
public class AscensionData : INumericDataRecord
{
    public int numericId;
    public string id;
    public string nameKey;
    public string descriptionKey;
    public string[] upgradeIds = Array.Empty<string>();

    public int NumericId => numericId;
}

[Serializable]
public class RunDifficultySaveData
{
    public int ascensionLevel;
    public string[] upgradeIds = Array.Empty<string>();
}

public class DifficultyUpgradeContext
{
    public LevelData Level { get; set; }
    public int ChapterNumericId { get; set; }
    public bool IsBoss { get; set; }
}

public abstract class DifficultyUpgrade
{
    protected DifficultyUpgrade(DifficultyUpgradeData data)
    {
        Data = data;
    }

    public DifficultyUpgradeData Data { get; }
    public string Id => Data != null ? Data.id : string.Empty;

    public abstract void ApplyEnemyUpgrade(EnemyRuntimeDefinition definition, DifficultyUpgradeContext context);

    public virtual void ApplyPlayerUpgrade(PlayerStatus player)
    {
    }

    public virtual int ModifyGoldGain(int amount)
    {
        return amount;
    }

    public virtual int ModifyRewardMagicChoiceCount(int choiceCount)
    {
        return choiceCount;
    }

    public virtual int ModifyShopPrice(int price)
    {
        return price;
    }

    public virtual int ModifyMapWidth(int width)
    {
        return width;
    }

    public virtual int ModifyMapHeight(int height)
    {
        return height;
    }

    public virtual int ModifyMapLevelWeight(LevelType levelType, int weight)
    {
        return weight;
    }

    public virtual int ModifyDesignedMapMainAreaHeight(int height)
    {
        return height;
    }

    public virtual int ModifyDesignedMapLevelCount(LevelType levelType, int count)
    {
        return count;
    }

    public virtual int ModifyChapterLength(int length)
    {
        return length;
    }

    public virtual void ApplyMapUpgrade(RunMapGridModel grid, Func<int, int, int> nextRandomInt, bool applyMapBlocks)
    {
    }

    public virtual int ModifyMagicSlotCount(int slotCount)
    {
        return slotCount;
    }

    public virtual float ModifyMagicRarityWeight(MagicRarity rarity, float weight)
    {
        return weight;
    }
}

public sealed class DataDrivenDifficultyUpgrade : DifficultyUpgrade
{
    public DataDrivenDifficultyUpgrade(DifficultyUpgradeData data) : base(data)
    {
    }

    public override void ApplyEnemyUpgrade(EnemyRuntimeDefinition definition, DifficultyUpgradeContext context)
    {
        if (definition == null || Data == null || !Matches(definition, context))
            return;

        for (int i = 0; Data.effects != null && i < Data.effects.Length; i++)
            ApplyEnemyEffect(definition, Data.effects[i]);
    }

    public override void ApplyPlayerUpgrade(PlayerStatus player)
    {
        if (player == null || Data == null)
            return;

        for (int i = 0; Data.effects != null && i < Data.effects.Length; i++)
            ApplyPlayerEffect(player, Data.effects[i]);
    }

    public override int ModifyGoldGain(int amount)
    {
        if (amount <= 0 || Data == null)
            return amount;

        int result = amount;
        for (int i = 0; Data.effects != null && i < Data.effects.Length; i++)
        {
            DifficultyUpgradeEffectData effect = Data.effects[i];
            if (effect != null && effect.type == DifficultyUpgradeEffectType.GoldGainMultiplier)
                result = Mathf.Max(0, Mathf.RoundToInt(result * Mathf.Max(0f, effect.value)));
        }
        return result;
    }

    public override int ModifyRewardMagicChoiceCount(int choiceCount)
    {
        if (Data == null)
            return choiceCount;

        int result = choiceCount;
        for (int i = 0; Data.effects != null && i < Data.effects.Length; i++)
        {
            DifficultyUpgradeEffectData effect = Data.effects[i];
            if (effect != null && effect.type == DifficultyUpgradeEffectType.RewardMagicChoiceDelta)
                result += GetEffectInt(effect);
        }
        return Mathf.Max(1, result);
    }

    public override int ModifyShopPrice(int price)
    {
        if (price <= 0 || Data == null)
            return price;

        int result = price;
        for (int i = 0; Data.effects != null && i < Data.effects.Length; i++)
        {
            DifficultyUpgradeEffectData effect = Data.effects[i];
            if (effect != null && effect.type == DifficultyUpgradeEffectType.ShopPriceMultiplier)
                result = Mathf.Max(1, Mathf.RoundToInt(result * Mathf.Max(0f, effect.value)));
        }
        return result;
    }

    public override int ModifyMapWidth(int width)
    {
        return ModifyMapSize(width, DifficultyUpgradeEffectType.MapWidthDelta);
    }

    public override int ModifyMapHeight(int height)
    {
        return ModifyMapSize(height, DifficultyUpgradeEffectType.MapHeightDelta);
    }

    public override int ModifyMapLevelWeight(LevelType levelType, int weight)
    {
        if (weight <= 0 || Data == null)
            return weight;

        int result = weight;
        for (int i = 0; Data.effects != null && i < Data.effects.Length; i++)
        {
            DifficultyUpgradeEffectData effect = Data.effects[i];
            if (effect != null && effect.type == DifficultyUpgradeEffectType.MapLevelWeightMultiplier && MatchesTargetLevelType(levelType))
                result = Mathf.RoundToInt(result * Mathf.Max(0f, effect.value));
        }
        return Mathf.Max(0, result);
    }

    public override int ModifyDesignedMapMainAreaHeight(int height)
    {
        return ModifyMapSize(height, DifficultyUpgradeEffectType.DesignedMapMainAreaHeightDelta);
    }

    public override int ModifyDesignedMapLevelCount(LevelType levelType, int count)
    {
        if (Data == null)
            return count;

        int result = count;
        for (int i = 0; Data.effects != null && i < Data.effects.Length; i++)
        {
            DifficultyUpgradeEffectData effect = Data.effects[i];
            if (effect != null && effect.type == DifficultyUpgradeEffectType.DesignedMapLevelCountDelta && MatchesTargetLevelType(levelType))
                result += GetEffectInt(effect);
        }
        return Mathf.Max(0, result);
    }

    public override int ModifyChapterLength(int length)
    {
        if (Data == null)
            return length;

        int result = length;
        for (int i = 0; Data.effects != null && i < Data.effects.Length; i++)
        {
            DifficultyUpgradeEffectData effect = Data.effects[i];
            if (effect != null && effect.type == DifficultyUpgradeEffectType.ChapterLengthDelta)
                result += GetEffectInt(effect);
        }
        return Mathf.Max(1, result);
    }

    public override void ApplyMapUpgrade(RunMapGridModel grid, Func<int, int, int> nextRandomInt, bool applyMapBlocks)
    {
        if (grid == null || Data == null)
            return;

        float hiddenWeight = 0f;
        for (int i = 0; Data.effects != null && i < Data.effects.Length; i++)
        {
            DifficultyUpgradeEffectData effect = Data.effects[i];
            if (effect == null)
                continue;

            if (applyMapBlocks && effect.type == DifficultyUpgradeEffectType.MapBlockAvailableCells)
                BlockAvailableMapCells(grid, Mathf.Max(0, GetEffectInt(effect)), nextRandomInt);
            else if (effect.type == DifficultyUpgradeEffectType.MapHiddenLevelChanceDelta)
                hiddenWeight += effect.value;
        }

        ApplyHiddenMapCells(grid, hiddenWeight, nextRandomInt);
    }

    public override int ModifyMagicSlotCount(int slotCount)
    {
        if (Data == null)
            return slotCount;

        int result = slotCount;
        for (int i = 0; Data.effects != null && i < Data.effects.Length; i++)
        {
            DifficultyUpgradeEffectData effect = Data.effects[i];
            if (effect != null && effect.type == DifficultyUpgradeEffectType.StartingMagicSlotDelta)
                result += GetEffectInt(effect);
        }
        return Mathf.Max(1, result);
    }

    public override float ModifyMagicRarityWeight(MagicRarity rarity, float weight)
    {
        if (weight <= 0f || Data == null)
            return weight;

        float result = weight;
        for (int i = 0; Data.effects != null && i < Data.effects.Length; i++)
        {
            DifficultyUpgradeEffectData effect = Data.effects[i];
            if (effect != null && effect.type == DifficultyUpgradeEffectType.MagicRarityWeightMultiplier && effect.rarity == rarity)
                result *= Mathf.Max(0f, effect.value);
        }
        return Mathf.Max(0f, result);
    }

    private int ModifyMapSize(int size, DifficultyUpgradeEffectType effectType)
    {
        if (Data == null)
            return size;

        int result = size;
        for (int i = 0; Data.effects != null && i < Data.effects.Length; i++)
        {
            DifficultyUpgradeEffectData effect = Data.effects[i];
            if (effect != null && effect.type == effectType)
                result += GetEffectInt(effect);
        }
        return Mathf.Max(1, result);
    }

    private bool MatchesTargetLevelType(LevelType levelType)
    {
        return Data == null || Data.targetLevelTypes == null || Data.targetLevelTypes.Length == 0 || Contains(Data.targetLevelTypes, levelType);
    }

    private bool Matches(EnemyRuntimeDefinition definition, DifficultyUpgradeContext context)
    {
        switch (Data.scope)
        {
            case DifficultyUpgradeScope.EnemySpecific:
                if (!Contains(Data.targetEnemyIds, definition.NumericId))
                    return false;
                break;
            case DifficultyUpgradeScope.BossOnly:
                if (context == null || !context.IsBoss)
                    return false;
                break;
            case DifficultyUpgradeScope.ChapterOnly:
                if (context == null || !Contains(Data.chapterIds, context.ChapterNumericId))
                    return false;
                break;
        }

        if (Data.scope != DifficultyUpgradeScope.EnemySpecific && Data.targetEnemyIds != null && Data.targetEnemyIds.Length > 0 && !Contains(Data.targetEnemyIds, definition.NumericId))
            return false;
        if (Data.scope != DifficultyUpgradeScope.ChapterOnly && Data.chapterIds != null && Data.chapterIds.Length > 0 && (context == null || !Contains(Data.chapterIds, context.ChapterNumericId)))
            return false;
        if (Data.targetLevelTypes != null && Data.targetLevelTypes.Length > 0 && (context == null || context.Level == null || !Contains(Data.targetLevelTypes, context.Level.levelType)))
            return false;

        return true;
    }

    private static void ApplyEnemyEffect(EnemyRuntimeDefinition definition, DifficultyUpgradeEffectData effect)
    {
        if (definition == null || effect == null)
            return;

        switch (effect.type)
        {
            case DifficultyUpgradeEffectType.EnemyMaxHealthMultiplier:
                definition.MaxHealth = Mathf.Max(1, Mathf.RoundToInt(definition.MaxHealth * Mathf.Max(0f, effect.value)));
                break;
            case DifficultyUpgradeEffectType.EnemyAttackIntentMultiplier:
                definition.IntentPlan?.MultiplyAttackIntentValues(Mathf.Max(0f, effect.value));
                break;
            case DifficultyUpgradeEffectType.EnemyInitialBuff:
                definition.AddInitialBuff(effect.buffType, effect.stack);
                break;
        }
    }

    private static void ApplyPlayerEffect(PlayerStatus player, DifficultyUpgradeEffectData effect)
    {
        if (player == null || effect == null)
            return;

        switch (effect.type)
        {
            case DifficultyUpgradeEffectType.PlayerMaxHealthDelta:
                player.AdjustMaxHealthOnly(GetEffectInt(effect));
                break;
            case DifficultyUpgradeEffectType.StartingDeckAddBasicDirections:
                AddBasicDirectionSet(player);
                break;
            case DifficultyUpgradeEffectType.StartingDeckAddPlaceholder:
                AddPlaceholderDeckCards(player, Mathf.Max(0, GetEffectInt(effect)));
                break;
        }
    }

    private static void AddBasicDirectionSet(PlayerStatus player)
    {
        player.AddDeckMaterial(MaterialEnum.Fire);
        player.AddDeckMaterial(MaterialEnum.Wind);
        player.AddDeckMaterial(MaterialEnum.Water);
        player.AddDeckMaterial(MaterialEnum.Earth);
    }

    private static void AddPlaceholderDeckCards(PlayerStatus player, int count)
    {
        for (int i = 0; i < count; i++)
            player.AddDeckPlaceholderMaterial();
    }

    private static int GetEffectInt(DifficultyUpgradeEffectData effect)
    {
        return effect.intValue != 0 ? effect.intValue : Mathf.RoundToInt(effect.value);
    }

    private static void ApplyHiddenMapCells(RunMapGridModel grid, float hiddenWeight, Func<int, int, int> nextRandomInt)
    {
        hiddenWeight = Mathf.Clamp01(hiddenWeight);
        if (grid == null || hiddenWeight <= 0f || grid.cells == null || grid.cells.Count == 0)
            return;

        int threshold = Mathf.RoundToInt(hiddenWeight * 10000f);
        for (int i = 0; i < grid.cells.Count; i++)
        {
            RunMapCellModel cell = grid.cells[i];
            if (CanHideMapCell(grid, cell))
                cell.isHidden = NextRandomInt(nextRandomInt, 0, 10000) < threshold;
            else if (cell != null && (cell.isBoss || cell.level != null && cell.level.levelType == LevelType.Elite))
                cell.isHidden = false;
        }
    }

    private static bool CanHideMapCell(RunMapGridModel grid, RunMapCellModel cell)
    {
        if (grid == null || cell == null || !cell.isAvailable || cell.isBoss || cell.level == null || cell.level.levelType == LevelType.Elite)
            return false;
        return cell.x != grid.playerX || cell.y != grid.playerY;
    }

    private static void BlockAvailableMapCells(RunMapGridModel grid, int count, Func<int, int, int> nextRandomInt)
    {
        if (grid == null || count <= 0 || grid.cells == null || grid.cells.Count == 0)
            return;

        int blocked = 0;
        int attempts = 0;
        int maxAttempts = grid.cells.Count * 12;
        while (blocked < count && attempts < maxAttempts)
        {
            attempts++;
            int index = NextRandomInt(nextRandomInt, 0, grid.cells.Count);
            RunMapCellModel cell = grid.cells[index];
            if (!CanBlockMapCell(grid, cell))
                continue;

            LevelData previousLevel = cell.level;
            bool previousAvailable = cell.isAvailable;
            cell.level = null;
            cell.isAvailable = false;
            if (IsMapStillTraversable(grid))
            {
                blocked++;
                continue;
            }

            cell.level = previousLevel;
            cell.isAvailable = previousAvailable;
        }
    }

    private static bool CanBlockMapCell(RunMapGridModel grid, RunMapCellModel cell)
    {
        if (grid == null || cell == null || !cell.isAvailable || cell.isBoss)
            return false;
        return cell.x != grid.playerX || cell.y != grid.playerY;
    }

    private static bool IsMapStillTraversable(RunMapGridModel grid)
    {
        RunMapCellModel start = grid != null ? grid.GetCurrentCell() : null;
        if (start == null || !start.isAvailable)
            return false;

        for (int i = 0; grid.cells != null && i < grid.cells.Count; i++)
        {
            RunMapCellModel cell = grid.cells[i];
            if (cell != null && cell.isAvailable && !grid.IsCellReachable(cell.x, cell.y))
                return false;
        }
        return true;
    }

    private static int NextRandomInt(Func<int, int, int> nextRandomInt, int minInclusive, int maxExclusive)
    {
        return nextRandomInt != null ? nextRandomInt(minInclusive, maxExclusive) : UnityEngine.Random.Range(minInclusive, maxExclusive);
    }

    private static bool Contains(int[] values, int value)
    {
        for (int i = 0; values != null && i < values.Length; i++)
        {
            if (values[i] == value)
                return true;
        }
        return false;
    }

    private static bool Contains(LevelType[] values, LevelType value)
    {
        for (int i = 0; values != null && i < values.Length; i++)
        {
            if (values[i] == value)
                return true;
        }
        return false;
    }
}

public static class DifficultyUpgradeSystem
{
    private static readonly List<DifficultyUpgrade> activeUpgrades = new List<DifficultyUpgrade>();
    private static RunDifficultySaveData currentState = new RunDifficultySaveData();

    public static int CurrentAscensionLevel => currentState != null ? currentState.ascensionLevel : 0;
    public static IReadOnlyList<DifficultyUpgrade> ActiveUpgrades => activeUpgrades;

    public static void InitializeNewRun(bool tutorialRun)
    {
        int ascensionLevel = tutorialRun ? 0 : AscensionSystem.SelectedAscensionLevel;
        Initialize(BuildRunStateForAscension(ascensionLevel));
    }

    public static void InitializeFromSave(RunSaveData save)
    {
        Initialize(save != null && save.difficulty != null ? CloneState(save.difficulty) : new RunDifficultySaveData());
    }

    public static RunDifficultySaveData ExportCurrentState()
    {
        return CloneState(currentState);
    }

    public static void ApplyEnemyUpgrades(EnemyRuntimeDefinition definition, DifficultyUpgradeContext context)
    {
        for (int i = 0; i < activeUpgrades.Count; i++)
            activeUpgrades[i]?.ApplyEnemyUpgrade(definition, context);
    }

    public static void ApplyPlayerUpgrades(PlayerStatus player)
    {
        for (int i = 0; i < activeUpgrades.Count; i++)
            activeUpgrades[i]?.ApplyPlayerUpgrade(player);
    }

    public static int ModifyGoldGain(int amount)
    {
        int result = amount;
        for (int i = 0; i < activeUpgrades.Count; i++)
            result = activeUpgrades[i] != null ? activeUpgrades[i].ModifyGoldGain(result) : result;
        return result;
    }

    public static int ModifyRewardMagicChoiceCount(int choiceCount)
    {
        int result = choiceCount;
        for (int i = 0; i < activeUpgrades.Count; i++)
            result = activeUpgrades[i] != null ? activeUpgrades[i].ModifyRewardMagicChoiceCount(result) : result;
        return Mathf.Max(1, result);
    }

    public static int ModifyShopPrice(int price)
    {
        int result = price;
        for (int i = 0; i < activeUpgrades.Count; i++)
            result = activeUpgrades[i] != null ? activeUpgrades[i].ModifyShopPrice(result) : result;
        return Mathf.Max(0, result);
    }

    public static int ModifyMapWidth(int width)
    {
        int result = width;
        for (int i = 0; i < activeUpgrades.Count; i++)
            result = activeUpgrades[i] != null ? activeUpgrades[i].ModifyMapWidth(result) : result;
        return Mathf.Max(1, result);
    }

    public static int ModifyMapHeight(int height)
    {
        int result = height;
        for (int i = 0; i < activeUpgrades.Count; i++)
            result = activeUpgrades[i] != null ? activeUpgrades[i].ModifyMapHeight(result) : result;
        return Mathf.Max(1, result);
    }

    public static int ModifyMapLevelWeight(LevelType levelType, int weight)
    {
        int result = weight;
        for (int i = 0; i < activeUpgrades.Count; i++)
            result = activeUpgrades[i] != null ? activeUpgrades[i].ModifyMapLevelWeight(levelType, result) : result;
        return Mathf.Max(0, result);
    }

    public static int ModifyDesignedMapMainAreaHeight(int height)
    {
        int result = height;
        for (int i = 0; i < activeUpgrades.Count; i++)
            result = activeUpgrades[i] != null ? activeUpgrades[i].ModifyDesignedMapMainAreaHeight(result) : result;
        return Mathf.Max(1, result);
    }

    public static int ModifyDesignedMapLevelCount(LevelType levelType, int count)
    {
        int result = count;
        for (int i = 0; i < activeUpgrades.Count; i++)
            result = activeUpgrades[i] != null ? activeUpgrades[i].ModifyDesignedMapLevelCount(levelType, result) : result;
        return Mathf.Max(0, result);
    }

    public static int ModifyChapterLength(int length)
    {
        int result = length;
        for (int i = 0; i < activeUpgrades.Count; i++)
            result = activeUpgrades[i] != null ? activeUpgrades[i].ModifyChapterLength(result) : result;
        return Mathf.Max(1, result);
    }

    public static int GetMapBlockedCellCount()
    {
        int count = 0;
        for (int i = 0; i < activeUpgrades.Count; i++)
        {
            DifficultyUpgradeData data = activeUpgrades[i] != null ? activeUpgrades[i].Data : null;
            for (int effectIndex = 0; data != null && data.effects != null && effectIndex < data.effects.Length; effectIndex++)
            {
                DifficultyUpgradeEffectData effect = data.effects[effectIndex];
                if (effect != null && effect.type == DifficultyUpgradeEffectType.MapBlockAvailableCells)
                    count += Mathf.Max(0, effect.intValue != 0 ? effect.intValue : Mathf.RoundToInt(effect.value));
            }
        }
        return count;
    }

    public static void ApplyMapUpgrades(RunMapGridModel grid, Func<int, int, int> nextRandomInt, bool applyMapBlocks = true)
    {
        for (int i = 0; i < activeUpgrades.Count; i++)
            activeUpgrades[i]?.ApplyMapUpgrade(grid, nextRandomInt, applyMapBlocks);
    }

    public static int ModifyMagicSlotCount(int slotCount)
    {
        int result = slotCount;
        for (int i = 0; i < activeUpgrades.Count; i++)
            result = activeUpgrades[i] != null ? activeUpgrades[i].ModifyMagicSlotCount(result) : result;
        return Mathf.Clamp(result, 1, Mathf.Max(1, slotCount));
    }

    public static float ModifyMagicRarityWeight(MagicRarity rarity, float weight)
    {
        float result = weight;
        for (int i = 0; i < activeUpgrades.Count; i++)
            result = activeUpgrades[i] != null ? activeUpgrades[i].ModifyMagicRarityWeight(rarity, result) : result;
        return Mathf.Max(0f, result);
    }

    private static void Initialize(RunDifficultySaveData state)
    {
        currentState = state ?? new RunDifficultySaveData();
        if (currentState.upgradeIds == null)
            currentState.upgradeIds = Array.Empty<string>();

        activeUpgrades.Clear();
        for (int i = 0; i < currentState.upgradeIds.Length; i++)
        {
            DifficultyUpgrade upgrade = DifficultyUpgradeFactory.Create(currentState.upgradeIds[i]);
            if (upgrade != null)
                activeUpgrades.Add(upgrade);
        }
    }

    public static RunDifficultySaveData BuildPreviewStateForAscension(int ascensionLevel)
    {
        return BuildRunStateForAscension(ascensionLevel);
    }

    private static RunDifficultySaveData BuildRunStateForAscension(int ascensionLevel)
    {
        ascensionLevel = Mathf.Clamp(ascensionLevel, 0, AscensionSystem.MaxAscensionLevel);
        List<string> upgradeIds = new List<string>();
        for (int level = 1; level <= ascensionLevel; level++)
        {
            if (!GameDataDatabase.TryGetAscensionData(level, out AscensionData data) || data == null || data.upgradeIds == null)
                continue;

            for (int i = 0; i < data.upgradeIds.Length; i++)
                AddUnique(upgradeIds, data.upgradeIds[i]);
        }

        return new RunDifficultySaveData
        {
            ascensionLevel = ascensionLevel,
            upgradeIds = upgradeIds.ToArray()
        };
    }

    private static RunDifficultySaveData CloneState(RunDifficultySaveData state)
    {
        if (state == null)
            return new RunDifficultySaveData();

        return new RunDifficultySaveData
        {
            ascensionLevel = Mathf.Max(0, state.ascensionLevel),
            upgradeIds = CloneStringArray(state.upgradeIds)
        };
    }

    private static string[] CloneStringArray(string[] source)
    {
        if (source == null || source.Length == 0)
            return Array.Empty<string>();

        string[] result = new string[source.Length];
        Array.Copy(source, result, source.Length);
        return result;
    }

    private static void AddUnique(List<string> values, string value)
    {
        if (values == null || string.IsNullOrEmpty(value) || values.Contains(value))
            return;

        values.Add(value);
    }
}

public static class AscensionSystem
{
    public static int MaxAscensionLevel
    {
        get
        {
            int maxLevel = 0;
            foreach (int level in GameDataDatabase.AscensionData.Keys)
            {
                if (level > maxLevel)
                    maxLevel = level;
            }
            return maxLevel;
        }
    }

    public static int SelectedAscensionLevel
    {
        get
        {
            if (!IsUnlocked())
                return 0;

            UnlockProgressData progress = UnlockProgressSaveSystem.LoadCurrent();
            int highestUnlocked = GetHighestUnlockedLevel(progress);
            return Mathf.Clamp(progress.selectedAscensionLevel, 0, highestUnlocked);
        }
        set
        {
            UnlockProgressData progress = UnlockProgressSaveSystem.LoadCurrent();
            int highestUnlocked = GetHighestUnlockedLevel(progress);
            progress.selectedAscensionLevel = Mathf.Clamp(value, 0, highestUnlocked);
            UnlockProgressSaveSystem.SaveCurrent(progress);
        }
    }

    public static int HighestUnlockedLevel => IsUnlocked() ? GetHighestUnlockedLevel(UnlockProgressSaveSystem.LoadCurrent()) : 0;
    public static int HighestClearedLevel => IsUnlocked() ? Mathf.Clamp(UnlockProgressSaveSystem.LoadCurrent().highestAscensionCleared, 0, MaxAscensionLevel) : 0;

    public static bool IsUnlocked()
    {
        return UnlockSystem.IsUnlocked(UnlockSystem.TargetFeature, "ascension");
    }

    public static bool IsLevelUnlocked(int level)
    {
        return level == 0 || (IsUnlocked() && level <= HighestUnlockedLevel);
    }

    private static int GetHighestUnlockedLevel(UnlockProgressData progress)
    {
        if (progress == null)
            return 0;

        int maxLevel = MaxAscensionLevel;
        int highestUnlocked = Mathf.Clamp(progress.highestAscensionUnlocked, 0, maxLevel);
        if (highestUnlocked <= 0 && IsUnlocked() && maxLevel > 0)
            highestUnlocked = 1;
        return highestUnlocked;
    }
}

public static class DifficultyUpgradeFactory
{
    public static DifficultyUpgrade Create(string upgradeId)
    {
        if (string.IsNullOrEmpty(upgradeId) || !GameDataDatabase.TryGetDifficultyUpgradeData(upgradeId, out DifficultyUpgradeData data) || data == null)
            return null;

        return new DataDrivenDifficultyUpgrade(data);
    }
}
