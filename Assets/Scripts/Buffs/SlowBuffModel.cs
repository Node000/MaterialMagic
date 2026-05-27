public class SlowBuffModel : BuffModel
{
    public SlowBuffModel(int stack) : base(BuffEnum.Slow, stack)
    {
    }

    public override void AfterGetAction(CombatantModel self, CombatantModel opponent)
    {
        self.ConsumeShield(stack);
    }

    public override void OnTurnEnd(CombatantModel self, CombatantModel opponent)
    {
        HalveStack();
    }
}
