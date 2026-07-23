using System.IO;
using UnityEngine;

public static class UnlockProgressSaveSystem
{
    private const int CurrentVersion = 1;

    public static string GetUnlockSavePath(int slotIndex)
    {
        return Path.Combine(RunSaveSystem.SaveFolderPath, $"unlock_slot_{Mathf.Clamp(slotIndex, 1, 3)}.json");
    }

    public static UnlockProgressData LoadCurrent()
    {
        return Load(RunSaveSystem.CurrentSlotIndex);
    }

    public static UnlockProgressData Load(int slotIndex)
    {
        int clampedSlotIndex = Mathf.Clamp(slotIndex, 1, 3);
        string path = GetUnlockSavePath(clampedSlotIndex);
        if (!File.Exists(path))
            return CreateEmpty(clampedSlotIndex);

        string json = File.ReadAllText(path);
        UnlockProgressData data = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<UnlockProgressData>(json);
        if (data == null)
            return CreateEmpty(clampedSlotIndex);

        data.version = CurrentVersion;
        data.slotIndex = clampedSlotIndex;
        if (data.unlockedIds == null)
            data.unlockedIds = System.Array.Empty<string>();
        if (data.startConfigNormalEndCounts == null)
            data.startConfigNormalEndCounts = System.Array.Empty<UnlockCounterData>();
        if (data.startConfigVictoryCounts == null)
            data.startConfigVictoryCounts = System.Array.Empty<UnlockCounterData>();
        if (data.startConfigDefeatCounts == null)
            data.startConfigDefeatCounts = System.Array.Empty<UnlockCounterData>();
        if (data.creditedRunIds == null)
            data.creditedRunIds = System.Array.Empty<string>();
        if (data.pendingUnlockMessages == null)
            data.pendingUnlockMessages = System.Array.Empty<UnlockPendingMessageData>();
        NormalizeAscensionProgress(data);
        return data;
    }

    public static void SaveCurrent(UnlockProgressData data)
    {
        Save(RunSaveSystem.CurrentSlotIndex, data);
    }

    public static void Save(int slotIndex, UnlockProgressData data)
    {
        if (data == null)
            return;

        int clampedSlotIndex = Mathf.Clamp(slotIndex, 1, 3);
        data.version = CurrentVersion;
        data.slotIndex = clampedSlotIndex;
        Directory.CreateDirectory(RunSaveSystem.SaveFolderPath);
        string path = GetUnlockSavePath(clampedSlotIndex);
        string tempPath = path + ".tmp";
        File.WriteAllText(tempPath, JsonUtility.ToJson(data, true));
        if (File.Exists(path))
            File.Delete(path);
        File.Move(tempPath, path);
    }

    public static void Clear(int slotIndex)
    {
        string path = GetUnlockSavePath(slotIndex);
        if (File.Exists(path))
            File.Delete(path);
    }

    private static UnlockProgressData CreateEmpty(int slotIndex)
    {
        UnlockProgressData data = new UnlockProgressData
        {
            version = CurrentVersion,
            slotIndex = Mathf.Clamp(slotIndex, 1, 3)
        };
        NormalizeAscensionProgress(data);
        return data;
    }

    private static void NormalizeAscensionProgress(UnlockProgressData data)
    {
        if (data == null)
            return;

        int maxLevel = GetMaxAscensionLevel();
        data.highestAscensionUnlocked = Mathf.Clamp(data.highestAscensionUnlocked, 0, maxLevel);
        data.highestAscensionCleared = Mathf.Clamp(data.highestAscensionCleared, 0, maxLevel);
        if (data.highestAscensionCleared > data.highestAscensionUnlocked)
            data.highestAscensionCleared = data.highestAscensionUnlocked;

        string ascensionUnlockKey = UnlockSystem.GetUnlockKey(UnlockSystem.TargetFeature, "ascension");
        bool ascensionUnlocked = Contains(data.unlockedIds, ascensionUnlockKey);
        if (data.highestAscensionUnlocked <= 0 && ascensionUnlocked && maxLevel > 0)
            data.highestAscensionUnlocked = 1;

        int minSelectedLevel = ascensionUnlocked ? AscensionSystem.MinAscensionLevel : 0;
        data.selectedAscensionLevel = Mathf.Clamp(data.selectedAscensionLevel, minSelectedLevel, data.highestAscensionUnlocked);
    }

    private static int GetMaxAscensionLevel()
    {
        int maxLevel = 0;
        foreach (int level in GameDataDatabase.AscensionData.Keys)
        {
            if (level > maxLevel)
                maxLevel = level;
        }
        return maxLevel;
    }

    private static bool Contains(string[] values, string value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        for (int i = 0; values != null && i < values.Length; i++)
        {
            if (values[i] == value)
                return true;
        }
        return false;
    }
}
