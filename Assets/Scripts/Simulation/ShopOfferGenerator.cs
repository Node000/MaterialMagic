using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Explicit external inputs. Defaults preserve the production database/unlock/price behavior.</summary>
public sealed class ShopGenerationServices
{
    public Func<int, int, int> NextRandomInt = (min, max) => ChoiceRuleRandom.Next(null, min, max);
    public Func<MagicData, bool> IsMagicUnlocked = UnlockSystem.IsMagicUnlocked;
    public Func<MaterialModifierData, bool> IsModifierUnlocked = UnlockSystem.IsMaterialModifierUnlocked;
    public Func<int, MagicData> FindMagic = DefaultFindMagic;
    public Func<int, RewardPoolData> FindRewardPool = DefaultFindRewardPool;
    public Func<int, ShopProductPoolData> FindProductPool = DefaultFindProductPool;
    public Func<IEnumerable<MagicData>> AllMagic = () => GameDataDatabase.MagicData.Values;
    public Func<IEnumerable<ShopProductPoolData>> AllProductPools = () => GameDataDatabase.ShopProductPoolData.Values;
    public Func<string, MaterialModifierData> FindModifier = DefaultFindModifier;
    public Func<string, int> ModifierPriceDelta = DefaultModifierPriceDelta;
    public Func<int, int> ModifyPrice = DifficultyUpgradeSystem.ModifyShopPrice;
    // Production preserves the old current-economy lookup; tests may quote exclusively from fixture data.
    public Func<MagicData, int> QuoteMagic = data => ShopOfferGenerator.GetMagicBuyPrice(data);

    private static MagicData DefaultFindMagic(int id) => GameDataDatabase.TryGetMagicData(id, out MagicData data) ? data : null;
    private static RewardPoolData DefaultFindRewardPool(int id) => GameDataDatabase.TryGetRewardPoolData(id, out RewardPoolData data) ? data : null;
    private static ShopProductPoolData DefaultFindProductPool(int id) => GameDataDatabase.TryGetShopProductPoolData(id, out ShopProductPoolData data) ? data : null;
    private static int DefaultModifierPriceDelta(string id) => MaterialModifierDatabase.TryGetData(id, out MaterialModifierData data) && data != null ? data.price : 0;
    private static MaterialModifierData DefaultFindModifier(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;
        DataTable<MaterialModifierData> table = GameDataReader.LoadTable<MaterialModifierData>("MaterialModifierData");
        for (int i = 0; table != null && table.items != null && i < table.items.Count; i++)
        {
            MaterialModifierData data = table.items[i];
            if (data != null && data.id == id)
                return data;
        }
        return null;
    }
}

/// <summary>Generation and quotes only; never reads a scene or mutates the player's inventory.</summary>
public sealed class ShopOfferGenerator
{
    private enum ShopMaterialPoolKind { Normal, Weak, Strong }
    private readonly EconomyConfigData config;
    private readonly ShopGenerationServices services;
    private readonly IReadOnlyList<MagicModel> ownedMagic;
    private readonly List<MagicData> magicPool = new List<MagicData>();
    private readonly List<ShopMaterialOfferData> strongMaterialOfferPool = new List<ShopMaterialOfferData>();
    private readonly List<ShopMaterialOfferData> normalMaterialOfferPool = new List<ShopMaterialOfferData>();
    private readonly List<ShopMaterialOfferData> weakMaterialOfferPool = new List<ShopMaterialOfferData>();
    private ShopProductPoolData productPool;
    private Func<int, int, int> nextRandomInt;

    public ShopOfferGenerator(EconomyConfigData config, ShopGenerationServices services = null,
        IReadOnlyList<MagicModel> ownedMagic = null)
    {
        this.config = config ?? new EconomyConfigData();
        this.services = services ?? new ShopGenerationServices();
        this.ownedMagic = ownedMagic;
    }

    public void Generate(IReadOnlyList<ShopLayer> layers, List<ShopOffer> offers, List<List<ShopOffer>> layerOffers,
        Func<int, int, int> batchRandom = null)
    {
        nextRandomInt = batchRandom ?? services.NextRandomInt;
        offers.Clear();
        layerOffers.Clear();
        productPool = GetShopProductPool();
        BuildMagicPool();
        BuildMaterialOfferPools();
        for (int i = 0; layers != null && i < layers.Count; i++)
        {
            List<ShopOffer> layerList = new List<ShopOffer>();
            GenerateLayerOffers(layers[i], layerList);
            layerOffers.Add(layerList);
            offers.AddRange(layerList);
        }
    }

