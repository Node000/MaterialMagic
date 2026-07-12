using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public class EnemyViewUI : MonoBehaviour
{
    [SerializeField] private RectTransform bodyRoot;
    [SerializeField] private RectTransform motionRoot;
    [SerializeField] private Image bodyImage;
    [SerializeField] private EnemySpriteAnimatorUI bodyAnimator;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private RectTransform infoBackFrame;
    [SerializeField] private RectTransform healthBarRoot;
    [SerializeField] private Image healthFill;
    [SerializeField] private Image healthBufferFill;
    [SerializeField] private Image shieldFill;
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private TMP_Text shieldText;
    [SerializeField] private RectTransform buffRoot;
    [SerializeField] private Graphic focusMarker;
    [SerializeField] private RectTransform intentRoot;
    [SerializeField] private BuffPopupEffectController buffPopupEffect;

    private bool baseLayoutCached;
    private Vector2 baseInfoBackFrameAnchoredPosition;
    private Vector2 baseInfoBackFrameSizeDelta;
    private Vector2 baseHealthBarAnchoredPosition;
    private Vector2 baseHealthBarSizeDelta;
    private Vector2 baseIntentRootAnchoredPosition;
    private JuicyMotion juicyMotion;

    public RectTransform BodyRoot => bodyRoot;
    public RectTransform MotionRoot => motionRoot != null ? motionRoot : bodyRoot;
    public Image BodyImage => bodyImage;
    public EnemySpriteAnimatorUI BodyAnimator => bodyAnimator;
    public TMP_Text NameText => nameText;
    public RectTransform InfoBackFrame => infoBackFrame;
    public RectTransform HealthBarRoot => healthBarRoot;
    public Image HealthFill => healthFill;
    public Image HealthBufferFill => healthBufferFill;
    public Image ShieldFill => shieldFill;
    public TMP_Text HealthText => healthText;
    public TMP_Text ShieldText => shieldText;
    public RectTransform BuffRoot => buffRoot;
    public Graphic FocusMarker => focusMarker;
    public RectTransform IntentRoot => intentRoot;
    public BuffPopupEffectController BuffPopupEffect => buffPopupEffect;

    private void Awake()
    {
        CacheMissingReferences();
    }

    private void OnValidate()
    {
        CacheMissingReferences();
    }

    public void CacheMissingReferences()
    {
        if (bodyRoot == null)
            bodyRoot = transform.Find("BodyRoot") as RectTransform;
        if (bodyImage == null)
        {
            Transform body = bodyRoot != null ? bodyRoot.Find("Body") : transform.Find("Body");
            if (body != null)
                bodyImage = body.GetComponent<Image>();
        }
        if (bodyAnimator == null && bodyImage != null)
            bodyAnimator = bodyImage.GetComponent<EnemySpriteAnimatorUI>();
        if (motionRoot == null)
            motionRoot = bodyRoot;
        if (nameText == null)
            nameText = FindText("NameText");
        if (infoBackFrame == null)
            infoBackFrame = FindRect("InfoBackFrame");
        if (healthBarRoot == null)
            healthBarRoot = FindRect("HealthBarBack");
        if (healthFill == null)
            healthFill = FindImage("HealthFill");
        if (healthBufferFill == null)
            healthBufferFill = FindImage("HealthBufferFill");
        if (shieldFill == null)
            shieldFill = FindImage("ShieldFill");
        if (healthText == null)
            healthText = FindText("HealthText");
        if (shieldText == null)
            shieldText = FindText("ShieldText");
        if (buffRoot == null)
            buffRoot = FindRect("BuffRoot");
        if (focusMarker == null)
            focusMarker = FindGraphic("FocusMarker");
        if (intentRoot == null)
            intentRoot = FindRect("IntentRoot");
        if (buffPopupEffect == null)
            buffPopupEffect = GetComponentInChildren<BuffPopupEffectController>(true);
        if (juicyMotion == null)
            juicyMotion = GetComponent<JuicyMotion>();
    }

    public void ApplyDataLayout(EnemyData data, int runtimeMaxHealth = 0)
    {
        CacheMissingReferences();
        CacheBaseLayout();

        Vector2 bodySize = GetBodySize(data);
        if (bodyRoot != null)
            bodyRoot.sizeDelta = bodySize;

        ApplyEnemyDataOverrides(data, runtimeMaxHealth);
        ApplyHoverEffect(data);

        if (bodyImage != null)
        {
            RectTransform bodyRect = bodyImage.rectTransform;
            bodyRect.anchorMin = Vector2.zero;
            bodyRect.anchorMax = Vector2.one;
            bodyRect.pivot = new Vector2(0.5f, 0.5f);
            bodyRect.anchoredPosition = Vector2.zero;
            bodyRect.sizeDelta = Vector2.zero;
            bodyImage.preserveAspect = true;
        }
    }

    private void CacheBaseLayout()
    {
        if (baseLayoutCached)
            return;

        if (infoBackFrame != null)
        {
            baseInfoBackFrameAnchoredPosition = infoBackFrame.anchoredPosition;
            baseInfoBackFrameSizeDelta = infoBackFrame.sizeDelta;
        }
        if (healthBarRoot != null)
        {
            baseHealthBarAnchoredPosition = healthBarRoot.anchoredPosition;
            baseHealthBarSizeDelta = healthBarRoot.sizeDelta;
        }
        if (intentRoot != null)
            baseIntentRootAnchoredPosition = intentRoot.anchoredPosition;
        baseLayoutCached = true;
    }

    private void ApplyEnemyDataOverrides(EnemyData data, int runtimeMaxHealth)
    {
        if (infoBackFrame != null)
        {
            infoBackFrame.anchoredPosition = GetInfoBoxAnchoredPosition(data);
            infoBackFrame.sizeDelta = GetInfoBoxSizeDelta(data, runtimeMaxHealth);
        }

        if (healthBarRoot != null)
        {
            healthBarRoot.anchoredPosition = GetHealthBarAnchoredPosition(data);
            healthBarRoot.sizeDelta = GetHealthBarSizeDelta(data, runtimeMaxHealth);
            ConfigureBuffRootLayout(healthBarRoot);
        }

        if (intentRoot != null)
            intentRoot.anchoredPosition = baseIntentRootAnchoredPosition + GetIntentOffset(data);
    }

    private bool HasInfoBoxWidthOverride(EnemyData data)
    {
        return data != null && data.infoBoxSize.x > 0f;
    }

    private bool HasInfoBoxHeightOverride(EnemyData data)
    {
        return data != null && data.infoBoxSize.y > 0f;
    }

    private bool HasInfoBoxOffsetOverride(EnemyData data)
    {
        return data != null && (data.infoBoxOffset.x != 0f || data.infoBoxOffset.y != 0f);
    }

    private Vector2 GetInfoBoxSizeDelta(EnemyData data, int runtimeMaxHealth)
    {
        EnemyStatusConfig config = LoadConfig();
        if (config == null)
            return baseInfoBackFrameSizeDelta;

        if (HasInfoBoxWidthOverride(data) || HasInfoBoxHeightOverride(data))
        {
            return new Vector2(
                HasInfoBoxWidthOverride(data) ? data.infoBoxSize.x : baseInfoBackFrameSizeDelta.x,
                HasInfoBoxHeightOverride(data) ? data.infoBoxSize.y : baseInfoBackFrameSizeDelta.y);
        }

        return new Vector2(GetInfoBoxWidth(data, runtimeMaxHealth, config), GetInfoBoxHeight(config));
    }

    private Vector2 GetInfoBoxAnchoredPosition(EnemyData data)
    {
        if (HasInfoBoxOffsetOverride(data))
            return baseInfoBackFrameAnchoredPosition + GetInfoBoxOffset(data);

        EnemyStatusConfig config = LoadConfig();
        if (config == null)
            return baseInfoBackFrameAnchoredPosition;

        return baseInfoBackFrameAnchoredPosition + new Vector2(0f, config.InfoBoxOffsetY);
    }

    private Vector2 GetHealthBarAnchoredPosition(EnemyData data)
    {
        return baseHealthBarAnchoredPosition + GetInfoBoxOffset(data);
    }

    private Vector2 GetHealthBarSizeDelta(EnemyData data, int runtimeMaxHealth)
    {
        EnemyStatusConfig config = LoadConfig();
        if (config == null)
            return baseHealthBarSizeDelta;

        float width = GetHealthBarWidth(data, runtimeMaxHealth, config);
        return new Vector2(width, baseHealthBarSizeDelta.y);
    }

    private static Vector2 GetInfoBoxOffset(EnemyData data)
    {
        return data != null ? data.infoBoxOffset : Vector2.zero;
    }

    private float GetInfoBoxWidth(EnemyData data, int runtimeMaxHealth, EnemyStatusConfig config)
    {
        float healthBarWidth = GetHealthBarWidth(data, runtimeMaxHealth, config);
        return Mathf.Max(config.InfoBoxMinWidth, healthBarWidth + config.InfoBoxHorizontalPadding * 2f + config.InfoBoxTextColumnWidth);
    }

    private float GetInfoBoxHeight(EnemyStatusConfig config)
    {
        return baseInfoBackFrameSizeDelta.y + config.InfoBoxVerticalPadding * 2f;
    }

    private float GetHealthBarWidth(EnemyData data, int runtimeMaxHealth, EnemyStatusConfig config)
    {
        if (data != null && data.healthBarWidth > 0f)
            return data.healthBarWidth;

        int maxHealth = runtimeMaxHealth > 0 ? runtimeMaxHealth : data != null ? data.maxHealth : 0;
        return GetHealthBarWidthFromHealth(maxHealth, config);
    }

    private static Vector2 GetIntentOffset(EnemyData data)
    {
        return data != null ? new Vector2(data.intentOffsetX, data.intentOffsetY) : Vector2.zero;
    }

    private void ApplyHoverEffect(EnemyData data)
    {
        if (juicyMotion != null)
            juicyMotion.SetHoverEffectEnabled(data == null || data.hoverEffect);
    }

    private Vector2 GetBodySize(EnemyData data)
    {
        if (bodyImage != null && bodyImage.sprite != null)
        {
            float scale = data != null && data.imageScale > 0f ? data.imageScale : 1f;
            return bodyImage.sprite.rect.size * scale;
        }

        if (bodyRoot != null)
        {
            Vector2 rootSize = bodyRoot.sizeDelta;
            if (rootSize.x > 0f && rootSize.y > 0f)
                return rootSize;
        }

        if (bodyImage != null)
        {
            Vector2 imageSize = bodyImage.rectTransform.sizeDelta;
            if (imageSize.x > 0f && imageSize.y > 0f)
                return imageSize;
        }

        return new Vector2(88f, 64f);
    }

    private static EnemyStatusConfig LoadConfig()
    {
        return Resources.Load<EnemyStatusConfig>("Config/EnemyStatusConfig");
    }

    private float GetHealthBarWidthFromHealth(int maxHealth, EnemyStatusConfig config)
    {
        float width = 0f;
        int previousBreakpoint = 0;
        IReadOnlyList<EnemyStatusBreakpointData> breaks = config.HealthBarGrowth;
        for (int i = 0; i < breaks.Count; i++)
        {
            EnemyStatusBreakpointData entry = breaks[i];
            int breakpoint = Mathf.Max(previousBreakpoint, entry.breakpoint);
            if (maxHealth <= breakpoint)
            {
                width += (maxHealth - previousBreakpoint) * entry.growthRate;
                return Mathf.Clamp(Mathf.Max(width, config.DefaultHealthBarWidth), config.HealthBarMinWidth, config.HealthBarMaxWidth);
            }

            width += (breakpoint - previousBreakpoint) * entry.growthRate;
            previousBreakpoint = breakpoint;
        }

        float tailRate = breaks.Count > 0 ? breaks[breaks.Count - 1].growthRate : 1f;
        width += (maxHealth - previousBreakpoint) * tailRate;
        return Mathf.Clamp(Mathf.Max(width, config.DefaultHealthBarWidth), config.HealthBarMinWidth, config.HealthBarMaxWidth);
    }

    private float ClampHealthBarWidth(float width, EnemyStatusConfig config)
    {
        return Mathf.Clamp(Mathf.Max(width, config.DefaultHealthBarWidth), config.HealthBarMinWidth, config.HealthBarMaxWidth);
    }

    private int GetBuffColumnCountFromHealthBarWidth(float healthBarWidth, EnemyStatusConfig config)
    {
        float normalized = Mathf.Max(config.HealthBarMinWidth, healthBarWidth);
        int columns = Mathf.FloorToInt(normalized / config.BuffColumnsPerHealthBarWidth);
        return Mathf.Clamp(columns, config.BuffMinColumnCount, config.BuffMaxColumnCount);
    }

    private void ConfigureBuffRootLayout(RectTransform root)
    {
        if (root == null)
            return;

        EnemyStatusConfig config = LoadConfig();
        if (config == null)
            return;

        GridLayoutGroup grid = root.GetComponent<GridLayoutGroup>();
        if (grid == null)
            return;

        float healthBarWidth = healthBarRoot != null ? healthBarRoot.sizeDelta.x : config.DefaultHealthBarWidth;
        int columnCount = GetBuffColumnCountFromHealthBarWidth(healthBarWidth, config);
        grid.cellSize = new Vector2(config.BuffSlotSize, config.BuffSlotSize);
        grid.spacing = new Vector2(config.BuffSlotSpacing, config.BuffSlotSpacing);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columnCount;
    }

    private TMP_Text FindText(string childName)
    {
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i].name == childName)
                return texts[i];
        }
        return null;
    }

    private Graphic FindGraphic(string childName)
    {
        Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i].name == childName)
                return graphics[i];
        }
        return null;
    }

    private Image FindImage(string childName)
    {
        Image[] images = GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            if (images[i].name == childName)
                return images[i];
        }
        return null;
    }

    private RectTransform FindRect(string childName)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == childName)
                return children[i] as RectTransform;
        }
        return null;
    }
}
