public class ShieldOnNextDrawBuffModel : BuffModel
{
    public ShieldOnNextDrawBuffModel(int stack) : base(BuffEnum.ShieldOnNextDraw, stack)
    {
    }

    public override void AfterTurnStartDraw(CombatantModel self, CombatantModel opponent, int drawCount)
    {
        if (self?.Player != null && drawCount > 0)
            self.Player.GainShield(drawCount * stack);

        stack = 0;
    }
}
