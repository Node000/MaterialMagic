using System.Collections.Generic;

public class CombatDamageResult
{
    public int RawDamage { get; set; }
    public int FinalDamage { get; set; }
    public int ShieldDamage { get; set; }
    public int HealthDamage { get; set; }
    public bool TargetDied { get; set; }

    public bool FullyBlocked => HealthDamage <= 0 && ShieldDamage > 0;
}

public abstract class UnitModel
{
    public abstract int NumericId { get; }
    public abstract string DisplayName { get; }
    public abstract int MaxHealth { get; }
    public abstract int CurrentHealth { get; protected set; }
    public abstract int Shield { get; protected set; }
    public abstract IReadOnlyDictionary<BuffEnum, BuffModel> Buffs { get; }
    public abstract bool IsDead { get; }
    public virtual bool DeathHandled => false;

    public abstract CombatDamageResult TakeDamageResult(int damage, CombatantModel attacker);
    public abstract int TakeDamage(int damage, CombatantModel attacker);
    public abstract int TakeDirectDamage(int damage);
    public abstract int GainShield(int amount);
    public abstract int ConsumeShield(int amount);
    public abstract void ClearShield();
    public abstract void AddBuff(BuffEnum buffType, int stack);
    public abstract int GetBuffStack(BuffEnum buffType);
    public abstract void ConsumeBuff(BuffEnum buffType, int amount);
}
