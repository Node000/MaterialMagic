public class ExplosionMagicModel : MagicModel
{
    public ExplosionMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    public override MagicEffectType EffectType => MagicEffectType.Damage;
    protected override void ResolveCast(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        TriggerBurning(playerState, Target(battleManager), result);
    }

    private void TriggerBurning(PlayerState playerState, EnemyModel target, MagicCastResult result)
    {
        int burning = target != null ? target.GetBuffStack(BuffEnum.Burning) : 0;
        if (burning <= 0)
            return;

        CombatDamageResult damageResult = target.TakeDamageResult(burning, null);
        int damage = damageResult.HealthDamage + damageResult.ShieldDamage;
        if (damage > 0)
            playerState.TriggerAfterEnemyBurningDamage(target, damage);
        target.ConsumeBuff(BuffEnum.Burning, burning - burning / 2);
        result.AddEnemyDamageHit(target, damageResult.HealthDamage, damageResult.ShieldDamage);
    }
}
