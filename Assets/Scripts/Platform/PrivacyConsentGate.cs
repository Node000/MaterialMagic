using UnityEngine;

/// <summary>
/// 隐私政策同意门控。
///
/// 依据 TapTap 官方合规说明：必须在用户同意《隐私政策》之后再初始化 SDK 进行数据收集。
/// 因此本工程把「是否同意」作为唯一的闸门：未同意时不调用 TapSDK，也不产生任何上报
/// （见 Assets/Docs/隐私政策全文（登录埋点版）.md）。
///
/// 三态规则：
/// * Undecided：首次启动弹**一次**邀请；
/// * Agreed：直接初始化 TapSDK、登录、上报；
/// * Declined：不初始化、不登录、不上报，**且不再弹**（游戏照常可玩，不退出）。
///
/// 「反悔」的路径是设置面板的「测试数据收集」开关
/// （`GameInitializer.ApplyConsentChange` / `AnalyticsConsentToggleUI`）；
/// 若该入口未上线，则走隐私政策第十条的联系方式人工通道。
/// </summary>
public enum PrivacyConsentState
{
    /// <summary>还没做过选择：首次启动时弹一次邀请。</summary>
    Undecided = 0,

    /// <summary>已同意：允许初始化 TapSDK、登录与数据上报。</summary>
    Agreed = 1,

    /// <summary>已拒绝：不初始化 SDK、不登录、不上报，且不再打扰。</summary>
    Declined = 2
}

public static class PrivacyConsentGate
{
    /// <summary>
    /// 数据口径版本。它代表「收集的信息种类与用途」的版本，**不是政策文档的日期**：
    /// 只做表述优化/语气调整时保持不变（老玩家不会重弹一次）；一旦扩大或改变收集范围必须递增。
    /// </summary>
    public const string PolicyVersion = "2026-09-30";

    private const string StatePrefsKey = "TapSDK.PrivacyConsent.State";

    /// <summary>旧版键：只记「已同意的版本号」，没有拒绝状态。用于迁移老玩家。</summary>
    private const string LegacyVersionPrefsKey = "TapSDK.PrivacyConsent.Version";

    private static PrivacyConsentState stateCache;

    /// <summary>当前选择状态。</summary>
    public static PrivacyConsentState State => stateCache;

    /// <summary>玩家是否已经做过选择（同意或拒绝）。为 true 时不再弹窗。</summary>
    public static bool HasDecided => stateCache != PrivacyConsentState.Undecided;

    /// <summary>玩家是否已同意当前数据口径。</summary>
    public static bool HasConsented => stateCache == PrivacyConsentState.Agreed;

    static PrivacyConsentGate()
    {
        stateCache = Load();
    }

    /// <summary>玩家同意分享：记录状态，允许初始化 SDK 与上报。</summary>
    public static void Grant()
    {
        Set(PrivacyConsentState.Agreed);
    }

    /// <summary>玩家拒绝分享：记录状态，之后不再询问（也不退出游戏）。</summary>
    public static void Decline()
    {
        Set(PrivacyConsentState.Declined);
    }

    /// <summary>清空选择（内部调试/回归用，正式包不应暴露入口）。</summary>
    public static void ResetForDebug()
    {
        stateCache = PrivacyConsentState.Undecided;
        PlayerPrefs.DeleteKey(StatePrefsKey);
        PlayerPrefs.DeleteKey(LegacyVersionPrefsKey);
        PlayerPrefs.Save();
    }

    private static void Set(PrivacyConsentState state)
    {
        stateCache = state;
        PlayerPrefs.SetString(StatePrefsKey, state.ToString().ToLowerInvariant() + ":" + PolicyVersion);
        PlayerPrefs.DeleteKey(LegacyVersionPrefsKey);
        PlayerPrefs.Save();
    }

    private static PrivacyConsentState Load()
    {
        // 老版本只记了「已同意的版本号」：命中即迁移为 Agreed（老玩家不重弹）。
        if (PlayerPrefs.GetString(LegacyVersionPrefsKey, string.Empty) == PolicyVersion)
        {
            Set(PrivacyConsentState.Agreed);
            return PrivacyConsentState.Agreed;
        }

        string raw = PlayerPrefs.GetString(StatePrefsKey, string.Empty);
        int split = raw.IndexOf(':');
        if (split <= 0 || raw.Substring(split + 1) != PolicyVersion)
            return PrivacyConsentState.Undecided;

        string stateText = raw.Substring(0, split);
        if (string.Equals(stateText, "agreed", System.StringComparison.OrdinalIgnoreCase))
            return PrivacyConsentState.Agreed;
        if (string.Equals(stateText, "declined", System.StringComparison.OrdinalIgnoreCase))
            return PrivacyConsentState.Declined;

        return PrivacyConsentState.Undecided;
    }
}
