public class BoilingRainMagicModel : ScriptedMagicModel
{
    public BoilingRainMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    protected override void CastScript(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        DamageAll(playerState, battleManager, 1, result);
        DamageAll(playerState, battleManager, 1, result);
        DamageAll(playerState, battleManager, 1, result);
    }
}
