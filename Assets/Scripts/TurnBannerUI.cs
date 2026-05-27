using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class TurnBannerUI : MonoBehaviour
{
    [SerializeField] private Text bannerText;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float fadeDuration = 0.16f;
    [SerializeField] private float holdDuration = 0.48f;
    [SerializeField] private float moveDistance = 36f;

    private RectTransform rectTransform;
    private Vector2 basePosition;
    private Tween tween;

    public void Initialize()
    {
        CacheReferences();
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }

    public Tween Show(string text)
    {
        CacheReferences();
        if (bannerText != null)
            bannerText.text = text;

        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        tween?.Kill(false);
        canvasGroup.alpha = 0f;
        rectTransform.anchoredPosition = basePosition + Vector2.left * moveDistance;
        Sequence sequence = DOTween.Sequence().SetTarget(this);
        sequence.Join(canvasGroup.DOFade(1f, fadeDuration).SetEase(Ease.OutCubic));
        sequence.Join(rectTransform.DOAnchorPos(basePosition, fadeDuration).SetEase(Ease.OutCubic));
        sequence.AppendInterval(holdDuration);
        sequence.Join(canvasGroup.DOFade(0f, fadeDuration).SetEase(Ease.InCubic));
        sequence.Join(rectTransform.DOAnchorPos(basePosition + Vector2.right * moveDistance, fadeDuration).SetEase(Ease.InCubic));
        sequence.OnComplete(() => gameObject.SetActive(false));
        tween = sequence;
        return sequence;
    }

    private void CacheReferences()
    {
        if (rectTransform == null)
        {
            rectTransform = (RectTransform)transform;
            basePosition = rectTransform.anchoredPosition;
        }
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        if (bannerText == null)
            bannerText = UIManager.FindChildComponent<Text>(transform, "Text");
        if (bannerText == null)
            bannerText = GetComponentInChildren<Text>(true);
    }

    private void OnDestroy()
    {
        tween?.Kill(false);
    }
}
