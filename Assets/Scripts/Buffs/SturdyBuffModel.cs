public class SturdyBuffModel : BuffModel
{
    private bool pendingNextTurn = true;

    public SturdyBuffModel(int stack) : base(BuffEnum.Sturdy, stack)
    {
    }

    public override void OnTurnStart(CombatantModel self, CombatantModel opponent)
    {
        if (pendingNextTurn)
            pendingNextTurn = false;
    }

    public override void OnTurnEnd(CombatantModel self, CombatantModel opponent)
    {
        if (!pendingNextTurn)
            stack = 0;
    }
}
