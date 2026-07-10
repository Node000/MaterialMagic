using System.Collections.Generic;
using System.Text;
using UnityEngine;

public static class AscensionUIUtility
{
    public static string FormatLevel(int level)
    {
        return "A" + Mathf.Max(0, level).ToString();
    }

    public static string GetLevelName(int level)
    {
        if (level <= 0)
            return LocalizationSystem.GetText("ascension.0.name", "A0 标准");

        if (GameDataDatabase.TryGetAscensionData(level, out AscensionData data) && data != null)
            return LocalizationSystem.GetText(data.nameKey, FormatLevel(level));
        return FormatLevel(level);
    }

    public static string GetLevelDescription(int level)
    {
        if (level <= 0)
            return LocalizationSystem.GetText("ascension.0.desc", "没有进阶词条，使用标准难度开始游戏。");

        if (GameDataDatabase.TryGetAscensionData(level, out AscensionData data) && data != null)
            return LocalizationSystem.GetText(data.descriptionKey, string.Empty);
        return string.Empty;
    }

    public static string BuildSelectorStatusText()
    {
        if (!AscensionSystem.IsUnlocked())
            return LocalizationSystem.GetText("ui.ascension.locked_hint", "首次通关后解锁进阶。");

        string format = LocalizationSystem.GetText("ui.ascension.selector_status", "已解锁至 {0} / 最高通关 {1}");
        return string.Format(format, FormatLevel(AscensionSystem.HighestUnlockedLevel), FormatLevel(AscensionSystem.HighestClearedLevel));
    }

    public static string BuildDetailBody(int level)
    {
        StringBuilder builder = new StringBuilder();
        string description = GetLevelDescription(level);
        if (!string.IsNullOrEmpty(description))
            builder.AppendLine(description);

        List<string> effectLines = BuildEffectLines(level);
        if (effectLines.Count == 0)
        {
            if (builder.Length > 0)
                builder.AppendLine();
            builder.Append(LocalizationSystem.GetText("ui.ascension.no_effects", "当前没有额外进阶效果。"));
            return builder.ToString();
        }

        if (builder.Length > 0)
            builder.AppendLine();
        builder.AppendLine(LocalizationSystem.GetText("ui.ascension.active_effects", "进阶词条："));
        for (int i = 0; i < effectLines.Count; i++)
        {
            builder.Append("• ");
            builder.AppendLine(effectLines[i]);
        }
        return builder.ToString();
    }

    public static UnifiedDetailContent BuildUnifiedDetailContent(int level, Sprite icon)
    {
        return new UnifiedDetailContent
        {
            SourceType = UnifiedDetailSourceType.None,
            Title = GetLevelName(level),
            Body = BuildDetailBody(level),
            AccentColor = Color.white,
            Icon = icon
        };
    }

    private static List<string> BuildEffectLines(int ascensionLevel)
    {
        RunDifficultySaveData state = DifficultyUpgradeSystem.BuildPreviewStateForAscension(ascensionLevel);
        List<string> lines = new List<string>();
        for (int i = 0; state != null && state.upgradeIds != null && i < state.upgradeIds.Length; i++)
        {
            string upgradeId = state.upgradeIds[i];
            if (string.IsNullOrEmpty(upgradeId) || !GameDataDatabase.TryGetDifficultyUpgradeData(upgradeId, out DifficultyUpgradeData data) || data == null)
                continue;

            string name = LocalizationSystem.GetText(data.nameKey, upgradeId);
            string description = LocalizationSystem.GetText(data.descriptionKey, string.Empty);
            lines.Add(string.IsNullOrEmpty(description) ? name : name + LocalizationSystem.GetText("ui.ascension.label_separator", "：") + description);
        }
        return lines;
    }
}
