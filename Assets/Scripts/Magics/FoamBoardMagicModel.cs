public class FoamBoardMagicModel : MagicModel
{
    public FoamBoardMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    public override MagicEffectType EffectType => MagicEffectType.ApplyBuff;
    public override bool CastParticleTargetsPlayer => true;

    protected override void ResolveCast(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        playerState.AddBuff(BuffEnum.FoamShield, 1);
    }
}
