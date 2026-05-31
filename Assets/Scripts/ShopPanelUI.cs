using System;
using System.Collections.Generic;
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

    private readonly List<ShopItemView> itemViews = new List<ShopItemView>();
    private readonly List<ShopOffer> offers = new List<ShopOffer>();
    private readonly List<MagicData> magicPool = new List<MagicData>();
    private readonly List<MaterialEnum> materialPool = new List<MaterialEnum>();
    private HandSystemUI owner;
    private EconomyConfigData config;
    private bool waitingForSelection;

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
        waitingForSelection = false;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        if (titleText != null)
            titleText.text = LocalizationSystem.GetText(level != null ? level.titleKey : string.Empty, "商店");
        if (hintText != null)
            hintText.text = "每件商品只能购买一次。法术购买后选择一个法术槽覆盖。";
        if (messageText != null)
            messageText.text = string.Empty;

        BuildOffers();
        BindLeaveButton();
        Refresh();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void CacheReferences()
    {
        if (itemRoot == null)
            itemRoot = UIManager.FindChildRect(transform, "ItemRoot");
        if (titleText == null)
            titleText = UIManager.FindChildComponent<TMP_Text>(transform, "Title");
        if (hintText == null)
            hintText = UIManager.FindChildComponent<TMP_Text>(transform, "Hint");
        if (goldText == null)
            goldText = UIManager.FindChildComponent<TMP_Text>(transform, "GoldText");
        if (messageText == null)
            messageText = UIManager.FindChildComponent<TMP_Text>(transform, "MessageText");
        if (leaveButton == null)
            leaveButton = UIManager.FindChildComponent<Button>(transform, "LeaveButton");
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
        BuildMagicPool();
        BuildMaterialPool();

        for (int i = 0; i < 3; i++)
            AddMagicOffer();
        for (int i = 0; i < 2; i++)
            AddMaterialOffer();
        offers.Add(new ShopOffer { kind = ShopItemKind.RemoveMaterial, price = config.shopRemoveMaterialPrice });
    }

    private void BuildMagicPool()
    {
        magicPool.Clear();
        RewardPoolData rewardPool = null;
        if (config.shopMagicRewardPoolId > 0)
            GameDataDatabase.TryGetRewardPoolData(config.shopMagicRewardPoolId, out rewardPool);

        if (rewardPool != null && rewardPool.magicIds != null && rewardPool.magicIds.Length > 0)
        {
            for (int i = 0; i < rewardPool.magicIds.Length; i++)
            {
                if (GameDataDatabase.TryGetMagicData(rewardPool.magicIds[i], out MagicData data) && !magicPool.Contains(data))
                    magicPool.Add(data);
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

    private void BuildMaterialPool()
    {
        materialPool.Clear();
        if (config.shopMaterialPool != null && config.shopMaterialPool.Length > 0)
        {
            for (int i = 0; i < config.shopMaterialPool.Length; i++)
            {
                MaterialEnum material = config.shopMaterialPool[i];
                if (material != MaterialEnum.None && !materialPool.Contains(material))
                    materialPool.Add(material);
            }
        }

        if (materialPool.Count == 0)
        {
            materialPool.Add(MaterialEnum.Fire);
            materialPool.Add(MaterialEnum.Wind);
            materialPool.Add(MaterialEnum.Water);
            materialPool.Add(MaterialEnum.Earth);
        }
    }

    private void AddMagicOffer()
    {
        if (magicPool.Count == 0)
            return;

        int index = UnityEngine.Random.Range(0, magicPool.Count);
        MagicData data = magicPool[index];
        magicPool.RemoveAt(index);
        offers.Add(new ShopOffer { kind = ShopItemKind.Magic, price = config.shopSpellPrice, magicData = data });
    }

    private void AddMaterialOffer()
    {
        if (materialPool.Count == 0)
            return;

        int index = UnityEngine.Random.Range(0, materialPool.Count);
        MaterialEnum material = materialPool[index];
        materialPool.RemoveAt(index);
        offers.Add(new ShopOffer { kind = ShopItemKind.Material, price = config.shopMaterialPrice, material = material });
    }

    private void Refresh()
    {
        CacheReferences();
        if (goldText != null && owner.PlayerState != null)
            goldText.text = "金币：" + owner.PlayerState.Gold;
        if (leaveButton != null)
            leaveButton.interactable = !waitingForSelection;

        for (int i = 0; i < itemViews.Count; i++)
        {
            bool visible = i < offers.Count;
            itemViews[i].gameObject.SetActive(visible);
            if (!visible)
                continue;

            ShopOffer offer = offers[i];
            bool canAfford = owner.PlayerState != null && owner.PlayerState.Gold >= offer.price;
            bool canUse = !waitingForSelection && (offer.kind != ShopItemKind.RemoveMaterial || HasRemovableMaterial());
            itemViews[i].Bind(this, offer, canAfford, canUse, OnOfferClicked);
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

        ShowMessage("选择一个法术槽完成购买");
        waitingForSelection = true;
        Refresh();
        owner.GetUIManager().ShowSlotSelect(offer.magicData, slotIndex => CompleteMagicPurchase(offer, slotIndex));
    }

    private void CompleteMagicPurchase(ShopOffer offer, int slotIndex)
    {
        waitingForSelection = false;
        if (offer == null || offer.purchased || offer.magicData == null)
        {
            Refresh();
            return;
        }
        if (!owner.TrySpendShopGold(offer.price))
        {
            ShowMessage("金币不足");
            Refresh();
            return;
        }

        owner.SetShopMagicAtSlot(offer.magicData, slotIndex);
        offer.purchased = true;
        ShowMessage("购买成功");
        Refresh();
    }

    private void CompleteMaterialPurchase(ShopOffer offer)
    {
        if (!owner.TrySpendShopGold(offer.price))
        {
            ShowMessage("金币不足");
            Refresh();
            return;
        }

        owner.AddShopMaterial(offer.material);
        offer.purchased = true;
        ShowMessage("购买成功");
        Refresh();
    }

    private void BeginRemoveMaterialPurchase(ShopOffer offer)
    {
        if (!HasRemovableMaterial())
        {
            ShowMessage("没有可删除的素材");
            Refresh();
            return;
        }

        ShowMessage("选择一张素材删除");
        waitingForSelection = true;
        Refresh();
        MaterialListPanelUI materialListPanel = owner.GetUIManager().MaterialListPanel;
        materialListPanel?.BeginSelection(1, IsRemovableMaterial, selected => CompleteRemoveMaterialPurchase(offer, selected));
        RectTransform materialRect = materialListPanel != null ? materialListPanel.transform as RectTransform : null;
        if (materialRect != null)
            PopupLayerUtility.ApplyTo(materialRect);
    }

    private bool IsRemovableMaterial(MaterialModel material)
    {
        return material != null && owner != null && owner.PlayerState != null && owner.PlayerState.Deck.Contains(material);
    }

    private void CompleteRemoveMaterialPurchase(ShopOffer offer, IReadOnlyList<MaterialModel> selected)
    {
        waitingForSelection = false;
        if (offer == null || offer.purchased || selected == null || selected.Count == 0)
        {
            Refresh();
            return;
        }
        if (!owner.TrySpendShopGold(offer.price))
        {
            ShowMessage("金币不足");
            Refresh();
            return;
        }

        if (owner.RemoveShopMaterial(selected[0]))
        {
            offer.purchased = true;
            ShowMessage("已删除素材");
        }
        Refresh();
    }

    private void ShowMessage(string text)
    {
        if (messageText != null)
            messageText.text = text;
    }
}
