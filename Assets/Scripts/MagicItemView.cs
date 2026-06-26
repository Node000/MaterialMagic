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
    private MagicModel magic;
    private Tween pulseTween;
    private Tween modifierMarkerTween;
    private bool warnedMissingBackgroundImage;
    private SpringLineHighlightUI hoverHighlight;

    public MagicModel Magic => magic;

    private void Awake()
    {
        CacheMissingReferences();
    }

    private void OnDisable()
    {
        pulseTween?.Kill(false);
        modifierMarkerTween?.Kill(false);
        UIManager uiManager = GetComponentInParent<UIManager>();
        uiManager?.HideUnifiedDetailPopup(this);
    }

    private void OnDestroy()
    {
        pulseTween?.Kill(false);
        modifierMarkerTween?.Kill(false);
    }

    public void Bind(MagicModel magic)
    {
        if (magic == null)
        {
            this.magic = null;
            CacheMissingReferences();

            SetIconVisible(false);

            if (backgroundImage != null)
                backgroundImage.color = emptyBackgroundColor;

            if (magicNameText != null)
                magicNameText.text = LocalizationSystem.GetText("ui.magic.empty_slot.label", "空槽");

            SetModifierMarkerVisible(false);
            SetHoverHighlightEnabled(false);
            RebuildRecipe();
            return;
        }

        this.magic = magic;
        CacheMissingReferences();

        SetIconVisible(true);
        if (iconImage != null)
        {
            iconImage.sprite = LoadMagicIcon(magic.Data.iconName);
            iconImage.color = iconImage.sprite != null ? Color.white : GetMagicElementColor(magic.Data.element);
        }

        if (backgroundImage != null)
            backgroundImage.color = GetMagicBackgroundColor(magic.Data.element);

        if (magicNameText != null)
            magicNameText.text = magic.Name;

        SetModifierMarkerVisible(magic.HasModifier);
        SetHoverHighlightEnabled(true);
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
        UIManager uiManager = GetComponentInParent<UIManager>();
        if (uiManager != null)
            uiManager.ShowUnifiedDetailPopup(this, magic != null ? UnifiedDetailContentBuilder.Build(magic) : UnifiedDetailContentBuilder.BuildEmptyMagicSlot());
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        UIManager uiManager = GetComponentInParent<UIManager>();
        uiManager?.HideUnifiedDetailPopup(this);
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
