public class VulnerableBuffModel : BuffModel
{
    public VulnerableBuffModel(int stack) : base(BuffEnum.Vulnerable, stack)
    {
    }

    public override void AfterAttack(CombatantModel self, CombatantModel target, ref int attackResult)
    {
        attackResult += stack;
    }

    public override void OnTurnEnd(CombatantModel self, CombatantModel opponent)
    {
        HalveStack();
    }
}
