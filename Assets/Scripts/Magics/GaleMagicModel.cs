public class GaleMagicModel : MagicModel
{
    public GaleMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    public override MagicEffectType EffectType => MagicEffectType.DrawNextTurn;
    public override bool CastParticleTargetsPlayer => true;
    protected override void ResolveCast(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        playerState.AddBuff(BuffEnum.ExtraDraw, 1);
        playerState.AddBuff(BuffEnum.RetainedNextDraw, 3);
    }
}
