using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lookup boundary only. Defaults read real production content inside the owning player's rule scope.
/// Custom content must also be present in that player's IRuleContentProvider: RunManager owns boss/pool draws.
/// Do not sort these sequences; their existing order is part of seeded generation.
/// </summary>
public sealed class RunMapGenerationServices
{
    public Func<int, LevelData> FindLevel = id => GameDataDatabase.TryGetLevelData(id, out LevelData level) ? level : null;
    public Func<IEnumerable<LevelData>> AllLevels = () => GameDataDatabase.LevelData.Values;
    public Func<IEnumerable<ChapterData>> AllChapters = () => GameDataDatabase.ChapterData.Values;
}

public sealed class RunMapGenerationOptions
{
    /// <summary>Optional existing adapter-owned list. Otherwise reuse the list already attached to RunManager.</summary>
    public List<RunMapNodeModel> MapNodes;
    /// <summary>Null uses the run's configured difficulty; zero explicitly disables extra top boss cells.</summary>
    public int? AdditionalTopBossCellCount;
}

/// <summary>The actual models attached to the run, not a fake/snapshot map.</summary>
public sealed class RunMapGenerationResult
{
    public ChapterData Chapter { get; }
    public List<RunMapNodeModel> MapNodes { get; }
    public RunMapGridModel Grid { get; }
    public int ChapterLength { get; }
    public bool Succeeded { get; }

    internal RunMapGenerationResult(ChapterData chapter, List<RunMapNodeModel> nodes,
        RunMapGridModel grid, int chapterLength, bool succeeded)
    {
        Chapter = chapter;
        MapNodes = nodes;
        Grid = grid;
        ChapterLength = chapterLength;
        Succeeded = succeeded;
    }
}

/// <summary>
/// First-floor production map rules extracted from HandSystemUI.BuildDebugMap/BuildChapterMapGrid.
/// No Scene, UI, animation or save hooks. Uses RunManager's existing grid builder, boss cache and RNG.
/// DungeonMapGenerator is a separate, currently uncalled room/corridor prototype; selecting it here
/// would change the existing first-floor topology and random consumption, so it is deliberately not used.
/// </summary>
public static class RunMapGenerationRules
{
    public const int DefaultChapterLength = 21;
    public const int TutorialBattleLevelId = 1001;
    public const int TutorialEventLevelId = 1002;
    public const int TutorialRestLevelId = 201;
    public const int TutorialBossLevelId = 1005;

    /// <summary>
    /// Real production default entry. Resolves a missing chapter from the run/content, generates the
    /// legacy choice nodes first and then the grid in exactly the existing RNG order, and attaches both.
    /// Does not initialize/reseed the player, change difficulty, touch RunManager.Current or save a run.
    /// </summary>
    public static RunMapGenerationResult Generate(RunManager run, ChapterData chapter = null,
        RunMapGenerationOptions options = null, RunMapGenerationServices services = null)
    {
        RequireOwner(run);
        using (run.PlayerStatus.Rules.Enter())
        {
            var generator = new Generator(run, services ?? new RunMapGenerationServices());
            chapter = generator.ResolveChapter(chapter);
            List<RunMapNodeModel> nodes = options != null && options.MapNodes != null
                ? options.MapNodes : run.MapNodes as List<RunMapNodeModel> ?? new List<RunMapNodeModel>();
            run.AttachMapNodes(nodes);
            nodes.Clear();
            run.SetActiveChapter(chapter);
            int chapterLength = EffectiveChapterLength(chapter);
            if (!generator.BuildMapNodes(chapter, chapterLength, nodes))
                return new RunMapGenerationResult(chapter, nodes, run.MapGrid, chapterLength, false);

            run.SetCurrentMapNodeIndex(0);
            generator.BuildChapterMapGrid(chapter, options?.AdditionalTopBossCellCount
                ?? DifficultyUpgradeSystem.GetAdditionalTopBossCellCount());
            run.Session.ConfigureMapProgress(chapterLength, IsTutorial(chapter));
            return new RunMapGenerationResult(chapter, nodes, run.MapGrid, chapterLength, true);
        }
    }

