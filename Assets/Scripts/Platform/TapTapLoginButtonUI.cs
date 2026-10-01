using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// TapTap 登录按钮。按钮本身与文案由美术在场景里搭好（按官方《登录按钮设计规范》绘制），
/// 这个组件只负责：把点击接到登录流程、按登录状态切换按钮可用性与状态对象的显隐。
/// 不在运行时改写美术设置的文案与颜色。
/// </summary>
public class TapTapLoginButtonUI : MonoBehaviour
{
    [SerializeField] private Button loginButton;

    [Tooltip("埋点用登录入口标识，例如 start_menu / settings。")]
    [SerializeField] private string entry = "start_menu";

    [Header("状态显隐（只切显隐，不改文案）")]
    [SerializeField] private GameObject loggedOutState;
    [SerializeField] private GameObject loggedInState;

    private bool busy;

    private void Awake()
    {
        if (loginButton != null)
            loginButton.onClick.AddListener(OnClick);
    }

    private void OnEnable()
    {
        TapTapAuthService.LoginStateChanged += OnLoginStateChanged;
        Refresh();
    }

    private void OnDisable()
    {
        TapTapAuthService.LoginStateChanged -= OnLoginStateChanged;
    }

    private void Start()
    {
        Refresh();
    }

    private async void OnClick()
    {
        if (busy || TapTapAuthService.IsLoggedIn)
            return;

        busy = true;
        Refresh();
        LoginOutcome outcome = await TapTapAuthService.LoginAsync(entry);
        busy = false;

        if (outcome == LoginOutcome.Unavailable)
            Debug.LogWarning("[TapTapLoginButton] 当前不可登录（TapSDK 未就绪）。");

        Refresh();
    }

    private void OnLoginStateChanged(bool loggedIn)
    {
        Refresh();
    }

    /// <summary>刷新按钮可用性与状态对象显隐。</summary>
    public void Refresh()
    {
        bool loggedIn = TapTapAuthService.IsLoggedIn;

        if (loginButton != null)
            loginButton.interactable = TapTapAuthService.SdkAvailable && !loggedIn && !busy;

        if (loggedOutState != null)
            loggedOutState.SetActive(!loggedIn);

        if (loggedInState != null)
            loggedInState.SetActive(loggedIn);
    }
}