    private void GenerateLayerOffers(ShopLayer layer, List<ShopOffer> target)
    {
        if (layer == null || layer.weights == null)
            return;
        int guaranteedArrows = layer.HasType(ShopSlotEnum.Arrow) ? Mathf.Max(0, layer.guaranteedEnchantedArrowCount) : 0;
        float budget = layer.slotLimit - guaranteedArrows * ShopLayer.GetSlotCost(ShopSlotEnum.Arrow);
        float used = 0f;
        bool progressed = true;
        int safety = 0;
        while (progressed && safety < 64)
        {
            safety++;
            progressed = false;
            // Dictionary order, including the legacy unused weight values, is intentional.
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
            case ShopSlotEnum.Item: return TryAddMagicOffer(target);
            case ShopSlotEnum.Arrow: return TryAddMaterialOffer(target);
            default: return false;
        }
    }

    private void AddGuaranteedEnchantedArrow(List<ShopOffer> target)
    {
        if (strongMaterialOfferPool.Count > 0) AddMaterialOfferFromPool(strongMaterialOfferPool, target);
        else if (normalMaterialOfferPool.Count > 0) AddMaterialOfferFromPool(normalMaterialOfferPool, target);
        else if (weakMaterialOfferPool.Count > 0) AddMaterialOfferFromPool(weakMaterialOfferPool, target);
    }