    /// <summary>
    /// Grid-only production entry for missing-grid recovery/legacy public BuildChapterMapGrid.
    /// Preserves attached nodes and node index; unlike Generate it does not redraw choice nodes.
    /// Boss resolution/cache behavior is unchanged. Restore the chapter/pools first when loading a save.
    /// </summary>
    public static RunMapGridModel GenerateGrid(RunManager run, ChapterData chapter = null,
        int? additionalTopBossCellCount = null, RunMapGenerationServices services = null)
    {
        RequireOwner(run);
        using (run.PlayerStatus.Rules.Enter())
        {
            var generator = new Generator(run, services ?? new RunMapGenerationServices());
            chapter = generator.ResolveChapter(chapter);
            generator.BuildChapterMapGrid(chapter, additionalTopBossCellCount
                ?? DifficultyUpgradeSystem.GetAdditionalTopBossCellCount());
            run.Session.ConfigureMapProgress(EffectiveChapterLength(chapter), IsTutorial(chapter));
            return run.MapGrid;
        }
    }

    public static int GetChapterLength(RunManager run, ChapterData chapter = null)
    {
        RequireOwner(run);
        using (run.PlayerStatus.Rules.Enter())
            return EffectiveChapterLength(chapter ?? run.ActiveChapter);
    }

    private static int EffectiveChapterLength(ChapterData chapter)
    {
        int length = chapter != null && chapter.levelLength > 0 ? chapter.levelLength : DefaultChapterLength;
        return DifficultyUpgradeSystem.ModifyChapterLength(length);
    }

    private static bool IsTutorial(ChapterData chapter) => chapter != null && chapter.numericId == TutorialPolicy.TutorialChapterNumericId;

    private static void RequireOwner(RunManager run)
    {
        if (run == null) throw new ArgumentNullException(nameof(run));
        if (run.PlayerStatus == null) throw new ArgumentException("Map generation requires a run-owned player and rule context.", nameof(run));
    }

    private sealed class Generator
    {
        private readonly RunManager run;
        private readonly RunMapGenerationServices services;

        internal Generator(RunManager run, RunMapGenerationServices services)
        {
            this.run = run;
            this.services = services;
        }

        internal ChapterData ResolveChapter(ChapterData chapter)
        {
            if (chapter != null) return chapter;
            if (run.ActiveChapter != null) return run.ActiveChapter;
            foreach (ChapterData candidate in services.AllChapters())
                if (candidate != null) return candidate;
            return null;
        }

        internal bool BuildMapNodes(ChapterData chapter, int chapterLength, List<RunMapNodeModel> nodes)
        {
            // Preserve the pre-grid boss draw even though ResolveChapterBoss draws/caches again below.
            // Removing it would shift every later RNG result and change existing seeded runs.
            LevelData bossLevel = run.DrawBossLevel(chapter);
            for (int i = 0; i < chapterLength; i++)
            {
                int progress = i + 1;
                var node = new RunMapNodeModel();
                if (progress == chapterLength && bossLevel != null)
                {
                    node.leftLevel = bossLevel;
                    node.rightLevel = bossLevel;
                    node.fixedSingleChoice = true;
                    ApplyHiddenRolls(chapter, node);
                    nodes.Add(node);
                    continue;
                }

                List<LevelData> candidates = GetLevelsForProgress(chapter, progress);
                RemoveBossBattleLevel(candidates, bossLevel);
                if (candidates.Count == 0) candidates = GetBattleLevels();
                if (candidates.Count == 0) return false;
                LevelData fixedLevel = GetFixedLevelForProgress(chapter, progress);
                if (fixedLevel != null)
                {
                    node.leftLevel = fixedLevel;
                    node.rightLevel = fixedLevel;
                    node.fixedSingleChoice = true;
                }
                else
                {
                    node.leftLevel = ChooseRandomMapLevel(chapter, candidates);
                    node.rightLevel = ChooseRandomMapLevel(chapter, candidates);
                }
                ApplyHiddenRolls(chapter, node);
                nodes.Add(node);
            }
            return true;
        }

