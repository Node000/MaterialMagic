public class BurningBuffModel : BuffModel
{
    public BurningBuffModel(int stack) : base(BuffEnum.Burning, stack)
    {
    }

    public override void OnTurnStart(CombatantModel self, CombatantModel opponent)
    {
        self.TakeDirectDamage(stack);
        HalveStack();
    }
}
