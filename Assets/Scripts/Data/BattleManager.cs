using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class BattleManager
{
    private readonly List<EnemyModel> enemies = new List<EnemyModel>();

    public static BattleManager Instance { get; private set; }

    public event Action<EnemyModel> EnemyAdded;

    public PlayerState PlayerState { get; private set; }
    public IReadOnlyList<EnemyModel> Enemies => enemies;
    public EnemyModel FocusTarget { get; private set; }
    public EnemyModel CurrentCastTarget { get; private set; }
    public int ContinuousCastCount { get; private set; }

    public BattleManager(PlayerState playerState)
    {
        PlayerState = playerState;
    }

    public static BattleManager Create(PlayerState playerState)
    {
        BattleManager manager = new BattleManager(playerState);
        Instance = manager;
        return manager;
    }

    public static void ClearInstance(BattleManager manager)
    {
        if (ReferenceEquals(Instance, manager))
            Instance = null;
    }

    public void SetEnemies(IEnumerable<EnemyModel> enemyModels)
    {
        enemies.Clear();
        FocusTarget = null;
        if (enemyModels == null)
            return;

        foreach (EnemyModel enemy in enemyModels)
        {
            if (enemy != null)
                enemies.Add(enemy);
        }
    }

    public EnemyModel SpawnEnemy(int enemyId)
    {
        return GameDataDatabase.TryGetEnemyData(enemyId, out EnemyData data) ? SpawnEnemy(data) : null;
    }

    public EnemyModel SpawnEnemy(LevelEnemyData placement)
    {
        if (placement == null)
            return null;

        return GameDataDatabase.TryGetEnemyData(placement.enemyId, out EnemyData data) ? SpawnEnemy(data, placement.x, placement.y) : null;
    }

    public EnemyModel SpawnEnemy(EnemyData data)
    {
        return data != null ? SpawnEnemy(EnemyFactory.Create(data)) : null;
    }

    public EnemyModel SpawnEnemy(EnemyData data, float x, float y)
    {
        if (data == null)
            return null;

        EnemyModel enemy = EnemyFactory.Create(data);
        if (enemy != null)
            enemy.SetSpawnPosition(x, y);
        return SpawnEnemy(enemy);
    }

    public EnemyModel SpawnEnemy(EnemyModel enemy)
    {
        if (enemy == null)
            return null;

        if (!enemies.Contains(enemy))
        {
            enemies.Add(enemy);
            EnemyAdded?.Invoke(enemy);
        }

        return enemy;
    }

    public void AddEnemy(EnemyModel enemy)
    {
        SpawnEnemy(enemy);
    }

    public void ClearEnemies()
    {
        enemies.Clear();
        FocusTarget = null;
        CurrentCastTarget = null;
        ContinuousCastCount = 0;
    }

    public void SetFocusTarget(EnemyModel enemy)
    {
        FocusTarget = enemy != null && enemies.Contains(enemy) && !enemy.IsDead ? enemy : null;
    }

    public void ClearFocusTarget()
    {
        FocusTarget = null;
    }

    public EnemyModel BeginCastTarget()
    {
        if (CurrentCastTarget != null && !CurrentCastTarget.IsDead)
            return CurrentCastTarget;

        CurrentCastTarget = SelectTargetEnemy();
        return CurrentCastTarget;
    }

    public int RegisterMagicCast()
    {
        ContinuousCastCount++;
        return ContinuousCastCount;
    }

    public int DamageRandomEnemy(int damage, PlayerState source)
    {
        if (damage <= 0)
            return 0;

        EnemyModel target = SelectTargetEnemy();
        if (target == null)
            return 0;

        return target.TakeDamage(damage, source != null ? new CombatantModel(source) : null);
    }

    public void AddBurningToRandomEnemy(int stack)
    {
        if (stack <= 0)
            return;

        EnemyModel target = SelectTargetEnemy();
        if (target != null)
            target.AddBuff(BuffEnum.Burning, stack);
    }

    public void AddRandomDebuffToRandomEnemy(int stack)
    {
        if (stack <= 0)
            return;

        EnemyModel target = SelectTargetEnemy();
        if (target == null)
            return;

        int index = Random.Range(0, 3);
        target.AddBuff(index == 0 ? BuffEnum.Weak : index == 1 ? BuffEnum.Slow : BuffEnum.Vulnerable, stack);
    }

    public void ResetContinuousCastCount()
    {
        ContinuousCastCount = 0;
    }

    public void EndCastTarget()
    {
        CurrentCastTarget = null;
    }

    public EnemyModel GetTargetEnemy()
    {
        if (CurrentCastTarget != null && !CurrentCastTarget.IsDead)
            return CurrentCastTarget;

        return SelectTargetEnemy();
    }

    private EnemyModel SelectTargetEnemy()
    {
        if (FocusTarget != null && !FocusTarget.IsDead)
            return FocusTarget;

        int aliveCount = 0;
        EnemyModel onlyAlive = null;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyModel enemy = enemies[i];
            if (enemy == null || enemy.IsDead)
                continue;

            aliveCount++;
            onlyAlive = enemy;
        }

        if (aliveCount <= 1)
            return onlyAlive;

        int targetIndex = Random.Range(0, aliveCount);
        int aliveIndex = 0;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyModel enemy = enemies[i];
            if (enemy == null || enemy.IsDead)
                continue;

            if (aliveIndex == targetIndex)
                return enemy;

            aliveIndex++;
        }

        CurrentCastTarget = null;
        return null;
    }

    public void CollectAliveEnemyCombatants(List<CombatantModel> results)
    {
        if (results == null)
            return;

        results.Clear();
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyModel enemy = enemies[i];
            if (enemy != null && !enemy.IsDead)
                results.Add(new CombatantModel(enemy));
        }
    }

    public EnemyModel GetFirstAliveEnemy()
    {
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyModel enemy = enemies[i];
            if (enemy != null && !enemy.IsDead)
                return enemy;
        }

        return null;
    }

    public bool HasAliveEnemyAfter(int index)
    {
        for (int i = index + 1; i < enemies.Count; i++)
        {
            EnemyModel enemy = enemies[i];
            if (enemy != null && !enemy.IsDead)
                return true;
        }

        return false;
    }

    public bool AllEnemiesDead()
    {
        if (enemies.Count == 0)
            return false;

        for (int i = 0; i < enemies.Count; i++)
        {
            if (enemies[i] != null && !enemies[i].IsDead)
                return false;
        }

        return true;
    }
}
