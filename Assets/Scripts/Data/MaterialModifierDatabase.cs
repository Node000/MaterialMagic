using System.Collections.Generic;
using UnityEngine;

public static class MaterialModifierDatabase
{
    private static IRuleContentProvider Content => RuleContentProviderHost.IsCapturing ? null : RuleExecutionScope.Current?.Content;
    private const string ResourcePath = "EnchantConfig/MaterialModifiers";

    private static Dictionary<string, MaterialModifierDefinition> definitionsById;
    private static Dictionary<string, MaterialModifierDefinition> definitionsByScript;
    private static Dictionary<string, MaterialModifierData> dataById;
    private static Dictionary<string, MaterialModifierData> dataByScript;
    private static List<MaterialModifierData> runtimeData;

    public static IReadOnlyList<MaterialModifierData> RuntimeData
    {
        get
        {
            if (Content != null) return new List<MaterialModifierData>(Content.Named<MaterialModifierData>().Values).AsReadOnly();
            EnsureLoaded();
            return runtimeData;
        }
    }

    public static bool TryGetData(string id, out MaterialModifierData data)
    {
        if (Content != null)
        {
            if (string.IsNullOrEmpty(id)) { data = null; return false; }
            return Content.Named<MaterialModifierData>().TryGetValue(id, out data);
        }
        EnsureLoaded();
        if (!string.IsNullOrEmpty(id) && dataById.TryGetValue(id, out data))
            return true;

        data = null;
        return false;
    }

    public static bool TryGetDataByScript(string script, out MaterialModifierData data)
    {
        if (Content != null)
        {
            foreach (MaterialModifierData candidate in Content.Named<MaterialModifierData>().Values)
                if (candidate != null && candidate.script == script) { data = candidate; return true; }
            data = null;
            return false;
        }
        EnsureLoaded();
        if (!string.IsNullOrEmpty(script) && dataByScript.TryGetValue(script, out data))
            return true;

        data = null;
        return false;
    }

    public static bool TryGetDefinition(string script, out MaterialModifierDefinition definition)
    {
        EnsureLoaded();
        if (!string.IsNullOrEmpty(script) && definitionsByScript.TryGetValue(script, out definition))
            return true;

        definition = null;
        return false;
    }

    public static void ClearCache()
    {
        definitionsById = null;
        definitionsByScript = null;
        dataById = null;
        dataByScript = null;
        runtimeData = null;
    }

    private static void EnsureLoaded()
    {
        if (runtimeData != null)
            return;

        MaterialModifierDefinition[] definitions = Resources.LoadAll<MaterialModifierDefinition>(ResourcePath);
        definitionsById = new Dictionary<string, MaterialModifierDefinition>(definitions.Length);
        definitionsByScript = new Dictionary<string, MaterialModifierDefinition>(definitions.Length);
        dataById = new Dictionary<string, MaterialModifierData>(definitions.Length);
        dataByScript = new Dictionary<string, MaterialModifierData>(definitions.Length);
        runtimeData = new List<MaterialModifierData>(definitions.Length);

        for (int i = 0; i < definitions.Length; i++)
        {
            MaterialModifierDefinition definition = definitions[i];
            if (definition == null || string.IsNullOrEmpty(definition.Id) || string.IsNullOrEmpty(definition.Script))
                continue;

            if (definitionsById.ContainsKey(definition.Id) || definitionsByScript.ContainsKey(definition.Script))
                continue;

            MaterialModifierData data = definition.CreateRuntimeData();
            definitionsById.Add(definition.Id, definition);
            definitionsByScript.Add(definition.Script, definition);
            dataById.Add(data.id, data);
            dataByScript.Add(data.script, data);
            runtimeData.Add(data);
        }
    }
}
