public class FloatingMagicModel : MagicModel
{
    public FloatingMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    public override MagicEffectType EffectType => MagicEffectType.GainShield;
    public override bool CastParticleTargetsPlayer => true;
    protected override void ResolveCast(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        GainShield(playerState, battleManager, 3, result);
        playerState.KeepHandOnEndTurn = true;
    }
}
