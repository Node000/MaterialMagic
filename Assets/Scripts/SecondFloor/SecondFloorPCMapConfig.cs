using System;
using UnityEngine;

[CreateAssetMenu(fileName = "SecondFloorPCMapConfig", menuName = "Config/Second Floor PC Map")]
public class SecondFloorPCMapConfig : ScriptableObject
{
    [Header("区域")]
    [SerializeField, Min(5)] private int regionWidth = 12;
    [SerializeField, Min(5)] private int regionHeight = 11;
    [SerializeField, Min(1)] private int stripsPerRegion = 6;

    [Header("普通格内容")]
    [SerializeField] private StripDungeonContentRule[] contentRules =
    {
        new StripDungeonContentRule { levelType = LevelType.Battle, minCount = 0, maxCount = 0, weight = 8 },
        new StripDungeonContentRule { levelType = LevelType.Event, minCount = 0, maxCount = 0, weight = 4 },
        new StripDungeonContentRule { levelType = LevelType.Elite, minCount = 0, maxCount = 0, weight = 2 },
        new StripDungeonContentRule { levelType = LevelType.Shop, minCount = 0, maxCount = 0, weight = 2 },
        new StripDungeonContentRule { levelType = LevelType.Rest, minCount = 0, maxCount = 0, weight = 2 },
        new StripDungeonContentRule { levelType = LevelType.Reward, minCount = 0, maxCount = 0, weight = 3 },
        new StripDungeonContentRule { levelType = LevelType.AddMaterial, minCount = 0, maxCount = 0, weight = 1 },
        new StripDungeonContentRule { levelType = LevelType.RemoveMaterial, minCount = 0, maxCount = 0, weight = 1 }
    };

    public int RegionWidth => regionWidth;
    public int RegionHeight => regionHeight;
    public int StripsPerRegion => stripsPerRegion;
    public int MapWidth => regionWidth * 2 + 1;
    public int MapHeight => regionHeight * 2 + 1;
    public StripDungeonContentRule[] ContentRules => contentRules;

    public bool TryValidate(out string error)
    {
        if (regionWidth < 5 || regionHeight < 5)
        {
            error = "每个区域至少需要 5×5 格。";
            return false;
        }

        if (stripsPerRegion != 6)
        {
            error = "当前四分区地图固定每区 6 条带，共 24 条带。";
            return false;
        }

        if (contentRules == null || contentRules.Length == 0)
        {
            error = "至少需要配置一种普通格内容。";
            return false;
        }

        int totalWeight = 0;
        for (int i = 0; i < contentRules.Length; i++)
        {
            StripDungeonContentRule rule = contentRules[i];
            if (rule == null || rule.weight <= 0)
            {
                error = "普通格内容权重必须大于零。";
                return false;
            }
            totalWeight += rule.weight;
        }

        if (totalWeight <= 0)
        {
            error = "普通格内容总权重必须大于零。";
            return false;
        }

        error = null;
        return true;
    }
}
