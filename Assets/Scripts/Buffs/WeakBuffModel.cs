public class WeakBuffModel : BuffModel
{
    public WeakBuffModel(int stack) : base(BuffEnum.Weak, stack)
    {
    }

    public override void OnAttack(CombatantModel self, CombatantModel target, ref int attackValue)
    {
        attackValue -= stack;
        if (attackValue < 0)
            attackValue = 0;
    }

    public override void AfterAttack(CombatantModel self, CombatantModel target, ref int attackResult)
    {
        int consumed = stack - stack / 2;
        if (consumed > 0)
            self.ConsumeBuff(BuffEnum.Weak, consumed);
    }

    public override void OnTurnEnd(CombatantModel self, CombatantModel opponent)
    {
        HalveStack();
    }
}
