using System;

[Serializable]
public class UnlockData : IDataRecord
{
    public string id;
    public string targetType;
    public string targetId;
    public UnlockConditionData[] conditions = Array.Empty<UnlockConditionData>();
    public string messageKey;

    public string Id => id;
}

[Serializable]
public class UnlockConditionData
{
    public string type;
    public string targetId;
    public int value;
}

[Serializable]
public class UnlockProgressData
{
    public int version = 1;
    public int slotIndex = 1;
    public string[] unlockedIds = Array.Empty<string>();
    public int normalEndCount;
    public int victoryCount;
    public int nonTutorialVictoryCount;
    public int defeatCount;
    public UnlockCounterData[] startConfigNormalEndCounts = Array.Empty<UnlockCounterData>();
    public UnlockCounterData[] startConfigVictoryCounts = Array.Empty<UnlockCounterData>();
    public UnlockCounterData[] startConfigDefeatCounts = Array.Empty<UnlockCounterData>();
    public string[] creditedRunIds = Array.Empty<string>();
    public UnlockPendingMessageData[] pendingUnlockMessages = Array.Empty<UnlockPendingMessageData>();
}

[Serializable]
public class UnlockCounterData
{
    public string id;
    public int count;
}

[Serializable]
public class UnlockPendingMessageData
{
    public string targetType;
    public string targetId;
    public string messageKey;
}
