using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class BuffModel
{
    public BuffEnum buffType;
    public int stack;

    public BuffKindEnum Kind => GetKind(buffType);
    public bool IsDeBuff => Kind == BuffKindEnum.DeBuff;

    public BuffModel(BuffEnum buffType, int stack)
    {
        this.buffType = buffType;
        this.stack = stack;
    }

    public virtual void OnTurnStart(CombatantModel self, CombatantModel opponent)
    {
    }

    public virtual void AfterTurnStart(CombatantModel self, CombatantModel opponent)
    {
    }

    public virtual void AfterDraw(CombatantModel self, MaterialModel card)
    {
    }

    public virtual void AfterDiscard(CombatantModel self, MaterialModel card)
    {
    }

    public virtual void OnInvoke(CombatantModel self, CombatantModel target)
    {
    }

    public virtual void OnGetAction(CombatantModel self, CombatantModel opponent)
    {
    }

    public virtual void AfterGetAction(CombatantModel self, CombatantModel opponent)
    {
    }

    public virtual void OnAttack(CombatantModel self, CombatantModel target, ref int attackValue)
    {
    }

    public virtual void AfterAttack(CombatantModel self, CombatantModel target, ref int attackResult)
    {
    }

    public virtual void OnGainShield(CombatantModel self, ref int shieldValue)
    {
    }

    public virtual void OnTurnEnd(CombatantModel self, CombatantModel opponent)
    {
    }

    public virtual void AfterTurnEnd(CombatantModel self, CombatantModel opponent)
    {
    }

    public virtual void OnDie(CombatantModel self, CombatantModel opponent)
    {
    }

    public virtual void OnExpire(CombatantModel self, CombatantModel opponent)
    {
    }

    public void AddStack(int amount)
    {
        stack += amount;
    }

    public void ConsumeStack(int amount)
    {
        stack -= amount;
        if (stack < 0)
            stack = 0;
    }

    protected void HalveStack()
    {
        stack /= 2;
    }

    public virtual string GetDesc()
    {
        string template = LocalizationKeys.GetBuffDescription(buffType);
        if (string.IsNullOrEmpty(template))
            return string.Empty;
        return template.Contains("{0}") ? string.Format(template, stack) : template;
    }

    public static BuffModel Create(BuffEnum buffType, int stack)
    {
        switch (buffType)
        {
            case BuffEnum.Vulnerable:
                return new VulnerableBuffModel(stack);
            case BuffEnum.Slow:
                return new SlowBuffModel(stack);
            case BuffEnum.Weak:
                return new WeakBuffModel(stack);
            case BuffEnum.Arc:
                return new ArcBuffModel(stack);
            case BuffEnum.Burning:
                return new BurningBuffModel(stack);
            case BuffEnum.BurningNextTurn:
                return new BurningNextTurnBuffModel(stack);
            case BuffEnum.SpellPower:
                return new SpellPowerBuffModel(stack);
            case BuffEnum.DefensePower:
                return new DefensePowerBuffModel(stack);
            case BuffEnum.ShieldReflect:
                return new ShieldReflectBuffModel(stack);
            case BuffEnum.ExtraDraw:
                return new ExtraDrawBuffModel(stack);
            case BuffEnum.ExtraRefresh:
                return new ExtraRefreshBuffModel(stack);
            case BuffEnum.Sturdy:
                return new SturdyBuffModel(stack);
            case BuffEnum.Stable:
                return new StableBuffModel(stack);
            case BuffEnum.Disorder:
                return new DisorderBuffModel(stack);
            default:
                return new BuffModel(buffType, stack);
        }
    }

    public static BuffKindEnum GetKind(BuffEnum buffType)
    {
        switch (buffType)
        {
            case BuffEnum.Vulnerable:
            case BuffEnum.Slow:
            case BuffEnum.Weak:
            case BuffEnum.Arc:
            case BuffEnum.Burning:
            case BuffEnum.BurningNextTurn:
                return BuffKindEnum.DeBuff;
            case BuffEnum.SpellPower:
            case BuffEnum.DefensePower:
            case BuffEnum.ShieldReflect:
            case BuffEnum.ExtraDraw:
            case BuffEnum.ExtraRefresh:
            case BuffEnum.Sturdy:
            case BuffEnum.Stable:
            case BuffEnum.Disorder:
            case BuffEnum.Shield:
                return BuffKindEnum.Buff;
            default:
                return BuffKindEnum.Neutral;
        }
    }
}

public class CombatantModel
{
    private readonly PlayerState player;
    private readonly EnemyModel enemy;

    public PlayerState Player => player;
    public EnemyModel Enemy => enemy;
    public bool IsPlayer => player != null;
    public bool IsEnemy => enemy != null;
    public IReadOnlyDictionary<BuffEnum, BuffModel> Buffs => IsPlayer ? player.Buffs : enemy.Buffs;
    public int CurrentHealth => IsPlayer ? player.CurrentHealth : enemy.CurrentHealth;
    public int Shield => IsPlayer ? player.Shield : enemy.Shield;
    public bool IsDead => IsPlayer ? player.CurrentHealth <= 0 : enemy.IsDead;

    public CombatantModel(PlayerState player)
    {
        this.player = player;
    }

    public CombatantModel(EnemyModel enemy)
    {
        this.enemy = enemy;
    }

    public void TakeDamage(int damage)
    {
        if (IsPlayer)
            player.TakeDamage(damage);
        else
            enemy.TakeDamage(damage);
    }

    public void TakeDirectDamage(int damage)
    {
        if (IsPlayer)
            player.TakeDirectDamage(damage);
        else
            enemy.TakeDirectDamage(damage);
    }

    public void GainShield(int amount)
    {
        if (IsPlayer)
            player.GainShield(amount);
        else
            enemy.GainShield(amount);
    }

    public void ConsumeShield(int amount)
    {
        if (IsPlayer)
            player.ConsumeShield(amount);
        else
            enemy.ConsumeShield(amount);
    }

    public void AddBuff(BuffEnum buffType, int stack)
    {
        if (IsPlayer)
            player.AddBuff(buffType, stack);
        else
            enemy.AddBuff(buffType, stack);
    }
}
