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
