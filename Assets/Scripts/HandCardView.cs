using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class HandCardView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image frameImage;
    [SerializeField] private Image iconImage;
    [SerializeField] private Text labelText;
    [SerializeField] private float selectedScale = 1.15f;
    [SerializeField] private float hoverTilt = 7f;
    [SerializeField] private float feedbackDuration = 0.18f;
    [SerializeField] private Ease feedbackEase = Ease.OutBack;

    private readonly Color selectedFrameColor = new Color(1f, 0.86f, 0.2f, 1f);
    private readonly Color normalFrameColor = new Color(0.18f, 0.18f, 0.18f, 1f);
    private HandSystemUI owner;
    private RectTransform rectTransform;
    private Tween feedbackTween;
    private Text modifierText;
    private bool selected;
    private bool inPlayZone;
    private bool hovered;
    private float baseZRotation;
    private MaterialModel card;

    public MaterialModel Card => card;
    public bool Selected => selected;
    public bool InPlayZone => inPlayZone;
    public RectTransform RectTransform => rectTransform;

    private void Awake()
    {
        rectTransform = (RectTransform)transform;
        JuicyMotion juicyMotion = GetComponent<JuicyMotion>();
        if (juicyMotion != null)
            juicyMotion.enabled = false;
    }

    private void OnDisable()
    {
        owner?.HideModifierTooltip(this);
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
        if (frameImage != null)
            frameImage.color = selected ? selectedFrameColor : normalFrameColor;

        PlayFeedback(instant);
    }

    public void RefreshFeedback()
    {
        PlayFeedback(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (owner == null)
            return;

        if (eventData.button == PointerEventData.InputButton.Right)
        {
            owner.OnCardPlayRequested(this);
            return;
        }

        if (eventData.button == PointerEventData.InputButton.Left)
            owner.OnCardLeftClicked(this);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        PlayFeedback(false);
        owner?.ShowModifierTooltip(this, card);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        PlayFeedback(false);
        owner?.HideModifierTooltip(this);
    }

    private void RefreshVisual()
    {
        if (labelText != null)
            labelText.text = MaterialCardView.GetMaterialName(card.material);

        EnsureModifierText();
        if (modifierText != null)
        {
            string modifierLabel = GetModifierLabel();
            modifierText.text = modifierLabel;
            modifierText.gameObject.SetActive(!string.IsNullOrEmpty(modifierLabel));
        }

        if (iconImage != null)
            iconImage.color = MaterialCardView.GetMaterialColor(card.material);
    }

    private string GetModifierLabel()
    {
        string text = string.Empty;
        if (card.isTemporary)
            text = LocalizationKeys.GetModifierName(MaterialModifierDisplayKind.Temporary);
        for (int i = 0; i < card.modifiers.Count; i++)
        {
            MaterialModifierModel modifier = card.modifiers[i];
            if (modifier is TemporaryModifier)
                continue;

            if (!string.IsNullOrEmpty(text))
                text += " ";
            text += LocalizationKeys.GetModifierName(modifier);
        }
        return text;
    }

    private void EnsureModifierText()
    {
        if (modifierText != null)
            return;

        modifierText = new GameObject("ModifierTagText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)).GetComponent<Text>();
        modifierText.transform.SetParent(transform, false);
        modifierText.font = labelText != null && labelText.font != null ? labelText.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        modifierText.fontSize = 13;
        modifierText.alignment = TextAnchor.MiddleCenter;
        modifierText.color = new Color(1f, 0.92f, 0.58f, 1f);
        modifierText.raycastTarget = false;
        RectTransform rect = modifierText.rectTransform;
        rect.anchorMin = new Vector2(0.08f, 0.16f);
        rect.anchorMax = new Vector2(0.92f, 0.42f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
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
