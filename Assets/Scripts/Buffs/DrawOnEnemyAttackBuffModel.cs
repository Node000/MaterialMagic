public class DrawOnEnemyAttackBuffModel : BuffModel
{
    public DrawOnEnemyAttackBuffModel(int stack) : base(BuffEnum.DrawOnEnemyAttack, stack)
    {
    }

    public override void AfterTakeDamage(CombatantModel self, CombatantModel attacker, CombatDamageResult result)
    {
        if (self?.Player != null && attacker != null && attacker.IsEnemy && result != null && result.RawDamage > 0)
            self.Player.AddBuff(BuffEnum.ExtraDraw, stack);
    }

    public override void OnTurnStart(CombatantModel self, CombatantModel opponent)
    {
        if (self.IsPlayer)
            stack = 0;
    }
}
