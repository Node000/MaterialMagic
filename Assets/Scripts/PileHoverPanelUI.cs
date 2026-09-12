using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 战斗界面牌堆按钮的 hover 预览：只显示当前 hover 的那一堆箭头（单行）。
/// 与点击打开的 MaterialListPanelUI（三行“箭头牌堆”面板）互不影响：本面板不拦截射线、不参与选择流程。
/// </summary>
public class PileHoverPanelUI : MonoBehaviour
{
    public enum PileKind
    {
        Draw,
        Discard,
        Consumed
    }

    private const string LayoutConfigResourcePath = "Config/MaterialListPanelLayoutConfig";
    private const string DrawTitleKey = "ui.material_list.draw_pile";
    private const string DiscardTitleKey = "ui.material_list.discard_pile";
    private const string ConsumedTitleKey = "ui.material_list.consumed_pile";
    private const string DrawTitleFallback = "抽牌堆";
    private const string DiscardTitleFallback = "弃牌堆";
    private const string ConsumedTitleFallback = "已消耗";
    private const string EmptyPileTextKey = "ui.material_list.empty_pile";
    private const string EmptyPileTextFallback = "没有箭头";

    [Header("引用")]
    [SerializeField] private RectTransform panelRoot;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private BattleMaterialRowUI pileRow;
    [SerializeField] private MaterialListPanelUI rowOwner;
    [SerializeField] private MaterialListPanelLayoutConfig layoutConfig;

    [Header("显示计时")]
    [SerializeField] private float showDelay = 0.2f;
    [SerializeField] private float hideDelay = 0.08f;

    [Header("定位")]
    [SerializeField] private float anchorOffsetY = 12f;
    [SerializeField] private float screenMargin = 16f;

    [Header("动画")]
    [SerializeField] private float fadeInDuration = 0.12f;
    [SerializeField] private float fadeOutDuration = 0.1f;
    [SerializeField] private float hiddenScale = 0.96f;
    [SerializeField] private Ease showEase = Ease.OutCubic;
    [SerializeField] private Ease hideEase = Ease.InCubic;

    [Header("整体缩放")]
    [SerializeField] private float panelScale = 0.5f;

    [Header("行布局")]
    [SerializeField] private float rowHoverYOffset = 32f;
    [SerializeField] private float rowHoverCurvePower = 1.35f;

    private HandSystemUI owner;
    private MaterialListPanelLayoutConfig cachedLayoutConfig;
    private Tween showDelayTween;
    private Tween hideDelayTween;
    private Tween fadeTween;
    private PileKind currentKind;
    private RectTransform currentAnchor;
    private RectTransform pendingAnchor;
    private PileKind pendingKind;
    private bool hasPendingRequest;

    public bool IsPanelVisible => gameObject.activeSelf && canvasGroup != null && canvasGroup.alpha > 0.001f;

    public void Initialize(HandSystemUI handSystemUI)
    {
        owner = handSystemUI;
        CacheReferences();
        HideImmediate();
        LocalizationSystem.LanguageChanged -= HandleLanguageChanged;
        LocalizationSystem.LanguageChanged += HandleLanguageChanged;
    }

    private void OnDestroy()
    {
        LocalizationSystem.LanguageChanged -= HandleLanguageChanged;
        KillTweens();
    }

    /// <summary>请求显示某一堆的预览；延迟时间内再次请求会复用同一面板，不做淡出淡入。</summary>
    public void Show(PileKind kind, RectTransform anchor)
    {
        if (anchor == null)
        {
            Hide();
            return;
        }

        CancelHideDelay();

        if (IsPanelVisible)
        {
            CancelShowDelay();
            hasPendingRequest = false;
            Apply(kind, anchor, false);
            return;
        }

        pendingKind = kind;
        pendingAnchor = anchor;
        hasPendingRequest = true;
        CancelShowDelay();

        float delay = Mathf.Max(0f, showDelay);
        if (delay <= 0f)
        {
            ShowPending();
            return;
        }

        showDelayTween = DOVirtual.DelayedCall(delay, ShowPending);
    }

    /// <summary>离开图标后延迟隐藏。</summary>
    public void Hide()
    {
        CancelShowDelay();
        hasPendingRequest = false;

        if (!gameObject.activeSelf)
            return;

        float delay = Mathf.Max(0f, hideDelay);
        if (delay <= 0f)
        {
            HideInternal();
            return;
        }

        CancelHideDelay();
        hideDelayTween = DOVirtual.DelayedCall(delay, HideInternal);
    }

    /// <summary>只有当前显示的堆与请求的堆一致时才隐藏，避免相邻图标反复划过时互相取消。</summary>
    public void Hide(PileKind kind)
    {
        if (IsPanelVisible && currentKind != kind)
            return;

        Hide();
    }

