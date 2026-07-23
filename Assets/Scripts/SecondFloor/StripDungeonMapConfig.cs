using System;
using UnityEngine;

[CreateAssetMenu(fileName = "StripDungeonMapConfig", menuName = "Config/Second Floor Strip Dungeon Map")]
public class StripDungeonMapConfig : ScriptableObject
{
    [Header("地图尺寸")]
    [SerializeField] private int mapWidth = 18;
    [SerializeField] private int mapHeight = 14;
    [SerializeField] private int stripCountMin = 6;
    [SerializeField] private int stripCountMax = 8;
    [SerializeField] private int[] stripLengths = { 6, 7, 8, 9 };
    [SerializeField] private int maxGenerationAttempts = 48;

    [Header("Boss")]
    [SerializeField] private int bossMinDistanceFromStart = 5;

    [Header("普通内容")]
    [SerializeField] private StripDungeonContentRule[] contentRules =
    {
        new StripDungeonContentRule { levelType = LevelType.Battle, minCount = 3, maxCount = 5, weight = 8 },
        new StripDungeonContentRule { levelType = LevelType.Event, minCount = 1, maxCount = 2, weight = 4 },
        new StripDungeonContentRule { levelType = LevelType.Elite, minCount = 1, maxCount = 2, weight = 3 },
        new StripDungeonContentRule { levelType = LevelType.Shop, minCount = 1, maxCount = 1, weight = 2 },
        new StripDungeonContentRule { levelType = LevelType.Rest, minCount = 1, maxCount = 1, weight = 2 },
        new StripDungeonContentRule { levelType = LevelType.Reward, minCount = 1, maxCount = 2, weight = 3 },
        new StripDungeonContentRule { levelType = LevelType.AddMaterial, minCount = 0, maxCount = 1, weight = 1 },
        new StripDungeonContentRule { levelType = LevelType.RemoveMaterial, minCount = 0, maxCount = 1, weight = 1 }
    };

    public int MapWidth => mapWidth;
    public int MapHeight => mapHeight;
    public int StripCountMin => stripCountMin;
    public int StripCountMax => stripCountMax;
    public int[] StripLengths => stripLengths;
    public int MaxGenerationAttempts => maxGenerationAttempts;
    public int BossMinDistanceFromStart => bossMinDistanceFromStart;
    public StripDungeonContentRule[] ContentRules => contentRules;

    public bool TryValidate(out string error)
    {
        if (mapWidth < 7 || mapHeight < 7)
        {
            error = "地图宽高至少需要为 7，才能放置带自由端点的环。";
            return false;
        }

        if (stripCountMin < 4 || stripCountMax < stripCountMin)
        {
            error = "条带数量范围无效；最少需要 4 条条带形成环。";
            return false;
        }

        if (stripLengths == null || stripLengths.Length == 0)
        {
            error = "至少需要配置一个条带长度。";
            return false;
        }

        bool hasHorizontalLoopLength = false;
        bool hasVerticalLoopLength = false;
        for (int i = 0; i < stripLengths.Length; i++)
        {
            int length = stripLengths[i];
            if (length >= 5 && length <= mapWidth)
                hasHorizontalLoopLength = true;
            if (length >= 5 && length <= mapHeight)
                hasVerticalLoopLength = true;
        }

        if (!hasHorizontalLoopLength || !hasVerticalLoopLength)
        {
            error = "条带长度中必须各有一个可用于横向和纵向环的长度（至少为 5）。";
            return false;
        }

        if (contentRules == null || contentRules.Length == 0)
        {
            error = "至少需要配置一种普通内容规则。";
            return false;
        }

        for (int i = 0; i < contentRules.Length; i++)
        {
            StripDungeonContentRule rule = contentRules[i];
            if (rule == null || rule.minCount < 0 || rule.maxCount < rule.minCount || rule.weight <= 0)
            {
                error = "普通内容规则存在无效的数量范围或权重。";
                return false;
            }

            for (int j = i + 1; j < contentRules.Length; j++)
            {
                if (contentRules[j] != null && contentRules[j].levelType == rule.levelType)
                {
                    error = "普通内容规则不能重复配置同一种关卡类型。";
                    return false;
                }
            }
        }

        error = null;
        return true;
    }
}

[Serializable]
public class StripDungeonContentRule
{
    public LevelType levelType;
    [Min(0)] public int minCount;
    [Min(0)] public int maxCount;
    [Min(1)] public int weight = 1;
}