        internal void BuildChapterMapGrid(ChapterData chapter, int additionalTopBossCellCount)
        {
            bool tutorial = IsTutorial(chapter);
            additionalTopBossCellCount = tutorial ? 0 : Mathf.Max(0, additionalTopBossCellCount);
            int width = Mathf.Max(1, chapter != null && chapter.mapWidth > 0 ? chapter.mapWidth : 5);
            int baseHeight = Mathf.Max(1, chapter != null && chapter.mapHeight > 0 ? chapter.mapHeight : 5);
            bool designed = chapter != null && !tutorial && width == 5 && baseHeight == 8;
            if (designed)
                baseHeight = DifficultyUpgradeSystem.ModifyDesignedMapMainAreaHeight(5) + 3;
            else if (!tutorial)
            {
                width = DifficultyUpgradeSystem.ModifyMapWidth(width);
                baseHeight = DifficultyUpgradeSystem.ModifyMapHeight(baseHeight);
            }

            int height = baseHeight + additionalTopBossCellCount;
            int cellCount = width * height;
            var levels = new List<LevelData>(cellCount);
            for (int i = 0; i < cellCount; i++) levels.Add(null);

            if (tutorial)
            {
                SetMapGridLevel(levels, width, 0, 1, services.FindLevel(TutorialBattleLevelId));
                SetMapGridLevel(levels, width, 0, 2, services.FindLevel(TutorialEventLevelId));
                SetMapGridLevel(levels, width, 0, 3, services.FindLevel(TutorialRestLevelId));
                SetMapGridLevel(levels, width, 0, 4, services.FindLevel(TutorialBossLevelId));
                run.BuildMapGrid(chapter, levels, width, height);
                ApplyTutorialChapterMapCellFlags();
                return;
            }

            LevelData bossLevel = run.ResolveChapterBoss(chapter) ?? GetChapterBossPreviewLevel(chapter);
            HashSet<Vector2Int> blocked = designed
                ? SelectDesignedChapterBlockedCells(baseHeight, DifficultyUpgradeSystem.GetMapBlockedCellCount()) : null;
            if (designed)
                BuildDesignedChapterMapGrid(chapter, levels, width, height, bossLevel, blocked, additionalTopBossCellCount);
            else
            {
                for (int i = 0; i < cellCount; i++)
                    levels[i] = ChooseRandomMapLevel(chapter, GetMapGridCandidateLevels(chapter, i + 1, bossLevel));
            }

            run.BuildMapGrid(chapter, levels, width, height, additionalTopBossCellCount, bossLevel);
            if (designed) ApplyDesignedChapterMapCellFlags(blocked, additionalTopBossCellCount);
            DifficultyUpgradeSystem.ApplyMapUpgrades(run.MapGrid, run.NextRandomInt, !designed);
            run.RevealCurrentMapNeighbors();
        }

        private void ApplyTutorialChapterMapCellFlags()
        {
            RunMapGridModel grid = run.MapGrid;
            if (grid == null) return;
            for (int i = 0; i < grid.cells.Count; i++)
            {
                RunMapCellModel cell = grid.cells[i];
                if (cell == null) continue;
                bool start = cell.x == grid.playerX && cell.y == grid.playerY;
                cell.isAvailable = start || cell.level != null;
                cell.isBoss = cell.level != null && cell.level.numericId == TutorialBossLevelId;
                cell.isRevealed = cell.isBoss;
            }
            run.RevealCurrentMapNeighbors();
        }

