using System;
using System.Collections.Generic;

/// <summary>Run-owned passive effect, deliberately separate from combat BuffModel.</summary>
public abstract class RelicModel
{
    public RelicData Data { get; }
    public string InstanceId { get; private set; }
    internal RelicInventory OwningInventory { get; set; }
    protected readonly Dictionary<string, int> State = new Dictionary<string, int>();
    public virtual bool IsEnabled => true;
    public virtual int DisplayCounter => Get("counter");

    protected RelicModel(RelicData data)
    {
        Data = data;
        InstanceId = Guid.NewGuid().ToString("N");
    }

    protected int Get(string key) => State.TryGetValue(key, out int value) ? value : 0;
    protected void Set(string key, int value) => State[key] = value;
    public virtual void OnEvent(RelicContext context, RelicEvent value) { }
    // Pure queries: never spend charges, generate RNG, or emit effects here (also used by previews).
    public virtual int ModifyAttack(int amount) => amount;
    public virtual int ModifyMagicAttack(MagicModel magic, int amount) => amount;
    public virtual int ModifyDamageTaken(int amount) => amount;
    public virtual int ModifyShieldGain(int amount) => amount;
    public virtual int ModifyGoldGain(int amount) => amount;
    public virtual int ModifyBuffGiven(BuffEnum type, int amount) => amount;
    public virtual int ModifyBuffReceived(BuffEnum type, int amount) => amount;
    public virtual void ResetBattleState() { }
    public virtual void ResetTurnState() { }

    public virtual RelicSaveData ExportState() => new RelicSaveData
    {
        numericId = Data.numericId, instanceId = InstanceId, stateVersion = 1,
        values = new Dictionary<string, int>(State)
    };

    public virtual void RestoreState(RelicSaveData data)
    {
        if (!string.IsNullOrEmpty(data.instanceId)) InstanceId = data.instanceId;
        State.Clear();
        if (data.values != null)
            foreach (var pair in data.values) State[pair.Key] = pair.Value;
    }
}

/// <summary>Keep unknown/newer content losslessly instead of granting compensation on load.</summary>
public sealed class UnavailableRelicModel : RelicModel
{
    private readonly RelicSaveData saved;
    public override bool IsEnabled => false;
    public UnavailableRelicModel(RelicSaveData saved) : base(new RelicData { numericId = saved.numericId })
    {
        this.saved = saved;
        RestoreState(saved);
    }
    public override RelicSaveData ExportState() => new RelicSaveData
    {
        numericId = saved.numericId, instanceId = saved.instanceId, stateVersion = saved.stateVersion,
        values = saved.values == null ? null : new Dictionary<string, int>(saved.values)
    };
}

public sealed class RelicContext
{
    private readonly RelicInventory inventory;
    private readonly RelicModel relic;
    internal RelicContext(RelicInventory inventory, RelicModel relic) { this.inventory = inventory; this.relic = relic; }
    public PlayerState Player => inventory.Owner;
    public BattleManager Battle => inventory.Battle;
    public void Enqueue(Action effect) => inventory.Enqueue(relic, effect);
    public void ForEachAliveEnemy(Action<CombatantModel> effect)
    {
        if (Battle == null) return;
        foreach (var enemy in Battle.Enemies)
            if (enemy != null && !enemy.IsDead) effect(new CombatantModel(enemy));
    }
    public void GiveBuff(CombatantModel target, BuffEnum type, int amount)
    {
        Enqueue(() => { if (inventory.IsCurrentTarget(target)) target.AddBuff(type, amount, new CombatantModel(Player)); });
    }
    public void GainShield(int amount) => Enqueue(() => Player.GainShield(amount));
    public void Heal(int amount) => Enqueue(() => Player.Heal(amount));
    public void Draw(int count) => Enqueue(() => Player.DrawCards(count));
    public void GrantTemporaryArrows(MaterialEnum material, int count)
    {
        Enqueue(() =>
        {
            for (int i = 0; i < count; i++)
                if (Player.GrantTemporaryMaterialToHand(material) == null)
                    break;
        });
    }
    public void GainGold(int amount) => Enqueue(() => Player.AddGold(amount, false, RelicGoldReason.RelicEffect));
    public void Damage(CombatantModel target, int amount, RelicDamageKind kind = RelicDamageKind.EffectDamage)
        => Enqueue(() => inventory.DealDamage(target, amount, kind));
}
