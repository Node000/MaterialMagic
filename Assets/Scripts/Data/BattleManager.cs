using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public enum BattlePhase
{
    None = 0,
    PlayerTurn = 1,
    EnemyTurn = 2,
    Finished = 3
}

public class BattleActionResult
{
    public int PlayerHealthBefore;
    public int PlayerHealthAfter;
    public int PlayerShieldBefore;
    public int PlayerShieldAfter;
    public int EnemyShieldBefore;
    public int EnemyShieldAfter;
    public bool PlayerDefeated;
    public bool AllEnemiesDead;
    public bool EnemyBuffChanged;
    public bool PlayerBuffChanged;
    public bool EnemySpawned;
    public bool EnemyRemoved;

    public void CapturePlayerBefore(PlayerState playerState)
    {
        if (playerState == null)
            return;

        PlayerHealthBefore = playerState.CurrentHealth;
        PlayerShieldBefore = playerState.Shield;
    }

    public void CapturePlayerAfter(PlayerState playerState)
    {
        if (playerState == null)
            return;

        PlayerHealthAfter = playerState.CurrentHealth;
        PlayerShieldAfter = playerState.Shield;
        PlayerDefeated = playerState.CurrentHealth <= 0;
    }

    public void CaptureEnemyShieldBefore(EnemyModel enemy)
    {
        EnemyShieldBefore = enemy != null ? enemy.Shield : 0;
    }

    public void CaptureEnemyShieldAfter(EnemyModel enemy)
    {
        EnemyShieldAfter = enemy != null ? enemy.Shield : 0;
    }
}

public class BattleManager
{
    private const string EnemySummonLayoutConfigPath = "Config/EnemySummonLayoutConfig";

    private readonly List<EnemyModel> enemies = new List<EnemyModel>();
    private static EnemySummonLayoutConfig summonLayoutConfig;

    public static BattleManager Instance { get; private set; }

    public event Action<EnemyModel> EnemyAdded;

    public PlayerState PlayerState { get; private set; }
    public PlayerModel Player { get; private set; }
    public IReadOnlyList<EnemyModel> Enemies => enemies;
    public EnemyModel FocusTarget { get; private set; }
    public EnemyModel CurrentCastTarget { get; private set; }
    public int ContinuousCastCount { get; private set; }
    public BattlePhase CurrentPhase { get; private set; }

