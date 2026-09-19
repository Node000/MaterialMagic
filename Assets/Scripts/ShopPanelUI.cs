using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public enum ShopItemKind
{
    Magic,
    Material,
    RemoveMaterial
}

public class ShopOffer
{
    public ShopItemKind kind;
    public int price;
    public MagicData magicData;
    public MaterialEnum material;
    public MaterialModifierData materialModifierData;
    public bool purchased;

    public ShopOfferSaveData Export()
    {
        return new ShopOfferSaveData
        {
            kind = (int)kind,
            price = price,
            magicNumericId = magicData != null ? magicData.numericId : 0,
            material = (int)material,
            materialModifierId = materialModifierData != null ? materialModifierData.id : string.Empty,
            purchased = purchased
        };
    }
}

public class ShopPanelUI : MonoBehaviour
{
    [SerializeField] private RectTransform itemRoot;
    [SerializeField] private RectTransform magicViewPrefab;
    [SerializeField] private RectTransform materialCardPrefab;
    [SerializeField] private RectTransform shopItemSlotPrefab;
    [SerializeField] private RectTransform shopArrowSlotPrefab;
    [SerializeField] private RectTransform shopLayerSeparatorPrefab;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text hintText;
    [SerializeField] private TMP_Text goldText;
    [SerializeField] private Button leaveButton;
    [SerializeField] private Button refreshButton;
    [SerializeField] private Button removeArrowButton;
    [SerializeField] private RectTransform revealMask;
    [SerializeField] private RectTransform contentRoot;
    [Header("CRT 开关动画")]
    [SerializeField] private Image crtScanLineImage;
    [SerializeField] private float crtCollapseDuration = 0.32f;
    [SerializeField] private Ease crtCollapseEase = Ease.InCubic;
    [SerializeField] private float crtLineHoldDuration = 0.12f;
    [SerializeField] private float crtShrinkDuration = 0.18f;
    [SerializeField] private Ease crtShrinkEase = Ease.InCubic;
    [SerializeField, Range(0.005f, 0.2f)] private float crtLineYRatio = 0.02f;
    [Header("商品槽出现/消失")]
    [SerializeField] private float slotAppearDuration = 0.28f;
    [SerializeField] private float slotDisappearDuration = 0.2f;
    [SerializeField] private float slotStaggerDelay = 0.1f;
    [SerializeField] private Ease slotAppearEase = Ease.OutBack;
    [SerializeField] private Ease slotDisappearEase = Ease.InBack;
    [SerializeField] private AnimationCurve slotScaleCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 1f));
    [SerializeField] private bool slotUseCurve = false;

    private readonly List<ShopSlotView> slotViews = new List<ShopSlotView>();
    private readonly List<ShopOffer> offers = new List<ShopOffer>();
    private readonly List<ShopLayer> shopLayers = new List<ShopLayer>();
    private readonly List<List<ShopOffer>> layerOffers = new List<List<ShopOffer>>();
    private readonly List<GameObject> createdSeparators = new List<GameObject>();
    private readonly List<MagicData> magicPool = new List<MagicData>();
    private readonly List<ShopMaterialOfferData> strongMaterialOfferPool = new List<ShopMaterialOfferData>();
    private readonly List<ShopMaterialOfferData> normalMaterialOfferPool = new List<ShopMaterialOfferData>();
    private readonly List<ShopMaterialOfferData> weakMaterialOfferPool = new List<ShopMaterialOfferData>();
    private HandSystemUI owner;
    private EconomyConfigData config;
    private ShopProductPoolData productPool;
    private ShopOffer selectedOffer;
    private bool waitingForSelection;
    private bool purchaseInProgress;
    private ShopOffer undoOffer;
    private int undoGold;
    private int undoMagicSlotIndex = -1;
    private MagicModel undoPreviousMagic;
    private MaterialModel undoAddedMaterial;
    private MaterialModel undoRemovedMaterial;
    private bool undoAvailable;
    private Vector2 panelOpenPosition;
    private Vector3 panelBaseScale;
    private bool hasPanelLayout;
    private Coroutine showRoutine;
    private int refreshCount;
    private bool refreshInProgress;
    private bool removeArrowUsed;
    private TMP_Text removeArrowCostText;

    public RectTransform MagicViewPrefab => magicViewPrefab;
    public RectTransform MaterialCardPrefab => materialCardPrefab;

    public void Initialize(HandSystemUI owner)
    {
        this.owner = owner;
        CacheReferences();
        gameObject.SetActive(false);
    }

    public void InitShop(List<ShopLayer> layers)
    {
        shopLayers.Clear();
        if (layers != null && layers.Count > 0)
            shopLayers.AddRange(layers);
    }

    public int RemoveArrowPrice => GetOfferPrice(config != null ? config.shopRemoveMaterialPrice : 0);
    public int RefreshCost => GetRefreshCost();

    private int GetRefreshCost()
    {
        int basePrice = config != null ? config.shopRefreshPrice : 0;
        return Mathf.Max(0, basePrice) + refreshCount;
    }

    /// <summary>
    /// 道具栏占用变化后刷新商品的可用状态。商店开着时玩家可以卖出道具腾出空位，
    /// 商品（尤其道具）的可用性需要跟着变，否则刷新后面板再关闭前的状态会过期。
    /// </summary>
    public void RefreshOfferAvailability()
    {
        if (!gameObject.activeInHierarchy)
            return;

        Refresh();
    }

    public void RefreshShop()
    {
        if (owner == null || config == null || refreshInProgress)
            return;
        int cost = GetRefreshCost();
        if (owner.PlayerState == null || owner.PlayerState.Gold < cost)
        {
            PlayShopSfx(GameSfxId.NotEnoughMoney);
            return;
        }
        if (!owner.TrySpendShopGold(cost))
        {
            PlayShopSfx(GameSfxId.NotEnoughMoney);
            return;
        }
        refreshCount++;
        refreshInProgress = true;
        StartCoroutine(RefreshRoutine());
    }

    /// <summary>
    /// 删除箭头选项的可用状态：每次刷新商店、或重新进入商店时都恢复为可用；
    /// 同一次商店里用过一次后保持置灰（连同隐藏价格文本），直到刷新或换到下一次商店。
    /// </summary>
    private void UpdateRemoveArrowButtonState()
    {
        if (removeArrowButton == null)
            return;

        removeArrowButton.interactable = !removeArrowUsed;
        if (removeArrowCostText == null)
            removeArrowCostText = UIManager.FindChildComponent<TMP_Text>(removeArrowButton.transform, "Cost");
        if (removeArrowCostText != null)
            removeArrowCostText.gameObject.SetActive(!removeArrowUsed);
    }

    private System.Collections.IEnumerator RefreshRoutine()
    {
        yield return AnimateSlotsDisappearRoutine();
        owner.ClearPendingShopMagic();
        ClearUndoPurchase();
        selectedOffer = null;
        waitingForSelection = false;
        purchaseInProgress = false;
        // 刷新等同于重新开一次商店：删除箭头选项重新可用。
        removeArrowUsed = false;
        BuildOffers();
        BuildLayerViews();
        Refresh();
        AnimateSlotsAppear();
        UpdateButtonCosts();
        UpdateRemoveArrowButtonState();
        refreshInProgress = false;
    }

    public void BeginRemoveArrowPurchase()
    {
        if (!HasRemovableMaterial())
            return;
        int price = GetOfferPrice(config != null ? config.shopRemoveMaterialPrice : 0);
        if (owner.PlayerState == null || owner.PlayerState.Gold < price)
        {
            PlayShopSfx(GameSfxId.NotEnoughMoney);
            return;
        }
        if (!owner.TrySpendShopGold(price))
        {
            PlayShopSfx(GameSfxId.NotEnoughMoney);
            return;
        }
        // 标记为已使用：按钮变暗、隐藏价格文本（下一次刷新商店或重新进入商店时恢复）。
        removeArrowUsed = true;
        UpdateRemoveArrowButtonState();
        BeginRemoveArrowSelection();
    }

    private void BeginRemoveArrowSelection()
    {
        owner.ClearPendingShopMagic();
        waitingForSelection = true;
        Refresh();
        MaterialListPanelUI materialListPanel = owner.GetUIManager().MaterialSelectionPanel;
        materialListPanel?.BeginSelection(1, IsRemovableMaterial, selected => CompleteRemoveArrowSelection(selected), CancelSelectionPurchase, LocalizationSystem.GetText("ui.shop.remove_material.title", "选择要删的牌"));
        RectTransform materialRect = materialListPanel != null ? materialListPanel.transform as RectTransform : null;
        if (materialRect != null)
            PopupLayerUtility.ApplyTo(materialRect);
    }

    private void CompleteRemoveArrowSelection(IReadOnlyList<MaterialModel> selected)
    {
        waitingForSelection = false;
        selectedOffer = null;
        if (selected == null || selected.Count == 0)
        {
            Refresh();
            return;
        }
        if (owner.RemoveShopMaterial(selected[0]))
            PlayShopSfx(GameSfxId.Buy);
        Refresh();
    }

    public void Show(LevelData level)
    {
        Show(level, null);
    }

    public void Show(LevelData level, ShopNodeSaveData savedState)
    {
        if (owner == null)
            return;

        CacheReferences();
        config = GameDataDatabase.GetDefaultEconomyConfig() ?? new EconomyConfigData();
        selectedOffer = null;
        waitingForSelection = false;
        purchaseInProgress = false;
        refreshCount = 0;
        refreshInProgress = false;
        // 进入商店（含战斗结束后进入下一个商店节点）时，删除箭头选项重新可用。
        removeArrowUsed = false;
        ClearUndoPurchase();
        owner.ClearPendingShopMagic();
        gameObject.SetActive(true);

        if (titleText != null)
            titleText.text = LocalizationSystem.GetText(level != null ? level.titleKey : string.Empty, LocalizationSystem.GetText("ui.shop.title", "商店"));
        if (hintText != null)
            hintText.text = LocalizationSystem.GetText("ui.shop.hint", "每件商品只能购买一次。道具栏已满时需先卖出道具才能购买道具。");

        BuildOffers();
        BuildLayerViews();
        if (savedState != null)
            RestoreState(savedState);
        BindActionButtons();
        StartShowRoutine();
    }

    public void Hide()
    {
        StopShowRoutine();
        owner?.ClearPendingShopMagic();
        ClearUndoPurchase();
        selectedOffer = null;
        waitingForSelection = false;
        purchaseInProgress = false;
        if (leaveButton != null)
            leaveButton.interactable = false;

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(false);
            return;
        }

        UIManager uiManager = owner?.GetUIManager();
        if (uiManager != null)
        {
            if (refreshButton != null)
                uiManager.HideUnifiedDetailPopup(refreshButton);
            if (removeArrowButton != null)
                uiManager.HideUnifiedDetailPopup(removeArrowButton);
        }

        PlayCloseAnimation();
    }

    public void ShowMaterialTooltip(RectTransform anchor, ShopOffer offer)
    {
        if (anchor == null || offer == null || offer.kind != ShopItemKind.Material)
            return;

        MaterialModel preview = new MaterialModel("shop_tooltip_" + offer.material, offer.material);
        MaterialModifierModel modifier = MaterialModifierFactory.Create(offer.materialModifierData);
        if (modifier != null)
            preview.AddModifier(modifier);
        owner.GetUIManager().MaterialListPanel?.ShowModifierTooltip(anchor, preview);
    }

    public void HideMaterialTooltip(RectTransform anchor)
    {
        owner.GetUIManager().MaterialListPanel?.HideModifierTooltip(anchor);
    }

    private void CacheReferences()
    {
        if (revealMask == null)
            revealMask = FindChildRectRecursive(transform, "RevealMask");
        if (contentRoot == null)
            contentRoot = FindChildRectRecursive(revealMask != null ? revealMask : transform, "Content");

        Transform searchRoot = contentRoot != null ? contentRoot : transform;
        if (itemRoot == null)
            itemRoot = FindChildRectRecursive(searchRoot, "ItemRoot");
        if (titleText == null)
            titleText = FindChildComponentRecursive<TMP_Text>(searchRoot, "Title");
        if (hintText == null)
            hintText = FindChildComponentRecursive<TMP_Text>(searchRoot, "Hint");
        if (goldText == null)
            goldText = FindChildComponentRecursive<TMP_Text>(searchRoot, "GoldText");
        if (goldText != null)
            goldText.gameObject.SetActive(false);
        if (leaveButton == null)
            leaveButton = FindChildComponentRecursive<Button>(searchRoot, "LeaveButton");
        if (refreshButton == null)
            refreshButton = FindChildComponentRecursive<Button>(searchRoot, "RefreshButton");
        if (removeArrowButton == null)
            removeArrowButton = FindChildComponentRecursive<Button>(searchRoot, "RemoveArrowButton");
        if (removeArrowCostText == null && removeArrowButton != null)
            removeArrowCostText = UIManager.FindChildComponent<TMP_Text>(removeArrowButton.transform, "Cost");
        if (materialCardPrefab == null)
        {
            PrefabReferenceLibrary library = GetComponentInParent<PrefabReferenceLibrary>();
            if (library != null)
                materialCardPrefab = library.MaterialCardPrefab;
        }
        if (crtScanLineImage == null)
            crtScanLineImage = FindChildComponentRecursive<Image>(transform, "CRTScanLine");
    }

    private void BuildLayerViews()
    {
        slotViews.Clear();
        if (itemRoot == null)
            return;

        for (int l = 0; l < layerOffers.Count; l++)
        {
            string rowName = GetLayerRowName(shopLayers[l]);
            Transform row = FindChildRecursive(itemRoot, rowName);
            if (row == null)
                continue;
            ShopSlotView[] views = row.GetComponentsInChildren<ShopSlotView>(true);
            System.Array.Sort(views, (ShopSlotView a, ShopSlotView b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
            for (int i = 0; i < views.Length; i++)
            {
                if (views[i] != null)
                    slotViews.Add(views[i]);
            }
        }
        LayoutLayerRows();
    }

    private void LayoutLayerRows()
    {
        if (itemRoot == null)
            return;

        // 隐藏场景中旧的手动分隔线，统一改用预制体实例（美术调整预制体颜色才能生效）。
        for (int i = itemRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = itemRoot.GetChild(i);
            if (child.name.StartsWith("LayerSep") && !IsCreatedSeparator(child.gameObject))
                child.gameObject.SetActive(false);
        }

        int layerCount = layerOffers.Count;
        const float layerGap = 6f;
        const float sepHeight = 1f;
        // 箭头层连同它上方的第一条分隔线整体下移、第二条分隔线保持原位，
        // 使“道具价格 → 第一条分隔线”与“箭头价格 → 第二条分隔线”的间距一致：
        // 道具价格下沿在道具层行框下方 6px、箭头价格下沿在箭头层行框上方 14px，两者相差 20px，各让一半。
        const float arrowLayerDrop = 10f;

        int arrowLayerIndex = -1;
        for (int l = 0; l < layerCount; l++)
        {
            if (GetLayerRowName(shopLayers[l]) == "ArrowLayer")
            {
                arrowLayerIndex = l;
                break;
            }
        }

        float[] rowHeight = new float[layerCount];
        float[] rowCenterY = new float[layerCount];
        float[] separatorCenterY = new float[layerCount];
        float totalHeight = 0f;
        for (int l = 0; l < layerCount; l++)
        {
            int n = layerOffers[l].Count;
            ShopOffer first = n > 0 ? layerOffers[l][0] : null;
            bool isArrowLayer = first != null && first.kind == ShopItemKind.Material;
            float baseHeight = isArrowLayer ? 144f : (first != null && first.kind == ShopItemKind.Magic ? 160f : 120f);
            rowHeight[l] = n > 0 ? baseHeight : 0f;
            totalHeight += rowHeight[l];
            if (l < layerCount - 1)
                totalHeight += layerGap + sepHeight;
        }

        float y = totalHeight * 0.5f + Mathf.Max(1f, Mathf.RoundToInt(439.2f * 0.1f));
        for (int l = 0; l < layerCount; l++)
        {
            rowCenterY[l] = y - rowHeight[l] * 0.5f;
            y -= rowHeight[l] + layerGap + sepHeight;
            separatorCenterY[l] = y + sepHeight * 0.5f;
            y -= sepHeight;
        }

        // 只挪箭头层和它上方的第一条分隔线，第二条分隔线保持原位。
        if (arrowLayerIndex > 0)
        {
            rowCenterY[arrowLayerIndex] -= arrowLayerDrop;
            separatorCenterY[arrowLayerIndex - 1] -= arrowLayerDrop;
        }

        for (int l = 0; l < layerCount; l++)
        {
            string rowName = GetLayerRowName(shopLayers[l]);
            RectTransform row = FindChildRectRecursive(itemRoot, rowName);
            if (row != null)
            {
                HorizontalLayoutGroup hlg = row.GetComponent<HorizontalLayoutGroup>();
                if (hlg != null) hlg.enabled = true;
                ContentSizeFitter csf = row.GetComponent<ContentSizeFitter>();
                if (csf != null) { csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize; csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize; csf.enabled = true; }
                row.anchorMin = new Vector2(0.5f, 0.5f);
                row.anchorMax = new Vector2(0.5f, 0.5f);
                row.pivot = new Vector2(0.5f, 0.5f);
                row.anchoredPosition = new Vector2(0f, rowCenterY[l]);
            }
            // 使用预制体实例化分隔线（如果已创建则只更新位置），避免场景手动放置的分隔线不生效。
            GameObject existingSep = null;
            for (int s = 0; s < createdSeparators.Count; s++)
            {
                if (createdSeparators[s] != null && createdSeparators[s].name == "LayerSep" + l)
                {
                    existingSep = createdSeparators[s];
                    break;
                }
            }
            if (existingSep != null)
            {
                RectTransform sep = existingSep.GetComponent<RectTransform>();
                if (sep != null)
                {
                    sep.anchorMin = new Vector2(0.5f, 0.5f);
                    sep.anchorMax = new Vector2(0.5f, 0.5f);
                    sep.pivot = new Vector2(0.5f, 0.5f);
                    sep.anchoredPosition = new Vector2(0f, separatorCenterY[l]);
                    sep.sizeDelta = new Vector2(754f, sepHeight);
                }
            }
            else if (shopLayerSeparatorPrefab != null)
            {
                RectTransform sepRect = Instantiate(shopLayerSeparatorPrefab, itemRoot);
                GameObject sepObj = sepRect.gameObject;
                sepObj.name = "LayerSep" + l;
                sepRect.anchorMin = new Vector2(0.5f, 0.5f);
                sepRect.anchorMax = new Vector2(0.5f, 0.5f);
                sepRect.pivot = new Vector2(0.5f, 0.5f);
                sepRect.anchoredPosition = new Vector2(0f, separatorCenterY[l]);
                sepRect.sizeDelta = new Vector2(754f, sepHeight);
                createdSeparators.Add(sepObj);
            }
        }
    }

    private bool IsCreatedSeparator(GameObject go)
    {
        for (int i = 0; i < createdSeparators.Count; i++)
        {
            if (createdSeparators[i] == go)
                return true;
        }
        return false;
    }

    private static string GetLayerRowName(ShopLayer layer)
    {
        if (layer == null || layer.weights == null)
            return string.Empty;
        foreach (KeyValuePair<ShopSlotEnum, float> kvp in layer.weights)
        {
            switch (kvp.Key)
            {
                case ShopSlotEnum.Item:
                    return "ItemLayer";
                case ShopSlotEnum.Arrow:
                    return "ArrowLayer";
                case ShopSlotEnum.Relic:
                    return "RelicLayer";
            }
        }
        return string.Empty;
    }

    private static RectTransform FindChildRectRecursive(Transform root, string name)
    {
        Transform child = FindChildRecursive(root, name);
        return child as RectTransform;
    }

    private static T FindChildComponentRecursive<T>(Transform root, string name) where T : Component
    {
        Transform child = FindChildRecursive(root, name);
        return child != null ? child.GetComponent<T>() : null;
    }

    private static Transform FindChildRecursive(Transform root, string name)
    {
        if (root == null)
            return null;

        Transform direct = root.Find(name);
        if (direct != null)
            return direct;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildRecursive(root.GetChild(i), name);
            if (found != null)
                return found;
        }
        return null;
    }

    private void BindLeaveButton()
    {
        if (leaveButton == null)
            return;

        leaveButton.onClick.RemoveAllListeners();
        leaveButton.onClick.AddListener(LeaveShop);
        TMP_Text text = UIManager.FindChildComponent<TMP_Text>(leaveButton.transform, "Text");
        if (text != null)
            text.text = LocalizationSystem.GetText("ui.common.leave", "离开");
    }

    private void BindActionButtons()
    {
        BindLeaveButton();
        if (refreshButton != null)
        {
            refreshButton.onClick.RemoveAllListeners();
            refreshButton.onClick.AddListener(RefreshShop);
        }
        if (removeArrowButton != null)
        {
            removeArrowButton.onClick.RemoveAllListeners();
            removeArrowButton.onClick.AddListener(BeginRemoveArrowPurchase);
        }
        BindHoverDetail(refreshButton, BuildRefreshDetail);
        BindHoverDetail(removeArrowButton, BuildRemoveArrowDetail);
        UpdateButtonCosts();
        UpdateRemoveArrowButtonState();
    }

    private void BindHoverDetail(Button button, Func<UnifiedDetailContent> contentProvider)
    {
        if (button == null)
            return;

        EventTrigger trigger = button.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = button.gameObject.AddComponent<EventTrigger>();

        trigger.triggers.Clear();
        EventTrigger.Entry enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => { UIManager ui = owner?.GetUIManager(); if (ui != null) ui.ShowUnifiedDetailPopup(button, contentProvider()); });
        trigger.triggers.Add(enter);

        EventTrigger.Entry exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ => { UIManager ui = owner?.GetUIManager(); if (ui != null) ui.HideUnifiedDetailPopup(button); });
        trigger.triggers.Add(exit);
    }

    private UnifiedDetailContent BuildRefreshDetail()
    {
        return new UnifiedDetailContent
        {
            SourceType = UnifiedDetailSourceType.ShopFunction,
            Title = LocalizationSystem.GetText("ui.shop.refresh.title", "刷新商品"),
            Body = LocalizationSystem.GetText("ui.shop.refresh.body", "消耗金币以刷新商店内所有商品"),
            Icon = GetButtonIcon(refreshButton),
            AccentColor = new Color(0.94f, 0.76f, 0.34f, 1f)
        };
    }

    private UnifiedDetailContent BuildRemoveArrowDetail()
    {
        return new UnifiedDetailContent
        {
            SourceType = UnifiedDetailSourceType.ShopFunction,
            Title = LocalizationSystem.GetText("ui.shop.remove.title", "删除箭头"),
            Body = LocalizationSystem.GetText("ui.shop.remove.body", "消耗金币以从牌组中删除一个箭头"),
            Icon = GetButtonIcon(removeArrowButton),
            AccentColor = new Color(1f, 0.62f, 0.46f, 1f)
        };
    }

    private static Sprite GetButtonIcon(Button button)
    {
        if (button == null)
            return null;
        Image image = button.GetComponent<Image>();
        return image != null ? image.sprite : null;
    }

    private void UpdateButtonCosts()
    {
        if (refreshButton != null)
        {
            TMP_Text cost = UIManager.FindChildComponent<TMP_Text>(refreshButton.transform, "Cost");
            if (cost != null)
                cost.text = RefreshCost + "$";
        }
        if (removeArrowButton != null)
        {
            if (removeArrowCostText == null)
                removeArrowCostText = UIManager.FindChildComponent<TMP_Text>(removeArrowButton.transform, "Cost");
            if (removeArrowCostText != null)
                removeArrowCostText.text = RemoveArrowPrice + "$";
        }
    }


    private void BuildOffers()
    {
        offers.Clear();
        layerOffers.Clear();
        productPool = GetShopProductPool();
        BuildMagicPool();
        BuildMaterialOfferPools();

        if (shopLayers.Count == 0)
            SetDefaultLayers();

        for (int i = 0; i < shopLayers.Count; i++)
        {
            List<ShopOffer> layerList = new List<ShopOffer>();
            GenerateLayerOffers(shopLayers[i], layerList);
            layerOffers.Add(layerList);
            offers.AddRange(layerList);
        }
    }

    private void SetDefaultLayers()
    {
        shopLayers.Clear();
        shopLayers.Add(ShopLayer.CreateItemLayer());
        shopLayers.Add(ShopLayer.CreateArrowLayer());
    }

    private void GenerateLayerOffers(ShopLayer layer, List<ShopOffer> target)
    {
        if (layer == null || layer.weights == null)
            return;

        // 箭头层：先按普通/弱附魔池销满保底格之外的槽位，再补上保底强附魔箭头（排在最后 = 最后一个格子）。
        int guaranteedArrows = layer.HasType(ShopSlotEnum.Arrow) ? Mathf.Max(0, layer.guaranteedEnchantedArrowCount) : 0;
        float budget = layer.slotLimit - guaranteedArrows * ShopLayer.GetSlotCost(ShopSlotEnum.Arrow);

        float used = 0f;
        bool progressed = true;
        int safety = 0;
        while (progressed && safety < 64)
        {
            safety++;
            progressed = false;
            foreach (KeyValuePair<ShopSlotEnum, float> kvp in layer.weights)
            {
                float cost = ShopLayer.GetSlotCost(kvp.Key);
                if (!layer.isLastLayer && used + cost > budget)
                    continue;
                if (!TryGenerateLayerOffer(kvp.Key, target))
                    continue;
                used += cost;
                progressed = true;
            }
        }

        for (int i = 0; i < guaranteedArrows; i++)
            AddGuaranteedEnchantedArrow(target);
    }

    private bool TryGenerateLayerOffer(ShopSlotEnum type, List<ShopOffer> target)
    {
        switch (type)
        {
            case ShopSlotEnum.Item:
                return TryAddMagicOffer(target);
            case ShopSlotEnum.Arrow:
                return TryAddMaterialOffer(target, preferEnchanted: false);
            default:
                return false;
        }
    }

    /// <summary>保底格：必带强附魔；强附魔池无可用项时退回普通/弱附魔，保证格子数量不变。</summary>
    private void AddGuaranteedEnchantedArrow(List<ShopOffer> target)
    {
        if (strongMaterialOfferPool.Count > 0)
        {
            AddMaterialOfferFromPool(strongMaterialOfferPool, target);
            return;
        }
        if (normalMaterialOfferPool.Count > 0)
        {
            AddMaterialOfferFromPool(normalMaterialOfferPool, target);
            return;
        }
        if (weakMaterialOfferPool.Count > 0)
            AddMaterialOfferFromPool(weakMaterialOfferPool, target);
    }

    private ShopProductPoolData GetShopProductPool()
    {
        if (config != null && config.shopProductPoolId > 0 && GameDataDatabase.TryGetShopProductPoolData(config.shopProductPoolId, out ShopProductPoolData configuredPool))
            return configuredPool;

        foreach (ShopProductPoolData poolData in GameDataDatabase.ShopProductPoolData.Values)
        {
            if (poolData != null)
                return poolData;
        }
        return null;
    }

    private void BuildMagicPool()
    {
        magicPool.Clear();
        if (productPool != null && productPool.magicIds != null && productPool.magicIds.Length > 0)
        {
            for (int i = 0; i < productPool.magicIds.Length; i++)
                AddMagicPoolData(productPool.magicIds[i]);
        }
        else
        {
            RewardPoolData rewardPool = null;
            if (config.shopMagicRewardPoolId > 0)
                GameDataDatabase.TryGetRewardPoolData(config.shopMagicRewardPoolId, out rewardPool);

            if (rewardPool != null && rewardPool.magicIds != null && rewardPool.magicIds.Length > 0)
            {
                for (int i = 0; i < rewardPool.magicIds.Length; i++)
                    AddMagicPoolData(rewardPool.magicIds[i]);
            }
        }

        if (magicPool.Count == 0)
        {
            foreach (MagicData data in GameDataDatabase.MagicData.Values)
            {
                if (data != null && UnlockSystem.IsMagicUnlocked(data))
                    magicPool.Add(data);
            }
        }
    }

    private void AddMagicPoolData(int magicId)
    {
        if (GameDataDatabase.TryGetMagicData(magicId, out MagicData data) && data != null && UnlockSystem.IsMagicUnlocked(data) && !magicPool.Contains(data))
            magicPool.Add(data);
    }

    private void BuildMaterialOfferPools()
    {
        strongMaterialOfferPool.Clear();
        normalMaterialOfferPool.Clear();
        weakMaterialOfferPool.Clear();

        if (productPool != null)
        {
            AddMaterialOffers(productPool.strongMaterialOffers, strongMaterialOfferPool, ShopMaterialPoolKind.Strong);
            AddMaterialOffers(productPool.normalMaterialOffers, normalMaterialOfferPool, ShopMaterialPoolKind.Normal);
            AddMaterialOffers(productPool.weakMaterialOffers, weakMaterialOfferPool, ShopMaterialPoolKind.Weak);
        }

        if (normalMaterialOfferPool.Count == 0)
        {
            normalMaterialOfferPool.Add(new ShopMaterialOfferData { material = MaterialEnum.Fire, price = config.shopMaterialPrice });
            normalMaterialOfferPool.Add(new ShopMaterialOfferData { material = MaterialEnum.Wind, price = config.shopMaterialPrice });
            normalMaterialOfferPool.Add(new ShopMaterialOfferData { material = MaterialEnum.Water, price = config.shopMaterialPrice });
            normalMaterialOfferPool.Add(new ShopMaterialOfferData { material = MaterialEnum.Earth, price = config.shopMaterialPrice });
        }
    }

    private int ResolveMaterialBasePrice()
    {
        // 基础价 = 商品池里“普通箭头”（无附魔）的报价；池里没有时回落到经济配置的箭头价。
        for (int i = 0; i < normalMaterialOfferPool.Count; i++)
        {
            ShopMaterialOfferData pooledOffer = normalMaterialOfferPool[i];
            if (pooledOffer != null && pooledOffer.price >= 0)
                return pooledOffer.price;
        }

        for (int i = 0; productPool != null && i < productPool.normalMaterialOffers.Length; i++)
        {
            ShopMaterialOfferData configuredOffer = productPool.normalMaterialOffers[i];
            if (configuredOffer != null && configuredOffer.price >= 0)
                return configuredOffer.price;
        }

        return config != null ? Mathf.Max(0, config.shopMaterialPrice) : 0;
    }

    private enum ShopMaterialPoolKind
    {
        Normal,
        Weak,
        Strong
    }

    private void AddMaterialOffers(ShopMaterialOfferData[] source, List<ShopMaterialOfferData> target, ShopMaterialPoolKind kind)
    {
        for (int i = 0; source != null && i < source.Length; i++)
        {
            ShopMaterialOfferData offer = source[i];
            if (IsValidShopMaterialOffer(offer, kind))
                target.Add(offer);
        }
    }

    private bool IsValidShopMaterialOffer(ShopMaterialOfferData offer, ShopMaterialPoolKind kind)
    {
        if (offer == null || offer.material == MaterialEnum.None)
            return false;

        if (string.IsNullOrEmpty(offer.modifierId))
            return kind == ShopMaterialPoolKind.Normal;

        if (!IsValidShopModifierId(offer.modifierId))
            return false;

        bool weak = IsWeakShopModifierId(offer.modifierId);
        if (kind == ShopMaterialPoolKind.Weak)
            return weak;
        if (kind == ShopMaterialPoolKind.Strong)
            return !weak;
        return false;
    }

    private bool IsValidShopModifierId(string modifierId)
    {
        if (string.IsNullOrEmpty(modifierId) || IsExcludedShopModifierId(modifierId))
            return false;

        MaterialModifierData data = GetMaterialModifierDataById(modifierId);
        return data != null && UnlockSystem.IsMaterialModifierUnlocked(data) && !string.IsNullOrEmpty(data.script) && MaterialModifierFactory.Create(data) != null;
    }

    private static bool IsWeakShopModifierId(string modifierId)
    {
        switch (modifierId)
        {
            case "half_arrow":
            case "temporary":
            case "doom":
            case "lazy":
            case "fragile_arrow":
                return true;
            default:
                return false;
        }
    }

    private static bool IsExcludedShopModifierId(string modifierId)
    {
        switch (modifierId)
        {
            case "omni_arrow":
            case "return_arrow":
            case "period_arrow":
            case "pack_arrow":
            case "linked_arrow":
            case "random_arrow":
            case "eternal_arrow":
            case "repeat_arrow":
                return true;
            default:
                return false;
        }
    }

    private MaterialModifierData GetMaterialModifierDataById(string modifierId)
    {
        if (string.IsNullOrEmpty(modifierId))
            return null;

        DataTable<MaterialModifierData> table = GameDataReader.LoadTable<MaterialModifierData>("MaterialModifierData");
        for (int i = 0; table != null && table.items != null && i < table.items.Count; i++)
        {
            MaterialModifierData data = table.items[i];
            if (data != null && data.id == modifierId)
                return data;
        }
        return null;
    }

    private bool TryAddMagicOffer(List<ShopOffer> target)
    {
        if (magicPool.Count == 0)
            return false;

        MagicData data = MagicRaritySystem.SelectWeightedMagic(magicPool, NextRunRandomInt);
        if (data == null)
            return false;

        magicPool.Remove(data);
        target.Add(new ShopOffer { kind = ShopItemKind.Magic, price = GetMagicBuyPrice(data), magicData = data });
        return true;
    }

    public static int GetMagicBuyPrice(MagicData data)
    {
        if (data == null)
            return 0;

        EconomyConfigData economy = GameDataDatabase.GetDefaultEconomyConfig() ?? new EconomyConfigData();
        return DifficultyUpgradeSystem.ModifyShopPrice(economy.shopSpellPrice + GetMagicRarityPriceOffset(data.rarity));
    }

    public static int GetMagicSellPrice(MagicData data)
    {
        // 卖出价 = 购买价减半（向下取整）。
        return Mathf.Max(0, GetMagicBuyPrice(data) / 2);
    }

    private static int GetMagicRarityPriceOffset(MagicRarity rarity)
    {
        switch (rarity)
        {
            case MagicRarity.Common:
                return -2;
            case MagicRarity.Rare:
                return -1;
            case MagicRarity.Legendary:
                return 1;
            default:
                return 0;
        }
    }

    private bool TryAddMaterialOffer(List<ShopOffer> target, bool preferEnchanted)
    {
        if (preferEnchanted && strongMaterialOfferPool.Count > 0)
            return AddMaterialOfferFromPool(strongMaterialOfferPool, target);

        // 普通格：默认普通箭头，按 weakMaterialChance 概率出弱附魔箭头；所选池子取完后回退其它池子。
        bool useWeak = ShouldUseWeakMaterialOffer();
        if (useWeak && weakMaterialOfferPool.Count > 0 && AddMaterialOfferFromPool(weakMaterialOfferPool, target))
            return true;
        if (normalMaterialOfferPool.Count > 0 && AddMaterialOfferFromPool(normalMaterialOfferPool, target))
            return true;
        if (!useWeak && weakMaterialOfferPool.Count > 0 && AddMaterialOfferFromPool(weakMaterialOfferPool, target))
            return true;
        if (strongMaterialOfferPool.Count > 0)
            return AddMaterialOfferFromPool(strongMaterialOfferPool, target);
        return false;
    }

    private bool ShouldUseWeakMaterialOffer()
    {
        float chance = productPool != null ? productPool.weakMaterialChance : 0.1f;
        if (chance <= 0f)
            return false;
        if (chance >= 1f)
            return true;

        int threshold = Mathf.RoundToInt(chance * 10000f);
        return NextRunRandomInt(0, 10000) < threshold;
    }

    private bool AddMaterialOfferFromPool(List<ShopMaterialOfferData> pool, List<ShopOffer> target)
    {
        if (pool.Count == 0)
            return false;

        int index = NextRunRandomInt(0, pool.Count);
        ShopMaterialOfferData offerData = pool[index];
        pool.RemoveAt(index);
        MaterialModifierData modifierData = GetMaterialModifierDataById(offerData.modifierId);
        target.Add(new ShopOffer { kind = ShopItemKind.Material, price = GetMaterialOfferPrice(offerData), material = offerData.material, materialModifierData = modifierData });
        return true;
    }

    private int GetOfferPrice(int price)
    {
        // 显式价格（含免费 0）直接生效；仅无效负价回落到箭头默认价。
        int basePrice = price >= 0 ? price : config.shopMaterialPrice;
        return DifficultyUpgradeSystem.ModifyShopPrice(basePrice);
    }

    /// <summary>
    /// 商店箭头价格：无附魔箭头用条目报价；带附魔的箭头 = 基础价 + 附魔自身的 price 差值
    /// （见 <see cref="MaterialModifierDefinition.price"/>，0 = 不影响价格、负数 = 弱附魔更便宜），
    /// 不再读取条目里为附魔箭头填的 price；最后统一经 <see cref="DifficultyUpgradeSystem.ModifyShopPrice"/>。
    /// </summary>
    private int GetMaterialOfferPrice(ShopMaterialOfferData offerData)
    {
        if (offerData == null)
            return GetOfferPrice(0);

        if (string.IsNullOrEmpty(offerData.modifierId))
            return GetOfferPrice(offerData.price);

        int price = ResolveMaterialBasePrice() + GetMaterialEnchantPriceDelta(offerData.modifierId);
        return GetOfferPrice(Mathf.Max(0, price));
    }

    /// <summary>附魔自身的价格差值（<see cref="MaterialModifierData.price"/>）；查不到定义时按 0 处理（不影响价格）。</summary>
    private static int GetMaterialEnchantPriceDelta(string modifierId)
    {
        return MaterialModifierDatabase.TryGetData(modifierId, out MaterialModifierData data) && data != null ? data.price : 0;
    }

    private int NextRunRandomInt(int minInclusive, int maxExclusive)
    {
        return owner != null && owner.RunManager != null ? owner.RunManager.NextRandomInt(minInclusive, maxExclusive) : UnityEngine.Random.Range(minInclusive, maxExclusive);
    }

    private void Refresh()
    {
        CacheReferences();
        if (hintText != null)
            hintText.gameObject.SetActive(!(waitingForSelection && selectedOffer != null && selectedOffer.kind == ShopItemKind.Magic));
        if (goldText != null)
            goldText.gameObject.SetActive(false);
        if (leaveButton != null)
            leaveButton.interactable = !purchaseInProgress;

        bool blockingSelection = waitingForSelection && selectedOffer != null && selectedOffer.kind != ShopItemKind.Magic;
        for (int i = 0; i < slotViews.Count; i++)
        {
            // 已购商品保留占位（保持 active、显示空），使 HLG 不因隐藏已购格而重排其它商品。
            bool visible = i < offers.Count;
            slotViews[i].gameObject.SetActive(visible);
            if (!visible)
                continue;

            ShopOffer offer = offers[i];
            bool canAfford = owner.PlayerState != null && owner.PlayerState.Gold >= offer.price;
            bool selected = offer == selectedOffer;
            bool canUse = !purchaseInProgress && (!blockingSelection || selected) && CanUseOffer(offer);
            slotViews[i].Bind(this, offer, canAfford, canUse, selected, OnOfferClicked);
        }
    }

    private bool CanUseOffer(ShopOffer offer)
    {
        if (offer == null)
            return false;

        switch (offer.kind)
        {
            case ShopItemKind.RemoveMaterial:
                return HasRemovableMaterial();
            case ShopItemKind.Magic:
                // 道具栏已满：替换机制已移除，必须先卖出道具腾出空位才能购买道具。
                return owner != null && owner.HasFreeMagicSlot;
            default:
                return true;
        }
    }

    private void StartShowRoutine()
    {
        StopShowRoutine();
        showRoutine = StartCoroutine(ShowRoutine());
    }

    private void StopShowRoutine()
    {
        if (showRoutine != null)
        {
            StopCoroutine(showRoutine);
            showRoutine = null;
        }
    }

    private IEnumerator ShowRoutine()
    {
        HideItemViewsForOpeningFrame();
        PlayOpenAnimation();
        yield return null;
        Refresh();
        AnimateSlotsAppear();
        showRoutine = null;
    }

    private void HideItemViewsForOpeningFrame()
    {
        for (int i = 0; i < slotViews.Count; i++)
        {
            if (slotViews[i] != null)
                slotViews[i].gameObject.SetActive(false);
        }
    }

    private void PlayOpenAnimation()
    {
        DOTween.Kill(this);

        RectTransform panelRect = transform as RectTransform;
        if (panelRect == null)
            return;

        CacheReferences();
        CapturePanelLayout(panelRect);
        panelRect.anchoredPosition = panelOpenPosition;

        // CRT 开机：从一条中心水平亮线开始，竖向展开还原整幅画面。
        panelRect.localScale = GetLineScale();
        SetScanLineAlpha(1f);

        Vector3 baseScale = panelBaseScale;
        float duration = Mathf.Max(0f, crtCollapseDuration);
        if (duration > 0f)
        {
            Sequence seq = DOTween.Sequence().SetTarget(this);
            seq.Join(panelRect.DOScale(baseScale, duration).SetEase(crtCollapseEase));
            if (crtScanLineImage != null)
                seq.Join(crtScanLineImage.DOFade(0f, duration));
        }
        else
        {
            panelRect.localScale = baseScale;
            SetScanLineAlpha(0f);
        }
    }

    private void PlayCloseAnimation()
    {
        DOTween.Kill(this);

        RectTransform panelRect = transform as RectTransform;
        if (panelRect == null)
        {
            gameObject.SetActive(false);
            return;
        }

        CacheReferences();
        CapturePanelLayout(panelRect);
        panelRect.anchoredPosition = panelOpenPosition;

        Vector3 baseScale = panelBaseScale;
        Vector3 lineScale = GetLineScale();
        Sequence seq = DOTween.Sequence().SetTarget(this);

        // 1) 上下合成一条水平线：亮线随画面压缩叠亮。
        float collapse = Mathf.Max(0f, crtCollapseDuration);
        if (collapse > 0f)
        {
            seq.Append(panelRect.DOScale(lineScale, collapse).SetEase(crtCollapseEase));
            if (crtScanLineImage != null)
                seq.Join(crtScanLineImage.DOFade(1f, collapse));
        }
        else
        {
            panelRect.localScale = lineScale;
            SetScanLineAlpha(1f);
        }

        // 2) 亮线短暂停留。
        if (crtLineHoldDuration > 0f)
            seq.AppendInterval(crtLineHoldDuration);

        // 3) 横向向中心收缩消失。
        float shrink = Mathf.Max(0f, crtShrinkDuration);
        if (shrink > 0f)
        {
            seq.Append(panelRect.DOScale(new Vector3(baseScale.x * 0.001f, lineScale.y, baseScale.z), shrink).SetEase(crtShrinkEase));
            if (crtScanLineImage != null)
                seq.Join(crtScanLineImage.DOFade(0f, shrink));
        }

        seq.OnComplete(FinishCloseAnimation);
    }

    private void AnimateSlotsAppear()
    {
        int viewIndex = 0;
        for (int l = 0; l < layerOffers.Count; l++)
        {
            int count = layerOffers[l].Count;
            for (int i = 0; i < count; i++, viewIndex++)
            {
                if (viewIndex >= slotViews.Count)
                    break;
                RectTransform itemRect = slotViews[viewIndex].transform as RectTransform;
                if (itemRect == null || !itemRect.gameObject.activeSelf)
                    continue;

                Vector3 targetScale = itemRect.localScale;
                if (targetScale == Vector3.zero)
                    targetScale = Vector3.one;

                itemRect.localScale = Vector3.zero;
                if (slotAppearDuration > 0f)
                {
                    Tweener tween = itemRect.DOScale(targetScale, slotAppearDuration);
                    tween.SetDelay(i * Mathf.Max(0f, slotStaggerDelay)).SetEase(slotAppearEase).SetTarget(this);
                }
                else
                {
                    itemRect.localScale = targetScale;
                }
            }
        }
    }

    private System.Collections.IEnumerator AnimateSlotsDisappearRoutine()
    {
        int viewIndex = 0;
        int total = 0;
        for (int l = 0; l < layerOffers.Count; l++)
        {
            int count = layerOffers[l].Count;
            for (int i = 0; i < count; i++, viewIndex++)
            {
                if (viewIndex >= slotViews.Count)
                    break;
                RectTransform itemRect = slotViews[viewIndex].transform as RectTransform;
                if (itemRect == null || !itemRect.gameObject.activeSelf)
                    continue;
                if (slotDisappearDuration > 0f)
                    total++;
            }
        }

        if (total == 0)
            yield break;

        viewIndex = 0;
        int remaining = total;
        for (int l = 0; l < layerOffers.Count; l++)
        {
            int count = layerOffers[l].Count;
            for (int i = 0; i < count; i++, viewIndex++)
            {
                if (viewIndex >= slotViews.Count)
                    break;
                RectTransform itemRect = slotViews[viewIndex].transform as RectTransform;
                if (itemRect == null || !itemRect.gameObject.activeSelf)
                    continue;

                if (slotDisappearDuration > 0f)
                {
                    itemRect.DOScale(Vector3.zero, slotDisappearDuration)
                        .SetDelay(i * Mathf.Max(0f, slotStaggerDelay))
                        .SetEase(slotDisappearEase)
                        .SetTarget(this)
                        .OnComplete(() => { remaining = remaining - 1; });
                }
            }
        }

        while (remaining > 0)
            yield return null;
    }

    private void FinishCloseAnimation()
    {
        RectTransform panelRect = transform as RectTransform;
        if (panelRect != null)
        {
            panelRect.anchoredPosition = panelOpenPosition;
            panelRect.localScale = panelBaseScale;
        }
        SetScanLineAlpha(0f);
        gameObject.SetActive(false);
    }

    private void CapturePanelLayout(RectTransform panelRect)
    {
        if (hasPanelLayout)
            return;

        panelOpenPosition = panelRect.anchoredPosition;
        panelBaseScale = panelRect.localScale;
        hasPanelLayout = true;
    }

    private Vector3 GetLineScale()
    {
        return new Vector3(panelBaseScale.x, panelBaseScale.y * crtLineYRatio, panelBaseScale.z);
    }

    private void SetScanLineAlpha(float alpha)
    {
        if (crtScanLineImage == null)
            return;
        Color c = crtScanLineImage.color;
        c.a = alpha;
        crtScanLineImage.color = c;
    }


    private bool HasRemovableMaterial()
    {
        return owner != null && owner.PlayerState != null && owner.PlayerState.Deck.Count > 0;
    }

    private void LeaveShop()
    {
        CancelMagicPurchaseSelection(false);
        ClearUndoPurchase();
        owner.FinishReward();
    }

    private void OnOfferClicked(ShopOffer offer)
    {
        if (offer == null || offer.purchased || owner == null || owner.PlayerState == null)
            return;

        if (purchaseInProgress)
            return;

        if (waitingForSelection && selectedOffer != null && selectedOffer.kind == ShopItemKind.Magic)
        {
            if (offer == selectedOffer)
            {
                CancelMagicPurchaseSelection(true);
                return;
            }

            CancelMagicPurchaseSelection(false);
        }

        if (owner.PlayerState.Gold < offer.price)
        {
            PlayShopSfx(GameSfxId.NotEnoughMoney);
            return;
        }

        switch (offer.kind)
        {
            case ShopItemKind.Magic:
                BeginMagicPurchase(offer);
                break;
            case ShopItemKind.Material:
                CompleteMaterialPurchase(offer);
                break;
            case ShopItemKind.RemoveMaterial:
                BeginRemoveMaterialPurchase(offer);
                break;
        }
    }

    private void BeginMagicPurchase(ShopOffer offer)
    {
        if (offer.magicData == null)
            return;

        // 道具栏已满：不再提供替换机制，购买直接不生效（按钮已由 CanUseOffer 置为不可用）。
        int targetSlot = owner != null ? owner.GetFreeMagicSlotIndex() : -1;
        if (targetSlot < 0)
            return;

        selectedOffer = offer;
        waitingForSelection = false;
        purchaseInProgress = false;
        owner.ClearPendingShopMagic();

        CompleteMagicPurchase(offer, targetSlot);
    }

    private void CancelMagicPurchaseSelection(bool refresh)
    {
        if (selectedOffer == null || selectedOffer.kind != ShopItemKind.Magic)
            return;

        owner.ClearPendingShopMagic();
        selectedOffer = null;
        waitingForSelection = false;
        if (refresh)
            Refresh();
    }

    private void CompleteMagicPurchase(ShopOffer offer, int slotIndex)
    {
        waitingForSelection = false;
        owner.ClearPendingShopMagic();
        if (offer == null || offer.purchased || offer.magicData == null)
        {
            selectedOffer = null;
            Refresh();
            return;
        }
        int goldBefore = owner.PlayerState.Gold;
        MagicModel previousMagic = owner.PlayerState.GetMagicAtSlot(slotIndex);
        if (!owner.TrySpendShopGold(offer.price))
        {
            selectedOffer = null;
            PlayShopSfx(GameSfxId.NotEnoughMoney);
            Refresh();
            return;
        }

        PlayShopSfx(GameSfxId.Buy);
        // 先捕获飞行起点（此时商品视觉仍在），再标记已购：进入 tween 时价格/内容立即消失（槽位保留占位，其它商品不重排）。
        RectTransform sourceRect = GetMagicOfferRect(offer);
        offer.purchased = true;
        purchaseInProgress = true;
        Refresh();
        owner.SetShopMagicAtSlotAnimated(offer.magicData, slotIndex, sourceRect, () =>
        {
            purchaseInProgress = false;
            selectedOffer = null;
            offer.purchased = true;
            RegisterUndoMagicPurchase(offer, goldBefore, slotIndex, previousMagic);
            Refresh();
        });
    }

    private void CompleteMaterialPurchase(ShopOffer offer)
    {
        int goldBefore = owner.PlayerState.Gold;
        int deckCountBefore = owner.PlayerState.Deck.Count;
        if (!owner.TrySpendShopGold(offer.price))
        {
            PlayShopSfx(GameSfxId.NotEnoughMoney);
            Refresh();
            return;
        }

        PlayShopSfx(GameSfxId.Buy);
        // 先捕获飞行起点，再标记已购：进入 tween 时价格/内容立即消失（槽位保留占位，其它商品不重排）。
        RectTransform sourceRect = GetMaterialOfferRect(offer);
        offer.purchased = true;
        purchaseInProgress = true;
        Refresh();
        owner.AddShopMaterialAnimated(offer.material, offer.materialModifierData, sourceRect, () =>
        {
            purchaseInProgress = false;
            offer.purchased = true;
            MaterialModel added = owner.PlayerState.Deck.Count > deckCountBefore ? owner.PlayerState.Deck[owner.PlayerState.Deck.Count - 1] : null;
            RegisterUndoMaterialPurchase(offer, goldBefore, added);
            Refresh();
        });
    }

    private void BeginRemoveMaterialPurchase(ShopOffer offer)
    {
        if (!HasRemovableMaterial())
        {
            Refresh();
            return;
        }

        selectedOffer = offer;
        owner.ClearPendingShopMagic();
        waitingForSelection = true;
        Refresh();
        MaterialListPanelUI materialListPanel = owner.GetUIManager().MaterialSelectionPanel;
        materialListPanel?.BeginSelection(1, IsRemovableMaterial, selected => CompleteRemoveMaterialPurchase(offer, selected), CancelSelectionPurchase, LocalizationSystem.GetText("ui.shop.remove_material.title", "选择要删的牌"));
        RectTransform materialRect = materialListPanel != null ? materialListPanel.transform as RectTransform : null;
        if (materialRect != null)
            PopupLayerUtility.ApplyTo(materialRect);
    }

    private void CancelSelectionPurchase()
    {
        waitingForSelection = false;
        selectedOffer = null;
        Refresh();
    }

    private bool IsRemovableMaterial(MaterialModel material)
    {
        return material != null && owner != null && owner.PlayerState != null && owner.PlayerState.Deck.Contains(material) && !PlayerState.IsDeckPlaceholderMaterial(material);
    }

    private void CompleteRemoveMaterialPurchase(ShopOffer offer, IReadOnlyList<MaterialModel> selected)
    {
        waitingForSelection = false;
        selectedOffer = null;
        if (offer == null || offer.purchased || selected == null || selected.Count == 0)
        {
            Refresh();
            return;
        }
        int goldBefore = owner.PlayerState.Gold;
        MaterialModel removedMaterial = selected[0];
        if (!owner.TrySpendShopGold(offer.price))
        {
            PlayShopSfx(GameSfxId.NotEnoughMoney);
            Refresh();
            return;
        }

        if (owner.RemoveShopMaterial(selected[0]))
        {
            PlayShopSfx(GameSfxId.Buy);
            offer.purchased = true;
            RegisterUndoRemoveMaterialPurchase(offer, goldBefore, removedMaterial);
        }
        Refresh();
    }

    public static bool TryExportCurrentState(PlayerState player, out ShopNodeSaveData data)
    {
        data = null;
        if (player == null)
            return false;

        ShopPanelUI panel = UnityEngine.Object.FindObjectOfType<ShopPanelUI>(true);
        if (panel == null || !panel.gameObject.activeInHierarchy)
            return false;

        data = panel.ExportState();
        return data != null;
    }

    public ShopNodeSaveData ExportState()
    {
        ShopNodeSaveData data = new ShopNodeSaveData
        {
            offers = new ShopOfferSaveData[offers.Count],
            selectedOfferIndex = selectedOffer != null ? offers.IndexOf(selectedOffer) : -1,
            waitingForSelection = waitingForSelection,
            purchaseInProgress = purchaseInProgress,
            removeArrowUsed = removeArrowUsed,
            undo = ExportUndoState()
        };

        for (int i = 0; i < offers.Count; i++)
            data.offers[i] = offers[i] != null ? offers[i].Export() : null;

        return data;
    }

    private void RestoreState(ShopNodeSaveData savedState)
    {
        if (savedState == null)
            return;

        int count = Mathf.Min(offers.Count, savedState.offers != null ? savedState.offers.Length : 0);
        for (int i = 0; i < count; i++)
        {
            ShopOffer target = offers[i];
            ShopOfferSaveData source = savedState.offers[i];
            if (target == null || source == null)
                continue;

            target.price = source.price;
            target.purchased = source.purchased;
            ApplySavedOfferData(target, source);
        }

        selectedOffer = savedState.selectedOfferIndex >= 0 && savedState.selectedOfferIndex < offers.Count ? offers[savedState.selectedOfferIndex] : null;
        waitingForSelection = savedState.waitingForSelection;
        purchaseInProgress = false;
        // 读档回到同一次商店：已用过的删除箭头选项保持置灰。
        removeArrowUsed = savedState.removeArrowUsed;
        RestoreUndoState(savedState.undo);

        // 道具购买已不再需要“等待点选道具槽”：旧存档里残留的该状态直接丢弃。
        if (waitingForSelection && selectedOffer != null && selectedOffer.kind == ShopItemKind.Magic)
        {
            selectedOffer = null;
            waitingForSelection = false;
        }

        if (!waitingForSelection)
            owner.ClearPendingShopMagic();
    }

    private void ApplySavedOfferData(ShopOffer target, ShopOfferSaveData source)
    {
        target.kind = (ShopItemKind)source.kind;
        target.material = (MaterialEnum)source.material;
        if (source.magicNumericId > 0)
            GameDataDatabase.TryGetMagicData(source.magicNumericId, out target.magicData);
        else
            target.magicData = null;
        target.materialModifierData = !string.IsNullOrEmpty(source.materialModifierId) ? GetMaterialModifierDataById(source.materialModifierId) : null;
    }

    private ShopUndoSaveData ExportUndoState()
    {
        if (!undoAvailable)
            return null;

        return new ShopUndoSaveData
        {
            offerIndex = undoOffer != null ? offers.IndexOf(undoOffer) : -1,
            gold = undoGold,
            magicSlotIndex = undoMagicSlotIndex,
            previousMagicNumericId = undoPreviousMagic != null ? undoPreviousMagic.NumericId : 0,
            previousMagicModifierId = undoPreviousMagic != null && undoPreviousMagic.PrimaryModifier != null ? undoPreviousMagic.PrimaryModifier.Id : string.Empty,
            addedMaterial = ExportUndoMaterial(undoAddedMaterial),
            removedMaterial = ExportUndoMaterial(undoRemovedMaterial)
        };
    }

    private void RestoreUndoState(ShopUndoSaveData data)
    {
        ClearUndoPurchase();
        if (data == null)
            return;

        undoOffer = data.offerIndex >= 0 && data.offerIndex < offers.Count ? offers[data.offerIndex] : null;
        undoGold = data.gold;
        undoMagicSlotIndex = data.magicSlotIndex;
        undoPreviousMagic = CreateUndoMagic(data.previousMagicNumericId, data.previousMagicModifierId, data.magicSlotIndex);
        undoAddedMaterial = CreateUndoMaterial(data.addedMaterial);
        undoRemovedMaterial = CreateUndoMaterial(data.removedMaterial);
        undoAvailable = undoOffer != null || undoAddedMaterial != null || undoRemovedMaterial != null || undoMagicSlotIndex >= 0;
    }

    private static MaterialCardSaveData ExportUndoMaterial(MaterialModel material)
    {
        if (material == null)
            return null;

        return new MaterialCardSaveData
        {
            instanceId = material.instanceId,
            material = (int)material.material,
            alternateMaterial = (int)material.alternateMaterial,
            enhancementIds = material.enhancementIds.ToArray(),
            modifierIds = ExportModifierIds(material.modifiers),
            linkedCards = Array.Empty<MaterialCardSaveData>(),
            isTemporary = material.isTemporary,
            isRetained = material.isRetained
        };
    }

    private static string[] ExportModifierIds(IReadOnlyList<MaterialModifierModel> modifiers)
    {
        List<string> ids = new List<string>();
        for (int i = 0; modifiers != null && i < modifiers.Count; i++)
        {
            string id = MaterialModifierFactory.GetId(modifiers[i]);
            if (!string.IsNullOrEmpty(id))
                ids.Add(id);
        }
        return ids.ToArray();
    }

    private static MaterialModel CreateUndoMaterial(MaterialCardSaveData data)
    {
        if (data == null)
            return null;

        MaterialModel card = new MaterialModel(data.instanceId, (MaterialEnum)data.material)
        {
            alternateMaterial = (MaterialEnum)data.alternateMaterial,
            isRetained = data.isRetained
        };
        if (data.enhancementIds != null)
            card.enhancementIds.AddRange(data.enhancementIds);
        for (int i = 0; data.modifierIds != null && i < data.modifierIds.Length; i++)
        {
            MaterialModifierModel modifier = MaterialModifierFactory.Create(data.modifierIds[i]);
            if (modifier != null)
                card.AddModifier(modifier);
        }
        if (data.isTemporary && !card.isTemporary)
            card.AddModifier(new TemporaryModifier());
        return card;
    }

    private MagicModel CreateUndoMagic(int magicNumericId, string modifierId, int slotIndex)
    {
        if (magicNumericId <= 0 || !GameDataDatabase.TryGetMagicData(magicNumericId, out MagicData data))
            return null;

        MagicModel magic = MagicFactory.Create(data, slotIndex);
        if (!string.IsNullOrEmpty(modifierId) && GameDataDatabase.TryGetMagicModifierData(modifierId, out MagicModifierData modifierData))
            magic.AddModifier(MagicModifierFactory.Create(modifierData));
        return magic;
    }


    private RectTransform GetMagicOfferRect(ShopOffer offer)
    {
        ShopSlotView view = GetItemView(offer);
        return view != null ? view.MagicVisualRect : null;
    }

    private RectTransform GetMaterialOfferRect(ShopOffer offer)
    {
        ShopSlotView view = GetItemView(offer);
        return view != null ? view.MaterialVisualRect : null;
    }

    private ShopSlotView GetItemView(ShopOffer offer)
    {
        int index = offers.IndexOf(offer);
        return index >= 0 && index < slotViews.Count ? slotViews[index] : null;
    }

    private static void PlayShopSfx(GameSfxId id)
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySfx(id);
    }

    public bool TryUndoLastPurchase()
    {
        if (!undoAvailable || owner == null || owner.PlayerState == null || purchaseInProgress)
            return false;

        int goldDelta = undoGold - owner.PlayerState.Gold;
        if (goldDelta != 0)
            owner.PlayerState.AddGold(goldDelta, false);

        if (undoMagicSlotIndex >= 0)
        {
            if (undoPreviousMagic != null)
                owner.PlayerState.SetMagicAtSlot(undoPreviousMagic, undoMagicSlotIndex);
            else
                owner.PlayerState.ClearMagicSlot(undoMagicSlotIndex);
        }
        if (undoAddedMaterial != null)
            owner.PlayerState.RemoveCardEverywhere(undoAddedMaterial);
        if (undoRemovedMaterial != null && !owner.PlayerState.Deck.Contains(undoRemovedMaterial))
            owner.PlayerState.Deck.Add(undoRemovedMaterial);
        if (undoOffer != null)
            undoOffer.purchased = false;

        owner.CreateMagicViewsForShopUndo();
        owner.RefreshShopUndoUI();
        ClearUndoPurchase();
        Refresh();
        return true;
    }

    private void RegisterUndoMagicPurchase(ShopOffer offer, int goldBefore, int slotIndex, MagicModel previousMagic)
    {
        ClearUndoPurchase();
        undoOffer = offer;
        undoGold = goldBefore;
        undoMagicSlotIndex = slotIndex;
        undoPreviousMagic = previousMagic;
        undoAvailable = true;
    }

    private void RegisterUndoMaterialPurchase(ShopOffer offer, int goldBefore, MaterialModel addedMaterial)
    {
        ClearUndoPurchase();
        undoOffer = offer;
        undoGold = goldBefore;
        undoAddedMaterial = addedMaterial;
        undoAvailable = true;
    }

    private void RegisterUndoRemoveMaterialPurchase(ShopOffer offer, int goldBefore, MaterialModel removedMaterial)
    {
        ClearUndoPurchase();
        undoOffer = offer;
        undoGold = goldBefore;
        undoRemovedMaterial = removedMaterial;
        undoAvailable = true;
    }

    private void ClearUndoPurchase()
    {
        undoOffer = null;
        undoGold = 0;
        undoMagicSlotIndex = -1;
        undoPreviousMagic = null;
        undoAddedMaterial = null;
        undoRemovedMaterial = null;
        undoAvailable = false;
    }
}
