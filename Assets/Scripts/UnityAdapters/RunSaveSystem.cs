using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

public static class RunSaveSystem
{
    private const int CurrentVersion = RuleRunSnapshot.CurrentVersion;
    private const int HistoryVersion = 2;
    private const int MaxHistoryRecords = 100;
#if !UNITY_EDITOR && DEVELOPMENT_BUILD && MATERIALMAGIC_QA_UNLOCK
    private const string CurrentSlotPlayerPrefsKey = "RunSaveSystem.QA.CurrentSlotIndex";
#else
    private const string CurrentSlotPlayerPrefsKey = "RunSaveSystem.CurrentSlotIndex";
#endif
    private const string MapSelectionState = RuleRunSnapshot.MapSelectionState;
    private const string BeforeNodeState = RuleRunSnapshot.BeforeNodeState;
    private static Action<int> clearCodexSlot;

    /// <summary>Presentation-owned codex persistence; no reverse assembly reference.</summary>
    public static void ConfigureCodexSlotClear(Action<int> clear) => clearCodexSlot = clear;

    private static bool forceNewRun;
    private static int newRunSeed;
    private static bool startingTutorialRun;
    private static bool tutorialRunActive;
    private static int currentSlotIndex = 1;
    private static bool currentSlotIndexLoaded;

    private static string SaveDirectory
    {
        get
        {
#if UNITY_EDITOR
            // Allows local integration checks without reading or writing active player save files.
            string overridePath = Environment.GetEnvironmentVariable("MATERIALMAGIC_EDITOR_SAVE_PATH");
            if (!string.IsNullOrEmpty(overridePath) && Path.IsPathRooted(overridePath))
                return overridePath;
#endif
#if !UNITY_EDITOR && DEVELOPMENT_BUILD && MATERIALMAGIC_QA_UNLOCK
            return Path.Combine(Application.persistentDataPath, "Save_QA");
#else
            return Path.Combine(Application.persistentDataPath, "Save");
#endif
        }
    }
    public static string SaveFolderPath => SaveDirectory;
    public static int CurrentSlotIndex
    {
        get
        {
            EnsureCurrentSlotIndexLoaded();
            return currentSlotIndex;
        }
    }
    private static string RunSavePath => GetRunSavePath(CurrentSlotIndex);

    public static void SelectSlot(int slotIndex)
    {
        EnsureCurrentSlotIndexLoaded();
        currentSlotIndex = Mathf.Clamp(slotIndex, 1, 3);
        currentSlotIndexLoaded = true;
        PlayerPrefs.SetInt(CurrentSlotPlayerPrefsKey, currentSlotIndex);
        PlayerPrefs.Save();
    }

    private static void EnsureCurrentSlotIndexLoaded()
    {
        if (currentSlotIndexLoaded)
            return;

        currentSlotIndex = Mathf.Clamp(PlayerPrefs.GetInt(CurrentSlotPlayerPrefsKey, 1), 1, 3);
        currentSlotIndexLoaded = true;
    }

    public static string GetRunSavePath(int slotIndex)
    {
        return Path.Combine(SaveDirectory, $"run_slot_{Mathf.Clamp(slotIndex, 1, 3)}.json");
    }

    public static string GetHistorySavePath(int slotIndex)
    {
        return Path.Combine(SaveDirectory, $"history_slot_{Mathf.Clamp(slotIndex, 1, 3)}.json");
    }

    public static RunSaveData LoadRun(int slotIndex)
    {
        string path = GetRunSavePath(slotIndex);
        if (!File.Exists(path))
            return null;

        string json = File.ReadAllText(path);
        if (string.IsNullOrEmpty(json))
            return null;

        return JsonConvert.DeserializeObject<RunSaveData>(json);
    }

    public static bool HasRun(int slotIndex)
    {
        return File.Exists(GetRunSavePath(slotIndex));
    }

    public static void BeginNewRun(int seed = 0)
    {
        newRunSeed = seed;
        forceNewRun = true;
        startingTutorialRun = false;
        tutorialRunActive = false;
        ClearCurrentRun();
    }

    public static void BeginNewTutorialRun()
    {
        newRunSeed = 0;
        forceNewRun = true;
        startingTutorialRun = true;
        tutorialRunActive = false;
    }

