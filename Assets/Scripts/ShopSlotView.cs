using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class ShopSlotView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private RectTransform visualRoot;
    [SerializeField] private Button button;
    [SerializeField] private Image backgroundImage;

    private ShopPanelUI owner;
    private Action<ShopOffer> clicked;
    private ShopOffer offer;
    private MagicItemView magicView;
    private MaterialCardView materialView;
    private JuicyMotion motion;
    private SpringLineHighlightUI hoverStroke;
    private bool pointerInside;

    public RectTransform MagicVisualRect => magicView != null ? magicView.transform as RectTransform : null;
    public RectTransform MaterialVisualRect => materialView != null ? materialView.transform as RectTransform : null;
    private RectTransform TooltipAnchor => MaterialVisualRect != null ? MaterialVisualRect : transform as RectTransform;

    public void Bind(ShopPanelUI panel, ShopOffer offer, bool canAfford, bool canUse, bool selected, Action<ShopOffer> clicked)
    {
        owner = panel;
        this.offer = offer;
        this.clicked = clicked;
        CacheReferences();
        ResetMotionState();
        ClearVisual();

        if (priceText != null)
            priceText.text = offer != null && !offer.purchased ? FormatShopPrice(offer.price) : string.Empty;
        if (backgroundImage != null)
            backgroundImage.color = Color.clear;
        ApplyBaseScale(selected);

        CreateVisual(panel, offer);
        if (priceText != null)
            priceText.transform.SetAsLastSibling();
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClicked);
            button.interactable = offer != null && !offer.purchased && canUse;
        }
    }

    private void CacheReferences()
    {
        if (button == null)
            button = GetComponent<Button>();
        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();
        if (priceText == null)
            priceText = UIManager.FindChildComponent<TMP_Text>(transform, "Price");
        if (visualRoot == null)
            visualRoot = UIManager.FindChildRect(transform, "VisualRoot");
        if (motion == null)
            motion = GetComponent<JuicyMotion>();
    }

    public static string FormatShopPrice(int price)
    {
        return price > 0 ? price + "$" : LocalizationSystem.GetText("ui.shop.free", "免费");
    }

    private void OnDisable()
    {
        pointerInside = false;
        SetHoverOutline(false);
        ResetMotionState();
        if (offer != null && offer.kind == ShopItemKind.Material)
            owner?.HideMaterialTooltip(TooltipAnchor);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        pointerInside = true;
        if (offer != null && offer.kind == ShopItemKind.Material)
            owner?.ShowMaterialTooltip(TooltipAnchor, offer);
        transform.DOScale(Vector3.one * 1.1f, 0.12f).SetEase(Ease.OutBack).SetTarget(this);
        SetHoverOutline(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerInside = false;
        if (offer != null && offer.kind == ShopItemKind.Material)
            owner?.HideMaterialTooltip(TooltipAnchor);
        transform.DOScale(Vector3.one, 0.12f).SetEase(Ease.OutQuad).SetTarget(this);
        SetHoverOutline(false);
    }

    private void EnsureHoverStroke()
    {
        if (hoverStroke != null)
            return;

        GameObject obj = new GameObject("HoverStroke", typeof(RectTransform), typeof(CanvasRenderer), typeof(SpringLineHighlightUI));
        obj.transform.SetParent(transform, false);
        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.SetAsLastSibling();

        hoverStroke = obj.GetComponent<SpringLineHighlightUI>();
        hoverStroke.SetShape(SpringLineHighlightUI.HighlightShape.RoundedRect);
        hoverStroke.SetLineCount(2);
        hoverStroke.SetLineWidth(2f);
        hoverStroke.SetOutset(1.4f);
        hoverStroke.SetFillEnabled(false);
        hoverStroke.SetBindHoverTarget(false);
        hoverStroke.color = Color.white;
        hoverStroke.raycastTarget = false;
        hoverStroke.gameObject.SetActive(false);
    }

    private void SetHoverOutline(bool visible)
    {
        if (!visible)
        {
            // 仅隐藏已存在的描边；不在 OnDisable/Awake（父物体激活/反激活）期间新建子物体。
            if (hoverStroke != null)
                hoverStroke.gameObject.SetActive(false);
            return;
        }

        EnsureHoverStroke();
        if (hoverStroke != null)
            hoverStroke.gameObject.SetActive(true);
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

        magicView = null;
        materialView = null;
        for (int i = 0; i < visualRoot.childCount; i++)
        {
            Transform child = visualRoot.GetChild(i);
            if (child.GetComponent<MagicItemView>() != null)
                magicView = child.GetComponent<MagicItemView>();
            if (child.GetComponent<MaterialCardView>() != null)
                materialView = child.GetComponent<MaterialCardView>();
            child.gameObject.SetActive(false);
        }
    }

    private void CreateVisual(ShopPanelUI panel, ShopOffer offer)
    {
        if (visualRoot == null || offer == null)
            return;

        // 已购商品：清空内容、保留占位（由 Refresh 保持 active），避免布局组重排其它商品。
        if (offer.purchased)
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
                    rect.anchoredPosition = new Vector2(0f, 0f);
                    rect.sizeDelta = new Vector2(82f, 118f);
                    rect.localScale = Vector3.one * 0.85f;
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
}