    public BattleManager(PlayerState playerState)
    {
        PlayerState = playerState;
        Player = playerState is PlayerStatus status ? new PlayerModel(status) : null;
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

    public EnemyModel SpawnMinion(int enemyId)
    {
        return SpawnMinion(enemyId, null, 0, 1);
    }

    public EnemyModel SpawnMinion(int enemyId, EnemyModel summoner, int summonIndex, int summonCount)
    {
        if (!GameDataDatabase.TryGetEnemyData(enemyId, out EnemyData data))
            return null;

        EnemyModel enemy = EnemyFactory.Create(data);
        if (enemy != null)
        {
            enemy.SetMinion(true);
            if (summoner != null && summoner.HasSpawnPosition)
            {
                Vector2 position = GetAvailableSummonPosition(summoner.SpawnPositionX, summoner.SpawnPositionY, summonIndex, summonCount);
                enemy.SetSpawnPosition(position.x, position.y);
            }
        }
        return SpawnEnemy(enemy);
    }

    public Vector2 GetAvailableSummonPosition(float centerX, float centerY, int summonIndex, int summonCount)
    {
        EnemySummonLayoutConfig config = LoadSummonLayoutConfig();
        int count = summonCount > 0 ? summonCount : 1;
        int index = summonIndex < 0 ? 0 : summonIndex;
        Vector2 fallback = GetSummonPosition(centerX, centerY, index, count, config);
        for (int attempt = 0; attempt < config.MaxSearchAttempts; attempt++)
        {
            int candidateIndex = index + attempt * count;
            Vector2 candidate = GetSummonPosition(centerX, centerY, candidateIndex, count, config);
            fallback = candidate;
            if (!IsSummonPositionOccupied(candidate, config.OccupiedRadius))
                return candidate;
        }

        return fallback;
    }

    public static Vector2 GetSummonPosition(float centerX, float centerY, int summonIndex, int summonCount)
    {
        return GetSummonPosition(centerX, centerY, summonIndex, summonCount, LoadSummonLayoutConfig());
    }

    private static Vector2 GetSummonPosition(float centerX, float centerY, int summonIndex, int summonCount, EnemySummonLayoutConfig config)
    {
        int count = summonCount > 0 ? summonCount : 1;
        int index = summonIndex < 0 ? 0 : summonIndex;
        int slotsPerRow = config.SameRowSlotCount;
        int row = index / slotsPerRow;
        int inRowIndex = index % slotsPerRow;
        int pairIndex = inRowIndex / 2 + 1;
        bool placeRight = count == 1 ? inRowIndex % 2 == 0 : inRowIndex % 2 == 1;
        float x = centerX + (placeRight ? config.HorizontalSpacing * pairIndex : -config.HorizontalSpacing * pairIndex);
        float y = centerY + GetSummonRowOffset(row, config.VerticalSpacing);
        return new Vector2(x, y);
    }

    private static float GetSummonRowOffset(int row, float verticalSpacing)
    {
        if (row <= 0 || verticalSpacing <= 0f)
            return 0f;

        int distance = (row + 1) / 2;
        return row % 2 == 1 ? verticalSpacing * distance : -verticalSpacing * distance;
    }

    private bool IsSummonPositionOccupied(Vector2 position, float occupiedRadius)
    {
        if (occupiedRadius <= 0f)
            return false;

        float occupiedRadiusSqr = occupiedRadius * occupiedRadius;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyModel enemy = enemies[i];
            if (enemy == null || enemy.IsDead || !enemy.HasSpawnPosition)
                continue;

            float deltaX = enemy.SpawnPositionX - position.x;
            float deltaY = enemy.SpawnPositionY - position.y;
            if (deltaX * deltaX + deltaY * deltaY < occupiedRadiusSqr)
                return true;
        }

        return false;
    }

    private static EnemySummonLayoutConfig LoadSummonLayoutConfig()
    {
        if (summonLayoutConfig == null)
            summonLayoutConfig = Resources.Load<EnemySummonLayoutConfig>(EnemySummonLayoutConfigPath);
        if (summonLayoutConfig == null)
            summonLayoutConfig = ScriptableObject.CreateInstance<EnemySummonLayoutConfig>();
        return summonLayoutConfig;
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
            if (CurrentPhase == BattlePhase.EnemyTurn)
            {
                enemy.SetCanActThisEnemyTurn(false);
                enemy.ClearCurrentIntents();
            }
            else
            {
                enemy.SetCanActThisEnemyTurn(true);
            }
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
        CurrentPhase = BattlePhase.None;
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

        EnemyModel target = SelectRandomEnemy();
        if (target == null)
            return 0;

        return target.TakeDamage(damage, source != null ? new CombatantModel(source) : null);
    }

    public void AddBurningToRandomEnemy(int stack)
    {
        if (stack <= 0)
            return;

        EnemyModel target = SelectRandomEnemy();
        if (target != null)
            target.AddBuff(BuffEnum.Burning, stack, PlayerState != null ? new CombatantModel(PlayerState) : null);
    }

    public bool AddArcToRandomEnemy(int stack)
    {
        if (stack <= 0)
            return false;

        EnemyModel target = SelectRandomEnemy();
        if (target == null)
            return false;

        target.AddBuff(BuffEnum.Arc, stack, PlayerState != null ? new CombatantModel(PlayerState) : null);
        return true;
    }

