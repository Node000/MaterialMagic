public class ThornBushMagicModel : ScriptedMagicModel
{
    public ThornBushMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    protected override void CastScript(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        GainShield(playerState, battleManager, 10, result);
        playerState.AddBuff(BuffEnum.ShieldReflect, playerState.Shield);
    }
}
