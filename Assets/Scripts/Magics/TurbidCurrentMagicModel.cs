public class TurbidCurrentMagicModel : ScriptedMagicModel
{
    public TurbidCurrentMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    protected override void CastScript(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        GainShield(playerState, battleManager, 7, result);
        AddMaterialNextTurn(playerState, MaterialEnum.Water, new VortexModifier());
        AddMaterialNextTurn(playerState, MaterialEnum.Water, new VortexModifier());
        AddMaterialNextTurn(playerState, MaterialEnum.Water, new VortexModifier());
    }
}
