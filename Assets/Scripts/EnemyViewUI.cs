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
    [SerializeField] private RectTransform healthBarRoot;
    [SerializeField] private Image healthFill;
    [SerializeField] private Image healthBufferFill;
    [SerializeField] private Image shieldFill;
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private RectTransform buffRoot;
    [SerializeField] private Graphic focusMarker;
    [SerializeField] private RectTransform intentRoot;
    [SerializeField] private BuffPopupEffectController buffPopupEffect;

    private bool baseLayoutCached;
    private Vector2 baseHealthBarAnchoredPosition;
    private Vector2 baseHealthBarSizeDelta;
    private Vector2 baseIntentRootAnchoredPosition;

    public RectTransform BodyRoot => bodyRoot;
    public RectTransform MotionRoot => motionRoot != null ? motionRoot : bodyRoot;
    public Image BodyImage => bodyImage;
    public EnemySpriteAnimatorUI BodyAnimator => bodyAnimator;
    public TMP_Text NameText => nameText;
    public RectTransform HealthBarRoot => healthBarRoot;
    public Image HealthFill => healthFill;
    public Image HealthBufferFill => healthBufferFill;
    public Image ShieldFill => shieldFill;
    public TMP_Text HealthText => healthText;
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
        if (buffRoot == null)
            buffRoot = FindRect("BuffRoot");
        if (focusMarker == null)
            focusMarker = FindGraphic("FocusMarker");
        if (intentRoot == null)
            intentRoot = FindRect("IntentRoot");
        if (buffPopupEffect == null)
            buffPopupEffect = GetComponentInChildren<BuffPopupEffectController>(true);
    }

    public void ApplyDataLayout(EnemyData data)
    {
        CacheMissingReferences();
        CacheBaseLayout();

        Vector2 bodySize = GetBodySize(data);
        if (bodyRoot != null)
            bodyRoot.sizeDelta = bodySize;

        ApplyEnemyDataOverrides(data);

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

        if (healthBarRoot != null)
        {
            baseHealthBarAnchoredPosition = healthBarRoot.anchoredPosition;
            baseHealthBarSizeDelta = healthBarRoot.sizeDelta;
        }
        if (intentRoot != null)
            baseIntentRootAnchoredPosition = intentRoot.anchoredPosition;
        baseLayoutCached = true;
    }

    private void ApplyEnemyDataOverrides(EnemyData data)
    {
        if (healthBarRoot != null)
        {
            healthBarRoot.anchoredPosition = baseHealthBarAnchoredPosition + GetHealthBarOffset(data);
            if (data != null && data.healthBarWidth > 0f)
                healthBarRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, data.healthBarWidth);
            else
                healthBarRoot.sizeDelta = baseHealthBarSizeDelta;
        }

        if (intentRoot != null)
            intentRoot.anchoredPosition = baseIntentRootAnchoredPosition + GetIntentOffset(data);
    }

    private static Vector2 GetHealthBarOffset(EnemyData data)
    {
        return data != null ? new Vector2(data.healthBarOffsetX, data.healthBarOffsetY) : Vector2.zero;
    }

    private static Vector2 GetIntentOffset(EnemyData data)
    {
        return data != null ? new Vector2(data.intentOffsetX, data.intentOffsetY) : Vector2.zero;
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
