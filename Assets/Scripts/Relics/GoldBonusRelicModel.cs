public sealed class GoldBonusRelicModel : RelicModel
{
    public GoldBonusRelicModel(RelicData data) : base(data) { }
    public override int ModifyGoldGain(int amount) => amount > 0 ? amount + Data.value : amount;
}
