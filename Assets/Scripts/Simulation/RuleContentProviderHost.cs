using System;

/// <summary>Creates detached content for a new world; the capture gate never crosses a yield/await.</summary>
public static class RuleContentProviderHost
{
    [ThreadStatic] private static bool capturing;
    internal static bool IsCapturing => capturing;
    public static IRuleContentProvider CaptureForNewWorld()
    {
        if (capturing) return null;
        capturing = true;
        try
        {
            var modifiers = new System.Collections.Generic.Dictionary<string, MaterialModifierData>();
            foreach (MaterialModifierData modifier in MaterialModifierDatabase.RuntimeData)
                if (modifier != null && !string.IsNullOrEmpty(modifier.id)) modifiers[modifier.id] = modifier;
            ArrowUpgradeConfig arrowConfig = UnityEngine.Resources.Load<ArrowUpgradeConfig>("Config/ArrowUpgradeConfig");
            var arrowValues = new System.Collections.Generic.Dictionary<string, RuleArrowUpgradeValue>();
            foreach (ArrowUpgradeNodeDefinition node in ArrowUpgradeSystem.Nodes)
            {
                int primary = arrowConfig != null ? arrowConfig.GetPrimaryValue(node.Id, int.MinValue) : int.MinValue;
                int secondary = arrowConfig != null ? arrowConfig.GetSecondaryValue(node.Id, int.MinValue) : int.MinValue;
                if (primary != int.MinValue || secondary != int.MinValue) arrowValues[node.Id] = new RuleArrowUpgradeValue(primary, secondary);
            }
            EnemySummonLayoutConfig layout = UnityEngine.Resources.Load<EnemySummonLayoutConfig>("Config/EnemySummonLayoutConfig");
            RuleSummonLayout summon = layout != null
                ? new RuleSummonLayout(layout.HorizontalSpacing, layout.VerticalSpacing, layout.OccupiedRadius, layout.SameRowSlotCount, layout.MaxSearchAttempts)
                : new RuleSummonLayout();
            return new RuleContentSnapshot(GameDataDatabase.MagicRarityData, new RuleConfiguration(summon, arrowValues))
                .AddNamed(modifiers)
                .AddNumeric(GameDataDatabase.RelicData).AddNamed(GameDataDatabase.RelicPoolData)
                .AddNumeric(GameDataDatabase.MagicData).AddNumeric(GameDataDatabase.EnemyData)
                .AddNumeric(GameDataDatabase.EventData).AddNumeric(GameDataDatabase.LevelData)
                .AddNumeric(GameDataDatabase.RewardPoolData).AddNumeric(GameDataDatabase.BonusLevelData)
                .AddNumeric(GameDataDatabase.ChapterData).AddNumeric(GameDataDatabase.MapGenConfigData)
                .AddNumeric(GameDataDatabase.EconomyConfigData).AddNumeric(GameDataDatabase.ShopProductPoolData)
                .AddNumeric(GameDataDatabase.AscensionData).AddNamed(GameDataDatabase.DifficultyUpgradeData)
                .AddNamed(GameDataDatabase.TagData).AddNamed(GameDataDatabase.MagicModifierData)
                .AddNamed(GameDataDatabase.PlayerStartConfigData).Freeze();
        }
        finally { capturing = false; }
    }
}
