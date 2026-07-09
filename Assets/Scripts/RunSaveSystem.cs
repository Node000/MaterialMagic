using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public enum RunHistoryResultType
{
    Victory,
    Defeat,
    Abandon
}

[Serializable]
public class RunHistoryData
{
    public int version = 1;
    public int slotIndex = 1;
    public RunHistoryRecordData[] records = Array.Empty<RunHistoryRecordData>();
}

[Serializable]
public class RunHistoryRecordData
{
    public int historyVersion = 1;
    public string gameVersion;
    public string runId;
    public string resultType;
    public string endedAtUtc;
    public float playSeconds;
    public int chapterNumericId;
    public int currentMapNodeIndex;
    public int totalMapNodeCount;
    public int currentLevelNumericId;
    public string progressText;
    public int maxHealth;
    public int currentHealth;
    public int gold;
    public string buildSummary;
    public MagicSlotSaveData[] magicBook = Array.Empty<MagicSlotSaveData>();
    public MaterialCardSaveData[] deck = Array.Empty<MaterialCardSaveData>();
}

[Serializable]
public class RunSaveData
{
    public int version = 1;
    public int slotIndex = 1;
    public string runId;
    public string createdAtUtc;
    public string lastSavedAtUtc;
    public string startConfigId;
    public string runState;
    public int chapterNumericId;
    public int currentMapNodeIndex;
    public int victoryCount;
    public bool tutorialCompleted;
    public bool tutorialEventShown;
    public float totalPlaySeconds;
    public string lastPlayedAtUtc;
    public RunMapNodeSaveData[] mapNodes = Array.Empty<RunMapNodeSaveData>();
    public RunMapGridSaveData mapGrid;
    public RunPoolSaveData runPools;
    public RunDifficultySaveData difficulty;
    public PlayerSaveData player;
    public CurrentNodeSaveData currentNode;
}

[Serializable]
public class RunMapNodeSaveData
{
    public int leftLevelId;
    public int rightLevelId;
    public int selectedLevelId;
    public bool fixedSingleChoice;
    public bool leftHidden;
    public bool rightHidden;
}

[Serializable]
public class RunMapGridSaveData
{
    public int width;
    public int height;
    public int playerX;
    public int playerY;
    public bool bossMapActive;
    public RunMapCellSaveData[] cells = Array.Empty<RunMapCellSaveData>();
}

[Serializable]
public class RunMapCellSaveData
{
    public int x;
    public int y;
    public int levelId;
    public bool isBoss;
    public bool isAvailable = true;
    public bool isRevealed;
}

[Serializable]
public class RunPoolSaveData
{
    public int battleCount;
    public int[] remainingBeginPool = Array.Empty<int>();
    public int[] remainingMidPool = Array.Empty<int>();
    public int[] remainingNormalPool = Array.Empty<int>();
    public int[] remainingEventPool = Array.Empty<int>();
    public int[] remainingElitePool = Array.Empty<int>();
}

[Serializable]
public class CurrentNodeSaveData
{
    public int levelId;
    public PlayerSaveData initialPlayer;
    public BattleNodeSaveData battle;
    public ShopNodeSaveData shop;
    public EventNodeSaveData eventState;
}

[Serializable]
public class BattleNodeSaveData
{
    public int levelId;
    public int phase;
    public int continuousCastCount;
    public EnemyBattleSaveData[] enemies = Array.Empty<EnemyBattleSaveData>();
    public PlayerCombatSaveData playerCombat;
}

[Serializable]
    public class EnemyBattleSaveData
    {
        public int enemyId;
        public int currentHealth;
        public int shield;
        public int actionIndex;
        public int phase;
        public bool deathHandled;
        public bool canActThisEnemyTurn = true;
        public bool isMinion;
        public bool hasSpawnPosition;
        public float spawnPositionX;
        public float spawnPositionY;
        public BuffStackData[] buffs = Array.Empty<BuffStackData>();
        public int[] consumedOnlyOnceIntentIds = Array.Empty<int>();
        public int lastResolvedIntentGroupId = -1;
        public int selectedIntentPhase = -1;
        public int selectedIntentActionIndex = -1;
        public int selectedIntentGroupId;
    }


[Serializable]
public class PlayerCombatSaveData
{
    public int shield;
    public int extraRefreshChancesThisTurn;
    public MaterialCardSaveData[] hand = Array.Empty<MaterialCardSaveData>();
    public MaterialCardSaveData[] drawPile = Array.Empty<MaterialCardSaveData>();
    public MaterialCardSaveData[] discardPile = Array.Empty<MaterialCardSaveData>();
    public MaterialCardSaveData[] playZone = Array.Empty<MaterialCardSaveData>();
    public MaterialCardSaveData[] consumedPile = Array.Empty<MaterialCardSaveData>();
    public MaterialCardSaveData[] temporaryMaterialsNextTurn = Array.Empty<MaterialCardSaveData>();
}

