using System;
using System.Collections.Generic;

[Serializable]
public sealed class RelicData : IDataRecord, INumericDataRecord
{
    public int numericId;
    public string id;
    public string nameKey;
    public string descriptionKey;
    public string iconPath;
    public string script;
    public int rarity;
    public int value;
    public int threshold;
    public MaterialEnum material;
    public int duplicateGold;
    public int price;
    public string Id => id;
    public int NumericId => numericId;
}

[Serializable]
public sealed class RelicPoolData : IDataRecord
{
    public string id;
    public int[] relicIds = Array.Empty<int>();
    public int[] weights = Array.Empty<int>();
    public string Id => id;
}

[Serializable]
public sealed class RelicSaveData
{
    public int numericId;
    public string instanceId;
    public int stateVersion = 1;
    public Dictionary<string, int> values = new Dictionary<string, int>();
}

[Serializable]
public sealed class RelicOfferSaveData
{
    public string offerId;
    public string poolId;
    public int[] relicIds = Array.Empty<int>();
    public int[] prices = Array.Empty<int>();
    public bool committed;
    public int selectedIndex = -1;
}

public enum RelicAcquireResult { Invalid, Acquired, DuplicateCompensated }
public enum RelicDamageKind { EffectDamage, Attack, HealthLoss }
public enum RelicGoldReason { Reward, Refund, Compensation, Restore, RelicEffect, Spend }
public enum RelicEventKind
{
    Acquired, BattleStarted, TurnPreparing, TurnStarted, TurnEnding, TurnEnded,
    CardDrawn, CardEnteredHand, ArrowRead, ArrowConsumed, MagicStarted, MagicResolved,
    DamageResolved, HealthLost, BuffApplied, BuffChanged, ShieldGained, Healed,
    GoldChanged, BattleEnded, NodeCompleted
}

/// <summary>Immutable committed fact. Amount is actual change, not a UI prediction.</summary>
public sealed class RelicEvent
{
    public RelicEventKind Kind { get; }
    public CombatantModel Source { get; }
    public CombatantModel Target { get; }
    public int Amount { get; }
    public int Before { get; }
    public int After { get; }
    public BuffEnum BuffType { get; }
    public MaterialModel Card { get; }
    public MagicModel Magic { get; }
    public RelicDamageKind DamageKind { get; }
    public int ShieldDamage { get; }
    public int RawDamage { get; }
    public int FinalDamage { get; }
    public bool TargetDied { get; }
    public bool IsCleanup { get; }
    public long EventId { get; internal set; }
    public bool Victory { get; }
    public bool IsReplay { get; }
    public string OriginRelicId { get; }

    public RelicEvent(RelicEventKind kind, CombatantModel source = null, CombatantModel target = null,
        int amount = 0, int before = 0, int after = 0, BuffEnum buffType = BuffEnum.None,
        MaterialModel card = null, MagicModel magic = null, RelicDamageKind damageKind = RelicDamageKind.EffectDamage,
        int shieldDamage = 0, bool victory = false, bool isReplay = false, string originRelicId = null,
        int rawDamage = 0, int finalDamage = 0, bool targetDied = false, bool isCleanup = false)
    {
        Kind = kind; Source = source; Target = target; Amount = amount; Before = before; After = after;
        BuffType = buffType; Card = card; Magic = magic; DamageKind = damageKind;
        ShieldDamage = shieldDamage; Victory = victory; IsReplay = isReplay; OriginRelicId = originRelicId;
        RawDamage = rawDamage; FinalDamage = finalDamage; TargetDied = targetDied; IsCleanup = isCleanup;
    }
}
