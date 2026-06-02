public class SandstormMagicModel : ScriptedMagicModel
{
    public SandstormMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    protected override void CastScript(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        GainShield(playerState, battleManager, 3, result);
        AddBuffAll(battleManager, BuffEnum.Arc, 1, result);
    }
}
