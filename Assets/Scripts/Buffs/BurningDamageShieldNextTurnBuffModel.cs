public class BurningDamageShieldNextTurnBuffModel : BuffModel
{
    public BurningDamageShieldNextTurnBuffModel(int stack) : base(BuffEnum.BurningDamageShieldNextTurn, stack)
    {
    }

    public override void AfterEnemyBurningDamage(CombatantModel self, EnemyModel enemy, int damage)
    {
        if (self?.Player != null && enemy != null && damage > 0)
            self.Player.AddBuff(BuffEnum.PreparedShield, damage * stack);
    }

    public override void OnTurnStart(CombatantModel self, CombatantModel opponent)
    {
        if (self.IsPlayer)
            stack = 0;
    }
}
