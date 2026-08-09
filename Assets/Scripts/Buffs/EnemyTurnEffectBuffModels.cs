public class RefreshLimitNextTurnBuffModel : BuffModel
{
    public RefreshLimitNextTurnBuffModel(int stack) : base(BuffEnum.RefreshLimitNextTurn, stack)
    {
    }

    public override void OnTurnStart(CombatantModel self, CombatantModel opponent)
    {
        if (self?.Player == null || stack <= 0)
            return;

        self.Player.SetRefreshLimitReductionThisTurn(stack);
        stack = 0;
    }
}

public class RandomNextDrawBuffModel : BuffModel
{
    private bool pendingNextTurn = true;

    public RandomNextDrawBuffModel(int stack) : base(BuffEnum.RandomNextDraw, stack)
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

        if (!card.HasModifier<RandomArrowModifier>())
        {
            RandomArrowModifier modifier = new RandomArrowModifier();
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

public class TemporaryProliferatingNextDrawBuffModel : BuffModel
{
    private bool pendingNextTurn = true;

    public TemporaryProliferatingNextDrawBuffModel(int stack) : base(BuffEnum.TemporaryProliferatingNextDraw, stack)
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

        if (!card.HasModifier<TemporaryModifier>())
            card.AddModifier(new TemporaryModifier());
        if (!card.HasModifier<ProliferatingArrowModifier>())
        {
            ProliferatingArrowModifier modifier = new ProliferatingArrowModifier();
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

public class HandLimitNextTurnBuffModel : BuffModel
{
    public HandLimitNextTurnBuffModel(int stack) : base(BuffEnum.HandLimitNextTurn, stack)
    {
    }

    public override void OnTurnStart(CombatantModel self, CombatantModel opponent)
    {
        if (self?.Player == null || stack <= 0)
            return;

        self.Player.SetHandLimitThisTurn(stack);
        stack = 0;
    }
}