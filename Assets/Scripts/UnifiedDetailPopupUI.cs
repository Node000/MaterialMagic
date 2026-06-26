using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UnifiedDetailPopupUI : MonoBehaviour, IBeginDragHandler, IEndDragHandler, IScrollHandler, IPointerDownHandler
{
    [SerializeField] private UnifiedDetailPopupTheme theme;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image borderImage;
    [SerializeField] private Graphic borderGraphic;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform frameRoot;
    [SerializeField] private RectTransform iconRect;
    [SerializeField] private ScrollRect bodyScrollRect;
    [SerializeField] private RectTransform bodyViewport;
    [SerializeField] private RectTransform bodyContent;
    [SerializeField] private UnifiedDetailTextConfig textConfig;
    [SerializeField] private AddedDetailedUI addedDetailPrefab;
    [SerializeField] private RectTransform addedDetailRoot;
    [SerializeField] private float autoScrollStartDelay = 1.2f;
    [SerializeField] private float autoScrollDuration = 3f;
    [SerializeField] private float autoScrollPause = 1.2f;
    [SerializeField] private float manualScrollResumeDelay = 2f;

    private Tween tween;
    private Tween autoScrollTween;
    private Tween hideDelayTween;
    private object currentAnchor;
    private object pendingHideAnchor;
    private bool pinned;
    private readonly List<AddedDetailedUI> addedDetailItems = new List<AddedDetailedUI>();
    private bool draggingBody;
    private float manualScrollResumeTime;
    private bool bodyCanScroll;
    private int pinnedFrame;

    public void Initialize()
    {
        CacheReferences();
        ApplyThemeLayout();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        transform.localScale = GetHiddenScale();
        gameObject.SetActive(false);
    }

    public void Show(object anchor, UnifiedDetailContent content)
    {
        CancelScheduledHide();
        if (pinned)
        {
            currentAnchor = anchor;
            UpdateVisibleContent(content, true);
            return;
        }

        currentAnchor = anchor;
        if (IsPopupVisibleOrTransitioning())
            UpdateVisibleContent(content, false);
        else
            ShowInternal(content, false);
    }

    public void Pin(object anchor, UnifiedDetailContent content)
    {
        CancelScheduledHide();
        pinned = true;
        pinnedFrame = Time.frameCount;
        currentAnchor = anchor;
        if (IsPopupVisibleOrTransitioning() || IsPopupShowingAnimation())
            UpdateVisibleContent(content, true);
        else
            ShowInternal(content, true);
    }

    public void Unpin()
    {
        pinned = false;
        BeginHideAnimation(true);
    }

    public bool IsPinnedFor(object anchor)
    {
        return pinned && currentAnchor != null && ReferenceEquals(anchor, currentAnchor);
    }

    public bool ContainsScreenPoint(Vector2 screenPosition)
    {
        Camera eventCamera = GetEventCamera();
        RectTransform rectTransform = transform as RectTransform;
        if (rectTransform != null && RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPosition, eventCamera))
            return true;

        for (int i = 0; i < addedDetailItems.Count; i++)
        {
            AddedDetailedUI item = addedDetailItems[i];
            RectTransform itemRect = item != null && item.gameObject.activeInHierarchy ? item.RectTransform : null;
            if (itemRect != null && RectTransformUtility.RectangleContainsScreenPoint(itemRect, screenPosition, eventCamera))
                return true;
        }
        return false;
    }

    public void Hide(object anchor)
    {
        if (pinned)
            return;
        if (anchor != null && currentAnchor != null && !ReferenceEquals(anchor, currentAnchor))
            return;
        if (!IsPopupVisibleOrTransitioning())
            return;

        ScheduleHide(anchor);
    }

    private void ScheduleHide(object anchor)
    {
        CancelScheduledHide();
        float delay = GetHoverExitHideDelay();
        if (delay <= 0f)
        {
            BeginHideAnimation(false);
            return;
        }

        pendingHideAnchor = anchor;
        hideDelayTween = DOVirtual.DelayedCall(delay, CompleteScheduledHide, true).SetTarget(this);
    }

    private void CompleteScheduledHide()
    {
        hideDelayTween = null;
        if (pinned)
            return;
        if (pendingHideAnchor != null && currentAnchor != null && !ReferenceEquals(pendingHideAnchor, currentAnchor))
        {
            pendingHideAnchor = null;
            return;
        }

        pendingHideAnchor = null;
        BeginHideAnimation(false);
    }

    private void CancelScheduledHide()
    {
        pendingHideAnchor = null;
        hideDelayTween?.Kill(false);
        hideDelayTween = null;
    }

    private void BeginHideAnimation(bool cancelScheduled)
    {
        if (cancelScheduled)
            CancelScheduledHide();
        currentAnchor = null;
        StopAutoScroll();
        tween?.Kill(false);
        Sequence sequence = DOTween.Sequence().SetTarget(this);
        sequence.Join(canvasGroup.DOFade(0f, GetFadeDuration()));
        sequence.Join(transform.DOScale(GetHiddenScale(), GetScaleDuration()).SetEase(GetHideEase()));
        sequence.OnComplete(() => gameObject.SetActive(false));
        tween = sequence;
    }

    public void HideImmediate()
    {
        currentAnchor = null;
        pinned = false;
        CancelScheduledHide();
        StopAutoScroll();
        tween?.Kill(false);
        CacheReferences();
        canvasGroup.alpha = 0f;
        transform.localScale = GetHiddenScale();
        gameObject.SetActive(false);
    }

    private bool IsPopupShowingAnimation()
    {
        return tween != null && tween.IsActive() && tween.IsPlaying() && canvasGroup != null && canvasGroup.alpha < 0.999f;
    }

    private bool IsPopupVisibleOrTransitioning()
    {
        return gameObject.activeSelf || (canvasGroup != null && canvasGroup.alpha > 0.001f);
    }

    private void ShowInternal(UnifiedDetailContent content, bool pinnedDisplay)
    {
        CancelScheduledHide();
        CacheReferences();
        ApplyThemeLayout();
        ApplyContent(content);
        gameObject.SetActive(true);
        PopupLayerUtility.ApplyTo((RectTransform)transform);
        transform.SetAsLastSibling();
        transform.localScale = GetHiddenScale();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = pinnedDisplay;
        canvasGroup.interactable = pinnedDisplay;
        tween?.Kill(false);
        Sequence sequence = DOTween.Sequence().SetTarget(this);
        sequence.Join(canvasGroup.DOFade(1f, GetFadeDuration()));
        sequence.Join(transform.DOScale(Vector3.one, GetScaleDuration()).SetEase(GetShowEase()));
        sequence.OnComplete(RefreshBodyScroll);
        tween = sequence;
        RefreshBodyScroll();
    }

    private void UpdateVisibleContent(UnifiedDetailContent content, bool pinnedDisplay)
    {
        CancelScheduledHide();
        tween?.Kill(false);
        gameObject.SetActive(true);
        CacheReferences();
        ApplyThemeLayout();
        ApplyContent(content);
        PopupLayerUtility.ApplyTo((RectTransform)transform);
        transform.SetAsLastSibling();
        transform.localScale = Vector3.one;
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = pinnedDisplay;
        canvasGroup.interactable = pinnedDisplay;
        RefreshBodyScroll();
    }

    private void ApplyContent(UnifiedDetailContent content)
    {
        if (titleText != null)
            titleText.text = InlineIconTextFormatter.Format(content.Title);
        if (bodyText != null)
            bodyText.text = InlineIconTextFormatter.Format(content.Body);
        if (iconImage != null)
        {
            iconImage.sprite = content.Icon;
            iconImage.gameObject.SetActive(content.Icon != null);
        }
        ApplyAddedDetails(content.AddedDetails);
    }

    private void ApplyAddedDetails(IReadOnlyList<UnifiedDetailAddedDetail> details)
    {
        int count = details != null ? details.Count : 0;
        for (int i = 0; i < addedDetailItems.Count; i++)
            addedDetailItems[i].gameObject.SetActive(false);

        if (count == 0 || addedDetailRoot == null || addedDetailPrefab == null)
            return;

        UnifiedDetailTextConfig config = GetTextConfig();
        float firstYOffset = config != null ? config.AddedDetailFirstYOffset : 0f;
        float ySpacing = config != null ? config.AddedDetailYSpacing : 0f;

        for (int i = 0; i < count; i++)
        {
            AddedDetailedUI item = GetAddedDetailItem(i);
            RectTransform itemRect = item.RectTransform;
            if (itemRect != null)
                itemRect.anchoredPosition = new Vector2(itemRect.anchoredPosition.x, firstYOffset + i * ySpacing);
            item.gameObject.SetActive(true);
            UnifiedDetailAddedDetail detail = details[i];
            item.Apply(detail.Title, detail.Body, GetAddedDetailColor(detail.Type));
        }
    }

    private AddedDetailedUI GetAddedDetailItem(int index)
    {
        while (addedDetailItems.Count <= index)
        {
            AddedDetailedUI item = Instantiate(addedDetailPrefab, addedDetailRoot);
            item.gameObject.name = "AddedDetailed" + addedDetailItems.Count;
            addedDetailItems.Add(item);
        }
        return addedDetailItems[index];
    }

    private Color GetAddedDetailColor(UnifiedDetailAddedDetailType type)
    {
        UnifiedDetailTextConfig config = GetTextConfig();
        if (config == null)
            return Color.white;

        switch (type)
        {
            case UnifiedDetailAddedDetailType.Keyword:
                return config.KeywordFrameColor;
            case UnifiedDetailAddedDetailType.Enhancement:
                return config.EnhancementFrameColor;
            case UnifiedDetailAddedDetailType.Modifier:
                return config.ModifierFrameColor;
            default:
                return Color.white;
        }
    }

    private UnifiedDetailTextConfig GetTextConfig()
    {
        if (textConfig == null)
            textConfig = Resources.Load<UnifiedDetailTextConfig>("Config/UnifiedDetailTextConfig");
        return textConfig;
    }

    private void CacheReferences()
    {
        if (frameRoot == null)
            frameRoot = transform as RectTransform;
        if (iconRect == null && iconImage != null)
            iconRect = iconImage.transform as RectTransform;
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        if (backgroundImage == null)
            backgroundImage = FindChildComponentRecursive<Image>("Content");
        if (borderImage == null)
            borderImage = FindChildComponentRecursive<Image>("InnerFrame");
        if (borderGraphic == null)
        {
            SpringLineHighlightUI springLine = GetComponent<SpringLineHighlightUI>();
            if (springLine != null)
                borderGraphic = springLine;
        }
        if (borderGraphic == null)
        {
            Transform innerFrame = transform.Find("InnerFrame");
            if (innerFrame != null)
                borderGraphic = innerFrame.GetComponent<Graphic>();
        }
        if (iconImage == null)
            iconImage = FindChildComponentRecursive<Image>("Icon");
        if (titleText == null)
            titleText = FindChildComponentRecursive<TMP_Text>("TitleText");
        if (bodyText == null)
            bodyText = FindChildComponentRecursive<TMP_Text>("BodyText");
        if (bodyScrollRect == null)
            bodyScrollRect = GetComponentInChildren<ScrollRect>(true);
        if (bodyViewport == null && bodyScrollRect != null)
            bodyViewport = bodyScrollRect.viewport;
        if (bodyViewport == null)
            bodyViewport = FindChildRectRecursive("BodyViewport");
        if (bodyContent == null && bodyScrollRect != null)
            bodyContent = bodyScrollRect.content;
        if (bodyContent == null && bodyText != null)
            bodyContent = bodyText.rectTransform;
        if (addedDetailRoot == null)
            addedDetailRoot = FindChildRectRecursive("AddedDetailRoot");
        if (bodyScrollRect != null)
        {
            if (bodyViewport != null)
                bodyScrollRect.viewport = bodyViewport;
            if (bodyContent != null)
                bodyScrollRect.content = bodyContent;
        }
        EnsureBodyViewportMaskVisible();
        if (iconRect == null && iconImage != null)
            iconRect = iconImage.transform as RectTransform;
    }

    private T FindChildComponentRecursive<T>(string childName) where T : Component
    {
        Transform found = UIManager.FindChildRecursive(transform, childName);
        return found != null ? found.GetComponent<T>() : null;
    }

    private RectTransform FindChildRectRecursive(string childName)
    {
        Transform found = UIManager.FindChildRecursive(transform, childName);
        return found as RectTransform;
    }

    private void EnsureBodyViewportMaskVisible()
    {
        if (bodyViewport == null)
            return;

        Mask mask = bodyViewport.GetComponent<Mask>();
        if (mask != null)
            mask.showMaskGraphic = false;

        Graphic maskGraphic = bodyViewport.GetComponent<Graphic>();
        if (maskGraphic == null)
            return;

        Color color = maskGraphic.color;
        if (color.a <= 0.001f)
        {
            color.a = 1f;
            maskGraphic.color = color;
        }
        maskGraphic.canvasRenderer.cullTransparentMesh = false;
    }

    private void ApplyThemeLayout()
    {
    }

    private void RefreshBodyScroll()
    {
        StopAutoScroll();
        bodyCanScroll = false;
        RectTransform scrollContent = GetBodyScrollContent();
        if (bodyText == null || bodyScrollRect == null || bodyViewport == null || scrollContent == null)
            return;

        Canvas.ForceUpdateCanvases();
        bodyText.ForceMeshUpdate();
        float viewportHeight = Mathf.Max(1f, bodyViewport.rect.height);
        float preferredHeight = Mathf.Ceil(bodyText.preferredHeight);
        float contentHeight = Mathf.Max(viewportHeight, preferredHeight);
        bodyCanScroll = preferredHeight > viewportHeight + 1f;
        scrollContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight);
        bodyText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight);
        bodyScrollRect.content = scrollContent;
        bodyScrollRect.verticalNormalizedPosition = 1f;
        bodyScrollRect.enabled = pinned && bodyCanScroll;
        if (pinned && bodyCanScroll)
            manualScrollResumeTime = Time.unscaledTime + autoScrollStartDelay;
    }

    private RectTransform GetBodyScrollContent()
    {
        if (bodyContent != null)
            return bodyContent;
        if (bodyText != null)
            return bodyText.rectTransform;
        return bodyScrollRect != null ? bodyScrollRect.content : null;
    }

    private void Update()
    {
        if (pinned && currentAnchor is UnityEngine.Object anchorObject && anchorObject == null)
            currentAnchor = null;

        HidePinnedPopupOnOutsideClick();

        if (!pinned || !bodyCanScroll || draggingBody || autoScrollTween != null)
            return;
        if (Time.unscaledTime < manualScrollResumeTime)
            return;

        StartAutoScroll();
    }

    private void HidePinnedPopupOnOutsideClick()
    {
        if (!pinned || Time.frameCount == pinnedFrame || !TryGetPrimaryPointerDown(out Vector2 screenPosition))
            return;

        if (ContainsScreenPoint(screenPosition) || IsScreenPointInsideAnchor(screenPosition))
            return;

        Unpin();
    }

    private bool TryGetPrimaryPointerDown(out Vector2 screenPosition)
    {
        screenPosition = default;
        if (Input.GetMouseButtonDown(0))
        {
            screenPosition = Input.mousePosition;
            return true;
        }

        if (Input.touchCount <= 0)
            return false;

        Touch touch = Input.GetTouch(0);
        if (touch.phase != TouchPhase.Began)
            return false;

        screenPosition = touch.position;
        return true;
    }

    private bool IsScreenPointInsideAnchor(Vector2 screenPosition)
    {
        RectTransform anchorRect = GetCurrentAnchorRectTransform();
        return anchorRect != null && RectTransformUtility.RectangleContainsScreenPoint(anchorRect, screenPosition, GetEventCamera());
    }

    private RectTransform GetCurrentAnchorRectTransform()
    {
        if (currentAnchor is UnityEngine.Object anchorObject && anchorObject == null)
            return null;
        if (currentAnchor is RectTransform rectTransform)
            return rectTransform;
        if (currentAnchor is Component component)
            return component.transform as RectTransform;
        if (currentAnchor is GameObject gameObject)
            return gameObject.transform as RectTransform;
        return null;
    }

    private Camera GetEventCamera()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        PauseAutoScrollAfterManualInput();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        draggingBody = true;
        PauseAutoScrollAfterManualInput();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        draggingBody = false;
        PauseAutoScrollAfterManualInput();
    }

    public void OnScroll(PointerEventData eventData)
    {
        PauseAutoScrollAfterManualInput();
    }

    private void PauseAutoScrollAfterManualInput()
    {
        if (!pinned)
            return;

        StopAutoScroll();
        manualScrollResumeTime = Time.unscaledTime + manualScrollResumeDelay;
    }

    private void StartAutoScroll()
    {
        if (bodyScrollRect == null)
            return;

        Sequence sequence = DOTween.Sequence().SetTarget(this).SetUpdate(true);
        sequence.AppendInterval(autoScrollPause);
        sequence.Append(DOVirtual.Float(1f, 0f, autoScrollDuration, value => bodyScrollRect.verticalNormalizedPosition = value).SetEase(Ease.InOutSine));
        sequence.AppendInterval(autoScrollPause);
        sequence.Append(DOVirtual.Float(0f, 1f, autoScrollDuration, value => bodyScrollRect.verticalNormalizedPosition = value).SetEase(Ease.InOutSine));
        sequence.SetLoops(-1);
        autoScrollTween = sequence;
    }

    private void StopAutoScroll()
    {
        autoScrollTween?.Kill(false);
        autoScrollTween = null;
    }

    private Vector3 GetHiddenScale()
    {
        return theme != null ? theme.HiddenScale : new Vector3(0.82f, 0.82f, 1f);
    }

    private float GetFadeDuration()
    {
        return theme != null ? theme.FadeDuration : 0.12f;
    }

    private float GetScaleDuration()
    {
        return theme != null ? theme.ScaleDuration : 0.18f;
    }

    private float GetHoverExitHideDelay()
    {
        return theme != null ? theme.HoverExitHideDelay : 0.15f;
    }

    private Ease GetShowEase()
    {
        return theme != null ? theme.ShowEase : Ease.OutBack;
    }

    private Ease GetHideEase()
    {
        return theme != null ? theme.HideEase : Ease.InBack;
    }

    private void OnDestroy()
    {
        tween?.Kill(false);
        hideDelayTween?.Kill(false);
        StopAutoScroll();
    }
}
