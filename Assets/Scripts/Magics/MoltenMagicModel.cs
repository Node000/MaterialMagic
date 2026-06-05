public class MoltenMagicModel : MagicModel
{
    public MoltenMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    public override MagicEffectType EffectType => MagicEffectType.Damage;
    protected override void ResolveCast(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        EnemyModel target = Target(battleManager);
        Damage(playerState, target, 9 + GetBuffStack(target, BuffEnum.Burning), result);
    }
}
