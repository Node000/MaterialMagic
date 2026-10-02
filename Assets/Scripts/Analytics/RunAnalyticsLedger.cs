using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 对局埋点台账：保存「当前这一局」的上下文与本局累计，供各挂点拼 payload。
///
/// 只用纯数据累积，不改任何游戏逻辑；字段口径见 Assets/Docs/eventProp.csv。
/// 挂点约定：
///   * 开局/续玩 → <see cref="BeginRun"/>（由 HandSystemUI.Awake 调用），再发 run_start / run_resume；
///   * 进入节点 → <see cref="SetNode"/>（更新 step / node_type / level_id）；
///   * 道具发到槽位前 → <see cref="SetAcquireContext"/>，落地时由 <see cref="RecordMagicAcquired"/> 记账；
///   * 对局结束 → 发 run_end，然后 <see cref="Reset"/>。
/// </summary>
public static class RunAnalyticsLedger
{
    // ── 对局上下文 ──
    public static bool HasRun { get; private set; }
    public static string RunId { get; private set; } = string.Empty;
    public static int ChapterId { get; private set; }
    public static int TotalSteps { get; private set; }
    public static int Step { get; private set; }
    public static string NodeType { get; private set; } = string.Empty;
    public static int LevelId { get; private set; }
    public static int Floor { get; private set; } = 1;
    public static bool IsTutorialRun { get; private set; }
    public static bool IsDebugRun { get; private set; }

    private static Func<float> secondsProvider;
    private static Func<PlayerState> playerProvider;

    /// <summary>本局当前玩家状态（由 HandSystemUI 在 BeginRun 时注入；拿不到返回 null）。</summary>
    public static PlayerState Player => playerProvider != null ? playerProvider() : null;

    /// <summary>本局已进行时长（秒）。没有 provider 时返回 0。</summary>
    public static float RunSeconds => secondsProvider != null ? Mathf.Max(0f, secondsProvider()) : 0f;

    // ── 本局累计（run_end 汇总）──
    private static readonly List<string> acquiredMagicIds = new List<string>();
    private static readonly List<string> defeatedEnemies = new List<string>();
    private static int battleWins;
    private static int battleLoses;

    public static int AcquiredMagicCount => acquiredMagicIds.Count;
    public static int BattleWins => battleWins;
    public static int BattleLoses => battleLoses;

    // ── 商店会话（shop_leave 汇总）──
    private static bool inShop;
    private static bool battleShop;
    private static int shopGoldOnEnter;
    private static int shopSpent;
    private static int shopPurchases;
    private static float shopEnterTime;
    private static bool battleShopFlagPending;

    public static bool InShop => inShop;
    public static bool IsBattleShop => battleShop;

    // ── 待落槽道具的获取上下文 ──
    // 各来源在“发道具”之前用 SetAcquireContext 声明来源与候选，落地槽位时消费一次并清空。
    private static string acquireSource = string.Empty;
    private static int acquirePrice;
    private static string acquireOfferIds = string.Empty;
    private static int acquireOfferIndex = -1;

    public static string AcquireSource => string.IsNullOrEmpty(acquireSource) ? "unknown" : acquireSource;
    public static int AcquirePrice => acquirePrice;
    public static string AcquireOfferIds => acquireOfferIds;
    public static int AcquireOfferIndex => acquireOfferIndex;

    /// <summary>开局或续玩时调用一次。<paramref name="runId"/> 为空表示新局（本地生成并让存档沿用）。</summary>
    public static void BeginRun(string runId, ChapterData chapter, int totalSteps, int step, bool isTutorial, bool isDebug, Func<float> runSecondsProvider, Func<PlayerState> playerSource = null)
    {
        HasRun = true;
        RunId = string.IsNullOrEmpty(runId) ? Guid.NewGuid().ToString("N") : runId;
        RunSaveSystem.AdoptRunId(RunId);
        ChapterId = chapter != null ? chapter.numericId : 0;
        TotalSteps = totalSteps;
        Step = Mathf.Max(1, step);
        NodeType = string.Empty;
        LevelId = 0;
        Floor = 1;
        IsTutorialRun = isTutorial;
        IsDebugRun = isDebug;
        secondsProvider = runSecondsProvider;
        playerProvider = playerSource;
        ResetCounters();
        ClearAcquireContext();
        inShop = false;
        battleShop = false;
        battleShopFlagPending = false;
        ResetNode();
        ResetBattle();
        ResetEvent();
    }

