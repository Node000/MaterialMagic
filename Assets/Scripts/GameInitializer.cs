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
    [Tooltip("启动后自动登录：先复用本地登录态，未命中则自动发起一次 TapTap 授权。玩家可取消，取消后以游客身份游玩。")]
    [SerializeField] private bool autoLoginOnStartup = true;
    [Tooltip("自动登录延后几秒发起，让 SDK 初始化/设备注册先稳定下来。首启的「测试数据收集」弹窗会等登录流程走完再出现。")]
    [SerializeField] private float autoLoginDelaySeconds = 0.5f;

    [Header("埋点")]
    [SerializeField] private bool enableAnalytics = true;
    [Tooltip("无版号的 TapTap 试玩版固定为 demo。")]
    [SerializeField] private bool analyticsBuildTypeIsDemo = true;
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

    /// <summary>首启需要弹「测试数据收集」但还没弹（等登录流程结束）。</summary>
    private bool pendingConsentPrompt;

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

        // 数据收集合规（2026-10-01 第三版）：
        //   * TapTap 登录不依赖同意，而且排在弹窗**之前**：启动即初始化 SDK（按上次的选择决定采不采集）并自动登录；
        //   * 「测试数据收集」只在首启（Undecided）问一次，问的时机是**登录流程结束之后**；
        //   * 首启未决时按“不采集”初始化（TapDB 开关只在 Init 时生效，运行期改不了），
        //     玩家同意后本会话的事件会落盘，下次启动自动补发（AnalyticsService.FlushPendingOnDisk）。
        pendingConsentPrompt = NeedsConsentPrompt;
        StartBackendWithCurrentConsent();
    }

    /// <summary>首启（还没做过选择）才需要弹「测试数据收集」；实际弹出时机在登录流程之后。</summary>
    private bool NeedsConsentPrompt =>
        EffectiveBackend != GameBackend.None && requirePrivacyConsent && !PrivacyConsentGate.HasDecided;

    /// <summary>
    /// 按当前同意状态启动后端，并定下本次启动的采集开关。幂等。
    /// </summary>
    public void StartBackendWithCurrentConsent()
    {
        if (IsInitialized)
            return;

        // TapDB 的采集开关只能在 Init 时决定；其它后端不受同意门控影响（开发/编辑器用）。
        bool collected = EffectiveBackend != GameBackend.TapTap
            || !requirePrivacyConsent
            || PrivacyConsentGate.HasConsented;

        collectionEnabledAtInit = collected;
        sessionCollectionEnabled = collected;
        AnalyticsService.SetSessionCollection(collected);
        AnalyticsService.SetEnabled(enableAnalytics && collected);

        if (!PrivacyConsentGate.HasConsented)
        {
            // 未同意（未决/已拒绝）：不采集，也不保留任何本地待发数据。
            AnalyticsService.DiscardPendingOnDisk();
        }
        else if (!collected)
        {
            Debug.Log("[GameInitializer] 本次启动不采集（启动时尚未取得同意）：事件会落盘，下次启动补发。");
        }

        InitializeBackend();
    }

    /// <summary>
    /// 玩家在设置界面改动「测试数据收集」开关时调用（`AnalyticsConsentToggleUI`），
    /// 也用于首次弹窗的选择（同意/不同意）。
    /// * 关闭：我们自己的上报立即停，并丢掉未上报的本地待发数据；原生预置事件下次启动起彻底停；
    /// * 打开：我们自己的上报立即恢复；若本进程原生采集未开启（首启未决/曾被拒绝），
    ///   接下来的事件会落盘，**下次启动自动补发**（原生开关运行期改不了）。
    /// </summary>
    public void ApplyConsentChange(bool consented)
    {
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
            AnalyticsService.DiscardPendingOnDisk();
            return;
        }

        if (!collectionEnabledAtInit)
        {
            Debug.Log("[GameInitializer] 已记录同意：本次启动原生采集未开启，接下来的事件会落盘，下次启动补发。");
        }
    }

    private void OnPrivacyConsentGranted()
    {
        PrivacyConsentGate.Grant();
        ApplyConsentChange(true);
    }

    private void OnPrivacyConsentRejected()
    {
        // 不同意只是不采集数据：TapTap 登录、存档、游玩全都照常（弹窗两个按钮都能继续玩）。
        PrivacyConsentGate.Decline();
        ApplyConsentChange(false);
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
            channel = string.IsNullOrEmpty(tapTapChannel) ? "default" : tapTapChannel,
            buildType = analyticsBuildTypeIsDemo ? AnalyticsBuildType.Demo : AnalyticsBuildType.Release,
            region = "CN",
            logToConsole = analyticsLogToConsole,
            writeLocalFile = analyticsWriteLocalFile
        };
        AnalyticsService.Init(config);

        // 设备属性（对应 TapDB 的「用户属性 - 设备」，见 Assets/Docs/userProp.csv）：
        // 这些是筛选/分部维度，不需要每个事件重复携带。
        AnalyticsService.SetDeviceProperty(AnalyticsProperty.AppVersion, Application.version);
        AnalyticsService.SetDeviceProperty(AnalyticsProperty.BuildType, config.buildType == AnalyticsBuildType.Demo ? "demo" : "release");
        AnalyticsService.SetDeviceProperty(AnalyticsProperty.Platform, Application.platform.ToString());
        AnalyticsService.SetDeviceProperty(AnalyticsProperty.Channel, config.channel);
        AnalyticsService.SetDeviceProperty(AnalyticsProperty.Region, config.region);
        AnalyticsService.SetDeviceProperty(AnalyticsProperty.Language, LocalizationSystem.CurrentLanguage);
        AnalyticsService.SetDeviceProperty(AnalyticsProperty.IsDev, Application.isEditor || Debug.isDebugBuild);
        AnalyticsService.SetDeviceProperty(AnalyticsProperty.SaveSlot, RunSaveSystem.CurrentSlotIndex);
        AnalyticsService.SetDeviceProperty(AnalyticsProperty.AscensionMax, ReadHighestUnlockedAscension());
        AnalyticsService.SetDeviceProperty(AnalyticsProperty.TutorialDone, ReadTutorialDone());
    }

    private static int ReadHighestUnlockedAscension()
    {
        try
        {
            UnlockProgressData progress = UnlockProgressSaveSystem.LoadCurrent();
            return progress != null ? progress.highestAscensionUnlocked : 0;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[GameInitializer] 读取解锁进度失败：" + exception.Message);
            return 0;
        }
    }

    private static bool ReadTutorialDone()
    {
        try
        {
            if (!RunSaveSystem.HasCurrentRun())
                return false;

            RunSaveData save = RunSaveSystem.LoadCurrentRun();
            return save != null && save.tutorialCompleted;
        }
        catch (Exception)
        {
            return false;
        }
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
        // 未同意时登录仍然照常（TapTap 平台默认账号），只是 TapDB 整个不启用。
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

    /// <summary>TapSDK 初始化成功：同意过则挂 TapDB 后端并上报设备属性；登录无论是否同意都发起。</summary>
    private void OnTapTapInitSuccess()
    {
        ReleaseInitCallback();
        IsInitialized = true;

        // 只在本次启动确实要采集时才挂 TapDB 后端（同意状态在 Init 之前就已确定）：
        // 之前入队的事件与已设置的设备属性会在挂载时自动补发/补报。
        if (enableAnalytics && collectionEnabledAtInit)
        {
            AnalyticsService.AddSink(new TapAnalyticsSink(analyticsLogToConsole));

            // 上次会话落盘的待发事件（首启同意后那一局、或开关重新打开后的数据）在这里补发。
            AnalyticsService.FlushPendingOnDisk();

            // TapDB 的设备注册在原生层也是异步的：首次设备属性上报可能撞上“设备初始化中”，
            // 延后再幂等重推一次（无害）。
            _ = RepushDevicePropertiesAfterDelayAsync();
        }

        // 登录与“是否同意采集数据”无关：登录始终尝试（TapPlay 默认账号），可取消。
        TapTapAuthService.NotifySdkAvailable(true);

        // 登录排在「测试数据收集」弹窗之前（首启时）：先走登录（静默/授权页/取消），再问采集。
        if (autoLoginOnStartup)
            _ = AutoLoginThenMaybePromptAsync();
        else
            ShowConsentPromptIfNeeded();
    }

    /// <summary>首启：先完成启动自动登录，再弹「测试数据收集」。</summary>
    private async Task AutoLoginThenMaybePromptAsync()
    {
        if (autoLoginDelaySeconds > 0f)
            await Task.Delay(Mathf.RoundToInt(autoLoginDelaySeconds * 1000f));

        await TapTapAuthService.TryAutoLoginAsync();
        ShowConsentPromptIfNeeded();
    }

    /// <summary>需要时弹出一次「测试数据收集」（首启、且还没问过）。</summary>
    private void ShowConsentPromptIfNeeded()
    {
        if (!pendingConsentPrompt)
            return;

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

        // SDK 挂了也把首启的询问弹出来（记录选择；下次启动若能初始化成功就按该选择生效）。
        ShowConsentPromptIfNeeded();
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
