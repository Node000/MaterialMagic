using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TapSDK.Login;
using UnityEngine;

public enum LoginOutcome
{
    Success = 0,
    Cancel = 1,
    Fail = 2,
    Unavailable = 3
}

/// <summary>
/// TapTap 登录封装。
///
/// 约定：
/// 1. 登录是**可选**流程，不登录也能正常游玩（隐私政策里已写明）；
/// 2. 只有 TapSDK 初始化成功（即玩家已同意隐私政策）后才可用；
/// 3. 登录成功后把账号标识交给埋点层（TapDB 的 UserID），未登录时数据按设备维度统计；
/// 4. 不采集昵称/头像等个人信息；账号标识交给 TapDB 做账号维度关联。
/// 5. 登录/登出本身不自定义埋点：TapDB 预置事件 user_login / disconnect 已覆盖。
/// </summary>
public static class TapTapAuthService
{
    /// <summary>自动登录（启动时发起）的入口标识（仅用于日志与后续手动入口区分）。</summary>
    public const string AutoLoginEntry = "auto_startup";

    private const string PrefsFirstLoginDoneKey = "TapTapAuth.FirstLoginDone";

    private static bool sdkAvailable;
    private static bool loggingIn;
    private static bool autoLoginAttempted;

    public static TapTapAccount Account { get; private set; }
    public static bool IsLoggedIn => Account != null;
    public static string UserId => Account == null ? null : ResolveUserId(Account);
    public static string OpenId => Account != null ? Account.openId : null;
    public static bool SdkAvailable => sdkAvailable;

    /// <summary>登录状态变化回调（true = 已登录）。</summary>
    public static event Action<bool> LoginStateChanged;

    internal static void NotifySdkAvailable(bool available)
    {
        sdkAvailable = available;
    }

    /// <summary>
    /// 静默登录：启动时尝试复用本地已有的登录态，不弹任何界面。
    /// </summary>
    public static async Task<bool> TrySilentLoginAsync()
    {
        if (!sdkAvailable || IsLoggedIn)
            return false;

        try
        {
            TapTapAccount account = await TapTapLogin.Instance.GetCurrentTapAccount();
            if (account == null || string.IsNullOrEmpty(ResolveUserId(account)))
                return false;

            ApplyAccount(account);
            return true;
        }
        catch (Exception exception)
        {
            // 静默登录失败不是错误路径（本地没有登录态时也会走到这里），只记调试日志。
            Debug.Log("[TapTapAuth] 静默登录未命中：" + exception.Message);
            return false;
        }
    }

    /// <summary>
    /// 启动自动登录：先复用本地已有登录态（完全不打扰玩家）；未命中则用 basic_info 自动发起一次登录。
    /// 玩家可以在授权界面取消；取消或失败后本次会话不重试，游戏以游客身份继续。
    /// </summary>
    public static async Task<LoginOutcome> TryAutoLoginAsync()
    {
        if (!sdkAvailable || IsLoggedIn || autoLoginAttempted)
            return LoginOutcome.Unavailable;

        autoLoginAttempted = true;

        if (await TrySilentLoginAsync() || IsLoggedIn)
            return LoginOutcome.Success;

        // basic_info 只取 openId/unionId：已登录 TapTap 客户端的玩家可无感完成，不取昵称头像。
        return await LoginFlowAsync(AutoLoginEntry, TapTapLogin.TAP_LOGIN_SCOPE_BASIC_INFO);
    }

    /// <summary>
    /// 主动登录（手动入口；当前版本主界面不放按钮，预留给后续的账号入口）。
    /// </summary>
    public static Task<LoginOutcome> LoginAsync(string entry)
    {
        return LoginFlowAsync(entry, TapTapLogin.TAP_LOGIN_SCOPE_PUBLIC_PROFILE);
    }

    private static async Task<LoginOutcome> LoginFlowAsync(string entry, string scope)
    {
        if (!sdkAvailable)
        {
            Debug.LogWarning("[TapTapAuth] TapSDK 尚未初始化（未同意隐私政策或未启用 TapTap 后端），无法登录。");
            return LoginOutcome.Unavailable;
        }

        if (IsLoggedIn)
            return LoginOutcome.Success;

        if (loggingIn)
            return LoginOutcome.Unavailable;

        loggingIn = true;

        try
        {
            string[] scopes = { scope };
            TapTapAccount account = await TapTapLogin.Instance.LoginWithScopes(scopes);
            if (account == null || string.IsNullOrEmpty(ResolveUserId(account)))
            {
                Debug.LogWarning("[TapTapAuth] 登录返回空账号。");
                return LoginOutcome.Fail;
            }

            ApplyAccount(account);
            PlayerPrefs.SetInt(PrefsFirstLoginDoneKey, 1);
            PlayerPrefs.Save();
            return LoginOutcome.Success;
        }
        catch (TaskCanceledException)
        {
            // 玩家在授权界面选择取消：这是正常路径，游戏继续以游客身份运行。
            return LoginOutcome.Cancel;
        }
        catch (Exception exception)
        {
            // 登录失败只记本地日志（TapDB 预置 user_login 事件会记录成功登录）。
            Debug.LogWarning("[TapTapAuth] 登录失败（entry=" + entry + "，scope=" + scope + "）：" + exception.Message);
            return LoginOutcome.Fail;
        }
        finally
        {
            loggingIn = false;
        }
    }

    public static void Logout()
    {
        if (!sdkAvailable)
            return;

        try
        {
            TapTapLogin.Instance.Logout();
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[TapTapAuth] 登出异常：" + exception.Message);
        }

        Account = null;
        AnalyticsService.ClearUser();
        LoginStateChanged?.Invoke(false);
    }

    private static void ApplyAccount(TapTapAccount account)
    {
        Account = account;
        string userId = ResolveUserId(account);

        // 账号维度标识交给埋点后端；未登录时数据仍以设备维度统计。
        AnalyticsService.SetUserId(userId);

        LoginStateChanged?.Invoke(true);
    }

    private static string ResolveUserId(TapTapAccount account)
    {
        if (account == null)
            return null;

        // unionId 在同一开发者主体下跨应用稳定，优先用它做账号维度标识。
        return !string.IsNullOrEmpty(account.unionId) ? account.unionId : account.openId;
    }
}
