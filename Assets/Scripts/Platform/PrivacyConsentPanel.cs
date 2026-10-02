using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 「测试数据收集」询问弹窗（原“隐私政策同意弹窗”，2026-10-01 改版）。
///
/// 语义：两个按钮是「同意 / 不同意」，**两个都能继续游玩**；同意只影响是否采集测试数据，
/// 不影响 TapTap 登录与任何游戏内容（登录由 TapTap 平台默认接入）。
///
/// 两种用法：
/// 1. **美术面板（正式）**：把面板搭在场景里，把 <see cref="panelRoot"/>、<see cref="bodyText"/>、
///    两个按钮按 Inspector 拖拽绑定，然后关闭
///    <see cref="buildFallbackPanelWhenUnbound"/>。代码只负责显隐与点击回调，
///    不改写美术面板上的任何文案与颜色。**面板上不要放隐私政策按钮**（游戏内不提供该入口）。
/// 2. **临时兜底面板**：没有绑定任何引用时，代码会生成一个纯功能性的弹窗，便于真机联调；
///    美术面板就绪后请关闭该开关。
/// </summary>
public class PrivacyConsentPanel : MonoBehaviour
{
    [Header("绑定美术面板（留空则使用临时兜底面板）")]
    [SerializeField] private RectTransform panelRoot;
    [Tooltip("如果美术把半透明遮罩做成了面板的兄弟节点，把它拖到这里；留空则只切换 panelRoot。兜底面板会连同它自己的整屏遮罩一起隐藏。")]
    [SerializeField] private GameObject dimOverlay;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private Button agreeButton;
    [SerializeField] private Button rejectButton;
    [SerializeField] private Button policyButton;

    [Header("临时兜底面板（美术面板就绪后请关闭）")]
    [SerializeField] private bool buildFallbackPanelWhenUnbound = true;
    [SerializeField] private string title = "测试数据收集";
    [Tooltip("暂不使用：按 2026-10-01 决定，游戏内不再提供隐私政策入口。")]
    [SerializeField] private string policyUrl = "https://docs.qq.com/doc/DYWpLSkRzU1pLekRC";
    [TextArea(3, 20)]
    [Tooltip("正式美术面板就绪后，文案以美术在场景里设置的为准（代码不向美术面板写文案）。")]
    [SerializeField]
    private string summary =
        "欢迎游玩《有氧地下城》！\n" +
        "\n" +
        "我们会收集您在游戏过程中的道具选取、事件选择、通关数据等游戏内数据，来辅助我们进行内容开发与平衡性调整。\n" +
        "\n" +
        "您随时可以在设置界面关闭这一功能。";
    [SerializeField] private string agreeLabel = "同意";
    [SerializeField] private string rejectLabel = "不同意";
    [Tooltip("暂不使用：游戏内不再提供隐私政策入口。")]
    [SerializeField] private string policyLabel = "查看完整的《隐私政策》";

    private Action onAgree;
    private Action onReject;
    private bool bound;

    /// <summary>
    /// 场景里的面板可能被美术临时打开（调样式时）；一旦玩家已经做过选择，这里立即收起。
    /// 注意：节点在场景里被置为 inactive 时不会再走到这里，那种情况下本就什么都不显示。
    /// </summary>
    private void Awake()
    {
        if (PrivacyConsentGate.HasDecided)
            SetVisible(false);
    }

    /// <summary>兜底面板的 Canvas 根（整屏遮罩 + 面板都在它下面）。</summary>
    private GameObject fallbackCanvasRoot;

    /// <summary>没有场景面板时，用代码生成一个临时兜底弹窗。</summary>
    public static PrivacyConsentPanel CreateRuntimeFallback()
    {
        GameObject root = new GameObject("PrivacyConsentPanel(临时兜底)");
        DontDestroyOnLoad(root);
        PrivacyConsentPanel panel = root.AddComponent<PrivacyConsentPanel>();
        panel.buildFallbackPanelWhenUnbound = true;
        return panel;
    }

    public void Show(Action agree, Action reject)
    {
        onAgree = agree;
        onReject = reject;

        if (panelRoot == null && buildFallbackPanelWhenUnbound)
            BuildFallbackPanel();

        Bind();

        if (panelRoot == null)
        {
            Debug.LogError("[PrivacyConsent] 没有可用的隐私弹窗面板：请在场景里绑定面板引用，或打开临时兜底面板开关。");
            return;
        }

        SetVisible(true);
    }

    public void Hide()
    {
        SetVisible(false);
    }

    /// <summary>
    /// 统一切换显隐。**必须连整屏遮罩一起关**：否则面板看似消失，但全屏 Image 还开着，
    /// 它 raycastTarget=true 会把所有点击吃掉（曾导致同意后无法交互）。
    /// 同时保证整条链都被激活（面板根节点在场景里可能是 inactive 的默认态）。
    /// </summary>
    private void SetVisible(bool visible)
    {
        if (fallbackCanvasRoot != null)
            fallbackCanvasRoot.SetActive(visible);

        if (visible && !gameObject.activeSelf)
            gameObject.SetActive(true);

        if (panelRoot != null)
            panelRoot.gameObject.SetActive(visible);

        if (dimOverlay != null)
            dimOverlay.SetActive(visible);
    }

