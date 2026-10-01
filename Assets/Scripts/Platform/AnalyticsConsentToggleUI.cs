using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 设置面板里的「测试数据收集」开关。
///
/// 美术在 Scene 的 SettingsPanel 里搭好一行（文字 + Toggle），把 Toggle 拖进
/// <see cref="sharingToggle"/>；代码只负责读写状态与切换可选的状态视觉对象，
/// **不写文案、不写颜色**（见 `memory/user-preference.md`）。
///
/// 语义（与 `plan/隐私授权与隐私政策软化改造.md` §7 一致）：
/// * 打开：记录同意 → 我们自己的上报立即恢复；但假设本次启动时 SDK 是按“不同意”
///   初始化的（TapDB 采集开关只在 Init 时生效，运行期改不了），则真正的收集要从**下次启动**开始；
/// * 关闭：记录拒绝 → 我们自己的事件**立即**停发并清空待发队列；TapDB 预置事件
///   （启动/时长）要到**下次启动**才彻底停止（下次启动会以 `enableTapTapEvent=false` 初始化）；
/// * 不管开关怎么动，**TapTap 登录都不受影响**（不需要重新登录或登出）；
/// * 首次启动的询问结果与这个开关共用同一份状态（`PrivacyConsentGate`），不会两处不一致。
/// </summary>
public class AnalyticsConsentToggleUI : MonoBehaviour
{
    [Header("绑定（美术在设置面板里搭好后拖进来）")]
    [Tooltip("设置面板里的「测试数据收集」Toggle。")]
    [SerializeField] private Toggle sharingToggle;
    [Tooltip("可选：开启状态下的视觉对象（如“开”的图标/底图），代码只切显隐。")]
    [SerializeField] private GameObject onStateRoot;
    [Tooltip("可选：关闭状态下的视觉对象。")]
    [SerializeField] private GameObject offStateRoot;

    private bool suppressCallback;

    private void OnEnable()
    {
        if (sharingToggle == null)
            return;

        // 回填当前状态：这是“同步显示”，不是玩家的修改，不能触发写状态。
        suppressCallback = true;
        sharingToggle.SetIsOnWithoutNotify(PrivacyConsentGate.HasConsented);
        suppressCallback = false;

        ApplyVisual(PrivacyConsentGate.HasConsented);
        sharingToggle.onValueChanged.AddListener(OnToggleChanged);
    }

    private void OnDisable()
    {
        if (sharingToggle != null)
            sharingToggle.onValueChanged.RemoveListener(OnToggleChanged);
    }

    private void OnToggleChanged(bool isOn)
    {
        if (suppressCallback)
            return;

        SetSharing(isOn);
    }

    /// <summary>
    /// 供其它入口直接调用（也是真机验收的入口）：写入同意状态并即时生效。
    /// </summary>
    public void SetSharing(bool sharing)
    {
        if (sharing)
            PrivacyConsentGate.Grant();
        else
            PrivacyConsentGate.Decline();

        if (GameInitializer.Instance != null)
        {
            // 由初始化器统一处理：立即停/启我们自己的上报；若本次启动的采集开关已定为关闭，
            // 打开会记录到下个启动生效。
            GameInitializer.Instance.ApplyConsentChange(sharing);
        }
        else
        {
            AnalyticsService.SetEnabled(sharing);
        }

        Debug.Log(sharing
            ? "[AnalyticsConsent] 打开「测试数据收集」：记录同意。"
            : "[AnalyticsConsent] 关闭「测试数据收集」：本地事件立即停发，下次启动起完全不上报。");

        if (sharingToggle != null && sharingToggle.isOn != sharing)
        {
            suppressCallback = true;
            sharingToggle.SetIsOnWithoutNotify(sharing);
            suppressCallback = false;
        }

        ApplyVisual(sharing);
    }

    private void ApplyVisual(bool sharing)
    {
        if (onStateRoot != null)
            onStateRoot.SetActive(sharing);
        if (offStateRoot != null)
            offStateRoot.SetActive(!sharing);
    }
}