    public static bool ConsumeStartingTutorialRun()
    {
        bool value = startingTutorialRun;
        startingTutorialRun = false;
        tutorialRunActive = value;
        return value;
    }

    public static bool IsTutorialRunActive()
    {
        return tutorialRunActive;
    }

    public static void EndTutorialRun()
    {
        tutorialRunActive = false;
    }

    public static int ConsumeNewRunSeed()
    {
        int seed = newRunSeed;
        newRunSeed = 0;
        return seed;
    }

    public static bool ConsumeForceNewRun()
    {
        bool value = forceNewRun;
        forceNewRun = false;
        return value;
    }

    public static bool HasCurrentRun()
    {
        return File.Exists(RunSavePath);
    }

    public static void RecordVictoryAndClearCurrentRun(float playSeconds = -1f)
    {
        RecordRunEndAndClearCurrentRun(RunHistoryResultType.Victory, null, null, 0, null, null, playSeconds);
    }

    public static void RecordCurrentRunAbandonedAndClearCurrentRun()
    {
        if (!HasCurrentRun())
            return;

        RecordRunEndAndClearCurrentRun(RunHistoryResultType.Abandon, null, null, 0, null, null);
    }

    public static void RecordRunEndAndClearCurrentRun(RunHistoryResultType resultType, PlayerState player, IReadOnlyList<RunMapNodeModel> mapNodes, int currentMapNodeIndex, ChapterData chapter, LevelData currentLevel, float playSeconds = -1f)
    {
        if (tutorialRunActive)
            return;

        RunSaveData data = CreateRunEndSnapshot(player, mapNodes, currentMapNodeIndex, chapter, currentLevel, playSeconds);
        if (data != null)
        {
            AppendHistoryRecord(data, resultType, playSeconds);
            UnlockSystem.ProcessRunEnded(data, resultType);
            if (resultType == RunHistoryResultType.Victory)
                data.victoryCount++;
            if (playSeconds >= 0f)
                data.totalPlaySeconds = playSeconds;
            data.lastPlayedAtUtc = DateTime.UtcNow.ToString("o");
            SaveSummaryOnly(data);
        }
        ClearCurrentRun();
    }

    private static void SaveSummaryOnly(RunSaveData data)
    {
        Directory.CreateDirectory(SaveDirectory);
        string path = Path.Combine(SaveDirectory, $"summary_slot_{CurrentSlotIndex}.json");
        File.WriteAllText(path, JsonConvert.SerializeObject(data, Formatting.Indented));
    }

    public static RunSaveData LoadSummary(int slotIndex)
    {
        RunSaveData run = LoadRun(slotIndex);
        if (run != null)
            return run;
        string path = Path.Combine(SaveDirectory, $"summary_slot_{Mathf.Clamp(slotIndex, 1, 3)}.json");
        if (!File.Exists(path))
            return null;
        return JsonConvert.DeserializeObject<RunSaveData>(File.ReadAllText(path));
    }

    public static RunHistoryData LoadHistory(int slotIndex)
    {
        int clampedSlotIndex = Mathf.Clamp(slotIndex, 1, 3);
        string path = GetHistorySavePath(clampedSlotIndex);
        if (!File.Exists(path))
            return new RunHistoryData { version = HistoryVersion, slotIndex = clampedSlotIndex };

        RunHistoryData history = JsonConvert.DeserializeObject<RunHistoryData>(File.ReadAllText(path));
        if (history == null)
            return new RunHistoryData { version = HistoryVersion, slotIndex = clampedSlotIndex };

        history.version = HistoryVersion;
        history.slotIndex = clampedSlotIndex;
        if (history.records == null)
            history.records = Array.Empty<RunHistoryRecordData>();
        return history;
    }

    public static void ClearHistory(int slotIndex)
    {
        string path = GetHistorySavePath(slotIndex);
        if (File.Exists(path))
            File.Delete(path);
    }

    public static void ClearSlot(int slotIndex)
    {
        if (clearCodexSlot == null) throw new InvalidOperationException("Codex slot persistence host is not configured.");
        int clampedSlotIndex = Mathf.Clamp(slotIndex, 1, 3);
        string runPath = GetRunSavePath(clampedSlotIndex);
        if (File.Exists(runPath))
            File.Delete(runPath);

        string summaryPath = Path.Combine(SaveDirectory, $"summary_slot_{clampedSlotIndex}.json");
        if (File.Exists(summaryPath))
            File.Delete(summaryPath);

        ClearHistory(clampedSlotIndex);
        UnlockProgressSaveSystem.Clear(clampedSlotIndex);
        clearCodexSlot(clampedSlotIndex);
    }

