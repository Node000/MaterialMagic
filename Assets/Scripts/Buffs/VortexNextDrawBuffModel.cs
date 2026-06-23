public class VortexNextDrawBuffModel : BuffModel
{
    private bool pendingNextTurn = true;

    public VortexNextDrawBuffModel(int stack) : base(BuffEnum.VortexNextDraw, stack)
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

        VortexModifier modifier = new VortexModifier();
        modifier.MarkRemoveAfterBattle();
        card.AddModifier(modifier);
        ConsumeStack(1);
    }

    public override void AfterTurnStart(CombatantModel self, CombatantModel opponent)
    {
        if (!pendingNextTurn)
            stack = 0;
    }
}
