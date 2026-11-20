public sealed class RetaliationRelicModel : RelicModel
{
    public RetaliationRelicModel(RelicData data) : base(data) { }
    public override void OnEvent(RelicContext context, RelicEvent value)
    {
        if (value.Kind != RelicEventKind.DamageResolved || value.Target?.Player != context.Player ||
            value.Amount <= 0 || value.Source?.Enemy == null || value.Source.IsDead) return;
        context.Damage(value.Source, Data.value);
    }
}
