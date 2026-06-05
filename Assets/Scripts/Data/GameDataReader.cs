using System;
using System.Collections.Generic;
using UnityEngine;

public interface IDataRecord
{
    string Id { get; }
}

public interface INumericDataRecord
{
    int NumericId { get; }
}

[Serializable]
public class DataTable<T>
{
    public List<T> items = new List<T>();
}

public static class GameDataReader
{
    private const string DataRoot = "Data/";
    private static readonly Dictionary<string, TextAsset> TextAssetCache = new Dictionary<string, TextAsset>();

    public static DataTable<T> LoadTable<T>(string tablePath)
    {
        TextAsset asset = LoadTextAsset(DataRoot + tablePath);
        if (asset == null)
            return new DataTable<T>();

        DataTable<T> table = JsonUtility.FromJson<DataTable<T>>(asset.text);
        return table ?? new DataTable<T>();
    }

    public static Dictionary<string, T> LoadDictionary<T>(string tablePath) where T : IDataRecord
    {
        DataTable<T> table = LoadTable<T>(tablePath);
        Dictionary<string, T> dictionary = new Dictionary<string, T>(table.items.Count);

        for (int i = 0; i < table.items.Count; i++)
        {
            T item = table.items[i];
            if (item == null || string.IsNullOrEmpty(item.Id))
                continue;

            dictionary[item.Id] = item;
        }

        return dictionary;
    }

    public static Dictionary<int, T> LoadNumericDictionary<T>(string tablePath) where T : INumericDataRecord
    {
        DataTable<T> table = LoadTable<T>(tablePath);
        Dictionary<int, T> dictionary = new Dictionary<int, T>(table.items.Count);

        for (int i = 0; i < table.items.Count; i++)
        {
            T item = table.items[i];
            AddNumericItem(dictionary, item);
        }

        return dictionary;
    }

    public static Dictionary<int, T> LoadNumericDictionary<T>(string tablePath, string folderPath) where T : INumericDataRecord
    {
        Dictionary<int, T> dictionary = LoadNumericDictionary<T>(tablePath);
        TextAsset[] assets = Resources.LoadAll<TextAsset>(DataRoot + folderPath);
        for (int i = 0; i < assets.Length; i++)
        {
            TextAsset asset = assets[i];
            if (asset == null)
                continue;

            if (!LooksLikeJsonObject(asset.text))
                continue;

            T item = JsonUtility.FromJson<T>(asset.text);
            AddNumericItem(dictionary, item);
        }

        return dictionary;
    }

    private static bool LooksLikeJsonObject(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        for (int i = 0; i < text.Length; i++)
        {
            if (!char.IsWhiteSpace(text[i]))
                return text[i] == '{';
        }

        return false;
    }

    private static void AddNumericItem<T>(Dictionary<int, T> dictionary, T item) where T : INumericDataRecord
    {
        if (item == null || item.NumericId <= 0)
            return;

        dictionary[item.NumericId] = item;
    }

    public static TextAsset LoadTextAsset(string path)
    {
        if (TextAssetCache.TryGetValue(path, out TextAsset cachedAsset))
            return cachedAsset;

        TextAsset asset = Resources.Load<TextAsset>(path);
        TextAssetCache[path] = asset;
        return asset;
    }

    public static void ClearCache()
    {
        TextAssetCache.Clear();
    }
}

public static class GameDataDatabase
{
    private static Dictionary<int, MagicData> magicData;
    private static Dictionary<int, EnemyData> enemyData;
    private static Dictionary<int, EventData> eventData;
    private static Dictionary<int, LevelData> levelData;
    private static Dictionary<int, RewardPoolData> rewardPoolData;
    private static Dictionary<int, BonusLevelData> bonusLevelData;
    private static Dictionary<int, ChapterData> chapterData;
    private static Dictionary<int, EconomyConfigData> economyConfigData;
    private static Dictionary<string, TagData> tagData;
    private static Dictionary<string, MagicModifierData> magicModifierData;
    private static Dictionary<string, PlayerStartConfigData> playerStartConfigData;

