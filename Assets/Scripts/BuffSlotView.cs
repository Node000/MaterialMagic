using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;
using System;
using TMPro;

public class EnemyViewClickHandler : MonoBehaviour, IPointerClickHandler
{
    private HandSystemUI owner;
    private EnemyModel enemy;

    public void Bind(HandSystemUI owner, EnemyModel enemy)
    {
        this.owner = owner;
        this.enemy = enemy;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
            owner?.OnEnemyLeftClicked(enemy);
    }
}

public class BuffSlotView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text stackText;

    private BuffModel buff;
    private HandSystemUI owner;
    private RectTransform rectTransform;
    private Tween motionTween;

    private const float AddDuration = 0.22f;
    private const float StackUpDuration = 0.12f;
    private const float StackDownDuration = 0.08f;
    private const float StackRecoverDuration = 0.12f;
    private const float RemoveGrowDuration = 0.08f;
    private const float RemoveShrinkDuration = 0.16f;
    private static readonly Vector3 NormalScale = Vector3.one;
    private static readonly Vector3 StackLargeScale = Vector3.one * 1.28f;
    private static readonly Vector3 StackSmallScale = Vector3.one * 0.9f;
    private static readonly Vector3 RemoveLargeScale = Vector3.one * 1.12f;
    private static readonly Vector3 HiddenScale = Vector3.zero;
    private const float VisualSize = 36f;
    private const float IconPadding = 3f;
    private const float StackFontSize = 18f;

    public const float LayoutSize = VisualSize;
    public const float LayoutSpacing = 5f;

    public BuffEnum BuffType => buff != null ? buff.buffType : BuffEnum.None;
    public int Stack => buff != null ? buff.stack : 0;

    public RectTransform RectTransform => rectTransform != null ? rectTransform : (RectTransform)transform;

    private void Awake()
    {
        rectTransform = (RectTransform)transform;
        ApplyVisualSizing();
    }

    public void Initialize(Image iconImage, TMP_Text stackText)
    {
        this.iconImage = iconImage;
        this.stackText = stackText;
        ApplyVisualSizing();
    }

    private void ApplyVisualSizing()
    {
        RectTransform.sizeDelta = new Vector2(VisualSize, VisualSize);

        if (iconImage != null)
        {
            RectTransform iconRect = iconImage.rectTransform;
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = new Vector2(IconPadding, IconPadding);
            iconRect.offsetMax = new Vector2(-IconPadding, -IconPadding);
            iconImage.raycastTarget = false;
        }

        if (stackText != null)
        {
            stackText.fontSize = StackFontSize;
            stackText.fontStyle = FontStyles.Bold;
            stackText.alignment = TextAlignmentOptions.BottomRight;
            stackText.enableWordWrapping = false;
            stackText.overflowMode = TextOverflowModes.Overflow;
            stackText.raycastTarget = false;

            RectTransform stackRect = stackText.rectTransform;
            stackRect.anchorMin = Vector2.zero;
            stackRect.anchorMax = Vector2.one;
            stackRect.offsetMin = Vector2.zero;
            stackRect.offsetMax = new Vector2(-2f, -1f);
        }
    }

    public void Bind(BuffModel buff, HandSystemUI owner)
    {
        BuffEnum previousType = BuffType;
        int previousStack = Stack;
        bool wasBound = this.buff != null;

        this.buff = buff;
        this.owner = owner;

        BuffDisplayData data = BuffDisplayDatabase.Get(buff.buffType);
        if (iconImage != null)
        {
            iconImage.sprite = data.Icon;
            iconImage.color = data.Icon != null ? Color.white : data.FallbackColor;
        }

        if (stackText != null)
            stackText.text = buff.stack > 1 ? buff.stack.ToString() : string.Empty;

        if (!wasBound || previousType != buff.buffType)
            PlayAddMotion();
        else if (buff.stack > previousStack)
            PlayStackMotion();
    }

    public void PlayRemoveMotion(Action onComplete)
    {
        KillMotion();
        Sequence sequence = DOTween.Sequence().SetTarget(this);
        sequence.Append(transform.DOScale(RemoveLargeScale, RemoveGrowDuration).SetEase(Ease.OutCubic));
        sequence.Append(transform.DOScale(HiddenScale, RemoveShrinkDuration).SetEase(Ease.InBack));
        sequence.OnComplete(() =>
        {
            buff = null;
            onComplete?.Invoke();
        });
        motionTween = sequence;
    }

    private void PlayAddMotion()
    {
        KillMotion();
        transform.localScale = HiddenScale;
        motionTween = transform.DOScale(NormalScale, AddDuration).SetEase(Ease.OutBack).SetTarget(this);
    }

    private void PlayStackMotion()
    {
        KillMotion();
        Sequence sequence = DOTween.Sequence().SetTarget(this);
        sequence.Append(transform.DOScale(StackLargeScale, StackUpDuration).SetEase(Ease.OutBack));
        sequence.Append(transform.DOScale(StackSmallScale, StackDownDuration).SetEase(Ease.InOutSine));
        sequence.Append(transform.DOScale(NormalScale, StackRecoverDuration).SetEase(Ease.OutBack));
        motionTween = sequence;
    }

    private void KillMotion()
    {
        if (motionTween != null)
        {
            motionTween.Kill(false);
            motionTween = null;
        }
        transform.localScale = NormalScale;
    }

    private void OnDisable()
    {
        KillMotion();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (buff != null)
            owner?.ShowBuffTooltip(this, buff);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        owner?.HideBuffTooltip(this);
    }
}
