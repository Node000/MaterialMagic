public sealed class EliteHealingRelicModel : RelicModel
{
    public EliteHealingRelicModel(RelicData data) : base(data) { }
    public override void OnEvent(RelicContext context, RelicEvent value)
    {
        if (value.Kind == RelicEventKind.BattleEnded && value.Victory && context.Battle?.CurrentLevel?.levelType == LevelType.Elite)
            context.Heal(Data.value);
    }
}
