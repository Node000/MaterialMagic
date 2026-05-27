public class BurningBuffModel : BuffModel
{
    public BurningBuffModel(int stack) : base(BuffEnum.Burning, stack)
    {
    }

    public override void OnTurnEnd(CombatantModel self, CombatantModel opponent)
    {
        self.TakeDamage(stack);
        HalveStack();
    }
}
