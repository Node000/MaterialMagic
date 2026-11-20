public sealed class TurnBurnRelicModel : RelicModel
{
    public TurnBurnRelicModel(RelicData data) : base(data) { }
    public override void OnEvent(RelicContext context, RelicEvent value)
    {
        if (value.Kind != RelicEventKind.TurnPreparing || context.Battle == null) return;
        var target = context.Battle.GetFirstAliveEnemy();
        if (target != null) context.GiveBuff(new CombatantModel(target), BuffEnum.Burning, Data.value);
    }
}
