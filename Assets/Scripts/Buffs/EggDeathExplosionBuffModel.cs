public class EggDeathExplosionBuffModel : BuffModel
{
    public EggDeathExplosionBuffModel(int stack) : base(BuffEnum.EggDeathExplosion, stack)
    {
    }

    public override void OnDie(CombatantModel self, CombatantModel opponent)
    {
        if (stack <= 0)
            return;

        BattleManager manager = BattleManager.Instance;
        if (manager == null)
            return;

        CombatantModel attacker = self;
        PlayerState player = manager.PlayerState;
        if (player != null && player.CurrentHealth > 0)
            player.TakeDamage(stack, attacker);

        var enemies = manager.Enemies;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyModel enemy = enemies[i];
            if (enemy == null)
                continue;
            if (enemy.IsDead && !ReferenceEquals(enemy, self.Enemy))
                continue;

            enemy.TakeDamage(stack, attacker);
        }
    }
}
