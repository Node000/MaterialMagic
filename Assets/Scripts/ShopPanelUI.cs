using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
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

    private System.Collections.IEnumerator RefreshRoutine()
    {
        yield return AnimateSlotsDisappearRoutine();
        owner.ClearPendingShopMagic();
        ClearUndoPurchase();
        selectedOffer = null;
        waitingForSelection = false;
        purchaseInProgress = false;
        BuildOffers();
        BuildLayerViews();
        Refresh();
        AnimateSlotsAppear();
        UpdateButtonCosts();
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
        // 标记为已使用：按钮变暗、隐藏价格文本
        removeArrowButton.interactable = false;
        if (removeArrowButton != null)
        {
            TMP_Text costText = UIManager.FindChildComponent<TMP_Text>(removeArrowButton.transform, "Cost");
            if (costText != null)
                costText.gameObject.SetActive(false);
        }
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
        ClearUndoPurchase();
        owner.ClearPendingShopMagic();
        gameObject.SetActive(true);

        if (titleText != null)
            titleText.text = LocalizationSystem.GetText(level != null ? level.titleKey : string.Empty, LocalizationSystem.GetText("ui.shop.title", "商店"));
        if (hintText != null)
            hintText.text = LocalizationSystem.GetText("ui.shop.hint", "每件商品只能购买一次。道具购买后点击已有道具槽完成覆盖。");

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

        float[] rowHeight = new float[layerCount];
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
                row.anchoredPosition = new Vector2(0f, y - rowHeight[l] * 0.5f);
            }
            y -= rowHeight[l] + layerGap + sepHeight;
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
                    sep.anchoredPosition = new Vector2(0f, y + sepHeight * 0.5f);
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
                sepRect.anchoredPosition = new Vector2(0f, y + sepHeight * 0.5f);
                sepRect.sizeDelta = new Vector2(754f, sepHeight);
                createdSeparators.Add(sepObj);
            }
            y -= sepHeight;
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
        UpdateButtonCosts();
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
            TMP_Text cost = UIManager.FindChildComponent<TMP_Text>(removeArrowButton.transform, "Cost");
            if (cost != null)
                cost.text = RemoveArrowPrice + "$";
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
                if (!layer.isLastLayer && used + cost > layer.slotLimit)
                    continue;
                if (!TryGenerateLayerOffer(kvp.Key, target))
                    continue;
                used += cost;
                progressed = true;
            }
        }
    }

    private bool TryGenerateLayerOffer(ShopSlotEnum type, List<ShopOffer> target)
    {
        switch (type)
        {
            case ShopSlotEnum.Item:
                return TryAddMagicOffer(target);
            case ShopSlotEnum.Arrow:
                return TryAddMaterialOffer(target);
            default:
                return false;
        }
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
        return Mathf.Max(0, GetMagicBuyPrice(data) - 1);
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

    private bool TryAddMaterialOffer(List<ShopOffer> target)
    {
        if (strongMaterialOfferPool.Count > 0)
            return AddMaterialOfferFromPool(strongMaterialOfferPool, target);

        List<ShopMaterialOfferData> pool = ShouldUseWeakMaterialOffer() && weakMaterialOfferPool.Count > 0 ? weakMaterialOfferPool : normalMaterialOfferPool;
        return AddMaterialOfferFromPool(pool, target);
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
        target.Add(new ShopOffer { kind = ShopItemKind.Material, price = GetOfferPrice(offerData.price), material = offerData.material, materialModifierData = modifierData });
        return true;
    }

    private int GetOfferPrice(int price)
    {
        // 显式价格（含免费 0）直接生效；仅无效负价回落到箭头默认价。
        int basePrice = price >= 0 ? price : config.shopMaterialPrice;
        return DifficultyUpgradeSystem.ModifyShopPrice(basePrice);
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

        selectedOffer = offer;
        waitingForSelection = false;
        purchaseInProgress = false;
        owner.ClearPendingShopMagic();

        int targetSlot = GetMagicPlacementSlot();
        CompleteMagicPurchase(offer, targetSlot);
    }

    private int GetMagicPlacementSlot()
    {
        if (owner == null || owner.PlayerState == null)
            return 0;

        int count = owner.PlayerState.MagicBook.Count;
        int capacity = owner.MagicSlotCapacity;
        if (capacity > 0 && count >= capacity)
            count = capacity - 1;
        return Mathf.Max(0, count);
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
        return material != null && owner != null && owner.PlayerState != null && owner.PlayerState.Deck.Contains(material);
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
        RestoreUndoState(savedState.undo);

        if (waitingForSelection && selectedOffer != null && selectedOffer.kind == ShopItemKind.Magic && selectedOffer.magicData != null)
            owner.SelectPendingShopMagic(selectedOffer.magicData, slotIndex => CompleteMagicPurchase(selectedOffer, slotIndex));
        else if (!waitingForSelection)
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
        owner.GetUIManager().TutorialManager?.OnShopPurchaseCompleted();
    }

    private void RegisterUndoMaterialPurchase(ShopOffer offer, int goldBefore, MaterialModel addedMaterial)
    {
        ClearUndoPurchase();
        undoOffer = offer;
        undoGold = goldBefore;
        undoAddedMaterial = addedMaterial;
        undoAvailable = true;
        owner.GetUIManager().TutorialManager?.OnShopPurchaseCompleted();
    }

    private void RegisterUndoRemoveMaterialPurchase(ShopOffer offer, int goldBefore, MaterialModel removedMaterial)
    {
        ClearUndoPurchase();
        undoOffer = offer;
        undoGold = goldBefore;
        undoRemovedMaterial = removedMaterial;
        undoAvailable = true;
        owner.GetUIManager().TutorialManager?.OnShopPurchaseCompleted();
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
