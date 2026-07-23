using TapSDK.Core;
using UnityEngine;

public enum GameBackend
{
    None,
    TapTap,
    Steam
}

[CreateAssetMenu(fileName = "GameBackendLocalCredentials", menuName = "Config/Game Backend Local Credentials")]
public class GameBackendLocalCredentials : ScriptableObject
{
    [SerializeField] private string tapTapClientId;
    [SerializeField] private string tapTapClientToken;

    public string TapTapClientId => tapTapClientId;
    public string TapTapClientToken => tapTapClientToken;
    public bool HasTapTapCredentials => !string.IsNullOrWhiteSpace(tapTapClientId) && !string.IsNullOrWhiteSpace(tapTapClientToken);
}

[DefaultExecutionOrder(-1000)]
public class GameInitializer : MonoBehaviour
{
    private const string CredentialsResourcePath = "LocalSettings/GameBackendLocalCredentials";

    public static GameInitializer Instance { get; private set; }

    [SerializeField] private GameBackend backend = GameBackend.None;
    [SerializeField] private string tapTapChannel = "default";

    public GameBackend Backend => backend;
    public bool IsInitialized { get; private set; }

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
        InitializeBackend();
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
        switch (backend)
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
        TapTapEventOptions eventOptions = new TapTapEventOptions
        {
            channel = tapTapChannel,
            enableTapTapEvent = true,
            enableAutoIAPEvent = false
        };

        TapTapSDK.Init(coreOptions, new TapTapSdkBaseOptions[] { eventOptions });
        IsInitialized = true;
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
