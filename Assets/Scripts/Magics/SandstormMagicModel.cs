public class SandstormMagicModel : MagicModel
{
    public SandstormMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    public override MagicEffectType EffectType => MagicEffectType.GainShield;
    public override bool CastParticleTargetsPlayer => true;
    protected override void ResolveCast(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        GainShield(playerState, battleManager, 3, result);
        AddBuffAll(battleManager, BuffEnum.Arc, 1, result);
    }
}