    /// <summary>对局结束后清空（下次 BeginRun 会重建）。</summary>
    public static void Reset()
    {
        HasRun = false;
        RunId = string.Empty;
        secondsProvider = null;
        playerProvider = null;
        ChapterId = 0;
        TotalSteps = 0;
        Step = 0;
        NodeType = string.Empty;
        LevelId = 0;
        Floor = 1;
        IsTutorialRun = false;
        IsDebugRun = false;
        ResetCounters();
        ClearAcquireContext();
        inShop = false;
        battleShop = false;
        battleShopFlagPending = false;
    }

    private static void ResetCounters()
    {
        acquiredMagicIds.Clear();
        defeatedEnemies.Clear();
        battleWins = 0;
        battleLoses = 0;
        shopSpent = 0;
        shopPurchases = 0;
    }

    // ── 节点上下文（node_enter / node_exit 用）──

    public static float NodeStartRealtime { get; private set; }
    public static string NodeResult { get; private set; } = "cleared";
    private static int nodeStartHp;
    private static int nodeStartGold;
    private static int nodeStartMagicCount;
    private static bool nodeExitEmitted;

    /// <summary>node_seconds：本节点已停留时长。</summary>
    public static int NodeSeconds => Mathf.RoundToInt(Mathf.Max(0f, Time.realtimeSinceStartup - NodeStartRealtime));

    /// <summary>进入节点时重置节点快照（hp/gold/道具数）与结果。</summary>
    public static void BeginNode()
    {
        NodeStartRealtime = Time.realtimeSinceStartup;
        NodeResult = "cleared";
        nodeExitEmitted = false;
        PlayerState player = Player;
        nodeStartHp = player != null ? player.CurrentHealth : 0;
        nodeStartGold = player != null ? player.Gold : 0;
        nodeStartMagicCount = player != null ? player.MagicBook.Count : 0;
    }

    /// <summary>
    /// node_exit 是否已为当前节点发过：FinishReward 在一次节点里会被调用两次
    /// （战斗结算 → 三选一 → 自动商店再次 FinishReward），必须去重。
    /// </summary>
    public static bool MarkNodeExitEmitted()
    {
        if (nodeExitEmitted)
            return false;

        nodeExitEmitted = true;
        return true;
    }

    public static void SetNodeResult(string result)
    {
        NodeResult = string.IsNullOrEmpty(result) ? "cleared" : result;
    }

    public static int NodeHpDelta
    {
        get { PlayerState player = Player; return (player != null ? player.CurrentHealth : nodeStartHp) - nodeStartHp; }
    }

    public static int NodeGoldDelta
    {
        get { PlayerState player = Player; return (player != null ? player.Gold : nodeStartGold) - nodeStartGold; }
    }

    public static int NodeMagicGainedCount
    {
        get { PlayerState player = Player; return (player != null ? player.MagicBook.Count : nodeStartMagicCount) - nodeStartMagicCount; }
    }

    private static void ResetNode()
    {
        NodeStartRealtime = Time.realtimeSinceStartup;
        NodeResult = "cleared";
        nodeStartHp = 0;
        nodeStartGold = 0;
        nodeStartMagicCount = 0;
    }

    // ── 战斗上下文（battle_start / battle_end 用）──

    private static float battleStartRealtime;
    private static int battleTurns;
    private static int damageDealt;
    private static int damageTaken;

    public static int BattleTurns => battleTurns;
    public static int DamageDealt => damageDealt;
    public static int DamageTaken => damageTaken;
    public static int BattleSeconds => Mathf.RoundToInt(Mathf.Max(0f, Time.realtimeSinceStartup - battleStartRealtime));

    /// <summary>battle_start 时重置本场战斗的计数（回合/伤害/计时）。</summary>
    public static void MarkBattleStart()
    {
        battleStartRealtime = Time.realtimeSinceStartup;
        battleTurns = 0;
        damageDealt = 0;
        damageTaken = 0;
    }

