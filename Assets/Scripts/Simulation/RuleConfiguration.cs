using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

/// <summary>Copied rule values; no Unity object or serialized asset reference is retained.</summary>
public sealed class RuleSummonLayout
{
    public float HorizontalSpacing { get; }
    public float VerticalSpacing { get; }
    public float OccupiedRadius { get; }
    public int SameRowSlotCount { get; }
    public int MaxSearchAttempts { get; }
    public RuleSummonLayout(float horizontal = 180f, float vertical = 160f, float radius = 150f, int rowSlots = 4, int attempts = 16)
    {
        HorizontalSpacing = Math.Max(1f, horizontal); VerticalSpacing = Math.Max(0f, vertical);
        OccupiedRadius = Math.Max(0f, radius); SameRowSlotCount = Math.Max(2, rowSlots); MaxSearchAttempts = Math.Max(1, attempts);
    }
}

public readonly struct RuleArrowUpgradeValue
{
    public int Primary { get; }
    public int Secondary { get; }
    [Newtonsoft.Json.JsonConstructor]
    public RuleArrowUpgradeValue(int primary, int secondary) { Primary = primary; Secondary = secondary; }
}

public interface IRuleConfigurationProvider
{
    RuleSummonLayout SummonLayout { get; }
    bool TryGetArrowUpgrade(string nodeId, out RuleArrowUpgradeValue value);
}

/// <summary>Explicit-world configuration input; its dictionary is detached and never exposes setters.</summary>
public sealed class RuleConfiguration : IRuleConfigurationProvider
{
    public RuleSummonLayout SummonLayout { get; }
    private readonly IReadOnlyDictionary<string, RuleArrowUpgradeValue> arrows;
    public RuleConfiguration(RuleSummonLayout summonLayout = null, IReadOnlyDictionary<string, RuleArrowUpgradeValue> arrows = null)
    {
        SummonLayout = summonLayout ?? new RuleSummonLayout();
        var copy = new Dictionary<string, RuleArrowUpgradeValue>();
        if (arrows != null) foreach (var pair in arrows) copy.Add(pair.Key, pair.Value);
        this.arrows = new ReadOnlyDictionary<string, RuleArrowUpgradeValue>(copy);
    }
    public bool TryGetArrowUpgrade(string nodeId, out RuleArrowUpgradeValue value) => arrows.TryGetValue(nodeId, out value);
}
