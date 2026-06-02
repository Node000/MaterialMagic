public class FlameBarrierMagicModel : ScriptedMagicModel
{
    public FlameBarrierMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    protected override void CastScript(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        AddBuff(Target(battleManager), BuffEnum.BurnOnAttack, 3, result);
    }
}
