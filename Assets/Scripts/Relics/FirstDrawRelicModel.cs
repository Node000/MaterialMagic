public sealed class FirstDrawRelicModel : RelicModel
{
    public FirstDrawRelicModel(RelicData data) : base(data) { }
    public override void ResetTurnState() { Set("used", 0); }
    public override void OnEvent(RelicContext context, RelicEvent value)
    {
        if (value.Kind != RelicEventKind.CardDrawn || context.Battle == null || Get("used") != 0) return;
        Set("used", 1);
        context.Draw(Data.value);
    }
}
