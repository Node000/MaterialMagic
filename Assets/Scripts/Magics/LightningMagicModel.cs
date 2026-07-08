public class LightningMagicModel : MagicModel
{
    public LightningMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    public override MagicEffectType EffectType => MagicEffectType.ApplyBuff;
    protected override void ResolveCast(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        EnemyModel target = Target(battleManager);
        DamageTarget(playerState, battleManager, 2, result);
        AddBuff(target, BuffEnum.Arc, 2, result);
    }
}
