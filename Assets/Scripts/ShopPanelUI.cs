using System;
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
}

public class ShopPanelUI : MonoBehaviour
{
    [SerializeField] private RectTransform itemRoot;
    [SerializeField] private RectTransform magicViewPrefab;
    [SerializeField] private RectTransform materialCardPrefab;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text hintText;
    [SerializeField] private TMP_Text goldText;
    [SerializeField] private TMP_Text messageText;
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
    private Vector2 panelOpenPosition;
    private Vector2 panelSize;
    private bool hasPanelLayout;

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
        if (owner == null)
            return;

        CacheReferences();
        config = GameDataDatabase.GetDefaultEconomyConfig() ?? new EconomyConfigData();
        selectedOffer = null;
        waitingForSelection = false;
        purchaseInProgress = false;
        owner.ClearPendingShopMagic();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        if (titleText != null)
            titleText.text = LocalizationSystem.GetText(level != null ? level.titleKey : string.Empty, "商店");
        if (hintText != null)
            hintText.text = "每件商品只能购买一次。道具购买后点击已有道具槽完成覆盖。";
        if (messageText != null)
            messageText.text = string.Empty;

        BuildOffers();
        BindLeaveButton();
        Refresh();
        PlayOpenAnimation();
    }

    public void Hide()
    {
        owner?.ClearPendingShopMagic();
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
        if (messageText == null)
            messageText = FindChildComponentRecursive<TMP_Text>(searchRoot, "MessageText");
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
        leaveButton.onClick.AddListener(owner.FinishReward);
        TMP_Text text = UIManager.FindChildComponent<TMP_Text>(leaveButton.transform, "Text");
        if (text != null)
            text.text = "离开";
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
        offers.Add(new ShopOffer { kind = ShopItemKind.RemoveMaterial, price = config.shopRemoveMaterialPrice });
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
                if (data != null)
                    magicPool.Add(data);
            }
        }
    }

    private void AddMagicPoolData(int magicId)
    {
        if (GameDataDatabase.TryGetMagicData(magicId, out MagicData data) && data != null && !magicPool.Contains(data))
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
        return data != null && !string.IsNullOrEmpty(data.script) && MaterialModifierFactory.Create(data) != null;
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

        int index = NextRunRandomInt(0, magicPool.Count);
        MagicData data = magicPool[index];
        magicPool.RemoveAt(index);
        offers.Add(new ShopOffer { kind = ShopItemKind.Magic, price = config.shopSpellPrice, magicData = data });
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
        return price > 0 ? price : config.shopMaterialPrice;
    }

    private int NextRunRandomInt(int minInclusive, int maxExclusive)
    {
        return owner != null && owner.RunManager != null ? owner.RunManager.NextRandomInt(minInclusive, maxExclusive) : UnityEngine.Random.Range(minInclusive, maxExclusive);
    }

    private void Refresh()
    {
        CacheReferences();
        if (goldText != null)
            goldText.gameObject.SetActive(false);
        if (leaveButton != null)
            leaveButton.interactable = !waitingForSelection && !purchaseInProgress;

        for (int i = 0; i < itemViews.Count; i++)
        {
            bool visible = i < offers.Count;
            itemViews[i].gameObject.SetActive(visible);
            if (!visible)
                continue;

            ShopOffer offer = offers[i];
            bool canAfford = owner.PlayerState != null && owner.PlayerState.Gold >= offer.price;
            bool selected = offer == selectedOffer;
            bool canUse = !purchaseInProgress && (!waitingForSelection || selected) && CanUseOffer(offer);
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

        PlayItemPopAnimations();
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

    private void OnOfferClicked(ShopOffer offer)
    {
        if (offer == null || offer.purchased || owner == null || owner.PlayerState == null)
            return;

        if (owner.PlayerState.Gold < offer.price)
        {
            PlayShopSfx(GameSfxId.NotEnoughMoney);
            ShowMessage("金币不足");
            Refresh();
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

        ShowMessage("点击道具槽完成购买");
        selectedOffer = offer;
        waitingForSelection = true;
        purchaseInProgress = false;
        owner.SelectPendingShopMagic(offer.magicData, slotIndex => CompleteMagicPurchase(offer, slotIndex));
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
        if (!owner.TrySpendShopGold(offer.price))
        {
            selectedOffer = null;
            PlayShopSfx(GameSfxId.NotEnoughMoney);
            ShowMessage("金币不足");
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
            ShowMessage("购买成功");
            Refresh();
        });
    }

    private void CompleteMaterialPurchase(ShopOffer offer)
    {
        if (!owner.TrySpendShopGold(offer.price))
        {
            PlayShopSfx(GameSfxId.NotEnoughMoney);
            ShowMessage("金币不足");
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
            ShowMessage("购买成功");
            Refresh();
        });
    }

    private void BeginRemoveMaterialPurchase(ShopOffer offer)
    {
        if (!HasRemovableMaterial())
        {
            ShowMessage("没有可删除的素材");
            Refresh();
            return;
        }

        selectedOffer = offer;
        owner.ClearPendingShopMagic();
        ShowMessage("选择一张箭头删除");
        waitingForSelection = true;
        Refresh();
        MaterialListPanelUI materialListPanel = owner.GetUIManager().MaterialListPanel;
        materialListPanel?.BeginSelection(1, IsRemovableMaterial, selected => CompleteRemoveMaterialPurchase(offer, selected), CancelSelectionPurchase, "选择要删的牌");
        RectTransform materialRect = materialListPanel != null ? materialListPanel.transform as RectTransform : null;
        if (materialRect != null)
            PopupLayerUtility.ApplyTo(materialRect);
    }

    private void CancelSelectionPurchase()
    {
        waitingForSelection = false;
        selectedOffer = null;
        ShowMessage(string.Empty);
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
        if (!owner.TrySpendShopGold(offer.price))
        {
            PlayShopSfx(GameSfxId.NotEnoughMoney);
            ShowMessage("金币不足");
            Refresh();
            return;
        }

        if (owner.RemoveShopMaterial(selected[0]))
        {
            PlayShopSfx(GameSfxId.Buy);
            offer.purchased = true;
            ShowMessage("已删除素材");
        }
        Refresh();
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

    private void ShowMessage(string text)
    {
        if (messageText != null)
            messageText.text = text;
    }
}
