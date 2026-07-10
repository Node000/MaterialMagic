public class RainBannerMagicModel : MagicModel
{
    public RainBannerMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    public override MagicEffectType EffectType => MagicEffectType.DrawNextTurn;
    public override bool CastParticleTargetsPlayer => true;

    protected override void ResolveCast(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        for (int i = 0; i < 5; i++)
            playerState.AddTemporaryMaterialToHand(MaterialEnum.Water);
    }
}