    /// <summary>玩家结束一次出手（EndTurn）时计数 → battle_end.battle_turns。</summary>
    public static void CountBattleTurn()
    {
        battleTurns++;
    }

    /// <summary>打到敌人身上的真实血量伤害（由 EnemyModel 受伤时累加）。</summary>
    public static void AddDamageDealt(int amount)
    {
        if (amount > 0)
            damageDealt += amount;
    }

    /// <summary>玩家实际掉的血量（由 PlayerState 受伤时累加）。</summary>
    public static void AddDamageTaken(int amount)
    {
        if (amount > 0)
            damageTaken += amount;
    }

    private static void ResetBattle()
    {
        battleStartRealtime = Time.realtimeSinceStartup;
        battleTurns = 0;
        damageDealt = 0;
        damageTaken = 0;
    }

    // ── 事件上下文（event_* 用）──

    private static bool eventResolved;
    private static string eventPlaySequence = string.Empty;
    private static int eventRefreshCount;
    private static string eventId = string.Empty;

    public static bool EventResolved => eventResolved;
    public static string EventPlaySequence => eventPlaySequence;
    public static int EventRefreshCount => eventRefreshCount;
    public static string EventId => eventId;

    /// <summary>本次判定实际打出的箭头序列（ArrowReadToken 的展示文本拼接）。</summary>
    public static void SetEventPlay(string sequence, int refreshCount)
    {
        eventPlaySequence = sequence ?? string.Empty;
        eventRefreshCount = Mathf.Max(0, refreshCount);
    }

    public static void MarkEventResolved()
    {
        eventResolved = true;
    }

    private static void ResetEvent()
    {
        eventResolved = false;
        eventPlaySequence = string.Empty;
        eventRefreshCount = 0;
        eventId = string.Empty;
    }

    /// <summary>当前事件节点的 id（event_enter 时设置，event_end 复用）。</summary>
    public static void SetEventId(string id)
    {
        eventId = id ?? string.Empty;
    }

    // ── 敌人快照（battle_start / battle_end）──

    public static List<string> EnemyIds(IReadOnlyList<EnemyModel> enemies)
    {
        List<string> ids = new List<string>();
        for (int i = 0; enemies != null && i < enemies.Count; i++)
        {
            EnemyModel enemy = enemies[i];
            if (enemy != null && enemy.Data != null)
                ids.Add(enemy.Data.numericId.ToString());
        }
        return ids;
    }

    public static List<string> EnemyNames(IReadOnlyList<EnemyModel> enemies)
    {
        List<string> names = new List<string>();
        for (int i = 0; enemies != null && i < enemies.Count; i++)
        {
            EnemyModel enemy = enemies[i];
            if (enemy != null)
                names.Add(AnalyticsService.Truncate(enemy.Name));
        }
        return names;
    }

    /// <summary>战斗开始时的血量快照 / 战斗结束时的剩余血量（同一套编码）。</summary>
    public static List<string> EnemyHpList(IReadOnlyList<EnemyModel> enemies)
    {
        List<string> values = new List<string>();
        for (int i = 0; enemies != null && i < enemies.Count; i++)
        {
            EnemyModel enemy = enemies[i];
            if (enemy != null)
                values.Add(enemy.CurrentHealth.ToString());
        }
        return values;
    }

    // ── 上下文更新 ──

    /// <summary>进入某个地图节点时更新（step 为 1 基）。</summary>
    public static void SetNode(int step, string nodeType, int levelId)
    {
        Step = Mathf.Max(1, step);
        NodeType = nodeType ?? string.Empty;
        LevelId = levelId;
    }

    public static void SetFloor(int floor)
    {
        Floor = Mathf.Max(1, floor);
    }

