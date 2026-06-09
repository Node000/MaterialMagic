using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class MaterialCardView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image frameImage;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private RectTransform enhancementRoot;
    [SerializeField] private SpringLineHighlightUI springHighlight;
    [Header("Modifier标签布局")]
    [SerializeField] private float enhancementLineHeight = 16f;
    [SerializeField] private float enhancementLineSpacing = 2f;
    [Header("动画参数")]
    [SerializeField] private float consumedPunchScale = 0.035f;
    [SerializeField] private float consumedPunchDuration = 0.32f;
    [SerializeField] private int consumedPunchVibrato = 4;
    [SerializeField] private float consumedPunchElasticity = 0.45f;

    private readonly System.Collections.Generic.List<TMP_Text> enhancementTexts = new System.Collections.Generic.List<TMP_Text>();
    private MaterialModel materialModel;
    private MaterialListPanelUI owner;
    private CanvasGroup canvasGroup;
    private Tween consumedTween;
    private bool inactive;
    private bool selected;
    private bool hovered;

    public RectTransform RectTransform => (RectTransform)transform;
    public MaterialModel MaterialModel => materialModel;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        CacheEnhancementTexts();
        CacheSpringHighlight();
        RefreshSpringHighlight();
    }

    private void OnDisable()
    {
        hovered = false;
        RefreshSpringHighlight();
        owner?.HideModifierTooltip(this);
        consumedTween?.Kill(false);
        consumedTween = null;
    }

    private void OnDestroy()
    {
        consumedTween?.Kill(false);
    }

    public void Initialize(Image frameImage, Image iconImage, TMP_Text labelText, RectTransform enhancementRoot)
    {
        this.frameImage = frameImage;
        this.iconImage = iconImage;
        this.labelText = labelText;
        this.enhancementRoot = enhancementRoot;
        CacheSpringHighlight();
    }

    public void Initialize(MaterialListPanelUI owner)
    {
        this.owner = owner;
    }

    public void Bind(MaterialModel materialModel)
    {
        Bind(materialModel, false);
    }

    public void Bind(MaterialModel materialModel, bool consumed)
    {
        this.materialModel = materialModel;
        this.inactive = consumed;
        selected = false;
        hovered = false;
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        CacheEnhancementTexts();
        RefreshVisual();
    }

    public void SetSelectionVisual(bool value, bool instant)
    {
        selected = value;
        RefreshSpringHighlight();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
            owner?.OnMaterialCardClicked(this);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        RefreshSpringHighlight();
        owner?.ShowModifierTooltip(this, materialModel);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        RefreshSpringHighlight();
        owner?.HideModifierTooltip(this);
    }
    public static Color GetMaterialColor(MaterialEnum material)
    {
        switch (material)
        {
            case MaterialEnum.Fire:
                return new Color(0.9f, 0.22f, 0.12f, 1f);
            case MaterialEnum.Wind:
                return new Color(0.25f, 0.85f, 0.45f, 1f);
            case MaterialEnum.Water:
                return new Color(0.2f, 0.5f, 1f, 1f);
            case MaterialEnum.Earth:
                return new Color(0.62f, 0.42f, 0.2f, 1f);
            case MaterialEnum.Wild:
                return new Color(0.8f, 0.45f, 1f, 1f);
            default:
                return Color.gray;
        }
    }
    private void RefreshVisual()
    {
        MaterialEnum material = materialModel != null ? materialModel.GetArrowDisplayMaterial() : MaterialEnum.None;

        if (labelText != null)
            labelText.text = GetCardLabel(materialModel);

        if (iconImage != null)
        {
            Sprite sprite = GetMaterialIcon(material);
            iconImage.sprite = sprite;
            iconImage.color = Color.white;
            iconImage.preserveAspect = true;
            iconImage.enabled = true;
            MaterialModifierVisualUtility.ApplyTo(iconImage, materialModel);
        }

        if (frameImage != null)
        {
            frameImage.raycastTarget = false;
        }

        Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
            graphics[i].raycastTarget = false;
        if (frameImage != null)
            frameImage.raycastTarget = true;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            consumedTween?.Kill(false);
            if (inactive)
            {
                consumedTween = transform.DOPunchScale(Vector3.one * consumedPunchScale, consumedPunchDuration, consumedPunchVibrato, consumedPunchElasticity).SetTarget(this);
            }
        }

        RebuildEnhancements();
        RefreshSpringHighlight();
    }

    private void CacheSpringHighlight()
    {
        if (springHighlight == null)
            springHighlight = GetComponentInChildren<SpringLineHighlightUI>(true);

        if (springHighlight == null)
            return;

        springHighlight.raycastTarget = false;
    }

    private void RefreshSpringHighlight()
    {
        CacheSpringHighlight();
        if (springHighlight == null)
            return;

        springHighlight.color = GetSpringHighlightColor();
        springHighlight.gameObject.SetActive(selected || hovered);
    }

    private Color GetSpringHighlightColor()
    {
        Color color = Color.white;
        if (materialModel == null || materialModel.modifiers == null)
            return color;

        for (int i = 0; i < materialModel.modifiers.Count; i++)
        {
            MaterialModifierModel modifier = materialModel.modifiers[i];
            if (modifier != null && MaterialModifierDisplayDatabase.TryGetLineColor(modifier, out Color modifierColor))
                color = modifierColor;
        }
        return color;
    }

    private void RebuildEnhancements()
    {
        if (enhancementRoot == null)
            return;

        CacheEnhancementTexts();
        EnsureEnhancementRootLayout();

        for (int i = 0; i < enhancementRoot.childCount; i++)
            enhancementRoot.GetChild(i).gameObject.SetActive(false);

        if (materialModel == null || (materialModel.enhancementIds.Count == 0 && materialModel.modifiers.Count == 0))
            return;

        int tagIndex = 0;
        for (int i = 0; i < materialModel.enhancementIds.Count; i++)
        {
            TMP_Text tagText = GetEnhancementText(tagIndex);
            ApplyEnhancementTextLayout(tagText, tagIndex++);
            tagText.gameObject.SetActive(true);
            tagText.text = materialModel.enhancementIds[i];
        }

        for (int i = 0; i < materialModel.modifiers.Count; i++)
        {
            TMP_Text tagText = GetEnhancementText(tagIndex);
            ApplyEnhancementTextLayout(tagText, tagIndex++);
            tagText.gameObject.SetActive(true);
            tagText.text = LocalizationKeys.GetModifierName(materialModel.modifiers[i]);
        }
    }

    private void CacheEnhancementTexts()
    {
        enhancementTexts.Clear();
        if (enhancementRoot == null)
            return;

        for (int i = 0; i < enhancementRoot.childCount; i++)
        {
            TMP_Text text = enhancementRoot.GetChild(i).GetComponent<TMP_Text>();
            if (text != null)
                enhancementTexts.Add(text);
        }
    }

    private void EnsureEnhancementRootLayout()
    {
        if (enhancementRoot == null)
            return;

        HorizontalLayoutGroup horizontalLayout = enhancementRoot.GetComponent<HorizontalLayoutGroup>();
        if (horizontalLayout != null)
            horizontalLayout.enabled = false;
    }

    private void ApplyEnhancementTextLayout(TMP_Text tagText, int index)
    {
        if (tagText == null)
            return;

        tagText.enableWordWrapping = false;
        tagText.overflowMode = TextOverflowModes.Overflow;
        RectTransform rect = tagText.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -index * (enhancementLineHeight + enhancementLineSpacing));
        rect.sizeDelta = new Vector2(0f, enhancementLineHeight);
    }

    private TMP_Text GetEnhancementText(int index)
    {
        while (enhancementTexts.Count <= index)
        {
            TMP_Text tagText = new GameObject("EnhancementTag", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
            tagText.transform.SetParent(enhancementRoot, false);
            tagText.font = labelText != null && labelText.font != null ? labelText.font : UIManager.GetDefaultTMPFont();
            tagText.fontSize = 12;
            tagText.alignment = TextAlignmentOptions.Center;
            tagText.color = Color.white;
            tagText.raycastTarget = false;
            tagText.enableWordWrapping = false;
            tagText.overflowMode = TextOverflowModes.Overflow;
            ApplyEnhancementTextLayout(tagText, enhancementTexts.Count);
            enhancementTexts.Add(tagText);
        }

        return enhancementTexts[index];
    }

    private static string GetCardLabel(MaterialModel materialModel)
    {
        if (materialModel == null)
            return GetMaterialName(MaterialEnum.None);

        string text = GetMaterialName(materialModel.material);
        if (materialModel.isTemporary)
            text += " " + LocalizationKeys.GetModifierName(MaterialModifierDisplayKind.Temporary);
        return text;
    }

    private static readonly System.Collections.Generic.Dictionary<MaterialEnum, Sprite> materialIconCache = new System.Collections.Generic.Dictionary<MaterialEnum, Sprite>();

    public static string GetMaterialName(MaterialEnum material)
    {
        return LocalizationKeys.GetMaterialName(material);
    }

    public static Sprite GetMaterialIcon(MaterialEnum material)
    {
        if (materialIconCache.TryGetValue(material, out Sprite sprite))
            return sprite;

        string path = GetMaterialIconPath(material);
        sprite = !string.IsNullOrEmpty(path) ? Resources.Load<Sprite>(path) : null;
        materialIconCache[material] = sprite;
        return sprite;
    }

    private static string GetMaterialIconPath(MaterialEnum material)
    {
        switch (material)
        {
            case MaterialEnum.Fire:
                return "Images/UI/1";
            case MaterialEnum.Wind:
                return "Images/UI/3";
            case MaterialEnum.Water:
                return "Images/UI/2";
            case MaterialEnum.Earth:
                return "Images/UI/4";
            case MaterialEnum.Wild:
                return "Images/UI/Wild";
            default:
                return null;
        }
    }

}
