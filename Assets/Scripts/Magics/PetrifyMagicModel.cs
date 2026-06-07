public class PetrifyMagicModel : MagicModel
{
    public PetrifyMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    public override MagicEffectType EffectType => MagicEffectType.ApplyBuff;
    protected override void ResolveCast(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        EnemyModel target = Target(battleManager);
        AddBuff(target, BuffEnum.Slow, 2, result);
        AddBuff(target, BuffEnum.Weak, 2, result);
    }
}