    /// <summary>每条事件都带的公共对局上下文。</summary>
    public static Dictionary<string, object> Context()
    {
        Dictionary<string, object> payload = new Dictionary<string, object>
        {
            [AnalyticsProperty.RunId] = RunId,
            [AnalyticsProperty.Step] = Step,
            [AnalyticsProperty.ChapterStep] = Step,
            [AnalyticsProperty.ChapterId] = ChapterId,
            [AnalyticsProperty.TotalSteps] = TotalSteps,
            [AnalyticsProperty.Floor] = Floor,
            [AnalyticsProperty.RunSeconds] = Mathf.RoundToInt(RunSeconds)
        };
        if (!string.IsNullOrEmpty(NodeType))
            payload[AnalyticsProperty.NodeType] = NodeType;
        if (LevelId > 0)
            payload[AnalyticsProperty.LevelId] = LevelId;
        return payload;
    }

    // ── 道具获取上下文 ──

    public static void SetAcquireContext(string source, int price = 0, string offerIds = null, int offerIndex = -1)
    {
        acquireSource = source ?? string.Empty;
        acquirePrice = Mathf.Max(0, price);
        acquireOfferIds = offerIds ?? string.Empty;
        acquireOfferIndex = offerIndex;
    }

    public static void ClearAcquireContext()
    {
        acquireSource = string.Empty;
        acquirePrice = 0;
        acquireOfferIds = string.Empty;
        acquireOfferIndex = -1;
    }

    public static void RecordMagicAcquired(MagicData data)
    {
        if (data != null && !string.IsNullOrEmpty(data.id))
            acquiredMagicIds.Add(data.id);
    }

    // ── 战斗累计（batch 2 的战斗事件共用）──

    public static void RecordBattle(bool win)
    {
        if (win)
            battleWins++;
        else
            battleLoses++;
    }

    public static void RecordDefeatEnemy(EnemyModel enemy)
    {
        if (enemy == null)
            return;

        string id = !string.IsNullOrEmpty(enemy.Id) ? enemy.Id : (enemy.Data != null ? enemy.Data.numericId.ToString() : string.Empty);
        if (!string.IsNullOrEmpty(id) && !defeatedEnemies.Contains(id))
            defeatedEnemies.Add(id);
    }

    // ── 商店会话 ──

    /// <summary>战斗结算后自动进商店：由 HandSystemUI 在置 pendingBattleRewardShop 时声明，商店 Show 时消费。</summary>
    public static void MarkBattleShopPending()
    {
        battleShopFlagPending = true;
    }

    public static void BeginShop(int gold)
    {
        inShop = true;
        battleShop = battleShopFlagPending;
        battleShopFlagPending = false;
        shopGoldOnEnter = gold;
        shopSpent = 0;
        shopPurchases = 0;
        shopEnterTime = Time.realtimeSinceStartup;
    }

    public static void RecordShopPurchase(int price)
    {
        shopSpent += Mathf.Max(0, price);
        shopPurchases++;
    }

    public static void EndShop(out int totalSpent, out int purchaseCount, out int seconds)
    {
        totalSpent = shopSpent;
        purchaseCount = shopPurchases;
        seconds = inShop ? Mathf.RoundToInt(Mathf.Max(0f, Time.realtimeSinceStartup - shopEnterTime)) : 0;
        inShop = false;
        battleShop = false;
    }

    // ── payload 组装（对局首尾两条，其余在挂点处就地拼）──

    /// <summary>run_start / run_resume 的 payload。</summary>
    public static Dictionary<string, object> RunStartPayload(PlayerState player, bool resumed)
    {
        Dictionary<string, object> payload = Context();
        payload[AnalyticsProperty.StartConfigId] = string.IsNullOrEmpty(PlayerState.SelectedStartConfigId) ? "balanced" : PlayerState.SelectedStartConfigId;
        payload[AnalyticsProperty.Ascension] = DifficultyUpgradeSystem.CurrentAscensionLevel;
        payload[AnalyticsProperty.IsTutorial] = IsTutorialRun;
        payload[AnalyticsProperty.IsDebugRun] = IsDebugRun;
        AddPlayerSnapshot(payload, player);
        return payload;
    }

