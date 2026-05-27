public class FlameDemonMagicModel : ScriptedMagicModel
{
    public FlameDemonMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    protected override void CastScript(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        playerState.ReturnLeftmostHandCardToDrawPile();
        AddTemporaryMaterialToHand(playerState, MaterialEnum.Fire);
    }
}
