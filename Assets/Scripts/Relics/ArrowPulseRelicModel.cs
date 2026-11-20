public sealed class ArrowPulseRelicModel : RelicModel
{
    public ArrowPulseRelicModel(RelicData data) : base(data) { }
    public override void OnEvent(RelicContext context, RelicEvent value)
    {
        if (value.Kind != RelicEventKind.ArrowRead || value.IsReplay || context.Battle == null) return;
        int threshold = System.Math.Max(1, Data.threshold);
        int count = Get("counter") + 1;
        if (count < threshold) { Set("counter", count); return; }
        var target = context.Battle.GetFirstAliveEnemy();
        if (target == null) { Set("counter", threshold); return; }
        Set("counter", count % threshold);
        context.Damage(new CombatantModel(target), Data.value);
    }
}
