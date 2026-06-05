using System.Collections.Generic;

public enum RunFlowState
{
    None = 0,
    MapSelection = 1,
    BeforeNode = 2,
    Battle = 3,
    Reward = 4,
    Event = 5,
    Rest = 6,
    Shop = 7,
    Victory = 8,
    Defeat = 9
}

public class RunManager
{
    private readonly List<int> remainingBeginPool = new List<int>();
    private readonly List<int> remainingMidPool = new List<int>();
    private readonly List<int> remainingNormalPool = new List<int>();
    private readonly List<int> remainingEventPool = new List<int>();
    private readonly List<int> remainingElitePool = new List<int>();
    private readonly List<int> combinedCandidateIndexes = new List<int>();
    private readonly List<int> combinedCandidatePools = new List<int>();
    private List<RunMapNodeModel> mapNodes;

    public static RunManager Current { get; private set; }

    public PlayerStatus PlayerStatus { get; }
    public IReadOnlyList<RunMapNodeModel> MapNodes => mapNodes;
    public int CurrentMapNodeIndex { get; private set; }
    public int BattleCount { get; private set; }
    public ChapterData ActiveChapter { get; private set; }
    public LevelData CurrentLevel { get; private set; }
    public RunFlowState State { get; private set; }
    public BattleManager CurrentBattle { get; private set; }

    public RunManager(PlayerStatus playerStatus)
    {
        PlayerStatus = playerStatus;
        mapNodes = new List<RunMapNodeModel>();
        State = RunFlowState.MapSelection;
    }

    public static RunManager Create(PlayerStatus playerStatus)
    {
        RunManager manager = new RunManager(playerStatus);
        Current = manager;
        return manager;
    }

    public static void ClearCurrent(RunManager manager)
    {
        if (ReferenceEquals(Current, manager))
            Current = null;
    }

    public void AttachMapNodes(List<RunMapNodeModel> nodes)
    {
        mapNodes = nodes ?? new List<RunMapNodeModel>();
    }

    public void SetActiveChapter(ChapterData chapter)
    {
        if (ReferenceEquals(ActiveChapter, chapter))
            return;

        ActiveChapter = chapter;
        ResetChapterBattlePools();
    }

    public void SetCurrentMapNodeIndex(int index)
    {
        CurrentMapNodeIndex = index < 0 ? 0 : index;
    }

    public void SelectCurrentNodeLevel(LevelData level)
    {
        CurrentLevel = level;
        State = level != null ? RunFlowState.BeforeNode : RunFlowState.MapSelection;
        if (mapNodes != null && CurrentMapNodeIndex >= 0 && CurrentMapNodeIndex < mapNodes.Count)
            mapNodes[CurrentMapNodeIndex].selectedLevel = level;
    }

    public void BeginLevel(LevelData level)
    {
        CurrentLevel = level;
        State = GetStateForLevel(level);
    }

    public void SetBattle(BattleManager battleManager)
    {
        CurrentBattle = battleManager;
    }

    public void ClearCurrentLevel()
    {
        CurrentLevel = null;
        CurrentBattle = null;
        State = RunFlowState.MapSelection;
    }

    public void AdvanceMapNode()
    {
        CurrentMapNodeIndex++;
        CurrentLevel = null;
        State = mapNodes != null && CurrentMapNodeIndex >= mapNodes.Count ? RunFlowState.Victory : RunFlowState.MapSelection;
    }

    public LevelData DrawNextBattleLevel(ChapterData chapter)
    {
        EnsureActiveChapter(chapter);
        if (ActiveChapter == null)
            return GetFallbackBattleLevel();

        BattleCount++;

        if (BattleCount <= 3)
            return DrawFromPool(remainingBeginPool, ActiveChapter.BeginPool, LevelType.Battle) ?? GetFallbackBattleLevel();

        if (BattleCount <= 6)
            return DrawFromPool(remainingMidPool, ActiveChapter.MidPool, LevelType.Battle) ?? GetFallbackBattleLevel();

        if (remainingMidPool.Count == 0 && remainingNormalPool.Count == 0)
            ResetMidAndNormalPools();

        return DrawFromMidNormalPools() ?? GetFallbackBattleLevel();
    }

