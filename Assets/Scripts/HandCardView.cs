using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class HandCardView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private Image frameImage;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private SpringLineHighlightUI springHighlight;
    [SerializeField] private float selectedScale = 1f;
    [SerializeField] private float hoverTilt = 0f;
    [SerializeField] private float feedbackDuration = 0.18f;
    [SerializeField] private Ease feedbackEase = Ease.OutBack;

    private HandSystemUI owner;
    private RectTransform rectTransform;
    private Tween feedbackTween;
    private bool selected;
    private bool inPlayZone;
    private bool hovered;
    private bool dragging;
    private bool suppressClick;
    private bool layoutHovered;
    private float layoutHoverScale = 1f;
    private float baseZRotation;
    private MaterialModel card;
    private Action<HandCardView, PointerEventData> clickOverride;
    private UnifiedDetailContent tooltipContentOverride;
    private bool hasTooltipContentOverride;

    public MaterialModel Card => card;
    public bool Selected => selected;
    public bool InPlayZone => inPlayZone;
    public RectTransform RectTransform => rectTransform;

    private void Awake()
    {
        rectTransform = (RectTransform)transform;
        CacheSpringHighlight();
        SetFrameTransparent();
        RefreshRaycastTargets();
        RefreshSpringHighlight();

        JuicyMotion juicyMotion = GetComponent<JuicyMotion>();
        if (juicyMotion != null)
            juicyMotion.enabled = false;
    }

    private void OnDisable()
    {
        hovered = false;
        layoutHovered = false;
        RefreshSpringHighlight();
        owner?.ClearCardHover(this, true);
        owner?.GetUIManager()?.HideUnifiedDetailPopup(this);
        feedbackTween?.Kill(false);
        feedbackTween = null;
    }

    private void OnDestroy()
    {
        feedbackTween?.Kill(false);
    }

    public void Initialize(HandSystemUI owner)
    {
        this.owner = owner;
    }

    public void Bind(MaterialModel card, bool inPlayZone)
    {
        this.card = card;
        this.inPlayZone = inPlayZone;
        SetSelected(false, true);
        RefreshVisual();
    }

    public void SetInPlayZone(bool value)
    {
        inPlayZone = value;
    }

    public void SetBaseRotation(float zRotation, bool instant)
    {
        baseZRotation = zRotation;
        PlayFeedback(instant);
    }

    public void SetSelected(bool value, bool instant)
    {
        selected = value;
        SetFrameTransparent();
        RefreshSpringHighlight();

        PlayFeedback(instant);
    }

    public void ClearPlayFeedback(bool instant)
    {
        selected = false;
        hovered = false;
        layoutHovered = false;
        SetFrameTransparent();
        RefreshSpringHighlight();
        PlayFeedback(instant);
    }

    public void RefreshFeedback()
    {
        PlayFeedback(false);
    }

    public void SetLayoutHover(bool value, float hoverScale, bool instant)
    {
        layoutHoverScale = Mathf.Max(0.01f, hoverScale);
        if (layoutHovered == value)
            return;

        layoutHovered = value;
        RefreshSpringHighlight();
        PlayFeedback(instant);
    }

    public void SetClickOverride(Action<HandCardView, PointerEventData> handler)
    {
        clickOverride = handler;
    }

    public void SetTooltipContentOverride(UnifiedDetailContent content)
    {
        tooltipContentOverride = content;
        hasTooltipContentOverride = true;
    }

    public void ClearTooltipContentOverride()
    {
        tooltipContentOverride = default;
        hasTooltipContentOverride = false;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (suppressClick)
        {
            suppressClick = false;
            return;
        }

        bool touchPointer = IsTouchPointer(eventData);

        if (clickOverride != null)
        {
            if (TryGetTooltipContent(out UnifiedDetailContent content))
                owner?.GetUIManager()?.PinUnifiedDetailPopup(this, content);
            clickOverride(this, eventData);
            if (touchPointer)
                ClearHoverAndHideTooltip();
            return;
        }

        if (owner == null)
            return;

        if (owner.TryPlaySelectedCardsFromCardClick(this, eventData))
            return;

        if (eventData.button == PointerEventData.InputButton.Right)
        {
            owner.OnCardPlayRequested(this);
            if (touchPointer)
                ClearHoverAndHideTooltip();
            return;
        }

        if (eventData.button == PointerEventData.InputButton.Left)
        {
            if (TryGetTooltipContent(out UnifiedDetailContent content))
                owner.GetUIManager()?.PinUnifiedDetailPopup(this, content);
            owner.OnCardLeftClicked(this);
        }

        if (touchPointer)
            ClearHoverAndHideTooltip();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (dragging || (owner != null && owner.IsCardDragActive))
            return;

        hovered = true;
        owner?.SetCardHover(this, false);
        RefreshSpringHighlight();
        PlayFeedback(false);
        if (TryGetTooltipContent(out UnifiedDetailContent content))
            owner?.GetUIManager()?.ShowUnifiedDetailPopup(this, content);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (dragging || (owner != null && owner.IsCardDragActive))
            return;

        hovered = false;
        owner?.ClearCardHover(this, false);
        RefreshSpringHighlight();
        PlayFeedback(false);
        owner?.GetUIManager()?.HideUnifiedDetailPopup(this);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (owner == null)
            return;

        if (!CanBeginMouseDrag(eventData))
            return;

        suppressClick = true;
        dragging = true;
        hovered = false;
        RefreshSpringHighlight();
        PlayFeedback(false);
        owner.OnCardDragBegin(this, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragging || owner == null)
            return;

        owner.OnCardDragged(this, eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!dragging)
            return;

        dragging = false;
        owner?.OnCardDragEnd(this, eventData);
    }

    private bool TryGetTooltipContent(out UnifiedDetailContent content)
    {
        if (hasTooltipContentOverride)
        {
            content = tooltipContentOverride;
            return true;
        }

        if (card != null)
        {
            content = UnifiedDetailContentBuilder.Build(card);
            return true;
        }

        content = default;
        return false;
    }

    private bool IsTouchPointer(PointerEventData eventData)
    {
        return Input.touchCount > 0 || (eventData != null && eventData.pointerId >= 0);
    }

    private bool CanBeginMouseDrag(PointerEventData eventData)
    {
        return clickOverride == null && eventData != null && eventData.button == PointerEventData.InputButton.Left;
    }

    private void ClearHoverAndHideTooltip()
    {
        hovered = false;
        owner?.ClearCardHover(this, false);
        RefreshSpringHighlight();
        PlayFeedback(false);
        owner?.GetUIManager()?.HideUnifiedDetailPopup(this);
    }

    private void RefreshVisual()
    {
        SetFrameTransparent();

        if (labelText != null)
            labelText.text = MaterialCardView.GetMaterialName(card.material);

        RefreshSpringHighlight();

        if (iconImage != null)
        {
            MaterialEnum displayMaterial = card != null ? card.GetArrowDisplayMaterial() : MaterialEnum.None;
            Sprite sprite = MaterialCardView.GetMaterialIcon(displayMaterial);
            iconImage.sprite = sprite;
            iconImage.color = sprite != null ? Color.white : MaterialCardView.GetMaterialColor(displayMaterial);
            iconImage.preserveAspect = true;
            MaterialModifierVisualUtility.ApplyTo(iconImage, card);
        }

        RefreshRaycastTargets();
    }

    public void RefreshRaycastTargets()
    {
        Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
            graphics[i].raycastTarget = false;

        Graphic raycastGraphic = iconImage != null ? iconImage : frameImage;
        if (raycastGraphic != null)
            raycastGraphic.raycastTarget = true;
        EnsureIconRaycastFilter();

        if (springHighlight != null)
            springHighlight.raycastTarget = false;
    }

    public bool ContainsRaycastPoint(Vector2 screenPosition, Camera eventCamera)
    {
        if (iconImage != null)
        {
            SpritePhysicsShapeRaycastFilter filter = iconImage.GetComponent<SpritePhysicsShapeRaycastFilter>();
            if (filter != null)
                return filter.ContainsScreenPoint(screenPosition, eventCamera);

            return RectTransformUtility.RectangleContainsScreenPoint(iconImage.rectTransform, screenPosition, eventCamera);
        }

        return rectTransform != null && RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPosition, eventCamera);
    }

    private void EnsureIconRaycastFilter()
    {
        if (iconImage != null && iconImage.GetComponent<SpritePhysicsShapeRaycastFilter>() == null)
            iconImage.gameObject.AddComponent<SpritePhysicsShapeRaycastFilter>();
    }

    private void CacheSpringHighlight()
    {
        if (springHighlight == null)
            springHighlight = GetComponentInChildren<SpringLineHighlightUI>(true);

        if (springHighlight != null)
            springHighlight.raycastTarget = false;
    }

    private void SetFrameTransparent()
    {
        if (frameImage != null)
            frameImage.color = Color.clear;
    }

    private void RefreshSpringHighlight()
    {
        CacheSpringHighlight();
        if (springHighlight == null)
            return;

        springHighlight.gameObject.SetActive(false);
    }

    private Color GetSpringHighlightColor()
    {
        Color color = Color.white;
        if (card == null || card.modifiers == null)
            return color;

        for (int i = 0; i < card.modifiers.Count; i++)
        {
            MaterialModifierModel modifier = card.modifiers[i];
            if (modifier != null && MaterialModifierDisplayDatabase.TryGetLineColor(modifier, out Color modifierColor))
                color = modifierColor;
        }
        return color;
    }

    private void PlayFeedback(bool instant)
    {
        feedbackTween?.Kill(false);

        Vector3 targetScale = layoutHovered ? Vector3.one * layoutHoverScale : selected ? Vector3.one * selectedScale : Vector3.one;
        float hoverOffset = hovered ? selected ? -hoverTilt : hoverTilt : 0f;
        Vector3 targetRotation = new Vector3(0f, 0f, baseZRotation + hoverOffset);

        if (instant)
        {
            transform.localScale = targetScale;
            transform.localEulerAngles = targetRotation;
            return;
        }

        Sequence sequence = DOTween.Sequence();
        sequence.Join(transform.DOScale(targetScale, feedbackDuration).SetEase(feedbackEase));
        sequence.Join(transform.DOLocalRotate(targetRotation, feedbackDuration).SetEase(feedbackEase));
        sequence.SetTarget(this);
        feedbackTween = sequence;
    }
}
