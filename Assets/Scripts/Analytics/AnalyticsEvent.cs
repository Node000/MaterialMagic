/// <summary>
/// TapDB 事件名常量（与 Assets/Docs/event.csv 一一对应，32 个）。
///
/// 启动 / 游戏时长 / 账号登录 / 登出 / 崩溃 已由 TapDB **预置事件**（device_login、play_game、
/// user_login、disconnect、exception_crash/error 等）自动上报，不在此列。
/// 命名规范：小写 snake_case，不带 "#"（"#" 是 TapDB 预置事件专用前缀）。
/// </summary>
public static class AnalyticsEvent
{
    // ── 对局（batch 1 已挂）──
    public const string RunStart = "run_start";
    public const string RunResume = "run_resume";
    public const string RunEnd = "run_end";

    // ── 道具（batch 1 已挂）──
    public const string MagicOffer = "magic_offer";
    public const string MagicAcquire = "magic_acquire";
    public const string MagicSell = "magic_sell";
    public const string MagicRemove = "magic_remove";

    // ── 商店（batch 1 已挂）──
    public const string ShopEnter = "shop_enter";
    public const string ShopPurchase = "shop_purchase";
    public const string ShopRefresh = "shop_refresh";
    public const string ShopLeave = "shop_leave";

    // ── 结算奖励与休息（batch 1 已挂）──
    public const string RewardEnter = "reward_enter";
    public const string RewardChoice = "reward_choice";
    public const string RewardSkip = "reward_skip";
    public const string RestChoose = "rest_choose";

    // ── 待挂（batch 2/3，见 plan/Tap埋点与玩家数据统计方案.md §12）──
    public const string NodeEnter = "node_enter";
    public const string NodeExit = "node_exit";
    public const string MapMoveFail = "map_move_fail";
    public const string BattleStart = "battle_start";
    public const string BattleEnd = "battle_end";
    public const string BattleTurn = "battle_turn";
    public const string MagicTrigger = "magic_trigger";
    public const string EventEnter = "event_enter";
    public const string EventOptionsShown = "event_options_shown";
    public const string EventOptionResolved = "event_option_resolved";
    public const string EventNoMatch = "event_no_match";
    public const string EventEnd = "event_end";
    public const string RewardGridEnd = "rewardgrid_end";
    public const string UpgradeChoose = "upgrade_choose";
    public const string FloorEnter = "floor_enter";
    public const string AscensionSelect = "ascension_select";
    public const string UnlockGain = "unlock_gain";
}
