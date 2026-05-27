public class ExtraRefreshBuffModel : BuffModel
{
    public ExtraRefreshBuffModel(int stack) : base(BuffEnum.ExtraRefresh, stack)
    {
    }

    public override void OnTurnEnd(CombatantModel self, CombatantModel opponent)
    {
        stack = 0;
    }
}
