using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

/// <summary>A world's explicit content catalog. Missing tables stay empty; they never fall back into another world.</summary>
public interface IRuleContentProvider
{
    IReadOnlyDictionary<int, T> Numeric<T>();
    IReadOnlyDictionary<string, T> Named<T>();
    IReadOnlyDictionary<MagicRarity, MagicRarityData> Rarities { get; }
}

/// <summary>
/// Detached catalog snapshot. Collections are read-only and every supplied record is deep-copied by the host.
/// Rule implementations must treat records as configuration, never mutate them.
/// </summary>
public sealed class RuleContentSnapshot : IRuleContentProvider, IRuleConfigurationProvider
{
    private readonly Dictionary<Type, object> numeric = new Dictionary<Type, object>();
    private readonly Dictionary<Type, object> named = new Dictionary<Type, object>();
    private readonly IReadOnlyDictionary<MagicRarity, MagicRarityData> rarities;
    private bool frozen;
    public RuleContentSnapshot Freeze() { frozen = true; return this; }
    public IReadOnlyDictionary<MagicRarity, MagicRarityData> Rarities => rarities;
    private readonly IRuleConfigurationProvider configuration;
    public RuleSummonLayout SummonLayout => configuration.SummonLayout;
    public bool TryGetArrowUpgrade(string id, out RuleArrowUpgradeValue value) => configuration.TryGetArrowUpgrade(id, out value);

    public RuleContentSnapshot(IReadOnlyDictionary<MagicRarity, MagicRarityData> rarities = null, IRuleConfigurationProvider configuration = null)
    {
        this.configuration = configuration ?? new RuleConfiguration();
        this.rarities = new ReadOnlyDictionary<MagicRarity, MagicRarityData>(rarities != null
            ? Copy(rarities) : new Dictionary<MagicRarity, MagicRarityData>());
    }
    public RuleContentSnapshot AddNumeric<T>(IReadOnlyDictionary<int, T> table)
    {
        if (frozen) throw new InvalidOperationException("This catalog is frozen.");
        numeric[typeof(T)] = new ReadOnlyDictionary<int, T>(table != null ? Copy(table) : new Dictionary<int, T>());
        return this;
    }
    public RuleContentSnapshot AddNamed<T>(IReadOnlyDictionary<string, T> table)
    {
        if (frozen) throw new InvalidOperationException("This catalog is frozen.");
        named[typeof(T)] = new ReadOnlyDictionary<string, T>(table != null ? Copy(table) : new Dictionary<string, T>());
        return this;
    }
    public IReadOnlyDictionary<int, T> Numeric<T>() => numeric.TryGetValue(typeof(T), out object value)
        ? (IReadOnlyDictionary<int, T>)value : EmptyNumeric<T>.Value;
    public IReadOnlyDictionary<string, T> Named<T>() => named.TryGetValue(typeof(T), out object value)
        ? (IReadOnlyDictionary<string, T>)value : EmptyNamed<T>.Value;
    private static Dictionary<K, V> Copy<K, V>(IReadOnlyDictionary<K, V> values)
    {
        var result = new Dictionary<K, V>();
        foreach (KeyValuePair<K, V> pair in values)
            result.Add(pair.Key, RuleJson.Clone(pair.Value));
        return result;
    }
    private static class EmptyNumeric<T> { internal static readonly IReadOnlyDictionary<int, T> Value = new ReadOnlyDictionary<int, T>(new Dictionary<int, T>()); }
    private static class EmptyNamed<T> { internal static readonly IReadOnlyDictionary<string, T> Value = new ReadOnlyDictionary<string, T>(new Dictionary<string, T>()); }
}
