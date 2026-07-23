using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class BuffIconEntry
{
    public BuffEnum buffType;
    public string iconName;
}

[Serializable]
public class BuffIconTable
{
    public List<BuffIconEntry> items = new List<BuffIconEntry>();
}

public class BuffDisplayData
{
    public string Name;
    public string Description;
    public Sprite Icon;
    public Color FallbackColor;
}

public static class BuffDisplayDatabase
{
    private static readonly Dictionary<BuffEnum, BuffDisplayData> DataByType = new Dictionary<BuffEnum, BuffDisplayData>();
    private static bool loaded;

    public static BuffDisplayData Get(BuffEnum buffType)
    {
        EnsureLoaded();
        if (DataByType.TryGetValue(buffType, out BuffDisplayData data))
            return data;

        data = CreateDefault(buffType, null);
        DataByType[buffType] = data;
        return data;
    }

    private static void EnsureLoaded()
    {
        if (loaded)
            return;

        loaded = true;
        LocalizationSystem.LanguageChanged += RefreshLocalizedText;
        TextAsset asset = GameDataReader.LoadTextAsset("Data/BuffIconData");
        BuffIconTable table = asset != null ? JsonUtility.FromJson<BuffIconTable>(asset.text) : null;
        if (table != null && table.items != null)
        {
            for (int i = 0; i < table.items.Count; i++)
            {
                BuffIconEntry entry = table.items[i];
                if (entry == null || entry.buffType == BuffEnum.None)
                    continue;

                Sprite icon = LoadIcon(entry.iconName);

                DataByType[entry.buffType] = CreateDefault(entry.buffType, icon);
            }
        }
    }

    private static void RefreshLocalizedText()
    {
        foreach (KeyValuePair<BuffEnum, BuffDisplayData> pair in DataByType)
        {
            BuffDisplayData data = pair.Value;
            data.Name = LocalizationKeys.GetBuffName(pair.Key);
            data.Description = LocalizationKeys.GetBuffDescription(pair.Key);
        }
    }

    private static Sprite LoadIcon(string iconName)
    {
        if (string.IsNullOrEmpty(iconName))
            return null;

        const string magicIconPrefix = "magic/";
        if (iconName.StartsWith(magicIconPrefix, StringComparison.Ordinal))
            return Resources.Load<Sprite>("Images/Magics/" + iconName.Substring(magicIconPrefix.Length));

        return Resources.Load<Sprite>("Images/Buffs/" + iconName);
    }

    private static BuffDisplayData CreateDefault(BuffEnum buffType, Sprite icon)
    {
        if (icon == null)
            icon = LoadDefaultIcon(buffType);

        return new BuffDisplayData
        {
            Name = LocalizationKeys.GetBuffName(buffType),
            Description = LocalizationKeys.GetBuffDescription(buffType),
            Icon = icon,
            FallbackColor = GetFallbackColor(buffType)
        };
    }

    private static Sprite LoadDefaultIcon(BuffEnum buffType)
    {
        switch (BuffModel.GetKind(buffType))
        {
            case BuffKindEnum.Buff:
                return Resources.Load<Sprite>("Images/Buffs/yellowStar");
            case BuffKindEnum.DeBuff:
                return Resources.Load<Sprite>("Images/Buffs/redStar");
            default:
                return null;
        }
    }

    private static Color GetFallbackColor(BuffEnum buffType)
    {
        switch (BuffModel.GetKind(buffType))
        {
            case BuffKindEnum.Buff:
                return new Color(0.25f, 0.55f, 1f, 1f);
            case BuffKindEnum.DeBuff:
                return new Color(0.85f, 0.22f, 0.16f, 1f);
            default:
                return new Color(0.55f, 0.55f, 0.58f, 1f);
        }
    }
}
