using System;
using System.Collections.Generic;

public static class MaterialModifierFactory
{
    private static readonly Dictionary<string, Type> TypesByScript = new Dictionary<string, Type>();
    private static bool loaded;

    public static MaterialModifierModel Create(MaterialModifierData data)
    {
        if (data == null || string.IsNullOrEmpty(data.script))
            return null;

        EnsureLoaded();
        return TypesByScript.TryGetValue(data.script, out Type type) ? Activator.CreateInstance(type) as MaterialModifierModel : null;
    }

    public static MaterialModifierModel Create(string id)
    {
        if (id == "heavy_arrow")
            id = "repeat_arrow";

        return MaterialModifierDatabase.TryGetData(id, out MaterialModifierData data) ? Create(data) : null;
    }

    public static string GetId(MaterialModifierModel modifier)
    {
        return modifier != null && MaterialModifierDatabase.TryGetDataByScript(modifier.GetType().Name, out MaterialModifierData data) ? data.id : string.Empty;
    }

    private static void EnsureLoaded()
    {
        if (loaded)
            return;

        loaded = true;
        Type baseType = typeof(MaterialModifierModel);
        Type[] types = baseType.Assembly.GetTypes();
        for (int i = 0; i < types.Length; i++)
        {
            Type type = types[i];
            if (type == null || type.IsAbstract || !baseType.IsAssignableFrom(type))
                continue;

            TypesByScript[type.Name] = type;
        }
    }
}
