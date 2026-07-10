public class WeakNextTurnBuffModel : BuffModel
{
    public WeakNextTurnBuffModel(int stack) : base(BuffEnum.WeakNextTurn, stack)
    {
    }

    public override void OnTurnStart(CombatantModel self, CombatantModel opponent)
    {
        self.AddBuff(BuffEnum.Weak, stack, opponent);
        stack = 0;
    }
}
