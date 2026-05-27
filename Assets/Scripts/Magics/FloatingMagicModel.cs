public class FloatingMagicModel : ScriptedMagicModel
{
    public FloatingMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    protected override void CastScript(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        playerState.KeepHandOnEndTurn = true;
    }
}
