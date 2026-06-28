public class ExtraRefreshBuffModel : BuffModel
{
    private bool pendingNextTurn = true;

    public ExtraRefreshBuffModel(int stack) : base(BuffEnum.ExtraRefresh, stack)
    {
    }

    public override void OnTurnStart(CombatantModel self, CombatantModel opponent)
    {
        if (pendingNextTurn)
            pendingNextTurn = false;
    }

    public override void AfterTurnStart(CombatantModel self, CombatantModel opponent)
    {
        if (pendingNextTurn || self?.Player == null || stack <= 0)
            return;

        self.Player.AddExtraRefreshChances(stack);
        stack = 0;
    }
}
