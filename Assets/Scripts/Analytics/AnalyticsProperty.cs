/// <summary>
/// 埋点属性名常量。
/// 设备属性（TapDB「用户属性 - 设备」）见 Assets/Docs/userProp.csv；事件属性见 Assets/Docs/eventProp.csv。
/// 设备属性只留 `app_version`（版本对比；TapDB 预置字段名，平台自带采集）；
/// 其余维度：平台/机型/地区/渠道 → TapDB 自带采集；语言/版本类型/存档栏位/最高已解锁进阶 → 零区分度或已由事件属性承载。
/// `is_dev` 改为**事件属性**（公共标记，挂在全部事件上）：TapDB 接收端对未登记的设备属性会整条丢弃。
/// 命名规范：小写 snake_case，不带 "#"（"#" 是 TapDB 预置事件/预置属性专用）。
/// </summary>
public static class AnalyticsProperty
{
    // 设备属性（DeviceUpdate）
    /// <summary>游戏版本号。TapDB 预置字段名（平台自带采集），用于「改动前/后」对比。</summary>
    public const string AppVersion = "app_version";

    // 事件属性：公共标记（绑定全部事件，由 AnalyticsService.BuildPayload 统一注入）
    /// <summary>是否内部版本（编辑器 / Development 包 / 调试包）。用于把内部测试数据排除在平衡样本之外。</summary>
    public const string IsDev = "is_dev";

    // 通用
    public const string Truncated = "truncated";

    /// <summary>补发标记：为 true 表示这条事件是上次启动未上报成功、本次补发的（时间戳为补发时刻）。</summary>
    public const string Replayed = "replayed";

    // ── 对局上下文（每条事件都带，由 RunAnalyticsLedger.Context() 组装；名字严格对齐 eventProp.csv）──
    public const string RunId = "run_id";
    public const string Step = "step";
    public const string ChapterStep = "chapter_step";
    public const string ChapterId = "chapter_id";
    public const string TotalSteps = "total_steps";
    public const string Floor = "floor";
    public const string NodeType = "node_type";
    public const string LevelId = "level_id";
    public const string RunSeconds = "run_seconds";

    // ── 对局首尾 ──
    public const string StartConfigId = "start_config_id";
    public const string Ascension = "ascension";
    public const string IsTutorial = "is_tutorial";
    public const string IsDebugRun = "is_debug_run";
    public const string Result = "result";
    public const string EndNodeType = "end_node_type";
    public const string EndLevelId = "end_level_id";
    public const string MagicBookIds = "magic_book_ids";
    public const string MagicAcquireCount = "magic_acquire_count";
    public const string MagicAcquireList = "magic_acquire_list";
    public const string BattleWinCount = "battle_win_count";
    public const string BattleLoseCount = "battle_lose_count";
    public const string DefeatEnemyIds = "defeat_enemy_ids";

    // ── 玩家/道具栏快照 ──
    public const string Hp = "hp";
    public const string MaxHp = "max_hp";
    public const string Gold = "gold";
    public const string MagicCount = "magic_count";
    public const string MagicIds = "magic_ids";
    public const string DeckCount = "deck_count";
    public const string DeckIds = "deck_ids";

    // ── 道具 ──
    public const string MagicId = "magic_id";
    public const string MagicNumericId = "magic_numeric_id";
    public const string MagicName = "magic_name";
    public const string Rarity = "rarity";
    public const string Source = "source";
    public const string Price = "price";
    public const string GoldBefore = "gold_before";
    public const string GoldAfter = "gold_after";
    public const string SlotIndex = "slot_index";
    public const string ReplacedMagicId = "replaced_magic_id";
    public const string MagicBookCount = "magic_book_count";
    public const string OfferMagicIds = "offer_magic_ids";
    public const string OfferIndex = "offer_index";
    public const string OfferSource = "offer_source";
    public const string OfferCount = "offer_count";
    public const string Reason = "reason";
    public const string IsElite = "is_elite";
    public const string IsBoss = "is_boss";

    // ── 商店 ──
    public const string IsBattleShop = "is_battle_shop";
    public const string OfferKinds = "offer_kinds";
    public const string OfferIds = "offer_ids";
    public const string Kind = "kind";
    public const string Material = "material";
    public const string ModifierId = "modifier_id";
    public const string RefreshIndex = "refresh_index";
    public const string IsUndo = "is_undo";
    public const string TotalSpent = "total_spent";
    public const string PurchaseCount = "purchase_count";
    public const string GoldLeft = "gold_left";
    public const string ShopSeconds = "shop_seconds";

    // ── 结算奖励与休息 ──
    public const string BaseGold = "base_gold";
    public const string GoldChoiceAmount = "gold_choice_amount";
    public const string ArrowOfferMaterial = "arrow_offer_material";
    public const string ArrowOfferModifier = "arrow_offer_modifier";
    public const string Choice = "choice";
    public const string ChoiceIndex = "choice_index";
    public const string RewardAmount = "reward_amount";
    public const string HealAmount = "heal_amount";
    public const string HpDelta = "hp_delta";
    public const string GoldDelta = "gold_delta";

    // ── 节点（node_enter / node_exit）──
    public const string NodeSeconds = "node_seconds";
    public const string IsHidden = "is_hidden";
    public const string MapX = "map_x";
    public const string MapY = "map_y";
    public const string MagicGainedCount = "magic_gained_count";
    public const string Direction = "direction";

    // ── 战斗（battle_start / battle_end）──
    public const string LevelType = "level_type";
    public const string EnemyIds = "enemy_ids";
    public const string EnemyNames = "enemy_names";
    public const string EnemyCount = "enemy_count";
    public const string EnemyHpList = "enemy_hp_list";
    public const string EnemyHpRemainingList = "enemy_hp_remaining_list";
    public const string EnemyGroupIndex = "enemy_group_index";
    public const string BattleTurns = "battle_turns";
    public const string BattleSeconds = "battle_seconds";
    public const string DefeatSourceEnemyId = "defeat_source_enemy_id";
    public const string DefeatSourceEnemyName = "defeat_source_enemy_name";
    public const string HpLeft = "hp_left";
    public const string DamageDealt = "damage_dealt";
    public const string DamageTaken = "damage_taken";
    public const string MagicTriggerCounts = "magic_trigger_counts";

    // ── 事件节点（event_*）──
    public const string EventId = "event_id";
    public const string EventNumericId = "event_numeric_id";
    public const string TitleKey = "title_key";
    public const string OptionCount = "option_count";
    public const string OptionIds = "option_ids";
    public const string OptionRecipes = "option_recipes";
    public const string OptionExitFlags = "option_exit_flags";
    public const string OptionTags = "option_tags";
    public const string HandMaterials = "hand_materials";
    public const string OptionId = "option_id";
    public const string OptionIndex = "option_index";
    public const string IsExit = "is_exit";
    public const string ResolveCount = "resolve_count";
    public const string PlayedSequence = "played_sequence";
    public const string RefreshCount = "refresh_count";
    public const string TurnsUsed = "turns_used";
    public const string EffectTypes = "effect_types";
    public const string EffectSummary = "effect_summary";
    public const string MagicDelta = "magic_delta";
    public const string ArrowDelta = "arrow_delta";
    public const string MagicGained = "magic_gained";

    // ── 强化（upgrade_choose）──
    public const string UpgradeKind = "upgrade_kind";
    public const string TargetId = "target_id";
    public const string ConsumedSequence = "consumed_sequence";
    public const string HighestUnlocked = "highest_unlocked";
}
