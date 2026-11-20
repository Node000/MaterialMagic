using System;
using System.Collections.Generic;

/// <summary>Value-only profile archive. Export and restore never share arrays with the policy.</summary>
[Serializable]
public sealed class RuleProfileSnapshot
{
    public string[] ruleKeys = Array.Empty<string>();
    public string[] unlockedKeys = Array.Empty<string>();
    public bool allowUnruledTargets;
    public bool allUnlocked;
}

/// <summary>
/// A world's immutable unlock decisions. Supplied keys are copied; this policy never reads or writes
/// a real profile. Explicit snapshots preserve production's unruled-target and magic-ID semantics.
/// </summary>
public sealed class RuleProfilePolicy
{
    private readonly HashSet<string> ruleKeys;
    private readonly HashSet<string> unlockedKeys;
    private readonly bool allowUnruledTargets;
    private readonly bool allUnlocked;

    /// <summary>Explicit unrestricted simulation policy. No profile is consulted.</summary>
    public static RuleProfilePolicy AllUnlocked { get; } = new RuleProfilePolicy(null, null, true, true);

    /// <summary>Explicit empty simulation profile: every valid target is locked, even without a rule.</summary>
    public static RuleProfilePolicy Empty { get; } = new RuleProfilePolicy(null, null, false, false);

    private RuleProfilePolicy(IEnumerable<string> rules, IEnumerable<string> unlocked,
        bool allowUnruledTargets, bool allUnlocked)
    {
        ruleKeys = CopyKeys(rules);
        unlockedKeys = CopyKeys(unlocked);
        this.allowUnruledTargets = allowUnruledTargets;
        this.allUnlocked = allUnlocked;
    }

    /// <summary>
    /// Snapshot production-style decisions: targets with no configured rule remain available.
    /// Keys use UnlockSystem.GetUnlockKey(targetType, targetId).
    /// </summary>
    public static RuleProfilePolicy Create(IEnumerable<string> ruleKeys, IEnumerable<string> unlockedKeys)
        => new RuleProfilePolicy(ruleKeys, unlockedKeys, true, false);

    /// <summary>Explicit allow-list policy; valid targets not in the supplied keys remain locked.</summary>
    public static RuleProfilePolicy FromUnlockedKeys(IEnumerable<string> unlockedKeys)
        => new RuleProfilePolicy(null, unlockedKeys, false, false);

    /// <summary>Detached, deterministically ordered values suitable for JSON memory snapshots.</summary>
    public RuleProfileSnapshot Export() => new RuleProfileSnapshot
    {
        ruleKeys = ExportKeys(ruleKeys),
        unlockedKeys = ExportKeys(unlockedKeys),
        allowUnruledTargets = allowUnruledTargets,
        allUnlocked = allUnlocked
    };

    /// <summary>Restore only supplied values, never recapture the host profile. Missing data is empty.</summary>
    public static RuleProfilePolicy Restore(RuleProfileSnapshot snapshot)
        => snapshot == null ? Empty : new RuleProfilePolicy(snapshot.ruleKeys, snapshot.unlockedKeys,
            snapshot.allowUnruledTargets, snapshot.allUnlocked);

    public bool IsUnlocked(string targetType, string targetId)
    {
        if (string.IsNullOrEmpty(targetType) || string.IsNullOrEmpty(targetId)) return true;
        string key = UnlockSystem.GetUnlockKey(targetType, targetId);
        return allUnlocked || unlockedKeys.Contains(key) || (allowUnruledTargets && !ruleKeys.Contains(key));
    }

    public bool IsStartConfigUnlocked(PlayerStartConfigData data)
        => data == null || IsUnlocked(UnlockSystem.TargetStartConfig, data.id);

    public bool IsMagicUnlocked(MagicData data)
    {
        if (data == null) return true;
        if (!allowUnruledTargets)
        {
            // Explicit simulation allow-lists accept either identity, never a host fallback.
            if (!string.IsNullOrEmpty(data.id) && IsUnlocked(UnlockSystem.TargetMagic, data.id)) return true;
            return data.numericId > 0 ? IsUnlocked(UnlockSystem.TargetMagic, data.numericId.ToString())
                : string.IsNullOrEmpty(data.id);
        }
        if (!string.IsNullOrEmpty(data.id) && ruleKeys.Contains(UnlockSystem.GetUnlockKey(UnlockSystem.TargetMagic, data.id)))
            return IsUnlocked(UnlockSystem.TargetMagic, data.id);
        return data.numericId <= 0 || IsUnlocked(UnlockSystem.TargetMagic, data.numericId.ToString());
    }

    public bool IsMaterialModifierUnlocked(MaterialModifierData data)
        => data == null || IsUnlocked(UnlockSystem.TargetMaterialModifier, data.id);

    public bool IsMagicModifierUnlocked(MagicModifierData data)
        => data == null || IsUnlocked(UnlockSystem.TargetMagicModifier, data.id);

    private static string[] ExportKeys(HashSet<string> values)
    {
        var result = new string[values.Count];
        values.CopyTo(result);
        Array.Sort(result, StringComparer.Ordinal);
        return result;
    }

    private static HashSet<string> CopyKeys(IEnumerable<string> values)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        if (values != null)
            foreach (string value in values)
                if (!string.IsNullOrEmpty(value)) result.Add(value);
        return result;
    }
}
