public class StormHandMagicModel : ScriptedMagicModel
{
    public StormHandMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    protected override void CastScript(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        AddBuffAll(battleManager, BuffEnum.Arc, 4, result);
    }
}
