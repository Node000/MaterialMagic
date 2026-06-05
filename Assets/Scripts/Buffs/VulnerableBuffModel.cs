public class VulnerableBuffModel : BuffModel
{
    public VulnerableBuffModel(int stack) : base(BuffEnum.Vulnerable, stack)
    {
    }

    public override void OnTakeDamage(CombatantModel self, CombatantModel attacker, ref int damage)
    {
        damage += stack;
    }

    public override void OnTurnEnd(CombatantModel self, CombatantModel opponent)
    {
        HalveStack();
    }
}
