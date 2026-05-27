public class CurrentAttachmentMagicModel : ScriptedMagicModel
{
    public CurrentAttachmentMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    protected override void CastScript(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        playerState.AddBuff(BuffEnum.SpellPower, 1);
        AddMaterialNextTurn(playerState, MaterialEnum.Wind, new ChargeModifier());
    }
}
