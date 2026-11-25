public sealed class RainBannerBuffModel : BuffModel
{
    private const int ArrowsPerStack = 2;
    private long ArrowCount => (long)stack * ArrowsPerStack;

    public RainBannerBuffModel(int stack) : base(BuffEnum.RainBanner, stack) { }

    public override string GetTooltipStackText() => string.Empty;

    public override string GetDesc() => string.Format(LocalizationKeys.GetBuffDescription(buffType), ArrowCount);

    public override void AfterTurnStart(CombatantModel self, CombatantModel opponent)
    {
        PlayerState player = self?.Player;
        if (player == null || stack <= 0)
            return;

        long count = ArrowCount;
        player.ConsumeBuff(buffType, stack);
        // These arrows enter the hand without being drawn, so draw effects cannot add Retained.
        for (long i = 0; i < count; i++)
            if (player.GrantTemporaryMaterialToHand(MaterialEnum.Water) == null)
                break;
    }
}
