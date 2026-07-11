using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayAreaUI : MonoBehaviour
{
    [SerializeField] private RectTransform resolveIndicator;
    [SerializeField] private RectTransform continuousCastCounterRect;
    [SerializeField] private TMP_Text continuousCastCounterText;
    [SerializeField] private ComboView comboView;

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

    private Sequence continuousCastCounterSequence;
    private Vector2 continuousCastCounterBasePosition;
    private Vector3 continuousCastCounterBaseScale;
    private Vector3 continuousCastCounterBaseEulerAngles;
    private bool continuousCastCounterBaseTransformCached;
    private readonly Color continuousCastCounterColor = new Color(1f, 0.86f, 0.18f, 1f);

    public RectTransform ResolveIndicator => resolveIndicator;

    public void Initialize(HandSystemUI owner)
    {
        CacheReferences();
        CacheContinuousCastCounterTextStyle(owner);
        if (comboView != null)
            comboView.Initialize(owner);
        if (resolveIndicator != null)
        {
            Graphic graphic = resolveIndicator.GetComponent<Graphic>();
            if (graphic != null)
                graphic.raycastTarget = false;
            resolveIndicator.gameObject.SetActive(false);
        }
        if (comboView == null)
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
        if (comboView != null)
        {
            comboView.UpdateCount(count, instant);
            return;
        }

        if (continuousCastCounterRect == null || continuousCastCounterText == null)
            return;

        bool visible = count > 0;
        continuousCastCounterSequence?.Kill(false);
        continuousCastCounterSequence = null;
        continuousCastCounterRect.DOKill(false);
        if (!continuousCastCounterBaseTransformCached)
        {
            continuousCastCounterBasePosition = continuousCastCounterRect.anchoredPosition;
            continuousCastCounterBaseScale = continuousCastCounterRect.localScale;
            continuousCastCounterBaseEulerAngles = continuousCastCounterRect.localEulerAngles;
            continuousCastCounterBaseTransformCached = true;
        }
        continuousCastCounterRect.anchoredPosition = continuousCastCounterBasePosition;
        continuousCastCounterRect.localScale = continuousCastCounterBaseScale;
        continuousCastCounterRect.localEulerAngles = continuousCastCounterBaseEulerAngles;
        continuousCastCounterRect.gameObject.SetActive(visible);
        if (!visible)
            return;

        continuousCastCounterText.text = BuildContinuousCastCounterText(count);
        continuousCastCounterText.color = continuousCastCounterColor;
        if (!instant)
        {
            float tiltProgress = Mathf.Clamp01(count / (float)Mathf.Max(1, counterMaxEffectCastCount));
            float tilt = counterMaxTiltAngle * tiltProgress;
            continuousCastCounterSequence = DOTween.Sequence().SetTarget(continuousCastCounterRect);
            continuousCastCounterSequence.Join(continuousCastCounterRect.DOPunchScale(Vector3.one * counterPunchScale, counterPunchDuration, counterPunchVibrato, counterPunchElasticity));
            continuousCastCounterSequence.Join(continuousCastCounterRect.DOPunchRotation(Vector3.forward * -tilt, counterPunchDuration, counterPunchVibrato, counterPunchElasticity));
            if (count >= counterShakeStartCastCount)
            {
                float shakeProgress = Mathf.InverseLerp(counterShakeStartCastCount, counterMaxEffectCastCount, count);
                continuousCastCounterSequence.Join(continuousCastCounterRect.DOShakeAnchorPos(counterPunchDuration, counterMaxShakeStrength * shakeProgress, counterShakeVibrato, 90f, false, true));
            }
            continuousCastCounterSequence.OnComplete(() =>
            {
                continuousCastCounterRect.anchoredPosition = continuousCastCounterBasePosition;
                continuousCastCounterRect.localEulerAngles = continuousCastCounterBaseEulerAngles;
                continuousCastCounterRect.localScale = continuousCastCounterBaseScale;
                continuousCastCounterSequence = null;
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

    private string BuildContinuousCastCounterText(int count)
    {
        string suffix = count > 10 ? "!!" : count > 5 ? "!" : string.Empty;
        return string.Format(LocalizationSystem.GetText("ui.battle.combo_format", "COMBO {0}{1}"), count, suffix);
    }

    private void CacheContinuousCastCounterTextStyle(HandSystemUI owner)
    {
        if (continuousCastCounterText == null || owner == null)
            return;

        UIManager manager = owner.GetUIManager();
        TMP_Text healthText = manager != null && manager.PlayerStatus != null ? manager.PlayerStatus.HealthText : null;
        if (healthText != null)
        {
            if (healthText.font != null)
                continuousCastCounterText.font = healthText.font;
            continuousCastCounterText.fontSize = healthText.fontSize;
            continuousCastCounterText.fontStyle = healthText.fontStyle;
        }
        continuousCastCounterText.color = continuousCastCounterColor;
        continuousCastCounterText.enableWordWrapping = false;
        continuousCastCounterText.overflowMode = TextOverflowModes.Overflow;
        continuousCastCounterText.alignment = TextAlignmentOptions.Center;
        continuousCastCounterText.raycastTarget = false;
    }

    private void CacheReferences()
    {
        if (resolveIndicator == null)
            resolveIndicator = UIManager.FindChildRect(transform, "CastRange");
        if (resolveIndicator == null)
            resolveIndicator = UIManager.FindChildRect(transform, "ResolveIndicator");
        if (continuousCastCounterRect == null)
            continuousCastCounterRect = UIManager.FindChildRect(transform, "ContinuousCastCounter");
        if (continuousCastCounterText == null && continuousCastCounterRect != null)
            continuousCastCounterText = continuousCastCounterRect.GetComponent<TMP_Text>();
        if (comboView == null)
        {
            if (continuousCastCounterRect != null)
                comboView = continuousCastCounterRect.GetComponent<ComboView>();
            if (comboView == null)
                comboView = GetComponentInChildren<ComboView>(true);
        }
    }
}
