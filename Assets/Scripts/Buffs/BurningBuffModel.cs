public class BurningBuffModel : BuffModel
{
    public BurningBuffModel(int stack) : base(BuffEnum.Burning, stack)
    {
    }

    public override void OnTurnStart(CombatantModel self, CombatantModel opponent)
    {
        int damage = self.IsEnemy ? self.Enemy.TakeDirectDamage(stack) : self.Player.TakeDirectDamage(stack);
        if (damage > 0 && self.IsEnemy)
            BattleManager.Instance?.PlayerState?.TriggerAfterEnemyBurningDamage(self.Enemy, damage);
        HalveStack();
    }
}