    /// <summary>
    /// 埋点台账生成的 runId：全新一局第一次存盘时沿用，保证 run_start 与后续事件、历史记录里的 run_id 一致。
    /// 只在“还没有存档数据”（全新一局）时生效；清档时一并清空。
    /// </summary>
    private static string adoptedRunId;

    /// <summary>由 RunAnalyticsLedger.BeginRun 调用：让本局存档沿用埋点侧生成的 run_id。</summary>
    public static void AdoptRunId(string runId)
    {
        adoptedRunId = runId ?? string.Empty;
    }

    public static void ClearCurrentRun()
    {
        adoptedRunId = string.Empty;
        if (File.Exists(RunSavePath))
            File.Delete(RunSavePath);
    }

    public static RunSaveData LoadCurrentRun()
    {
        return LoadRun(CurrentSlotIndex);
    }


    public static RunSaveData ExportCurrentRun(PlayerState player, RunManager run,
        IReadOnlyList<RunMapNodeModel> mapNodes = null, int currentMapNodeIndex = 0, ChapterData chapter = null,
        LevelData currentLevel = null, BattleManager battleManager = null, EventModel currentEvent = null,
        string sceneName = null)
    {
        return RuleRunSnapshot.ExportCurrentRun(player, run, mapNodes, currentMapNodeIndex, chapter, currentLevel, battleManager, currentEvent, sceneName);
    }

    public static RunMemorySnapshot ExportSnapshot(PlayerState player, RunManager run,
        IReadOnlyList<RunMapNodeModel> mapNodes = null, int currentMapNodeIndex = 0, ChapterData chapter = null,
        LevelData currentLevel = null, BattleManager battleManager = null, EventModel currentEvent = null,
        BattleCommandSession commands = null)
    {
        return RuleRunSnapshot.ExportSnapshot(player, run, mapNodes, currentMapNodeIndex, chapter, currentLevel, battleManager, currentEvent, commands);
    }

    public static PlayerStatus RestoreSnapshot(RunMemorySnapshot snapshot, PlayerStateServices services = null,
        RunDifficultySaveData difficulty = null, IRuleContentProvider content = null, RuleProfilePolicy profilePolicy = null)
    {
        return RuleRunSnapshot.RestoreSnapshot(snapshot, services, difficulty, content, profilePolicy);
    }

    public static BattleManager RestoreSnapshotBattle(RunMemorySnapshot snapshot, PlayerState player)
    {
        return RuleRunSnapshot.RestoreSnapshotBattle(snapshot, player);
    }

    public static BattleCommandSession RestoreSnapshotCommands(RunMemorySnapshot snapshot, PlayerState player,
        BattleManager battle = null, TutorialPolicy tutorial = null)
    {
        return RuleRunSnapshot.RestoreSnapshotCommands(snapshot, player, battle, tutorial);
    }

    private static T CopyDto<T>(T value) where T : class
    {
        return RuleRunSnapshot.CopyDto<T>(value);
    }

    public static PlayerState CreatePlayer(RunSaveData save, PlayerStateServices services = null, RunDifficultySaveData difficulty = null,
        IRuleContentProvider content = null, RuleProfilePolicy profilePolicy = null)
    {
        return RuleRunSnapshot.CreatePlayer(save, services, difficulty, content, profilePolicy);
    }

    public static MagicModel CreateMagic(MagicSlotSaveData slot)
    {
        return RuleRunSnapshot.CreateMagic(slot);
    }

    public static MaterialModel CreateMaterialCard(MaterialCardSaveData data)
    {
        return RuleRunSnapshot.CreateMaterialCard(data);
    }

    public static PlayerStatus CreatePlayerStatus(RunSaveData save, PlayerStateServices services = null,
        RunDifficultySaveData difficulty = null, bool restoreSavedPlayLimit = false,
        IRuleContentProvider content = null, RuleProfilePolicy profilePolicy = null)
    {
        return RuleRunSnapshot.CreatePlayerStatus(save, services, difficulty, restoreSavedPlayLimit, content, profilePolicy);
    }