[Serializable]
public class EventNodeSaveData
{
    public int levelId;
    public int eventNumericId;
    public string eventId;
    public string currentNodeId;
    public EventOptionRecipeSaveData[] optionRecipes = Array.Empty<EventOptionRecipeSaveData>();
    public EventOptionResolveCountSaveData[] optionResolveCounts = Array.Empty<EventOptionResolveCountSaveData>();
}

[Serializable]
public class EventOptionRecipeSaveData
{
    public string nodeId;
    public string optionId;
    public string recipe;
}

[Serializable]
public class EventOptionResolveCountSaveData
{
    public string optionId;
    public int count;
}

[Serializable]
public class ShopNodeSaveData
{
    public ShopOfferSaveData[] offers = Array.Empty<ShopOfferSaveData>();
    public int selectedOfferIndex = -1;
    public bool waitingForSelection;
    public bool purchaseInProgress;
    public ShopUndoSaveData undo;
}

[Serializable]
public class ShopOfferSaveData
{
    public int kind;
    public int price;
    public int magicNumericId;
    public int material;
    public string materialModifierId;
    public bool purchased;
}

[Serializable]
public class ShopUndoSaveData
{
    public int offerIndex = -1;
    public int gold;
    public int magicSlotIndex = -1;
    public int previousMagicNumericId;
    public string previousMagicModifierId;
    public MaterialCardSaveData addedMaterial;
    public MaterialCardSaveData removedMaterial;
}

[Serializable]
public class PlayerSaveData
{
    public int maxHealth;
    public int currentHealth;
    public int gold;
    public int drawCount;
    public int maxPlayCount;
    public BuffStackData[] preparedBuffs = Array.Empty<BuffStackData>();
    public int runRandomSeed;
    public int runRandomStep;
    public MaterialCardSaveData[] deck = Array.Empty<MaterialCardSaveData>();
    public MagicSlotSaveData[] magicBook = Array.Empty<MagicSlotSaveData>();
}

[Serializable]
public class MaterialCardSaveData
{
    public string instanceId;
    public int material;
    public int alternateMaterial;
    public string[] enhancementIds = Array.Empty<string>();
    public string[] modifierIds = Array.Empty<string>();
    public MaterialCardSaveData[] linkedCards = Array.Empty<MaterialCardSaveData>();
    public bool isTemporary;
    public bool isRetained;
}

[Serializable]
public class MagicSlotSaveData
{
    public int slotIndex;
    public int magicNumericId;
    public string modifierId;
}

public static class RunSaveSystem
{
    private const int CurrentVersion = 3;
    private const int HistoryVersion = 1;
    private const int MaxHistoryRecords = 100;
    private const string CurrentSlotPlayerPrefsKey = "RunSaveSystem.CurrentSlotIndex";
    private const string MapSelectionState = "MapSelection";
    private const string BeforeNodeState = "BeforeNode";
    private static bool forceNewRun;
    private static bool startingTutorialRun;
    private static int currentSlotIndex = 1;
    private static bool currentSlotIndexLoaded;

    private static string SaveDirectory => Path.Combine(Application.persistentDataPath, "Save");
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

