public class BurningWindMagicModel : ScriptedMagicModel
{
    public BurningWindMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    protected override void CastScript(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        playerState.AddBuff(BuffEnum.RepeatSpell, 1);
    }
}
