public class RetainedNextDrawBuffModel : BuffModel
{
    private bool pendingNextTurn = true;
    public override System.Collections.Generic.Dictionary<string, int> ExportRuntimeState() => new System.Collections.Generic.Dictionary<string, int> { { "pending", pendingNextTurn ? 1 : 0 } };
    public override void RestoreRuntimeState(System.Collections.Generic.IReadOnlyDictionary<string, int> values) => pendingNextTurn = ReadState(values, "pending", 1) != 0;

    public RetainedNextDrawBuffModel(int stack) : base(BuffEnum.RetainedNextDraw, stack)
    {
    }

    public override void OnTurnStart(CombatantModel self, CombatantModel opponent)
    {
        if (pendingNextTurn)
            pendingNextTurn = false;
    }

    public override void AfterDraw(CombatantModel self, MaterialModel card)
    {
        if (pendingNextTurn || card == null || stack <= 0)
            return;

        if (!card.HasModifier<RetainedArrowModifier>())
        {
            RetainedArrowModifier modifier = new RetainedArrowModifier();
            modifier.MarkRemoveAfterBattle();
            card.AddModifier(modifier);
        }
        ConsumeStack(1);
    }

    public override void AfterTurnStart(CombatantModel self, CombatantModel opponent)
    {
        if (!pendingNextTurn)
            stack = 0;
    }
}
