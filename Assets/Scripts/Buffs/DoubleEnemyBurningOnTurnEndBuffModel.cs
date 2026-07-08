public class DoubleEnemyBurningOnTurnEndBuffModel : BuffModel
{
    public DoubleEnemyBurningOnTurnEndBuffModel(int stack) : base(BuffEnum.DoubleEnemyBurningOnTurnEnd, stack)
    {
    }

    public override void OnPlayerTurnEnd(CombatantModel self, CombatantModel opponent)
    {
        EnemyModel enemy = self != null && self.IsEnemy ? self.Enemy : null;
        if (enemy != null && !enemy.IsDead)
        {
            int burning = enemy.GetBuffStack(BuffEnum.Burning);
            if (burning > 0)
                enemy.AddBuff(BuffEnum.Burning, burning, null);
        }

        stack = 0;
    }
}
