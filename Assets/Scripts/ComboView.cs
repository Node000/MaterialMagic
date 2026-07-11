using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public class ComboView : MonoBehaviour
{
    [SerializeField] private RectTransform targetRect;
    [SerializeField] private TMP_Text comboText;
    [SerializeField] private Color comboColor = new Color(1f, 0.86f, 0.18f, 1f);
    [SerializeField] private float punchDuration = 0.24f;
    [SerializeField] private float punchScale = 0.55f;
    [SerializeField] private float maxTiltAngle = 45f;
    [SerializeField] private int maxEffectCastCount = 10;
    [SerializeField] private int shakeStartCastCount = 5;
    [SerializeField] private float maxShakeStrength = 12f;
    [SerializeField] private int shakeVibrato = 12;
    [SerializeField] private int punchVibrato = 8;
    [SerializeField] private float punchElasticity = 0.7f;
    [SerializeField] private bool idleShakeEnabled = true;
    [SerializeField] private float idleShakeStrength = 2f;
    [SerializeField] private float idleShakeDuration = 0.24f;
    [SerializeField] private int idleShakeVibrato = 6;
    [SerializeField] private float idleShakeRandomness = 90f;

    private Sequence punchSequence;
    private Tween idleShakeTween;
    private Vector2 basePosition;
    private Vector3 baseScale;
    private Vector3 baseEulerAngles;
    private bool baseTransformCached;

    public TMP_Text ComboText => comboText;

    private void Awake()
    {
        CacheReferences();
    }

    private void OnDisable()
    {
        StopTweens();
    }

    private void OnDestroy()
    {
        StopTweens();
    }

    public void Initialize(HandSystemUI owner)
    {
        CacheReferences();
        CacheTextStyle(owner);
        UpdateCount(0, true);
    }

    public void UpdateCount(int count, bool instant)
    {
        CacheReferences();
        if (targetRect == null || comboText == null)
            return;

        StopTweens();
        CacheBaseTransform();
        ResetTransform();

        bool visible = count > 0;
        targetRect.gameObject.SetActive(visible);
        if (!visible)
            return;

        comboText.text = BuildText(count);
        comboText.color = comboColor;

        if (instant)
        {
            StartIdleShake();
            return;
        }

        float tiltProgress = Mathf.Clamp01(count / (float)Mathf.Max(1, maxEffectCastCount));
        float tilt = maxTiltAngle * tiltProgress;
        punchSequence = DOTween.Sequence().SetTarget(this);
        punchSequence.Join(targetRect.DOPunchScale(Vector3.one * punchScale, punchDuration, punchVibrato, punchElasticity));
        punchSequence.Join(targetRect.DOPunchRotation(Vector3.forward * -tilt, punchDuration, punchVibrato, punchElasticity));
        if (count >= shakeStartCastCount)
        {
            float shakeProgress = Mathf.InverseLerp(shakeStartCastCount, maxEffectCastCount, count);
            punchSequence.Join(targetRect.DOShakeAnchorPos(punchDuration, maxShakeStrength * shakeProgress, shakeVibrato, 90f, false, true));
        }
        punchSequence.OnComplete(() =>
        {
            ResetTransform();
            punchSequence = null;
            StartIdleShake();
        });
    }

    private void StartIdleShake()
    {
        if (!idleShakeEnabled || idleShakeStrength <= 0f || idleShakeDuration <= 0f || targetRect == null || !targetRect.gameObject.activeInHierarchy)
            return;

        idleShakeTween = targetRect.DOShakeAnchorPos(idleShakeDuration, idleShakeStrength, idleShakeVibrato, idleShakeRandomness, false, true)
            .SetLoops(-1, LoopType.Restart)
            .SetEase(Ease.Linear)
            .SetTarget(this);
    }

    private void StopTweens()
    {
        punchSequence?.Kill(false);
        punchSequence = null;
        idleShakeTween?.Kill(false);
        idleShakeTween = null;
        DOTween.Kill(this);
    }

    private void ResetTransform()
    {
        if (targetRect == null)
            return;

        targetRect.anchoredPosition = basePosition;
        targetRect.localScale = baseScale;
        targetRect.localEulerAngles = baseEulerAngles;
    }

    private void CacheBaseTransform()
    {
        if (!baseTransformCached && targetRect != null)
        {
            basePosition = targetRect.anchoredPosition;
            baseScale = targetRect.localScale;
            baseEulerAngles = targetRect.localEulerAngles;
            baseTransformCached = true;
        }
    }

    private string BuildText(int count)
    {
        string suffix = count > 10 ? "!!" : count > 5 ? "!" : string.Empty;
        return string.Format(LocalizationSystem.GetText("ui.battle.combo_format", "COMBO {0}{1}"), count, suffix);
    }

    private void CacheTextStyle(HandSystemUI owner)
    {
        if (comboText == null)
            return;

        UIManager manager = owner != null ? owner.GetUIManager() : null;
        TMP_Text healthText = manager != null && manager.PlayerStatus != null ? manager.PlayerStatus.HealthText : null;
        if (healthText != null)
        {
            if (healthText.font != null)
                comboText.font = healthText.font;
            comboText.fontSize = healthText.fontSize;
            comboText.fontStyle = healthText.fontStyle;
        }
        comboText.color = comboColor;
        comboText.enableWordWrapping = false;
        comboText.overflowMode = TextOverflowModes.Overflow;
        comboText.alignment = TextAlignmentOptions.Center;
        comboText.raycastTarget = false;

        Graphic graphic = comboText as Graphic;
        if (graphic != null)
            graphic.raycastTarget = false;
    }

    private void CacheReferences()
    {
        if (targetRect == null)
            targetRect = transform as RectTransform;
        if (comboText == null)
            comboText = GetComponent<TMP_Text>();
    }
}
