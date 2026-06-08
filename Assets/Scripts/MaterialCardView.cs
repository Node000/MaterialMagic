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
    [Header("动画参数")]
    [SerializeField] private float consumedPunchScale = 0.035f;
    [SerializeField] private float consumedPunchDuration = 0.32f;
    [SerializeField] private int consumedPunchVibrato = 4;
    [SerializeField] private float consumedPunchElasticity = 0.45f;

    private readonly Color emptyEnhancementColor = new Color(0.08f, 0.08f, 0.1f, 1f);
    private readonly System.Collections.Generic.List<TMP_Text> enhancementTexts = new System.Collections.Generic.List<TMP_Text>();
    private MaterialModel materialModel;
    private MaterialListPanelUI owner;
    private CanvasGroup canvasGroup;
    private Tween consumedTween;
    private bool inactive;

    public RectTransform RectTransform => (RectTransform)transform;
    public MaterialModel MaterialModel => materialModel;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        CacheEnhancementTexts();
    }

    private void OnDisable()
    {
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
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        CacheEnhancementTexts();
        RefreshVisual();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
            owner?.OnMaterialCardClicked(this);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        owner?.ShowModifierTooltip(this, materialModel);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
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
        MaterialEnum material = materialModel != null ? materialModel.material : MaterialEnum.None;

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
    }

    private void RebuildEnhancements()
    {
        if (enhancementRoot == null)
            return;

        CacheEnhancementTexts();

        for (int i = 0; i < enhancementRoot.childCount; i++)
            enhancementRoot.GetChild(i).gameObject.SetActive(false);

        if (materialModel == null || (materialModel.enhancementIds.Count == 0 && materialModel.modifiers.Count == 0))
        {
            Image empty = enhancementRoot.childCount > 0 ? enhancementRoot.GetChild(0).GetComponent<Image>() : null;
            if (empty == null)
                empty = CreateEnhancementBlock("EmptyTag");
            empty.gameObject.SetActive(true);
            empty.color = emptyEnhancementColor;
            return;
        }

        int tagIndex = 0;
        for (int i = 0; i < materialModel.enhancementIds.Count; i++)
        {
            TMP_Text tagText = GetEnhancementText(tagIndex++);
            tagText.gameObject.SetActive(true);
            tagText.text = materialModel.enhancementIds[i];
        }

        for (int i = 0; i < materialModel.modifiers.Count; i++)
        {
            TMP_Text tagText = GetEnhancementText(tagIndex++);
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
            RectTransform rect = tagText.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -enhancementTexts.Count * 18f);
            rect.sizeDelta = new Vector2(0f, 16f);
            enhancementTexts.Add(tagText);
        }

        return enhancementTexts[index];
    }

    private Image CreateEnhancementBlock(string name)
    {
        Image image = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(enhancementRoot, false);
        image.raycastTarget = false;
        RectTransform rect = image.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return image;
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
            default:
                return null;
        }
    }

}
