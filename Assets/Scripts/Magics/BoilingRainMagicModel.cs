public class BoilingRainMagicModel : MagicModel
{
    public BoilingRainMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    public override MagicEffectType EffectType => MagicEffectType.Damage;
    public override bool CastParticleTargetsAllEnemies => true;

    protected override void ResolveCast(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        DamageAll(playerState, battleManager, 1, result);
        DamageAll(playerState, battleManager, 1, result);
        DamageAll(playerState, battleManager, 1, result);
    }
}
