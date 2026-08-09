public class ShieldOnHealthLossBuffModel : BuffModel
{
    public ShieldOnHealthLossBuffModel(int stack) : base(BuffEnum.ShieldOnHealthLoss, stack)
    {
    }

    public override void AfterTakeDamage(CombatantModel self, CombatantModel attacker, CombatDamageResult result)
    {
        if (result != null && result.HealthDamage > 0)
            self.GainShield(result.HealthDamage * stack / 2);
    }

    public override void OnTurnStart(CombatantModel self, CombatantModel opponent)
    {
        if (self?.Enemy == null || opponent?.Player == null || self.Shield <= 0)
            return;

        int shield = self.Shield;
        opponent.Player.AddBuff(BuffEnum.Slow, shield, self);
        self.ConsumeShield(shield);
    }
}
