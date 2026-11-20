public sealed class PeriodicTemporaryArrowRelicModel : RelicModel
{
    private const string TurnsUntilGrant = "turnsUntilGrant";

    public PeriodicTemporaryArrowRelicModel(RelicData data) : base(data) { }

    internal static bool IsValid(RelicData data) => data.value > 0 && data.threshold > 0 &&
        (data.material == MaterialEnum.Fire || data.material == MaterialEnum.Wind ||
         data.material == MaterialEnum.Water || data.material == MaterialEnum.Earth);

    public override void ResetBattleState() => Set(TurnsUntilGrant, 1);

    public override void OnEvent(RelicContext context, RelicEvent value)
    {
        // TurnStarted follows the normal draw and its follow-up hooks.
        if (value.Kind != RelicEventKind.TurnStarted || context.Battle == null) return;

        int remaining = Get(TurnsUntilGrant);
        if (remaining == 0) return; // Acquired after battle start; wait for the next battle.
        if (remaining > 1)
        {
            Set(TurnsUntilGrant, remaining - 1);
            return;
        }

        Set(TurnsUntilGrant, Data.threshold);
        context.GrantTemporaryArrows(Data.material, Data.value);
    }
}
