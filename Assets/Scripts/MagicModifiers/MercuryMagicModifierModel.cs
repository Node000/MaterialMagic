public class MercuryMagicModifierModel : MagicModifierModel
{
    public MercuryMagicModifierModel(MagicModifierData data) : base(data)
    {
    }

    public override void AfterCast(MagicCastResult result)
    {
        MagicModifierContext context = Context;
        EnemyModel target = context != null && context.BattleManager != null ? context.BattleManager.GetRandomAliveEnemy() : null;
        if (target == null || target.IsDead)
            return;

        target.AddBuff(BuffEnum.Weak, Data != null && Data.value > 0 ? Data.value : 2, context.PlayerState != null ? new CombatantModel(context.PlayerState) : null);
        if (result != null)
            result.enemyBuffApplied = true;
    }
}
