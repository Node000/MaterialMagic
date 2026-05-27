public class FlameBarrierMagicModel : ScriptedMagicModel
{
    public FlameBarrierMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    protected override void CastScript(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        GainShield(playerState, battleManager, 9, result);
        EnemyModel target = Target(battleManager);
        if (target != null)
        {
            EnemyActionData action = target.GetCurrentAction();
            if (action != null && action.actionType == EnemyActionType.Attack)
                AddBuff(target, BuffEnum.Burning, 3, result);
        }
    }
}
