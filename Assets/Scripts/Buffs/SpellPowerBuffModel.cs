public class SpellPowerBuffModel : BuffModel
{
    public SpellPowerBuffModel(int stack) : base(BuffEnum.SpellPower, stack)
    {
    }

    public override void OnAttack(CombatantModel self, CombatantModel target, ref int attackValue)
    {
        attackValue += stack;
    }

    public override void OnTurnEnd(CombatantModel self, CombatantModel opponent)
    {
        stack = 0;
    }
}
