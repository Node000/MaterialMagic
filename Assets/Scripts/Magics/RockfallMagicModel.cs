public class RockfallMagicModel : MagicModel
{
    public RockfallMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    public override MagicEffectType EffectType => MagicEffectType.GainShield;
    public override bool CastParticleTargetsPlayer => true;
    protected override void ResolveCast(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        GainShield(playerState, battleManager, 6, result);
        AddBuffSelf(playerState, BuffEnum.Sturdy, 1);
    }
}
