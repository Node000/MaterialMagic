using System.Collections.Generic;
using UnityEngine;

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
    // 出战分档：前 2 场弱怪（BeginPool）→ 接下来 3 场普怪（MidPool）→ 之后全部强怪（NormalPool）。
    // 三档池互相独立，跨档时不把上一档没抽完的关卡并入下一档，避免普怪池混弱怪、强怪池混普怪与弱怪。
    private const int BeginPoolBattleLimit = 2;
    private const int MidPoolBattleLimit = 5;

    private readonly List<int> remainingBeginPool = new List<int>();
    private readonly List<int> remainingMidPool = new List<int>();
    private readonly List<int> remainingNormalPool = new List<int>();
    private readonly List<int> remainingEventPool = new List<int>();
    private readonly List<int> remainingElitePool = new List<int>();
    private readonly List<int> combinedCandidateIndexes = new List<int>();
    private List<RunMapNodeModel> mapNodes;
    private RunMapGridModel mapGrid;
    private ChapterData chapterBossOwner;
    private LevelData chapterBossLevel;
    private int chapterBossGroupIndex = -1;

    public static RunManager Current { get; private set; }

    public PlayerStatus PlayerStatus { get; }
    public IReadOnlyList<RunMapNodeModel> MapNodes => mapNodes;
    public RunMapGridModel MapGrid => mapGrid;
    public int CurrentMapX => mapGrid != null ? mapGrid.playerX : 0;
    public int CurrentMapY => mapGrid != null ? mapGrid.playerY : 0;
    public int CurrentMapNodeIndex { get; private set; }
    public int BattleCount { get; private set; }
    public ChapterData ActiveChapter { get; private set; }
    public LevelData CurrentLevel { get; private set; }
    public RunFlowState State { get; private set; }
    public BattleManager CurrentBattle { get; private set; }

    /// <summary>本局在生成章节地图时确定的 Boss 关卡（未确定时为 null）。</summary>
    public LevelData ChapterBossLevel => chapterBossLevel;

    /// <summary>本局 Boss 使用的随机敌人组下标；无组或未确定时为 -1。</summary>
    public int ChapterBossGroupIndex => chapterBossGroupIndex;

    public RunManager(PlayerStatus playerStatus)
    {
        PlayerStatus = playerStatus;
        mapNodes = new List<RunMapNodeModel>();
        mapGrid = new RunMapGridModel();
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

    public void AttachMapGrid(RunMapGridModel grid)
    {
        mapGrid = grid ?? new RunMapGridModel();
    }

    public void BuildMapGrid(ChapterData chapter, IList<LevelData> levels, int width, int height, int additionalTopBossCellCount = 0, LevelData bossLevel = null)
    {
        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);
        mapGrid = new RunMapGridModel
        {
            width = width,
            height = height,
            playerX = chapter != null ? chapter.startMapX : width / 2,
            playerY = chapter != null ? chapter.startMapY : height / 2,
            bossMapActive = false
        };
        mapGrid.ClampPosition(ref mapGrid.playerX, ref mapGrid.playerY);

        int cellCount = width * height;
        for (int i = 0; i < cellCount; i++)
        {
            LevelData level = levels != null && i < levels.Count ? levels[i] : null;
            RunMapCellModel cell = new RunMapCellModel
            {
                x = i % width,
                y = i / width,
                level = level,
                isBoss = false,
                isAvailable = level != null,
                isRevealed = false
            };
            if (cell.x == mapGrid.playerX && cell.y == mapGrid.playerY)
            {
                cell.level = null;
                cell.isAvailable = true;
            }
            mapGrid.cells.Add(cell);
        }

        if (additionalTopBossCellCount > 0 && bossLevel != null)
        {
            int bossCellCount = additionalTopBossCellCount + 1;
            int bossX = Mathf.Clamp(width / 2, 0, width - 1);
            int bossStartY = height - bossCellCount;
            for (int bossIndex = 0; bossIndex < bossCellCount; bossIndex++)
            {
                RunMapCellModel bossCell = mapGrid.GetCell(bossX, bossStartY + bossIndex);
                if (bossCell == null)
                    continue;

                bossCell.level = bossLevel;
                bossCell.isBoss = true;
                bossCell.isEnd = bossIndex == bossCellCount - 1;
                bossCell.isCompleted = false;
                bossCell.isAvailable = true;
                bossCell.isRevealed = true;
                bossCell.isHidden = false;
            }
        }
        RevealCurrentMapNeighbors();
    }

    public bool RestoreMapGrid(RunMapGridModel grid)
    {
        if (grid == null || grid.width <= 0 || grid.height <= 0 || grid.cells == null || grid.cells.Count == 0)
            return false;

        mapGrid = grid;
        mapGrid.ClampPosition(ref mapGrid.playerX, ref mapGrid.playerY);
        return true;
    }

    public RunMapCellModel MoveMapPlayer(MaterialEnum material)
    {
        if (mapGrid == null || mapGrid.width <= 0 || mapGrid.height <= 0)
            return null;

        Vector2Int direction = GetMapDirection(material);
        if (direction == Vector2Int.zero)
            return mapGrid.GetCurrentCell();

        int nextX = mapGrid.playerX + direction.x;
        int nextY = mapGrid.playerY + direction.y;
        if (nextX < 0 || nextX >= mapGrid.width || nextY < 0 || nextY >= mapGrid.height)
            return null;

        RunMapCellModel targetCell = mapGrid.GetCell(nextX, nextY);
        if (!mapGrid.CanEnterCell(targetCell))
            return null;

        mapGrid.playerX = nextX;
        mapGrid.playerY = nextY;
        RevealCurrentMapNeighbors();
        return targetCell;
    }

    public void ConsumeCurrentMapCellLevel()
    {
        RunMapCellModel cell = mapGrid != null ? mapGrid.GetCurrentCell() : null;
        if (cell != null && !cell.isBoss)
        {
            cell.level = null;
            cell.isHidden = false;
        }
    }

    public bool CompleteCurrentBossCell()
    {
        RunMapCellModel cell = mapGrid != null ? mapGrid.GetCurrentCell() : null;
        if (cell == null || !cell.isBoss || !mapGrid.HasFixedBossPath)
            return false;

        cell.isCompleted = true;
        RevealMapCell(cell.x, cell.y + 1);
        return cell.isEnd;
    }

    public void RevealCurrentMapNeighbors()
    {
        if (mapGrid == null)
            return;

        RevealMapCell(mapGrid.playerX, mapGrid.playerY);
        RevealMapCell(mapGrid.playerX, mapGrid.playerY + 1);
        RevealMapCell(mapGrid.playerX, mapGrid.playerY - 1);
        RevealMapCell(mapGrid.playerX - 1, mapGrid.playerY);
        RevealMapCell(mapGrid.playerX + 1, mapGrid.playerY);
    }

    private void RevealMapCell(int x, int y)
    {
        RunMapCellModel cell = mapGrid.GetCell(x, y);
        if (cell != null && cell.isAvailable)
            cell.isRevealed = true;
    }

    public void ActivateBossMap()
    {
        if (mapGrid == null || mapGrid.HasFixedBossPath)
            return;

        mapGrid.bossMapActive = true;
        for (int i = 0; i < mapGrid.cells.Count; i++)
        {
            RunMapCellModel cell = mapGrid.cells[i];
            if (cell != null && mapGrid.IsCellReachable(cell.x, cell.y))
            {
                cell.isBoss = true;
                cell.isRevealed = true;
                cell.isHidden = false;
            }
        }
    }

    public static Vector2Int GetMapDirection(MaterialEnum material)
    {
        switch (material)
        {
            case MaterialEnum.Fire:
                return Vector2Int.up;
            case MaterialEnum.Wind:
                return Vector2Int.left;
            case MaterialEnum.Water:
                return Vector2Int.down;
            case MaterialEnum.Earth:
                return Vector2Int.right;
            default:
                return Vector2Int.zero;
        }
    }

    public void SetActiveChapter(ChapterData chapter)
    {
        if (ReferenceEquals(ActiveChapter, chapter))
            return;

        ActiveChapter = chapter;
        ResetChapterBoss();
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

        if (BattleCount <= BeginPoolBattleLimit)
            return DrawFromPool(remainingBeginPool, ActiveChapter.BeginPool, LevelType.Battle) ?? GetFallbackBattleLevel();

        if (BattleCount <= MidPoolBattleLimit)
            return DrawFromPool(remainingMidPool, ActiveChapter.MidPool, LevelType.Battle) ?? GetFallbackBattleLevel();

        return DrawFromPool(remainingNormalPool, ActiveChapter.NormalPool, LevelType.Battle) ?? GetFallbackBattleLevel();
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

    /// <summary>
    /// 确定（并缓存）本局章节 Boss：在生成章节地图时调用一次，先从章节 BossPool 抽关卡，
    /// 再为它抽一个随机敌人组（若关卡配了 randomEnemyGroups）。这样地图格图标与真正开打时用的是同一个 Boss。
    /// 同一章节重复调用返回缓存结果；换章节或新开局会自动重置。
    /// </summary>
    public LevelData ResolveChapterBoss(ChapterData chapter)
    {
        if (chapterBossLevel != null && ReferenceEquals(chapterBossOwner, chapter))
            return chapterBossLevel;

        chapterBossOwner = chapter;
        chapterBossLevel = DrawBossLevel(chapter);
        chapterBossGroupIndex = chapterBossLevel != null && chapterBossLevel.randomEnemyGroups != null && chapterBossLevel.randomEnemyGroups.Length > 0
            ? NextRandomInt(0, chapterBossLevel.randomEnemyGroups.Length)
            : -1;
        return chapterBossLevel;
    }

    /// <summary>
    /// 若 level 就是本局已确定的 Boss，则返回它固定使用的敌人组下标；否则返回 -1（表示按普通随机）。
    /// </summary>
    public int ResolveChapterBossGroupIndex(LevelData level)
    {
        return level != null && ReferenceEquals(level, chapterBossLevel) ? chapterBossGroupIndex : -1;
    }

    /// <summary>Boss 地图格图标路径（Resources 相对路径）；未配或未确定时返回 null（调用方回退通用图标）。</summary>
    public string ResolveChapterBossMapIconPath()
    {
        if (chapterBossLevel == null)
            return null;

        if (chapterBossLevel.randomEnemyGroups != null && chapterBossGroupIndex >= 0 && chapterBossGroupIndex < chapterBossLevel.randomEnemyGroups.Length)
        {
            string groupPath = chapterBossLevel.randomEnemyGroups[chapterBossGroupIndex] != null ? chapterBossLevel.randomEnemyGroups[chapterBossGroupIndex].mapIconPath : null;
            if (!string.IsNullOrEmpty(groupPath))
                return groupPath;
        }

        return chapterBossLevel.mapIconPath;
    }

    private void ResetChapterBoss()
    {
        chapterBossOwner = null;
        chapterBossLevel = null;
        chapterBossGroupIndex = -1;
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
            remainingElitePool = remainingElitePool.ToArray(),
            chapterBossLevelId = chapterBossLevel != null ? chapterBossLevel.numericId : 0,
            chapterBossGroupIndex = chapterBossGroupIndex
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
        RestoreChapterBoss(data.chapterBossLevelId, data.chapterBossGroupIndex);
    }

    /// <summary>从存档恢复本局 Boss（关卡 id + 敌人组下标）；id 为 0 表示旧存档没存过，保持未确定。</summary>
    private void RestoreChapterBoss(int bossLevelId, int bossGroupIndex)
    {
        chapterBossOwner = ActiveChapter;
        chapterBossGroupIndex = bossGroupIndex >= 0 ? bossGroupIndex : -1;
        chapterBossLevel = bossLevelId > 0 && GameDataDatabase.TryGetLevelData(bossLevelId, out LevelData level) ? level : null;
        if (chapterBossLevel == null)
            chapterBossGroupIndex = -1;
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
        FillPool(remainingMidPool, ActiveChapter != null ? ActiveChapter.MidPool : null);
        FillPool(remainingNormalPool, ActiveChapter != null ? ActiveChapter.NormalPool : null);
        FillPool(remainingEventPool, ActiveChapter != null ? ActiveChapter.EventPool : null);
        FillPool(remainingElitePool, ActiveChapter != null ? ActiveChapter.ElitePool : null);
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
            case LevelType.RemoveMaterial:
            case LevelType.AddMaterial:
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
