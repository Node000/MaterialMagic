using System;
using System.IO;
using UnityEngine;

public static class MagicCodexProgressSystem
{
    private const int CurrentVersion = 1;

    private static int cachedSlotIndex = -1;
    private static MagicCodexProgressData cachedCurrentProgress;

    public static bool IsMagicDiscovered(MagicData data)
    {
        string key = GetMagicKey(data);
        if (string.IsNullOrEmpty(key))
            return false;

        MagicCodexProgressData progress = LoadCurrent();
        return Contains(progress.discoveredMagicIds, key);
    }

    public static bool IsMagicNew(MagicData data)
    {
        string key = GetMagicKey(data);
        if (string.IsNullOrEmpty(key))
            return false;

        MagicCodexProgressData progress = LoadCurrent();
        return Contains(progress.discoveredMagicIds, key) && !Contains(progress.seenNewMagicIds, key);
    }

    public static bool MarkMagicDiscovered(MagicData data)
    {
        string key = GetMagicKey(data);
        if (string.IsNullOrEmpty(key))
            return false;

        MagicCodexProgressData progress = LoadCurrent();
        if (Contains(progress.discoveredMagicIds, key))
            return false;

        progress.discoveredMagicIds = AddUnique(progress.discoveredMagicIds, key);
        SaveCurrent(progress);
        return true;
    }

    public static void MarkMagicSeen(MagicData data)
    {
        string key = GetMagicKey(data);
        if (string.IsNullOrEmpty(key))
            return;

        MagicCodexProgressData progress = LoadCurrent();
        progress.discoveredMagicIds = AddUnique(progress.discoveredMagicIds, key);
        progress.seenNewMagicIds = AddUnique(progress.seenNewMagicIds, key);
        SaveCurrent(progress);
    }

    public static void Clear(int slotIndex)
    {
        int clampedSlotIndex = Mathf.Clamp(slotIndex, 1, 3);
        string path = GetSavePath(clampedSlotIndex);
        if (File.Exists(path))
            File.Delete(path);
        if (cachedSlotIndex == clampedSlotIndex)
            cachedCurrentProgress = CreateEmpty(clampedSlotIndex);
    }

    private static MagicCodexProgressData LoadCurrent()
    {
        int slotIndex = Mathf.Clamp(RunSaveSystem.CurrentSlotIndex, 1, 3);
        if (cachedCurrentProgress != null && cachedSlotIndex == slotIndex)
            return cachedCurrentProgress;

        cachedCurrentProgress = Load(slotIndex);
        cachedSlotIndex = slotIndex;
        return cachedCurrentProgress;
    }

    private static MagicCodexProgressData Load(int slotIndex)
    {
        int clampedSlotIndex = Mathf.Clamp(slotIndex, 1, 3);
        string path = GetSavePath(clampedSlotIndex);
        if (!File.Exists(path))
            return CreateEmpty(clampedSlotIndex);

        string json = File.ReadAllText(path);
        MagicCodexProgressData data = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<MagicCodexProgressData>(json);
        if (data == null)
            return CreateEmpty(clampedSlotIndex);

        data.version = CurrentVersion;
        data.slotIndex = clampedSlotIndex;
        if (data.discoveredMagicIds == null)
            data.discoveredMagicIds = Array.Empty<string>();
        if (data.seenNewMagicIds == null)
            data.seenNewMagicIds = Array.Empty<string>();
        return data;
    }

    private static void SaveCurrent(MagicCodexProgressData data)
    {
        Save(RunSaveSystem.CurrentSlotIndex, data);
    }

    private static void Save(int slotIndex, MagicCodexProgressData data)
    {
        if (data == null)
            return;

        int clampedSlotIndex = Mathf.Clamp(slotIndex, 1, 3);
        data.version = CurrentVersion;
        data.slotIndex = clampedSlotIndex;
        Directory.CreateDirectory(RunSaveSystem.SaveFolderPath);
        string path = GetSavePath(clampedSlotIndex);
        string tempPath = path + ".tmp";
        File.WriteAllText(tempPath, JsonUtility.ToJson(data, true));
        if (File.Exists(path))
            File.Delete(path);
        File.Move(tempPath, path);
        cachedSlotIndex = clampedSlotIndex;
        cachedCurrentProgress = data;
    }

    private static string GetSavePath(int slotIndex)
    {
        return Path.Combine(RunSaveSystem.SaveFolderPath, $"magic_codex_slot_{Mathf.Clamp(slotIndex, 1, 3)}.json");
    }

    private static MagicCodexProgressData CreateEmpty(int slotIndex)
    {
        return new MagicCodexProgressData
        {
            version = CurrentVersion,
            slotIndex = Mathf.Clamp(slotIndex, 1, 3),
            discoveredMagicIds = Array.Empty<string>(),
            seenNewMagicIds = Array.Empty<string>()
        };
    }

    private static string GetMagicKey(MagicData data)
    {
        if (data == null)
            return null;
        if (!string.IsNullOrEmpty(data.id))
            return data.id;
        return data.numericId > 0 ? data.numericId.ToString() : null;
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

    private static string[] AddUnique(string[] values, string value)
    {
        if (string.IsNullOrEmpty(value) || Contains(values, value))
            return values ?? Array.Empty<string>();

        int length = values != null ? values.Length : 0;
        string[] result = new string[length + 1];
        for (int i = 0; i < length; i++)
            result[i] = values[i];
        result[length] = value;
        return result;
    }
}

[Serializable]
public class MagicCodexProgressData
{
    public int version = 1;
    public int slotIndex = 1;
    public string[] discoveredMagicIds = Array.Empty<string>();
    public string[] seenNewMagicIds = Array.Empty<string>();
}
