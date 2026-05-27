public class GaleMagicModel : ScriptedMagicModel
{
    public GaleMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    protected override void CastScript(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        playerState.DrawCardsToPlayZoneTail(1);
        playerState.AddBuff(BuffEnum.ExtraDraw, 1);
    }
}
