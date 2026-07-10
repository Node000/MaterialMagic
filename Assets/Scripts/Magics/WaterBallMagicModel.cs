public class WaterBallMagicModel : MagicModel
{
    public WaterBallMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    public override MagicEffectType EffectType => MagicEffectType.DrawNextTurn;
    public override bool CastParticleTargetsPlayer => true;

    protected override void ResolveCast(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        int extraDraw = GetTotalEnemyDebuffStacks(battleManager) / 3;
        if (extraDraw > 0)
            playerState.AddBuff(BuffEnum.ExtraDraw, extraDraw);
    }
}
