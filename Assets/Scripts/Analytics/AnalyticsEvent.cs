/// <summary>
/// TapDB 事件名常量。
///
/// 启动 / 游戏时长 / 账号登录 / 登出 / 崩溃 已由 TapDB **预置事件**（device_login、play_game、
/// user_login、disconnect、exception_crash/error 等）自动上报，不再自定义重复登记。
/// 本工程只需登记对局与内容事件（见 Assets/Docs/event.csv，35 个），在 P0-B / P0-C 接入时逐个加到这里。
/// 命名规范：小写 snake_case，不带 "#"（"#" 是 TapDB 预置事件专用前缀）。
/// </summary>
public static class AnalyticsEvent
{
}
