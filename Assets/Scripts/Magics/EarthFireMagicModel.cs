public class EarthFireMagicModel : ScriptedMagicModel
{
    public EarthFireMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    protected override void CastScript(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        AddBuffAll(battleManager, BuffEnum.BurningNextTurn, 2, result);
    }
}
