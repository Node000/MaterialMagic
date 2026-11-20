public class ExtraRefreshBuffModel : BuffModel
{
    private bool pendingNextTurn = true;
    public override System.Collections.Generic.Dictionary<string, int> ExportRuntimeState() => new System.Collections.Generic.Dictionary<string, int> { { "pending", pendingNextTurn ? 1 : 0 } };
    public override void RestoreRuntimeState(System.Collections.Generic.IReadOnlyDictionary<string, int> values) => pendingNextTurn = ReadState(values, "pending", 1) != 0;

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
