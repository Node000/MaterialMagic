using System;
using System.Collections.Generic;
using Newtonsoft.Json;

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
    public string startConfigId;
    public string endedAtUtc;
    public float playSeconds;
    public int chapterNumericId;
    public int currentMapNodeIndex;
    public int totalMapNodeCount;
    public int currentLevelNumericId;
    public string progressText;
    public int ascensionLevel;
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
    public string sceneName;
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
    public SlotReadingRankSaveData[] magicReadingOrder;
    public int magicLayoutSlotCount;
    public int magicRightNeighborStep;
}

[Serializable]
public sealed class SlotReadingRankSaveData
{
    public int slotIndex;
    public int rank;
}

/// <summary>In-memory rule snapshot, not a disk checkpoint. Pending choice continuations are owned by their sessions.</summary>
public sealed class RunMemorySnapshot
{
    [JsonProperty] public RunSaveData Run { get; internal set; }
    [JsonProperty] public PlayerCombatSaveData Combat { get; internal set; }
    [JsonProperty] public BattleNodeSaveData Battle { get; internal set; }
    [JsonProperty] public RuleContentArchive Content { get; internal set; }
    [JsonProperty] public RuleProfileSnapshot Profile { get; internal set; }
    [JsonIgnore] public RulePlayerGraphSnapshot FullGraph { get; internal set; }
    [JsonIgnore] public IReadOnlyList<RuleEnemyRuntimeSnapshot> EnemyRuntime { get; internal set; }
    [JsonProperty] public bool RefreshUsedThisTurn { get; internal set; }
    [JsonProperty] public bool CommandTurnResolving { get; internal set; }

    public RunMemorySnapshot() { }
    public RunMemorySnapshot(RunSaveData run, PlayerCombatSaveData combat = null, BattleNodeSaveData battle = null,
        bool refreshUsedThisTurn = false, bool commandTurnResolving = false)
    {
        Run = run;
        Combat = combat;
        Battle = battle;
        RefreshUsedThisTurn = refreshUsedThisTurn;
        CommandTurnResolving = commandTurnResolving;
    }
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
    public bool isEnd;
    public bool isCompleted;
    public bool isAvailable = true;
    public bool isRevealed;
    public bool isHidden;
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
    /// <summary>本局在地图生成时确定的 Boss 关卡 id；0 表示旧存档未存（保持未确定）。</summary>
    public int chapterBossLevelId;
    /// <summary>Boss 使用的随机敌人组下标；-1 表示无组或未确定。</summary>
    public int chapterBossGroupIndex = -1;
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
    public int chapterNumericId;
    public bool isBoss;
    public int focusEnemyIndex = -1;
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
    public BuffRuntimeSaveData[] runtimeBuffs;
    public int battleDrawSeed;
    public int battleDrawStep;
    public int shield;
    public int extraRefreshChancesThisTurn;
    public int playedCardCountThisTurn;
    public int[] chargedPlayZoneIndices;
    public int permanentRefreshChancesUsedThisTurn;
    public int refreshLimitReductionThisTurn;
    public int handLimitThisTurn = int.MaxValue;
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
    public bool removeArrowUsed;
    public int refreshCount;
    // Zero retains the shared random stream for shops already in progress in legacy saves.
    public int batchSeed;
    public int[] layerOfferCounts = Array.Empty<int>();
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
    // -1 distinguishes legacy receipts, which did not persist the actual paid price.
    public int price = -1;
    public bool countedPaidArrowRemoval;
    public int paidArrowRemovalCountBefore;
    public int magicSlotIndex = -1;
    public int previousMagicNumericId;
    public string previousMagicModifierId;
    public int previousMagicSellPriceState = -1;
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
    public int drawOrderVersion;
    public MaterialCardSaveData[] deck = Array.Empty<MaterialCardSaveData>();
    public MagicSlotSaveData[] magicBook = Array.Empty<MagicSlotSaveData>();
    public string[] arrowUpgradeUnlockedNodeIds = Array.Empty<string>();
    public int arrowUpgradePendingNextTurnShield;
    public int permanentRefreshChances;
    public int paidShopArrowRemovalCount;
    public RelicSaveData[] relics;
    public RelicOfferSaveData pendingRelicOffer;
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
    public int sellPriceState = -1;
}
