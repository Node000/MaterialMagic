public class BoxingGloveMagicModel : MagicModel
{
    public BoxingGloveMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    public override MagicEffectType EffectType => MagicEffectType.None;
    public override bool CastParticleTargetsPlayer => true;

    protected override void ResolveCast(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        playerState.AddBuff(BuffEnum.MaterialBaseEffectRepeat, 1);
    }
}
