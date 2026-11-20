using System;
using System.Collections.Generic;

/// <summary>
/// Explicit content/adaptation boundary. Defaults use the same production factories and extracted
/// reward/event/shop rules. Replace lookups/filters for a deterministic in-memory content set;
/// do not replace resolution algorithms. ConfigureRun supplies the real generated map, not a copy
/// of HandSystemUI's still-Unity-owned designed-map generator.
/// </summary>
public sealed class GameSessionContent
{
    public string Version = string.Empty;
    public EconomyConfigData Economy;
    public int MagicSlotCapacity = 12;
    public LevelData AutomaticShopLevel;
    public Func<int, LevelData> FindLevel = id => GameDataDatabase.TryGetLevelData(id, out LevelData data) ? data : null;
    public Func<int, EnemyData> FindEnemy = id => GameDataDatabase.TryGetEnemyData(id, out EnemyData data) ? data : null;
    public Func<LevelData, EventData> FindEvent = level => level != null && GameDataDatabase.TryGetEventData(level.eventPoolId, out EventData data) ? data : null;
    public Func<LevelData, BonusLevelData> FindBonus = level =>
    {
        if (level == null) return null;
        int id = level.bonusLevelId > 0 ? level.bonusLevelId : level.numericId;
        return GameDataDatabase.TryGetBonusLevelData(id, out BonusLevelData data) ? data : null;
    };
    public Func<LevelData, IReadOnlyList<ShopLayer>> ShopLayers = level => null;
    public Func<PlayerStatus, ShopGenerationServices> ShopServices = player => new ShopGenerationServices { NextRandomInt = player.NextRunRandomInt };
    /// <summary>Null uses node-aware production reward pools and this content's slot capacity.</summary>
    public Func<PlayerStatus, EventRuleServices> EventServices;
    public Func<PlayerStatus, RewardGenerationServices> RewardServices = player => new RewardGenerationServices { NextRandomInt = player.NextRunRandomInt };
    public Func<LevelData, bool> IsTutorialBattle = level => false;
    /// <summary>Trusted map/chapter policy, not supplied by an AI command payload.</summary>
    public Func<RunManager, LevelData, RunNodePolicy> NodePolicy = DefaultNodePolicy;

    internal RunNodePolicy GetDestinationPolicy(RunManager run, LevelData level, RunMapCellModel cell)
        => NodePolicy == DefaultNodePolicy ? DefaultNodePolicyForCell(run, level, cell) : NodePolicy(run, level);

    private static RunNodePolicy DefaultNodePolicy(RunManager run, LevelData level)
        => DefaultNodePolicyForCell(run, level, run.MapGrid?.GetCurrentCell());

    private static RunNodePolicy DefaultNodePolicyForCell(RunManager run, LevelData level, RunMapCellModel cell)
    {
        bool hasGrid = run.MapGrid != null && run.MapGrid.CellCount > 0;
        bool boss = hasGrid ? cell != null && (cell.isBoss || run.MapGrid.bossMapActive)
            : ReferenceEquals(level, run.ChapterBossLevel);
        return new RunNodePolicy
        {
            IsBoss = boss,
            CompleteRunOnBattleWin = boss && (hasGrid ? cell.isEnd || !run.MapGrid.HasFixedBossPath : run.CurrentMapNodeIndex >= run.MapNodes.Count - 1)
        };
    }
    public Func<int, int> ModifyBonusGold;

    internal BattleManager CreateBattle(GameSession session, LevelData level)
    {
        var battle = new BattleManager(session.Player);
        battle.ConfigureLevelContext(level, session.Run.ActiveChapter?.numericId ?? 0, session.Run.Session.IsBossNode);
        LevelEnemyGroupData group = RunLevelRules.SelectEnemyGroup(session.Run, level);
        if (level.randomEnemyGroups != null && level.randomEnemyGroups.Length > 0)
        {
            for (int i = 0; group?.enemies != null && i < group.enemies.Length; i++) Spawn(battle, group.enemies[i]);
        }
        else if (level.enemies != null && level.enemies.Length > 0)
        {
            for (int i = 0; i < level.enemies.Length; i++) Spawn(battle, level.enemies[i]);
        }
        else
        {
            for (int i = 0; level.enemyIds != null && i < level.enemyIds.Length; i++)
            {
                EnemyData data = FindEnemy(level.enemyIds[i]);
                if (data != null) battle.SpawnEnemy(data);
            }
        }
        return battle;
    }
    private void Spawn(BattleManager battle, LevelEnemyData placement)
    {
        if (placement == null) return;
        EnemyData data = FindEnemy(placement.enemyId);
        if (data != null) battle.SpawnEnemy(data, placement.x, placement.y);
    }
}

public sealed class GameSessionOptions
{
    public int Seed;
    public PlayerStateServices Services = PlayerStateServices.Simulation;
    public RunDifficultySaveData Difficulty;
    public IRuleContentProvider RuleContent;
    public RuleProfilePolicy Profile;
    public int MaxHealth = 50;
    public int StartingGold;
    /// <summary>Receives a seeded player with explicit services. Add configured starting content only.</summary>
    public Action<PlayerStatus> InitializePlayer;
    public Action<RunManager> ConfigureRun;
    public GameSessionContent Content;
    /// <summary>Optional scoped adapter factory; invoked after checkpoint catalog/profile replacement.</summary>
    public Func<PlayerStatus, GameSessionContent> CreateContent;
    public TutorialPolicy Tutorial;
}
