public class TangramMagicModel : MagicModel
{
    public TangramMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    public override MagicEffectType EffectType => MagicEffectType.DrawNextTurn;
    public override bool CastParticleTargetsPlayer => true;

    protected override void ResolveCast(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        AddTemporaryMaterialToHand(playerState, MaterialEnum.Wind);
        AddTemporaryMaterialToHand(playerState, MaterialEnum.Water);
    }
}
