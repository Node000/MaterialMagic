public class PlasticBagMagicModel : MagicModel
{
    public PlasticBagMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    public override MagicEffectType EffectType => MagicEffectType.ApplyBuff;

    protected override void ResolveCast(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        AddBuff(Target(battleManager), BuffEnum.Burning, playerState.ConsumedPile.Count * 2, result);
    }
}