    public bool AddRandomDebuffToRandomEnemy(int stack)
    {
        if (stack <= 0)
            return false;

        EnemyModel target = SelectRandomEnemy();
        if (target == null)
            return false;

        int index = NextRandomInt(0, 3);
        target.AddBuff(index == 0 ? BuffEnum.Weak : index == 1 ? BuffEnum.Slow : BuffEnum.Vulnerable, stack, PlayerState != null ? new CombatantModel(PlayerState) : null);
        return true;
    }

    public void ResetContinuousCastCount()
    {
        ContinuousCastCount = 0;
    }

    public void RestoreBattleState(BattlePhase phase, int continuousCastCount)
    {
        CurrentPhase = phase;
        ContinuousCastCount = continuousCastCount < 0 ? 0 : continuousCastCount;
        FocusTarget = null;
        CurrentCastTarget = null;
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

        return SelectLeftmostEnemy();
    }

    private EnemyModel SelectLeftmostEnemy()
    {
        EnemyModel selected = null;
        float selectedX = 0f;
        int selectedIndex = 0;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyModel enemy = enemies[i];
            if (enemy == null || enemy.IsDead)
                continue;

            float enemyX = enemy.HasSpawnPosition ? enemy.SpawnPositionX : i;
            if (selected == null || enemyX < selectedX || (Mathf.Approximately(enemyX, selectedX) && i < selectedIndex))
            {
                selected = enemy;
                selectedX = enemyX;
                selectedIndex = i;
            }
        }