    /// <summary>run_end 的 payload（result = Victory / Defeat / Abandon）。</summary>
    public static Dictionary<string, object> RunEndPayload(PlayerState player, string result)
    {
        Dictionary<string, object> payload = Context();
        payload[AnalyticsProperty.Result] = result;
        payload[AnalyticsProperty.Ascension] = DifficultyUpgradeSystem.CurrentAscensionLevel;
        payload[AnalyticsProperty.EndNodeType] = NodeType;
        payload[AnalyticsProperty.EndLevelId] = LevelId;
        AddPlayerSnapshot(payload, player);
        payload[AnalyticsProperty.MagicBookIds] = MagicBookIds(player);
        payload[AnalyticsProperty.MagicAcquireCount] = AcquiredMagicCount;
        payload[AnalyticsProperty.MagicAcquireList] = AnalyticsService.JoinList(acquiredMagicIds);
        payload[AnalyticsProperty.BattleWinCount] = BattleWins;
        payload[AnalyticsProperty.BattleLoseCount] = BattleLoses;
        payload[AnalyticsProperty.DefeatEnemyIds] = AnalyticsService.JoinList(defeatedEnemies);
        return payload;
    }

    /// <summary>hp / max_hp / gold / magic_count / magic_ids / deck_count / deck_ids —— 很多事件都要带。</summary>
    public static void AddPlayerSnapshot(Dictionary<string, object> payload, PlayerState player)
    {
        if (payload == null || player == null)
            return;

        bool ignored;
        payload[AnalyticsProperty.Hp] = player.CurrentHealth;
        payload[AnalyticsProperty.MaxHp] = player.MaxHealth;
        payload[AnalyticsProperty.Gold] = player.Gold;
        payload[AnalyticsProperty.MagicCount] = player.MagicBook.Count;
        payload[AnalyticsProperty.MagicIds] = AnalyticsService.JoinList(MagicIds(player), out ignored);
        payload[AnalyticsProperty.DeckCount] = player.Deck.Count;
        payload[AnalyticsProperty.DeckIds] = AnalyticsService.JoinList(DeckIds(player), out ignored);
    }

    public static List<string> MagicIds(PlayerState player)
    {
        List<string> ids = new List<string>();
        if (player == null)
            return ids;

        for (int i = 0; i < player.MagicBook.Count; i++)
        {
            MagicModel magic = player.MagicBook[i];
            if (magic != null && !string.IsNullOrEmpty(magic.Id))
                ids.Add(magic.Id);
        }
        return ids;
    }

    /// <summary>道具栏（含空槽）里实际有道具的 id 列表，用于 run_end 的 magic_book_ids。</summary>
    public static string MagicBookIds(PlayerState player)
    {
        return AnalyticsService.JoinList(MagicIds(player));
    }

    /// <summary>箭头（材料卡）列表：用元素名表示，便于后台按元素聚合。</summary>
    public static List<string> DeckIds(PlayerState player)
    {
        List<string> ids = new List<string>();
        if (player == null)
            return ids;

        for (int i = 0; i < player.Deck.Count; i++)
        {
            MaterialModel card = player.Deck[i];
            if (card != null)
                ids.Add(card.material.ToString());
        }
        return ids;
    }

    /// <summary>把 ArrowReadToken 序列描述成可读字符串（event_* 的 played_sequence）。</summary>
    public static string DescribeTokens(IReadOnlyList<ArrowReadToken> tokens)
    {
        if (tokens == null || tokens.Count == 0)
            return string.Empty;

        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        for (int i = 0; i < tokens.Count; i++)
        {
            if (i > 0)
                builder.Append(',');
            builder.Append(tokens[i].DisplayMaterial.ToString());
        }
        return builder.ToString();
    }

    /// <summary>把一批道具转成 id 列表。</summary>
    public static List<string> MagicIdList(IReadOnlyList<MagicData> magics)
    {
        List<string> ids = new List<string>();
        if (magics == null)
            return ids;

        for (int i = 0; i < magics.Count; i++)
        {
            if (magics[i] != null && !string.IsNullOrEmpty(magics[i].id))
                ids.Add(magics[i].id);
        }
        return ids;
    }

    public static int IndexOfMagic(IReadOnlyList<MagicData> magics, MagicData target)
    {
        if (magics == null || target == null)
            return -1;

        for (int i = 0; i < magics.Count; i++)
        {
            if (magics[i] == target || (magics[i] != null && magics[i].id == target.id))
                return i;
        }
        return -1;
    }
}
