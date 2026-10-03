using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TapSDK.Core;
using UnityEngine;

public enum GameBackend
{
    None,
    TapTap,
    Steam
}

[DefaultExecutionOrder(-1000)]
public class GameInitializer : MonoBehaviour
{
    private const string CredentialsResourcePath = "LocalSettings/GameBackendLocalCredentials";

    public static GameInitializer Instance { get; private set; }

    [Header("后端")]
    [SerializeField] private GameBackend backend = GameBackend.None;
    [SerializeField] private string tapTapChannel = "default";
    [Tooltip("编辑器 Play Mode 下也运行 TapSDK（默认关闭：TapSDK 在 Windows 编辑器会做原生初始化与网络校验，可能影响编辑器稳定性）。真机打包不受此开关影响。")]
    [SerializeField] private bool runTapTapInEditor = false;

    [Header("隐私合规")]
    [Tooltip("TapTap 后端下必须在玩家同意隐私政策后才初始化 SDK（官方合规要求）。仅在内部联调时可关闭。")]
    [SerializeField] private bool requirePrivacyConsent = true;
    [Tooltip("场景里的隐私弹窗；留空时使用代码生成的临时兜底弹窗。")]
    [SerializeField] private PrivacyConsentPanel privacyConsentPanel;

    [Header("登录")]
    [Tooltip("启动后自动登录：先复用本地登录态，未命中则自动发起一次 TapTap 授权。玩家可取消，取消后以游客身份游玩。玩家曾在首启选「不同意」、之后又在设置里打开「测试数据收集」时，SDK 会在那时才初始化，并同样发起一次登录。")]
    [SerializeField] private bool autoLoginOnStartup = true;
    [Tooltip("自动登录延后几秒发起，让 SDK 初始化/设备注册先稳定下来。首启会先弹「测试数据收集」，玩家作答后才初始化 SDK 并发起登录。")]
    [SerializeField] private float autoLoginDelaySeconds = 0.5f;

    [Header("埋点")]
    [SerializeField] private bool enableAnalytics = true;
    [SerializeField] private bool analyticsLogToConsole = false;
    [Tooltip("开发期把事件写入 persistentDataPath/Save/analytics_events.jsonl。")]
    [SerializeField] private bool analyticsWriteLocalFile = false;

    public GameBackend Backend => backend;

    /// <summary>
    /// 实际生效的后端：真机按场景配置执行；编辑器里默认跳过 TapSDK（避免原生初始化/网络校验影响编辑器）
    /// —— 需要联调时把 <see cref="runTapTapInEditor"/> 打开。
    /// </summary>
    private GameBackend EffectiveBackend
    {
        get
        {
            if (Application.isEditor && backend == GameBackend.TapTap && !runTapTapInEditor)
                return GameBackend.None;

            return backend;
        }
    }

    public bool IsInitialized { get; private set; }

    /// <summary>
    /// TapSDK 初始化结果回调。TapTapSDK.Init **是异步的**：在 OnInitSuccess 之前调用
    /// DeviceUpdate / LogEvent / 登录 都会拿到“SDK 正在初始化中/设备初始化中，请稍后再试”。
    /// </summary>
    private ITapInitCallback tapTapInitCallback;
    public bool IsAnalyticsEnabled => enableAnalytics;

    /// <summary>首启需要弹「测试数据收集」（还没做过选择）。玩家作答后才初始化 SDK 并发起登录。</summary>
    private bool pendingConsentPrompt;

    /// <summary>
    /// 后端启动流程是否已经发起。TapTapSDK.Init 是异步的，<see cref="IsInitialized"/> 要好几个帧之后才为 true；
    /// 不单独记这一笔的话，同一帧里的第二次调用会重复 Init 并重复注册回调。
    /// </summary>
    private bool backendStartRequested;

    /// <summary>本次启动在 Init 时定下的 TapDB 采集开关（SDK 不能在运行期改变它）。</summary>
    private bool collectionEnabledAtInit;

