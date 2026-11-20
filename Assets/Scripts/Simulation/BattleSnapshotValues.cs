using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

/// <summary>Frozen display values. No live BuffModel is retained by a result snapshot.</summary>
public static class BattleSnapshotValues
{
    public static EnemyIntentData CopyIntent(EnemyIntentData source)
    {
        if (source == null) return null;
        var buffs = new BuffStackData[source.buffs != null ? source.buffs.Length : 0];
        for (int i = 0; i < buffs.Length; i++)
            buffs[i] = source.buffs[i] == null ? null : new BuffStackData { buffType = source.buffs[i].buffType, stack = source.buffs[i].stack };
        return new EnemyIntentData
        {
            intentType = source.intentType, actionType = source.actionType, value = source.value,
            times = source.times, hitInterval = source.hitInterval, summonEnemyId = source.summonEnemyId,
            summonCount = source.summonCount, buffs = buffs, displayType = source.displayType,
            descriptionKey = source.descriptionKey
        };
    }

    public static IReadOnlyDictionary<BuffEnum, BuffModel> CopyBuffs(IReadOnlyDictionary<BuffEnum, BuffModel> source)
    {
        var copy = new Dictionary<BuffEnum, BuffModel>();
        if (source != null)
            foreach (var pair in source)
                if (pair.Value != null) copy.Add(pair.Key, new FrozenBuff(pair.Value));
        return new ReadOnlyDictionary<BuffEnum, BuffModel>(copy);
    }

    private sealed class FrozenBuff : BuffModel
    {
        private readonly bool visible;
        private readonly string slot;
        private readonly string tooltip;
        private readonly string description;
        public FrozenBuff(BuffModel source) : base(source.buffType, source.stack)
        {
            visible = source.IsVisible;
            slot = source.GetSlotStackText();
            tooltip = source.GetTooltipStackText();
            description = source.GetDesc();
        }
        public override bool IsVisible => visible;
        public override string GetSlotStackText() => slot;
        public override string GetTooltipStackText() => tooltip;
        public override string GetDesc() => description;
    }
}

public sealed class BattleIntentSnapshot
{
    private readonly EnemyIntentData intent;
    public EnemyIntentData Intent => BattleSnapshotValues.CopyIntent(intent);
    public int Value { get; }
    public int TotalDamage { get; }
    public string DisplayValue { get; }
    public string TooltipTitle { get; }
    public string TooltipDescription { get; }
    private readonly IReadOnlyList<BuffStackData> tooltipBuffs;
    public IReadOnlyList<BuffStackData> TooltipBuffs
    {
        get
        {
            var copy = new List<BuffStackData>();
            if (tooltipBuffs != null)
                foreach (BuffStackData buff in tooltipBuffs)
                    copy.Add(new BuffStackData { buffType = buff.buffType, stack = buff.stack });
            return copy.AsReadOnly();
        }
    }

    internal BattleIntentSnapshot(EnemyModel enemy, EnemyIntentData intent, PlayerState player)
    {
        // Runtime definitions are immutable during a turn; copy the value object for replay identity.
        this.intent = BattleSnapshotValues.CopyIntent(intent);
        if (intent == null) return;
        TooltipTitle = enemy.GetIntentTooltipTitle(intent);
        TooltipDescription = enemy.GetIntentTooltipDescription(intent, player);
        var tooltip = enemy.GetIntentTooltipBuffs(intent, player);
        var tooltipCopy = new List<BuffStackData>();
        if (tooltip != null)
            foreach (BuffStackData buff in tooltip)
                if (buff != null) tooltipCopy.Add(new BuffStackData { buffType = buff.buffType, stack = buff.stack });
        tooltipBuffs = tooltipCopy.AsReadOnly();
        switch (intent.actionType)
        {
            case EnemyActionType.Attack:
            case EnemyActionType.AttackAll:
                Value = enemy.GetIntentAttackValue(intent, player);
                TotalDamage = enemy.GetIntentAttackTotalValue(intent, player);
                break;
            case EnemyActionType.GainShield: Value = enemy.GetIntentShieldValue(intent); break;
            case EnemyActionType.Special: DisplayValue = enemy.GetSpecialIntentDisplayValue(intent, player); break;
        }
    }
}
