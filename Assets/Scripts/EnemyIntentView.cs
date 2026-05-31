using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EnemyIntentView : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text valueText;
    [SerializeField] private Image rippleImage;
    [SerializeField] private float floatAmplitude = 4f;
    [SerializeField] private float floatFrequency = 1.15f;
    [SerializeField] private float rippleDuration = 0.58f;
    [SerializeField] private float rippleSize = 74f;
    [SerializeField] private int rippleRingCount = 3;
    [SerializeField] private float fadeOutDuration = 0.18f;

    private RectTransform rectTransform;
    private Vector2 baseAnchoredPosition;
    private float phase;
    private Material rippleMaterial;
    private Tween fadeTween;
    private bool hidden;

    public RectTransform RectTransform => rectTransform != null ? rectTransform : (RectTransform)transform;

    public void SetBaseAnchoredPosition(Vector2 position)
    {
        CacheReferences();
        baseAnchoredPosition = position;
        rectTransform.anchoredPosition = position;
    }

    private void Awake()
    {
        CacheReferences();
    }

    private void Update()
    {
        CacheReferences();
        rectTransform.anchoredPosition = baseAnchoredPosition + Vector2.up * (Mathf.Sin(Time.time * floatFrequency + phase) * floatAmplitude);
    }

    public void Bind(EnemyModel enemy, EnemyIntentData intent, int phaseIndex, int phaseCount)
    {
        Bind(intent, phaseIndex, phaseCount, enemy != null ? enemy.GetIntentAttackValue(intent) : 0);
    }

    public void Bind(EnemyIntentData intent, int phaseIndex, int phaseCount)
    {
        Bind(intent, phaseIndex, phaseCount, 0);
    }

    private void Bind(EnemyIntentData intent, int phaseIndex, int phaseCount, int attackValue)
    {
        CacheReferences();
        phase = phaseCount > 0 ? phaseIndex * Mathf.PI * 2f / phaseCount : 0f;

        fadeTween?.Kill(false);
        hidden = false;

        if (iconImage != null)
        {
            iconImage.sprite = LoadIntentSprite(intent);
            iconImage.color = GetIntentColor(intent);
            iconImage.raycastTarget = false;
            iconImage.canvasRenderer.SetAlpha(1f);
        }

        if (valueText != null)
        {
            Color textColor = valueText.color;
            textColor.a = 1f;
            valueText.color = textColor;
            valueText.text = GetIntentDisplayValue(intent, attackValue);
            valueText.raycastTarget = false;
            valueText.canvasRenderer.SetAlpha(1f);
        }
    }

    public Tween PlayRipple(float durationOverride)
    {
        CacheReferences();
        if (rippleImage == null)
            return null;

        float duration = durationOverride > 0f ? durationOverride : rippleDuration;
        rippleImage.gameObject.SetActive(true);
        rippleImage.color = iconImage != null ? iconImage.color : Color.white;
        rippleImage.rectTransform.sizeDelta = new Vector2(rippleSize, rippleSize);
        rippleImage.rectTransform.anchoredPosition = Vector2.zero;
        EnsureRippleMaterial();
        if (rippleMaterial == null)
            return rippleImage.DOFade(0f, duration).OnComplete(() => rippleImage.gameObject.SetActive(false));

        rippleMaterial.SetColor("_Color", iconImage != null ? iconImage.color : Color.white);
        rippleMaterial.SetFloat("_Progress", 0f);
        rippleMaterial.SetFloat("_RingCount", rippleRingCount);
        return DOTween.To(() => 0f, value => rippleMaterial.SetFloat("_Progress", value), 1f, duration)
            .SetEase(Ease.OutQuad)
            .SetTarget(this)
            .OnComplete(() => rippleImage.gameObject.SetActive(false));
    }

    public Tween PlayFadeOut(float durationOverride)
    {
        CacheReferences();
        if (hidden)
            return null;

        hidden = true;
        float duration = durationOverride > 0f ? durationOverride : fadeOutDuration;
        fadeTween?.Kill(false);
        Sequence sequence = DOTween.Sequence().SetTarget(this);
        if (iconImage != null)
            sequence.Join(iconImage.DOFade(0f, duration));
        if (valueText != null)
            sequence.Join(DOTween.ToAlpha(() => valueText.color, color => valueText.color = color, 0f, duration));
        fadeTween = sequence;
        return sequence;
    }

    private void CacheReferences()
    {
        if (rectTransform == null)
        {
            rectTransform = (RectTransform)transform;
            baseAnchoredPosition = rectTransform.anchoredPosition;
        }
        if (iconImage == null)
            iconImage = UIManager.FindChildComponent<Image>(transform, "Icon");
        if (valueText == null)
            valueText = UIManager.FindChildComponent<TMP_Text>(transform, "ValueText");
        if (rippleImage == null)
            rippleImage = UIManager.FindChildComponent<Image>(transform, "Ripple");
    }

    private void EnsureRippleMaterial()
    {
        if (rippleImage == null)
            return;
        if (rippleMaterial == null)
        {
            Shader shader = Shader.Find("UI/IntentRipple");
            if (shader == null)
                return;
            rippleMaterial = new Material(shader);
            rippleMaterial.SetColor("_Color", iconImage != null ? iconImage.color : Color.white);
            rippleImage.material = rippleMaterial;
        }
    }

    private static Sprite LoadIntentSprite(EnemyIntentData intent)
    {
        if (intent == null)
            return Resources.Load<Sprite>("Images/UI/mixed");

        switch (intent.intentType)
        {
            case EnemyIntentType.Attack: return Resources.Load<Sprite>("Images/UI/attack");
            case EnemyIntentType.Defend: return Resources.Load<Sprite>("Images/UI/defend");
            case EnemyIntentType.Special: return Resources.Load<Sprite>("Images/UI/mixed");
            default: return Resources.Load<Sprite>("Images/UI/mixed");
        }
    }

    private static Color GetIntentColor(EnemyIntentData intent)
    {
        if (intent == null)
            return Color.gray;

        switch (intent.intentType)
        {
            case EnemyIntentType.Attack: return new Color(0.95f, 0.18f, 0.14f, 1f);
            case EnemyIntentType.Defend: return new Color(0.25f, 0.55f, 1f, 1f);
            case EnemyIntentType.Special: return new Color(0.75f, 0.35f, 1f, 1f);
            default: return Color.gray;
        }
    }

    private static string GetIntentDisplayValue(EnemyIntentData intent, int attackValue)
    {
        if (intent == null)
            return string.Empty;
        if (intent.actionType == EnemyActionType.Attack)
            return attackValue.ToString();
        if (intent.value > 0)
            return intent.value.ToString();
        if (intent.buffAmount > 0)
            return intent.buffAmount.ToString();
        return LocalizationSystem.GetText(intent.descriptionKey, string.Empty);
    }

    private void OnDestroy()
    {
        fadeTween?.Kill(false);
        if (rippleMaterial != null)
            Destroy(rippleMaterial);
    }
}