    /// <summary>立即收起（打开整块牌堆面板、初始化、切换场景时使用）。</summary>
    public void HideImmediate()
    {
        CancelShowDelay();
        CancelHideDelay();
        hasPendingRequest = false;
        KillFadeTween();
        CacheReferences();

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        transform.localScale = GetBaseScale();
        gameObject.SetActive(false);
    }

    private void ShowPending()
    {
        showDelayTween = null;
        if (this == null || !hasPendingRequest)
            return;

        hasPendingRequest = false;
        Apply(pendingKind, pendingAnchor, true);
    }

    private void Apply(PileKind kind, RectTransform anchor, bool animate)
    {
        PlayerState state = owner != null ? owner.PlayerState : null;
        if (state == null || anchor == null)
        {
            HideImmediate();
            return;
        }

        currentKind = kind;
        currentAnchor = anchor;

        gameObject.SetActive(true);
        CacheReferences();
        EnsureNonBlocking();
        transform.SetAsLastSibling();

        if (titleText != null)
            titleText.text = GetPileTitle(kind);

        RefreshRow(kind, state);
        PositionNear(anchor);

        if (animate)
            PlayShowAnimation();
        else
            ShowInstant();
    }

    private void HideInternal()
    {
        hideDelayTween = null;
        if (this == null)
            return;

        CancelShowDelay();
        hasPendingRequest = false;

        if (!gameObject.activeSelf)
        {
            if (canvasGroup != null)
                canvasGroup.alpha = 0f;
            return;
        }

        KillFadeTween();

        float duration = Mathf.Max(0.01f, fadeOutDuration);
        Sequence sequence = DOTween.Sequence().SetTarget(this);
        sequence.Join(canvasGroup.DOFade(0f, duration).SetEase(hideEase));
        sequence.Join(transform.DOScale(GetHiddenPanelScale(), duration).SetEase(hideEase));
        sequence.OnComplete(() =>
        {
            fadeTween = null;
            if (this == null)
                return;

            transform.localScale = GetBaseScale();
            gameObject.SetActive(false);
        });
        fadeTween = sequence;
    }

    private void PlayShowAnimation()
    {
        KillFadeTween();
        canvasGroup.alpha = 0f;
        transform.localScale = GetHiddenPanelScale();

        float duration = Mathf.Max(0.01f, fadeInDuration);
        Sequence sequence = DOTween.Sequence().SetTarget(this);
        sequence.Join(canvasGroup.DOFade(1f, duration).SetEase(showEase));
        sequence.Join(transform.DOScale(GetBaseScale(), duration).SetEase(showEase));
        sequence.OnComplete(() =>
        {
            fadeTween = null;
            if (this != null)
                transform.localScale = GetBaseScale();
        });
        fadeTween = sequence;
    }

    private void ShowInstant()
    {
        KillFadeTween();
        canvasGroup.alpha = 1f;
        transform.localScale = GetBaseScale();
    }

    private void RefreshRow(PileKind kind, PlayerState state)
    {
        if (pileRow == null)
            return;

        pileRow.gameObject.SetActive(true);
        pileRow.SetOwnerPanel(ResolveRowOwner());
        pileRow.SetHoverSelectionOutlineEnabled(false);

        MaterialListPanelLayoutConfig config = GetLayoutConfig();
        float rowLength = config != null ? config.ArrowRowTotalLength : 780f;
        float defaultScale = config != null ? config.ArrowDefaultScale : 0.72f;
        float hoverScale = config != null ? config.ArrowHoverScale : 1.18f;
        pileRow.ConfigureArrowRowLayout(rowLength, defaultScale, hoverScale, rowHoverYOffset, rowHoverCurvePower);
        pileRow.Refresh(GetPileTitle(kind), GetPileMaterials(kind, state), null, null, false, EmptyPileTextKey, EmptyPileTextFallback);

        DisableRowInteraction();
    }

    /// <summary>预览只做展示：卡片实例化后 MaterialCardView 会重新打开射线，这里统一关闭，保证点击可以穿透。</summary>
    private void DisableRowInteraction()
    {
        if (pileRow == null)
            return;

        Graphic[] graphics = pileRow.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
            graphics[i].raycastTarget = false;

        CanvasGroup[] groups = pileRow.GetComponentsInChildren<CanvasGroup>(true);
        for (int i = 0; i < groups.Length; i++)
        {
            groups[i].blocksRaycasts = false;
            groups[i].interactable = false;
        }

        BattleMaterialRowItemUI[] items = pileRow.GetComponentsInChildren<BattleMaterialRowItemUI>(true);
        for (int i = 0; i < items.Length; i++)
            items[i].enabled = false;
    }