        private void BuildDesignedChapterMapGrid(ChapterData chapter, List<LevelData> levels, int width,
            int height, LevelData bossLevel, HashSet<Vector2Int> blockedCells, int additionalTopBossCellCount)
        {
            int bossCellCount = additionalTopBossCellCount + 1;
            int bossStartY = height - bossCellCount;
            for (int bossIndex = 0; bossIndex < bossCellCount; bossIndex++)
                SetMapGridLevel(levels, width, 2, bossStartY + bossIndex, bossLevel);
            SetMapGridLevel(levels, width, 2, 2, ChooseRandomMapLevel(chapter, GetBattleLevels()));
            SetMapGridLevel(levels, width, 2, 1, ChooseRandomMapLevel(chapter, GetBattleLevels()));

            var positions = new List<Vector2Int>();
            for (int y = bossStartY - 1; y >= 2; y--)
            {
                for (int x = 0; x < width; x++)
                {
                    var position = new Vector2Int(x, y);
                    if (x == 2 && y == 2 || blockedCells != null && blockedCells.Contains(position)) continue;
                    positions.Add(position);
                }
            }
            AssignDesignedMapLevels(chapter, levels, width, positions, LevelType.Battle, DifficultyUpgradeSystem.ModifyDesignedMapLevelCount(LevelType.Battle, 3));
            AssignDesignedMapLevels(chapter, levels, width, positions, LevelType.Elite, DifficultyUpgradeSystem.ModifyDesignedMapLevelCount(LevelType.Elite, 2));
            AssignDesignedMapLevels(chapter, levels, width, positions, LevelType.Rest, DifficultyUpgradeSystem.ModifyDesignedMapLevelCount(LevelType.Rest, 2));
            while (positions.Count > 0)
            {
                Vector2Int position = TakeRandomMapPosition(positions);
                List<LevelData> candidates = GetMapGridCandidateLevels(chapter, position.y * width + position.x + 1, bossLevel);
                SetMapGridLevel(levels, width, position.x, position.y, ChooseRandomMapLevel(chapter, candidates));
            }
        }

        private void AssignDesignedMapLevels(ChapterData chapter, List<LevelData> levels, int width,
            List<Vector2Int> positions, LevelType type, int count)
        {
            for (int i = 0; i < count && positions.Count > 0; i++)
            {
                Vector2Int position = TakeRandomMapPosition(positions);
                List<LevelData> candidates = type == LevelType.Battle ? GetBattleLevels() : GetLevels(type);
                SetMapGridLevel(levels, width, position.x, position.y, ChooseRandomMapLevel(chapter, candidates));
            }
        }

        private Vector2Int TakeRandomMapPosition(List<Vector2Int> positions)
        {
            int index = run.NextRandomInt(0, positions.Count);
            Vector2Int position = positions[index];
            positions.RemoveAt(index);
            return position;
        }

        private static void SetMapGridLevel(List<LevelData> levels, int width, int x, int y, LevelData level)
        {
            int index = y * width + x;
            if (index >= 0 && index < levels.Count) levels[index] = level;
        }

        private HashSet<Vector2Int> SelectDesignedChapterBlockedCells(int baseHeight, int count)
        {
            var blocked = new HashSet<Vector2Int>();
            var candidates = new List<Vector2Int>();
            int maxY = baseHeight == 7 ? 4 : 5;
            for (int y = 3; y <= maxY; y++)
                for (int x = 1; x <= 3; x++) candidates.Add(new Vector2Int(x, y));
            for (int i = 0; i < count && candidates.Count > 0; i++)
            {
                int index = run.NextRandomInt(0, candidates.Count);
                blocked.Add(candidates[index]);
                candidates.RemoveAt(index);
            }
            return blocked;
        }

        private void ApplyDesignedChapterMapCellFlags(HashSet<Vector2Int> blocked, int additionalTopBossCellCount)
        {
            RunMapGridModel grid = run.MapGrid;
            if (grid == null) return;
            int bossCellCount = Mathf.Max(0, additionalTopBossCellCount) + 1;
            int bossStartY = grid.height - bossCellCount;
            bool fixedBossPath = additionalTopBossCellCount > 0;
            for (int i = 0; i < grid.cells.Count; i++)
            {
                RunMapCellModel cell = grid.cells[i];
                if (cell == null) continue;
                bool boss = cell.x == 2 && cell.y >= bossStartY;
                bool available = cell.y >= 2 && cell.y < bossStartY || cell.x == 2 && (cell.y == 0 || cell.y == 1);
                cell.isAvailable = boss || available && (blocked == null || !blocked.Contains(new Vector2Int(cell.x, cell.y)));
                cell.isBoss = boss;
                cell.isEnd = fixedBossPath && cell.x == 2 && cell.y == grid.height - 1;
                cell.isCompleted = false;
                cell.isRevealed = boss;
            }
            run.RevealCurrentMapNeighbors();
        }

