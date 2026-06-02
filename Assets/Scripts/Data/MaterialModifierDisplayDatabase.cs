using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class MaterialModifierData : IDataRecord
{
    public string id;
    public string script;
    public string nameKey;
    public string descriptionKey;
    public string lineColor;

    public string Id => id;
}

public static class MaterialModifierDisplayDatabase
{
    private static readonly Dictionary<string, MaterialModifierData> DataByScript = new Dictionary<string, MaterialModifierData>();
    private static bool loaded;

    public static bool TryGetName(object modifier, out string name)
    {
        if (TryGetData(modifier, out MaterialModifierData data) && !string.IsNullOrEmpty(data.nameKey))
        {
            name = LocalizationSystem.GetText(data.nameKey, modifier.GetType().Name);
            return true;
        }

        name = string.Empty;
        return false;
    }

    public static bool TryGetDescription(object modifier, out string description)
    {
        if (TryGetData(modifier, out MaterialModifierData data) && !string.IsNullOrEmpty(data.descriptionKey))
        {
            description = LocalizationSystem.GetText(data.descriptionKey, string.Empty);
            return true;
        }

        description = string.Empty;
        return false;
    }

    public static bool TryGetLineColor(object modifier, out Color color)
    {
        if (TryGetData(modifier, out MaterialModifierData data) && TryParseColor(data.lineColor, out color))
            return true;

        color = Color.white;
        return false;
    }

    private static bool TryGetData(object modifier, out MaterialModifierData data)
    {
        EnsureLoaded();
        if (modifier == null)
        {
            data = null;
            return false;
        }

        return DataByScript.TryGetValue(modifier.GetType().Name, out data);
    }

    private static void EnsureLoaded()
    {
        if (loaded)
            return;

        loaded = true;
        DataTable<MaterialModifierData> table = GameDataReader.LoadTable<MaterialModifierData>("MaterialModifierData");
        if (table == null || table.items == null)
            return;

        for (int i = 0; i < table.items.Count; i++)
        {
            MaterialModifierData entry = table.items[i];
            if (entry == null || string.IsNullOrEmpty(entry.script))
                continue;

            DataByScript[entry.script] = entry;
        }
    }

    private static bool TryParseColor(string value, out Color color)
    {
        if (string.IsNullOrEmpty(value))
        {
            color = Color.white;
            return false;
        }

        if (value[0] != '#')
            value = "#" + value;

        return ColorUtility.TryParseHtmlString(value, out color);
    }
}