    private void EnsureNonBlocking()
    {
        Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
            graphics[i].raycastTarget = false;

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }

    private void PositionNear(RectTransform anchor)
    {
        RectTransform root = panelRoot != null ? panelRoot : transform as RectTransform;
        if (root == null || anchor == null)
            return;

        RectTransform parentRect = root.parent as RectTransform;
        if (parentRect == null)
            return;

        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.zero;
        root.pivot = new Vector2(0.5f, 0f);

        Camera eventCamera = GetCanvasCamera();
        Vector3[] corners = new Vector3[4];
        anchor.GetWorldCorners(corners);
        Vector3 topCenter = (corners[1] + corners[2]) * 0.5f;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(eventCamera, topCenter);

        Vector2 localPoint;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, eventCamera, out localPoint))
            return;

        Rect parentSize = parentRect.rect;
        Vector2 cornerPoint = localPoint - parentSize.min;

        Vector3 baseScale = GetBaseScale();
        float halfWidth = root.rect.width * root.pivot.x * baseScale.x;
        float height = root.rect.height * (1f - root.pivot.y) * baseScale.y;
        float margin = Mathf.Max(0f, screenMargin);

        float minX = margin + halfWidth;
        float maxX = Mathf.Max(minX, parentSize.width - margin - halfWidth);
        float minY = margin;
        float maxY = Mathf.Max(minY, parentSize.height - margin - height);

        float x = Mathf.Clamp(cornerPoint.x, minX, maxX);
        float y = Mathf.Clamp(cornerPoint.y + anchorOffsetY, minY, maxY);
        root.anchoredPosition = new Vector2(x, y);
    }

    private IReadOnlyList<MaterialModel> GetPileMaterials(PileKind kind, PlayerState state)
    {
        switch (kind)
        {
            case PileKind.Draw:
                return state.DrawPile;
            case PileKind.Discard:
                return state.DiscardPile;
            default:
                return state.ConsumedPile;
        }
    }

    private string GetPileTitle(PileKind kind)
    {
        switch (kind)
        {
            case PileKind.Draw:
                return LocalizationSystem.GetText(DrawTitleKey, DrawTitleFallback);
            case PileKind.Discard:
                return LocalizationSystem.GetText(DiscardTitleKey, DiscardTitleFallback);
            default:
                return LocalizationSystem.GetText(ConsumedTitleKey, ConsumedTitleFallback);
        }
    }

    private MaterialListPanelUI ResolveRowOwner()
    {
        if (rowOwner != null)
            return rowOwner;

        return owner != null ? owner.GetUIManager().MaterialListPanel : null;
    }

    private MaterialListPanelLayoutConfig GetLayoutConfig()
    {
        if (layoutConfig != null)
            return layoutConfig;

        if (cachedLayoutConfig == null)
            cachedLayoutConfig = Resources.Load<MaterialListPanelLayoutConfig>(LayoutConfigResourcePath);
        return cachedLayoutConfig;
    }

    private Camera GetCanvasCamera()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;

        return canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
    }

    private float GetHiddenScale()
    {
        return Mathf.Clamp(hiddenScale, 0.01f, 1f);
    }

    private Vector3 GetBaseScale()
    {
        float scale = Mathf.Clamp(panelScale, 0.05f, 4f);
        return new Vector3(scale, scale, 1f);
    }

    private Vector3 GetHiddenPanelScale()
    {
        Vector3 baseScale = GetBaseScale();
        float scale = GetHiddenScale();
        return new Vector3(baseScale.x * scale, baseScale.y * scale, 1f);
    }

    private void HandleLanguageChanged()
    {
        if (this == null || !IsPanelVisible || currentAnchor == null)
            return;

        Apply(currentKind, currentAnchor, false);
    }

    private void CacheReferences()
    {
        if (panelRoot == null)
            panelRoot = transform as RectTransform;

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (titleText == null)
            titleText = UIManager.FindChildComponent<TMP_Text>(transform, "Title");

        if (pileRow == null)
            pileRow = UIManager.FindChildComponent<BattleMaterialRowUI>(transform, "PileRow");
    }

    private void CancelShowDelay()
    {
        showDelayTween?.Kill(false);
        showDelayTween = null;
    }

    private void CancelHideDelay()
    {
        hideDelayTween?.Kill(false);
        hideDelayTween = null;
    }

    private void KillFadeTween()
    {
        fadeTween?.Kill(false);
        fadeTween = null;
    }

    private void KillTweens()
    {
        CancelShowDelay();
        CancelHideDelay();
        KillFadeTween();
    }

    private void OnDisable()
    {
        KillTweens();
        hasPendingRequest = false;
    }
}
