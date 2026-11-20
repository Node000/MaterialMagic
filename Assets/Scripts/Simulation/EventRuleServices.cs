using System;
using System.Collections.Generic;

/// <summary>Content/random inputs only. Effects themselves live in EventResolutionSession, shared by UI and scripts.</summary>
public sealed class EventRuleServices
{
    private readonly PlayerState owner;
    public Func<int, int, int> NextRandomInt;
    public Func<int, MagicData> FindMagic = id => GameDataDatabase.TryGetMagicData(id, out MagicData data) ? data : null;
    public Func<string, MagicModifierData> FindMagicModifier = id => !string.IsNullOrEmpty(id) && GameDataDatabase.TryGetMagicModifierData(id, out MagicModifierData data) ? data : null;
    public Func<string, MaterialModifierData> FindMaterialModifier = FindDefaultMaterialModifier;
    public Func<int, IReadOnlyList<MagicData>> MagicChoices;
    public Func<int, IReadOnlyList<MagicModifierData>> MagicModifierChoices;
    public Func<int, IReadOnlyList<MaterialModifierData>> MaterialModifierChoices;
    public int MagicSlotCapacity = 12;

    public EventRuleServices(PlayerState owner = null)
    {
        this.owner = owner;
        NextRandomInt = (min, max) => ChoiceRuleRandom.Next(owner, min, max);
        MagicChoices = count => DefaultMagicChoices(count);
        MagicModifierChoices = count => ModifierDropRules.GetMagicChoices(owner ?? RuleExecutionScope.Current?.Player, count, NextRandomInt);
        MaterialModifierChoices = count => ModifierDropRules.GetMaterialChoices(owner ?? RuleExecutionScope.Current?.Player, count, NextRandomInt);
    }

    private IReadOnlyList<MagicData> DefaultMagicChoices(int count)
    {
        PlayerState player = owner ?? RuleExecutionScope.Current?.Player;
        if (player == null) throw new InvalidOperationException("Reward generation requires an owner or active rule context.");
        return new RewardOfferGenerator(player, economy: GameDataDatabase.GetDefaultEconomyConfig(),
            services: new RewardGenerationServices { NextRandomInt = NextRandomInt }).GetMagicChoices(count);
    }

    private static MaterialModifierData FindDefaultMaterialModifier(string id)
        => MaterialModifierDatabase.TryGetData(id, out MaterialModifierData data) ? data : null;
}

/// <summary>Shared random reward pools. Fixed grants and shop stock use their own content rules.</summary>
public static class ModifierDropRules
{
    public static List<MagicModifierData> GetMagicChoices(PlayerState player, int count, Func<int, int, int> nextRandomInt)
    {
        if (player == null) return new List<MagicModifierData>();
        using (player.Rules.Enter())
            return PickUnique(GameDataDatabase.MagicModifierData.Values, count, data => data.weight, data =>
            {
                if (!UnlockSystem.IsMagicModifierUnlocked(data)) return false;
                MagicModifierModel sample = MagicModifierFactory.Create(data);
                return sample != null && player.MagicBook.Exists(magic => magic != null && magic.CanAddModifier(sample));
            }, nextRandomInt);
    }

    public static List<MaterialModifierData> GetMaterialChoices(PlayerState player, int count, Func<int, int, int> nextRandomInt)
    {
        if (player == null) return new List<MaterialModifierData>();
        using (player.Rules.Enter())
            return PickUnique(MaterialModifierDatabase.RuntimeData, count, data => data.weight, data =>
            {
                if (!data.inArrowModifierRewardPool || !UnlockSystem.IsMaterialModifierUnlocked(data)) return false;
                MaterialModifierModel sample = MaterialModifierFactory.Create(data);
                return sample != null && player.Deck.Exists(card => card != null && card.material != MaterialEnum.None && card.CanAddModifier(sample));
            }, nextRandomInt);
    }

    internal static List<T> PickUnique<T>(IEnumerable<T> source, int count, Func<T, int> weight,
        Predicate<T> allowed, Func<int, int, int> nextRandomInt) where T : class, IDataRecord
    {
        var result = new List<T>();
        if (count <= 0) return result;
        var pool = new List<(T data, int weight)>();
        long totalWeight = 0;
        foreach (T data in source)
        {
            if (data == null || string.IsNullOrEmpty(data.Id)) continue;
            int value = weight(data);
            if (value < 0) UnityEngine.Debug.LogError($"Modifier {data.Id} has a negative drop weight: {value}.");
            if (value <= 0 || !allowed(data)) continue;
            pool.Add((data, value));
            totalWeight += value;
        }
        if (totalWeight > int.MaxValue)
        {
            UnityEngine.Debug.LogError("Modifier reward pool weight exceeds supported range.");
            return result;
        }
        pool.Sort((a, b) => StringComparer.Ordinal.Compare(a.data.Id, b.data.Id));
        while (result.Count < count && pool.Count > 0)
        {
            int roll = nextRandomInt(0, (int)totalWeight);
            int selected = 0;
            while (roll >= pool[selected].weight) roll -= pool[selected++].weight;
            result.Add(pool[selected].data);
            totalWeight -= pool[selected].weight;
            pool.RemoveAt(selected);
        }
        return result;
    }
}

public enum EventResolutionStepKind { Effect, Discard, Choice, Complete }

/// <summary>Committed deltas for presentation; effects and discard must never be re-applied by the view.</summary>
public sealed class EventResolutionStep
{
    public EventResolutionStepKind Kind { get; internal set; }
    public EventEffectData Effect { get; internal set; }
    public ChoiceResult Result { get; internal set; }
    public IReadOnlyList<MaterialModel> RemovedTemporaryCards { get; internal set; }
    public IReadOnlyList<MagicModel> AcquiredMagics { get; internal set; }
}
