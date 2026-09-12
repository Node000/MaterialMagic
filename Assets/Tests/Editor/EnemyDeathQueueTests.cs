using NUnit.Framework;

/// <summary>
/// 死亡队列：敌人血量归零（IsDead）即时生效，但亡语结算与死亡演出推迟到死亡管线排空时，
/// 这样“同时死亡”的多个敌人也能按确定顺序逐项结算，避免同帧竞态。
/// 收尾清场（击败 Boss 后清爪牙）属于“纯表现死亡”：入队但不结算亡语。
/// </summary>
public class EnemyDeathQueueTests
{
    private sealed class ProbeEnemyModel : EnemyModel
    {
        public int DeathEffectCount;

        public ProbeEnemyModel(EnemyData data) : base(data) { }

        protected override void HandleDeathEffect(CombatantModel opponent)
        {
            DeathEffectCount++;
        }
    }

    private static EnemyData CreateData(int numericId, int maxHealth = 10)
    {
        return new EnemyData
        {
            numericId = numericId,
            string_id = "test_enemy_" + numericId,
            maxHealth = maxHealth
        };
    }

    [Test]
    public void DeathIsQueuedInsteadOfResolvedImmediately()
    {
        PlayerState player = new PlayerState(50, 0);
        BattleManager manager = BattleManager.Create(player);
        try
        {
            ProbeEnemyModel enemy = new ProbeEnemyModel(CreateData(901));
            manager.SpawnEnemy(enemy);

            enemy.Kill();

            Assert.That(enemy.IsDead, Is.True, "血量归零应即时生效");
            Assert.That(enemy.DeathHandled, Is.False, "亡语与死亡演出应推迟到管线排空");
            Assert.That(enemy.DeathEffectCount, Is.Zero);
            Assert.That(manager.HasPendingDeaths, Is.True);
        }
        finally
        {
            BattleManager.ClearInstance(manager);
        }
    }

    [Test]
    public void DeathsResolveInEnqueueOrderAndOnlyOnce()
    {
        PlayerState player = new PlayerState(50, 0);
        BattleManager manager = BattleManager.Create(player);
        try
        {
            ProbeEnemyModel first = new ProbeEnemyModel(CreateData(902));
            ProbeEnemyModel second = new ProbeEnemyModel(CreateData(903));
            manager.SpawnEnemy(first);
            manager.SpawnEnemy(second);

            first.Kill();
            second.Kill();
            Assert.That(manager.EnqueueEnemyDeath(first, null, true), Is.False, "同一敌人只应入队一次");

            Assert.That(manager.TryDequeueEnemyDeath(out EnemyDeathEntry entryA), Is.True);
            Assert.That(entryA.Enemy, Is.SameAs(first));
            Assert.That(entryA.ResolveOnDie, Is.True);
            manager.ResolveEnemyDeath(entryA);

            Assert.That(manager.TryDequeueEnemyDeath(out EnemyDeathEntry entryB), Is.True);
            Assert.That(entryB.Enemy, Is.SameAs(second));
            manager.ResolveEnemyDeath(entryB);

            Assert.That(first.DeathEffectCount, Is.EqualTo(1));
            Assert.That(second.DeathEffectCount, Is.EqualTo(1));
            Assert.That(first.DeathHandled, Is.True);
            Assert.That(second.DeathHandled, Is.True);
            Assert.That(manager.HasPendingDeaths, Is.False);
            Assert.That(manager.TryDequeueEnemyDeath(out _), Is.False);
        }
        finally
        {
            BattleManager.ClearInstance(manager);
        }
    }

    [Test]
    public void CleanupDeathIsQueuedButSkipsDeathEffect()
    {
        PlayerState player = new PlayerState(50, 0);
        BattleManager manager = BattleManager.Create(player);
        try
        {
            ProbeEnemyModel minion = new ProbeEnemyModel(CreateData(904));
            manager.SpawnEnemy(minion);
            minion.SetMinion(true);

            Assert.That(manager.KillAliveMinionsForVictory(), Is.True);
            Assert.That(minion.IsDead, Is.True);
            Assert.That(manager.HasPendingDeaths, Is.True);

            Assert.That(manager.TryDequeueEnemyDeath(out EnemyDeathEntry entry), Is.True);
            Assert.That(entry.Enemy, Is.SameAs(minion));
            Assert.That(entry.ResolveOnDie, Is.False, "收尾清场不应结算亡语");

            manager.ResolveEnemyDeath(entry);

            Assert.That(minion.DeathEffectCount, Is.Zero);
            Assert.That(minion.DeathHandled, Is.True, "清场死亡也要标记已处理，避免重复播碎裂");
        }
        finally
        {
            BattleManager.ClearInstance(manager);
        }
    }

    [Test]
    public void UnhandledDeadEnemiesFallBackToVisualOnlyQueue()
    {
        PlayerState player = new PlayerState(50, 0);
        BattleManager manager = BattleManager.Create(player);
        try
        {
            ProbeEnemyModel enemy = new ProbeEnemyModel(CreateData(905));
            manager.SpawnEnemy(enemy);

            // 模拟“读档恢复出 0 血敌人”这类没有走过死亡入口的情况。
            enemy.RestoreBattleState(new EnemyBattleSaveData { currentHealth = 0 });
            Assert.That(enemy.IsDead, Is.True);
            Assert.That(enemy.DeathHandled, Is.False);

            Assert.That(manager.EnqueueUnhandledDeadEnemies(), Is.EqualTo(1));

            Assert.That(manager.TryDequeueEnemyDeath(out EnemyDeathEntry entry), Is.True);
            Assert.That(entry.ResolveOnDie, Is.False);
            manager.ResolveEnemyDeath(entry);
            Assert.That(enemy.DeathEffectCount, Is.Zero, "恢复出的死亡不应补结算亡语");
            Assert.That(enemy.DeathHandled, Is.True);
        }
        finally
        {
            BattleManager.ClearInstance(manager);
        }
    }

    [Test]
    public void ClearPendingDeathsDropsQueuedEntries()
    {
        PlayerState player = new PlayerState(50, 0);
        BattleManager manager = BattleManager.Create(player);
        try
        {
            ProbeEnemyModel enemy = new ProbeEnemyModel(CreateData(906));
            manager.SpawnEnemy(enemy);
            enemy.Kill();
            Assert.That(manager.HasPendingDeaths, Is.True);

            manager.ClearPendingDeaths();

            Assert.That(manager.HasPendingDeaths, Is.False);
            Assert.That(manager.TryDequeueEnemyDeath(out _), Is.False);
        }
        finally
        {
            BattleManager.ClearInstance(manager);
        }
    }
}
