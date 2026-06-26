using System;

[Serializable]
public class MapGenConfigData : INumericDataRecord
{
    public int numericId;
    public string id;
    public string displayName;
    public int mapWidthMin = 9;
    public int mapWidthMax = 11;
    public int mapHeightMin = 9;
    public int mapHeightMax = 12;
    public int roomCountMin = 12;
    public int roomCountMax = 16;
    public int roomWidthMin = 1;
    public int roomWidthMax = 2;
    public int roomHeightMin = 1;
    public int roomHeightMax = 2;
    public int roomPlacementAttempts = 200;
    public int roomPadding = 1;
    public int extraConnectionChance = 18;
    public float minBossDepthRatio = 0.65f;
    public int bossCandidateTopPercent = 30;
    public bool preferDeadEndBoss = true;
    public int corridorEncounterChance = 12;
    public int maxCorridorEncounters = 3;
    public int corridorEncounterMinDepth = 2;
    public LevelType corridorEncounterLevelType = LevelType.Battle;
    public MapGenLevelTypeRuleData[] roomContentRules = Array.Empty<MapGenLevelTypeRuleData>();

    public int NumericId => numericId;
}

[Serializable]
public class MapGenLevelTypeRuleData
{
    public LevelType levelType;
    public int minCount;
    public int maxCount;
    public int weight = 1;
    public int minDepth;
    public int maxDepth = 999;
    public bool allowOnMainPath = true;
    public bool allowOnBranch = true;
    public bool allowOnDeadEnd = true;
}
