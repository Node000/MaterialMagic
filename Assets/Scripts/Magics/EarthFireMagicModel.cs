public class EarthFireMagicModel : MagicModel
{
    public EarthFireMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    public override MagicEffectType EffectType => MagicEffectType.ApplyBuff;
    public override bool CastParticleTargetsPlayer => true;
    protected override void ResolveCast(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        playerState.AddBuff(BuffEnum.BurningOnEnemyAttack, 3);
    }
}
