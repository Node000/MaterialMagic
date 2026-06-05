public class MagmaMagicModel : MagicModel
{
    public MagmaMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    public override MagicEffectType EffectType => MagicEffectType.ApplyBuff;
    protected override void ResolveCast(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        AddBuffAll(battleManager, BuffEnum.Burning, 3, result);
    }
}
