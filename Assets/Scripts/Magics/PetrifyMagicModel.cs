public class PetrifyMagicModel : MagicModel
{
    public PetrifyMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    public override MagicEffectType EffectType => MagicEffectType.Damage;
    protected override void ResolveCast(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        EnemyModel target = Target(battleManager);
        Damage(playerState, target, 4, result);
        AddBuff(target, BuffEnum.Weak, 1, result);
        AddBuff(target, BuffEnum.Slow, 1, result);
    }
}
