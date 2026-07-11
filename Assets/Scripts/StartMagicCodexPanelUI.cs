using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StartMagicCodexPanelUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Button closeButton;
    [SerializeField] private RectTransform itemRoot;
    [SerializeField] private MagicItemView magicViewPrefab;
    [SerializeField] private TMP_Text countText;
    [SerializeField] private TMP_Text detailNameText;
    [SerializeField] private TMP_Text detailRarityText;
    [SerializeField] private TMP_Text detailRecipeText;
    [SerializeField] private TMP_Text detailEffectText;
    [SerializeField] private Image detailIconImage;
    [SerializeField] private Image detailAccentImage;
    [SerializeField] private SpringLineHighlightUI detailFrameGraphic;
    [SerializeField] private RectTransform detailRecipeIconRoot;
    [SerializeField] private ScrollRect detailBodyScrollRect;
    [SerializeField] private RectTransform detailBodyContent;
    [SerializeField] private CanvasGroup panelCanvasGroup;
    [SerializeField] private RectTransform panelRectTransform;

    [Header("List")]
    [SerializeField, Range(0f, 1f)] private float unlockedAlpha = 1f;
    [SerializeField, Range(0f, 1f)] private float lockedAlpha = 0.32f;
    [SerializeField] private Vector2 itemPreferredSize = new Vector2(240f, 64f);
    [SerializeField] private Vector2 itemGridSpacing = new Vector2(18f, 16f);
    [SerializeField] private RectOffset itemGridPadding;

    [Header("Recipe Icons")]
    [SerializeField] private Vector2 detailRecipeIconSize = new Vector2(36f, 36f);
    [SerializeField] private Vector2 detailRecipeIconSpacing = new Vector2(34f, 34f);
    [SerializeField] private Vector2 detailRecipeIconPadding = Vector2.zero;

    [Header("New Label")]
    [SerializeField] private string newLabelLocalizationKey = "ui.magic_codex.new_label";
    [SerializeField] private float newLabelFontSize = 17f;
    [SerializeField] private Color newLabelColor = new Color(1f, 0.92f, 0.18f, 1f);
    [SerializeField] private Vector2 newLabelAnchoredPosition = new Vector2(-4f, -2f);
    [SerializeField] private Vector2 newLabelSize = new Vector2(96f, 24f);

    [Header("Animation")]
    [SerializeField, Min(0f)] private float panelShowDuration = 0.18f;
    [SerializeField, Min(0f)] private float panelHideDuration = 0.12f;
    [SerializeField] private Vector2 panelSlideOffset = new Vector2(0f, -24f);
    [SerializeField] private Vector3 panelHiddenScale = new Vector3(0.96f, 0.96f, 1f);
    [SerializeField] private Ease panelShowEase = Ease.OutBack;
    [SerializeField] private Ease panelHideEase = Ease.InCubic;

    private readonly List<StartMagicCodexItemUI> itemViews = new List<StartMagicCodexItemUI>();
    private readonly List<MagicData> magicItems = new List<MagicData>();
    private readonly List<Image> detailRecipeIcons = new List<Image>();
    private static readonly Dictionary<string, Sprite> magicIconCache = new Dictionary<string, Sprite>();
    private static readonly Dictionary<MaterialEnum, Sprite> recipeIconCache = new Dictionary<MaterialEnum, Sprite>();
    private bool closeButtonBound;
    private bool languageChangedBound;
    private bool itemsBuilt;
    private bool itemsNeedRebind;
    private string itemsLanguage;
    private MagicData currentMagicData;
    private bool currentMagicUnlocked;
    private Tween panelTween;
    private Vector2 shownAnchoredPosition;
    private bool shownPositionCached;

    public bool IsShowing => gameObject.activeSelf;

    private void Awake()
    {
        CacheReferences();
        BindCloseButton();
        BindLanguageChanged();
    }

    private void OnDestroy()
    {
        panelTween?.Kill(false);
        UnbindLanguageChanged();
        if (closeButton != null && closeButtonBound)
            closeButton.onClick.RemoveListener(Hide);
    }

    public void Show()
    {
        CacheReferences();
        BindCloseButton();
        BindLanguageChanged();
        if (itemsBuilt && itemsLanguage != LocalizationSystem.CurrentLanguage)
            itemsNeedRebind = true;
        CacheShownPosition();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        Refresh();
        PlayShowAnimation();
    }

    public void Hide()
    {
        if (!gameObject.activeSelf)
            return;

        PlayHideAnimation();
    }

    public void Prewarm()
    {
        CacheReferences();
        BindCloseButton();
        EnsureItemsBuilt();
        if (itemsNeedRebind)
            RebindItems();
        RefreshItemStates();
        SetDefaultDetail();
    }

    public bool Contains(Transform hit)
    {
        return hit != null && hit.IsChildOf(transform);
    }

    public void RefreshIfShowing()
    {
        if (gameObject.activeSelf)
            Refresh();
    }

    public void ShowMagic(MagicData data, bool unlocked)
    {
        currentMagicData = data;
        currentMagicUnlocked = unlocked;
        RefreshDetail();
    }

    private void Refresh()
    {
        EnsureItemsBuilt();
        if (itemsNeedRebind)
            RebindItems();
        RefreshItemStates();
        SetDefaultDetail();
    }

    private void EnsureItemsBuilt()
    {
        if (itemsBuilt)
        {
            ApplyItemGridLayout();
            return;
        }

        RebuildMagicItemList();
        if (itemRoot == null || magicViewPrefab == null)
        {
            RefreshCountText(0, magicItems.Count);
            return;
        }

        ClearItems();
        for (int i = 0; i < magicItems.Count; i++)
        {
            MagicData data = magicItems[i];
            bool unlocked = MagicCodexProgressSystem.IsMagicDiscovered(data);
            CreateItem(data, unlocked, MagicCodexProgressSystem.IsMagicNew(data));
        }

        itemsBuilt = itemViews.Count == magicItems.Count;
        itemsLanguage = LocalizationSystem.CurrentLanguage;
        ApplyItemGridLayout();
    }

    private void RebuildMagicItemList()
    {
        magicItems.Clear();
        foreach (MagicData data in GameDataDatabase.MagicData.Values)
        {
            if (data != null)
                magicItems.Add(data);
        }
        magicItems.Sort((a, b) => a.numericId.CompareTo(b.numericId));
    }

    private void RebindItems()
    {
        for (int i = 0; i < magicItems.Count && i < itemViews.Count; i++)
        {
            StartMagicCodexItemUI item = itemViews[i];
            if (item == null)
                continue;

            MagicData data = magicItems[i];
            bool unlocked = MagicCodexProgressSystem.IsMagicDiscovered(data);
            item.ConfigureNewLabel(newLabelLocalizationKey, newLabelFontSize, newLabelColor, newLabelAnchoredPosition, newLabelSize);
            item.Bind(data, unlocked, MagicCodexProgressSystem.IsMagicNew(data), this, unlockedAlpha, lockedAlpha);
        }
        itemsNeedRebind = false;
        itemsLanguage = LocalizationSystem.CurrentLanguage;
    }

    private void RefreshItemStates()
    {
        if (!itemsBuilt)
            EnsureItemsBuilt();

        int unlockedCount = 0;
        for (int i = 0; i < magicItems.Count; i++)
        {
            MagicData data = magicItems[i];
            bool unlocked = MagicCodexProgressSystem.IsMagicDiscovered(data);
            if (unlocked)
                unlockedCount++;
            if (i < itemViews.Count && itemViews[i] != null)
            {
                itemViews[i].ConfigureNewLabel(newLabelLocalizationKey, newLabelFontSize, newLabelColor, newLabelAnchoredPosition, newLabelSize);
                itemViews[i].RefreshState(unlocked, MagicCodexProgressSystem.IsMagicNew(data), unlockedAlpha, lockedAlpha);
            }
        }

        ApplyItemGridLayout();
        RefreshCountText(unlockedCount, magicItems.Count);
    }

    private void CreateItem(MagicData data, bool unlocked, bool isNew)
    {
        if (itemRoot == null || magicViewPrefab == null)
            return;

        MagicItemView view = Instantiate(magicViewPrefab, itemRoot);
        view.gameObject.SetActive(true);
        ApplyItemLayout(view);
        StartMagicCodexItemUI item = view.GetComponent<StartMagicCodexItemUI>();
        if (item == null)
            item = view.gameObject.AddComponent<StartMagicCodexItemUI>();
        if (view.GetComponent<CanvasGroup>() == null)
            view.gameObject.AddComponent<CanvasGroup>();
        item.ConfigureNewLabel(newLabelLocalizationKey, newLabelFontSize, newLabelColor, newLabelAnchoredPosition, newLabelSize);
        item.Bind(data, unlocked, isNew, this, unlockedAlpha, lockedAlpha);
        itemViews.Add(item);
    }

    private void ApplyItemLayout(MagicItemView view)
    {
        LayoutElement[] layoutElements = view.GetComponents<LayoutElement>();
        if (layoutElements.Length == 0)
            layoutElements = new[] { view.gameObject.AddComponent<LayoutElement>() };

        for (int i = 0; i < layoutElements.Length; i++)
        {
            layoutElements[i].preferredWidth = itemPreferredSize.x;
            layoutElements[i].preferredHeight = itemPreferredSize.y;
            layoutElements[i].flexibleWidth = 0f;
            layoutElements[i].flexibleHeight = 0f;
        }

        RectTransform rect = view.transform as RectTransform;
        if (rect != null)
            rect.sizeDelta = itemPreferredSize;
    }

    private void ApplyItemGridLayout()
    {
        if (itemRoot == null)
            return;

        GridLayoutGroup grid = itemRoot.GetComponent<GridLayoutGroup>();
        if (grid == null)
            return;

        Canvas.ForceUpdateCanvases();
        grid.cellSize = new Vector2(Mathf.Max(1f, itemPreferredSize.x), Mathf.Max(1f, itemPreferredSize.y));
        grid.spacing = itemGridSpacing;
        grid.padding = GetItemGridPadding();
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = CalculateItemGridColumnCount(grid);
    }

    private RectOffset GetItemGridPadding()
    {
        if (itemGridPadding == null)
            itemGridPadding = new RectOffset(12, 12, 14, 14);
        return itemGridPadding;
    }

    private int CalculateItemGridColumnCount(GridLayoutGroup grid)
    {
        RectTransform viewport = itemRoot.parent as RectTransform;
        float availableWidth = viewport != null ? viewport.rect.width : ((RectTransform)itemRoot).rect.width;
        availableWidth -= grid.padding.left + grid.padding.right;
        float cellWidth = Mathf.Max(1f, grid.cellSize.x);
        float spacing = Mathf.Max(0f, grid.spacing.x);
        return Mathf.Max(1, Mathf.FloorToInt((availableWidth + spacing) / (cellWidth + spacing)));
    }

    private void OnRectTransformDimensionsChange()
    {
        if (gameObject.activeInHierarchy)
            ApplyItemGridLayout();
    }

    private void RefreshDetail()
    {
        if (currentMagicData == null)
        {
            SetDefaultDetail();
            return;
        }

        Color accentColor = MagicRaritySystem.GetBorderColor(currentMagicData, Color.white);
        ApplyAccentColor(accentColor);

        if (detailIconImage != null)
        {
            detailIconImage.sprite = currentMagicUnlocked ? LoadMagicIcon(currentMagicData.iconName) : LoadLockedPlaceholderIcon();
            detailIconImage.enabled = detailIconImage.sprite != null;
            detailIconImage.color = Color.white;
        }

        string name = currentMagicUnlocked ? LocalizationSystem.GetText(currentMagicData.nameKey, currentMagicData.id) : GetLockedPlaceholderText();
        if (detailNameText != null)
            detailNameText.text = name;

        string rarity = GetRarityName(currentMagicData.rarity);
        if (detailRarityText != null)
        {
            detailRarityText.richText = true;
            detailRarityText.text = FormatLabel(LocalizationSystem.GetText("ui.magic_codex.rarity_label", "稀有度"), rarity);
            detailRarityText.color = accentColor;
        }

        if (detailRecipeText != null)
        {
            detailRecipeText.richText = true;
            detailRecipeText.text = FormatLabel(LocalizationSystem.GetText("ui.magic_codex.recipe_label", "素材序列"), string.Empty);
        }
        RebuildDetailRecipeIcons(currentMagicData);

        if (detailEffectText != null)
        {
            detailEffectText.richText = true;
            string effect = currentMagicUnlocked ? LocalizationSystem.GetText(currentMagicData.descriptionKey, string.Empty) : GetLockedPlaceholderText();
            detailEffectText.text = FormatLabel(LocalizationSystem.GetText("ui.magic_codex.effect_label", "具体效果"), InlineIconTextFormatter.Format(effect));
        }
        RefreshBodyScroll();
    }

    private void SetDefaultDetail()
    {
        currentMagicData = null;
        ApplyAccentColor(Color.white);
        ClearDetailRecipeIcons();
        if (detailIconImage != null)
        {
            detailIconImage.sprite = null;
            detailIconImage.enabled = false;
        }
        if (detailNameText != null)
            detailNameText.text = LocalizationSystem.GetText("ui.magic_codex.detail_hint_title", "图鉴");
        if (detailRarityText != null)
        {
            detailRarityText.text = string.Empty;
            detailRarityText.color = Color.white;
        }
        if (detailRecipeText != null)
            detailRecipeText.text = string.Empty;
        if (detailEffectText != null)
            detailEffectText.text = LocalizationSystem.GetText("ui.magic_codex.detail_hint", "悬停下方道具查看详情");
        RefreshBodyScroll();
    }

    private void ApplyAccentColor(Color accentColor)
    {
        if (detailAccentImage != null)
            detailAccentImage.color = accentColor;
        if (detailFrameGraphic != null)
            detailFrameGraphic.color = accentColor;
    }

    private void RefreshCountText(int unlockedCount, int totalCount)
    {
        if (countText == null)
            return;

        string format = LocalizationSystem.GetText("ui.magic_codex.count", "已获取 {0}/{1}");
        countText.text = format.Replace("{0}", unlockedCount.ToString()).Replace("{1}", totalCount.ToString());
    }

    private void RebuildDetailRecipeIcons(MagicData data)
    {
        if (detailRecipeIconRoot == null)
            return;

        int count = data != null && data.recipe != null ? data.recipe.Length : 0;
        for (int i = detailRecipeIconRoot.childCount - 1; i >= count; i--)
            detailRecipeIconRoot.GetChild(i).gameObject.SetActive(false);

        for (int i = detailRecipeIconRoot.childCount; i < count; i++)
        {
            Image icon = new GameObject("RecipeIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            icon.transform.SetParent(detailRecipeIconRoot, false);
            icon.raycastTarget = false;
        }

        detailRecipeIcons.Clear();
        for (int i = 0; i < count; i++)
        {
            Image icon = detailRecipeIconRoot.GetChild(i).GetComponent<Image>();
            if (icon == null)
                icon = detailRecipeIconRoot.GetChild(i).gameObject.AddComponent<Image>();
            icon.gameObject.SetActive(true);
            icon.sprite = GetRecipeIcon(data.recipe[i]);
            icon.preserveAspect = true;
            icon.color = Color.white;
            icon.raycastTarget = false;
            detailRecipeIcons.Add(icon);

            RectTransform rect = (RectTransform)icon.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(detailRecipeIconPadding.x + i * detailRecipeIconSpacing.x, -detailRecipeIconPadding.y);
            rect.sizeDelta = detailRecipeIconSize;
        }
    }

    private void ClearDetailRecipeIcons()
    {
        detailRecipeIcons.Clear();
        if (detailRecipeIconRoot == null)
            return;

        for (int i = 0; i < detailRecipeIconRoot.childCount; i++)
            detailRecipeIconRoot.GetChild(i).gameObject.SetActive(false);
    }

    private static Sprite GetRecipeIcon(MaterialEnum material)
    {
        if (recipeIconCache.TryGetValue(material, out Sprite sprite))
            return sprite;

        string path = GetRecipeIconPath(material);
        sprite = !string.IsNullOrEmpty(path) ? Resources.Load<Sprite>(path) : MaterialCardView.GetMaterialIcon(material);
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

    private string GetRarityName(MagicRarity rarity)
    {
        switch (rarity)
        {
            case MagicRarity.Rare:
                return LocalizationSystem.GetText("ui.magic_codex.rarity.rare", "稀有");
            case MagicRarity.Epic:
                return LocalizationSystem.GetText("ui.magic_codex.rarity.epic", "史诗");
            case MagicRarity.Legendary:
                return LocalizationSystem.GetText("ui.magic_codex.rarity.legendary", "传说");
            default:
                return LocalizationSystem.GetText("ui.magic_codex.rarity.common", "普通");
        }
    }

    private static string FormatLabel(string label, string value)
    {
        if (string.IsNullOrEmpty(label))
            return value ?? string.Empty;
        return "<b>" + label + LocalizationSystem.GetText("ui.magic_codex.label_separator", "：") + "</b>" + (value ?? string.Empty);
    }

    public static string GetLockedPlaceholderText()
    {
        return LocalizationSystem.GetText("ui.magic_codex.locked_unknown", "？？？");
    }

    public static Sprite LoadLockedPlaceholderIcon()
    {
        return Resources.Load<Sprite>("Images/Magics/unknown");
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

    private void CacheReferences()
    {
        if (closeButton == null)
            closeButton = UIManager.FindChildComponent<Button>(transform, "CloseButton");
        if (itemRoot == null)
            itemRoot = UIManager.FindChildRect(transform, "CodexItemContent");
        if (countText == null)
            countText = FindChildTMP("CountText");
        if (detailNameText == null)
            detailNameText = FindChildTMP("DetailNameText");
        if (detailRarityText == null)
            detailRarityText = FindChildTMP("DetailRarityText");
        if (detailRecipeText == null)
            detailRecipeText = FindChildTMP("DetailRecipeText");
        if (detailEffectText == null)
            detailEffectText = FindChildTMP("DetailEffectText");
        if (detailIconImage == null)
            detailIconImage = UIManager.FindChildComponent<Image>(transform, "DetailIcon");
        if (detailAccentImage == null)
            detailAccentImage = UIManager.FindChildComponent<Image>(transform, "DetailAccent");
        if (detailFrameGraphic == null)
            detailFrameGraphic = UIManager.FindChildComponent<SpringLineHighlightUI>(transform, "DetailFrame");
        if (detailRecipeIconRoot == null)
            detailRecipeIconRoot = UIManager.FindChildRect(transform, "DetailRecipeIconRoot");
        if (detailBodyScrollRect == null)
            detailBodyScrollRect = UIManager.FindChildComponent<ScrollRect>(transform, "DetailBodyViewport");
        if (detailBodyContent == null && detailEffectText != null)
            detailBodyContent = detailEffectText.transform as RectTransform;
        if (panelCanvasGroup == null)
            panelCanvasGroup = GetComponent<CanvasGroup>();
        if (panelCanvasGroup == null)
            panelCanvasGroup = gameObject.AddComponent<CanvasGroup>();
        if (panelRectTransform == null)
            panelRectTransform = transform as RectTransform;
    }

    private TMP_Text FindChildTMP(string childName)
    {
        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == childName && children[i].TryGetComponent(out TMP_Text text))
                return text;
        }
        return null;
    }

    private void BindCloseButton()
    {
        if (closeButton == null || closeButtonBound)
            return;

        closeButton.onClick.AddListener(Hide);
        closeButtonBound = true;
    }

    private void BindLanguageChanged()
    {
        if (languageChangedBound)
            return;

        LocalizationSystem.LanguageChanged += HandleLanguageChanged;
        languageChangedBound = true;
    }

    private void UnbindLanguageChanged()
    {
        if (!languageChangedBound)
            return;

        LocalizationSystem.LanguageChanged -= HandleLanguageChanged;
        languageChangedBound = false;
    }

    private void ClearItems()
    {
        itemsBuilt = false;
        for (int i = itemViews.Count - 1; i >= 0; i--)
        {
            if (itemViews[i] != null)
                DestroyItem(itemViews[i].gameObject);
        }
        itemViews.Clear();

        if (itemRoot == null)
            return;
        for (int i = itemRoot.childCount - 1; i >= 0; i--)
            DestroyItem(itemRoot.GetChild(i).gameObject);
    }

    private static void DestroyItem(GameObject item)
    {
        if (item == null)
            return;
        if (Application.isPlaying)
            Destroy(item);
        else
            DestroyImmediate(item);
    }

    private void CacheShownPosition()
    {
        if (shownPositionCached || panelRectTransform == null)
            return;

        shownAnchoredPosition = panelRectTransform.anchoredPosition;
        shownPositionCached = true;
    }

    private void PlayShowAnimation()
    {
        CacheShownPosition();
        panelTween?.Kill(false);
        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 0f;
            panelCanvasGroup.interactable = true;
            panelCanvasGroup.blocksRaycasts = true;
        }
        if (panelRectTransform != null)
        {
            panelRectTransform.anchoredPosition = shownAnchoredPosition + panelSlideOffset;
            panelRectTransform.localScale = panelHiddenScale;
        }

        Sequence sequence = DOTween.Sequence().SetTarget(this);
        if (panelCanvasGroup != null)
            sequence.Join(panelCanvasGroup.DOFade(1f, panelShowDuration));
        if (panelRectTransform != null)
        {
            sequence.Join(panelRectTransform.DOAnchorPos(shownAnchoredPosition, panelShowDuration).SetEase(panelShowEase));
            sequence.Join(panelRectTransform.DOScale(Vector3.one, panelShowDuration).SetEase(panelShowEase));
        }
        panelTween = sequence;
    }

    private void PlayHideAnimation()
    {
        CacheShownPosition();
        panelTween?.Kill(false);
        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.interactable = false;
            panelCanvasGroup.blocksRaycasts = false;
        }

        Sequence sequence = DOTween.Sequence().SetTarget(this);
        if (panelCanvasGroup != null)
            sequence.Join(panelCanvasGroup.DOFade(0f, panelHideDuration));
        if (panelRectTransform != null)
        {
            sequence.Join(panelRectTransform.DOAnchorPos(shownAnchoredPosition + panelSlideOffset, panelHideDuration).SetEase(panelHideEase));
            sequence.Join(panelRectTransform.DOScale(panelHiddenScale, panelHideDuration).SetEase(panelHideEase));
        }
        sequence.OnComplete(() =>
        {
            if (panelRectTransform != null)
            {
                panelRectTransform.anchoredPosition = shownAnchoredPosition;
                panelRectTransform.localScale = Vector3.one;
            }
            gameObject.SetActive(false);
        });
        panelTween = sequence;
    }

    private void RefreshBodyScroll()
    {
        if (detailBodyContent != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(detailBodyContent);
        if (detailBodyScrollRect == null)
            return;

        Canvas.ForceUpdateCanvases();
        detailBodyScrollRect.verticalNormalizedPosition = 1f;
    }

    private void HandleLanguageChanged()
    {
        itemsNeedRebind = true;
        if (gameObject.activeSelf)
            Refresh();
    }
}
