public class ChargeNextDrawBuffModel : BuffModel
{
    private bool pendingNextTurn = true;

    public ChargeNextDrawBuffModel(int stack) : base(BuffEnum.ChargeNextDraw, stack)
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

        if (!card.HasModifier<ChargeModifier>())
        {
            ChargeModifier modifier = new ChargeModifier();
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
