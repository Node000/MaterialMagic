using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class EnemyIntentView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
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
    [SerializeField] private float horizontalPadding = 8f;
    [SerializeField] private float iconTextSpacing = 6f;
    [SerializeField] private float minWidth = 58f;

    private const string RippleMaterialResourcePath = "Materials/IntentRipple";
    private const string RippleShaderName = "UI/IntentRipple";
    private static Material rippleMaterialTemplate;
    private static readonly Dictionary<string, Sprite> intentSpriteCache = new Dictionary<string, Sprite>();

    private RectTransform rectTransform;
    private Vector2 baseAnchoredPosition;
    private float phase;
    private Material rippleMaterial;
    private Tween fadeTween;
    private HandSystemUI owner;
    private EnemyModel boundEnemy;
    private EnemyIntentData boundIntent;
    private PlayerState boundPlayerState;
    private Image hitImage;
    private bool hidden;

    public RectTransform RectTransform => rectTransform != null ? rectTransform : (RectTransform)transform;
    public float LayoutWidth
    {
        get
        {
            CacheReferences();
            return rectTransform != null ? rectTransform.sizeDelta.x : 0f;
        }
    }

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

    public void SetOwner(HandSystemUI owner)
    {
        this.owner = owner;
    }

    public void Bind(EnemyModel enemy, EnemyIntentData intent, int phaseIndex, int phaseCount)
    {
        Bind(owner, enemy, intent, null, phaseIndex, phaseCount);
    }

    public void Bind(EnemyModel enemy, EnemyIntentData intent, PlayerState playerState, int phaseIndex, int phaseCount)
    {
        Bind(owner, enemy, intent, playerState, phaseIndex, phaseCount);
    }

    public void Bind(HandSystemUI owner, EnemyModel enemy, EnemyIntentData intent, PlayerState playerState, int phaseIndex, int phaseCount)
    {
        this.owner = owner;
        boundEnemy = enemy;
        boundIntent = intent;
        boundPlayerState = playerState;

        int intentValue = 0;
        string intentDisplayValue = null;
        if (enemy != null && intent != null)
        {
            if (intent.actionType == EnemyActionType.Attack || intent.actionType == EnemyActionType.AttackAll)
                intentValue = enemy.GetIntentAttackValue(intent, playerState);
            else if (intent.actionType == EnemyActionType.GainShield)
                intentValue = enemy.GetIntentShieldValue(intent);
            else if (intent.actionType == EnemyActionType.Special)
                intentDisplayValue = enemy.GetSpecialIntentDisplayValue(intent, playerState);
        }
        Bind(intent, phaseIndex, phaseCount, intentValue, intentDisplayValue);
    }

    public void Bind(EnemyIntentData intent, int phaseIndex, int phaseCount)
    {
        Bind(intent, phaseIndex, phaseCount, 0);
    }

    private void Bind(EnemyIntentData intent, int phaseIndex, int phaseCount, int attackValue)
    {
        Bind(intent, phaseIndex, phaseCount, attackValue, null);
    }

    private void Bind(EnemyIntentData intent, int phaseIndex, int phaseCount, int attackValue, string displayValueOverride)
    {
        CacheReferences();
        phase = phaseCount > 0 ? phaseIndex * Mathf.PI * 2f / phaseCount : 0f;

        fadeTween?.Kill(false);
        hidden = false;
        SetRaycastEnabled(true);

        if (iconImage != null)
        {
            iconImage.sprite = LoadIntentSprite(intent);
            iconImage.color = Color.white;
            iconImage.raycastTarget = false;
            iconImage.canvasRenderer.SetAlpha(1f);
        }

        if (valueText != null)
        {
            Color textColor = valueText.color;
            textColor.a = 1f;
            valueText.color = textColor;
            string displayValue = GetIntentDisplayValue(intent, attackValue, displayValueOverride);
            valueText.text = displayValue;
            valueText.raycastTarget = false;
            valueText.canvasRenderer.SetAlpha(1f);
            ApplyAdaptiveWidth(displayValue);
        }
        else
        {
            ApplyAdaptiveWidth(string.Empty);
        }
    }

    public Tween PlayRipple(float durationOverride)
    {
        CacheReferences();
        if (rippleImage == null)
            return null;

        float duration = durationOverride > 0f ? durationOverride : rippleDuration;
        rippleImage.rectTransform.sizeDelta = new Vector2(rippleSize, rippleSize);
        rippleImage.rectTransform.anchoredPosition = Vector2.zero;
        EnsureRippleMaterial();
        if (rippleMaterial == null)
        {
            rippleImage.gameObject.SetActive(false);
            return null;
        }

        rippleImage.gameObject.SetActive(true);
        rippleImage.color = Color.white;
        rippleImage.canvasRenderer.SetAlpha(1f);
        rippleImage.material = rippleMaterial;
        rippleMaterial.SetColor("_Color", Color.white);
        rippleMaterial.SetFloat("_Progress", 0f);
        rippleMaterial.SetFloat("_RippleSize", rippleSize);
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
        owner?.HideEnemyIntentTooltip(this);
        SetRaycastEnabled(false);
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
        if (hitImage == null)
        {
            hitImage = GetComponent<Image>();
            if (hitImage == null)
                hitImage = gameObject.AddComponent<Image>();
            hitImage.sprite = null;
            hitImage.color = Color.clear;
            hitImage.maskable = false;
        }
    }

    private void SetRaycastEnabled(bool enabled)
    {
        if (hitImage != null)
            hitImage.raycastTarget = enabled;
    }

    private void EnsureRippleMaterial()
    {
        if (rippleImage == null)
            return;
        if (rippleMaterial == null)
        {
            if (rippleMaterialTemplate == null)
                rippleMaterialTemplate = Resources.Load<Material>(RippleMaterialResourcePath);

            if (rippleMaterialTemplate != null)
            {
                rippleMaterial = new Material(rippleMaterialTemplate);
            }
            else
            {
                Shader shader = Shader.Find(RippleShaderName);
                if (shader == null)
                    return;
                rippleMaterial = new Material(shader);
            }

            rippleMaterial.hideFlags = HideFlags.DontSave;
            rippleMaterial.SetColor("_Color", Color.white);
            rippleImage.material = rippleMaterial;
        }
    }

    public static void PrewarmIntentSprite(EnemyIntentData intent)
    {
        LoadIntentSprite(intent);
    }

    private static Sprite LoadIntentSprite(EnemyIntentData intent)
    {
        string displayType = ResolveIntentDisplayType(intent);
        Sprite sprite = LoadIntentSpriteByName(displayType);
        if (sprite != null)
            return sprite;

        switch (intent != null ? intent.actionType : EnemyActionType.None)
        {
            case EnemyActionType.Attack:
            case EnemyActionType.AttackAll:
                return LoadIntentSpriteByName("attack");
            case EnemyActionType.GainShield:
                return LoadIntentSpriteByName("defend");
            case EnemyActionType.ApplyDebuff:
                return LoadIntentSpriteByName("debuff");
            case EnemyActionType.Summon:
                return LoadIntentSpriteByName("summon");
            case EnemyActionType.Stunned:
                return LoadIntentSpriteByName("stun");
            case EnemyActionType.ApplyBuff:
                return LoadIntentSpriteByName("buff");
            case EnemyActionType.Special:
            default:
                return LoadIntentSpriteByName("spAttack");
        }
    }

    private static Sprite LoadIntentSpriteByName(string displayType)
    {
        if (string.IsNullOrEmpty(displayType))
            return null;

        if (intentSpriteCache.TryGetValue(displayType, out Sprite sprite))
            return sprite;

        sprite = Resources.Load<Sprite>("Images/Intent/" + displayType);
        intentSpriteCache[displayType] = sprite;
        return sprite;
    }

    private static string ResolveIntentDisplayType(EnemyIntentData intent)
    {
        if (intent == null)
            return "spAttack";
        if (!string.IsNullOrEmpty(intent.displayType))
            return intent.displayType;

        switch (intent.actionType)
        {
            case EnemyActionType.AttackAll:
                return "allAttack";
            case EnemyActionType.Attack:
                if (intent.times > 1)
                    return intent.value >= 6 ? "bitMultiAttack" : "multiAttack";
                return intent.value >= 8 ? "bigAttack" : "attack";
            case EnemyActionType.GainShield:
                return intent.value >= 8 ? "bigDefend" : "defend";
            case EnemyActionType.ApplyDebuff:
                return "debuff";
            case EnemyActionType.ApplyBuff:
                return "buff";
            case EnemyActionType.Summon:
                return "summon";
            case EnemyActionType.Stunned:
                return "stun";
            case EnemyActionType.Special:
                return "spAttack";
            default:
                return "spAttack";
        }
    }

    private void ApplyAdaptiveWidth(string displayValue)
    {
        if (rectTransform == null || iconImage == null)
            return;

        RectTransform iconRect = iconImage.rectTransform;
        float iconWidth = iconRect.sizeDelta.x > 0f ? iconRect.sizeDelta.x : iconRect.rect.width;
        if (iconWidth <= 0f)
            iconWidth = 42f;

        bool hasValue = !string.IsNullOrEmpty(displayValue);
        float textWidth = 0f;
        if (valueText != null)
        {
            valueText.gameObject.SetActive(hasValue);
            if (hasValue)
            {
                valueText.ForceMeshUpdate();
                textWidth = Mathf.Ceil(valueText.preferredWidth);
                RectTransform textRect = valueText.rectTransform;
                Vector2 textSize = textRect.sizeDelta;
                textSize.x = textWidth;
                textRect.sizeDelta = textSize;
                float textX = valueText.transform.parent == iconImage.transform
                    ? iconTextSpacing + textWidth * 0.5f
                    : iconRect.anchoredPosition.x + iconWidth * 0.5f + iconTextSpacing + textWidth * 0.5f;
                textRect.anchoredPosition = new Vector2(textX, textRect.anchoredPosition.y);
            }
        }

        float width = hasValue ? horizontalPadding * 2f + iconWidth + iconTextSpacing + textWidth : horizontalPadding * 2f + iconWidth;
        width = Mathf.Max(minWidth, width);
        Vector2 size = rectTransform.sizeDelta;
        size.x = width;
        rectTransform.sizeDelta = size;
        iconRect.anchoredPosition = new Vector2(horizontalPadding + iconWidth * 0.5f, iconRect.anchoredPosition.y);
    }

    private static string GetIntentDisplayValue(EnemyIntentData intent, int attackValue, string displayValueOverride = null)
    {
        if (!string.IsNullOrEmpty(displayValueOverride))
            return displayValueOverride;
        if (intent == null)
            return string.Empty;
        if (intent.actionType == EnemyActionType.Attack || intent.actionType == EnemyActionType.AttackAll)
        {
            int times = intent.times > 0 ? intent.times : 1;
            return times > 1 ? attackValue + "x" + times : attackValue.ToString();
        }
        if (intent.actionType == EnemyActionType.GainShield)
            return attackValue.ToString();
        if (intent.actionType == EnemyActionType.Summon)
            return intent.summonCount > 1 ? "×" + intent.summonCount : string.Empty;
        if (intent.actionType == EnemyActionType.ApplyBuff || intent.actionType == EnemyActionType.ApplyDebuff || intent.actionType == EnemyActionType.Special || intent.actionType == EnemyActionType.Stunned)
            return string.Empty;
        if (intent.value > 0)
            return intent.value.ToString();
        return string.Empty;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!hidden && boundEnemy != null && boundIntent != null)
            owner?.ShowEnemyIntentTooltip(this, boundEnemy, boundIntent, boundPlayerState);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        owner?.HideEnemyIntentTooltip(this);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left && !hidden && boundEnemy != null && boundIntent != null)
            owner?.GetUIManager().PinEnemyIntentTooltip(this, boundEnemy, boundIntent, boundPlayerState);
    }

    private void OnDisable()
    {
        owner?.HideEnemyIntentTooltip(this);
    }

    private void OnDestroy()
    {
        fadeTween?.Kill(false);
        if (rippleMaterial != null)
            Destroy(rippleMaterial);
    }
}
