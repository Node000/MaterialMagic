public class ExplosionMagicModel : MagicModel
{
    public ExplosionMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    public override MagicEffectType EffectType => MagicEffectType.Damage;
    protected override void ResolveCast(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        EnemyModel target = Target(battleManager);
        AddBuff(target, BuffEnum.Burning, 2, result);
        int damage = target != null ? target.GetBuffStack(BuffEnum.Burning) : 0;
        Damage(playerState, target, damage, result);
    }
}
