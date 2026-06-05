public class ShieldOnHealthLossBuffModel : BuffModel
{
    public ShieldOnHealthLossBuffModel(int stack) : base(BuffEnum.ShieldOnHealthLoss, stack)
    {
    }

    public override void AfterTakeDamage(CombatantModel self, CombatantModel attacker, CombatDamageResult result)
    {
        if (result != null && result.HealthDamage > 0)
            self.GainShield(result.HealthDamage * stack);
    }
}
