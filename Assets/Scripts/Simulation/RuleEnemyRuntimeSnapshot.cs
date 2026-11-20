using System;
using System.Collections.Generic;
using System.Reflection;

/// <summary>Memory-only runtime supplement to the compatible persistent enemy DTO.</summary>
public sealed class RuleEnemyRuntimeSnapshot
{
    private readonly Type type;
    private readonly Dictionary<FieldInfo, object> derivedFields = new Dictionary<FieldInfo, object>();
    private readonly BuffRuntimeSaveData[] buffs;
    private readonly EnemyIntentData[] intents;
    public RuleEnemyRuntimeSnapshot(EnemyModel source)
    {
        type = source.GetType();
        for (Type current = type; current != null && current != typeof(EnemyModel); current = current.BaseType)
            foreach (FieldInfo field in current.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                if (!field.FieldType.IsValueType && field.FieldType != typeof(string))
                    throw new NotSupportedException("Enemy runtime snapshot requires an explicit copier for " + current.Name + "." + field.Name);
                derivedFields.Add(field, field.GetValue(source));
            }
        buffs = Copy(BuffRuntimeSaveData.Export(source.Buffs));
        intents = new EnemyIntentData[source.CurrentIntents.Count];
        for (int i = 0; i < intents.Length; i++) intents[i] = Copy(source.CurrentIntents[i]);
    }
    public void Restore(EnemyModel target)
    {
        if (target.GetType() != type) throw new InvalidOperationException("Enemy snapshot type differs from restored content.");
        foreach (var pair in derivedFields) pair.Key.SetValue(target, pair.Value);
        target.RestoreBattleBuffs(Copy(buffs));
        var list = target.CurrentIntents as List<EnemyIntentData>;
        if (list == null) throw new InvalidOperationException("Enemy intents require the production mutable list implementation.");
        list.Clear();
        foreach (EnemyIntentData intent in intents) list.Add(Copy(intent));
    }
    private static T Copy<T>(T value) => RuleJson.Clone(value);
}
