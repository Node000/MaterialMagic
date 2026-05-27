using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class PlayAreaUI : MonoBehaviour
{
    [SerializeField] private RectTransform resolveIndicator;
    [SerializeField] private RectTransform continuousCastCounterRect;
    [SerializeField] private Text continuousCastCounterText;

    [SerializeField] private float counterPunchDuration = 0.24f;
    [SerializeField] private float counterPunchScale = 0.55f;
    [SerializeField] private float counterMaxTiltAngle = 45f;
    [SerializeField] private int counterMaxEffectCastCount = 10;
    [SerializeField] private int counterShakeStartCastCount = 5;
    [SerializeField] private float counterMaxShakeStrength = 12f;
    [SerializeField] private int counterShakeVibrato = 12;
    [SerializeField] private int counterPunchVibrato = 8;
    [SerializeField] private float counterPunchElasticity = 0.7f;
    [SerializeField] private Ease resolveIndicatorEase = Ease.OutQuad;

    public RectTransform ResolveIndicator => resolveIndicator;

    public void Initialize(HandSystemUI owner)
    {
        CacheReferences();
        if (resolveIndicator != null)
        {
            Image image = resolveIndicator.GetComponent<Image>();
            if (image != null)
            {
                image.color = new Color(1f, 0.86f, 0.18f, 0.18f);
                image.raycastTarget = false;
            }
            resolveIndicator.gameObject.SetActive(false);
        }
        UpdateContinuousCastCounter(0, true);
    }

    public void HideResolveIndicator()
    {
        if (resolveIndicator != null)
            resolveIndicator.gameObject.SetActive(false);
    }

    public void ShowResolveIndicator()
    {
        if (resolveIndicator != null)
            resolveIndicator.gameObject.SetActive(true);
    }

    public void UpdateContinuousCastCounter(int count, bool instant)
    {
        CacheReferences();
        if (continuousCastCounterRect == null || continuousCastCounterText == null)
            return;

        bool visible = count > 0;
        continuousCastCounterRect.gameObject.SetActive(visible);
        if (!visible)
            return;

        continuousCastCounterText.text = "x" + count;
        continuousCastCounterRect.DOKill(false);
        continuousCastCounterRect.localScale = Vector3.one;
        continuousCastCounterRect.localEulerAngles = Vector3.zero;
        if (!instant)
        {
            float tiltProgress = Mathf.Clamp01(count / (float)Mathf.Max(1, counterMaxEffectCastCount));
            float tilt = counterMaxTiltAngle * tiltProgress;
            Sequence sequence = DOTween.Sequence().SetTarget(this);
            sequence.Join(continuousCastCounterRect.DOPunchScale(Vector3.one * counterPunchScale, counterPunchDuration, counterPunchVibrato, counterPunchElasticity));
            sequence.Join(continuousCastCounterRect.DOPunchRotation(Vector3.forward * -tilt, counterPunchDuration, counterPunchVibrato, counterPunchElasticity));
            if (count >= counterShakeStartCastCount)
            {
                float shakeProgress = Mathf.InverseLerp(counterShakeStartCastCount, counterMaxEffectCastCount, count);
                sequence.Join(continuousCastCounterRect.DOShakeAnchorPos(counterPunchDuration, counterMaxShakeStrength * shakeProgress, counterShakeVibrato, 90f, false, true));
            }
            sequence.OnComplete(() =>
            {
                continuousCastCounterRect.localEulerAngles = Vector3.zero;
                continuousCastCounterRect.localScale = Vector3.one;
            });
        }
    }

    public void MoveIndicatorToCardRange(RectTransform firstCard, RectTransform lastCard, RectTransform playArea, float layoutDuration, Ease layoutEase, bool instant)
    {
        CacheReferences();
        if (resolveIndicator == null || firstCard == null || lastCard == null || playArea == null)
            return;

        if (resolveIndicator.parent != playArea)
            resolveIndicator.SetParent(playArea, false);

        Vector2 first = firstCard.anchoredPosition;
        Vector2 last = lastCard.anchoredPosition;
        Vector2 position = (first + last) * 0.5f;
        float width = Mathf.Abs(last.x - first.x) + firstCard.rect.width + 24f;
        Vector2 size = new Vector2(width, firstCard.rect.height + 24f);
        resolveIndicator.SetAsLastSibling();
        resolveIndicator.DOKill(false);
        if (instant)
        {
            resolveIndicator.anchoredPosition = position;
            resolveIndicator.sizeDelta = size;
            return;
        }

        Sequence sequence = DOTween.Sequence();
        sequence.Join(resolveIndicator.DOAnchorPos(position, layoutDuration).SetEase(resolveIndicatorEase));
        sequence.Join(resolveIndicator.DOSizeDelta(size, layoutDuration).SetEase(resolveIndicatorEase));
        sequence.SetTarget(this);
    }

    private void CacheReferences()
    {
        if (resolveIndicator == null)
            resolveIndicator = UIManager.FindChildRect(transform, "ResolveIndicator");
        if (continuousCastCounterRect == null)
            continuousCastCounterRect = UIManager.FindChildRect(transform, "ContinuousCastCounter");
        if (continuousCastCounterText == null && continuousCastCounterRect != null)
            continuousCastCounterText = continuousCastCounterRect.GetComponent<Text>();
    }
}