        return selected;
    }

    private EnemyModel SelectRandomEnemy()
    {
        int aliveCount = 0;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyModel enemy = enemies[i];
            if (enemy != null && !enemy.IsDead)
                aliveCount++;
        }

        if (aliveCount == 0)
            return null;

        int targetIndex = NextRandomInt(0, aliveCount);
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

        return null;
    }

    private int NextRandomInt(int minInclusive, int maxExclusive)
    {
        if (Player != null && Player.Status != null)
            return Player.Status.NextRunRandomInt(minInclusive, maxExclusive);
        return Random.Range(minInclusive, maxExclusive);
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

    public void BeginBattleRules()
    {
        TriggerMagicBattleStart();
    }

    public void FinishBattleRules(List<MaterialModel> removedTemporaryCards)
    {
        TriggerMagicBattleEnd();
        ResetContinuousCastCount();
        FinishBattle();
        BeginPlayerResolveRules(false);
        PlayerState?.EndTurn(removedTemporaryCards);
        EndPlayerResolveRules();
        PlayerState?.ClearCombatState();
    }

    public void BeginPlayerResolveRules(bool afterPlayerDecide = true)
    {
        MaterialModifierModel.CurrentContext = new MaterialModifierContext { PlayerState = PlayerState, BattleManager = this };
        if (afterPlayerDecide)
            PlayerState?.TriggerAfterPlayerDecide(new CombatantModel(GetFirstAliveEnemy()));
        PlayerState?.TriggerMaterialBegin();
    }

    public void EndPlayerResolveRules()
    {
        MaterialModifierModel.CurrentContext = null;
    }

    public BattleActionResult BeginPlayerTurnStartRules(int drawCount, Func<bool> tryApplyFixedTurnHand)
    {
        BattleActionResult result = new BattleActionResult();
        if (PlayerState == null)
            return result;

        GameLog.Data($"Begin player turn extra={PlayerState.GetBuffStack(BuffEnum.ExtraDraw)}");
        BeginPlayerTurn();
        PlayerState.ResetExtraRefreshChancesThisTurn();
        result.CapturePlayerBefore(PlayerState);
        CombatantModel opponent = new CombatantModel(GetFirstAliveEnemy());
        PlayerState.ClearShield();
        PlayerState.TriggerOnTurnStart(opponent);
        bool skipNormalDraw = tryApplyFixedTurnHand != null && tryApplyFixedTurnHand();
        DrawPlayerTurnCards(drawCount, skipNormalDraw);
        PlayerState.TriggerAfterTurnStart(opponent);
        TriggerMagicTurnStart();
        result.CapturePlayerAfter(PlayerState);
        return result;
    }

    private void DrawPlayerTurnCards(int drawCount, bool skipNormalDraw)
    {
        if (!skipNormalDraw)
        {
            int extraDraw = PlayerState.GetBuffStack(BuffEnum.ExtraDraw);
            int directionExtraDraw = PlayerState.GetBuffStack(BuffEnum.DirectionExtraDraw);
            int lazyDrawReduction = PlayerState.GetBuffStack(BuffEnum.LazyNextDraw);
            int finalDrawCount = drawCount + extraDraw + directionExtraDraw - lazyDrawReduction;
            if (finalDrawCount < 0)
                finalDrawCount = 0;
            PlayerState.DrawCards(finalDrawCount);
            if (extraDraw > 0)
                PlayerState.ConsumeBuff(BuffEnum.ExtraDraw, extraDraw);
            if (lazyDrawReduction > 0)
                PlayerState.ConsumeBuff(BuffEnum.LazyNextDraw, lazyDrawReduction);
            PlayerState.ConsumeTemporaryMaterialsNextTurn();
        }

    }

    public BattleActionResult EndPlayerTurnRules(List<MaterialModel> removedTemporaryCards)
    {
        BattleActionResult result = new BattleActionResult();
        if (PlayerState == null)
            return result;

        result.CapturePlayerBefore(PlayerState);
        PlayerState.TriggerOnTurnEnd(new CombatantModel(GetFirstAliveEnemy()));
        TriggerEnemyPlayerTurnEndBuffs();
        TriggerMagicTurnEnd();
        PlayerState.RemoveTurnOnlyModifiers();
        PlayerState.EndTurn(removedTemporaryCards);
        PlayerState.TriggerAfterTurnEnd(new CombatantModel(GetFirstAliveEnemy()));
        PlayerState.TriggerMaterialEnd();
        EndPlayerResolveRules();
        result.CapturePlayerAfter(PlayerState);
        result.AllEnemiesDead = AllEnemiesDead();
        return result;
    }

    private void TriggerEnemyPlayerTurnEndBuffs()
    {
        CombatantModel opponent = new CombatantModel(PlayerState);
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyModel enemy = enemies[i];
            if (enemy != null && !enemy.IsDead)
                enemy.TriggerOnPlayerTurnEnd(opponent);
        }
    }

    public BattleActionResult BeginEnemyAction(EnemyModel enemy)
    {
        BattleActionResult result = new BattleActionResult();
        if (enemy == null || PlayerState == null)
            return result;

        result.CapturePlayerBefore(PlayerState);
        result.CaptureEnemyShieldBefore(enemy);
        CombatantModel opponent = new CombatantModel(PlayerState);
        enemy.ClearShield();
        enemy.TriggerOnTurnStart(opponent);
        enemy.TriggerAfterTurnStart(opponent);
        enemy.BeginResolveIntents(PlayerState);
        result.CapturePlayerAfter(PlayerState);
        result.CaptureEnemyShieldAfter(enemy);
        return result;
    }

    public BattleActionResult ResolveEnemyIntentAt(EnemyModel enemy, int intentIndex)
    {
        BattleActionResult result = new BattleActionResult();
        if (enemy == null || PlayerState == null)
            return result;

        result.CapturePlayerBefore(PlayerState);
        result.CaptureEnemyShieldBefore(enemy);
        enemy.ResolveCurrentIntentAt(intentIndex, PlayerState);
        result.CapturePlayerAfter(PlayerState);
        result.CaptureEnemyShieldAfter(enemy);
        result.AllEnemiesDead = AllEnemiesDead();
        return result;
    }

    public BattleActionResult ResolveEnemyIntentHitAt(EnemyModel enemy, int intentIndex, int hitIndex)
    {
        BattleActionResult result = new BattleActionResult();
        if (enemy == null || PlayerState == null)
            return result;

        result.CapturePlayerBefore(PlayerState);
        result.CaptureEnemyShieldBefore(enemy);
        enemy.ResolveCurrentIntentHitAt(intentIndex, hitIndex, PlayerState);
        result.CapturePlayerAfter(PlayerState);
        result.CaptureEnemyShieldAfter(enemy);
        result.AllEnemiesDead = AllEnemiesDead();
        return result;
    }

    public BattleActionResult EndEnemyAction(EnemyModel enemy)
    {
        BattleActionResult result = new BattleActionResult();
        if (enemy == null || PlayerState == null)
            return result;

        result.CapturePlayerBefore(PlayerState);
        result.CaptureEnemyShieldBefore(enemy);
        CombatantModel opponent = new CombatantModel(PlayerState);
        enemy.EndResolveIntents(PlayerState);
        enemy.TriggerOnTurnEnd(opponent);
        enemy.TriggerAfterTurnEnd(opponent);
        result.CapturePlayerAfter(PlayerState);
        result.CaptureEnemyShieldAfter(enemy);
        result.AllEnemiesDead = AllEnemiesDead();
        return result;
    }

    private void TriggerMagicBattleStart()
    {
        if (PlayerState == null)
            return;

        for (int i = 0; i < PlayerState.MagicBook.Count; i++)
            PlayerState.MagicBook[i]?.TriggerMagicBattleStart(PlayerState, this);
    }

    private void TriggerMagicBattleEnd()
    {
        if (PlayerState == null)
            return;

        for (int i = 0; i < PlayerState.MagicBook.Count; i++)
            PlayerState.MagicBook[i]?.TriggerMagicBattleEnd(PlayerState, this);
    }

    private void TriggerMagicTurnStart()
    {
        if (PlayerState == null)
            return;

        for (int i = 0; i < PlayerState.MagicBook.Count; i++)
            PlayerState.MagicBook[i]?.TriggerMagicTurnStart(PlayerState, this);
    }

    private void TriggerMagicTurnEnd()
    {
        if (PlayerState == null)
            return;

        for (int i = 0; i < PlayerState.MagicBook.Count; i++)
            PlayerState.MagicBook[i]?.TriggerMagicTurnEnd(PlayerState, this);
    }

    public void BeginPlayerTurn()
    {
        CurrentPhase = BattlePhase.PlayerTurn;
        for (int i = 0; i < enemies.Count; i++)
            enemies[i]?.SetCanActThisEnemyTurn(true);
    }

    public void BeginEnemyTurn()
    {
        CurrentPhase = BattlePhase.EnemyTurn;
        for (int i = 0; i < enemies.Count; i++)
            enemies[i]?.SetCanActThisEnemyTurn(true);
    }

    public void EndEnemyTurn()
    {
        CurrentPhase = BattlePhase.PlayerTurn;
        for (int i = 0; i < enemies.Count; i++)
            enemies[i]?.SetCanActThisEnemyTurn(true);
    }

    public void FinishBattle()
    {
        CurrentPhase = BattlePhase.Finished;
    }

    public bool KillAliveMinionsForVictory()
    {
        bool killedAny = false;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyModel enemy = enemies[i];
            if (enemy != null && enemy.IsMinion && !enemy.IsDead)
            {
                enemy.KillAsBattleCleanup();
                killedAny = true;
            }
        }
        return killedAny;
    }

    public bool HasActingEnemyAfter(int index)
    {
        for (int i = index + 1; i < enemies.Count; i++)
        {
            EnemyModel enemy = enemies[i];
            if (enemy != null && !enemy.IsDead && enemy.CanActThisEnemyTurn)
                return true;
        }

        return false;
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

        bool hasVictoryTarget = false;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyModel enemy = enemies[i];
            if (enemy == null || enemy.IsMinion)
                continue;

            hasVictoryTarget = true;
            if (!enemy.IsDead)
                return false;
        }

        return hasVictoryTarget;
    }
}