    public static void RestoreMapNodes(RunSaveData save, List<RunMapNodeModel> target)
    {
        RuleRunSnapshot.RestoreMapNodes(save, target);
    }

    public static RunMapGridModel RestoreMapGrid(RunSaveData save)
    {
        return RuleRunSnapshot.RestoreMapGrid(save);
    }

    public static bool ShouldAutoStartSavedNode(RunSaveData save)
    {
        return RuleRunSnapshot.ShouldAutoStartSavedNode(save);
    }

    public static LevelData GetSavedCurrentLevel(RunSaveData save)
    {
        return RuleRunSnapshot.GetSavedCurrentLevel(save);
    }

    public static BattleNodeSaveData GetSavedBattle(RunSaveData save)
    {
        return RuleRunSnapshot.GetSavedBattle(save);
    }

    public static ShopNodeSaveData GetSavedShop(RunSaveData save, LevelData level)
    {
        return RuleRunSnapshot.GetSavedShop(save, level);
    }

    public static EventNodeSaveData GetSavedEvent(RunSaveData save, LevelData level)
    {
        return RuleRunSnapshot.GetSavedEvent(save, level);
    }

    public static void RestoreBattle(BattleNodeSaveData data, BattleManager battleManager, PlayerState player)
    {
        RuleRunSnapshot.RestoreBattle(data, battleManager, player);
    }

    private static CurrentNodeSaveData ExportCurrentNode(LevelData currentLevel, PlayerState player, BattleManager battleManager, EventModel currentEvent, PlayerSaveData initialPlayer)
    {
        return RuleRunSnapshot.ExportCurrentNode(currentLevel, player, battleManager, currentEvent, initialPlayer);
    }

    private static RunMapNodeSaveData[] ExportMapNodes(IReadOnlyList<RunMapNodeModel> mapNodes)
    {
        return RuleRunSnapshot.ExportMapNodes(mapNodes);
    }

    private static RunMapGridSaveData ExportMapGrid(RunMapGridModel grid)
    {
        return RuleRunSnapshot.ExportMapGrid(grid);
    }

    private static PlayerSaveData ExportPlayer(PlayerState player)
    {
        return RuleRunSnapshot.ExportPlayer(player);
    }

    private static LevelData GetLevel(int id)
    {
        return RuleRunSnapshot.GetLevel(id);
    }

    public static bool SaveCurrentRun(PlayerState player, IReadOnlyList<RunMapNodeModel> mapNodes, int currentMapNodeIndex, ChapterData chapter, LevelData currentLevel, float playSeconds = -1f, BattleManager battleManager = null, EventModel currentEvent = null, string sceneName = null)
    {
        try
        {
            return SaveCurrentRunCore(player, mapNodes, currentMapNodeIndex, chapter, currentLevel, playSeconds, battleManager, currentEvent, sceneName);
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is JsonException)
        {
            Debug.LogError($"Run save was not committed: {e.Message}");
            return false;
        }
    }

    private static bool SaveCurrentRunCore(PlayerState player, IReadOnlyList<RunMapNodeModel> mapNodes, int currentMapNodeIndex, ChapterData chapter, LevelData currentLevel, float playSeconds, BattleManager battleManager, EventModel currentEvent, string sceneName)
    {
        if (tutorialRunActive || player == null || !player.Relics.IsSettled || mapNodes == null || mapNodes.Count == 0)
            return false;

        RunSaveData currentData = LoadCurrentRun();
        RunSaveData previousData = currentData ?? LoadSummary(CurrentSlotIndex);
        string now = DateTime.UtcNow.ToString("o");
        RunSaveData data = ExportCurrentRun(player, RunManager.Current, mapNodes, currentMapNodeIndex,
            chapter, currentLevel, battleManager, currentEvent, sceneName);
        data.slotIndex = CurrentSlotIndex;
        data.runId = currentData != null && !string.IsNullOrEmpty(currentData.runId)
            ? currentData.runId : (string.IsNullOrEmpty(adoptedRunId) ? Guid.NewGuid().ToString("N") : adoptedRunId);
        data.createdAtUtc = currentData != null && !string.IsNullOrEmpty(currentData.createdAtUtc) ? currentData.createdAtUtc : now;
        data.lastSavedAtUtc = data.lastPlayedAtUtc = now;
        data.victoryCount = previousData != null ? previousData.victoryCount : 0;
        data.tutorialCompleted = previousData != null && previousData.tutorialCompleted;
        data.tutorialEventShown = previousData != null && previousData.tutorialEventShown;
        data.totalPlaySeconds = playSeconds >= 0f ? playSeconds : (previousData != null ? previousData.totalPlaySeconds : 0f);
        data.startConfigId = currentData != null && !string.IsNullOrEmpty(currentData.startConfigId) ? currentData.startConfigId : PlayerState.SelectedStartConfigId;
        if (data.runPools == null && previousData != null) data.runPools = CopyDto(previousData.runPools);

        Directory.CreateDirectory(SaveDirectory);
        string tempPath = RunSavePath + ".tmp";
        File.WriteAllText(tempPath, JsonConvert.SerializeObject(data, Formatting.Indented));
        if (File.Exists(RunSavePath))
            File.Replace(tempPath, RunSavePath, null);
        else
            File.Move(tempPath, RunSavePath);
        GameLog.Data($"Save run state={data.runState} node={currentMapNodeIndex + 1}");
        return true;
    }