    /// <summary>本次会话当前是否在采集数据（设置开关会改它；SDK 侧的要下次启动才跟上）。</summary>
    private bool sessionCollectionEnabled;

#if STEAMWORKS_NET
    private bool steamInitialized;
#endif

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeAnalytics();

        // 数据收集合规（2026-10-02 第四版）：
        //   * 「测试数据收集」只在首启（Undecided）问一次，而且问在**最前面**：玩家作答之后才初始化 SDK、
        //     才发起 TapTap 登录（SDK 初始化本身就会处理设备与网络信息，必须排在同意之后）；
        //   * 因为同意在 Init 之前就定了，首启选「同意」时本次启动即可正常采集，
        //     不再需要“未决时按不采集初始化 + 事件落盘、下次启动补发”那条路径；
        //   * 老玩家（Agreed / Declined）不弹窗，按已记录的选择直接初始化后端并登录。
        pendingConsentPrompt = NeedsConsentPrompt;

        // 场景里的「测试数据收集」面板可能留在激活状态（美术调面板时就是这样）。
        // 不需要询问时（已做过选择的老玩家）必须在这一帧就收起：否则面板会一直糊在菜单上，
        // 而且它的按钮只在 Show() 里绑回调，点上去没有任何反应（等于卡住界面）。
        if (!pendingConsentPrompt && privacyConsentPanel != null)
            privacyConsentPanel.Hide();
    }

    private void Start()
    {
        // 首启：先把「测试数据收集」问清楚再往下走。
        // 面板不可用时 Show() 只会报错、不会有玩家作答，启动就停在这里（既不初始化 SDK 也不登录）——
        // 这是有意的：这类问题要在联调时立刻暴露，而不是静默降级继续跑。
        if (pendingConsentPrompt)
        {
            ShowConsentPrompt();
            return;
        }

        StartBackendWithCurrentConsent();
    }

    /// <summary>首启（还没做过选择）才需要弹「测试数据收集」；弹出时机是启动最开始。</summary>
    private bool NeedsConsentPrompt =>
        EffectiveBackend != GameBackend.None && requirePrivacyConsent && !PrivacyConsentGate.HasDecided;

    /// <summary>
    /// 按当前同意状态启动后端，并定下本次启动的采集开关。幂等：
    /// Init 是异步的，重复调用由 <see cref="backendStartRequested"/> 挡住。
    ///
    /// 2026-10-02 决定：**未同意「测试数据收集」就完全不初始化 TapSDK**。SDK 同时提供登录，
    /// 所以“不同意”既是“不上报”也是“不使用 TapTap 登录”（见 plan/隐私授权与隐私政策软化改造.md §3）。
    /// </summary>
    public void StartBackendWithCurrentConsent()
    {
        if (backendStartRequested || IsInitialized)
            return;

        backendStartRequested = true;

        // TapDB 的采集开关只能在 Init 时决定；其它后端不受同意门控影响（开发/编辑器用）。
        bool collected = EffectiveBackend != GameBackend.TapTap
            || !requirePrivacyConsent
            || PrivacyConsentGate.HasConsented;

        // 只针对 TapTap 后端、且在开启同意门控时才把整个 SDK 关掉。
        bool blockedByConsent = EffectiveBackend == GameBackend.TapTap
            && requirePrivacyConsent
            && !PrivacyConsentGate.HasConsented;

        collectionEnabledAtInit = collected;
        sessionCollectionEnabled = collected;
        AnalyticsService.SetSessionCollection(collected);
        AnalyticsService.SetEnabled(enableAnalytics && collected);

        if (!PrivacyConsentGate.HasConsented)
        {
            // 未同意（未决/已拒绝）：不采集，也不保留任何本地待发数据。
            AnalyticsService.DiscardPendingOnDisk();
        }

        if (blockedByConsent)
        {
            Debug.Log("[GameInitializer] 未同意「测试数据收集」：本次启动不初始化 TapSDK，也不发起登录。");

            // 放掉“已发起”标记：玩家之后在设置里重新打开「测试数据收集」时，还要能补上 SDK 初始化。
            backendStartRequested = false;
            return;
        }

        InitializeBackend();
    }

    /// <summary>
    /// 玩家在设置界面改动「测试数据收集」开关时调用（`AnalyticsConsentToggleUI`），
    /// 也用于首次弹窗的选择（同意/不同意）。
    /// * 关闭：我们自己的上报立即停、丢掉未上报的本地待发数据（即**只取消数据传输**，
    ///   **不结束当前登录态**）；原生预置事件下次启动起彻底停（SDK 无法在运行期反初始化）；
    ///   下次启动起不再初始化 SDK、也不再登录；
    /// * 打开：我们自己的上报立即恢复；若 SDK 尚未初始化（曾被拒绝），则补上初始化并按
    ///   <see cref="autoLoginOnStartup"/> 发起一次登录；若 SDK 已就绪但当前未登录，也当场补一次登录。
    /// </summary>
    public void ApplyConsentChange(bool consented)
    {
        // 重新打开开关时允许再尝试一次自动登录：此时若尚未登录就要补一次（玩家可能取消过授权）。
        if (consented)
            TapTapAuthService.AllowAutoLoginRetry();

        if (consented && !IsInitialized)
        {
            // 后端还没启动（如初始化失败的路径）：直接用新状态启动。
            StartBackendWithCurrentConsent();
            return;
        }

        sessionCollectionEnabled = consented;
        AnalyticsService.SetEnabled(enableAnalytics && consented);

        if (!consented)
        {
            // 关闭只取消数据传输（含未上报的本地暂存）；**不做登出**，当前登录态保留到本次会话结束，
            // 但下次启动会因未同意而不初始化 SDK、也不再登录。
            AnalyticsService.DiscardPendingOnDisk();
            return;
        }

        // 走到这里说明 SDK 本次会话已经初始化过：若当前未登录（如玩家取消过授权），当场补一次登录。
        if (autoLoginOnStartup && !TapTapAuthService.IsLoggedIn)
            _ = AutoLoginAsync();
    }

    private void OnPrivacyConsentGranted()
    {
        PrivacyConsentGate.Grant();
        ApplyConsentChange(true);
        ContinueStartupAfterConsent();
    }

    private void OnPrivacyConsentRejected()
    {
        // 不同意只是不采集数据，也不使用 TapTap 登录：存档与游玩全都照常（弹窗两个按钮都能继续玩）。
        PrivacyConsentGate.Decline();
        ApplyConsentChange(false);
        ContinueStartupAfterConsent();
    }

    /// <summary>
    /// 玩家作答后继续启动流程。同意时 <see cref="ApplyConsentChange"/> 内部已经启动过后端（此处幂等），
    /// 但**拒绝时它不会启动**——所以这里必须补一次，否则拒绝的玩家既不初始化 SDK 也不登录。
    /// </summary>
    private void ContinueStartupAfterConsent()
    {
        StartBackendWithCurrentConsent();
    }

    private void Update()
    {
#if STEAMWORKS_NET
        if (steamInitialized)
            Steamworks.SteamAPI.RunCallbacks();
#endif
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

#if STEAMWORKS_NET
        if (steamInitialized)
            Steamworks.SteamAPI.Shutdown();
#endif

        Instance = null;
    }

    private void InitializeBackend()
    {
        if (Application.isEditor && backend == GameBackend.TapTap && !runTapTapInEditor)
            Debug.Log("[GameInitializer] 编辑器内跳过 TapSDK 初始化（要联调把 runTapTapInEditor 打开）；真机打包不受影响。");

        switch (EffectiveBackend)
        {
            case GameBackend.None:
                return;
            case GameBackend.TapTap:
                InitializeTapTap();
                return;
            case GameBackend.Steam:
                InitializeSteam();
                return;
        }
    }

    private void InitializeAnalytics()
    {
        AnalyticsConfig config = new AnalyticsConfig
        {
            enabled = enableAnalytics,
            logToConsole = analyticsLogToConsole,
            writeLocalFile = analyticsWriteLocalFile
        };
        AnalyticsService.Init(config);

        // 设备属性（对应 TapDB 的「用户属性 - 设备」，见 Assets/Docs/userProp.csv）：
        // 只上报两个必要维度；平台/机型、地区、渠道由 TapDB 自带采集，语言/存档栏位/进阶进度/
        // 教程完成对平衡分析无增量（进阶与教程局已由事件属性 ascension / is_tutorial 承载）。
        // 设备属性只留 app_version（TapDB 预置字段名）。is_dev 已改挂事件属性，
        // 不再走 DeviceUpdate（实测设备属性通道会被接收端整条丢弃，见 Assets/Docs/埋点后台录入表.md 第四部分）。
        AnalyticsService.SetDeviceProperty(AnalyticsProperty.AppVersion, Application.version);
    }

    private void InitializeTapTap()
    {
        GameBackendLocalCredentials credentials = Resources.Load<GameBackendLocalCredentials>(CredentialsResourcePath);
        if (credentials == null || !credentials.HasTapTapCredentials)
        {
            Debug.LogError($"TapTap initialization requires Resources/{CredentialsResourcePath}.asset.");
            return;
        }

        TapTapSdkOptions coreOptions = new TapTapSdkOptions
        {
            clientId = credentials.TapTapClientId,
            clientToken = credentials.TapTapClientToken,
            region = TapTapRegionType.CN,
            enableLog = Application.isEditor || Debug.isDebugBuild
        };
        // TapDB 的采集开关只在 Init 时生效：只有同意过（或本次已选择同意）才打开。
        // 未同意时整个 SDK 都不初始化（登录与采集一起关，见 StartBackendWithCurrentConsent）。
        TapTapEventOptions eventOptions = new TapTapEventOptions
        {
            channel = tapTapChannel,
            enableTapTapEvent = enableAnalytics && collectionEnabledAtInit,
            enableAutoIAPEvent = false
        };

        // Init 是异步的：先注册结果回调，等 OnInitSuccess 之后再调用任何 SDK API
        // （否则会拿到“SDK 正在初始化中/设备初始化中，请稍后再试”）。
        tapTapInitCallback = new TapTapInitCallback(this);
        TapTapSDK.AddInitCallback(tapTapInitCallback);
        TapTapSDK.Init(coreOptions, new TapTapSdkBaseOptions[] { eventOptions });
    }

    /// <summary>TapSDK 初始化成功：挂 TapDB 后端并上报设备属性，随后按启动流程发起登录。</summary>
    private void OnTapTapInitSuccess()
    {
        ReleaseInitCallback();
        IsInitialized = true;

        // 只在本次启动确实要采集时才挂 TapDB 后端（同意状态在 Init 之前就已确定）：
        // 之前入队的事件与已设置的设备属性会在挂载时自动补发/补报。
        if (enableAnalytics && collectionEnabledAtInit)
        {
            AnalyticsService.AddSink(new TapAnalyticsSink(analyticsLogToConsole));

            // 上次会话落盘的待发事件（运行期从「关」重新打开后产生的数据）在这里补发。
            AnalyticsService.FlushPendingOnDisk();

            // TapDB 的设备注册在原生层也是异步的：首次设备属性上报可能撞上“设备初始化中”，
            // 延后再幂等重推一次（无害）。
            _ = RepushDevicePropertiesAfterDelayAsync();
        }

        // 走到这里说明本次启动已经取得采集同意（未同意时上面就已经 return，不会初始化 SDK），
        // 因此登录与采集同进同出：能初始化就说明登录可用。
        TapTapAuthService.NotifySdkAvailable(true);

        // 登录跟着同意走：含“玩家在设置里重新打开开关、SDK 此时才被初始化”的情形，
        // 所以这里不看是不是启动阶段。
        if (autoLoginOnStartup)
            _ = AutoLoginAsync();
    }

    /// <summary>自动登录：先延后一小会儿让设备注册稳定，再走一次静默/授权登录。每次会话只会尝试一次。</summary>
    private async Task AutoLoginAsync()
    {
        if (autoLoginDelaySeconds > 0f)
            await Task.Delay(Mathf.RoundToInt(autoLoginDelaySeconds * 1000f));

        // 延后期间玩家可能又把「测试数据收集」关掉了：那时不能再发起登录
        // （关掉只取消数据传输，但不应反过来弹出登录授权界面）。
        if (!PrivacyConsentGate.HasConsented)
            return;

        await TapTapAuthService.TryAutoLoginAsync();
    }

    /// <summary>
    /// 弹出一次「测试数据收集」。必须先于 SDK 初始化与登录调用。
    /// 面板不可用（场景没绑定、兜底面板也关着）时 <see cref="PrivacyConsentPanel.Show"/> 只会报错，
    /// 启动会停在这里等一个永远不会来的回答——这是有意的，让问题在联调时直接暴露，而不是静默放行。
    /// </summary>
    private void ShowConsentPrompt()
    {
        pendingConsentPrompt = false;

        PrivacyConsentPanel panel = privacyConsentPanel != null
            ? privacyConsentPanel
            : PrivacyConsentPanel.CreateRuntimeFallback();
        panel.Show(OnPrivacyConsentGranted, OnPrivacyConsentRejected);
    }

    /// <summary>延后重推一次设备属性，避开 TapDB 设备注册窗口。</summary>
    private async Task RepushDevicePropertiesAfterDelayAsync()
    {
        await Task.Delay(3000);
        AnalyticsService.RepushDeviceProperties();
    }

    /// <summary>TapSDK 初始化失败：不上报、不开放登录，只记错误（errorCode 见 TapInitErrorCode）。</summary>
    private void OnTapTapInitFail(int errorCode, string errorMsg)
    {
        ReleaseInitCallback();
        IsInitialized = false;

        string hint = string.Empty;
        switch (errorCode)
        {
            case 1000:
                hint = "（参数非法：clientId / clientToken 为空？）";
                break;
            case 1001:
                hint = "（应用信息与 clientId / clientToken 不匹配：检查后台配的应用与本地凭据是否同一个）";
                break;
            case 1003:
                hint = "（SDK 内部同步初始化异常，与后台配置无关）";
                break;
        }

        Debug.LogError("[GameInitializer] TapSDK 初始化失败：code=" + errorCode + " msg=" + errorMsg + hint);

        // 放掉“已发起”标记：首启的「测试数据收集」现在问在初始化之前，这里不再补弹；
        // 但初始化失败后仍应允许玩家通过设置里的开关重新打开采集（或重开游戏）再试一次。
        backendStartRequested = false;
    }

    private void ReleaseInitCallback()
    {
        if (tapTapInitCallback == null)
            return;

        TapTapSDK.RemoveInitCallback(tapTapInitCallback);
        tapTapInitCallback = null;
    }

    /// <summary>ITapInitCallback 适配器（SDK 内部对回调持有强引用，所以只注册一个、用完即注销）。</summary>
    private class TapTapInitCallback : ITapInitCallback
    {
        private readonly GameInitializer owner;

        public TapTapInitCallback(GameInitializer owner)
        {
            this.owner = owner;
        }

        public void OnInitSuccess()
        {
            owner.OnTapTapInitSuccess();
        }

        public void OnInitFail(int errorCode, string errorMsg)
        {
            owner.OnTapTapInitFail(errorCode, errorMsg);
        }
    }

    private void InitializeSteam()
    {
#if STEAMWORKS_NET
        steamInitialized = Steamworks.SteamAPI.Init();
        IsInitialized = steamInitialized;
        if (!steamInitialized)
            Debug.LogError("Steamworks.NET initialization failed. Confirm Steam is running and steam_appid.txt is configured.");
#else
        Debug.LogError("Steam backend is selected, but Steamworks.NET is not installed. Install it and add the STEAMWORKS_NET scripting define symbol.");
#endif
    }
}
