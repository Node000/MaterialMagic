using System.Collections.Generic;

public class PlayerModel : UnitModel
{
    public PlayerStatus Status { get; }

    public override int NumericId => 0;
    public override string DisplayName => "Player";
    public override int MaxHealth => Status.MaxHealth;
    public override int CurrentHealth
    {
        get => Status.CurrentHealth;
        protected set { }
    }

    public override int Shield
    {
        get => Status.Shield;
        protected set { }
    }
    public override IReadOnlyDictionary<BuffEnum, BuffModel> Buffs => Status.Buffs;
    public override bool IsDead => Status.CurrentHealth <= 0;

    public PlayerModel(PlayerStatus status)
    {
        Status = status;
    }

    public override CombatDamageResult TakeDamageResult(int damage, CombatantModel attacker)
    {
        return Status.TakeDamageResult(damage, attacker);
    }

    public override int TakeDamage(int damage, CombatantModel attacker)
    {
        return Status.TakeDamage(damage, attacker);
    }

    public override int TakeDirectDamage(int damage)
    {
        return Status.TakeDirectDamage(damage);
    }

    public override int GainShield(int amount)
    {
        return Status.GainShield(amount);
    }

    public override int ConsumeShield(int amount)
    {
        return Status.ConsumeShield(amount);
    }

    public override void ClearShield()
    {
        Status.ClearShield();
    }

    public override void AddBuff(BuffEnum buffType, int stack, CombatantModel source)
    {
        Status.AddBuff(buffType, stack, source);
    }

    public override int GetBuffStack(BuffEnum buffType)
    {
        return Status.GetBuffStack(buffType);
    }

    public override void ConsumeBuff(BuffEnum buffType, int amount)
    {
        Status.ConsumeBuff(buffType, amount);
    }
}