    public static IReadOnlyDictionary<int, MagicData> MagicData => magicData ??= GameDataReader.LoadNumericDictionary<MagicData>("MagicData");
    public static IReadOnlyDictionary<int, EnemyData> EnemyData => enemyData ??= GameDataReader.LoadNumericDictionary<EnemyData>("EnemyData", "Enemies");
    public static IReadOnlyDictionary<int, EventData> EventData => eventData ??= GameDataReader.LoadNumericDictionary<EventData>("EventData");
    public static IReadOnlyDictionary<int, LevelData> LevelData => levelData ??= GameDataReader.LoadNumericDictionary<LevelData>("LevelData");
    public static IReadOnlyDictionary<int, RewardPoolData> RewardPoolData => rewardPoolData ??= GameDataReader.LoadNumericDictionary<RewardPoolData>("RewardPoolData");
    public static IReadOnlyDictionary<int, BonusLevelData> BonusLevelData => bonusLevelData ??= GameDataReader.LoadNumericDictionary<BonusLevelData>("BonusLevelData");
    public static IReadOnlyDictionary<int, ChapterData> ChapterData => chapterData ??= GameDataReader.LoadNumericDictionary<ChapterData>("ChapterData");
    public static IReadOnlyDictionary<int, EconomyConfigData> EconomyConfigData => economyConfigData ??= GameDataReader.LoadNumericDictionary<EconomyConfigData>("EconomyConfig");
    public static IReadOnlyDictionary<string, TagData> TagData => tagData ??= GameDataReader.LoadDictionary<TagData>("TagData");
    public static IReadOnlyDictionary<string, MagicModifierData> MagicModifierData => magicModifierData ??= GameDataReader.LoadDictionary<MagicModifierData>("MagicModifierData");
    public static IReadOnlyDictionary<string, PlayerStartConfigData> PlayerStartConfigData => playerStartConfigData ??= GameDataReader.LoadDictionary<PlayerStartConfigData>("StartConfig");

    public static bool TryGetMagicData(int id, out MagicData data)
    {
        return MagicData.TryGetValue(id, out data);
    }

    public static bool TryGetEnemyData(int id, out EnemyData data)
    {
        return EnemyData.TryGetValue(id, out data);
    }

    public static bool TryGetEventData(int id, out EventData data)
    {
        return EventData.TryGetValue(id, out data);
    }

    public static bool TryGetLevelData(int id, out LevelData data)
    {
        return LevelData.TryGetValue(id, out data);
    }

    public static bool TryGetRewardPoolData(int id, out RewardPoolData data)
    {
        return RewardPoolData.TryGetValue(id, out data);
    }

    public static bool TryGetBonusLevelData(int id, out BonusLevelData data)
    {
        return BonusLevelData.TryGetValue(id, out data);
    }

    public static bool TryGetChapterData(int id, out ChapterData data)
    {
        return ChapterData.TryGetValue(id, out data);
    }

    public static bool TryGetEconomyConfigData(int id, out EconomyConfigData data)
    {
        return EconomyConfigData.TryGetValue(id, out data);
    }

    public static EconomyConfigData GetDefaultEconomyConfig()
    {
        if (TryGetEconomyConfigData(1, out EconomyConfigData data))
            return data;

        foreach (EconomyConfigData value in EconomyConfigData.Values)
        {
            if (value != null)
                return value;
        }
        return null;
    }

    public static bool TryGetTagData(string id, out TagData data)
    {
        return TagData.TryGetValue(id, out data);
    }

    public static bool TryGetMagicModifierData(string id, out MagicModifierData data)
    {
        return MagicModifierData.TryGetValue(id, out data);
    }

    public static bool TryGetPlayerStartConfigData(string id, out PlayerStartConfigData data)
    {
        return PlayerStartConfigData.TryGetValue(id, out data);
    }

    public static void ClearCache()
    {
        magicData = null;
        enemyData = null;
        eventData = null;
        levelData = null;
        rewardPoolData = null;
        bonusLevelData = null;
        chapterData = null;
        economyConfigData = null;
        tagData = null;
        magicModifierData = null;
        playerStartConfigData = null;
        GameDataReader.ClearCache();
    }
}
