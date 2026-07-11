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
    public virtual bool IsVisible => true;

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

    public virtual void AfterTurnStartDraw(CombatantModel self, CombatantModel opponent, int drawCount)
    {
    }

    public virtual void AfterDraw(CombatantModel self, MaterialModel card)
    {
    }

    public virtual void AfterDiscard(CombatantModel self, MaterialModel card)
    {
    }

    public virtual void AfterMaterialConsumed(CombatantModel self, MaterialModel card)
    {
    }

    public virtual void AfterArrowConsumed(CombatantModel self, MaterialModel card)
    {
    }

    public virtual void AfterEnemyBurningDamage(CombatantModel self, EnemyModel enemy, int damage)
    {
    }

    public virtual void OnInvoke(CombatantModel self, CombatantModel target)
    {
    }

    public virtual void AfterPlayerDecide(CombatantModel self, CombatantModel opponent)
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

    public virtual void OnTakeDamage(CombatantModel self, CombatantModel attacker, ref int damage)
    {
    }

    public virtual void OnGiveBuff(CombatantModel self, CombatantModel target, BuffEnum buffType, ref int stack)
    {
    }

    public virtual void OnReceiveBuff(CombatantModel self, CombatantModel source, BuffEnum buffType, ref int stack)
    {
    }

    public virtual void AfterGiveBuff(CombatantModel self, CombatantModel target, BuffEnum buffType, int stack)
    {
    }

    public virtual void AfterTakeDamage(CombatantModel self, CombatantModel attacker, CombatDamageResult result)
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

    public virtual void OnPlayerTurnEnd(CombatantModel self, CombatantModel opponent)
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

    public virtual string GetSlotStackText()
    {
        if (buffType == BuffEnum.Claw)
            return string.Empty;

        return stack > 1 ? stack.ToString() : string.Empty;
    }

    public virtual string GetTooltipStackText()
    {
        if (buffType == BuffEnum.Claw)
            return string.Empty;

        return stack > 0 ? stack.ToString() : string.Empty;
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
            case BuffEnum.BurnOnAttack:
                return new BurnOnAttackBuffModel(stack);
            case BuffEnum.RepeatSpell:
                return new RepeatSpellBuffModel(stack);
            case BuffEnum.DebuffPower:
                return new DebuffPowerBuffModel(stack);
            case BuffEnum.VortexNextDraw:
                return new VortexNextDrawBuffModel(stack);
            case BuffEnum.EggDeathExplosion:
                return new EggDeathExplosionBuffModel(stack);
            case BuffEnum.ShieldOnHealthLoss:
                return new ShieldOnHealthLossBuffModel(stack);
            case BuffEnum.ShuffleHandOnInvokeChance:
                return new ShuffleHandOnInvokeChanceBuffModel(stack);
            case BuffEnum.AttributeDisabled:
                return new AttributeDisabledBuffModel(stack);
            case BuffEnum.Curse:
                return new CurseBuffModel(stack);
            case BuffEnum.DirectionDamageBonus:
                return new DirectionDamageBonusBuffModel(stack);
            case BuffEnum.DirectionWeakBonus:
                return new DirectionWeakBonusBuffModel(stack);
            case BuffEnum.DirectionExtraDraw:
                return new DirectionExtraDrawBuffModel(stack);
            case BuffEnum.DirectionShieldBonus:
                return new DirectionShieldBonusBuffModel(stack);
            case BuffEnum.MaterialOverplayDebuff:
                return new MaterialOverplayDebuffBuffModel(stack);
            case BuffEnum.PreparedShield:
                return new PreparedShieldBuffModel(stack);
            case BuffEnum.KindlingNextDraw:
                return new KindlingNextDrawBuffModel(stack);
            case BuffEnum.DrawOnEnemyAttack:
                return new DrawOnEnemyAttackBuffModel(stack);
            case BuffEnum.BurningOnEnemyAttack:
                return new BurningOnEnemyAttackBuffModel(stack);
            case BuffEnum.ExtraDrawOnEnemyDamage:
                return new ExtraDrawOnEnemyDamageBuffModel(stack);
            case BuffEnum.ShieldReflectBoost:
                return new ShieldReflectBoostBuffModel(stack);
            case BuffEnum.MagicAttackAll:
                return new MagicAttackAllBuffModel(stack);
            case BuffEnum.NextMagicRepeat:
                return new NextMagicRepeatBuffModel(stack);
            case BuffEnum.KeepHand:
                return new KeepHandBuffModel(stack);
            case BuffEnum.RetainedNextDraw:
                return new RetainedNextDrawBuffModel(stack);
            case BuffEnum.DoubleEnemyBurningOnTurnEnd:
                return new DoubleEnemyBurningOnTurnEndBuffModel(stack);
            case BuffEnum.ExtraEnemyDebuff:
                return new ExtraEnemyDebuffBuffModel(stack);
            case BuffEnum.MaterialBaseEffectRepeat:
                return new MaterialBaseEffectRepeatBuffModel(stack);
            case BuffEnum.KeepShieldNextTurn:
                return new KeepShieldNextTurnBuffModel(stack);
            case BuffEnum.BurningDamageShieldNextTurn:
                return new BurningDamageShieldNextTurnBuffModel(stack);
            case BuffEnum.SpellPowerOnExtraDraw:
                return new SpellPowerOnExtraDrawBuffModel(stack);
            case BuffEnum.WeakOnEnemyAttack:
                return new WeakOnEnemyAttackBuffModel(stack);
            case BuffEnum.TemporaryWindOnMaterialConsumed:
                return new TemporaryWindOnMaterialConsumedBuffModel(stack);
            case BuffEnum.WeakNextTurn:
                return new WeakNextTurnBuffModel(stack);
            case BuffEnum.FoamShield:
                return new FoamShieldBuffModel(stack);
            case BuffEnum.ShieldOnNextDraw:
                return new ShieldOnNextDrawBuffModel(stack);
            case BuffEnum.LazyNextDraw:
                return new LazyNextDrawBuffModel(stack);
            case BuffEnum.ChargeNextDraw:
                return new ChargeNextDrawBuffModel(stack);
            case BuffEnum.TutorialDeath:
                return new TutorialDeathBuffModel(stack);
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
            case BuffEnum.BurnOnAttack:
            case BuffEnum.AttributeDisabled:
            case BuffEnum.Curse:
            case BuffEnum.MaterialOverplayDebuff:
            case BuffEnum.LazyNextDraw:
            case BuffEnum.TutorialDeath:
            case BuffEnum.ShuffleHandOnInvokeChance:
            case BuffEnum.DoubleEnemyBurningOnTurnEnd:
            case BuffEnum.WeakNextTurn:
            case BuffEnum.DebuffPower:
                return BuffKindEnum.DeBuff;
            case BuffEnum.SpellPower:
            case BuffEnum.DefensePower:
            case BuffEnum.RepeatSpell:
            case BuffEnum.VortexNextDraw:
            case BuffEnum.ShieldOnHealthLoss:
            case BuffEnum.DirectionDamageBonus:
            case BuffEnum.DirectionWeakBonus:
            case BuffEnum.DirectionExtraDraw:
            case BuffEnum.DirectionShieldBonus:
            case BuffEnum.PreparedShield:
            case BuffEnum.KindlingNextDraw:
            case BuffEnum.DrawOnEnemyAttack:
            case BuffEnum.BurningOnEnemyAttack:
            case BuffEnum.ExtraDrawOnEnemyDamage:
            case BuffEnum.ShieldReflectBoost:
            case BuffEnum.MagicAttackAll:
            case BuffEnum.NextMagicRepeat:
            case BuffEnum.KeepHand:
            case BuffEnum.RetainedNextDraw:
            case BuffEnum.ExtraEnemyDebuff:
            case BuffEnum.MaterialBaseEffectRepeat:
            case BuffEnum.KeepShieldNextTurn:
            case BuffEnum.BurningDamageShieldNextTurn:
            case BuffEnum.SpellPowerOnExtraDraw:
            case BuffEnum.WeakOnEnemyAttack:
            case BuffEnum.TemporaryWindOnMaterialConsumed:
            case BuffEnum.Claw:
            case BuffEnum.ShieldReflect:
            case BuffEnum.ExtraDraw:
            case BuffEnum.ExtraRefresh:
            case BuffEnum.ChargeNextDraw:
            case BuffEnum.Sturdy:
            case BuffEnum.Stable:
            case BuffEnum.Disorder:
            case BuffEnum.Shield:
            case BuffEnum.EggDeathExplosion:
            case BuffEnum.FoamShield:
            case BuffEnum.ShieldOnNextDraw:
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

    public CombatDamageResult TakeDamageResult(int damage, CombatantModel attacker)
    {
        if (IsPlayer)
            return player.TakeDamageResult(damage, attacker);
        return enemy.TakeDamageResult(damage, attacker);
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
        AddBuff(buffType, stack, null);
    }

    public void AddBuff(BuffEnum buffType, int stack, CombatantModel source)
    {
        if (IsPlayer)
            player.AddBuff(buffType, stack, source);
        else
            enemy.AddBuff(buffType, stack, source);
    }

    public void ConsumeBuff(BuffEnum buffType, int amount)
    {
        if (IsPlayer)
            player.ConsumeBuff(buffType, amount);
        else
            enemy.ConsumeBuff(buffType, amount);
    }
}