        private List<LevelData> GetMapGridCandidateLevels(ChapterData chapter, int progress, LevelData bossLevel)
        {
            List<LevelData> candidates = GetLevelsForProgress(chapter, progress);
            RemoveBossBattleLevel(candidates, bossLevel);
            AddLevelsIfMissing(candidates, GetEventLevelsForChapter(chapter));
            AddLevelsIfMissing(candidates, GetLevels(LevelType.Rest));
            AddLevelsIfMissing(candidates, GetLevels(LevelType.Reward));
            if (candidates.Count == 0) candidates = GetBattleLevels();
            return candidates;
        }

        private static void AddLevelsIfMissing(List<LevelData> target, List<LevelData> source)
        {
            if (target == null || source == null) return;
            for (int i = 0; i < source.Count; i++)
            {
                LevelData level = source[i];
                if (level != null && !target.Contains(level)) target.Add(level);
            }
        }

        private LevelData GetChapterBossPreviewLevel(ChapterData chapter)
        {
            if (run.ChapterBossLevel != null) return run.ChapterBossLevel;
            for (int i = 0; chapter?.BossPool != null && i < chapter.BossPool.Length; i++)
            {
                LevelData level = services.FindLevel(chapter.BossPool[i]);
                if (level != null) return level;
            }
            return GetBossBattleLevel();
        }

        private void ApplyHiddenRolls(ChapterData chapter, RunMapNodeModel node)
        {
            float hiddenWeight = Mathf.Clamp01(chapter != null ? chapter.hiddenLevelWeight : 0f);
            if (hiddenWeight <= 0f) return;
            node.leftHidden = CanHideMapNodeLevel(node.leftLevel) && RollHiddenLevel(hiddenWeight);
            node.rightHidden = node.fixedSingleChoice || node.rightLevel == node.leftLevel
                ? node.leftHidden : CanHideMapNodeLevel(node.rightLevel) && RollHiddenLevel(hiddenWeight);
        }

        private bool RollHiddenLevel(float weight) => run.NextRandomInt(0, 10000) < Mathf.RoundToInt(weight * 10000f);
        private static bool CanHideMapNodeLevel(LevelData level) => level != null && level.levelType != LevelType.Elite;

        private LevelData ChooseRandomMapLevel(ChapterData chapter, List<LevelData> levels)
        {
            if (levels == null || levels.Count == 0) return null;
            var types = new List<LevelType>();
            var weights = new List<int>();
            int totalWeight = 0;
            for (int i = 0; i < levels.Count; i++)
            {
                LevelData level = levels[i];
                if (level == null || types.Contains(level.levelType)) continue;
                int weight = GetMapLevelWeight(level, chapter);
                if (weight <= 0) continue;
                types.Add(level.levelType);
                weights.Add(weight);
                totalWeight += weight;
            }
            if (types.Count == 0 || totalWeight <= 0) return levels[run.NextRandomInt(0, levels.Count)];
            LevelType selectedType = types[types.Count - 1];
            int roll = run.NextRandomInt(0, totalWeight);
            for (int i = 0; i < types.Count; i++)
            {
                if (roll < weights[i])
                {
                    selectedType = types[i];
                    break;
                }
                roll -= weights[i];
            }
            var sameType = new List<LevelData>();
            for (int i = 0; i < levels.Count; i++)
                if (levels[i] != null && levels[i].levelType == selectedType) sameType.Add(levels[i]);
            return sameType.Count > 0 ? sameType[run.NextRandomInt(0, sameType.Count)] : levels[run.NextRandomInt(0, levels.Count)];
        }

        private static int GetMapLevelWeight(LevelData level, ChapterData chapter)
        {
            int weight = chapter != null ? chapter.defaultMapLevelWeight : 1;
            switch (level.levelType)
            {
                case LevelType.Battle: weight = chapter != null ? chapter.battleMapLevelWeight : 10; break;
                case LevelType.Elite: weight = chapter != null ? chapter.eliteMapLevelWeight : 10; break;
                case LevelType.Event: weight = chapter != null ? chapter.eventMapLevelWeight : 4; break;
                case LevelType.RemoveMaterial:
                case LevelType.AddMaterial: weight = 0; break;
                case LevelType.Rest: weight = chapter != null ? chapter.restMapLevelWeight : 2; break;
                case LevelType.Shop: weight = chapter != null ? chapter.shopMapLevelWeight : 2; break;
                case LevelType.Reward: weight = chapter != null ? chapter.rewardMapLevelWeight : 2; break;
            }
            return DifficultyUpgradeSystem.ModifyMapLevelWeight(level.levelType, Mathf.Max(0, weight));
        }

