public class RockfallMagicModel : ScriptedMagicModel
{
    public RockfallMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    protected override void CastScript(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        GainShield(playerState, battleManager, 6, result);
        AddBuffSelf(playerState, BuffEnum.Sturdy, 1);
    }
}