        return JsonUtility.FromJson<RunSaveData>(json);
    }

    public static bool HasRun(int slotIndex)
    {
        return File.Exists(GetRunSavePath(slotIndex));
    }

    public static void BeginNewRun()
    {
        forceNewRun = true;
        startingTutorialRun = false;
        ClearCurrentRun();
    }

    public static void BeginNewTutorialRun()
    {
        forceNewRun = true;
        startingTutorialRun = true;
        ClearCurrentRun();
    }

    public static bool ConsumeStartingTutorialRun()
    {
        bool value = startingTutorialRun;
        startingTutorialRun = false;
        return value;
    }

    public static bool IsTutorialCompleted()
    {
        RunSaveData data = LoadSummary(CurrentSlotIndex);
        return data != null && data.tutorialCompleted;
    }

    public static bool ShouldShowTutorialEntry()
    {
        RunSaveData data = LoadSummary(CurrentSlotIndex);
        return data == null || !data.tutorialCompleted;
    }

    public static bool IsTutorialEventShown()
    {
        RunSaveData data = LoadSummary(CurrentSlotIndex);
        return data != null && data.tutorialEventShown;
    }

    public static void SetTutorialCompleted(bool completed)
    {
        RunSaveData data = LoadSummary(CurrentSlotIndex) ?? CreateEmptySlotData();
        data.tutorialCompleted = completed;
        if (!completed)
            data.tutorialEventShown = false;
        SaveSummaryOnly(data);
    }

    public static void SetTutorialEventShown(bool shown)
    {
        RunSaveData data = LoadSummary(CurrentSlotIndex) ?? CreateEmptySlotData();
        data.tutorialEventShown = shown;
        SaveSummaryOnly(data);
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
        File.WriteAllText(path, JsonUtility.ToJson(data, true));
    }

    public static RunSaveData LoadSummary(int slotIndex)
    {
        RunSaveData run = LoadRun(slotIndex);
        if (run != null)
            return run;
        string path = Path.Combine(SaveDirectory, $"summary_slot_{Mathf.Clamp(slotIndex, 1, 3)}.json");
        if (!File.Exists(path))
            return null;
        return JsonUtility.FromJson<RunSaveData>(File.ReadAllText(path));
    }

    public static RunHistoryData LoadHistory(int slotIndex)
    {
        int clampedSlotIndex = Mathf.Clamp(slotIndex, 1, 3);
        string path = GetHistorySavePath(clampedSlotIndex);
        if (!File.Exists(path))
            return new RunHistoryData { version = HistoryVersion, slotIndex = clampedSlotIndex };

        RunHistoryData history = JsonUtility.FromJson<RunHistoryData>(File.ReadAllText(path));
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
        int clampedSlotIndex = Mathf.Clamp(slotIndex, 1, 3);
        string runPath = GetRunSavePath(clampedSlotIndex);
        if (File.Exists(runPath))
            File.Delete(runPath);

        string summaryPath = Path.Combine(SaveDirectory, $"summary_slot_{clampedSlotIndex}.json");
        if (File.Exists(summaryPath))
            File.Delete(summaryPath);

        ClearHistory(clampedSlotIndex);
        UnlockProgressSaveSystem.Clear(clampedSlotIndex);
    }

    public static void ClearCurrentRun()
    {
        if (File.Exists(RunSavePath))
            File.Delete(RunSavePath);
    }

    public static RunSaveData LoadCurrentRun()
    {
        return LoadRun(CurrentSlotIndex);
    }

    public static void SaveCurrentRun(PlayerState player, IReadOnlyList<RunMapNodeModel> mapNodes, int currentMapNodeIndex, ChapterData chapter, LevelData currentLevel, float playSeconds = -1f, BattleManager battleManager = null, EventModel currentEvent = null)
    {
        if (player == null || mapNodes == null || mapNodes.Count == 0)
            return;

        RunSaveData currentData = LoadCurrentRun();
        RunSaveData previousData = currentData ?? LoadSummary(CurrentSlotIndex);
        string now = DateTime.UtcNow.ToString("o");
        RunSaveData data = new RunSaveData
        {
            version = CurrentVersion,
            slotIndex = CurrentSlotIndex,
            runId = currentData != null && !string.IsNullOrEmpty(currentData.runId) ? currentData.runId : Guid.NewGuid().ToString("N"),
            createdAtUtc = currentData != null && !string.IsNullOrEmpty(currentData.createdAtUtc) ? currentData.createdAtUtc : now,
            lastSavedAtUtc = now,
            lastPlayedAtUtc = now,
            victoryCount = previousData != null ? previousData.victoryCount : 0,
            tutorialCompleted = previousData != null && previousData.tutorialCompleted,
            tutorialEventShown = previousData != null && previousData.tutorialEventShown,
            totalPlaySeconds = playSeconds >= 0f ? playSeconds : (previousData != null ? previousData.totalPlaySeconds : 0f),
            startConfigId = currentData != null && !string.IsNullOrEmpty(currentData.startConfigId) ? currentData.startConfigId : PlayerState.SelectedStartConfigId,
            runState = currentLevel != null ? BeforeNodeState : MapSelectionState,
            chapterNumericId = chapter != null ? chapter.numericId : 0,
            currentMapNodeIndex = currentMapNodeIndex,
            mapNodes = ExportMapNodes(mapNodes),
            mapGrid = ExportMapGrid(RunManager.Current != null ? RunManager.Current.MapGrid : null),
            runPools = RunManager.Current != null ? RunManager.Current.ExportPoolState() : previousData != null ? previousData.runPools : null,
            difficulty = DifficultyUpgradeSystem.ExportCurrentState(),
            player = ExportPlayer(player),
            currentNode = currentLevel != null ? ExportCurrentNode(currentLevel, player, battleManager, currentEvent, null) : null
        };

        Directory.CreateDirectory(SaveDirectory);
        string tempPath = RunSavePath + ".tmp";
        File.WriteAllText(tempPath, JsonUtility.ToJson(data, true));
        if (File.Exists(RunSavePath))
            File.Delete(RunSavePath);
        File.Move(tempPath, RunSavePath);
        GameLog.Data($"Save run state={data.runState} node={currentMapNodeIndex + 1}");
    }

    private static bool HasCurrentNodeSnapshot(CurrentNodeSaveData node)
    {
        return node != null && (HasBattleSnapshot(node.battle) || node.shop != null || node.eventState != null);
    }

    private static bool HasBattleSnapshot(BattleNodeSaveData battle)
    {
        if (battle == null || battle.enemies == null)
            return false;

        for (int i = 0; i < battle.enemies.Length; i++)
        {
            EnemyBattleSaveData enemy = battle.enemies[i];
            if (enemy != null && enemy.enemyId > 0)
                return true;
        }
        return false;
    }

    public static PlayerState CreatePlayer(RunSaveData save)
    {
        return CreatePlayerStatus(save);
    }

    public static MagicModel CreateMagic(MagicSlotSaveData slot)
    {
        if (slot == null || !GameDataDatabase.TryGetMagicData(slot.magicNumericId, out MagicData data))
            return null;

        MagicModel magic = MagicFactory.Create(data, slot.slotIndex);
        if (!string.IsNullOrEmpty(slot.modifierId) && GameDataDatabase.TryGetMagicModifierData(slot.modifierId, out MagicModifierData modifierData))
            magic.AddModifier(MagicModifierFactory.Create(modifierData));
        return magic;
    }

    public static MaterialModel CreateMaterialCard(MaterialCardSaveData data)
    {
        return CreateMaterial(data);
    }

    public static PlayerStatus CreatePlayerStatus(RunSaveData save)
    {
        PlayerSaveData playerData = save != null ? save.player : null;
        if (playerData == null)
            return PlayerStatus.CreateDefaultStatus();

        PlayerStatus player = new PlayerStatus(playerData.maxHealth, playerData.gold);
        if (playerData.currentHealth < playerData.maxHealth)
            player.TakeDirectDamage(playerData.maxHealth - playerData.currentHealth);
        player.DrawCount = playerData.drawCount;
        player.MaxPlayCount = playerData.maxPlayCount;
        ApplyPreparedBuffs(player, playerData.preparedBuffs);
        player.SetRunRandomState(playerData.runRandomSeed, playerData.runRandomStep);
        player.Deck.Clear();
        player.DrawPile.Clear();
        player.DiscardPile.Clear();
        player.ConsumedPile.Clear();
        player.Hand.Clear();
        player.PlayZone.Clear();
        player.MagicBook.Clear();

        for (int i = 0; playerData.deck != null && i < playerData.deck.Length; i++)
        {
            MaterialModel card = CreateMaterial(playerData.deck[i]);
            if (card != null)
                player.Deck.Add(card);
        }

        player.DrawPile.AddRange(player.Deck);

        for (int i = 0; playerData.magicBook != null && i < playerData.magicBook.Length; i++)
        {
            MagicSlotSaveData slot = playerData.magicBook[i];
            MagicModel magic = CreateMagic(slot);
            if (magic != null)
                player.SetMagicAtSlot(magic, slot.slotIndex);
        }

        return player;
    }

    public static void RestoreMapNodes(RunSaveData save, List<RunMapNodeModel> target)
    {
        target.Clear();
        for (int i = 0; save != null && save.mapNodes != null && i < save.mapNodes.Length; i++)
        {
            RunMapNodeSaveData nodeData = save.mapNodes[i];
            RunMapNodeModel node = new RunMapNodeModel
            {
                fixedSingleChoice = nodeData.fixedSingleChoice,
                leftHidden = nodeData.leftHidden,
                rightHidden = nodeData.rightHidden,
                leftLevel = GetLevel(nodeData.leftLevelId),
                rightLevel = GetLevel(nodeData.rightLevelId),
                selectedLevel = GetLevel(nodeData.selectedLevelId)
            };
            target.Add(node);
        }
    }

    public static RunMapGridModel RestoreMapGrid(RunSaveData save)
    {
        RunMapGridSaveData data = save != null ? save.mapGrid : null;
        if (data == null || data.width <= 0 || data.height <= 0 || data.cells == null || data.cells.Length == 0)
            return null;

        bool legacyGridAvailability = save == null || save.version < 2;
        bool legacyGridReveal = save == null || save.version < 3;
        RunMapGridModel grid = new RunMapGridModel
        {
            width = data.width,
            height = data.height,
            playerX = data.playerX,
            playerY = data.playerY,
            bossMapActive = data.bossMapActive
        };

        for (int i = 0; i < data.cells.Length; i++)
        {
            RunMapCellSaveData cellData = data.cells[i];
            if (cellData == null)
                continue;

            grid.cells.Add(new RunMapCellModel
            {
                x = cellData.x,
                y = cellData.y,
                level = GetLevel(cellData.levelId),
                isBoss = cellData.isBoss,
                isAvailable = legacyGridAvailability || cellData.isAvailable || cellData.isBoss || cellData.levelId > 0 || (cellData.x == data.playerX && cellData.y == data.playerY),
                isRevealed = legacyGridReveal ? cellData.isBoss || (cellData.x == data.playerX && cellData.y == data.playerY) : cellData.isRevealed
            });
        }
        grid.ClampPosition(ref grid.playerX, ref grid.playerY);
        return grid;
    }

    public static bool ShouldAutoStartSavedNode(RunSaveData save)
    {
        return save != null && save.runState == BeforeNodeState && save.currentNode != null && save.currentNode.levelId > 0;
    }

    public static LevelData GetSavedCurrentLevel(RunSaveData save)
    {
        return save != null && save.currentNode != null ? GetLevel(save.currentNode.levelId) : null;
    }

    public static BattleNodeSaveData GetSavedBattle(RunSaveData save)
    {
        BattleNodeSaveData battle = save != null && save.currentNode != null ? save.currentNode.battle : null;
        return HasBattleSnapshot(battle) ? battle : null;
    }

    public static ShopNodeSaveData GetSavedShop(RunSaveData save, LevelData level)
    {
        return save != null && save.currentNode != null && level != null && save.currentNode.levelId == level.numericId ? save.currentNode.shop : null;
    }

    public static EventNodeSaveData GetSavedEvent(RunSaveData save, LevelData level)
    {
        return save != null && save.currentNode != null && level != null && save.currentNode.levelId == level.numericId ? save.currentNode.eventState : null;
    }

    public static void RestoreBattle(BattleNodeSaveData data, BattleManager battleManager, PlayerState player)
    {
        if (data == null || battleManager == null)
            return;

        battleManager.ClearEnemies();
        for (int i = 0; data.enemies != null && i < data.enemies.Length; i++)
        {
            EnemyBattleSaveData enemyData = data.enemies[i];
            if (enemyData == null || !GameDataDatabase.TryGetEnemyData(enemyData.enemyId, out EnemyData baseData))
                continue;

            EnemyModel enemy = EnemyFactory.Create(baseData, battleManager.CreateDifficultyContext());
            if (enemy == null)
                continue;
            enemy.RestoreBattleState(enemyData);
            battleManager.SpawnEnemy(enemy);
        }

        RestorePlayerCombat(data.playerCombat, player);
        battleManager.RestoreBattleState((BattlePhase)data.phase, data.continuousCastCount);
    }

    private static void RestorePlayerCombat(PlayerCombatSaveData data, PlayerState player)
    {
        if (data == null || player == null)
            return;

        Dictionary<string, MaterialModel> deckLookup = BuildDeckLookup(player);
        player.RestoreCombatSnapshot(
            data.shield,
            RestoreCards(data.hand, deckLookup),
            RestoreCards(data.drawPile, deckLookup),
            RestoreCards(data.discardPile, deckLookup),
            RestoreCards(data.playZone, deckLookup),
            RestoreCards(data.consumedPile, deckLookup),
            RestoreCards(data.temporaryMaterialsNextTurn, deckLookup),
            data.extraRefreshChancesThisTurn);
    }

    private static Dictionary<string, MaterialModel> BuildDeckLookup(PlayerState player)
    {
        Dictionary<string, MaterialModel> lookup = new Dictionary<string, MaterialModel>();
        for (int i = 0; player != null && i < player.Deck.Count; i++)
        {
            MaterialModel card = player.Deck[i];
            if (card != null && !string.IsNullOrEmpty(card.instanceId) && !lookup.ContainsKey(card.instanceId))
                lookup.Add(card.instanceId, card);
        }
        return lookup;
    }

    private static List<MaterialModel> RestoreCards(MaterialCardSaveData[] data, Dictionary<string, MaterialModel> deckLookup)
    {
        List<MaterialModel> cards = new List<MaterialModel>();
        for (int i = 0; data != null && i < data.Length; i++)
        {
            MaterialCardSaveData cardData = data[i];
            if (cardData == null)
                continue;

            MaterialModel card = null;
            if (!string.IsNullOrEmpty(cardData.instanceId) && deckLookup != null)
                deckLookup.TryGetValue(cardData.instanceId, out card);
            if (card == null)
                card = CreateMaterial(cardData);
            if (card != null)
                cards.Add(card);
        }
        return cards;
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
            runId = currentData != null && !string.IsNullOrEmpty(currentData.runId) ? currentData.runId : Guid.NewGuid().ToString("N"),
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
        File.WriteAllText(GetHistorySavePath(CurrentSlotIndex), JsonUtility.ToJson(history, true));
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
            endedAtUtc = DateTime.UtcNow.ToString("o"),
            playSeconds = playSeconds >= 0f ? playSeconds : data.totalPlaySeconds,
            chapterNumericId = data.chapterNumericId,
            currentMapNodeIndex = data.currentMapNodeIndex,
            totalMapNodeCount = totalNodeCount,
            currentLevelNumericId = data.currentNode != null ? data.currentNode.levelId : 0,
            progressText = BuildProgressText(data.chapterNumericId, progressNode, totalNodeCount, data.currentNode != null ? data.currentNode.levelId : 0),
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
        string levelText = level != null ? LocalizationSystem.GetText(level.titleKey, UIManager.GetLevelTypeName(level.levelType)) : string.Empty;
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

    private static CurrentNodeSaveData ExportCurrentNode(LevelData currentLevel, PlayerState player, BattleManager battleManager, EventModel currentEvent, PlayerSaveData initialPlayer)
    {
        if (currentLevel == null)
            return null;

        if (player == null)
            return null;

        CurrentNodeSaveData data = new CurrentNodeSaveData
        {
            levelId = currentLevel.numericId,
            initialPlayer = initialPlayer ?? ExportPlayer(player)
        };

        if ((currentLevel.levelType == LevelType.Battle || currentLevel.levelType == LevelType.Elite) && battleManager != null)
        {
            BattleNodeSaveData battle = ExportBattle(currentLevel, battleManager, player);
            if (HasBattleSnapshot(battle))
                data.battle = battle;
        }
        if (currentLevel.levelType == LevelType.Shop && ShopPanelUI.TryExportCurrentState(player, out ShopNodeSaveData shopData))
            data.shop = shopData;
        if (currentEvent != null && IsEventSnapshotLevel(currentLevel.levelType))
            data.eventState = currentEvent.ExportSaveData(currentLevel.numericId);

        return data;
    }

    private static bool IsEventSnapshotLevel(LevelType levelType)
    {
        return levelType == LevelType.Event || levelType == LevelType.Rest || levelType == LevelType.RemoveMaterial || levelType == LevelType.AddMaterial;
    }

    private static BattleNodeSaveData ExportBattle(LevelData currentLevel, BattleManager battleManager, PlayerState player)
    {
        BattleNodeSaveData data = new BattleNodeSaveData
        {
            levelId = currentLevel != null ? currentLevel.numericId : 0,
            phase = (int)battleManager.CurrentPhase,
            continuousCastCount = battleManager.ContinuousCastCount,
            playerCombat = ExportPlayerCombat(player)
        };

        IReadOnlyList<EnemyModel> enemies = battleManager.Enemies;
        data.enemies = new EnemyBattleSaveData[enemies != null ? enemies.Count : 0];
        for (int i = 0; enemies != null && i < enemies.Count; i++)
            data.enemies[i] = ExportEnemy(enemies[i]);
        return data;
    }

    private static EnemyBattleSaveData ExportEnemy(EnemyModel enemy)
    {
        if (enemy == null)
            return null;

        return new EnemyBattleSaveData
        {
            enemyId = enemy.NumericId,
            currentHealth = enemy.CurrentHealth,
            shield = enemy.Shield,
            actionIndex = enemy.ActionIndex,
            phase = enemy.Phase,
            deathHandled = enemy.DeathHandled,
            canActThisEnemyTurn = enemy.CanActThisEnemyTurn,
            isMinion = enemy.IsMinion,
            hasSpawnPosition = enemy.HasSpawnPosition,
            spawnPositionX = enemy.SpawnPositionX,
            spawnPositionY = enemy.SpawnPositionY,
            buffs = ExportBuffs(enemy.Buffs),
            consumedOnlyOnceIntentIds = enemy.ExportConsumedOnlyOnceIntentIds(),
            lastResolvedIntentGroupId = enemy.LastResolvedIntentGroupId,
            selectedIntentPhase = enemy.SelectedIntentPhase,
            selectedIntentActionIndex = enemy.SelectedIntentActionIndex,
            selectedIntentGroupId = enemy.SelectedIntentGroupId
        };
    }

    private static PlayerCombatSaveData ExportPlayerCombat(PlayerState player)
    {
        if (player == null)
            return null;

        return new PlayerCombatSaveData
        {
            shield = player.Shield,
            extraRefreshChancesThisTurn = player.ExtraRefreshChancesThisTurn,
            hand = ExportDeck(player.Hand),
            drawPile = ExportDeck(player.DrawPile),
            discardPile = ExportDeck(player.DiscardPile),
            playZone = ExportDeck(player.PlayZone),
            consumedPile = ExportDeck(player.ConsumedPile),
            temporaryMaterialsNextTurn = ExportDeck(player.TemporaryMaterialsNextTurn)
        };
    }

    private static BuffStackData[] ExportBuffs(IReadOnlyDictionary<BuffEnum, BuffModel> buffs)
    {
        if (buffs == null || buffs.Count == 0)
            return Array.Empty<BuffStackData>();

        List<BuffStackData> results = new List<BuffStackData>();
        foreach (BuffModel buff in buffs.Values)
        {
            if (buff != null && buff.buffType != BuffEnum.None && buff.stack > 0)
                results.Add(new BuffStackData { buffType = buff.buffType, stack = buff.stack });
        }
        return results.ToArray();
    }

    private static RunMapNodeSaveData[] ExportMapNodes(IReadOnlyList<RunMapNodeModel> mapNodes)
    {
        RunMapNodeSaveData[] results = new RunMapNodeSaveData[mapNodes.Count];
        for (int i = 0; i < mapNodes.Count; i++)
        {
            RunMapNodeModel node = mapNodes[i];
            results[i] = new RunMapNodeSaveData
            {
                leftLevelId = node.leftLevel != null ? node.leftLevel.numericId : 0,
                rightLevelId = node.rightLevel != null ? node.rightLevel.numericId : 0,
                selectedLevelId = node.selectedLevel != null ? node.selectedLevel.numericId : 0,
                fixedSingleChoice = node.fixedSingleChoice,
                leftHidden = node.leftHidden,
                rightHidden = node.rightHidden
            };
        }
        return results;
    }

    private static RunMapGridSaveData ExportMapGrid(RunMapGridModel grid)
    {
        if (grid == null || grid.width <= 0 || grid.height <= 0 || grid.cells == null || grid.cells.Count == 0)
            return null;

        RunMapCellSaveData[] cells = new RunMapCellSaveData[grid.cells.Count];
        for (int i = 0; i < grid.cells.Count; i++)
        {
            RunMapCellModel cell = grid.cells[i];
            cells[i] = new RunMapCellSaveData
            {
                x = cell != null ? cell.x : 0,
                y = cell != null ? cell.y : 0,
                levelId = cell != null && cell.level != null ? cell.level.numericId : 0,
                isBoss = cell != null && cell.isBoss,
                isAvailable = cell != null && cell.isAvailable,
                isRevealed = cell != null && cell.isRevealed
            };
        }

        return new RunMapGridSaveData
        {
            width = grid.width,
            height = grid.height,
            playerX = grid.playerX,
            playerY = grid.playerY,
            bossMapActive = grid.bossMapActive,
            cells = cells
        };
    }

    private static PlayerSaveData ExportPlayer(PlayerState player)
    {
        PlayerSaveData data = new PlayerSaveData
        {
            maxHealth = player.MaxHealth,
            currentHealth = player.CurrentHealth,
            gold = player.Gold,
            drawCount = player.DrawCount,
            maxPlayCount = player.MaxPlayCount,
            preparedBuffs = ExportPreparedBuffs(player.Buffs),
            runRandomSeed = player is PlayerStatus status ? status.RunRandomSeed : 0,
            runRandomStep = player is PlayerStatus statusForStep ? statusForStep.RunRandomStep : 0,
            deck = ExportDeck(player.Deck),
            magicBook = ExportMagicBook(player.MagicBook)
        };
        return data;
    }

    private static MaterialCardSaveData[] ExportDeck(IReadOnlyList<MaterialModel> deck)
    {
        MaterialCardSaveData[] results = new MaterialCardSaveData[deck.Count];
        for (int i = 0; i < deck.Count; i++)
            results[i] = ExportMaterialCard(deck[i]);
        return results;
    }

    private static MaterialCardSaveData ExportMaterialCard(MaterialModel card)
    {
        if (card == null)
            return null;

        return new MaterialCardSaveData
        {
            instanceId = card.instanceId,
            material = (int)card.material,
            alternateMaterial = (int)card.alternateMaterial,
            enhancementIds = card.enhancementIds.ToArray(),
            modifierIds = ExportMaterialModifiers(card.modifiers),
            linkedCards = ExportDeck(card.linkedCards),
            isTemporary = card.isTemporary,
            isRetained = card.isRetained
        };
    }

    private static MagicSlotSaveData[] ExportMagicBook(IReadOnlyList<MagicModel> magicBook)
    {
        MagicSlotSaveData[] results = new MagicSlotSaveData[magicBook.Count];
        for (int i = 0; i < magicBook.Count; i++)
        {
            MagicModel magic = magicBook[i];
            results[i] = new MagicSlotSaveData
            {
                slotIndex = magic.SlotIndex,
                magicNumericId = magic.NumericId,
                modifierId = magic.PrimaryModifier != null ? magic.PrimaryModifier.Id : string.Empty
            };
        }
        return results;
    }

    private static BuffStackData[] ExportPreparedBuffs(IReadOnlyDictionary<BuffEnum, BuffModel> buffs)
    {
        if (buffs == null || buffs.Count == 0)
            return Array.Empty<BuffStackData>();

        List<BuffStackData> results = new List<BuffStackData>();
        foreach (BuffModel buff in buffs.Values)
        {
            if (buff != null && buff.buffType != BuffEnum.None && buff.stack > 0 && !buff.IsVisible)
                results.Add(new BuffStackData { buffType = buff.buffType, stack = buff.stack });
        }
        return results.ToArray();
    }

    private static void ApplyPreparedBuffs(PlayerState player, BuffStackData[] buffs)
    {
        if (player == null || buffs == null)
            return;

        for (int i = 0; i < buffs.Length; i++)
        {
            BuffStackData buff = buffs[i];
            if (buff != null)
                player.AddBuff(buff.buffType, buff.stack);
        }
    }

    private static string[] ExportMaterialModifiers(IReadOnlyList<MaterialModifierModel> modifiers)
    {
        List<string> ids = new List<string>();
        for (int i = 0; modifiers != null && i < modifiers.Count; i++)
        {
            string id = GetMaterialModifierId(modifiers[i]);
            if (!string.IsNullOrEmpty(id))
                ids.Add(id);
        }
        return ids.ToArray();
    }

    private static MaterialModel CreateMaterial(MaterialCardSaveData data)
    {
        if (data == null)
            return null;

        MaterialModel card = new MaterialModel(data.instanceId, (MaterialEnum)data.material)
        {
            alternateMaterial = (MaterialEnum)data.alternateMaterial,
            isRetained = data.isRetained
        };
        if (data.enhancementIds != null)
            card.enhancementIds.AddRange(data.enhancementIds);
        for (int i = 0; data.linkedCards != null && i < data.linkedCards.Length; i++)
        {
            MaterialModel linkedCard = CreateMaterial(data.linkedCards[i]);
            if (linkedCard != null)
                card.linkedCards.Add(linkedCard);
        }
        for (int i = 0; data.modifierIds != null && i < data.modifierIds.Length; i++)
        {
            MaterialModifierModel modifier = CreateMaterialModifier(data.modifierIds[i]);
            if (modifier != null)
                card.AddModifier(modifier);
        }
        if (data.isTemporary && !card.isTemporary)
            card.AddModifier(new TemporaryModifier());
        return card;
    }

    private static LevelData GetLevel(int id)
    {
        return id > 0 && GameDataDatabase.TryGetLevelData(id, out LevelData level) ? level : null;
    }

    private static string GetMaterialModifierId(MaterialModifierModel modifier)
    {
        if (modifier is KindlingModifier) return "kindling";
        if (modifier is FlowModifier) return "flow";
        if (modifier is LiquefyModifier) return "liquefy";
        if (modifier is ChargeModifier) return "charge";
        if (modifier is VortexModifier) return "vortex";
        if (modifier is RepeatArrowModifier) return "repeat_arrow";
        if (modifier is OmniArrowModifier) return "omni_arrow";
        if (modifier is PeriodArrowModifier) return "period_arrow";
        if (modifier is PackArrowModifier) return "pack_arrow";
        if (modifier is LinkedArrowModifier) return "linked_arrow";
        if (modifier is BigArrow2Modifier) return "big_arrow_2";
        if (modifier is BigArrow3Modifier) return "big_arrow_3";
        if (modifier is BigArrow4Modifier) return "big_arrow_4";
        if (modifier is ReturnArrowModifier) return "return_arrow";
        if (modifier is RandomArrowModifier) return "random_arrow";
        if (modifier is ProliferatingArrowModifier) return "proliferating_arrow";
        if (modifier is EternalArrowModifier) return "eternal_arrow";
        if (modifier is FragileArrowModifier) return "fragile_arrow";
        if (modifier is RetainedArrowModifier) return "retained_arrow";
        if (modifier is HalfArrowModifier) return "half_arrow";
        if (modifier is DoomModifier) return "doom";
        if (modifier is LazyModifier) return "lazy";
        if (modifier is TemporaryModifier) return "temporary";
        return string.Empty;
    }

    private static MaterialModifierModel CreateMaterialModifier(string id)
    {
        switch (id)
        {
            case "kindling": return new KindlingModifier();
            case "flow": return new FlowModifier();
            case "liquefy": return new LiquefyModifier();
            case "charge": return new ChargeModifier();
            case "vortex": return new VortexModifier();
            case "heavy_arrow": return new RepeatArrowModifier();
            case "repeat_arrow": return new RepeatArrowModifier();
            case "omni_arrow": return new OmniArrowModifier();
            case "period_arrow": return new PeriodArrowModifier();
            case "pack_arrow": return new PackArrowModifier();
            case "linked_arrow": return new LinkedArrowModifier();
            case "big_arrow_2": return new BigArrow2Modifier();
            case "big_arrow_3": return new BigArrow3Modifier();
            case "big_arrow_4": return new BigArrow4Modifier();
            case "return_arrow": return new ReturnArrowModifier();
            case "random_arrow": return new RandomArrowModifier();
            case "proliferating_arrow": return new ProliferatingArrowModifier();
            case "eternal_arrow": return new EternalArrowModifier();
            case "fragile_arrow": return new FragileArrowModifier();
            case "retained_arrow": return new RetainedArrowModifier();
            case "half_arrow": return new HalfArrowModifier();
            case "doom": return new DoomModifier();
            case "lazy": return new LazyModifier();
            case "temporary": return new TemporaryModifier();
            default: return null;
        }
    }
}
