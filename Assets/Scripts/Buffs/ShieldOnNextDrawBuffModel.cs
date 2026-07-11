public class ShieldOnNextDrawBuffModel : BuffModel
{
    private bool pendingNextTurn = true;

    public ShieldOnNextDrawBuffModel(int stack) : base(BuffEnum.ShieldOnNextDraw, stack)
    {
    }

    public override void OnTurnStart(CombatantModel self, CombatantModel opponent)
    {
        if (pendingNextTurn)
            pendingNextTurn = false;
    }

    public override void AfterDraw(CombatantModel self, MaterialModel card)
    {
        if (pendingNextTurn || self?.Player == null || card == null)
            return;

        self.Player.GainShield(stack);
    }

    public override void AfterTurnStart(CombatantModel self, CombatantModel opponent)
    {
        if (!pendingNextTurn)
            stack = 0;
    }
}
