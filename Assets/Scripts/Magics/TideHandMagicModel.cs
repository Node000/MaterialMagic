public class TideHandMagicModel : MagicModel
{
    public TideHandMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    public override MagicEffectType EffectType => MagicEffectType.Damage;
    public override bool CastParticleTargetsAllEnemies => true;

    protected override void ResolveCast(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        for (int i = 0; i < 7; i++)
            DamageAll(playerState, battleManager, 1, result);
        AddAllEnemyDebuffStacks(battleManager, 2);
        playerState.AddBuff(BuffEnum.DebuffPower, 1);
    }
}
