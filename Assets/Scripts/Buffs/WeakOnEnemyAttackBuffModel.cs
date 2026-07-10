public class WeakOnEnemyAttackBuffModel : BuffModel
{
    public WeakOnEnemyAttackBuffModel(int stack) : base(BuffEnum.WeakOnEnemyAttack, stack)
    {
    }

    public override void AfterTakeDamage(CombatantModel self, CombatantModel attacker, CombatDamageResult result)
    {
        if (self?.Player != null && attacker?.Enemy != null && result != null && result.RawDamage > 0)
            attacker.Enemy.AddBuff(BuffEnum.WeakNextTurn, stack, self);
    }

    public override void OnTurnStart(CombatantModel self, CombatantModel opponent)
    {
        if (self.IsPlayer)
            stack = 0;
    }
}
