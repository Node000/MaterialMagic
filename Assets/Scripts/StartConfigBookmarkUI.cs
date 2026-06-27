using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class StartConfigBookmarkUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private RectTransform rectTransform;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image textureImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private RectTransform magicRoot;
    [SerializeField] private RectTransform materialRoot;
    [SerializeField] private MagicItemView magicViewPrefab;
    [SerializeField] private MaterialCardView materialCardPrefab;
    [Header("魔法预览布局")]
    [SerializeField, Min(1)] private int magicPreviewSlotCount = 6;
    [SerializeField] private Vector2 magicItemFallbackSize = new Vector2(196f, 92f);
    [SerializeField] private Vector2 magicItemFallbackSpacing = new Vector2(142f, 72f);
    [SerializeField, Min(1)] private int magicItemFallbackColumns = 3;
    [SerializeField] private float magicItemScale = 0.68f;
    [Header("素材预览布局")]
    [Tooltip("0 表示按 StartConfig initialMaterials 数量生成；大于 0 时固定生成该数量的道具格，不足则留空。")]
    [SerializeField, Min(0)] private int materialPreviewSlotCount;
    [SerializeField] private Vector2 materialItemFallbackSize = new Vector2(118f, 72f);
    [SerializeField] private Vector2 materialItemFallbackSpacing = new Vector2(126f, 76f);
    [SerializeField, Min(1)] private int materialItemFallbackColumns = 2;
    [SerializeField] private float materialCardScale = 0.42f;
    [SerializeField] private Vector2 materialCardAnchoredPosition = Vector2.zero;
    [SerializeField] private Color materialPreviewFrameColor = Color.clear;
    [SerializeField] private bool materialPreviewFrameRaycastTarget = true;
    [SerializeField] private bool materialPreviewShadowEnabled;
    [SerializeField] private Vector2 materialPreviewIconAnchorMin = new Vector2(0.5f, 0.5f);
    [SerializeField] private Vector2 materialPreviewIconAnchorMax = new Vector2(0.5f, 0.5f);
    [SerializeField] private Vector2 materialPreviewIconPivot = new Vector2(0.5f, 0.5f);
    [SerializeField] private Vector2 materialPreviewIconAnchoredPosition = Vector2.zero;
    [SerializeField] private Vector2 materialPreviewIconSize = new Vector2(96f, 96f);
    [SerializeField] private Color materialPreviewIconColor = Color.white;
    [SerializeField] private bool materialPreviewIconRaycastTarget;
    [SerializeField] private bool materialPreviewIconPreserveAspect = true;
    [SerializeField] private Vector2 materialCountTextAnchorMin = new Vector2(1f, 0.5f);
    [SerializeField] private Vector2 materialCountTextAnchorMax = new Vector2(1f, 0.5f);
    [SerializeField] private Vector2 materialCountTextPivot = new Vector2(1f, 0.5f);
    [SerializeField] private Vector2 materialCountTextAnchoredPosition = new Vector2(0f, -6f);
    [SerializeField] private Vector2 materialCountTextSize = new Vector2(52f, 28f);
    [SerializeField] private TMP_FontAsset materialCountTextFont;
    [SerializeField] private float materialCountTextFontSize = 22f;
    [SerializeField] private FontStyles materialCountTextFontStyle = FontStyles.Bold;
    [SerializeField] private TextAlignmentOptions materialCountTextAlignment = TextAlignmentOptions.MidlineRight;
    [SerializeField] private Color materialCountTextColor = new Color(1f, 0.94f, 0.7f, 1f);
    [SerializeField] private Button button;
    [SerializeField] private Button selectButton;
    [SerializeField] private Image selectButtonImage;
    [SerializeField] private TMP_Text selectButtonText;
    [SerializeField] private Button windowCloseButton;
    [SerializeField] private string selectTextKey = "ui.start_config.select";
    [SerializeField] private string selectedTextKey = "ui.start_config.selected";
    [Header("选择按钮颜色")]
    [SerializeField] private Color selectButtonUnselectedOutlineColor = new Color(0.18f, 0.68f, 0.28f, 1f);
    [SerializeField] private Color selectButtonUnselectedTextColor = new Color(0.18f, 0.68f, 0.28f, 1f);
    [SerializeField] private Color selectButtonSelectedFillColor = new Color(0.18f, 0.68f, 0.28f, 1f);
    [SerializeField] private Color selectButtonSelectedTextColor = Color.white;
    [Header("书签移动")]
    [SerializeField] private float enterDuration = 0.34f;
    [SerializeField] private float selectDuration = 0.34f;
    [SerializeField] private float exitDuration = 0.34f;
    [SerializeField] private Ease enterEase = Ease.OutCubic;
    [SerializeField] private Ease selectEase = Ease.OutCubic;
    [SerializeField] private Ease exitEase = Ease.OutCubic;
    [Header("浮动")]
    [SerializeField] private Vector2 floatAmplitude = new Vector2(10f, 6f);
    [SerializeField] private float floatSpeed = 0.75f;
    [SerializeField] private float floatPhaseStep = 0.8f;

    private readonly List<MagicItemView> magicViews = new List<MagicItemView>();
    private readonly List<GameObject> materialItems = new List<GameObject>();
    private Action<PlayerStartConfigData> onClick;
    private Action<StartConfigBookmarkUI> onClose;
    private Tween moveTween;
    private Tween scaleTween;
    private bool selected;
    private bool visible;
    private bool dragging;
    private Vector2 floatCenter;
    private Vector2 currentFloatOffset;
    private Vector2 dragOffset;
    private float floatPhase;

    public PlayerStartConfigData Config { get; private set; }
    public RectTransform RectTransform => rectTransform != null ? rectTransform : (RectTransform)transform;

    private void Awake()
    {
        ResolveReferences();
        if (selectButton != null)
            selectButton.onClick.AddListener(HandleClick);
        if (windowCloseButton != null)
            windowCloseButton.onClick.AddListener(HandleClose);
    }

    private void OnDestroy()
    {
        if (selectButton != null)
            selectButton.onClick.RemoveListener(HandleClick);
        if (windowCloseButton != null)
            windowCloseButton.onClick.RemoveListener(HandleClose);
        KillTweens();
    }

    private void Update()
    {
        if (!visible || dragging || (moveTween != null && moveTween.IsActive() && moveTween.IsPlaying()))
            return;

        float t = Time.unscaledTime * floatSpeed + floatPhase;
        currentFloatOffset = new Vector2(Mathf.Sin(t) * floatAmplitude.x, Mathf.Sin(t * 0.73f + floatPhaseStep) * floatAmplitude.y);
        RectTransform.anchoredPosition = floatCenter + currentFloatOffset;
    }

    public void Bind(PlayerStartConfigData config, Action<PlayerStartConfigData> clickHandler, Action<StartConfigBookmarkUI> closeHandler = null)
    {
        Config = config;
        onClick = clickHandler;
        onClose = closeHandler;
        selected = false;

        if (nameText != null)
            nameText.text = !string.IsNullOrEmpty(config.displayName) ? config.displayName : config.id;
        if (healthText != null)
            healthText.text = string.Format(LocalizationSystem.GetText("ui.start_config.health", "生命值 {0}"), config.maxHealth);

        Color color = Color.white;
        if (!string.IsNullOrEmpty(config.color))
            ColorUtility.TryParseHtmlString(config.color, out color);
        color.a = 1f;
        if (backgroundImage != null)
            backgroundImage.color = color;

        if (textureImage != null)
        {
            textureImage.sprite = string.IsNullOrEmpty(config.texturePath) ? null : Resources.Load<Sprite>(config.texturePath);
            textureImage.enabled = textureImage.sprite != null;
        }

        RebuildMagicViews(config.initialMagics);
        RebuildMaterialViews(config.initialMaterials);
        RefreshSelectButtonState();
        RectTransform.localScale = Vector3.one;
    }

    public void Show(float initialX, float readyX, float delay)
    {
        visible = true;
        currentFloatOffset = Vector2.zero;
        floatPhase = transform.GetSiblingIndex() * floatPhaseStep;
        SetCenter(new Vector2(readyX, RectTransform.anchoredPosition.y));
        moveTween?.Kill(false);
        scaleTween?.Kill(false);
        RectTransform.localScale = Vector3.zero;
        moveTween = RectTransform.DOScale(Vector3.one, enterDuration)
            .SetDelay(delay)
            .SetEase(enterEase)
            .SetUpdate(true)
            .SetTarget(this);
    }

    public void Hide(float initialX, float delay, Action<StartConfigBookmarkUI> onComplete)
    {
        visible = false;
        currentFloatOffset = Vector2.zero;
        SetCenter(RectTransform.anchoredPosition);
        moveTween?.Kill(false);
        scaleTween?.Kill(false);
        moveTween = RectTransform.DOScale(Vector3.zero, exitDuration)
            .SetDelay(delay)
            .SetEase(exitEase)
            .SetUpdate(true)
            .SetTarget(this)
            .OnComplete(() => onComplete?.Invoke(this));
    }

    public void SetSelected(bool selected, float readyX, float displayX)
    {
        this.selected = selected;
        RefreshSelectButtonState();
        scaleTween?.Kill(false);
        scaleTween = RectTransform.DOScale(selected ? Vector3.one * 1.035f : Vector3.one, selectDuration).SetEase(selectEase).SetUpdate(true).SetTarget(this);
    }

    public void SetSelectedImmediate(bool selected)
    {
        this.selected = selected;
        RefreshSelectButtonState();
        scaleTween?.Kill(false);
        RectTransform.localScale = selected ? Vector3.one * 1.035f : Vector3.one;
    }

    public void HideImmediate()
    {
        visible = false;
        dragging = false;
        currentFloatOffset = Vector2.zero;
        KillTweens();
        RectTransform.localScale = Vector3.zero;
        gameObject.SetActive(false);
    }

    public void KillTweens()
    {
        moveTween?.Kill(false);
        scaleTween?.Kill(false);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!CanDragFrom(eventData))
            return;

        RectTransform parent = RectTransform.parent as RectTransform;
        if (parent == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, eventData.position, GetEventCamera(eventData), out Vector2 localPoint))
            return;

        moveTween?.Kill(false);
        currentFloatOffset = Vector2.zero;
        SetCenter(RectTransform.anchoredPosition);
        dragging = true;
        dragOffset = floatCenter - localPoint;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragging)
            return;

        RectTransform parent = RectTransform.parent as RectTransform;
        if (parent == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, eventData.position, GetEventCamera(eventData), out Vector2 localPoint))
            return;

        SetCenter(localPoint + dragOffset);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        dragging = false;
        currentFloatOffset = Vector2.zero;
        SetCenter(RectTransform.anchoredPosition);
    }

    private void ResolveReferences()
    {
        if (rectTransform == null)
            rectTransform = (RectTransform)transform;
        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();
        if (button == null)
            button = GetComponent<Button>();
        if (selectButton == null)
            selectButton = transform.Find("SelectButton")?.GetComponent<Button>();
        if (selectButton == null)
            selectButton = button;
        if (selectButtonImage == null && selectButton != null)
        {
            selectButtonImage = selectButton.targetGraphic as Image;
            if (selectButtonImage == null)
                selectButtonImage = selectButton.GetComponent<Image>();
        }
        if (selectButtonText == null && selectButton != null)
            selectButtonText = selectButton.GetComponentInChildren<TMP_Text>(true);
        if (windowCloseButton == null)
            windowCloseButton = transform.Find("PopupDragonWindowBackground/Frame/TitleBar/Close")?.GetComponent<Button>();
    }

    private bool CanDragFrom(PointerEventData eventData)
    {
        Transform hit = eventData.pointerPressRaycast.gameObject != null
            ? eventData.pointerPressRaycast.gameObject.transform
            : eventData.pointerCurrentRaycast.gameObject != null ? eventData.pointerCurrentRaycast.gameObject.transform : null;
        if (hit == null || !hit.IsChildOf(transform))
            return false;
        if (selectButton != null && hit.IsChildOf(selectButton.transform))
            return false;
        if (windowCloseButton != null && hit.IsChildOf(windowCloseButton.transform))
            return false;
        if (magicRoot != null && hit.IsChildOf(magicRoot))
            return false;
        if (materialRoot != null && hit.IsChildOf(materialRoot))
            return false;
        Button hitButton = hit.GetComponentInParent<Button>();
        if (hitButton != null && hitButton != button)
            return false;
        return true;
    }

    private Camera GetEventCamera(PointerEventData eventData)
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;
        return eventData.pressEventCamera != null ? eventData.pressEventCamera : canvas != null ? canvas.worldCamera : null;
    }

    private void SetCenter(Vector2 center)
    {
        floatCenter = center;
        RectTransform.anchoredPosition = floatCenter + currentFloatOffset;
    }

    private void RefreshSelectButtonState()
    {
        if (selectButtonImage != null)
        {
            selectButtonImage.color = selected ? selectButtonSelectedFillColor : selectButtonUnselectedOutlineColor;
            selectButtonImage.fillCenter = selected;
        }

        if (selectButtonText != null)
        {
            selectButtonText.text = LocalizationSystem.GetText(selected ? selectedTextKey : selectTextKey, selected ? "已选" : "选择");
            selectButtonText.color = selected ? selectButtonSelectedTextColor : selectButtonUnselectedTextColor;
        }
    }

    private void HandleClick()
    {
        onClick?.Invoke(Config);
    }

    private void HandleClose()
    {
        onClose?.Invoke(this);
    }

    private void RebuildMagicViews(PlayerStartMagicData[] magics)
    {
        for (int i = 0; i < magicViews.Count; i++)
        {
            if (magicViews[i] != null)
                Destroy(magicViews[i].gameObject);
        }
        magicViews.Clear();

        if (magicRoot == null || magicViewPrefab == null)
            return;

        for (int i = 0; i < magicPreviewSlotCount; i++)
        {
            MagicItemView view = Instantiate(magicViewPrefab, magicRoot);
            view.gameObject.SetActive(true);
            RectTransform rect = view.transform as RectTransform;
            rect.localScale = Vector3.one * magicItemScale;
            ApplyPreviewItemLayout(rect, magicRoot, i, magicItemFallbackColumns, magicItemFallbackSize, magicItemFallbackSpacing);
            view.Bind(GetMagicForSlot(magics, i));
            magicViews.Add(view);
        }
    }

    private void RebuildMaterialViews(PlayerStartMaterialData[] materials)
    {
        for (int i = 0; i < materialItems.Count; i++)
        {
            if (materialItems[i] != null)
                Destroy(materialItems[i]);
        }
        materialItems.Clear();

        if (materialRoot == null)
            return;

        int slotCount = GetMaterialPreviewSlotCount(materials);
        for (int i = 0; i < slotCount; i++)
        {
            PlayerStartMaterialData data = materials != null && i < materials.Length ? materials[i] : null;

            GameObject item = new GameObject("MaterialItem", typeof(RectTransform));
            item.transform.SetParent(materialRoot, false);
            RectTransform itemRect = item.GetComponent<RectTransform>();
            ApplyPreviewItemLayout(itemRect, materialRoot, i, materialItemFallbackColumns, materialItemFallbackSize, materialItemFallbackSpacing);

            if (data != null && materialCardPrefab != null)
            {
                MaterialCardView card = Instantiate(materialCardPrefab, itemRect);
                card.gameObject.SetActive(true);
                RectTransform cardRect = card.transform as RectTransform;
                cardRect.localScale = Vector3.one * materialCardScale;
                cardRect.anchoredPosition = materialCardAnchoredPosition;
                card.Bind(new MaterialModel(data.material + "_preview", data.material));
                ConfigureMaterialPreviewCard(cardRect);

                TMP_Text countText = CreateCountText(itemRect);
                countText.text = "×" + data.count;
            }
            materialItems.Add(item);
        }
    }

    private int GetMaterialPreviewSlotCount(PlayerStartMaterialData[] materials)
    {
        return materialPreviewSlotCount > 0 ? materialPreviewSlotCount : materials != null ? materials.Length : 0;
    }

    private void ConfigureMaterialPreviewCard(RectTransform cardRect)
    {
        if (cardRect == null)
            return;

        Image frameImage = cardRect.GetComponent<Image>();
        if (frameImage != null)
        {
            frameImage.color = materialPreviewFrameColor;
            frameImage.raycastTarget = materialPreviewFrameRaycastTarget;
        }

        Shadow shadow = cardRect.GetComponent<Shadow>();
        if (shadow != null)
            shadow.enabled = materialPreviewShadowEnabled;

        Transform icon = cardRect.Find("Icon");
        if (icon != null)
        {
            RectTransform iconRect = icon as RectTransform;
            if (iconRect != null)
            {
                iconRect.anchorMin = materialPreviewIconAnchorMin;
                iconRect.anchorMax = materialPreviewIconAnchorMax;
                iconRect.pivot = materialPreviewIconPivot;
                iconRect.anchoredPosition = materialPreviewIconAnchoredPosition;
                iconRect.sizeDelta = materialPreviewIconSize;
            }

            Image iconImage = icon.GetComponent<Image>();
            if (iconImage != null)
            {
                iconImage.color = materialPreviewIconColor;
                iconImage.raycastTarget = materialPreviewIconRaycastTarget;
                iconImage.preserveAspect = materialPreviewIconPreserveAspect;
            }
        }
    }

    private static void ApplyPreviewItemLayout(RectTransform rect, RectTransform root, int index, int columns, Vector2 fallbackSize, Vector2 fallbackSpacing)
    {
        if (rect == null)
            return;

        if (UsesLayoutGroup(root))
            return;

        int safeColumns = Mathf.Max(1, columns);
        rect.sizeDelta = fallbackSize;
        rect.anchoredPosition = new Vector2((index % safeColumns) * fallbackSpacing.x, -(index / safeColumns) * fallbackSpacing.y);
    }

    private static bool UsesLayoutGroup(RectTransform root)
    {
        if (root == null)
            return false;

        LayoutGroup layoutGroup = root.GetComponent<LayoutGroup>();
        return layoutGroup != null && layoutGroup.enabled;
    }

    private static MagicModel GetMagicForSlot(PlayerStartMagicData[] magics, int slotIndex)
    {
        if (magics == null)
            return null;

        for (int i = 0; i < magics.Length; i++)
        {
            if (magics[i] != null && magics[i].slotIndex == slotIndex)
                return PlayerState.CreateMagicFromData(magics[i].magicId, slotIndex);
        }
        return null;
    }

    private TMP_Text CreateCountText(RectTransform parent)
    {
        GameObject textObject = new GameObject("Count", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = materialCountTextAnchorMin;
        rect.anchorMax = materialCountTextAnchorMax;
        rect.pivot = materialCountTextPivot;
        rect.anchoredPosition = materialCountTextAnchoredPosition;
        rect.sizeDelta = materialCountTextSize;

        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.font = materialCountTextFont != null ? materialCountTextFont : UIManager.GetDefaultTMPFont();
        text.fontSize = materialCountTextFontSize;
        text.fontStyle = materialCountTextFontStyle;
        text.alignment = materialCountTextAlignment;
        text.color = materialCountTextColor;
        text.raycastTarget = false;
        return text;
    }
}
