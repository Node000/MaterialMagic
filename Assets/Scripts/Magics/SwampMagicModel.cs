public class SwampMagicModel : MagicModel
{
    public SwampMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    public override MagicEffectType EffectType => MagicEffectType.ApplyBuff;
    public override bool CastParticleTargetsAllEnemies => true;
    protected override void ResolveCast(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        AddBuffAll(battleManager, BuffEnum.DebuffPower, 1, result);
    }
}
