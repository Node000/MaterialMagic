using System.Collections.Generic;
using System.Text;
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
    [SerializeField] private RectTransform tooltipRoot;
    [SerializeField] private TMP_Text tooltipNameText;
    [SerializeField] private TMP_Text tooltipDescriptionText;
    [SerializeField] private TMP_Text tooltipEffectText;
    [SerializeField] private Vector2 tooltipSize = new Vector2(230f, 108f);
    [SerializeField] private float tooltipDescriptionVerticalPadding = 54f;
    [SerializeField] private RectTransform modifierTooltipRoot;
    [SerializeField] private TMP_Text modifierTooltipText;
    [SerializeField] private Vector2 modifierTooltipSize = new Vector2(230f, 76f);
    [SerializeField] private float modifierTooltipVerticalPadding = 22f;
    [SerializeField] private float modifierTooltipGap = 8f;
    [SerializeField] private Image modifierMarkerImage;
    [Header("背景颜色")]
    [SerializeField] private Color UpBackgroundColor = new Color(0.9f, 0.22f, 0.12f, 1f);
    [SerializeField] private Color LeftBackgroundColor = new Color(0.25f, 0.85f, 0.45f, 1f);
    [SerializeField] private Color DownBackgroundColor = new Color(0.2f, 0.5f, 1f, 1f);
    [SerializeField] private Color RightBackgroundColor = new Color(0.62f, 0.42f, 0.2f, 1f);
    [Header("施法序列")]
    [SerializeField] private Vector2 recipeIconSize = new Vector2(36f, 36f);
    [SerializeField] private Vector2 recipeIconSpacing = new Vector2(22f, 22f);
    [SerializeField] private Vector2 recipeIconPadding = Vector2.zero;
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
    private readonly Color tooltipTitleColor = new Color(1f, 0.92f, 0.62f, 1f);
    private MagicModel magic;
    private CanvasGroup tooltipCanvasGroup;
    private CanvasGroup tagTooltipCanvasGroup;
    private CanvasGroup modifierTooltipCanvasGroup;
    private Tween tooltipTween;
    private Tween tagTooltipTween;
    private Tween modifierTooltipTween;
    private Tween pulseTween;
    private Tween modifierMarkerTween;
    private bool tooltipInitialized;
    private float tooltipBottomAnchoredY;
    private bool warnedMissingBackgroundImage;
    private bool tooltipPinned;
    private static MagicItemView pinnedTooltipView;
    private static readonly List<RaycastResult> pointerRaycastResults = new List<RaycastResult>(8);
    private static PointerEventData pointerEventData;
    private static EventSystem pointerEventSystem;

    public MagicModel Magic => magic;

    private void Awake()
    {
        EnsureTooltipInitialized();
    }

    private void EnsureTooltipInitialized()
    {
        if (tooltipInitialized)
            return;

        tooltipInitialized = true;
        if (tooltipRoot != null)
        {
            tooltipCanvasGroup = tooltipRoot.GetComponent<CanvasGroup>();
            if (tooltipCanvasGroup == null)
                tooltipCanvasGroup = tooltipRoot.gameObject.AddComponent<CanvasGroup>();

            tooltipRoot.pivot = new Vector2(0.5f, 0.5f);
            tooltipRoot.anchoredPosition += new Vector2(0f, tooltipRoot.sizeDelta.y * 0.5f);
            tooltipBottomAnchoredY = tooltipRoot.anchoredPosition.y - tooltipRoot.sizeDelta.y * tooltipRoot.pivot.y;
            tooltipRoot.localScale = tooltipHiddenScale;
            tooltipRoot.gameObject.SetActive(false);
            tooltipCanvasGroup.alpha = 0f;
        }

        EnsureTagTooltip();
        EnsureModifierTooltip();
    }

    private void OnDisable()
    {
        UnpinTooltip(false);
        HideTooltip(true);
        HideModifierTooltipImmediate();
        pulseTween?.Kill(false);
        modifierMarkerTween?.Kill(false);
    }

    private void OnDestroy()
    {
        tooltipTween?.Kill(false);
        tagTooltipTween?.Kill(false);
        modifierTooltipTween?.Kill(false);
        pulseTween?.Kill(false);
        modifierMarkerTween?.Kill(false);
    }

    public void Bind(MagicModel magic)
    {
        if (magic == null)
        {
            this.magic = null;
            CacheMissingReferences();

            if (iconImage != null)
                iconImage.gameObject.SetActive(false);

            if (backgroundImage != null)
                backgroundImage.color = emptyBackgroundColor;

            if (magicNameText != null)
                magicNameText.text = "空槽";

            if (tooltipNameText != null)
                tooltipNameText.text = "空道具槽";

            if (tooltipDescriptionText != null)
                tooltipDescriptionText.text = "选择奖励道具后，可以填入或覆盖此位置。";

            if (tagTooltipText != null)
                tagTooltipText.text = string.Empty;

            if (modifierTooltipText != null)
                modifierTooltipText.text = string.Empty;

            SetModifierMarkerVisible(false);
            RebuildRecipe();
            return;
        }

        this.magic = magic;
        CacheMissingReferences();

        if (iconImage != null)
        {
            iconImage.gameObject.SetActive(true);
            iconImage.sprite = LoadMagicIcon(magic.Data.iconName);
            iconImage.color = iconImage.sprite != null ? Color.white : GetMagicElementColor(magic.Data.element);
        }

        if (backgroundImage != null)
            backgroundImage.color = GetMagicBackgroundColor(magic.Data.element);

        if (magicNameText != null)
            magicNameText.text = magic.Name;

        if (tooltipNameText != null)
            tooltipNameText.text = magic.Name;

        if (tooltipDescriptionText != null)
            tooltipDescriptionText.text = BuildDescriptionText(magic);

        if (modifierTooltipText != null)
            modifierTooltipText.text = BuildModifierTooltipText(magic);

        if (tagTooltipText != null)
            tagTooltipText.text = BuildTagTooltipText(magic.Data.tagIds);

        SetModifierMarkerVisible(magic.HasModifier);
        RebuildRecipe();
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

    public void PulseCast()
    {
        pulseTween?.Kill(false);
        transform.localScale = Vector3.one;
        pulseTween = transform.DOPunchScale(Vector3.one * castPulseScale, castPulseDuration, castPulseVibrato, castPulseElasticity).SetTarget(this);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        ShowTooltip();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!tooltipPinned)
            HideTooltip(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
            return;

        PinTooltip();
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

    private void Update()
    {
        if (!tooltipPinned || !IsPointerDownThisFrame(out Vector2 screenPosition))
            return;

        if (!IsPointerOverThisMagicView(screenPosition))
            UnpinTooltip(true);
    }

    private void PinTooltip()
    {
        if (pinnedTooltipView != null && pinnedTooltipView != this)
            pinnedTooltipView.UnpinTooltip(true);

        pinnedTooltipView = this;
        tooltipPinned = true;
        ShowTooltip();
    }

    private void UnpinTooltip(bool hide)
    {
        if (pinnedTooltipView == this)
            pinnedTooltipView = null;

        tooltipPinned = false;
        if (hide)
            HideTooltip(false);
    }

    private static bool IsPointerDownThisFrame(out Vector2 screenPosition)
    {
        screenPosition = default;
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase != TouchPhase.Began)
                return false;

            screenPosition = touch.position;
            return true;
        }

        if (!Input.GetMouseButtonDown(0))
            return false;

        screenPosition = Input.mousePosition;
        return true;
    }

    private bool IsPointerOverThisMagicView(Vector2 screenPosition)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return false;

        if (pointerEventData == null || pointerEventSystem != eventSystem)
        {
            pointerEventSystem = eventSystem;
            pointerEventData = new PointerEventData(eventSystem);
        }

        pointerEventData.Reset();
        pointerEventData.position = screenPosition;
        pointerRaycastResults.Clear();
        eventSystem.RaycastAll(pointerEventData, pointerRaycastResults);

        for (int i = 0; i < pointerRaycastResults.Count; i++)
        {
            GameObject hitObject = pointerRaycastResults[i].gameObject;
            if (hitObject != null && hitObject.transform.IsChildOf(transform))
                return true;
        }

        return false;
    }

    private void CacheMissingReferences()
    {
        Graphic raycastGraphic = GetComponent<Graphic>();
        if (raycastGraphic != null)
            raycastGraphic.raycastTarget = true;

        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();

        if (tagTooltipText != null)
            tagTooltipText.richText = true;

        if (modifierTooltipText != null)
            modifierTooltipText.richText = true;

        if (tooltipRoot != null)
        {
            Transform effectText = tooltipRoot.Find("EffectText");
            if (effectText != null)
                effectText.gameObject.SetActive(false);
        }

        EnsureTooltipInitialized();
        EnsureModifierMarker();

        if (backgroundImage == null && !warnedMissingBackgroundImage)
        {
            warnedMissingBackgroundImage = true;
            GameLog.Data($"MagicItemView missing background image on {name}");
        }
    }

    private string BuildDescriptionText(MagicModel magic)
    {
        if (magic == null)
            return string.Empty;

        return magic.Description;
    }

    private string BuildModifierTooltipText(MagicModel magic)
    {
        if (magic == null || !magic.HasModifier || magic.PrimaryModifier == null)
            return string.Empty;

        MagicModifierModel modifier = magic.PrimaryModifier;
        string name = modifier.Name;
        string description = modifier.Description;
        if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(description))
            return string.Empty;

        if (string.IsNullOrEmpty(description))
            return "<color=#FFE99E>强化效果：</color>" + name;

        return "<color=#FFE99E>强化效果：" + name + "</color>\n" + description;
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

        modifierMarkerImage.color = new Color(1f, 0.88f, 0.38f, 1f);
        Shader shader = Shader.Find("UI/MagicModifierBreath");
        if (shader != null && modifierMarkerImage.material == null)
            modifierMarkerImage.material = new Material(shader);
    }

    private void SetModifierMarkerVisible(bool visible)
    {
        EnsureModifierMarker();
        if (modifierMarkerImage == null)
            return;

        modifierMarkerTween?.Kill(false);
        modifierMarkerImage.gameObject.SetActive(visible);
        if (!visible)
            return;

        Color baseColor = modifierMarkerImage.color;
        modifierMarkerImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, 1f);
        modifierMarkerTween = modifierMarkerImage.DOFade(1f, 0.86f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetTarget(this);
    }

    private void RebuildRecipe()
    {
        if (recipeRoot == null)
            return;

        recipeBlocks.Clear();
        for (int i = recipeRoot.childCount - 1; i >= 0; i--)
            Destroy(recipeRoot.GetChild(i).gameObject);

        if (magic == null || magic.Data.recipe == null)
            return;

        for (int i = 0; i < magic.Data.recipe.Length; i++)
        {
            Image block = new GameObject("MaterialBlock", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            block.transform.SetParent(recipeRoot, false);
            Sprite materialSprite = GetRecipeIcon(magic.Data.recipe[i]);
            block.sprite = materialSprite;
            block.preserveAspect = true;
            block.color = GetRecipeIconColor(magic.Data.recipe[i]);
            SetBlockOpaque(block);
            recipeBlocks.Add(block);

            RectTransform blockRect = (RectTransform)block.transform;
            blockRect.anchorMin = new Vector2(0f, 1f);
            blockRect.anchorMax = new Vector2(0f, 1f);
            blockRect.pivot = new Vector2(0f, 1f);
            blockRect.anchoredPosition = new Vector2(recipeIconPadding.x + (i % 4) * recipeIconSpacing.x, -recipeIconPadding.y - (i / 4) * recipeIconSpacing.y);
            blockRect.sizeDelta = recipeIconSize;
        }
    }

    private void ShowTooltip()
    {
        EnsureTooltipInitialized();
        if (tooltipRoot == null || tooltipCanvasGroup == null)
            return;

        EnsureTagTooltip();
        tooltipTween?.Kill(false);
        tagTooltipTween?.Kill(false);
        tooltipRoot.gameObject.SetActive(true);
        PopupLayerUtility.ApplyTo(tooltipRoot);
        UpdateTooltipSize();
        tooltipRoot.localScale = tooltipHiddenScale;

        Sequence sequence = DOTween.Sequence().SetTarget(this);
        sequence.Join(tooltipCanvasGroup.DOFade(1f, tooltipFadeDuration));
        sequence.Join(tooltipRoot.DOScale(Vector3.one, tooltipScaleDuration).SetEase(tooltipEase));
        tooltipTween = sequence;

        ShowModifierTooltip();
        ShowTagTooltip();
    }

    private void UpdateTooltipSize()
    {
        if (tooltipRoot == null)
            return;

        Vector2 size = GetTooltipSize();
        tooltipRoot.sizeDelta = size;
        tooltipRoot.anchoredPosition = new Vector2(tooltipRoot.anchoredPosition.x, tooltipBottomAnchoredY + size.y * tooltipRoot.pivot.y);
    }

    private Vector2 GetTooltipSize()
    {
        float width = tooltipSize.x > 0f ? tooltipSize.x : tooltipRoot.sizeDelta.x;
        float minHeight = tooltipSize.y > 0f ? tooltipSize.y : tooltipRoot.sizeDelta.y;
        if (tooltipDescriptionText == null || string.IsNullOrEmpty(tooltipDescriptionText.text))
            return new Vector2(width, minHeight);

        tooltipRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        RectTransform descriptionRect = tooltipDescriptionText.rectTransform;
        descriptionRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(0f, width + descriptionRect.sizeDelta.x));
        Canvas.ForceUpdateCanvases();

        float height = Mathf.Max(minHeight, tooltipDescriptionText.preferredHeight + tooltipDescriptionVerticalPadding);
        return new Vector2(width, height);
    }

    private void ShowModifierTooltip()
    {
        if (modifierTooltipRoot == null || modifierTooltipCanvasGroup == null || modifierTooltipText == null || string.IsNullOrEmpty(modifierTooltipText.text))
            return;

        modifierTooltipText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, modifierTooltipSize.x - 24f);
        Canvas.ForceUpdateCanvases();
        modifierTooltipRoot.sizeDelta = GetModifierTooltipSize();
        modifierTooltipRoot.gameObject.SetActive(true);
        PopupLayerUtility.ApplyTo(modifierTooltipRoot);
        modifierTooltipRoot.SetAsLastSibling();
        modifierTooltipRoot.anchoredPosition = GetModifierTooltipShownPosition();
        modifierTooltipRoot.localScale = tooltipHiddenScale;
        modifierTooltipCanvasGroup.alpha = 0f;
        modifierTooltipTween?.Kill(false);

        Sequence sequence = DOTween.Sequence().SetTarget(this);
        sequence.Join(modifierTooltipCanvasGroup.DOFade(1f, tooltipFadeDuration));
        sequence.Join(modifierTooltipRoot.DOScale(Vector3.one, tooltipScaleDuration).SetEase(tooltipEase));
        modifierTooltipTween = sequence;
    }

    private Vector2 GetModifierTooltipShownPosition()
    {
        if (tooltipRoot == null)
            return Vector2.zero;

        float tooltipTop = tooltipRoot.anchoredPosition.y + tooltipRoot.sizeDelta.y * (1f - tooltipRoot.pivot.y);
        return new Vector2(tooltipRoot.anchoredPosition.x, tooltipTop + modifierTooltipGap);
    }

    private Vector2 GetModifierTooltipSize()
    {
        if (modifierTooltipText == null || string.IsNullOrEmpty(modifierTooltipText.text))
            return modifierTooltipSize;

        float height = Mathf.Max(modifierTooltipSize.y, modifierTooltipText.preferredHeight + modifierTooltipVerticalPadding);
        return new Vector2(modifierTooltipSize.x, height);
    }

    private void HideModifierTooltipImmediate()
    {
        modifierTooltipTween?.Kill(false);
        if (modifierTooltipRoot == null || modifierTooltipCanvasGroup == null)
            return;

        modifierTooltipCanvasGroup.alpha = 0f;
        modifierTooltipRoot.localScale = tooltipHiddenScale;
        modifierTooltipRoot.gameObject.SetActive(false);
    }

    private void ShowTagTooltip()
    {
        if (tagTooltipRoot == null || tagTooltipCanvasGroup == null || tagTooltipText == null || string.IsNullOrEmpty(tagTooltipText.text))
            return;

        tagTooltipText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, tagTooltipSize.x - 24f);
        tagTooltipText.text = tagTooltipText.text;
        Canvas.ForceUpdateCanvases();
        tagTooltipRoot.sizeDelta = GetTagTooltipSize();
        Vector2 shownPosition = GetTagTooltipShownPosition();
        Vector2 slideOffset = new Vector2(tagTooltipSlideDistance * GetTagTooltipSlideDirection(), 0f);
        tagTooltipRoot.gameObject.SetActive(true);
        PopupLayerUtility.ApplyTo(tagTooltipRoot);
        tagTooltipRoot.SetAsLastSibling();
        tagTooltipCanvasGroup.alpha = 0f;
        tagTooltipRoot.localScale = Vector3.one;
        tagTooltipRoot.anchoredPosition = shownPosition - slideOffset;

        Sequence sequence = DOTween.Sequence().SetTarget(this);
        sequence.Join(tagTooltipCanvasGroup.DOFade(1f, tooltipFadeDuration));
        sequence.Join(tagTooltipRoot.DOAnchorPos(shownPosition, tooltipScaleDuration).SetEase(tooltipEase));
        tagTooltipTween = sequence;
    }

    private void HideTooltip(bool instant)
    {
        if (tooltipRoot == null || tooltipCanvasGroup == null)
            return;

        tooltipTween?.Kill(false);
        tagTooltipTween?.Kill(false);
        if (instant)
        {
            tooltipCanvasGroup.alpha = 0f;
            tooltipRoot.localScale = tooltipHiddenScale;
            tooltipRoot.gameObject.SetActive(false);
            if (tagTooltipRoot != null && tagTooltipCanvasGroup != null)
            {
                tagTooltipCanvasGroup.alpha = 0f;
                tagTooltipRoot.gameObject.SetActive(false);
            }
            HideModifierTooltipImmediate();
            return;
        }

        Sequence sequence = DOTween.Sequence().SetTarget(this);
        sequence.Join(tooltipCanvasGroup.DOFade(0f, tooltipFadeDuration));
        sequence.Join(tooltipRoot.DOScale(tooltipHiddenScale, tooltipScaleDuration).SetEase(Ease.InBack));
        tooltipTween = sequence.OnComplete(() => tooltipRoot.gameObject.SetActive(false));
        HideTagTooltip();
        HideModifierTooltip();
    }

    private void HideModifierTooltip()
    {
        if (modifierTooltipRoot == null || modifierTooltipCanvasGroup == null || !modifierTooltipRoot.gameObject.activeSelf)
            return;

        modifierTooltipTween?.Kill(false);
        Sequence sequence = DOTween.Sequence().SetTarget(this);
        sequence.Join(modifierTooltipCanvasGroup.DOFade(0f, tooltipFadeDuration));
        sequence.Join(modifierTooltipRoot.DOScale(tooltipHiddenScale, tooltipScaleDuration).SetEase(Ease.InBack));
        modifierTooltipTween = sequence.OnComplete(() => modifierTooltipRoot.gameObject.SetActive(false));
    }

    private void HideTagTooltip()
    {
        if (tagTooltipRoot == null || tagTooltipCanvasGroup == null || !tagTooltipRoot.gameObject.activeSelf)
            return;

        Vector2 hiddenPosition = GetTagTooltipShownPosition() - new Vector2(tagTooltipSlideDistance * GetTagTooltipSlideDirection(), 0f);
        Sequence sequence = DOTween.Sequence().SetTarget(this);
        sequence.Join(tagTooltipCanvasGroup.DOFade(0f, tooltipFadeDuration));
        sequence.Join(tagTooltipRoot.DOAnchorPos(hiddenPosition, tooltipScaleDuration).SetEase(Ease.InBack));
        tagTooltipTween = sequence.OnComplete(() => tagTooltipRoot.gameObject.SetActive(false));
    }

    private void EnsureModifierTooltip()
    {
        if (tooltipRoot == null)
            return;

        if (modifierTooltipRoot == null)
        {
            Transform existing = tooltipRoot.Find("ModifierTooltip");
            if (existing != null)
                modifierTooltipRoot = (RectTransform)existing;
        }

        if (modifierTooltipRoot == null)
        {
            modifierTooltipRoot = new GameObject("ModifierTooltip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup)).GetComponent<RectTransform>();
            modifierTooltipRoot.SetParent(tooltipRoot.parent, false);
            modifierTooltipRoot.anchorMin = tooltipRoot.anchorMin;
            modifierTooltipRoot.anchorMax = tooltipRoot.anchorMax;
            modifierTooltipRoot.pivot = new Vector2(0.5f, 0f);
            modifierTooltipRoot.sizeDelta = modifierTooltipSize;
            Image image = modifierTooltipRoot.GetComponent<Image>();
            image.color = new Color(0.075f, 0.055f, 0.03f, 1f);
            image.raycastTarget = false;
        }

        modifierTooltipCanvasGroup = modifierTooltipRoot.GetComponent<CanvasGroup>();
        if (modifierTooltipCanvasGroup == null)
            modifierTooltipCanvasGroup = modifierTooltipRoot.gameObject.AddComponent<CanvasGroup>();
        PopupLayerUtility.ApplyTo(modifierTooltipRoot);
        modifierTooltipCanvasGroup.alpha = 0f;
        modifierTooltipCanvasGroup.blocksRaycasts = false;
        modifierTooltipRoot.gameObject.SetActive(false);

        if (modifierTooltipText == null)
        {
            Transform textTransform = modifierTooltipRoot.Find("Text");
            if (textTransform != null)
                modifierTooltipText = textTransform.GetComponent<TMP_Text>();
        }

        if (modifierTooltipText == null)
        {
            modifierTooltipText = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
            modifierTooltipText.transform.SetParent(modifierTooltipRoot, false);
            modifierTooltipText.font = tooltipDescriptionText != null && tooltipDescriptionText.font != null ? tooltipDescriptionText.font : UIManager.GetDefaultTMPFont();
            modifierTooltipText.fontSize = 15;
            modifierTooltipText.alignment = TextAlignmentOptions.TopLeft;
            modifierTooltipText.color = new Color(1f, 0.92f, 0.72f, 1f);
            modifierTooltipText.raycastTarget = false;
            modifierTooltipText.richText = true;
            modifierTooltipText.enableWordWrapping = true;
            modifierTooltipText.overflowMode = TextOverflowModes.Overflow;
            RectTransform textRect = (RectTransform)modifierTooltipText.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12f, 10f);
            textRect.offsetMax = new Vector2(-12f, -10f);
        }
    }

    private void EnsureTagTooltip()
    {
        if (tooltipRoot == null)
            return;

        if (tagTooltipRoot == null)
        {
            Transform existing = tooltipRoot.Find("TagTooltip");
            if (existing != null)
                tagTooltipRoot = (RectTransform)existing;
        }

        if (tagTooltipRoot == null)
        {
            tagTooltipRoot = new GameObject("TagTooltip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup)).GetComponent<RectTransform>();
            tagTooltipRoot.SetParent(tooltipRoot.parent, false);
            tagTooltipRoot.anchorMin = tooltipRoot.anchorMin;
            tagTooltipRoot.anchorMax = tooltipRoot.anchorMax;
            tagTooltipRoot.pivot = showTagTooltipOnLeft ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
            tagTooltipRoot.sizeDelta = tagTooltipSize;
            Image image = tagTooltipRoot.GetComponent<Image>();
            image.color = new Color(0.03f, 0.03f, 0.04f, 1f);
            image.raycastTarget = false;
        }

        tagTooltipRoot.pivot = showTagTooltipOnLeft ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
        tagTooltipCanvasGroup = tagTooltipRoot.GetComponent<CanvasGroup>();
        if (tagTooltipCanvasGroup == null)
            tagTooltipCanvasGroup = tagTooltipRoot.gameObject.AddComponent<CanvasGroup>();
        PopupLayerUtility.ApplyTo(tagTooltipRoot);
        tagTooltipCanvasGroup.alpha = 0f;
        tagTooltipCanvasGroup.blocksRaycasts = false;
        tagTooltipRoot.gameObject.SetActive(false);

        if (tagTooltipText == null)
        {
            Transform textTransform = tagTooltipRoot.Find("Text");
            if (textTransform != null)
                tagTooltipText = textTransform.GetComponent<TMP_Text>();
        }

        if (tagTooltipText == null)
        {
            tagTooltipText = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
            tagTooltipText.transform.SetParent(tagTooltipRoot, false);
            tagTooltipText.font = tooltipDescriptionText != null && tooltipDescriptionText.font != null ? tooltipDescriptionText.font : UIManager.GetDefaultTMPFont();
            tagTooltipText.fontSize = 16;
            tagTooltipText.alignment = TextAlignmentOptions.TopLeft;
            tagTooltipText.color = new Color(1f, 0.88f, 0.58f, 1f);
            tagTooltipText.raycastTarget = false;
            tagTooltipText.richText = true;
            tagTooltipText.enableWordWrapping = true;
            tagTooltipText.overflowMode = TextOverflowModes.Overflow;
            RectTransform textRect = (RectTransform)tagTooltipText.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12f, 10f);
            textRect.offsetMax = new Vector2(-12f, -10f);
        }
    }

    private Vector2 GetTagTooltipShownPosition()
    {
        if (tooltipRoot == null)
            return Vector2.zero;

        float direction = GetTagTooltipSlideDirection();
        return tooltipRoot.anchoredPosition + new Vector2(direction * (tooltipRoot.sizeDelta.x * 0.5f + tagTooltipXOffset), tooltipRoot.sizeDelta.y * (1f - tooltipRoot.pivot.y));
    }

    private float GetTagTooltipSlideDirection()
    {
        return showTagTooltipOnLeft ? -1f : 1f;
    }

    private Vector2 GetTagTooltipSize()
    {
        if (tagTooltipText == null || string.IsNullOrEmpty(tagTooltipText.text))
            return new Vector2(tagTooltipSize.x, tagTooltipVerticalPadding + tagTooltipLineHeight);

        float height = tagTooltipText.preferredHeight + tagTooltipVerticalPadding;
        return new Vector2(tagTooltipSize.x, height);
    }

    private static readonly Dictionary<MaterialEnum, Sprite> recipeIconCache = new Dictionary<MaterialEnum, Sprite>();

    private static Sprite GetRecipeIcon(MaterialEnum material)
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

    private Color GetMagicElementColor(MaterialEnum element)
    {
        return element != MaterialEnum.None ? GetMaterialColor(element) : Color.gray;
    }

    public Color GetMaterialColor(MaterialEnum material)
    {
        switch (material)
        {
            case MaterialEnum.Fire:
                return UpBackgroundColor;
            case MaterialEnum.Wind:
                return LeftBackgroundColor;
            case MaterialEnum.Water:
                return DownBackgroundColor;
            case MaterialEnum.Earth:
                return RightBackgroundColor;
            case MaterialEnum.Wild:
                return new Color(0.8f, 0.45f, 1f, 1f);
            default:
                return Color.gray;
        }
    }
    private Color GetMagicBackgroundColor(MaterialEnum element)
    {
        Color color = GetMagicElementColor(element);
        color = Color.Lerp(new Color(0.08f, 0.08f, 0.12f, 1f), color, 0.42f);
        color.a = 1;
        return color;
    }

    private string BuildTagTooltipText(string[] tagIds)
    {
        if (tagIds == null || tagIds.Length == 0)
            return string.Empty;

        StringBuilder builder = null;
        for (int i = 0; i < tagIds.Length; i++)
        {
            string tagId = tagIds[i];
            if (string.IsNullOrEmpty(tagId) || !GameDataDatabase.TryGetTagData(tagId, out TagData tag))
                continue;

            string name = LocalizationKeys.GetTagName(tag);
            string description = LocalizationKeys.GetTagDescription(tag);
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(description))
                continue;

            if (builder == null)
                builder = new StringBuilder();
            else
                builder.Append("\n\n");

            builder.Append("<color=#FFE99E>");
            builder.Append(name);
            builder.Append("：</color>\n");
            builder.Append(description);
        }

        return builder != null ? builder.ToString() : string.Empty;
    }

    private static Sprite LoadMagicIcon(string iconName)
    {
        if (string.IsNullOrEmpty(iconName))
            return null;

        return Resources.Load<Sprite>("Images/Magics/" + iconName);
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
