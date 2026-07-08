public class TideHandMagicModel : MagicModel
{
    public TideHandMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    public override MagicEffectType EffectType => MagicEffectType.Damage;
    public override bool CastParticleTargetsAllEnemies => true;

    protected override void ResolveCast(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        DamageAllTimes(playerState, battleManager, 1, 10, result);
        AddAllEnemyDebuffStacks(battleManager, 2);
    }
}
