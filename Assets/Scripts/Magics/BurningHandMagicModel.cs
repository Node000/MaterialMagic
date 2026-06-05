public class BurningHandMagicModel : MagicModel
{
    public BurningHandMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    public override MagicEffectType EffectType => MagicEffectType.ApplyBuff;
    protected override void ResolveCast(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        playerState.AddBuff(BuffEnum.SpellPower, 2);
        AddBuffAll(battleManager, BuffEnum.Burning, 8, result);
    }
}
