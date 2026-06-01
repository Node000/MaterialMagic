using System.Collections.Generic;

public static class MagicFactory
{
    public static MagicModel Create(MagicData data, int slotIndex = 0)
    {
        if (data == null)
            return null;

        switch (data.numericId)
        {
            case 1: return new IgniteMagicModel(data, slotIndex);
            case 2: return new FireballArtMagicModel(data, slotIndex);
            case 3: return new FlameBarrierMagicModel(data, slotIndex);
            case 4: return new MagmaMagicModel(data, slotIndex);
            case 5: return new MoltenMagicModel(data, slotIndex);
            case 6: return new ExplosionMagicModel(data, slotIndex);
            case 7: return new BurningHandMagicModel(data, slotIndex);
            case 8: return new FlameDemonMagicModel(data, slotIndex);
            case 9: return new AirflowMagicModel(data, slotIndex);
            case 10: return new WindBladeMagicModel(data, slotIndex);
            case 11: return new BurningWindMagicModel(data, slotIndex);
            case 12: return new LightningMagicModel(data, slotIndex);
            case 13: return new StormHandMagicModel(data, slotIndex);
            case 14: return new CurrentAttachmentMagicModel(data, slotIndex);
            case 15: return new SandstormMagicModel(data, slotIndex);
            case 16: return new GaleMagicModel(data, slotIndex);
            case 17: return new IcePickMagicModel(data, slotIndex);
            case 18: return new SwampMagicModel(data, slotIndex);
            case 19: return new PoisonFogMagicModel(data, slotIndex);
            case 20: return new BoilingRainMagicModel(data, slotIndex);
            case 21: return new BlizzardMagicModel(data, slotIndex);
            case 22: return new TurbidCurrentMagicModel(data, slotIndex);
            case 23: return new TideHandMagicModel(data, slotIndex);
            case 24: return new WaterCorrosionMagicModel(data, slotIndex);
            case 25: return new StoneWallMagicModel(data, slotIndex);
            case 26: return new RockfallMagicModel(data, slotIndex);
            case 27: return new EarthFireMagicModel(data, slotIndex);
            case 28: return new PetrifyMagicModel(data, slotIndex);
            case 29: return new FloatingMagicModel(data, slotIndex);
            case 30: return new ThornBushMagicModel(data, slotIndex);
            case 31: return new RefineMagicModel(data, slotIndex);
            case 32: return new EarthHandMagicModel(data, slotIndex);
            default: return new MagicModel(data, slotIndex);
        }
    }
}

