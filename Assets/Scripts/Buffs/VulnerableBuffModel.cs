public class VulnerableBuffModel : BuffModel
{
    public VulnerableBuffModel(int stack) : base(BuffEnum.Vulnerable, stack)
    {
    }

    public override void OnTakeDamage(CombatantModel self, CombatantModel attacker, ref int damage)
    {
        damage += stack;
    }

    public override void AfterTakeDamage(CombatantModel self, CombatantModel attacker, CombatDamageResult result)
    {
        if (result != null && result.FinalDamage > 0)
            ConsumeStack(1);
    }
}