    private void Bind()
    {
        if (bound)
            return;

        bound = true;
        if (agreeButton != null)
            agreeButton.onClick.AddListener(HandleAgree);
        if (rejectButton != null)
            rejectButton.onClick.AddListener(HandleReject);
        if (policyButton != null)
        {
            // 2026-10-01 决定：游戏内不再提供隐私政策入口，因此不绑定该按钮。
            // （字段保留供日后审核要求时重新启用。）
            policyButton.onClick.RemoveListener(OpenPolicy);
        }
    }

    private void HandleAgree()
    {
        Hide();
        onAgree?.Invoke();
    }

    private void HandleReject()
    {
        Hide();
        onReject?.Invoke();
    }

    private void OpenPolicy()
    {
        if (string.IsNullOrEmpty(policyUrl))
            return;

        try { Application.OpenURL(policyUrl); }
        catch (Exception exception) { Debug.LogWarning("[PrivacyConsent] 打开隐私政策失败：" + exception.Message); }
    }

    private void BuildFallbackPanel()
    {
        GameObject canvasObject = new GameObject("PrivacyConsentCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        fallbackCanvasRoot = canvasObject;
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform overlay = CreateRect("Overlay", canvasObject.transform);
        Stretch(overlay);
        Image overlayImage = overlay.gameObject.AddComponent<Image>();
        overlayImage.color = new Color(0f, 0f, 0f, 0.85f);

        panelRoot = CreateRect("Panel", overlay);
        panelRoot.anchorMin = new Vector2(0.5f, 0.5f);
        panelRoot.anchorMax = new Vector2(0.5f, 0.5f);
        panelRoot.pivot = new Vector2(0.5f, 0.5f);
        panelRoot.anchoredPosition = Vector2.zero;
        panelRoot.sizeDelta = new Vector2(1180f, 580f);
        Image panelImage = panelRoot.gameObject.AddComponent<Image>();
        panelImage.color = new Color(0.10f, 0.11f, 0.14f, 0.98f);

        TMP_FontAsset font = UIManager.GetDefaultTMPFont();

        TMP_Text titleText = CreateText("Title", panelRoot, font, 46f, TextAlignmentOptions.TopLeft);
        titleText.text = title;
        RectTransform titleRect = (RectTransform)titleText.transform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -40f);
        titleRect.sizeDelta = new Vector2(-80f, 70f);

        bodyText = CreateText("Body", panelRoot, font, 28f, TextAlignmentOptions.TopLeft);
        bodyText.text = summary;
        RectTransform bodyRect = (RectTransform)bodyText.transform;
        bodyRect.anchorMin = new Vector2(0f, 1f);
        bodyRect.anchorMax = new Vector2(1f, 1f);
        bodyRect.pivot = new Vector2(0.5f, 1f);
        bodyRect.anchoredPosition = new Vector2(0f, -130f);
        bodyRect.sizeDelta = new Vector2(-80f, 250f);

        agreeButton = CreateButton("AgreeButton", panelRoot, font, agreeLabel, new Color(0.30f, 0.72f, 0.42f, 1f));
        RectTransform agreeRect = (RectTransform)agreeButton.transform;
        agreeRect.anchorMin = new Vector2(0.5f, 0f);
        agreeRect.anchorMax = new Vector2(0.5f, 0f);
        agreeRect.pivot = new Vector2(0.5f, 0f);
        agreeRect.anchoredPosition = new Vector2(150f, 60f);
        agreeRect.sizeDelta = new Vector2(240f, 90f);

        rejectButton = CreateButton("RejectButton", panelRoot, font, rejectLabel, new Color(0.32f, 0.33f, 0.36f, 1f));
        RectTransform rejectRect = (RectTransform)rejectButton.transform;
        rejectRect.anchorMin = new Vector2(0.5f, 0f);
        rejectRect.anchorMax = new Vector2(0.5f, 0f);
        rejectRect.pivot = new Vector2(0.5f, 0f);
        rejectRect.anchoredPosition = new Vector2(-150f, 60f);
        rejectRect.sizeDelta = new Vector2(240f, 90f);
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
    }

    private static TMP_Text CreateText(string name, Transform parent, TMP_FontAsset font, float fontSize, TextAlignmentOptions alignment)
    {
        RectTransform rect = CreateRect(name, parent);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null)
            text.font = font;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.enableWordWrapping = true;
        text.color = new Color(0.94f, 0.94f, 0.94f, 1f);
        return text;
    }

    private static Button CreateButton(string name, Transform parent, TMP_FontAsset font, string label, Color background)
    {
        RectTransform rect = CreateRect(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = background;
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        TMP_Text text = CreateText("Label", rect, font, 32f, TextAlignmentOptions.Center);
        text.text = label;
        Stretch((RectTransform)text.transform);
        return button;
    }
}
