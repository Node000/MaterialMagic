public class StormHandMagicModel : ScriptedMagicModel
{
    public StormHandMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    protected override void CastScript(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        playerState.AddBuff(BuffEnum.ShieldReflect, 1);
        AddBuffAll(battleManager, BuffEnum.Arc, 1, result);
    }
}
