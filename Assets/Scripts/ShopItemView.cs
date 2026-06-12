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
        this.offer = offer;
        this.clicked = clicked;
        CacheReferences();
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
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerInside = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
    }

    private void ApplyBaseScale(bool selected)
    {
        Vector3 baseScale = selected ? Vector3.one * 1.1f : Vector3.one;
        if (motion != null)
        {
            motion.SetBaseScale(baseScale, !pointerInside);
            return;
        }

        if (!pointerInside)
            transform.localScale = baseScale;
    }

    private void ClearVisual()
    {
        if (visualRoot == null)
            return;

        for (int i = visualRoot.childCount - 1; i >= 0; i--)
            Destroy(visualRoot.GetChild(i).gameObject);
        magicView = null;
        materialView = null;
        removeText = null;
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
                    RectTransform rect = Instantiate(panel.MagicViewPrefab, visualRoot);
                    rect.gameObject.SetActive(true);
                    rect.anchorMin = new Vector2(0.5f, 0.5f);
                    rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.anchoredPosition = Vector2.zero;
                    rect.sizeDelta = new Vector2(196f, 92f);
                    rect.localScale = Vector3.one * 0.8f;
                    magicView = rect.GetComponent<MagicItemView>();
                    magicView?.Bind(MagicFactory.Create(offer.magicData));
                }
                break;
            case ShopItemKind.Material:
                if (panel.MaterialCardPrefab != null)
                {
                    RectTransform rect = Instantiate(panel.MaterialCardPrefab, visualRoot);
                    rect.gameObject.SetActive(true);
                    rect.anchorMin = new Vector2(0.5f, 0.5f);
                    rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.anchoredPosition = new Vector2(0f, -30f);
                    rect.sizeDelta = new Vector2(82f, 118f);
                    materialView = rect.GetComponent<MaterialCardView>();
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
                removeText = new GameObject("RemoveText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
                removeText.transform.SetParent(visualRoot, false);
                removeText.font = UIManager.GetDefaultTMPFont();
                removeText.fontSize = 26;
                removeText.fontStyle = FontStyles.Bold;
                removeText.alignment = TextAlignmentOptions.Center;
                removeText.color = new Color(1f, 0.62f, 0.46f, 1f);
                removeText.raycastTarget = false;
                removeText.text = "删牌";
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
                return offer.magicData != null ? LocalizationSystem.GetText(offer.magicData.nameKey, offer.magicData.id) : "道具";
            case ShopItemKind.Material:
                return GetMaterialArrowTitle(offer.material);
            case ShopItemKind.RemoveMaterial:
                return "删牌";
            default:
                return string.Empty;
        }
    }

    private static string GetMaterialArrowTitle(MaterialEnum material)
    {
        switch (material)
        {
            case MaterialEnum.Fire:
                return "上箭头";
            case MaterialEnum.Water:
                return "下箭头";
            case MaterialEnum.Wind:
                return "左箭头";
            case MaterialEnum.Earth:
                return "右箭头";
            default:
                return LocalizationKeys.GetMaterialName(material);
        }
    }


    private static string GetStateText(ShopOffer offer, bool canAfford, bool canUse, bool selected)
    {
        if (offer != null && offer.purchased)
            return "已购买";
        if (selected)
        {
            if (offer != null && offer.kind == ShopItemKind.Magic)
                return "已选中";
            if (offer != null && offer.kind == ShopItemKind.RemoveMaterial)
                return "选择要删的牌";
        }
        if (!canUse)
            return "不可用";
        return canAfford ? "点击购买" : "金币不足";
    }
}
