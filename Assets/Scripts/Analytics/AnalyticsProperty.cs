/// <summary>
/// 设备属性名常量（对应 TapDB 的「用户属性 - 设备」，见 Assets/Docs/userProp.csv）。
/// 这些是筛选/分部维度，通过 TapTapEvent.DeviceUpdate 上报，不需要每个事件携带。
/// 对局事件属性（134 个）见 Assets/Docs/eventProp.csv，在 P0-B 接入时按需在此补充常量。
/// 命名规范：小写 snake_case，不带 "#"（"#" 是 TapDB 预置事件/预置属性专用）。
/// </summary>
public static class AnalyticsProperty
{
    // 设备属性
    public const string AppVersion = "app_version";
    public const string BuildType = "build_type";
    public const string Platform = "platform";
    public const string Channel = "channel";
    public const string Region = "region";
    public const string Language = "language";
    public const string IsDev = "is_dev";
    public const string SaveSlot = "save_slot";
    public const string AscensionMax = "ascension_max";
    public const string TutorialDone = "tutorial_done";

    // 通用
    public const string Truncated = "truncated";

    /// <summary>补发标记：为 true 表示这条事件是上次启动未上报成功、本次补发的（时间戳为补发时刻）。</summary>
    public const string Replayed = "replayed";
}
