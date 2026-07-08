public class ThornBushMagicModel : MagicModel
{
    public ThornBushMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    public override MagicEffectType EffectType => MagicEffectType.GainShield;
    public override bool CastParticleTargetsPlayer => true;
    protected override void ResolveCast(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        GainShield(playerState, battleManager, 3, result);
        playerState.AddBuff(BuffEnum.ShieldReflect, 1);
    }
}