    public LevelData DrawEventLevel(ChapterData chapter)
    {
        EnsureActiveChapter(chapter);
        if (ActiveChapter == null)
            return null;

        return DrawFromPool(remainingEventPool, ActiveChapter.EventPool, LevelType.Event, LevelType.Battle);
    }

    public LevelData DrawEliteLevel(ChapterData chapter)
    {
        EnsureActiveChapter(chapter);
        if (ActiveChapter == null)
            return null;

        return DrawFromPool(remainingElitePool, ActiveChapter.ElitePool, LevelType.Elite, LevelType.Battle);
    }

    public LevelData DrawBossLevel(ChapterData chapter)
    {
        EnsureActiveChapter(chapter);
        if (ActiveChapter == null)
            return GetFallbackBossLevel();

        return DrawFromArray(ActiveChapter.BossPool, LevelType.Battle, LevelType.Elite) ?? GetFallbackBossLevel();
    }

    public RunPoolSaveData ExportPoolState()
    {
        return new RunPoolSaveData
        {
            battleCount = BattleCount,
            remainingBeginPool = remainingBeginPool.ToArray(),
            remainingMidPool = remainingMidPool.ToArray(),
            remainingNormalPool = remainingNormalPool.ToArray(),
            remainingEventPool = remainingEventPool.ToArray(),
            remainingElitePool = remainingElitePool.ToArray()
        };
    }

    public void RestorePoolState(RunPoolSaveData data)
    {
        if (data == null)
            return;

        BattleCount = data.battleCount < 0 ? 0 : data.battleCount;
        FillPool(remainingBeginPool, data.remainingBeginPool);
        FillPool(remainingMidPool, data.remainingMidPool);
        FillPool(remainingNormalPool, data.remainingNormalPool);
        FillPool(remainingEventPool, data.remainingEventPool);
        FillPool(remainingElitePool, data.remainingElitePool);
    }

    public int NextRandomInt(int minInclusive, int maxExclusive)
    {
        return PlayerStatus != null ? PlayerStatus.NextRunRandomInt(minInclusive, maxExclusive) : UnityEngine.Random.Range(minInclusive, maxExclusive);
    }

    public void AdvanceRunRandomStep()
    {
        NextRandomInt(0, int.MaxValue);
    }

    private void EnsureActiveChapter(ChapterData chapter)
    {
        if (!ReferenceEquals(ActiveChapter, chapter))
            SetActiveChapter(chapter);
    }

    private void ResetChapterBattlePools()
    {
        BattleCount = 0;
        FillPool(remainingBeginPool, ActiveChapter != null ? ActiveChapter.BeginPool : null);
        FillPool(remainingEventPool, ActiveChapter != null ? ActiveChapter.EventPool : null);
        FillPool(remainingElitePool, ActiveChapter != null ? ActiveChapter.ElitePool : null);
        ResetMidAndNormalPools();
    }

    private void ResetMidAndNormalPools()
    {
        FillPool(remainingMidPool, ActiveChapter != null ? ActiveChapter.MidPool : null);
        FillPool(remainingNormalPool, ActiveChapter != null ? ActiveChapter.NormalPool : null);
    }

    private static void FillPool(List<int> target, int[] source)
    {
        target.Clear();
        if (source == null)
            return;

        for (int i = 0; i < source.Length; i++)
            target.Add(source[i]);
    }

