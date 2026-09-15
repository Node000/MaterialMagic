using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class MagicItemView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private TMP_Text magicNameText;
    [SerializeField] private RectTransform recipeRoot;
    [Header("施法序列")]
    [SerializeField] private Vector2 recipeIconSize = new Vector2(36f, 36f);
    [SerializeField] private Vector2 recipeIconSpacing = new Vector2(-14f, -14f);
    [SerializeField] private RectOffset recipeIconPadding = new RectOffset();
    [SerializeField] private Image modifierMarkerImage;
    [SerializeField] private SpringLineHighlightUI slotFrame;
    [SerializeField] private Button sellButton;
    [SerializeField] private TMP_Text sellButtonText;
    [SerializeField] private CanvasGroup sellButtonCanvasGroup;
    [SerializeField] private RectTransform tagTooltipRoot;
    [SerializeField] private TMP_Text tagTooltipText;
    [SerializeField] private bool showTagTooltipOnLeft;
    [SerializeField] private float tagTooltipXOffset = 12f;
    [SerializeField] private float tagTooltipSlideDistance = 28f;
    [SerializeField] private Vector2 tagTooltipSize = new Vector2(230f, 120f);
    [SerializeField] private float tagTooltipLineHeight = 22f;
    [SerializeField] private float tagTooltipVerticalPadding = 20f;
    [SerializeField] private float tooltipFadeDuration = 0.12f;
    [SerializeField] private float tooltipScaleDuration = 0.18f;
    [SerializeField] private Ease tooltipEase = Ease.OutBack;
    [Header("本地详情提示")]
    [SerializeField] private RectTransform localDetailTooltipRoot;
    [SerializeField] private TMP_Text localDetailTooltipTitleText;
    [SerializeField] private TMP_Text localDetailTooltipBodyText;
    [SerializeField] private CanvasGroup localDetailTooltipCanvasGroup;
    [Header("动画参数")]
    [SerializeField] private Vector3 tooltipHiddenScale = new Vector3(0.82f, 0.82f, 1f);
    [SerializeField] private float recipeHighlightPunchScale = 0.25f;
    [SerializeField] private float recipeHighlightDuration = 0.18f;
    [SerializeField] private int recipeHighlightVibrato = 6;
    [SerializeField] private float recipeHighlightElasticity = 0.6f;
    [SerializeField] private float castPulseScale = 0.16f;
    [SerializeField] private float castPulseDuration = 0.28f;
    [SerializeField] private int castPulseVibrato = 8;
    [SerializeField] private float castPulseElasticity = 0.65f;

    private readonly List<Image> recipeBlocks = new List<Image>();
    private readonly Color emptyBackgroundColor = new Color(0.08f, 0.08f, 0.12f, 1f);
    private MagicModel magic;
    private Tween pulseTween;
    private Tween modifierMarkerTween;
    private Tween localDetailTooltipTween;
    private bool warnedMissingBackgroundImage;
    private SpringLineHighlightUI hoverHighlight;
    private Sprite modifierMarkerFallbackSprite;
    private Material modifierMarkerFallbackMaterial;
    private Color modifierMarkerFallbackColor;
    private int slotIndex = -1;
    private Tween sellButtonTween;
    private bool raiseToFrontOnHover;
    private int hoverRaiseSiblingIndex = -1;
    private bool pointerHovering;

    private static readonly Dictionary<string, Sprite> magicIconCache = new Dictionary<string, Sprite>();
    private static Material sharedModifierMarkerFallbackMaterial;

    public MagicModel Magic => magic;
    public Button SellButton => sellButton;

    private void Awake()
    {
        CacheMissingReferences();
    }

    private void Start()
    {
        CacheMissingReferences();
    }

    private void OnDisable()
    {
        pointerHovering = false;
        ReleaseHoverRaise();
        // Kill(false) 会把缩放停在放大过程的中间值上，所以中断 Pulse 时必须复位到槽位基准缩放。
        if (pulseTween != null)
        {
            pulseTween.Kill(false);
            pulseTween = null;
            transform.localScale = GetPulseBaseScale();
        }
        modifierMarkerTween?.Kill(false);
        HideLocalDetailTooltip(true);
        HideSellPopupImmediate();
        UIManager uiManager = GetComponentInParent<UIManager>();
        uiManager?.HideUnifiedDetailPopup(this);
    }

    private void OnDestroy()
    {
        pulseTween?.Kill(false);
        modifierMarkerTween?.Kill(false);
        localDetailTooltipTween?.Kill(false);
        sellButtonTween?.Kill(false);
    }

    public void Bind(MagicModel magic)
    {
        if (magic == null)
        {
            this.magic = null;
            CacheMissingReferences();
            HideSellPopupImmediate();
            SetSlotFillVisible(false);

            SetIconVisible(false);

            if (backgroundImage != null)
                backgroundImage.color = emptyBackgroundColor;

            if (magicNameText != null)
                magicNameText.text = string.Empty;

            SetModifierMarker(null);
            SetHoverHighlightEnabled(true);
            RebuildRecipe();
            return;
        }

        this.magic = magic;
        CacheMissingReferences();
        HideSellPopupImmediate();
        SetSlotFillVisible(true);

        SetIconVisible(true);
        if (iconImage != null)
        {
            iconImage.sprite = LoadMagicIcon(magic.Data.iconName);
            iconImage.color = Color.white;
        }

        if (magicNameText != null)
            magicNameText.text = magic.Name;

        SetModifierMarker(magic.PrimaryModifier);
        SetHoverHighlightEnabled(true);
        RebuildRecipe();
    }

    public void SetCodexLockedVisual(string placeholderText, Sprite placeholderIcon)
    {
        CacheMissingReferences();

        if (iconImage != null)
        {
            iconImage.sprite = placeholderIcon;
            iconImage.color = Color.white;
            iconImage.preserveAspect = true;
            SetIconVisible(placeholderIcon != null);
        }

        if (magicNameText != null)
            magicNameText.text = placeholderText;

        SetModifierMarker(null);
        HideSellPopupImmediate();
    }

    public void SetSlotIndex(int index)
    {
        slotIndex = index;
    }

    /// <summary>
    /// 由 HandSystemUI 对道具栏槽位打开：Hover 时把槽位提到同级最后，
    /// 让放大后的图标与弹簧线框绘制在相邻道具之上，而不是被它们盖住。
    /// </summary>
    public void SetRaiseToFrontOnHover(bool value)
    {
        raiseToFrontOnHover = value;
        if (!value)
            ReleaseHoverRaise();
    }

    private void RaiseToFrontOnHover()
    {
        if (!raiseToFrontOnHover)
            return;

        Transform parent = transform.parent;
        if (parent == null)
            return;

        if (hoverRaiseSiblingIndex < 0)
            hoverRaiseSiblingIndex = transform.GetSiblingIndex();

        if (transform.GetSiblingIndex() != parent.childCount - 1)
            transform.SetAsLastSibling();
    }

    /// <summary>指针是否停在本槽位上（Hover 状态），跨重建后仍可用它恢复提层。</summary>
    public bool IsPointerHovering => pointerHovering;

    /// <summary>
    /// 重建/重排后重新提层：指针还停在槽位上时把提层补回来，
    /// 避免一直悬停的道具在重建后失去提层、又被相邻道具盖住。
    /// </summary>
    public void ReapplyHoverRaiseIfHovering()
    {
        if (pointerHovering)
            RaiseToFrontOnHover();
    }

    /// <summary>
    /// 结束 Hover 提层，把槽位放回原来的兄弟索引。
    /// 道具栏的圆弧布局与“槽位壳↔槽位索引”绑定都按子物体顺序读取，
    /// 所以任何按顺序读取或重建子物体的流程（重排、绑定、飞入动画）都必须先调用它。
    /// </summary>
    public void ReleaseHoverRaise()
    {
        if (hoverRaiseSiblingIndex < 0)
            return;

        Transform parent = transform.parent;
        if (parent != null)
            transform.SetSiblingIndex(Mathf.Clamp(hoverRaiseSiblingIndex, 0, parent.childCount - 1));

        hoverRaiseSiblingIndex = -1;
    }

    public int GetSlotIndex()
    {
        return slotIndex;
    }

    public void ShowSellPopup(int price)
    {
        if (sellButton == null || magic == null)
            return;

        CacheSellButton();
        sellButtonTween?.Kill(false);
        sellButton.gameObject.SetActive(true);
        sellButton.transform.localScale = tooltipHiddenScale;
        if (sellButtonText != null)
            sellButtonText.text = "卖出 " + price + "$";
        if (sellButtonCanvasGroup != null)
        {
            sellButtonCanvasGroup.alpha = 0f;
            sellButtonCanvasGroup.blocksRaycasts = true;
            sellButtonCanvasGroup.interactable = true;
        }
        sellButton.interactable = true;

        Sequence sequence = DOTween.Sequence().SetTarget(this);
        if (sellButtonCanvasGroup != null)
            sequence.Join(sellButtonCanvasGroup.DOFade(1f, tooltipFadeDuration));
        sequence.Join(sellButton.transform.DOScale(Vector3.one, tooltipScaleDuration).SetEase(tooltipEase));
        sellButtonTween = sequence;
    }

    public void HideSellPopup()
    {
        if (sellButton == null || !sellButton.gameObject.activeSelf)
            return;

        sellButtonTween?.Kill(false);
        sellButton.interactable = false;
        if (sellButtonCanvasGroup != null)
        {
            sellButtonCanvasGroup.blocksRaycasts = false;
            sellButtonCanvasGroup.interactable = false;
        }

        Sequence sequence = DOTween.Sequence().SetTarget(this);
        if (sellButtonCanvasGroup != null)
            sequence.Join(sellButtonCanvasGroup.DOFade(0f, tooltipFadeDuration));
        sequence.Join(sellButton.transform.DOScale(tooltipHiddenScale, tooltipScaleDuration).SetEase(tooltipEase));
        sequence.OnComplete(HideSellPopupImmediate);
        sellButtonTween = sequence;
    }

    public void HideSellPopupImmediate()
    {
        sellButtonTween?.Kill(false);
        sellButtonTween = null;
        if (sellButton == null)
            return;

        sellButton.interactable = false;
        if (sellButtonCanvasGroup != null)
        {
            sellButtonCanvasGroup.alpha = 0f;
            sellButtonCanvasGroup.blocksRaycasts = false;
            sellButtonCanvasGroup.interactable = false;
        }
        sellButton.transform.localScale = tooltipHiddenScale;
        sellButton.gameObject.SetActive(false);
    }

    private void OnSellButtonClicked()
    {
        if (slotIndex < 0)
            return;

        GetComponentInParent<HandSystemUI>()?.TrySellMagicAtSlot(slotIndex);
    }

    public void ResetRecipeHighlights()
    {
        for (int i = 0; i < recipeBlocks.Count; i++)
            SetBlockOpaque(recipeBlocks[i]);
    }

    public void HighlightRecipeSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= recipeBlocks.Count)
            return;

        SetBlockOpaque(recipeBlocks[slotIndex]);
        recipeBlocks[slotIndex].transform.DOKill(false);
        recipeBlocks[slotIndex].transform.DOPunchScale(Vector3.one * recipeHighlightPunchScale, recipeHighlightDuration, recipeHighlightVibrato, recipeHighlightElasticity).SetTarget(this);
    }

    /// <summary>
    /// 打出素材触发本道具时的“放大”反馈。
    /// 基准缩放不能写死 Vector3.one：道具栏的圆弧布局（MagicBookCurveLayout）会按槽位位置把槽根设成 0.88~1，
    /// 若从这里复位成 1，两端槽位在反馈结束后会停在放大状态。
    /// 基准缩放以 JuicyMotion 同步的圆弧基准值为准（布局每次排布都会通过 SetBaseTransform 同步它），
    /// 并在 Punch 结束时显式复位，保证“放大”一定回到槽位自身的缩放。
    /// </summary>
    public void PulseCast()
    {
        pulseTween?.Kill(false);
        Vector3 baseScale = GetPulseBaseScale();
        transform.localScale = baseScale;
        pulseTween = transform.DOPunchScale(baseScale * castPulseScale, castPulseDuration, castPulseVibrato, castPulseElasticity)
            .SetTarget(this)
            .OnComplete(() => transform.localScale = baseScale);
    }

    /// <summary>Pulse 的基准缩放：优先取 JuicyMotion 里由圆弧布局同步的基准值，取不到才回退当前缩放。</summary>
    private Vector3 GetPulseBaseScale()
    {
        JuicyMotion motion = GetComponent<JuicyMotion>();
        if (motion != null)
        {
            Vector3 motionBase = motion.BaseScale;
            if (motionBase.x > 0.0001f && motionBase.y > 0.0001f)
                return motionBase;
        }

        Vector3 current = transform.localScale;
        return current.x > 0.0001f && current.y > 0.0001f ? current : Vector3.one;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerHovering = true;
        RaiseToFrontOnHover();

        UnifiedDetailContent content = magic != null ? UnifiedDetailContentBuilder.Build(magic) : UnifiedDetailContentBuilder.BuildEmptyMagicSlot();
        if (localDetailTooltipRoot != null)
        {
            ShowLocalDetailTooltip(content);
            return;
        }

        UIManager uiManager = GetComponentInParent<UIManager>();
        uiManager?.ShowUnifiedDetailPopup(this, content);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerHovering = false;
        ReleaseHoverRaise();

        if (localDetailTooltipRoot != null)
        {
            HideLocalDetailTooltip(false);
            return;
        }

        UIManager uiManager = GetComponentInParent<UIManager>();
        uiManager?.HideUnifiedDetailPopup(this);
    }

    private void ShowLocalDetailTooltip(UnifiedDetailContent content)
    {
        if (localDetailTooltipTitleText != null)
            localDetailTooltipTitleText.text = content.Title;
        if (localDetailTooltipBodyText != null)
            localDetailTooltipBodyText.text = content.Body;

        localDetailTooltipTween?.Kill(false);
        localDetailTooltipRoot.gameObject.SetActive(true);
        localDetailTooltipRoot.SetAsLastSibling();
        if (localDetailTooltipCanvasGroup != null)
        {
            localDetailTooltipCanvasGroup.alpha = 0f;
            localDetailTooltipCanvasGroup.blocksRaycasts = false;
            localDetailTooltipTween = localDetailTooltipCanvasGroup.DOFade(1f, tooltipFadeDuration).SetTarget(this);
        }
    }

    private void HideLocalDetailTooltip(bool instant)
    {
        if (localDetailTooltipRoot == null)
            return;

        localDetailTooltipTween?.Kill(false);
        if (instant || localDetailTooltipCanvasGroup == null)
        {
            localDetailTooltipRoot.gameObject.SetActive(false);
            return;
        }

        localDetailTooltipTween = localDetailTooltipCanvasGroup.DOFade(0f, tooltipFadeDuration).SetTarget(this)
            .OnComplete(() => localDetailTooltipRoot.gameObject.SetActive(false));
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
            return;

        UIManager uiManager = GetComponentInParent<UIManager>();
        if (uiManager != null)
            uiManager.PinUnifiedDetailPopup(this, magic != null ? UnifiedDetailContentBuilder.Build(magic) : UnifiedDetailContentBuilder.BuildEmptyMagicSlot());

        ForwardClickToParentButton(eventData);
    }

    private void ForwardClickToParentButton(PointerEventData eventData)
    {
        Transform current = transform.parent;
        while (current != null)
        {
            Button button = current.GetComponent<Button>();
            if (button != null && button.IsActive() && button.interactable)
            {
                button.OnPointerClick(eventData);
                return;
            }
            current = current.parent;
        }
    }

    private void CacheMissingReferences()
    {
        Graphic raycastGraphic = GetComponent<Graphic>();
        if (raycastGraphic != null)
            raycastGraphic.raycastTarget = true;

        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();

        EnsureModifierMarker();
        CacheSlotFrame();
        CacheSellButton();

        if (backgroundImage == null && !warnedMissingBackgroundImage)
        {
            warnedMissingBackgroundImage = true;
            GameLog.Data($"MagicItemView missing background image on {name}");
        }
    }

    private void SetIconVisible(bool visible)
    {
        if (iconImage == null)
            return;

        Transform iconRoot = iconImage.transform;
        while (iconRoot.parent != null && iconRoot.parent != transform)
            iconRoot = iconRoot.parent;

        if (iconRoot != null)
            iconRoot.gameObject.SetActive(visible);
        iconImage.gameObject.SetActive(visible);
    }

    private void SetSlotFillVisible(bool visible)
    {
        CacheSlotFrame();
        if (slotFrame == null)
            return;

        slotFrame.gameObject.SetActive(true);
        slotFrame.SetFillEnabled(visible);
    }

    private void CacheSlotFrame()
    {
        if (slotFrame == null)
            slotFrame = GetComponent<SpringLineHighlightUI>();
    }

    private void CacheSellButton()
    {
        if (sellButton == null)
            sellButton = GetComponentInChildren<Button>(true);
        if (sellButton == null)
            return;

        if (sellButtonText == null)
            sellButtonText = sellButton.GetComponentInChildren<TMP_Text>(true);
        if (sellButtonCanvasGroup == null)
            sellButtonCanvasGroup = sellButton.GetComponent<CanvasGroup>();

        sellButton.onClick.RemoveListener(OnSellButtonClicked);
        sellButton.onClick.AddListener(OnSellButtonClicked);
    }

    private void SetHoverHighlightEnabled(bool enabled)
    {
        SpringLineHighlightUI highlight = GetHoverHighlight();
        if (highlight == null)
            return;

        HoverHighlightTargetRelayUI relay = this.GetComponent<HoverHighlightTargetRelayUI>();
        if (!enabled)
        {
            relay?.Unregister(highlight.gameObject);
            highlight.Hide();
            return;
        }

        highlight.SetHoverTarget(gameObject);
        highlight.Hide();
    }

    private SpringLineHighlightUI GetHoverHighlight()
    {
        if (hoverHighlight != null)
            return hoverHighlight;

        SpringLineHighlightUI[] highlights = this.GetComponentsInChildren<SpringLineHighlightUI>(true);
        for (int i = 0; i < highlights.Length; i++)
        {
            if (highlights[i] != null && highlights[i].transform != transform)
            {
                hoverHighlight = highlights[i];
                return hoverHighlight;
            }
        }
        return null;
    }

    private void EnsureModifierMarker()
    {
        if (modifierMarkerImage == null)
        {
            Transform existing = transform.Find("ModifierMarker");
            if (existing != null)
                modifierMarkerImage = existing.GetComponent<Image>();
        }

        if (modifierMarkerImage == null)
        {
            modifierMarkerImage = new GameObject("ModifierMarker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            modifierMarkerImage.transform.SetParent(transform, false);
            modifierMarkerImage.raycastTarget = false;
            RectTransform rect = modifierMarkerImage.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(14f, -14f);
            rect.sizeDelta = new Vector2(18f, 18f);
        }

        if (modifierMarkerFallbackSprite == null)
            modifierMarkerFallbackSprite = modifierMarkerImage.sprite;
        if (modifierMarkerFallbackMaterial == null)
            modifierMarkerFallbackMaterial = modifierMarkerImage.material;
        if (modifierMarkerFallbackColor == default)
            modifierMarkerFallbackColor = modifierMarkerImage.color;

        if (modifierMarkerFallbackMaterial == null)
        {
            if (sharedModifierMarkerFallbackMaterial == null)
            {
                Shader shader = Shader.Find("UI/MagicModifierBreath");
                if (shader != null)
                    sharedModifierMarkerFallbackMaterial = new Material(shader);
            }
            modifierMarkerFallbackMaterial = sharedModifierMarkerFallbackMaterial;
            if (modifierMarkerFallbackMaterial != null)
                modifierMarkerImage.material = modifierMarkerFallbackMaterial;
        }
    }

    private void SetModifierMarker(MagicModifierModel modifier)
    {
        EnsureModifierMarker();
        if (modifierMarkerImage == null)
            return;

        modifierMarkerTween?.Kill(false);
        bool visible = modifier != null;
        modifierMarkerImage.gameObject.SetActive(visible);
        if (!visible)
            return;

        Sprite modifierIcon = MagicModifierIconDatabase.Get(modifier);
        if (modifierIcon != null)
        {
            modifierMarkerImage.sprite = modifierIcon;
            modifierMarkerImage.material = null;
            modifierMarkerImage.color = Color.white;
            modifierMarkerImage.preserveAspect = true;
        }
        else
        {
            modifierMarkerImage.sprite = modifierMarkerFallbackSprite;
            modifierMarkerImage.material = modifierMarkerFallbackMaterial;
            modifierMarkerImage.color = modifierMarkerFallbackColor;
            modifierMarkerImage.preserveAspect = false;
        }

        Color baseColor = modifierMarkerImage.color;
        modifierMarkerImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, 1f);
        modifierMarkerTween = modifierMarkerImage.DOFade(1f, 0.86f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetTarget(this);
    }

    private void RebuildRecipe()
    {
        if (recipeRoot == null)
            return;

        GridLayoutGroup recipeLayout = recipeRoot.GetComponent<GridLayoutGroup>();
        if (recipeLayout == null)
            return;
        recipeLayout.cellSize = recipeIconSize;
        recipeLayout.spacing = recipeIconSpacing;
        recipeLayout.padding = recipeIconPadding;

        recipeBlocks.Clear();
        MaterialEnum[] recipe = magic != null ? magic.GetEffectiveRecipe() : null;
        int recipeCount = recipe != null ? recipe.Length : 0;
        for (int i = 0; i < recipeRoot.childCount; i++)
            recipeRoot.GetChild(i).gameObject.SetActive(i < recipeCount);

        for (int i = recipeRoot.childCount; i < recipeCount; i++)
        {
            Image block = new GameObject("MaterialBlock", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            block.transform.SetParent(recipeRoot, false);
        }

        for (int i = 0; i < recipeCount; i++)
        {
            Image block = recipeRoot.GetChild(i).GetComponent<Image>();
            if (block == null)
                block = recipeRoot.GetChild(i).gameObject.AddComponent<Image>();
            block.gameObject.SetActive(true);
            Sprite materialSprite = GetRecipeIcon(recipe[i]);
            block.sprite = materialSprite;
            block.preserveAspect = true;
            block.color = GetRecipeIconColor(recipe[i]);
            SetBlockOpaque(block);
            recipeBlocks.Add(block);

        }
    }


    private static readonly Dictionary<MaterialEnum, Sprite> recipeIconCache = new Dictionary<MaterialEnum, Sprite>();

    /// <summary>箭头序列（施法序列）图标；详情面板的序列小线框复用同一套美术。</summary>
    public static Sprite GetRecipeIcon(MaterialEnum material)
    {
        if (recipeIconCache.TryGetValue(material, out Sprite sprite))
            return sprite;

        string path = GetRecipeIconPath(material);
        sprite = !string.IsNullOrEmpty(path) ? Resources.Load<Sprite>(path) : null;
        recipeIconCache[material] = sprite;
        return sprite;
    }

    private static string GetRecipeIconPath(MaterialEnum material)
    {
        switch (material)
        {
            case MaterialEnum.Fire:
                return "Images/UI/up";
            case MaterialEnum.Wind:
                return "Images/UI/left";
            case MaterialEnum.Water:
                return "Images/UI/down";
            case MaterialEnum.Earth:
                return "Images/UI/right";
            default:
                return null;
        }
    }

    private Color GetRecipeIconColor(MaterialEnum material)
    {
        return Color.white;
    }


    private static Sprite LoadMagicIcon(string iconName)
    {
        if (string.IsNullOrEmpty(iconName))
            return null;

        if (magicIconCache.TryGetValue(iconName, out Sprite sprite))
            return sprite;

        sprite = Resources.Load<Sprite>("Images/Magics/" + iconName);
        magicIconCache[iconName] = sprite;
        return sprite;
    }

    private static void SetBlockOpaque(Image block)
    {
        CanvasGroup canvasGroup = block.GetComponent<CanvasGroup>();
        if (canvasGroup != null)
            canvasGroup.alpha = 1f;

        Color color = block.color;
        color.a = 1f;
        block.color = color;
    }
}