public abstract class ScriptedMagicModel : MagicModel
{
    protected ScriptedMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex)
    {
    }

    protected override void ResolveCast(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        CastScript(playerState, battleManager, result);
    }

    protected abstract void CastScript(PlayerState playerState, BattleManager battleManager, MagicCastResult result);

    protected EnemyModel Target(BattleManager battleManager)
    {
        return battleManager?.GetTargetEnemy();
    }

    protected void Damage(PlayerState playerState, EnemyModel target, int damage, MagicCastResult result)
    {
        if (target == null || damage <= 0)
            return;

        CombatantModel targetCombatant = new CombatantModel(target);
        CombatantModel caster = new CombatantModel(playerState);
        int attackValue = damage;
        TriggerMagicBeforeAttack(target, ref attackValue);
        playerState.TriggerOnAttack(targetCombatant, ref attackValue);
        GameLog.Data($"Scripted magic {Id} damage target={target.Id} value={attackValue}");
        int shieldBefore = target.Shield;
        int attackResult = target.TakeDamage(attackValue, caster);
        int shieldBlocked = shieldBefore - target.Shield;
        if (shieldBlocked < 0)
            shieldBlocked = 0;
        TriggerMagicAfterAttack(target, ref attackResult);
        result.AddEnemyDamageHit(target, attackResult, shieldBlocked);
    }

    protected void DamageTarget(PlayerState playerState, BattleManager battleManager, int damage, MagicCastResult result)
    {
        Damage(playerState, Target(battleManager), damage, result);
    }

    protected void DamageAll(PlayerState playerState, BattleManager battleManager, int damage, MagicCastResult result)
    {
        if (battleManager == null)
            return;

        IReadOnlyList<EnemyModel> enemies = battleManager.Enemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyModel enemy = enemies[i];
            if (enemy != null && !enemy.IsDead)
                Damage(playerState, enemy, damage, result);
        }
    }

    protected void AddBuff(EnemyModel target, BuffEnum buffType, int stack, MagicCastResult result)
    {
        if (target == null || buffType == BuffEnum.None || stack <= 0)
            return;

        target.AddBuff(buffType, stack);
        GameLog.Data($"Scripted magic {Id} add buff target={target.Id} buff={buffType} stack={stack}");
        result.enemyBuffApplied = true;
    }

    protected void AddBuffAll(BattleManager battleManager, BuffEnum buffType, int stack, MagicCastResult result)
    {
        if (battleManager == null)
            return;

        IReadOnlyList<EnemyModel> enemies = battleManager.Enemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyModel enemy = enemies[i];
            if (enemy != null && !enemy.IsDead)
                AddBuff(enemy, buffType, stack, result);
        }
    }

    protected bool UseExtraRefreshChance(PlayerState playerState)
    {
        return playerState.UseExtraRefreshChance();
    }

    protected void AddBuffSelf(PlayerState playerState, BuffEnum buffType, int stack)
    {
        playerState.AddBuff(buffType, stack);
    }

    protected MaterialModel AddTemporaryMaterialToHand(PlayerState playerState, MaterialEnum material)
    {
        return playerState.AddTemporaryMaterialNextTurn(material, true);
    }

    protected MaterialModel AddMaterialNextTurn(PlayerState playerState, MaterialEnum material, MaterialModifierModel modifier)
    {
        return playerState.AddMaterialNextTurn(material, modifier);
    }

    protected int GetBuffStack(EnemyModel target, BuffEnum buffType)
    {
        return target != null ? target.GetBuffStack(buffType) : 0;
    }

    protected int GetTotalEnemyDebuffStacks(BattleManager battleManager)
    {
        if (battleManager == null)
            return 0;

        int total = 0;
        IReadOnlyList<EnemyModel> enemies = battleManager.Enemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyModel enemy = enemies[i];
            if (enemy == null || enemy.IsDead)
                continue;

            foreach (BuffModel buff in enemy.Buffs.Values)
            {
                if (buff.IsDeBuff)
                    total += buff.stack;
            }
        }
        return total;
    }

    protected void AddAllEnemyDebuffStacks(BattleManager battleManager, int amount)
    {
        if (battleManager == null || amount <= 0)
            return;

        IReadOnlyList<EnemyModel> enemies = battleManager.Enemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyModel enemy = enemies[i];
            if (enemy == null || enemy.IsDead)
                continue;

            if (enemy.GetBuffStack(BuffEnum.Vulnerable) > 0)
                enemy.AddBuff(BuffEnum.Vulnerable, amount);
            if (enemy.GetBuffStack(BuffEnum.Slow) > 0)
                enemy.AddBuff(BuffEnum.Slow, amount);
            if (enemy.GetBuffStack(BuffEnum.Weak) > 0)
                enemy.AddBuff(BuffEnum.Weak, amount);
            if (enemy.GetBuffStack(BuffEnum.Arc) > 0)
                enemy.AddBuff(BuffEnum.Arc, amount);
            if (enemy.GetBuffStack(BuffEnum.Burning) > 0)
                enemy.AddBuff(BuffEnum.Burning, amount);
            if (enemy.GetBuffStack(BuffEnum.BurningNextTurn) > 0)
                enemy.AddBuff(BuffEnum.BurningNextTurn, amount);
        }
    }

    protected void GainShield(PlayerState playerState, BattleManager battleManager, int amount, MagicCastResult result)
    {
        int shieldValue = amount;
        TriggerMagicBeforeGainShield(ref shieldValue);
        int shieldGain = playerState.GainShield(shieldValue);
        TriggerMagicAfterGainShield(ref shieldGain);
        GameLog.Data($"Scripted magic {Id} gain shield={shieldGain}");
        result.playerShield += shieldGain;
        TriggerShieldAttack(playerState, battleManager, shieldGain, result);
    }
}

