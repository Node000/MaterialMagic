using System;

public sealed class BarChartMagicModel : MagicModel
{
    private int currentSellPrice = -1;

    public BarChartMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }

    protected override bool AllowsModifiers => false;
    protected override MagicMatchRule NativeMatchRule => MagicMatchRule.NoTrigger;
    public override int ModifySellPrice(int basePrice) => currentSellPrice >= 0 ? currentSellPrice : basePrice;

    public override void OnRoomEntered(PlayerStatus player)
    {
        int current = ShopOfferGenerator.GetMagicSellPrice(this, player);
        int delta = player.NextRunRandomInt(-2, 3);
        currentSellPrice = (int)Math.Max(0L, Math.Min(int.MaxValue, (long)current + delta));
    }

    internal override int ExportSellPriceState() => currentSellPrice;
    internal override void RestoreSellPriceState(int value) => currentSellPrice = Math.Max(-1, value);
}
