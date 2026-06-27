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
    [SerializeField] private SpringLineHighlightUI springHighlight;
    [Header("动画参数")]
    [SerializeField] private float consumedPunchScale = 0.035f;
    [SerializeField] private float consumedPunchDuration = 0.32f;
    [SerializeField] private int consumedPunchVibrato = 4;
    [SerializeField] private float consumedPunchElasticity = 0.45f;

    private MaterialModel materialModel;
    private MaterialListPanelUI owner;
    private CanvasGroup canvasGroup;
    private Tween consumedTween;
    private bool inactive;
    private bool selected;
    private bool hovered;
    private bool springHighlightEnabled = true;

    public RectTransform RectTransform => (RectTransform)transform;
    public MaterialModel MaterialModel => materialModel;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        CacheSpringHighlight();
        RefreshRaycastTargets();
        RefreshSpringHighlight();
    }

    private void OnDisable()
    {
        hovered = false;
        RefreshSpringHighlight();
        owner?.GetComponentInParent<UIManager>()?.HideUnifiedDetailPopup(this);
        consumedTween?.Kill(false);
        consumedTween = null;
    }

    private void OnDestroy()
    {
        consumedTween?.Kill(false);
    }

    public void Initialize(Image frameImage, Image iconImage, TMP_Text labelText)
    {
        this.frameImage = frameImage;
        this.iconImage = iconImage;
        this.labelText = labelText;
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
        RefreshVisual();
    }

    public void SetSelectionVisual(bool value, bool instant)
    {
        selected = value;
        RefreshSpringHighlight();
    }

    public void SetSpringHighlightEnabled(bool enabled)
    {
        springHighlightEnabled = enabled;
        RefreshSpringHighlight();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            UIManager uiManager = owner != null ? owner.GetComponentInParent<UIManager>() : GetComponentInParent<UIManager>();
            if (materialModel != null)
                uiManager?.PinUnifiedDetailPopup(this, UnifiedDetailContentBuilder.Build(materialModel));
            owner?.OnMaterialCardClicked(this);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        RefreshSpringHighlight();
        UIManager uiManager = owner != null ? owner.GetComponentInParent<UIManager>() : null;
        if (materialModel != null)
            uiManager?.ShowUnifiedDetailPopup(this, UnifiedDetailContentBuilder.Build(materialModel));
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        RefreshSpringHighlight();
        owner?.GetComponentInParent<UIManager>()?.HideUnifiedDetailPopup(this);
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

        RefreshRaycastTargets();

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

        RefreshSpringHighlight();
    }

    public void RefreshRaycastTargets()
    {
        Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
            graphics[i].raycastTarget = false;

        Graphic raycastGraphic = iconImage != null ? iconImage : frameImage;
        if (raycastGraphic != null)
            raycastGraphic.raycastTarget = true;
        EnsureIconRaycastFilter();

        if (springHighlight != null)
            springHighlight.raycastTarget = false;
    }

    private void EnsureIconRaycastFilter()
    {
        if (iconImage != null && iconImage.GetComponent<SpritePhysicsShapeRaycastFilter>() == null)
            iconImage.gameObject.AddComponent<SpritePhysicsShapeRaycastFilter>();
    }

    private void CacheSpringHighlight()
    {
        if (springHighlight == null)
            springHighlight = GetComponentInChildren<SpringLineHighlightUI>(true);

        if (springHighlight == null)
            springHighlight = CreateSpringHighlight();

        if (springHighlight == null)
            return;

        springHighlight.raycastTarget = false;
    }

    private SpringLineHighlightUI CreateSpringHighlight()
    {
        GameObject highlightObject = new GameObject("SpringLineHighlight", typeof(RectTransform), typeof(CanvasRenderer), typeof(SpringLineHighlightUI));
        RectTransform rect = highlightObject.GetComponent<RectTransform>();
        rect.SetParent(transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.SetAsLastSibling();
        highlightObject.SetActive(false);
        return highlightObject.GetComponent<SpringLineHighlightUI>();
    }

    private void RefreshSpringHighlight()
    {
        CacheSpringHighlight();
        if (springHighlight == null)
            return;

        springHighlight.color = GetSpringHighlightColor();
        springHighlight.gameObject.SetActive(springHighlightEnabled && (selected || hovered));
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

    private static string GetCardLabel(MaterialModel materialModel)
    {
        if (materialModel == null)
            return GetMaterialName(MaterialEnum.None);

        return GetMaterialName(materialModel.material);
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
