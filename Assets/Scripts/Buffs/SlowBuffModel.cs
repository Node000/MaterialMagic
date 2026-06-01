public class SlowBuffModel : BuffModel
{
    public SlowBuffModel(int stack) : base(BuffEnum.Slow, stack)
    {
    }

    public override void OnGainShield(CombatantModel self, ref int shieldValue)
    {
        shieldValue -= stack;
        if (shieldValue < 0)
            shieldValue = 0;
    }

    public override void OnTurnEnd(CombatantModel self, CombatantModel opponent)
    {
        HalveStack();
    }
}
