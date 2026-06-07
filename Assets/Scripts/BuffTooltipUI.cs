using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BuffTooltipUI : MonoBehaviour
{
    private static readonly Vector3 HiddenScale = new Vector3(0.82f, 0.82f, 1f);

    [SerializeField] private float tooltipFadeDuration = 0.12f;
    [SerializeField] private float tooltipScaleDuration = 0.18f;
    [SerializeField] private Ease tooltipShowEase = Ease.OutBack;
    [SerializeField] private Ease tooltipHideEase = Ease.InBack;
    [SerializeField] private float tooltipYOffset = 54f;

    private CanvasGroup canvasGroup;
    private Tween tween;
    private BuffSlotView currentSlot;

    public void Initialize(HandSystemUI owner)
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        transform.localScale = HiddenScale;
        gameObject.SetActive(false);
    }

    public void Show(BuffSlotView slot, BuffModel buff)
    {
        if (slot == null || buff == null)
            return;

        currentSlot = slot;
        BuffDisplayData display = BuffDisplayDatabase.Get(buff.buffType);
        TMP_Text title = UIManager.FindChildComponent<TMP_Text>(transform, "Title");
        if (title != null)
        {
            string stackText = buff.GetTooltipStackText();
            title.text = display.Name + (!string.IsNullOrEmpty(stackText) ? "  " + stackText : string.Empty);
        }

        TMP_Text description = UIManager.FindChildComponent<TMP_Text>(transform, "Description");
        if (description != null)
            description.text = buff.GetDesc();

        gameObject.SetActive(true);
        transform.localScale = HiddenScale;
        canvasGroup.alpha = 0f;
        RectTransform slotRect = slot.RectTransform;
        float slotHeight = Mathf.Max(slotRect.rect.height, slotRect.sizeDelta.y);
        float yOffset = Mathf.Max(tooltipYOffset, slotHeight + 18f);
        transform.position = slotRect.TransformPoint(new Vector3(0f, yOffset, 0f));
        PopupLayerUtility.ApplyTo((RectTransform)transform);
        transform.SetAsLastSibling();
        tween?.Kill(false);
        Sequence sequence = DOTween.Sequence().SetTarget(this);
        sequence.Join(canvasGroup.DOFade(1f, tooltipFadeDuration));
        sequence.Join(transform.DOScale(Vector3.one, tooltipScaleDuration).SetEase(tooltipShowEase));
        tween = sequence;
    }

    public void Hide(BuffSlotView slot)
    {
        if (slot != null && currentSlot != null && slot != currentSlot)
            return;

        currentSlot = null;
        tween?.Kill(false);
        Sequence sequence = DOTween.Sequence().SetTarget(this);
        sequence.Join(canvasGroup.DOFade(0f, tooltipFadeDuration));
        sequence.Join(transform.DOScale(HiddenScale, tooltipScaleDuration).SetEase(tooltipHideEase));
        sequence.OnComplete(() => gameObject.SetActive(false));
        tween = sequence;
    }

    private void OnDestroy()
    {
        tween?.Kill(false);
    }
}
