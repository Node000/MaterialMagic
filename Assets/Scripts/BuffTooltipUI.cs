using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

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

    public void Initialize(HandSystemUI owner)
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        transform.localScale = HiddenScale;
        gameObject.SetActive(false);
    }

    public void Show(BuffSlotView slot, BuffModel buff)
    {
        if (slot == null || buff == null)
            return;

        BuffDisplayData display = BuffDisplayDatabase.Get(buff.buffType);
        Text title = UIManager.FindChildComponent<Text>(transform, "Title");
        if (title != null)
            title.text = display.Name + (buff.stack > 0 ? "  " + buff.stack : string.Empty);

        Text description = UIManager.FindChildComponent<Text>(transform, "Description");
        if (description != null)
            description.text = display.Description;

        gameObject.SetActive(true);
        transform.localScale = HiddenScale;
        canvasGroup.alpha = 0f;
        transform.position = slot.RectTransform.position + new Vector3(0f, tooltipYOffset, 0f);
        transform.SetAsLastSibling();
        tween?.Kill(false);
        Sequence sequence = DOTween.Sequence().SetTarget(this);
        sequence.Join(canvasGroup.DOFade(1f, tooltipFadeDuration));
        sequence.Join(transform.DOScale(Vector3.one, tooltipScaleDuration).SetEase(tooltipShowEase));
        tween = sequence;
    }

    public void Hide(BuffSlotView slot)
    {
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
