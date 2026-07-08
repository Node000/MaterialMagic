public class BurningNextTurnBuffModel : BuffModel
{
    public BurningNextTurnBuffModel(int stack) : base(BuffEnum.BurningNextTurn, stack)
    {
    }

    public override void OnTurnStart(CombatantModel self, CombatantModel opponent)
    {
        if (opponent != null)
            opponent.AddBuff(BuffEnum.Burning, stack, self);
        stack = 0;
    }
}
