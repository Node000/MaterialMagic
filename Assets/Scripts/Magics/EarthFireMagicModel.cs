public class EarthFireMagicModel : MagicModel
{
    public EarthFireMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    public override MagicEffectType EffectType => MagicEffectType.ApplyBuff;
    protected override void ResolveCast(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        AddBuffAll(battleManager, BuffEnum.BurningNextTurn, 2, result);
    }
}
