public class BlizzardMagicModel : MagicModel
{
    public BlizzardMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    public override MagicEffectType EffectType => MagicEffectType.Damage;
    public override bool CastParticleTargetsAllEnemies => true;

    protected override void ResolveCast(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        AddBuffAll(battleManager, BuffEnum.Vulnerable, 1, result);
        AddBuffAll(battleManager, BuffEnum.Slow, 1, result);
        AddBuffAll(battleManager, BuffEnum.Weak, 1, result);
        DamageAll(playerState, battleManager, GetTotalEnemyDebuffStacks(battleManager), result);
    }
}
