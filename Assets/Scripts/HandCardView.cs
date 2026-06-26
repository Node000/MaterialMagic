using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class HandCardView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image frameImage;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private TMP_Text modifierText;
    [SerializeField] private SpringLineHighlightUI springHighlight;
    [SerializeField] private float selectedScale = 1f;
    [SerializeField] private float hoverTilt = 0f;
    [SerializeField] private float feedbackDuration = 0.18f;
    [SerializeField] private Ease feedbackEase = Ease.OutBack;
    [Header("Modifier标签布局")]
    [SerializeField] private Vector2 modifierTextAnchoredPosition = new Vector2(0f, 8f);
    [SerializeField] private Vector2 modifierTextSizeDelta = new Vector2(0f, 22f);

    private HandSystemUI owner;
    private RectTransform rectTransform;
    private Tween feedbackTween;
    private bool selected;
    private bool inPlayZone;
    private bool hovered;
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
        RefreshSpringHighlight();

        JuicyMotion juicyMotion = GetComponent<JuicyMotion>();
        if (juicyMotion != null)
            juicyMotion.enabled = false;
    }

    private void OnDisable()
    {
        hovered = false;
        RefreshSpringHighlight();
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
        if (inPlayZone)
            SetSelected(false, false);
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

    public void RefreshFeedback()
    {
        PlayFeedback(false);
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
        hovered = true;
        RefreshSpringHighlight();
        PlayFeedback(false);
        if (TryGetTooltipContent(out UnifiedDetailContent content))
            owner?.GetUIManager()?.ShowUnifiedDetailPopup(this, content);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        RefreshSpringHighlight();
        PlayFeedback(false);
        owner?.GetUIManager()?.HideUnifiedDetailPopup(this);
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

    private void ClearHoverAndHideTooltip()
    {
        hovered = false;
        RefreshSpringHighlight();
        PlayFeedback(false);
        owner?.GetUIManager()?.HideUnifiedDetailPopup(this);
    }

    private void RefreshVisual()
    {
        SetFrameTransparent();

        if (labelText != null)
            labelText.text = MaterialCardView.GetMaterialName(card.material);

        EnsureModifierText();
        if (modifierText != null)
        {
            string modifierLabel = GetModifierLabel();
            modifierText.text = modifierLabel;
            modifierText.gameObject.SetActive(!string.IsNullOrEmpty(modifierLabel));
            ApplyModifierTextLayout();
        }

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

        springHighlight.color = GetSpringHighlightColor();
        springHighlight.gameObject.SetActive(selected || hovered);
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

    private string GetModifierLabel()
    {
        string text = string.Empty;
        bool hasTemporaryModifier = false;
        for (int i = 0; i < card.modifiers.Count; i++)
        {
            MaterialModifierModel modifier = card.modifiers[i];
            if (modifier == null)
                continue;

            if (modifier is TemporaryModifier)
                hasTemporaryModifier = true;

            if (!string.IsNullOrEmpty(text))
                text += " ";
            text += LocalizationKeys.GetModifierName(modifier);
        }

        if (card.isTemporary && !hasTemporaryModifier)
        {
            if (!string.IsNullOrEmpty(text))
                text += " ";
            text += LocalizationKeys.GetModifierName(MaterialModifierDisplayKind.Temporary);
        }

        return text;
    }

    private void ApplyModifierTextLayout()
    {
        if (modifierText == null)
            return;

        RectTransform rect = modifierText.rectTransform;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = modifierTextAnchoredPosition;
        rect.sizeDelta = modifierTextSizeDelta;
    }

    private void EnsureModifierText()
    {
        if (modifierText != null)
            return;

        modifierText = new GameObject("ModifierTagText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        modifierText.transform.SetParent(transform, false);
        modifierText.font = labelText != null && labelText.font != null ? labelText.font : UIManager.GetDefaultTMPFont();
        modifierText.fontSize = 13;
        modifierText.alignment = TextAlignmentOptions.Center;
        modifierText.color = Color.white;
        modifierText.raycastTarget = false;
        modifierText.enableWordWrapping = false;
        modifierText.overflowMode = TextOverflowModes.Overflow;
        ApplyModifierTextLayout();
    }

    private void PlayFeedback(bool instant)
    {
        feedbackTween?.Kill(false);

        Vector3 targetScale = selected ? Vector3.one * selectedScale : Vector3.one;
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
