using System;
using System.Collections.Generic;

[Serializable]
public sealed class BuffRuntimeSaveData
{
    public BuffEnum type;
    public int stack;
    public Dictionary<string, int> values;

    public static BuffRuntimeSaveData[] Export(IReadOnlyDictionary<BuffEnum, BuffModel> buffs)
    {
        var result = new List<BuffRuntimeSaveData>();
        foreach (var buff in buffs.Values)
            if (buff.stack > 0) result.Add(new BuffRuntimeSaveData
            { type = buff.buffType, stack = buff.stack, values = buff.ExportRuntimeState() });
        result.Sort((a, b) => a.type.CompareTo(b.type));
        return result.ToArray();
    }
}