    private ShopProductPoolData GetShopProductPool()
    {
        if (config.shopProductPoolId > 0)
        {
            ShopProductPoolData configuredPool = services.FindProductPool(config.shopProductPoolId);
            if (configuredPool != null)
                return configuredPool;
        }
        ShopProductPoolData fallback = null;
        foreach (ShopProductPoolData pool in services.AllProductPools())
            if (pool != null && (fallback == null || pool.numericId < fallback.numericId)) fallback = pool;
        return fallback;
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
            RewardPoolData rewardPool = config.shopMagicRewardPoolId > 0 ? services.FindRewardPool(config.shopMagicRewardPoolId) : null;
            if (rewardPool != null && rewardPool.magicIds != null && rewardPool.magicIds.Length > 0)
            {
                for (int i = 0; i < rewardPool.magicIds.Length; i++)
                    AddMagicPoolData(rewardPool.magicIds[i]);
            }
        }
        if (magicPool.Count == 0)
        {
            foreach (MagicData data in services.AllMagic())
            {
                if (data != null && services.IsMagicUnlocked(data))
                    magicPool.Add(data);
            }
            magicPool.Sort((a, b) => a.numericId.CompareTo(b.numericId));
        }
    }

    private void AddMagicPoolData(int id)
    {
        MagicData data = services.FindMagic(id);
        if (data != null && services.IsMagicUnlocked(data) && !magicPool.Contains(data))
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

    private void AddMaterialOffers(ShopMaterialOfferData[] source, List<ShopMaterialOfferData> target, ShopMaterialPoolKind kind)
    {
        for (int i = 0; source != null && i < source.Length; i++)
        {
            if (IsValidShopMaterialOffer(source[i], kind))
                target.Add(source[i]);
        }
    }

    private bool IsValidShopMaterialOffer(ShopMaterialOfferData offer, ShopMaterialPoolKind kind)
    {
        if (offer == null || offer.material == MaterialEnum.None)
            return false;
        if (string.IsNullOrEmpty(offer.modifierId))
            return kind == ShopMaterialPoolKind.Normal;
        if (IsExcludedShopModifierId(offer.modifierId))
            return false;
        MaterialModifierData data = services.FindModifier(offer.modifierId);
        if (data == null || !services.IsModifierUnlocked(data) || string.IsNullOrEmpty(data.script) || MaterialModifierFactory.Create(data) == null)
            return false;
        bool weak = IsWeakShopModifierId(offer.modifierId);
        return kind == ShopMaterialPoolKind.Weak ? weak : kind == ShopMaterialPoolKind.Strong && !weak;
    }

    private static bool IsWeakShopModifierId(string id)
    {
        switch (id)
        {
            case "half_arrow": case "temporary": case "doom": case "lazy": case "fragile_arrow": return true;
            default: return false;
        }
    }

    private static bool IsExcludedShopModifierId(string id)
    {
        switch (id)
        {
            case "omni_arrow": case "return_arrow": case "period_arrow": case "pack_arrow":
            case "linked_arrow": case "random_arrow": case "eternal_arrow": case "repeat_arrow": return true;
            default: return false;
        }
    }

    private bool TryAddMagicOffer(List<ShopOffer> target)
    {
        if (magicPool.Count == 0)
            return false;
        MagicData data = MagicRaritySystem.SelectWeightedMagic(magicPool, nextRandomInt, ownedMagic, config.ownedMagicWeightMultiplier);
        if (data == null)
            return false;
        magicPool.Remove(data);
        target.Add(new ShopOffer { kind = ShopItemKind.Magic, price = services.QuoteMagic(data), magicData = data });
        return true;
    }

    public static int GetMagicBuyPrice(MagicData data, EconomyConfigData economy = null, Func<int, int> modifyPrice = null)
    {
        if (data == null)
            return 0;
        economy = economy ?? GameDataDatabase.GetDefaultEconomyConfig() ?? new EconomyConfigData();
        int offset;
        switch (data.rarity)
        {
            case MagicRarity.Common: offset = -2; break;
            case MagicRarity.Rare: offset = -1; break;
            case MagicRarity.Legendary: offset = 1; break;
            default: offset = 0; break;
        }
        Func<int, int> priceModifier = modifyPrice ?? new Func<int, int>(DifficultyUpgradeSystem.ModifyShopPrice);
        return priceModifier(economy.shopSpellPrice + offset);
    }

    public static int GetMagicSellPrice(MagicData data) => Mathf.Max(0, GetMagicBuyPrice(data) / 2);

    public static int GetMagicSellPrice(MagicModel magic, PlayerState player)
    {
        if (magic == null) return 0;
        using (player?.Rules.Enter())
            return Mathf.Max(0, magic.ModifySellPrice(GetMagicSellPrice(magic.Data)));
    }

    private bool TryAddMaterialOffer(List<ShopOffer> target)
    {
        bool useWeak = ShouldUseWeakMaterialOffer();
        if (useWeak && weakMaterialOfferPool.Count > 0 && AddMaterialOfferFromPool(weakMaterialOfferPool, target)) return true;
        if (normalMaterialOfferPool.Count > 0 && AddMaterialOfferFromPool(normalMaterialOfferPool, target)) return true;
        if (!useWeak && weakMaterialOfferPool.Count > 0 && AddMaterialOfferFromPool(weakMaterialOfferPool, target)) return true;
        return strongMaterialOfferPool.Count > 0 && AddMaterialOfferFromPool(strongMaterialOfferPool, target);
    }

    private bool ShouldUseWeakMaterialOffer()
    {
        float chance = productPool != null ? productPool.weakMaterialChance : 0.1f;
        if (chance <= 0f) return false;
        if (chance >= 1f) return true;
        return nextRandomInt(0, 10000) < Mathf.RoundToInt(chance * 10000f);
    }

    private bool AddMaterialOfferFromPool(List<ShopMaterialOfferData> pool, List<ShopOffer> target)
    {
        if (pool.Count == 0)
            return false;
        int index = nextRandomInt(0, pool.Count);
        ShopMaterialOfferData offerData = pool[index];
        // Keep removal before quote: the legacy enchanted base price uses the remaining normal pool.
        pool.RemoveAt(index);
        MaterialModifierData modifierData = services.FindModifier(offerData.modifierId);
        target.Add(new ShopOffer { kind = ShopItemKind.Material, price = GetMaterialOfferPrice(offerData), material = offerData.material, materialModifierData = modifierData });
        return true;
    }

    public int GetOfferPrice(int price) => services.ModifyPrice(price >= 0 ? price : config.shopMaterialPrice);

    private int GetMaterialOfferPrice(ShopMaterialOfferData offer)
    {
        if (offer == null) return GetOfferPrice(0);
        if (string.IsNullOrEmpty(offer.modifierId)) return GetOfferPrice(offer.price);
        return GetOfferPrice(Mathf.Max(0, ResolveMaterialBasePrice() + services.ModifierPriceDelta(offer.modifierId)));
    }

    private int ResolveMaterialBasePrice()
    {
        for (int i = 0; i < normalMaterialOfferPool.Count; i++)
        {
            ShopMaterialOfferData offer = normalMaterialOfferPool[i];
            if (offer != null && offer.price >= 0) return offer.price;
        }
        for (int i = 0; productPool != null && productPool.normalMaterialOffers != null && i < productPool.normalMaterialOffers.Length; i++)
        {
            ShopMaterialOfferData offer = productPool.normalMaterialOffers[i];
            if (offer != null && offer.price >= 0) return offer.price;
        }
        return Mathf.Max(0, config.shopMaterialPrice);
    }
}
