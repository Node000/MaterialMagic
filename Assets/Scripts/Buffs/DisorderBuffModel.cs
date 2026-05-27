public class DisorderBuffModel : BuffModel
{
    public DisorderBuffModel(int stack) : base(BuffEnum.Disorder, stack)
    {
    }

    public override void OnAttack(CombatantModel self, CombatantModel target, ref int attackValue)
    {
        attackValue += stack;
    }

    public override void OnTurnEnd(CombatantModel self, CombatantModel opponent)
    {
        if (self.IsEnemy && !self.IsDead)
        {
            self.TakeDirectDamage(stack);
            AddStack(1);
        }
    }
}
