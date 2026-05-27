public class StoneWallMagicModel : ScriptedMagicModel
{
    public StoneWallMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    protected override void CastScript(PlayerState playerState, BattleManager battleManager, MagicCastResult result) => GainShield(playerState, battleManager, 4, result);
}
