using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class ShopItemView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private TMP_Text stateText;
    [SerializeField] private RectTransform visualRoot;
    [SerializeField] private Button button;
    [SerializeField] private Image backgroundImage;

    private ShopPanelUI owner;
    private Action<ShopOffer> clicked;
    private ShopOffer offer;
    private MagicItemView magicView;
    private MaterialCardView materialView;
    private TMP_Text removeText;
    private JuicyMotion motion;
    private bool pointerInside;

    public RectTransform MagicVisualRect => magicView != null ? magicView.transform as RectTransform : null;
    public RectTransform MaterialVisualRect => materialView != null ? materialView.transform as RectTransform : null;

    public void Bind(ShopPanelUI panel, ShopOffer offer, bool canAfford, bool canUse, bool selected, Action<ShopOffer> clicked)
    {
        owner = panel;
        this.offer = offer;
        this.clicked = clicked;
        CacheReferences();
        ResetMotionState();
        ClearVisual();

        if (titleText != null)
            titleText.text = GetTitle(offer);
        if (priceText != null)
            priceText.text = offer.price + "$";
        if (stateText != null)
            stateText.text = GetStateText(offer, canAfford, canUse, selected);
        if (backgroundImage != null)
            backgroundImage.color = selected ? new Color(0.16f, 0.12f, 0.2f, 1f) : offer.purchased ? new Color(0.035f, 0.035f, 0.045f, 1f) : new Color(0.08f, 0.08f, 0.12f, 1f);
        ApplyBaseScale(selected);

        CreateVisual(panel, offer);
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClicked);
            button.interactable = !offer.purchased && canUse;
        }
    }

    private void CacheReferences()
    {
        if (button == null)
            button = GetComponent<Button>();
        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();
        if (titleText == null)
            titleText = UIManager.FindChildComponent<TMP_Text>(transform, "Title");
        if (priceText == null)
            priceText = UIManager.FindChildComponent<TMP_Text>(transform, "Price");
        if (stateText == null)
            stateText = UIManager.FindChildComponent<TMP_Text>(transform, "State");
        if (visualRoot == null)
            visualRoot = UIManager.FindChildRect(transform, "VisualRoot");
        if (motion == null)
            motion = GetComponent<JuicyMotion>();
    }

    private void OnDisable()
    {
        pointerInside = false;
        ResetMotionState();
        if (offer != null && offer.kind == ShopItemKind.Material)
            owner?.HideMaterialTooltip(this);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerInside = true;
        if (offer != null && offer.kind == ShopItemKind.Material)
            owner?.ShowMaterialTooltip(this, offer);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        if (offer != null && offer.kind == ShopItemKind.Material)
            owner?.HideMaterialTooltip(this);
    }

    private void ApplyBaseScale(bool selected)
    {
        Vector3 baseScale = selected ? Vector3.one * 1.1f : Vector3.one;
        if (motion != null)
        {
            motion.SetBaseScale(baseScale, !pointerInside);
            if (!pointerInside)
                transform.localEulerAngles = Vector3.zero;
            return;
        }

        if (!pointerInside)
        {
            transform.localScale = baseScale;
            transform.localEulerAngles = Vector3.zero;
        }
    }

    private void ResetMotionState()
    {
        transform.localEulerAngles = Vector3.zero;
        if (!pointerInside)
            transform.localScale = Vector3.one;

        motion?.CaptureCurrentTransformAsBase(true);
    }

    private void ClearVisual()
    {
        if (visualRoot == null)
            return;

        for (int i = 0; i < visualRoot.childCount; i++)
        {
            Transform child = visualRoot.GetChild(i);
            MagicItemView childMagicView = child.GetComponent<MagicItemView>();
            if (childMagicView != null)
                magicView = childMagicView;
            MaterialCardView childMaterialView = child.GetComponent<MaterialCardView>();
            if (childMaterialView != null)
                materialView = childMaterialView;
            TMP_Text childText = child.GetComponent<TMP_Text>();
            if (childText != null && child.name == "RemoveText")
                removeText = childText;
            child.gameObject.SetActive(false);
        }
    }

    private void CreateVisual(ShopPanelUI panel, ShopOffer offer)
    {
        if (visualRoot == null || offer == null)
            return;

        switch (offer.kind)
        {
            case ShopItemKind.Magic:
                if (panel.MagicViewPrefab != null && offer.magicData != null)
                {
                    RectTransform rect = magicView != null ? magicView.transform as RectTransform : null;
                    if (rect == null)
                    {
                        rect = Instantiate(panel.MagicViewPrefab, visualRoot);
                        magicView = rect.GetComponent<MagicItemView>();
                    }
                    rect.SetParent(visualRoot, false);
                    rect.gameObject.SetActive(true);
                    rect.anchorMin = new Vector2(0.5f, 0.5f);
                    rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.anchoredPosition = Vector2.zero;
                    rect.sizeDelta = new Vector2(196f, 92f);
                    rect.localScale = Vector3.one * 0.8f;
                    magicView?.Bind(MagicFactory.Create(offer.magicData));
                }
                break;
            case ShopItemKind.Material:
                if (panel.MaterialCardPrefab != null)
                {
                    RectTransform rect = materialView != null ? materialView.transform as RectTransform : null;
                    if (rect == null)
                    {
                        rect = Instantiate(panel.MaterialCardPrefab, visualRoot);
                        materialView = rect.GetComponent<MaterialCardView>();
                    }
                    rect.SetParent(visualRoot, false);
                    rect.gameObject.SetActive(true);
                    rect.anchorMin = new Vector2(0.5f, 0.5f);
                    rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.anchoredPosition = new Vector2(0f, -30f);
                    rect.sizeDelta = new Vector2(82f, 118f);
                    MaterialModel preview = new MaterialModel("shop_preview_" + offer.material, offer.material);
                    MaterialModifierModel modifier = MaterialModifierFactory.Create(offer.materialModifierData);
                    if (modifier != null)
                        preview.AddModifier(modifier);
                    materialView?.Bind(preview);
                    JuicyMotion motion = rect.GetComponent<JuicyMotion>();
                    if (motion != null)
                        motion.enabled = false;
                    DisableChildRaycasts(rect);
                }
                break;
            case ShopItemKind.RemoveMaterial:
                if (removeText == null)
                {
                    removeText = new GameObject("RemoveText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
                    removeText.font = UIManager.GetDefaultTMPFont();
                    removeText.fontSize = 26;
                    removeText.fontStyle = FontStyles.Bold;
                    removeText.alignment = TextAlignmentOptions.Center;
                    removeText.color = new Color(1f, 0.62f, 0.46f, 1f);
                    removeText.raycastTarget = false;
                }
                removeText.transform.SetParent(visualRoot, false);
                removeText.gameObject.SetActive(true);
                removeText.text = LocalizationSystem.GetText("ui.shop.item.remove_material", string.Empty);
                RectTransform textRect = removeText.rectTransform;
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;
                break;
        }
    }

    private static void DisableChildRaycasts(RectTransform root)
    {
        Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
            graphics[i].raycastTarget = false;
    }

    private void OnClicked()
    {
        clicked?.Invoke(offer);
    }

    private static string GetTitle(ShopOffer offer)
    {
        if (offer == null)
            return string.Empty;

        switch (offer.kind)
        {
            case ShopItemKind.Magic:
                return offer.magicData != null ? LocalizationSystem.GetText(offer.magicData.nameKey, offer.magicData.id) : LocalizationSystem.GetText("ui.shop.item.fallback_magic", string.Empty);
            case ShopItemKind.Material:
                return GetMaterialArrowTitle(offer.material);
            case ShopItemKind.RemoveMaterial:
                return LocalizationSystem.GetText("ui.shop.item.remove_material", string.Empty);
            default:
                return string.Empty;
        }
    }

    private static string GetMaterialArrowTitle(MaterialEnum material)
    {
        string materialName = LocalizationKeys.GetMaterialName(material);
        if (material == MaterialEnum.Fire || material == MaterialEnum.Water || material == MaterialEnum.Wind || material == MaterialEnum.Earth)
            return string.Format(LocalizationSystem.GetText("ui.shop.item.arrow_title", "{0}"), materialName);
        return materialName;
    }


    private static string GetStateText(ShopOffer offer, bool canAfford, bool canUse, bool selected)
    {
        if (offer != null && offer.purchased)
            return LocalizationSystem.GetText("ui.shop.state.purchased", string.Empty);
        if (selected)
        {
            if (offer != null && offer.kind == ShopItemKind.Magic)
                return LocalizationSystem.GetText("ui.shop.state.selected", string.Empty);
            if (offer != null && offer.kind == ShopItemKind.RemoveMaterial)
                return LocalizationSystem.GetText("ui.shop.state.select_remove_material", string.Empty);
        }
        if (!canUse)
            return LocalizationSystem.GetText("ui.shop.state.unavailable", string.Empty);
        return canAfford ? LocalizationSystem.GetText("ui.shop.state.buy", string.Empty) : LocalizationSystem.GetText("ui.shop.state.not_enough_gold", string.Empty);
    }
}
