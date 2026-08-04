public class ThornsBuffModel : BuffModel
{
    public ThornsBuffModel(int stack) : base(BuffEnum.Thorns, stack)
    {
    }

    public override void AfterTakeDamage(CombatantModel self, CombatantModel attacker, CombatDamageResult result)
    {
        if (stack <= 0 || self == null || !self.IsPlayer || attacker == null || !attacker.IsEnemy || attacker.IsDead || result == null || result.FinalDamage <= 0)
            return;

        attacker.Enemy.TakeDamageResult(stack, self);
    }

    public override void OnTurnStart(CombatantModel self, CombatantModel opponent)
    {
        if (self != null && self.IsPlayer)
            stack = 0;
    }
}
