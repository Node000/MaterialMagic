using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class MagicModifierIconEntry
{
    public string modifierId;
    public string iconName;
}

[Serializable]
public class MagicModifierIconTable
{
    public List<MagicModifierIconEntry> items = new List<MagicModifierIconEntry>();
}

public static class MagicModifierIconDatabase
{
    private const string IconRoot = "Images/Buffs/";
    private static readonly Dictionary<string, Sprite> IconByModifierId = new Dictionary<string, Sprite>();
    private static bool loaded;

    public static Sprite Get(string modifierId)
    {
        EnsureLoaded();
        if (string.IsNullOrEmpty(modifierId))
            return null;

        IconByModifierId.TryGetValue(modifierId, out Sprite sprite);
        return sprite;
    }

    public static Sprite Get(MagicModifierData data)
    {
        return data != null ? Get(data.id) : null;
    }

    public static Sprite Get(MagicModifierModel modifier)
    {
        return modifier != null ? Get(modifier.Id) : null;
    }

    private static void EnsureLoaded()
    {
        if (loaded)
            return;

        loaded = true;
        TextAsset asset = GameDataReader.LoadTextAsset("Data/MagicModifierIconData");
        MagicModifierIconTable table = asset != null ? JsonUtility.FromJson<MagicModifierIconTable>(asset.text) : null;
        if (table == null || table.items == null)
            return;

        for (int i = 0; i < table.items.Count; i++)
        {
            MagicModifierIconEntry entry = table.items[i];
            if (entry == null || string.IsNullOrEmpty(entry.modifierId) || string.IsNullOrEmpty(entry.iconName))
                continue;

            Sprite icon = Resources.Load<Sprite>(IconRoot + entry.iconName);
            if (icon != null)
                IconByModifierId[entry.modifierId] = icon;
        }
    }
}
