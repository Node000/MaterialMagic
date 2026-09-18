using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AddedDetailedUI : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private Graphic frameGraphic;
    [SerializeField] private SpringLineHighlightUI springLine;
    [SerializeField] private GameObject bodyRoot;
    [SerializeField] private ScrollRect bodyScrollRect;
    [SerializeField] private RectTransform bodyViewport;
    [SerializeField] private RectTransform bodyContent;
    [Tooltip("附加框图标（附魔，两层合成）预制体：Assets/Prefabs/UI/EnchantIcon.prefab。")]
    [SerializeField] private EnchantIconUI enchantIconPrefab;
    [Tooltip("附加框图标容器：容器尺寸 = 图标尺寸，容器位置 = 图标中心（现摆在标题左侧）。")]
    [SerializeField] private RectTransform enchantIconRoot;
    [Tooltip("容器内的单层图标槽（道具强化等 Sprite 图标用它；附魔用两层图标预制体）。")]
    [SerializeField] private Image modifierIconImage;
    [Tooltip("有图标时标题的字号（放大到接近图标大小）。")]
    [SerializeField] private float iconTitleFontSize = 44f;
    [Tooltip("图标与标题之间的间隔（约一个空格宽）。")]
    [SerializeField] private float iconTitleGap = 16f;
    /// <summary>正文顶边与图标底边之间保留的间距。</summary>
    private const float BodyIconGap = 4f;
    [SerializeField] private bool syncTitleColorWithFrame = true;
    [SerializeField] private float autoScrollStartDelay = 1.2f;
    [SerializeField] private float autoScrollDuration = 3f;
    [SerializeField] private float autoScrollPause = 1.2f;
    [SerializeField] private float manualScrollResumeDelay = 2f;

    private Tween autoScrollTween;
    private bool bodyCanScroll;
    private bool scrollInteractionEnabled;
    private bool scrollListenerRegistered;
    private bool updatingAutoScroll;
    private float manualScrollResumeTime;
    private EnchantIconUI enchantIcon;
    private bool baseLayoutCached;
    private Vector2 titleBasePosition;
    private Vector2 titleBaseSize;
    private Vector2 bodyBasePosition;
    private Vector2 bodyBaseSize;
    private float titleBaseFontSize;

    public RectTransform RectTransform => transform as RectTransform;

    public void Apply(string title, string body, Color lineColor, string enchantId = null, Sprite iconSprite = null)
    {
        CacheReferences();
        if (titleText != null)
        {
            titleText.richText = true;
            titleText.text = InlineIconTextFormatter.Format(title);
            if (syncTitleColorWithFrame)
                titleText.color = lineColor;
        }
        if (bodyText != null)
        {
            bodyText.richText = true;
            bodyText.text = InlineIconTextFormatter.Format(body);
        }
        ApplyBoxIcon(enchantId, iconSprite);
        if (bodyRoot != null)
            bodyRoot.SetActive(!string.IsNullOrEmpty(body));
        if (frameGraphic != null)
            frameGraphic.color = lineColor;
        if (springLine != null)
            springLine.SetVerticesDirty();
        RefreshBodyScroll();
    }

    /// <summary>
    /// 附加框图标：附魔用两层图标预制体（颜色与视觉效果由 EnchantIconUI 刷新），
    /// 道具强化等用 Sprite 图标槽；两者互斥，都没有就隐藏。
    /// </summary>
    private void ApplyBoxIcon(string enchantId, Sprite iconSprite)
    {
        bool hasEnchantIcon = !string.IsNullOrEmpty(enchantId) && enchantIconPrefab != null && enchantIconRoot != null;
        if (hasEnchantIcon)
        {
            EnsureEnchantIcon();
            if (enchantIcon != null)
            {
                enchantIcon.gameObject.SetActive(true);
                enchantIcon.Apply(enchantId);
            }
        }
        else if (enchantIcon != null)
        {
            enchantIcon.gameObject.SetActive(false);
        }

        bool hasSpriteIcon = !hasEnchantIcon && iconSprite != null && modifierIconImage != null;
        if (modifierIconImage != null)
        {
            modifierIconImage.sprite = hasSpriteIcon ? iconSprite : null;
            modifierIconImage.gameObject.SetActive(hasSpriteIcon);
        }

        ApplyIconTitleLayout(hasEnchantIcon || hasSpriteIcon);
    }

    private void EnsureEnchantIcon()
    {
        if (enchantIcon != null || enchantIconPrefab == null || enchantIconRoot == null)
            return;

        enchantIcon = Instantiate(enchantIconPrefab, enchantIconRoot);
        enchantIcon.name = "EnchantIcon";
        if (enchantIcon.transform is RectTransform iconRect)
        {
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.localScale = Vector3.one;
            iconRect.sizeDelta = enchantIconRoot.rect.size;
        }
    }

    /// <summary>
    /// 有图标时：标题字号放大到接近图标，标题右移到图标右侧并留一个间隔，正文整块下移让出标题变高的部分；
    /// 无图标时全部还原成美术摆好的值。位移都在运行时算，不改美术数值。
    /// </summary>
    private void ApplyIconTitleLayout(bool hasIcon)
    {
        RectTransform titleRect = titleText != null ? titleText.transform as RectTransform : null;
        if (titleRect == null)
            return;

        if (!baseLayoutCached)
        {
            titleBasePosition = titleRect.anchoredPosition;
            titleBaseSize = titleRect.sizeDelta;
            titleBaseFontSize = titleText.fontSize;
            if (bodyViewport != null)
            {
                bodyBasePosition = bodyViewport.anchoredPosition;
                bodyBaseSize = bodyViewport.sizeDelta;
            }
            baseLayoutCached = true;
        }

        if (!hasIcon)
        {
            titleText.fontSize = titleBaseFontSize;
            titleRect.anchoredPosition = titleBasePosition;
            titleRect.sizeDelta = titleBaseSize;
            if (bodyViewport != null)
            {
                bodyViewport.anchoredPosition = bodyBasePosition;
                bodyViewport.sizeDelta = bodyBaseSize;
            }
            return;
        }

        // 1) 标题变高（字号放大后需要的高度），正文整块下移同样的量（底边不动）。
        float extraHeight = Mathf.Max(0f, Mathf.Ceil(iconTitleFontSize * 1.2f) - titleBaseSize.y);
        titleText.fontSize = iconTitleFontSize;
        titleRect.anchoredPosition = titleBasePosition;
        titleRect.sizeDelta = new Vector2(titleBaseSize.x, titleBaseSize.y + extraHeight);
        if (bodyViewport != null)
        {
            bodyViewport.anchoredPosition = new Vector2(bodyBasePosition.x, bodyBasePosition.y - extraHeight * 0.5f);
            bodyViewport.sizeDelta = new Vector2(bodyBaseSize.x, bodyBaseSize.y - extraHeight);
        }

        // 2) 横向：标题左边缘移到「图标右边缘 + 间隔」。
        if (!(transform is RectTransform box) || enchantIconRoot == null)
            return;

        float titleLeft = box.InverseTransformPoint(titleRect.TransformPoint(new Vector3(titleRect.rect.xMin, 0f, 0f))).x;
        float iconRight = box.InverseTransformPoint(enchantIconRoot.TransformPoint(new Vector3(enchantIconRoot.rect.xMax, 0f, 0f))).x;
        float shift = iconRight + Mathf.Max(0f, iconTitleGap) - titleLeft;
        if (shift <= 0f)
            return;

        // 标题是「左边缘 = anchoredPosition.x」的枢轴（pivot.x = 0，横向拉伸），
        // 所以左边缘右移 shift 就是 anchoredPosition.x += shift，宽度同时减 shift（右边缘不动）。
        titleRect.anchoredPosition = new Vector2(titleBasePosition.x + shift, titleRect.anchoredPosition.y);
        titleRect.sizeDelta = new Vector2(titleBaseSize.x - shift, titleRect.sizeDelta.y);

        EnsureBodyBelowIcon();
    }

    /// <summary>
    /// 图标比标题行还高时（图标 50 / 标题行约 53），把正文顶边让到图标下方，避免正文首行被图标盖住。
    /// </summary>
    private void EnsureBodyBelowIcon()
    {
        if (!(transform is RectTransform box) || enchantIconRoot == null || bodyViewport == null)
            return;

        RectTransform bodyRect = bodyText != null ? bodyText.rectTransform : bodyViewport;
        float iconBottom = box.InverseTransformPoint(enchantIconRoot.TransformPoint(new Vector3(0f, enchantIconRoot.rect.yMin, 0f))).y;
        float bodyTop = box.InverseTransformPoint(bodyRect.TransformPoint(new Vector3(0f, bodyRect.rect.yMax, 0f))).y;
        float extra = bodyTop - (iconBottom - BodyIconGap);
        if (extra <= 0f)
            return;

        bodyViewport.anchoredPosition = new Vector2(bodyViewport.anchoredPosition.x, bodyViewport.anchoredPosition.y - extra);
        bodyViewport.sizeDelta = new Vector2(bodyViewport.sizeDelta.x, bodyViewport.sizeDelta.y - extra);
    }

    public void SetScrollInteractionEnabled(bool enabled)
    {
        CacheReferences();
        scrollInteractionEnabled = enabled;
        if (scrollInteractionEnabled && bodyCanScroll)
        {
            StopAutoScroll();
            manualScrollResumeTime = Time.unscaledTime + autoScrollStartDelay;
        }
        if (bodyScrollRect != null)
            bodyScrollRect.enabled = scrollInteractionEnabled && bodyCanScroll;
    }

    private void RefreshBodyScroll()
    {
        StopAutoScroll();
        bodyCanScroll = false;
        if (bodyText == null || bodyScrollRect == null || bodyViewport == null || bodyContent == null)
            return;

        Canvas.ForceUpdateCanvases();
        bodyText.ForceMeshUpdate();
        float viewportHeight = Mathf.Max(1f, bodyViewport.rect.height);
        float preferredHeight = Mathf.Ceil(bodyText.preferredHeight);
        float contentHeight = Mathf.Max(viewportHeight, preferredHeight);
        bodyCanScroll = preferredHeight > viewportHeight + 1f;
        bodyContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight);
        bodyText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight);
        bodyScrollRect.content = bodyContent;
        SetScrollPosition(1f);
        bodyScrollRect.enabled = scrollInteractionEnabled && bodyCanScroll;
        if (scrollInteractionEnabled && bodyCanScroll)
            manualScrollResumeTime = Time.unscaledTime + autoScrollStartDelay;
    }

    private void Update()
    {
        if (!bodyCanScroll || autoScrollTween != null)
            return;
        if (Time.unscaledTime < manualScrollResumeTime)
            return;

        StartAutoScroll();
    }

    private void PauseAutoScrollAfterManualInput()
    {
        StopAutoScroll();
        manualScrollResumeTime = Time.unscaledTime + manualScrollResumeDelay;
    }

    private void StartAutoScroll()
    {
        if (bodyScrollRect == null)
            return;

        Sequence sequence = DOTween.Sequence().SetTarget(this).SetUpdate(true);
        sequence.AppendInterval(autoScrollPause);
        sequence.Append(DOVirtual.Float(1f, 0f, autoScrollDuration, SetScrollPosition).SetEase(Ease.InOutSine));
        sequence.AppendInterval(autoScrollPause);
        sequence.Append(DOVirtual.Float(0f, 1f, autoScrollDuration, SetScrollPosition).SetEase(Ease.InOutSine));
        sequence.SetLoops(-1);
        autoScrollTween = sequence;
    }

    private void SetScrollPosition(float value)
    {
        if (bodyScrollRect == null)
            return;

        updatingAutoScroll = true;
        bodyScrollRect.verticalNormalizedPosition = value;
        updatingAutoScroll = false;
    }

    private void OnScrollPositionChanged(Vector2 value)
    {
        if (!updatingAutoScroll)
            PauseAutoScrollAfterManualInput();
    }

    private void StopAutoScroll()
    {
        autoScrollTween?.Kill(false);
        autoScrollTween = null;
    }

    private void Awake()
    {
        CacheReferences();
    }

    private void CacheReferences()
    {
        if (titleText == null)
            titleText = FindChildText("TitleText");
        if (bodyText == null)
            bodyText = FindChildText("BodyText");
        if (bodyRoot == null && bodyText != null)
            bodyRoot = bodyText.gameObject;
        if (bodyScrollRect == null)
            bodyScrollRect = GetComponentInChildren<ScrollRect>(true);
        if (bodyScrollRect != null && !scrollListenerRegistered)
        {
            bodyScrollRect.onValueChanged.AddListener(OnScrollPositionChanged);
            scrollListenerRegistered = true;
        }
        if (bodyViewport == null && bodyScrollRect != null)
            bodyViewport = bodyScrollRect.viewport;
        if (bodyContent == null && bodyScrollRect != null)
            bodyContent = bodyScrollRect.content;
        if (springLine == null)
            springLine = GetComponent<SpringLineHighlightUI>();
        if (frameGraphic == null)
            frameGraphic = springLine != null ? springLine : GetComponent<Graphic>();
    }

    private TMP_Text FindChildText(string childName)
    {
        Transform found = UIManager.FindChildRecursive(transform, childName);
        return found != null ? found.GetComponent<TMP_Text>() : null;
    }

    private void OnDisable()
    {
        StopAutoScroll();
    }

    private void OnDestroy()
    {
        if (bodyScrollRect != null && scrollListenerRegistered)
            bodyScrollRect.onValueChanged.RemoveListener(OnScrollPositionChanged);
        StopAutoScroll();
    }
}