        private LevelData GetFixedLevelForProgress(ChapterData chapter, int progress)
        {
            for (int i = 0; chapter?.fixed_level != null && i < chapter.fixed_level.Length; i++)
            {
                ChapterFixedLevelData fixedLevel = chapter.fixed_level[i];
                if (fixedLevel == null || fixedLevel.levelIndex != progress) continue;
                if (fixedLevel.levelId > 0)
                {
                    LevelData configured = services.FindLevel(fixedLevel.levelId);
                    if (configured != null) return configured;
                }
                List<LevelData> levels = fixedLevel.levelType == LevelType.Battle || fixedLevel.levelType == LevelType.Elite
                    ? GetBattleLevels() : GetLevels(fixedLevel.levelType);
                return levels.Count > 0 ? levels[run.NextRandomInt(0, levels.Count)] : null;
            }
            return null;
        }

        private List<LevelData> GetLevelsForProgress(ChapterData chapter, int progress)
        {
            int[] ids = GetLevelPoolIdsForProgress(chapter, progress);
            if (ids == null || ids.Length == 0) return GetBattleLevels();
            var levels = new List<LevelData>();
            for (int i = 0; i < ids.Length; i++)
            {
                LevelData level = services.FindLevel(ids[i]);
                if (level != null && (level.levelType == LevelType.Battle || level.levelType == LevelType.Elite
                    || level.levelType == LevelType.Event || level.levelType == LevelType.Rest || level.levelType == LevelType.Reward))
                    levels.Add(level);
            }
            return levels;
        }

        private static int[] GetLevelPoolIdsForProgress(ChapterData chapter, int progress)
        {
            if (chapter == null) return null;
            for (int i = 0; chapter.levelPoolRanges != null && i < chapter.levelPoolRanges.Length; i++)
            {
                ChapterLevelPoolRangeData range = chapter.levelPoolRanges[i];
                if (range != null && progress >= range.startProgress && progress <= range.endProgress)
                    return range.levelPoolIds;
            }
            return chapter.levelPoolIds;
        }

        private List<LevelData> GetEventLevelsForChapter(ChapterData chapter)
        {
            var events = new List<LevelData>();
            for (int i = 0; chapter?.eventPoolIds != null && i < chapter.eventPoolIds.Length; i++)
            {
                LevelData level = services.FindLevel(chapter.eventPoolIds[i]);
                if (level != null && level.levelType == LevelType.Event) events.Add(level);
            }
            if (events.Count == 0) events.AddRange(GetLevels(LevelType.Event));
            return events;
        }

        private List<LevelData> GetLevels(LevelType type)
        {
            var levels = new List<LevelData>();
            foreach (LevelData level in services.AllLevels())
                if (level != null && level.levelType == type) levels.Add(level);
            return levels;
        }

        private List<LevelData> GetBattleLevels()
        {
            List<LevelData> levels = GetLevels(LevelType.Battle);
            RemoveBossBattleLevel(levels, GetBossBattleLevel(levels));
            return levels;
        }

        private LevelData GetBossBattleLevel(List<LevelData> battleLevels = null)
        {
            LevelData boss = null;
            foreach (LevelData level in battleLevels != null ? (IEnumerable<LevelData>)battleLevels : services.AllLevels())
                if (IsBattleLevel(level) && (boss == null || level.numericId > boss.numericId)) boss = level;
            return boss;
        }

        private static bool IsBattleLevel(LevelData level) => level != null && (level.levelType == LevelType.Battle || level.levelType == LevelType.Elite);

        private static void RemoveBossBattleLevel(List<LevelData> levels, LevelData boss)
        {
            if (levels == null || boss == null) return;
            for (int i = levels.Count - 1; i >= 0; i--)
                if (IsBattleLevel(levels[i]) && levels[i].numericId == boss.numericId) levels.RemoveAt(i);
        }
    }
}