    private static RunSaveData CreateEmptySlotData()
    {
        string now = DateTime.UtcNow.ToString("o");
        return new RunSaveData
        {
            version = CurrentVersion,
            slotIndex = CurrentSlotIndex,
            runId = Guid.NewGuid().ToString("N"),
            createdAtUtc = now,
            lastSavedAtUtc = now,
            lastPlayedAtUtc = now,
            runState = MapSelectionState,
            startConfigId = PlayerState.SelectedStartConfigId,
            difficulty = DifficultyUpgradeSystem.ExportCurrentState()
        };
    }

    private static RunSaveData CreateRunEndSnapshot(PlayerState player, IReadOnlyList<RunMapNodeModel> mapNodes, int currentMapNodeIndex, ChapterData chapter, LevelData currentLevel, float playSeconds)
    {
        RunSaveData currentData = LoadCurrentRun();
        RunSaveData previousData = currentData ?? LoadSummary(CurrentSlotIndex);
        if (player == null && currentData == null)
            return null;

        string now = DateTime.UtcNow.ToString("o");
        RunSaveData data = new RunSaveData
        {
            version = CurrentVersion,
            slotIndex = CurrentSlotIndex,
            runId = currentData != null && !string.IsNullOrEmpty(currentData.runId)
                ? currentData.runId
                : (string.IsNullOrEmpty(adoptedRunId) ? Guid.NewGuid().ToString("N") : adoptedRunId),
            createdAtUtc = currentData != null && !string.IsNullOrEmpty(currentData.createdAtUtc) ? currentData.createdAtUtc : now,
            lastSavedAtUtc = now,
            lastPlayedAtUtc = now,
            victoryCount = previousData != null ? previousData.victoryCount : 0,
            tutorialCompleted = previousData != null && previousData.tutorialCompleted,
            tutorialEventShown = previousData != null && previousData.tutorialEventShown,
            totalPlaySeconds = playSeconds >= 0f ? playSeconds : (previousData != null ? previousData.totalPlaySeconds : 0f),
            startConfigId = currentData != null && !string.IsNullOrEmpty(currentData.startConfigId) ? currentData.startConfigId : PlayerState.SelectedStartConfigId,
            runState = currentLevel != null ? BeforeNodeState : MapSelectionState,
            chapterNumericId = chapter != null ? chapter.numericId : (previousData != null ? previousData.chapterNumericId : 0),
            currentMapNodeIndex = mapNodes != null && mapNodes.Count > 0 ? currentMapNodeIndex : (previousData != null ? previousData.currentMapNodeIndex : 0),
            mapNodes = mapNodes != null && mapNodes.Count > 0 ? ExportMapNodes(mapNodes) : (previousData != null ? previousData.mapNodes : Array.Empty<RunMapNodeSaveData>()),
            mapGrid = RunManager.Current != null ? ExportMapGrid(RunManager.Current.MapGrid) : previousData != null ? previousData.mapGrid : null,
            runPools = RunManager.Current != null ? RunManager.Current.ExportPoolState() : previousData != null ? previousData.runPools : null,
            difficulty = currentData != null && currentData.difficulty != null ? currentData.difficulty : DifficultyUpgradeSystem.ExportCurrentState(),
            player = player != null ? ExportPlayer(player) : previousData != null ? previousData.player : null,
            currentNode = currentLevel != null ? ExportCurrentNode(currentLevel, player, null, null, null) : previousData != null ? previousData.currentNode : null
        };
        return data;
    }

