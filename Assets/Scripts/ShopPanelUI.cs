using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

#if !MATERIALMAGIC_STANDALONE
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "ShopPanelUI")]
#endif
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
    private readonly List<ShopLayer> shopLayers = new List<ShopLayer>();
    private readonly List<GameObject> createdSeparators = new List<GameObject>();
    private static readonly List<ShopOffer> emptyOffers = new List<ShopOffer>();
    private static readonly List<List<ShopOffer>> emptyLayerOffers = new List<List<ShopOffer>>();
    private HandSystemUI owner;
    private ShopSession session;
    private IReadOnlyList<ShopOffer> offers => session != null ? session.Offers : emptyOffers;
    private IReadOnlyList<List<ShopOffer>> layerOffers => session != null ? session.LayerOffers : emptyLayerOffers;
    private ShopOffer selectedOffer => session?.SelectedOffer;
    private bool waitingForSelection => session != null && session.WaitingForSelection;
    // Presentation lock only: all inventory and undo changes precede animation.
    private bool purchaseInProgress;
    private Vector2 panelOpenPosition;
    private Vector3 panelBaseScale;
    private bool hasPanelLayout;
    private Coroutine showRoutine;
    private int refreshCount => session != null ? session.RefreshCount : 0;
    private bool refreshInProgress;
    private bool removeArrowUsed => session != null && session.RemoveArrowUsed;
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

    public ShopSession Session => session;
    public int RemoveArrowPrice => session != null ? session.RemoveArrowPrice : 0;
    public int RefreshCost => session != null ? session.RefreshCost : 0;

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
        if (owner == null || session == null || refreshInProgress || purchaseInProgress || waitingForSelection)
            return;
        int[] previousLayerCounts = new int[layerOffers.Count];
        for (int i = 0; i < previousLayerCounts.Length; i++)
            previousLayerCounts[i] = layerOffers[i].Count;
        if (owner.PlayerState.Gold < session.RefreshCost)
        {
            PlayShopSfx(GameSfxId.NotEnoughMoney);
            return;
        }
        if (!session.TryRefresh(out int cost, owner.TrySaveRunProgressForce))
        {
            Refresh();
            return;
        }
        EmitShopRefresh(cost);
        refreshInProgress = true;
        if (leaveButton != null) leaveButton.interactable = false;
        if (refreshButton != null) refreshButton.interactable = false;
        UpdateRemoveArrowButtonState();
        owner.RefreshShopUndoUI();
        StartCoroutine(RefreshRoutine(previousLayerCounts));
    }

    /// <summary>
    /// 删除箭头选项的可用状态：每次刷新商店、或重新进入商店时都恢复为可用；
    /// 同一次商店里用过一次后保持置灰（连同隐藏价格文本），直到刷新或换到下一次商店。
    /// </summary>
    private void UpdateRemoveArrowButtonState()
    {
        if (removeArrowButton == null)
            return;

        removeArrowButton.interactable = !removeArrowUsed && !purchaseInProgress && !refreshInProgress && !waitingForSelection;
        if (removeArrowCostText == null)
            removeArrowCostText = UIManager.FindChildComponent<TMP_Text>(removeArrowButton.transform, "Cost");
        if (removeArrowCostText != null)
            removeArrowCostText.gameObject.SetActive(!removeArrowUsed);
    }

    private System.Collections.IEnumerator RefreshRoutine(int[] previousLayerCounts)
    {
        yield return AnimateSlotsDisappearRoutine(previousLayerCounts);
        owner.ClearPendingShopMagic();
        BuildLayerViews();
        refreshInProgress = false;
        Refresh();
        AnimateSlotsAppear();
        UpdateButtonCosts();
    }

    public void BeginRemoveArrowPurchase()
    {
        if (session == null || purchaseInProgress || refreshInProgress || waitingForSelection || !session.BeginRemovalSelection())
            return;
        BeginRemoveArrowSelection();
    }

    private void BeginRemoveArrowSelection()
    {
        owner.ClearPendingShopMagic();
        Refresh();
        MaterialListPanelUI materialListPanel = owner.GetUIManager().MaterialSelectionPanel;
        materialListPanel?.BeginSelection(1, IsRemovableMaterial, selected => CompleteRemoveArrowSelection(selected), CancelSelectionPurchase, LocalizationSystem.GetText("ui.shop.remove_material.title", "选择要删的牌"));
        RectTransform materialRect = materialListPanel != null ? materialListPanel.transform as RectTransform : null;
        if (materialRect != null)
            PopupLayerUtility.ApplyTo(materialRect);
    }

    private void CompleteRemoveArrowSelection(IReadOnlyList<MaterialModel> selected)
    {
        session?.CancelSelection();
        if (selected != null && selected.Count > 0 && session != null)
        {
            if (session.TryRemoveMaterial(selected[0], out ShopTransaction transaction, out ShopTransactionFailure failure))
            {
                PlayShopSfx(GameSfxId.Buy);
                owner.NotifyShopTransactionCommitted(null, -1, null, false);
            }
            else if (failure == ShopTransactionFailure.NotEnoughGold)
                PlayShopSfx(GameSfxId.NotEnoughMoney);
        }
        Refresh();
    }

    public void Show(LevelData level)
    {
        Show(level, null);
    }

    public void Show(LevelData level, ShopNodeSaveData savedState)
    {
        if (owner == null || owner.PlayerState == null)
            return;

        CacheReferences();
        EconomyConfigData config = GameDataDatabase.GetDefaultEconomyConfig() ?? new EconomyConfigData();
        session = new ShopSession(owner.PlayerState, config, owner.MagicSlotCapacity,
            new ShopGenerationServices { NextRandomInt = NextRunRandomInt });
        session.Open(shopLayers, savedState, owner.RunManager?.GetShopSeed(level) ?? 0);
        owner.PlayerState.CurrentShop = session;
        shopLayers.Clear();
        for (int i = 0; i < session.Layers.Count; i++)
            shopLayers.Add(session.Layers[i]);
        purchaseInProgress = false;
        refreshInProgress = false;
        owner.ClearPendingShopMagic();
        gameObject.SetActive(true);

        if (titleText != null)
            titleText.text = LocalizationSystem.GetText(level != null ? level.titleKey : string.Empty, LocalizationSystem.GetText("ui.shop.title", "商店"));
        if (hintText != null)
            hintText.text = LocalizationSystem.GetText("ui.shop.hint", "每件商品只能购买一次。道具栏已满时需先卖出道具才能购买道具。");

        BuildLayerViews();
        RunAnalyticsLedger.BeginShop(owner.PlayerState != null ? owner.PlayerState.Gold : 0);
        EmitShopEnter();
        BindActionButtons();
        StartShowRoutine();
    }

    public void Hide()
    {
        StopShowRoutine();
        owner?.ClearPendingShopMagic();
        ClearUndoPurchase();
        session?.CancelSelection();
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

    /// <summary>
    /// 商店里“材料/附魔”商品的详情内容：与槽位里的预览一致（带上附魔），供 UnifiedDetailTriggerUI 的 Provider 使用；
    /// 非材料商品返回空内容（道具走自己的详情来源）。
    /// </summary>
    public UnifiedDetailContent BuildMaterialTooltipContent(ShopOffer offer)
    {
        if (offer == null || offer.kind != ShopItemKind.Material)
            return default;

        MaterialModel preview = new MaterialModel("shop_tooltip_" + offer.material, offer.material);
        MaterialModifierModel modifier = MaterialModifierFactory.Create(offer.materialModifierData);
        if (modifier != null)
            preview.AddModifier(modifier);
        return UnifiedDetailContentBuilder.Build(preview);
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
        if (button == null || contentProvider == null)
            return;

        // 旧实现用 EventTrigger 自己接 enter/exit；现在统一交给 UnifiedDetailTriggerUI
        // （PC 悬停显示详情，PE 长按看详情、短按放行按钮自身的点击动作）。
        EventTrigger legacyTrigger = button.GetComponent<EventTrigger>();
        if (legacyTrigger != null)
            legacyTrigger.triggers.Clear();

        UnifiedDetailTriggerUI trigger = button.GetComponent<UnifiedDetailTriggerUI>();
        if (trigger == null)
            trigger = button.gameObject.AddComponent<UnifiedDetailTriggerUI>();

        trigger.SetAnchor(button);
        trigger.SetShortPressBehavior(UnifiedDetailTriggerUI.ShortPressBehavior.ClickThrough);
        trigger.SetContentProvider(contentProvider);
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


    public static int GetMagicBuyPrice(MagicData data) => ShopOfferGenerator.GetMagicBuyPrice(data);

    public static int GetMagicSellPrice(MagicData data) => ShopOfferGenerator.GetMagicSellPrice(data);

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
        if (session != null && owner != null)
            session.MagicSlotCapacity = owner.MagicSlotCapacity;
        if (leaveButton != null)
            leaveButton.interactable = !purchaseInProgress && !refreshInProgress && !waitingForSelection;
        if (refreshButton != null)
            refreshButton.interactable = !purchaseInProgress && !refreshInProgress && !waitingForSelection;
        UpdateRemoveArrowButtonState();

        bool blockingSelection = waitingForSelection;
        for (int i = 0; i < slotViews.Count; i++)
        {
            // 已购商品保留占位（保持 active、显示空），使 HLG 不因隐藏已购格而重排其它商品。
            bool visible = i < offers.Count;
            slotViews[i].gameObject.SetActive(visible);
            if (!visible)
                continue;

            ShopOffer offer = offers[i];
            bool canAfford = session != null && session.CanAfford(offer);
            bool selected = offer == selectedOffer;
            bool canUse = !purchaseInProgress && !refreshInProgress && (!blockingSelection || selected) && CanUseOffer(offer);
            slotViews[i].Bind(this, offer, canAfford, canUse, selected, OnOfferClicked);
        }
    }

    private bool CanUseOffer(ShopOffer offer) => session != null && session.CanUseOffer(offer);

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
        // Selection windows are presentation, so reconstruct them from the persisted rule state.
        if (waitingForSelection)
        {
            ShopOffer pendingRemoval = selectedOffer;
            session.CancelSelection();
            if (pendingRemoval != null && pendingRemoval.kind == ShopItemKind.RemoveMaterial)
                BeginRemoveMaterialPurchase(pendingRemoval);
            else if (pendingRemoval == null)
                BeginRemoveArrowPurchase();
            else
                Refresh();
        }
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

    private System.Collections.IEnumerator AnimateSlotsDisappearRoutine(int[] previousLayerCounts)
    {
        int viewIndex = 0;
        int total = 0;
        for (int l = 0; l < previousLayerCounts.Length; l++)
        {
            int count = previousLayerCounts[l];
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
        for (int l = 0; l < previousLayerCounts.Length; l++)
        {
            int count = previousLayerCounts[l];
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


    private void LeaveShop()
    {
        if (purchaseInProgress || refreshInProgress || waitingForSelection)
            return;
        session?.CancelSelection();
        ClearUndoPurchase();
        EmitShopLeave();
        owner.FinishReward();
    }

    private void OnOfferClicked(ShopOffer offer)
    {
        if (offer == null || offer.purchased || owner == null || owner.PlayerState == null)
            return;

        if (purchaseInProgress || refreshInProgress || waitingForSelection || session == null)
            return;

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
        owner.ClearPendingShopMagic();
        RectTransform sourceRect = GetMagicOfferRect(offer);
        if (!TryCommitPurchase(offer, null, out ShopTransaction transaction))
            return;
        purchaseInProgress = true;
        Refresh();
        RunAnalyticsLedger.SetAcquireContext(RunAnalyticsLedger.IsBattleShop ? "battle_shop" : "shop", offer.price, offer.magicData.id, 0);
        EmitShopPurchase("magic", offer, transaction.GoldBefore, transaction.PreviousMagic, transaction.MagicSlotIndex, null);
        RunAnalyticsLedger.RecordShopPurchase(offer.price);
        owner.NotifyShopTransactionCommitted(offer.magicData, transaction.MagicSlotIndex, transaction.PreviousMagic, true);
        owner.PlayCommittedShopMagicAcquisition(offer.magicData, transaction.MagicSlotIndex, transaction.ResultingMagicCount, sourceRect, () =>
        {
            purchaseInProgress = false;
            Refresh();
        });
    }

    private void CompleteMaterialPurchase(ShopOffer offer)
    {
        RectTransform sourceRect = GetMaterialOfferRect(offer);
        if (!TryCommitPurchase(offer, null, out ShopTransaction transaction))
            return;
        purchaseInProgress = true;
        Refresh();
        EmitShopPurchase("material", offer, transaction.GoldBefore, null, -1, offer.material.ToString());
        RunAnalyticsLedger.RecordShopPurchase(offer.price);
        owner.NotifyShopTransactionCommitted(null, -1, null, false);
        owner.PlayCommittedShopMaterialAcquisition(offer.material, sourceRect, () =>
        {
            purchaseInProgress = false;
            Refresh();
        });
    }

    private bool TryCommitPurchase(ShopOffer offer, MaterialModel removalTarget, out ShopTransaction transaction)
    {
        transaction = null;
        if (session == null)
            return false;
        session.MagicSlotCapacity = owner.MagicSlotCapacity;
        if (!session.TryPurchase(offer, out transaction, out ShopTransactionFailure failure, removalTarget))
        {
            if (failure == ShopTransactionFailure.NotEnoughGold)
                PlayShopSfx(GameSfxId.NotEnoughMoney);
            Refresh();
            return false;
        }
        PlayShopSfx(GameSfxId.Buy);
        return true;
    }

    private void BeginRemoveMaterialPurchase(ShopOffer offer)
    {
        if (session == null || !session.BeginRemovalSelection(offer))
        {
            Refresh();
            return;
        }

        owner.ClearPendingShopMagic();
        Refresh();
        MaterialListPanelUI materialListPanel = owner.GetUIManager().MaterialSelectionPanel;
        materialListPanel?.BeginSelection(1, IsRemovableMaterial, selected => CompleteRemoveMaterialPurchase(offer, selected), CancelSelectionPurchase, LocalizationSystem.GetText("ui.shop.remove_material.title", "选择要删的牌"));
        RectTransform materialRect = materialListPanel != null ? materialListPanel.transform as RectTransform : null;
        if (materialRect != null)
            PopupLayerUtility.ApplyTo(materialRect);
    }

    private void CancelSelectionPurchase()
    {
        session?.CancelSelection();
        Refresh();
    }

    private bool IsRemovableMaterial(MaterialModel material)
    {
        return session != null && session.IsRemovableMaterial(material);
    }

    private void CompleteRemoveMaterialPurchase(ShopOffer offer, IReadOnlyList<MaterialModel> selected)
    {
        session?.CancelSelection();
        if (selected != null && selected.Count > 0 && TryCommitPurchase(offer, selected[0], out ShopTransaction transaction))
        {
            EmitShopPurchase("remove_material", offer, transaction.GoldBefore, null, -1, selected[0].material.ToString());
            RunAnalyticsLedger.RecordShopPurchase(offer.price);
            owner.NotifyShopTransactionCommitted(null, -1, null, false);
        }
        Refresh();
    }

    public static bool TryExportCurrentState(PlayerState player, out ShopNodeSaveData data)
    {
        data = player?.CurrentShop?.ExportState();
        return data != null;
    }

    public ShopNodeSaveData ExportState() => session?.ExportState();


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
        int index = session != null ? session.IndexOfOffer(offer) : -1;
        return index >= 0 && index < slotViews.Count ? slotViews[index] : null;
    }

    private static void PlayShopSfx(GameSfxId id)
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySfx(id);
    }

    public bool TryUndoLastPurchase()
    {
        if (session == null || owner == null || purchaseInProgress || refreshInProgress || waitingForSelection)
            return false;
        int goldBefore = owner.PlayerState.Gold;
        if (!session.TryUndo(out ShopTransaction transaction))
            return false;
        EmitShopUndoPurchase(transaction, goldBefore);
        owner.CreateMagicViewsForShopUndo();
        owner.RefreshShopUndoUI();
        owner.SaveRunProgressForce();
        Refresh();
        return true;
    }

    // ── 埋点（batch 1）：商店相关事件 ──

    /// <summary>进入商店（shop_enter）。</summary>
    private void EmitShopEnter()
    {
        Dictionary<string, object> payload = RunAnalyticsLedger.Context();
        payload[AnalyticsProperty.IsBattleShop] = RunAnalyticsLedger.IsBattleShop;
        payload[AnalyticsProperty.OfferCount] = offers != null ? offers.Count : 0;
        if (offers != null && offers.Count > 0)
        {
            List<string> kinds = new List<string>();
            List<string> ids = new List<string>();
            for (int i = 0; i < offers.Count; i++)
            {
                ShopOffer offer = offers[i];
                if (offer == null)
                    continue;
                kinds.Add(offer.kind.ToString());
                if (offer.magicData != null)
                    ids.Add(offer.magicData.id);
                else
                    ids.Add(offer.kind.ToString());
            }
            payload[AnalyticsProperty.OfferKinds] = AnalyticsService.JoinList(kinds);
            payload[AnalyticsProperty.OfferIds] = AnalyticsService.JoinList(ids);
        }
        if (owner != null && owner.PlayerState != null)
            payload[AnalyticsProperty.Gold] = owner.PlayerState.Gold;
        AnalyticsService.Track(AnalyticsEvent.ShopEnter, payload);
    }

    /// <summary>刷新商店（shop_refresh）；调用时金币已扣。 </summary>
    private void EmitShopRefresh(int cost)
    {
        Dictionary<string, object> payload = RunAnalyticsLedger.Context();
        payload[AnalyticsProperty.RefreshIndex] = refreshCount;
        payload[AnalyticsProperty.Price] = cost;
        if (owner != null && owner.PlayerState != null)
        {
            payload[AnalyticsProperty.GoldBefore] = owner.PlayerState.Gold + cost;
            payload[AnalyticsProperty.GoldAfter] = owner.PlayerState.Gold;
        }
        AnalyticsService.Track(AnalyticsEvent.ShopRefresh, payload);
    }

    /// <summary>商店购买（shop_purchase）。道具类的 magic_acquire 由 HandSystemUI 在完整提交后发。</summary>
    private void EmitShopPurchase(string kind, ShopOffer offer, int goldBefore, MagicModel replacedMagic, int slotIndex, string materialName)
    {
        Dictionary<string, object> payload = RunAnalyticsLedger.Context();
        payload[AnalyticsProperty.Kind] = kind;
        payload[AnalyticsProperty.Price] = offer != null ? offer.price : 0;
        payload[AnalyticsProperty.GoldBefore] = goldBefore;
        payload[AnalyticsProperty.GoldAfter] = owner != null && owner.PlayerState != null ? owner.PlayerState.Gold : goldBefore;
        payload[AnalyticsProperty.RefreshIndex] = refreshCount;
        payload[AnalyticsProperty.IsUndo] = false;
        if (offer != null && offer.magicData != null)
        {
            payload[AnalyticsProperty.MagicId] = offer.magicData.id;
            payload[AnalyticsProperty.Rarity] = (int)offer.magicData.rarity;
        }
        if (offer != null && offer.materialModifierData != null)
            payload[AnalyticsProperty.ModifierId] = offer.materialModifierData.id;
        if (!string.IsNullOrEmpty(materialName))
            payload[AnalyticsProperty.Material] = materialName;
        if (slotIndex >= 0)
            payload[AnalyticsProperty.SlotIndex] = slotIndex;
        if (replacedMagic != null)
            payload[AnalyticsProperty.ReplacedMagicId] = replacedMagic.Id;
        AnalyticsService.Track(AnalyticsEvent.ShopPurchase, payload);
    }

    /// <summary>撤回一次购买（shop_purchase + is_undo=true）。</summary>
    private void EmitShopUndoPurchase(ShopTransaction transaction, int goldBefore)
    {
        string kind = transaction.MagicSlotIndex >= 0 ? "magic" : (transaction.AddedMaterial != null ? "material" : (transaction.RemovedMaterial != null ? "remove_material" : "unknown"));
        Dictionary<string, object> payload = RunAnalyticsLedger.Context();
        payload[AnalyticsProperty.Kind] = kind;
        payload[AnalyticsProperty.IsUndo] = true;
        payload[AnalyticsProperty.GoldBefore] = goldBefore;
        payload[AnalyticsProperty.GoldAfter] = transaction.GoldBefore;
        if (transaction.Offer != null && transaction.Offer.magicData != null)
        {
            payload[AnalyticsProperty.MagicId] = transaction.Offer.magicData.id;
            payload[AnalyticsProperty.Rarity] = (int)transaction.Offer.magicData.rarity;
        }
        AnalyticsService.Track(AnalyticsEvent.ShopPurchase, payload);
    }

    /// <summary>离开商店（shop_leave），汇总本次商店花费与停留时长。</summary>
    private void EmitShopLeave()
    {
        int totalSpent, purchaseCount, seconds;
        RunAnalyticsLedger.EndShop(out totalSpent, out purchaseCount, out seconds);
        Dictionary<string, object> payload = RunAnalyticsLedger.Context();
        payload[AnalyticsProperty.TotalSpent] = totalSpent;
        payload[AnalyticsProperty.PurchaseCount] = purchaseCount;
        payload[AnalyticsProperty.GoldLeft] = owner != null && owner.PlayerState != null ? owner.PlayerState.Gold : 0;
        payload[AnalyticsProperty.ShopSeconds] = seconds;
        AnalyticsService.Track(AnalyticsEvent.ShopLeave, payload);
    }

    private void ClearUndoPurchase() => session?.ClearUndo();
}
