public class MagmaMagicModel : ScriptedMagicModel
{
    public MagmaMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    protected override void CastScript(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        AddBuffAll(battleManager, BuffEnum.Burning, 3, result);
        AddBuffAll(battleManager, BuffEnum.BurningNextTurn, 2, result);
    }
}