    private static void AppendHistoryRecord(RunSaveData data, RunHistoryResultType resultType, float playSeconds)
    {
        if (data == null)
            return;

        RunHistoryData history = LoadHistory(CurrentSlotIndex);
        RunHistoryRecordData record = CreateHistoryRecord(data, resultType, playSeconds);
        List<RunHistoryRecordData> records = new List<RunHistoryRecordData>();
        records.Add(record);
        for (int i = 0; history.records != null && i < history.records.Length && records.Count < MaxHistoryRecords; i++)
        {
            if (history.records[i] != null)
                records.Add(history.records[i]);
        }

        history.records = records.ToArray();
        Directory.CreateDirectory(SaveDirectory);
        File.WriteAllText(GetHistorySavePath(CurrentSlotIndex), JsonConvert.SerializeObject(history, Formatting.Indented));
    }

    private static RunHistoryRecordData CreateHistoryRecord(RunSaveData data, RunHistoryResultType resultType, float playSeconds)
    {
        PlayerSaveData player = data.player;
        int totalNodeCount = data.mapNodes != null ? data.mapNodes.Length : 0;
        int progressNode = totalNodeCount > 0 ? Mathf.Clamp(data.currentMapNodeIndex + 1, 1, totalNodeCount) : 0;
        string resultText = resultType.ToString();
        return new RunHistoryRecordData
        {
            historyVersion = HistoryVersion,
            gameVersion = Application.version,
            runId = data.runId,
            resultType = resultText,
            startConfigId = data.startConfigId,
            endedAtUtc = DateTime.UtcNow.ToString("o"),
            playSeconds = playSeconds >= 0f ? playSeconds : data.totalPlaySeconds,
            chapterNumericId = data.chapterNumericId,
            currentMapNodeIndex = data.currentMapNodeIndex,
            totalMapNodeCount = totalNodeCount,
            currentLevelNumericId = data.currentNode != null ? data.currentNode.levelId : 0,
            progressText = BuildProgressText(data.chapterNumericId, progressNode, totalNodeCount, data.currentNode != null ? data.currentNode.levelId : 0),
            ascensionLevel = data.difficulty != null ? data.difficulty.ascensionLevel : 0,
            maxHealth = player != null ? player.maxHealth : 0,
            currentHealth = player != null ? player.currentHealth : 0,
            gold = player != null ? player.gold : 0,
            buildSummary = BuildHistoryBuildSummary(player),
            magicBook = player != null && player.magicBook != null ? player.magicBook : Array.Empty<MagicSlotSaveData>(),
            deck = player != null && player.deck != null ? player.deck : Array.Empty<MaterialCardSaveData>()
        };
    }

    private static string BuildProgressText(int chapterNumericId, int progressNode, int totalNodeCount, int currentLevelId)
    {
        string chapterText = chapterNumericId > 0 ? $"章节 {chapterNumericId}" : "未知章节";
        string nodeText = totalNodeCount > 0 ? $"{progressNode}/{totalNodeCount}" : "未知进度";
        LevelData level = GetLevel(currentLevelId);
        string levelText = level != null ? LocalizationSystem.GetText(level.titleKey, LevelTypeText.GetName(level.levelType)) : string.Empty;
        return string.IsNullOrEmpty(levelText) ? $"{chapterText} · {nodeText}" : $"{chapterText} · {nodeText} · {levelText}";
    }

    private static string BuildHistoryBuildSummary(PlayerSaveData player)
    {
        if (player == null)
            return string.Empty;

        int magicCount = 0;
        for (int i = 0; player.magicBook != null && i < player.magicBook.Length; i++)
        {
            if (player.magicBook[i] != null && player.magicBook[i].magicNumericId > 0)
                magicCount++;
        }

        int arrowCount = player.deck != null ? player.deck.Length : 0;
        return $"道具 {magicCount} / 箭头 {arrowCount}";
    }

}