    private LevelData DrawFromPool(List<int> pool, int[] resetSource, params LevelType[] allowedTypes)
    {
        if (pool.Count == 0)
            FillPool(pool, resetSource);

        if (pool.Count == 0)
            return null;

        combinedCandidateIndexes.Clear();
        for (int i = 0; i < pool.Count; i++)
        {
            if (TryGetAllowedLevel(pool[i], allowedTypes, out _))
                combinedCandidateIndexes.Add(i);
        }

        if (combinedCandidateIndexes.Count == 0)
        {
            pool.Clear();
            return null;
        }

        int poolIndex = combinedCandidateIndexes[NextRandomInt(0, combinedCandidateIndexes.Count)];
        int levelId = pool[poolIndex];
        pool.RemoveAt(poolIndex);
        return GameDataDatabase.TryGetLevelData(levelId, out LevelData level) ? level : null;
    }

    private LevelData DrawFromMidNormalPools()
    {
        combinedCandidateIndexes.Clear();
        combinedCandidatePools.Clear();
        AddPoolCandidates(remainingMidPool, 0, LevelType.Battle);
        AddPoolCandidates(remainingNormalPool, 1, LevelType.Battle);

        if (combinedCandidateIndexes.Count == 0)
            return null;

        int candidateIndex = NextRandomInt(0, combinedCandidateIndexes.Count);
        List<int> pool = combinedCandidatePools[candidateIndex] == 0 ? remainingMidPool : remainingNormalPool;
        int poolIndex = combinedCandidateIndexes[candidateIndex];
        int levelId = pool[poolIndex];
        pool.RemoveAt(poolIndex);
        return GameDataDatabase.TryGetLevelData(levelId, out LevelData level) ? level : null;
    }

    private void AddPoolCandidates(List<int> pool, int poolId, params LevelType[] allowedTypes)
    {
        for (int i = 0; pool != null && i < pool.Count; i++)
        {
            if (!TryGetAllowedLevel(pool[i], allowedTypes, out _))
                continue;

            combinedCandidateIndexes.Add(i);
            combinedCandidatePools.Add(poolId);
        }
    }

    private LevelData DrawFromArray(int[] levelIds, params LevelType[] allowedTypes)
    {
        combinedCandidateIndexes.Clear();
        for (int i = 0; levelIds != null && i < levelIds.Length; i++)
        {
            if (TryGetAllowedLevel(levelIds[i], allowedTypes, out _))
                combinedCandidateIndexes.Add(i);
        }

        if (combinedCandidateIndexes.Count == 0)
            return null;

        int index = combinedCandidateIndexes[NextRandomInt(0, combinedCandidateIndexes.Count)];
        return GameDataDatabase.TryGetLevelData(levelIds[index], out LevelData level) ? level : null;
    }

    private static bool TryGetAllowedLevel(int levelId, LevelType[] allowedTypes, out LevelData level)
    {
        if (!GameDataDatabase.TryGetLevelData(levelId, out level) || level == null)
            return false;

        for (int i = 0; allowedTypes != null && i < allowedTypes.Length; i++)
        {
            if (level.levelType == allowedTypes[i])
                return true;
        }
        return false;
    }

    private LevelData GetFallbackBattleLevel()
    {
        foreach (LevelData level in GameDataDatabase.LevelData.Values)
        {
            if (level != null && level.levelType == LevelType.Battle)
                return level;
        }
        return null;
    }

    private LevelData GetFallbackBossLevel()
    {
        LevelData bossLevel = null;
        foreach (LevelData level in GameDataDatabase.LevelData.Values)
        {
            if (level != null && (level.levelType == LevelType.Battle || level.levelType == LevelType.Elite) && (bossLevel == null || level.numericId > bossLevel.numericId))
                bossLevel = level;
        }
        return bossLevel;
    }

    private static RunFlowState GetStateForLevel(LevelData level)
    {
        if (level == null)
            return RunFlowState.MapSelection;

        switch (level.levelType)
        {
            case LevelType.Battle:
            case LevelType.Elite:
                return RunFlowState.Battle;
            case LevelType.Event:
                return RunFlowState.Event;
            case LevelType.Rest:
                return RunFlowState.Rest;
            case LevelType.Shop:
                return RunFlowState.Shop;
            case LevelType.Reward:
                return RunFlowState.Reward;
            default:
                return RunFlowState.BeforeNode;
        }
    }
}
