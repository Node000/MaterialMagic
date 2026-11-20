public sealed class OpeningShieldRelicModel : RelicModel
{
    public OpeningShieldRelicModel(RelicData data) : base(data) { }
    public override void ResetBattleState() { Set("pending", 1); }
    public override void OnEvent(RelicContext context, RelicEvent value)
    {
        if (value.Kind != RelicEventKind.TurnPreparing || Get("pending") == 0) return;
        Set("pending", 0);
        context.GainShield(Data.value);
    }
}
