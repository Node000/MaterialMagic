public class SearingMagicModifierModel : MagicModifierModel
{
    public SearingMagicModifierModel(MagicModifierData data) : base(data)
    {
    }

    public override void AfterCast(MagicCastResult result)
    {
        EnemyModel target = Context != null && Context.BattleManager != null ? Context.BattleManager.GetTargetEnemy() : null;
        if (target == null && Context != null && Context.Targets != null)
        {
            for (int i = 0; i < Context.Targets.Count; i++)
            {
                if (Context.Targets[i] != null && !Context.Targets[i].IsDead)
                {
                    target = Context.Targets[i];
                    break;
                }
            }
        }

        if (target == null || target.IsDead)
            return;

        target.AddBuff(BuffEnum.Burning, Data != null && Data.value > 0 ? Data.value : 1);
        if (result != null)
            result.enemyBuffApplied = true;
    }
}
