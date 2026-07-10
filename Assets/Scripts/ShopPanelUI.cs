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
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text hintText;
    [SerializeField] private TMP_Text goldText;
    [SerializeField] private Button leaveButton;
    [SerializeField] private RectTransform revealMask;
    [SerializeField] private RectTransform contentRoot;
    [Header("开关动画")]
    [SerializeField] private float panelRevealDuration = 0.22f;
    [SerializeField] private Ease panelRevealEase = Ease.OutCubic;
    [SerializeField] private float panelHideDuration = 0.18f;
    [SerializeField] private Ease panelHideEase = Ease.InCubic;
    [SerializeField] private Vector2 panelOpenMoveOffset = new Vector2(-36f, 36f);
    [SerializeField] private Vector2 panelCloseMoveOffset = new Vector2(36f, -36f);
    [Header("商品槽弹出")]
    [SerializeField] private float itemPopDelayStep = 0.045f;
    [SerializeField] private float itemPopDuration = 0.28f;
    [SerializeField] private Ease itemPopEase = Ease.OutBack;

    private readonly List<ShopItemView> itemViews = new List<ShopItemView>();
    private readonly List<ShopOffer> offers = new List<ShopOffer>();
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
    private Vector2 panelSize;
    private bool hasPanelLayout;
    private Coroutine showRoutine;

    public RectTransform MagicViewPrefab => magicViewPrefab;
    public RectTransform MaterialCardPrefab => materialCardPrefab;

    public void Initialize(HandSystemUI owner)
    {
        this.owner = owner;
        CacheReferences();
        gameObject.SetActive(false);
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
        ClearUndoPurchase();
        owner.ClearPendingShopMagic();
        gameObject.SetActive(true);

        if (titleText != null)
            titleText.text = LocalizationSystem.GetText(level != null ? level.titleKey : string.Empty, LocalizationSystem.GetText("ui.shop.title", "商店"));
        if (hintText != null)
            hintText.text = LocalizationSystem.GetText("ui.shop.hint", "每件商品只能购买一次。道具购买后点击已有道具槽完成覆盖。");

        BuildOffers();
        if (savedState != null)
            RestoreState(savedState);
        BindLeaveButton();
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

    public void ShowMaterialTooltip(ShopItemView itemView, ShopOffer offer)
    {
        if (itemView == null || offer == null || offer.kind != ShopItemKind.Material)
            return;

        MaterialModel preview = new MaterialModel("shop_tooltip_" + offer.material, offer.material);
        MaterialModifierModel modifier = MaterialModifierFactory.Create(offer.materialModifierData);
        if (modifier != null)
            preview.AddModifier(modifier);
        owner.GetUIManager().MaterialListPanel?.ShowModifierTooltip(itemView.MaterialVisualRect != null ? itemView.MaterialVisualRect : itemView.transform as RectTransform, preview);
    }

    public void HideMaterialTooltip(ShopItemView itemView)
    {
        owner.GetUIManager().MaterialListPanel?.HideModifierTooltip(itemView != null ? (itemView.MaterialVisualRect != null ? itemView.MaterialVisualRect : itemView.transform as RectTransform) : null);
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
        if (materialCardPrefab == null)
        {
            PrefabReferenceLibrary library = GetComponentInParent<PrefabReferenceLibrary>();
            if (library != null)
                materialCardPrefab = library.MaterialCardPrefab;
        }

        CacheItemViews();
    }

    private void CacheItemViews()
    {
        itemViews.Clear();
        if (itemRoot == null)
            return;

        ShopItemView[] views = itemRoot.GetComponentsInChildren<ShopItemView>(true);
        for (int i = 0; i < views.Length; i++)
            itemViews.Add(views[i]);
        itemViews.Sort(CompareItemViewNames);
    }

    private static int CompareItemViewNames(ShopItemView left, ShopItemView right)
    {
        return string.CompareOrdinal(left != null ? left.name : string.Empty, right != null ? right.name : string.Empty);
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

    private void BuildOffers()
    {
        offers.Clear();
        productPool = GetShopProductPool();
        BuildMagicPool();
        BuildMaterialOfferPools();

        for (int i = 0; i < 3; i++)
            AddMagicOffer();
        AddStrongMaterialOffer();
        for (int i = 0; i < 3; i++)
            AddNormalMaterialOffer();
        offers.Add(new ShopOffer { kind = ShopItemKind.RemoveMaterial, price = GetOfferPrice(config.shopRemoveMaterialPrice) });
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

    private void AddMagicOffer()
    {
        if (magicPool.Count == 0)
            return;

        MagicData data = MagicRaritySystem.SelectWeightedMagic(magicPool, NextRunRandomInt);
        if (data == null)
            return;

        magicPool.Remove(data);
        offers.Add(new ShopOffer { kind = ShopItemKind.Magic, price = GetOfferPrice(config.shopSpellPrice + GetMagicRarityPriceOffset(data.rarity)), magicData = data });
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

    private void AddStrongMaterialOffer()
    {
        if (strongMaterialOfferPool.Count == 0)
            return;

        AddMaterialOfferFromPool(strongMaterialOfferPool);
    }

    private void AddNormalMaterialOffer()
    {
        List<ShopMaterialOfferData> pool = ShouldUseWeakMaterialOffer() && weakMaterialOfferPool.Count > 0 ? weakMaterialOfferPool : normalMaterialOfferPool;
        AddMaterialOfferFromPool(pool);
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

    private void AddMaterialOfferFromPool(List<ShopMaterialOfferData> pool)
    {
        if (pool.Count == 0)
            return;

        int index = NextRunRandomInt(0, pool.Count);
        ShopMaterialOfferData offerData = pool[index];
        pool.RemoveAt(index);
        MaterialModifierData modifierData = GetMaterialModifierDataById(offerData.modifierId);
        offers.Add(new ShopOffer { kind = ShopItemKind.Material, price = GetOfferPrice(offerData.price), material = offerData.material, materialModifierData = modifierData });
    }

    private int GetOfferPrice(int price)
    {
        int basePrice = price > 0 ? price : config.shopMaterialPrice;
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
        for (int i = 0; i < itemViews.Count; i++)
        {
            bool visible = i < offers.Count && !offers[i].purchased;
            itemViews[i].gameObject.SetActive(visible);
            if (!visible)
                continue;

            ShopOffer offer = offers[i];
            bool canAfford = owner.PlayerState != null && owner.PlayerState.Gold >= offer.price;
            bool selected = offer == selectedOffer;
            bool canUse = !purchaseInProgress && (!blockingSelection || selected) && CanUseOffer(offer);
            itemViews[i].Bind(this, offer, canAfford, canUse, selected, OnOfferClicked);
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
        PlayItemPopAnimations();
        showRoutine = null;
    }

    private void HideItemViewsForOpeningFrame()
    {
        for (int i = 0; i < itemViews.Count; i++)
        {
            if (itemViews[i] != null)
                itemViews[i].gameObject.SetActive(false);
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
        SetAnimationRectLayout();
        panelRect.anchoredPosition = panelOpenPosition + panelOpenMoveOffset;
        ApplyOpenReveal(0f);

        float duration = Mathf.Max(0f, panelRevealDuration);
        if (duration > 0f)
        {
            Sequence sequence = DOTween.Sequence().SetTarget(this);
            sequence.Join(panelRect.DOAnchorPos(panelOpenPosition, duration).SetEase(panelRevealEase));
            sequence.Join(DOVirtual.Float(0f, 1f, duration, ApplyOpenReveal).SetEase(panelRevealEase));
        }
        else
        {
            panelRect.anchoredPosition = panelOpenPosition;
            ApplyOpenReveal(1f);
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
        SetAnimationRectLayout();
        panelRect.anchoredPosition = panelOpenPosition;
        ApplyOpenReveal(1f);

        float duration = Mathf.Max(0f, panelHideDuration);
        if (duration > 0f)
        {
            Sequence sequence = DOTween.Sequence().SetTarget(this);
            sequence.Join(panelRect.DOAnchorPos(panelOpenPosition + panelCloseMoveOffset, duration).SetEase(panelHideEase));
            sequence.Join(DOVirtual.Float(0f, 1f, duration, ApplyCloseReveal).SetEase(panelHideEase));
            sequence.OnComplete(FinishCloseAnimation);
        }
        else
        {
            ApplyCloseReveal(1f);
            FinishCloseAnimation();
        }
    }

    private void PlayItemPopAnimations()
    {
        for (int i = 0; i < itemViews.Count; i++)
        {
            RectTransform itemRect = itemViews[i].transform as RectTransform;
            if (itemRect == null || !itemRect.gameObject.activeSelf)
                continue;

            Vector3 targetItemScale = itemRect.localScale;
            if (targetItemScale == Vector3.zero)
                targetItemScale = Vector3.one;

            itemRect.localScale = Vector3.zero;
            if (itemPopDuration > 0f)
            {
                itemRect.DOScale(targetItemScale, itemPopDuration)
                    .SetDelay(i * Mathf.Max(0f, itemPopDelayStep))
                    .SetEase(itemPopEase)
                    .SetTarget(this);
            }
            else
            {
                itemRect.localScale = targetItemScale;
            }
        }
    }

    private void FinishCloseAnimation()
    {
        gameObject.SetActive(false);

        RectTransform panelRect = transform as RectTransform;
        if (panelRect != null)
            panelRect.anchoredPosition = panelOpenPosition;
        ApplyOpenReveal(1f);
    }

    private void CapturePanelLayout(RectTransform panelRect)
    {
        if (hasPanelLayout)
            return;

        panelOpenPosition = panelRect.anchoredPosition;
        panelSize = panelRect.rect.size;
        if (panelSize.x <= 0f || panelSize.y <= 0f)
            panelSize = panelRect.sizeDelta;
        hasPanelLayout = true;
    }

    private void SetAnimationRectLayout()
    {
        if (revealMask == null)
            return;

        Image panelImage = GetComponent<Image>();
        if (panelImage != null)
            panelImage.enabled = false;

        revealMask.anchorMin = new Vector2(0f, 1f);
        revealMask.anchorMax = new Vector2(0f, 1f);
        revealMask.pivot = new Vector2(0f, 1f);
        revealMask.localScale = Vector3.one;
        revealMask.localRotation = Quaternion.identity;

        if (contentRoot == null)
            return;

        contentRoot.anchorMin = new Vector2(0f, 1f);
        contentRoot.anchorMax = new Vector2(0f, 1f);
        contentRoot.pivot = new Vector2(0f, 1f);
        contentRoot.sizeDelta = panelSize;
        contentRoot.localScale = Vector3.one;
        contentRoot.localRotation = Quaternion.identity;
    }

    private void ApplyOpenReveal(float progress)
    {
        progress = Mathf.Clamp01(progress);
        if (revealMask == null)
            return;

        revealMask.anchoredPosition = Vector2.zero;
        revealMask.sizeDelta = new Vector2(panelSize.x * progress, panelSize.y * progress);
        if (contentRoot != null)
        {
            contentRoot.anchoredPosition = Vector2.zero;
            contentRoot.sizeDelta = panelSize;
        }
    }

    private void ApplyCloseReveal(float progress)
    {
        progress = Mathf.Clamp01(progress);
        if (revealMask == null)
            return;

        Vector2 maskPosition = new Vector2(panelSize.x * progress, -panelSize.y * progress);
        revealMask.anchoredPosition = maskPosition;
        revealMask.sizeDelta = new Vector2(panelSize.x * (1f - progress), panelSize.y * (1f - progress));
        if (contentRoot != null)
        {
            contentRoot.anchoredPosition = -maskPosition;
            contentRoot.sizeDelta = panelSize;
        }
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
        waitingForSelection = true;
        purchaseInProgress = false;
        owner.SelectPendingShopMagic(offer.magicData, slotIndex => CompleteMagicPurchase(offer, slotIndex));
        Refresh();
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
        purchaseInProgress = true;
        Refresh();
        RectTransform sourceRect = GetMagicOfferRect(offer);
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
        purchaseInProgress = true;
        Refresh();
        RectTransform sourceRect = GetMaterialOfferRect(offer);
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
            string id = RunSaveSystem_GetMaterialModifierId(modifiers[i]);
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
            MaterialModifierData modifierData = !string.IsNullOrEmpty(data.modifierIds[i]) ? panelModifierDataLookup(data.modifierIds[i]) : null;
            MaterialModifierModel modifier = modifierData != null ? MaterialModifierFactory.Create(modifierData) : null;
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

    private static MaterialModifierData panelModifierDataLookup(string modifierId)
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

    private static string RunSaveSystem_GetMaterialModifierId(MaterialModifierModel modifier)
    {
        if (modifier is KindlingModifier) return "kindling";
        if (modifier is FlowModifier) return "flow";
        if (modifier is LiquefyModifier) return "liquefy";
        if (modifier is ChargeModifier) return "charge";
        if (modifier is VortexModifier) return "vortex";
        if (modifier is RepeatArrowModifier) return "repeat_arrow";
        if (modifier is OmniArrowModifier) return "omni_arrow";
        if (modifier is PeriodArrowModifier) return "period_arrow";
        if (modifier is PackArrowModifier) return "pack_arrow";
        if (modifier is LinkedArrowModifier) return "linked_arrow";
        if (modifier is BigArrow2Modifier) return "big_arrow_2";
        if (modifier is BigArrow3Modifier) return "big_arrow_3";
        if (modifier is BigArrow4Modifier) return "big_arrow_4";
        if (modifier is ReturnArrowModifier) return "return_arrow";
        if (modifier is RandomArrowModifier) return "random_arrow";
        if (modifier is ProliferatingArrowModifier) return "proliferating_arrow";
        if (modifier is EternalArrowModifier) return "eternal_arrow";
        if (modifier is FragileArrowModifier) return "fragile_arrow";
        if (modifier is RetainedArrowModifier) return "retained_arrow";
        if (modifier is HalfArrowModifier) return "half_arrow";
        if (modifier is DoomModifier) return "doom";
        if (modifier is LazyModifier) return "lazy";
        if (modifier is TemporaryModifier) return "temporary";
        return string.Empty;
    }

    private RectTransform GetMagicOfferRect(ShopOffer offer)
    {
        ShopItemView view = GetItemView(offer);
        return view != null ? view.MagicVisualRect : null;
    }

    private RectTransform GetMaterialOfferRect(ShopOffer offer)
    {
        ShopItemView view = GetItemView(offer);
        return view != null ? view.MaterialVisualRect : null;
    }

    private ShopItemView GetItemView(ShopOffer offer)
    {
        int index = offers.IndexOf(offer);
        return index >= 0 && index < itemViews.Count ? itemViews[index] : null;
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
