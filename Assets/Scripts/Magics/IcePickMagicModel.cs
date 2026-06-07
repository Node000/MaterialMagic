public class IcePickMagicModel : MagicModel
{
    public IcePickMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    public override MagicEffectType EffectType => MagicEffectType.Damage;
    protected override void ResolveCast(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        EnemyModel target = Target(battleManager);
        AddBuff(target, BuffEnum.Vulnerable, 2, result);
        DamageTargetTimes(playerState, battleManager, 1, 2, result);
    }
}
