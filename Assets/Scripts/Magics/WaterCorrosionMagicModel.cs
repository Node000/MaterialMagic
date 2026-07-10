public class WaterCorrosionMagicModel : MagicModel
{
    public WaterCorrosionMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    public override MagicEffectType EffectType => MagicEffectType.ApplyBuff;
    protected override void ResolveCast(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        EnemyModel target = Target(battleManager);
        AddBuff(target, BuffEnum.Vulnerable, 2, result);
    }
}
