public sealed class BuffStudyRelicModel : RelicModel
{
    public BuffStudyRelicModel(RelicData data) : base(data) { }
    public override void OnEvent(RelicContext context, RelicEvent value)
    {
        if (value.Kind != RelicEventKind.BuffApplied || value.Source?.Player != context.Player ||
            value.Target?.Enemy == null || value.Amount <= 0 || BuffModel.GetKind(value.BuffType) != BuffKindEnum.DeBuff) return;
        int count = Get("counter") + 1;
        int threshold = System.Math.Max(1, Data.threshold);
        Set("counter", count % threshold);
        if (count >= threshold) context.GiveBuff(new CombatantModel(context.Player), BuffEnum.ExtraDraw, Data.value);
    }
}
