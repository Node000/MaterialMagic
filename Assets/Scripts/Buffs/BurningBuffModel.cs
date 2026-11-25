public class BurningBuffModel : BuffModel
{
    public BurningBuffModel(int stack) : base(BuffEnum.Burning, stack)
    {
    }

    public override void OnTurnStart(CombatantModel self, CombatantModel opponent)
    {
        if (self?.Player != null)
        {
            self.Player.TakeDirectDamage(stack);
            HalveStack();
        }
    }

    public override void OnEnemyTurnStart(CombatantModel self, CombatantModel opponent)
    {
        EnemyModel enemy = self?.Enemy;
        if (enemy == null || enemy.IsDead)
            return;

        int damage = enemy.TakeDirectDamage(stack);
        if (damage > 0)
            enemy.Battle?.PlayerState?.TriggerAfterEnemyBurningDamage(enemy, damage);
        HalveStack();
    }
}
