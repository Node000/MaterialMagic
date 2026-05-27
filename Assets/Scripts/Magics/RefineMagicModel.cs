public class RefineMagicModel : ScriptedMagicModel
{
    public RefineMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    protected override void CastScript(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        GainShield(playerState, battleManager, 8, result);
        playerState.AddBuff(BuffEnum.ExtraDraw, 1);
    }
}
