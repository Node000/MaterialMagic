public class BurningOnEnemyAttackBuffModel : BuffModel
{
    public BurningOnEnemyAttackBuffModel(int stack) : base(BuffEnum.BurningOnEnemyAttack, stack)
    {
    }

    public override void AfterTakeDamage(CombatantModel self, CombatantModel attacker, CombatDamageResult result)
    {
        if (self.IsPlayer && attacker != null && attacker.IsEnemy && result != null && result.RawDamage > 0)
            attacker.Enemy.AddBuff(BuffEnum.Burning, stack);
    }

    public override void OnTurnStart(CombatantModel self, CombatantModel opponent)
    {
        if (self.IsPlayer)
            stack = 0;
    }
}
