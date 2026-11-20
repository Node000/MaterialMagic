using System;
using System.Collections.Generic;

/// <summary>
/// Authoritative shop state and synchronous transactions. No scene, MonoBehaviour or UGUI dependency.
/// A successful operation is fully committed before its caller may save or start presentation.
/// </summary>
public sealed class ShopSession
{
    private readonly EconomyConfigData config;
    private readonly ShopGenerationServices services;
    private readonly ShopOfferGenerator generator;
    private readonly List<ShopOffer> offers = new List<ShopOffer>();
    private readonly List<List<ShopOffer>> layerOffers = new List<List<ShopOffer>>();
    private readonly List<ShopLayer> layers = new List<ShopLayer>();
    private ShopTransaction undo;
    private int batchSeed;

    public PlayerState Owner { get; }
    public int MagicSlotCapacity { get; set; }
    public IReadOnlyList<ShopOffer> Offers => offers;
    public IReadOnlyList<List<ShopOffer>> LayerOffers => layerOffers;
    public IReadOnlyList<ShopLayer> Layers => layers;
    public int RefreshCount { get; private set; }
    public bool RemoveArrowUsed { get; private set; }
    public ShopOffer SelectedOffer { get; private set; }
    public bool WaitingForSelection { get; private set; }
    public ShopTransaction UndoTransaction => undo;
    public int RefreshCost => Math.Max(0, config.shopRefreshPrice) + RefreshCount;
    // Apply the run surcharge after difficulty/rounding, so each paid removal adds exactly one.
    public int RemoveArrowPrice { get { using (Owner.Rules.Enter()) return generator.GetOfferPrice(config.shopRemoveMaterialPrice) + Owner.PaidShopArrowRemovalCount; } }

    public ShopSession(PlayerState owner, EconomyConfigData config, int magicSlotCapacity, ShopGenerationServices services = null)
    {
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        this.config = config ?? new EconomyConfigData();
        this.services = services ?? new ShopGenerationServices { NextRandomInt = (min, max) => ChoiceRuleRandom.Next(Owner, min, max) };
        generator = new ShopOfferGenerator(this.config, this.services, Owner.MagicBook);
        MagicSlotCapacity = Math.Max(0, magicSlotCapacity);
    }

    public void Open(IReadOnlyList<ShopLayer> configuredLayers, ShopNodeSaveData savedState = null, int nodeSeed = 0)
    {
        using (Owner.Rules.Enter()) OpenCore(configuredLayers, savedState, nodeSeed);
    }

    private void OpenCore(IReadOnlyList<ShopLayer> configuredLayers, ShopNodeSaveData savedState, int nodeSeed)
    {
        layers.Clear();
        for (int i = 0; configuredLayers != null && i < configuredLayers.Count; i++)
            layers.Add(configuredLayers[i]);
        if (layers.Count == 0)
        {
            layers.Add(ShopLayer.CreateItemLayer());
            layers.Add(ShopLayer.CreateArrowLayer());
        }
        batchSeed = savedState?.batchSeed ?? nodeSeed;
        RefreshCount = 0;
        RemoveArrowUsed = false;
        CancelSelection();
        ClearUndo();
        if (savedState != null)
            RestoreState(savedState);
        else
            GenerateBatch(0);
    }

    private void GenerateBatch(int batch)
    {
        int step = 0;
        int seed = unchecked(batchSeed * 397 ^ batch);
        generator.Generate(layers, offers, layerOffers, batchSeed == 0 ? null :
            (Func<int, int, int>)((min, max) => RunRandom.Range(seed, step++, min, max)));
    }

    public int IndexOfOffer(ShopOffer offer) => offers.IndexOf(offer);
    public bool CanAfford(ShopOffer offer) => offer != null && offer.price >= 0 && Owner.Gold >= offer.price;
    public int GetFreeMagicSlotIndex()
    {
        for (int i = 0; i < MagicSlotCapacity; i++)
        {
            if (Owner.GetMagicAtSlot(i) == null)
                return i;
        }
        return -1;
    }

    public bool IsRemovableMaterial(MaterialModel material)
        => material != null && Owner.Deck.Contains(material) && !PlayerState.IsDeckPlaceholderMaterial(material);

    public bool HasRemovableMaterial()
    {
        for (int i = 0; i < Owner.Deck.Count; i++)
        {
            if (IsRemovableMaterial(Owner.Deck[i]))
                return true;
        }
        return false;
    }

    public bool CanUseOffer(ShopOffer offer)
    {
        if (offer == null || !offers.Contains(offer) || offer.purchased)
            return false;
        switch (offer.kind)
        {
            case ShopItemKind.Magic: return offer.magicData != null && GetFreeMagicSlotIndex() >= 0;
            case ShopItemKind.Material: return offer.material != MaterialEnum.None;
            case ShopItemKind.RemoveMaterial: return HasRemovableMaterial();
            default: return false;
        }
    }

    public bool BeginRemovalSelection(ShopOffer offer = null)
    {
        using (Owner.Rules.Enter()) return BeginRemovalSelectionCore(offer);
    }

    private bool BeginRemovalSelectionCore(ShopOffer offer)
    {
        if (!HasRemovableMaterial() || (offer == null ? RemoveArrowUsed || Owner.Gold < RemoveArrowPrice : offer.kind != ShopItemKind.RemoveMaterial || !CanUseOffer(offer) || !CanAfford(offer)))
            return false;
        SelectedOffer = offer;
        WaitingForSelection = true;
        return true;
    }

    public void CancelSelection()
    {
        SelectedOffer = null;
        WaitingForSelection = false;
    }

    public bool TryRefresh(out int cost, Func<bool> persist = null)
    {
        using (Owner.Rules.Enter()) return TryRefreshCore(out cost, persist);
    }

    private bool TryRefreshCore(out int cost, Func<bool> persist)
    {
        cost = RefreshCost;
        if (Owner.Gold < cost)
            return false;
        var previousOffers = persist != null ? offers.ToArray() : null;
        var previousLayers = persist != null ? layerOffers.ToArray() : null;
        ShopTransaction previousUndo = undo;
        ShopOffer previousSelection = SelectedOffer;
        bool wasSelecting = WaitingForSelection;
        bool usedRemoval = RemoveArrowUsed;
        int previousGold = Owner.Gold;
        var status = Owner as PlayerStatus;
        int previousStep = status?.RunRandomStep ?? 0;
        GenerateBatch(RefreshCount + 1);
        Owner.AddGold(-cost);
        RefreshCount++;
        RemoveArrowUsed = false;
        CancelSelection();
        ClearUndo();
        if (persist == null || persist()) return true;

        offers.Clear();
        offers.AddRange(previousOffers);
        layerOffers.Clear();
        layerOffers.AddRange(previousLayers);
        RefreshCount--;
        RemoveArrowUsed = usedRemoval;
        SelectedOffer = previousSelection;
        WaitingForSelection = wasSelecting;
        undo = previousUndo;
        Owner.SetStartingGold(previousGold);
        if (status != null) status.SetRunRandomState(status.RunRandomSeed, previousStep);
        return false;
    }

    public bool TryPurchase(ShopOffer offer, out ShopTransaction transaction, out ShopTransactionFailure failure, MaterialModel removalTarget = null)
    {
        using (Owner.Rules.Enter()) return TryPurchaseCore(offer, out transaction, out failure, removalTarget);
    }

    private bool TryPurchaseCore(ShopOffer offer, out ShopTransaction transaction, out ShopTransactionFailure failure, MaterialModel removalTarget)
    {
        transaction = null;
        failure = ValidateOffer(offer);
        if (failure != ShopTransactionFailure.None)
            return false;

        MagicModel acquired = null;
        MaterialModifierModel modifier = null;
        int slot = -1;
        switch (offer.kind)
        {
            case ShopItemKind.Magic:
                slot = GetFreeMagicSlotIndex();
                if (slot < 0) return Fail(ShopTransactionFailure.NoMagicSlot, out failure);
                if (offer.magicData == null) return Fail(ShopTransactionFailure.InvalidOffer, out failure);
                acquired = MagicFactory.Create(offer.magicData, slot);
                if (acquired == null) return Fail(ShopTransactionFailure.InvalidOffer, out failure);
                break;
            case ShopItemKind.Material:
                if (offer.material == MaterialEnum.None) return Fail(ShopTransactionFailure.InvalidMaterial, out failure);
                modifier = MaterialModifierFactory.Create(offer.materialModifierData);
                break;
            case ShopItemKind.RemoveMaterial:
                if (!IsRemovableMaterial(removalTarget)) return Fail(ShopTransactionFailure.InvalidMaterial, out failure);
                break;
            default:
                return Fail(ShopTransactionFailure.InvalidOffer, out failure);
        }

        transaction = new ShopTransaction
        {
            Offer = offer, Price = offer.price, GoldBefore = Owner.Gold,
            MagicSlotIndex = slot, AcquiredMagic = acquired,
            PreviousMagic = slot >= 0 ? Owner.GetMagicAtSlot(slot) : null
        };
        Owner.AddGold(-offer.price);
        if (slot >= 0)
            Owner.SetMagicAtSlot(acquired, slot);
        else if (offer.kind == ShopItemKind.Material)
            transaction.AddedMaterial = Owner.AddDeckMaterial(offer.material, modifier);
        else
        {
            Owner.RemoveCardEverywhere(removalTarget);
            transaction.RemovedMaterial = removalTarget;
            CountPaidArrowRemoval(transaction);
        }
        offer.purchased = true;
        transaction.ResultingMagicCount = Owner.MagicBook.Count;
        undo = transaction;
        CancelSelection();
        return true;
    }

    public bool TryRemoveMaterial(MaterialModel target, out ShopTransaction transaction, out ShopTransactionFailure failure)
    {
        using (Owner.Rules.Enter()) return TryRemoveMaterialCore(target, out transaction, out failure);
    }

    private bool TryRemoveMaterialCore(MaterialModel target, out ShopTransaction transaction, out ShopTransactionFailure failure)
    {
        transaction = null;
        failure = ShopTransactionFailure.None;
        if (RemoveArrowUsed) return Fail(ShopTransactionFailure.RemoveAlreadyUsed, out failure);
        if (!IsRemovableMaterial(target)) return Fail(ShopTransactionFailure.InvalidMaterial, out failure);
        int price = RemoveArrowPrice;
        if (price < 0 || Owner.Gold < price) return Fail(ShopTransactionFailure.NotEnoughGold, out failure);
        transaction = new ShopTransaction { Price = price, GoldBefore = Owner.Gold, RemovedMaterial = target, UsedRemoveAction = true };
        Owner.AddGold(-price);
        Owner.RemoveCardEverywhere(target);
        CountPaidArrowRemoval(transaction);
        RemoveArrowUsed = true;
        undo = transaction;
        CancelSelection();
        return true;
    }

    private void CountPaidArrowRemoval(ShopTransaction transaction)
    {
        if (transaction.Price <= 0)
            return;
        transaction.CountedPaidArrowRemoval = true;
        transaction.PaidArrowRemovalCountBefore = Owner.PaidShopArrowRemovalCount;
        Owner.RestorePaidShopArrowRemovalCount(Owner.PaidShopArrowRemovalCount + 1);
    }

    private ShopTransactionFailure ValidateOffer(ShopOffer offer)
    {
        if (offer == null || !offers.Contains(offer) || offer.price < 0) return ShopTransactionFailure.InvalidOffer;
        if (offer.purchased) return ShopTransactionFailure.AlreadyPurchased;
        if (Owner.Gold < offer.price) return ShopTransactionFailure.NotEnoughGold;
        return ShopTransactionFailure.None;
    }

    private static bool Fail(ShopTransactionFailure value, out ShopTransactionFailure failure)
    {
        failure = value;
        return false;
    }

    public void ClearUndo() => undo = null;

    public bool TryUndo(out ShopTransaction transaction)
    {
        using (Owner.Rules.Enter()) return TryUndoCore(out transaction);
    }

    private bool TryUndoCore(out ShopTransaction transaction)
    {
        transaction = undo;
        if (transaction == null)
            return false;
        // A restored receipt must target the actual deck object, not a newly constructed lookalike.
        MaterialModel added = ResolveDeckMaterial(transaction.AddedMaterial);
        if (transaction.AddedMaterial != null && added == null)
            return false;
        if (transaction.MagicSlotIndex >= 0)
        {
            MagicModel current = Owner.GetMagicAtSlot(transaction.MagicSlotIndex);
            if (current == null || (transaction.AcquiredMagic != null && current != transaction.AcquiredMagic))
                return false;
            if (transaction.PreviousMagic != null)
                Owner.SetMagicAtSlot(transaction.PreviousMagic, transaction.MagicSlotIndex);
            else
                Owner.ClearMagicSlot(transaction.MagicSlotIndex);
        }
        int delta = transaction.GoldBefore - Owner.Gold;
        if (delta != 0)
            Owner.AddGold(delta, false, RelicGoldReason.Refund);
        if (added != null)
            Owner.RemoveCardEverywhere(added);
        if (transaction.RemovedMaterial != null && ResolveDeckMaterial(transaction.RemovedMaterial) == null)
            Owner.Deck.Add(transaction.RemovedMaterial);
        if (transaction.Offer != null)
            transaction.Offer.purchased = false;
        if (transaction.UsedRemoveAction)
            RemoveArrowUsed = false;
        if (transaction.CountedPaidArrowRemoval)
            Owner.RestorePaidShopArrowRemovalCount(transaction.PaidArrowRemovalCountBefore);
        CancelSelection();
        ClearUndo();
        return true;
    }

    private MaterialModel ResolveDeckMaterial(MaterialModel candidate)
    {
        if (candidate == null) return null;
        for (int i = 0; i < Owner.Deck.Count; i++)
        {
            MaterialModel card = Owner.Deck[i];
            if (card == candidate || (card != null && !string.IsNullOrEmpty(candidate.instanceId) && card.instanceId == candidate.instanceId))
                return card;
        }
        return null;
    }

    public ShopNodeSaveData ExportState()
    {
        using (Owner.Rules.Enter()) return ExportStateCore();
    }

    private ShopNodeSaveData ExportStateCore()
    {
        ShopNodeSaveData data = new ShopNodeSaveData
        {
            offers = new ShopOfferSaveData[offers.Count],
            layerOfferCounts = new int[layerOffers.Count],
            selectedOfferIndex = offers.IndexOf(SelectedOffer),
            waitingForSelection = WaitingForSelection,
            purchaseInProgress = false, // Presentation is never a rule/persistence lock.
            removeArrowUsed = RemoveArrowUsed,
            refreshCount = RefreshCount,
            batchSeed = batchSeed,
            undo = ExportUndoState()
        };
        for (int i = 0; i < offers.Count; i++) data.offers[i] = offers[i]?.Export();
        for (int i = 0; i < layerOffers.Count; i++) data.layerOfferCounts[i] = layerOffers[i].Count;
        return data;
    }

    public void RestoreState(ShopNodeSaveData data)
    {
        using (Owner.Rules.Enter()) RestoreStateCore(data);
    }

    private void RestoreStateCore(ShopNodeSaveData data)
    {
        if (data == null) return;
        offers.Clear();
        for (int i = 0; data.offers != null && i < data.offers.Length; i++)
        {
            ShopOfferSaveData source = data.offers[i];
            offers.Add(source == null ? null : new ShopOffer
            {
                kind = (ShopItemKind)source.kind, price = source.price, purchased = source.purchased,
                material = (MaterialEnum)source.material,
                magicData = source.magicNumericId > 0 ? services.FindMagic(source.magicNumericId) : null,
                materialModifierData = !string.IsNullOrEmpty(source.materialModifierId) ? services.FindModifier(source.materialModifierId) : null
            });
        }
        RestoreLayerOffers(data.layerOfferCounts);
        batchSeed = data.batchSeed;
        RefreshCount = Math.Max(0, data.refreshCount);
        RemoveArrowUsed = data.removeArrowUsed;
        SelectedOffer = data.selectedOfferIndex >= 0 && data.selectedOfferIndex < offers.Count ? offers[data.selectedOfferIndex] : null;
        WaitingForSelection = data.waitingForSelection;
        if (SelectedOffer != null && SelectedOffer.kind == ShopItemKind.Magic)
            CancelSelection();
        RestoreUndoState(data.undo);
    }

    private void RestoreLayerOffers(int[] counts)
    {
        layerOffers.Clear();
        int index = 0;
        bool exactCounts = counts != null && counts.Length == layers.Count;
        int sum = 0;
        if (exactCounts)
        {
            for (int i = 0; i < counts.Length; i++) { if (counts[i] < 0) exactCounts = false; sum += counts[i]; }
            exactCounts &= sum == offers.Count;
        }
        for (int i = 0; i < layers.Count; i++)
        {
            List<ShopOffer> row = new List<ShopOffer>();
            if (exactCounts)
            {
                for (int j = 0; j < counts[i]; j++) row.Add(offers[index++]);
            }
            else
            {
                // Legacy saves had flat offers only. Reconstruct ordered layer runs without consuming RNG.
                ShopLayer layer = layers[i];
                float used = 0f;
                while (index < offers.Count)
                {
                    ShopOffer offer = offers[index];
                    ShopSlotEnum type = offer != null && offer.kind == ShopItemKind.Magic ? ShopSlotEnum.Item : ShopSlotEnum.Arrow;
                    if (layer == null || !layer.HasType(type)) break;
                    float cost = ShopLayer.GetSlotCost(type);
                    if (!layer.isLastLayer && row.Count > 0 && used + cost > layer.slotLimit) break;
                    row.Add(offer);
                    index++;
                    used += cost;
                }
                // Keep every saved offer, including legacy/custom-layer overflow.
                if (i == layers.Count - 1)
                    while (index < offers.Count) row.Add(offers[index++]);
            }
            layerOffers.Add(row);
        }
    }

    private ShopUndoSaveData ExportUndoState()
    {
        if (undo == null) return null;
        return new ShopUndoSaveData
        {
            offerIndex = offers.IndexOf(undo.Offer), gold = undo.GoldBefore, price = undo.Price,
            countedPaidArrowRemoval = undo.CountedPaidArrowRemoval,
            paidArrowRemovalCountBefore = undo.PaidArrowRemovalCountBefore,
            magicSlotIndex = undo.MagicSlotIndex,
            previousMagicNumericId = undo.PreviousMagic != null ? undo.PreviousMagic.NumericId : 0,
            previousMagicModifierId = undo.PreviousMagic?.PrimaryModifier?.Id ?? string.Empty,
            previousMagicSellPriceState = undo.PreviousMagic?.ExportSellPriceState() ?? -1,
            addedMaterial = ExportMaterial(undo.AddedMaterial), removedMaterial = ExportMaterial(undo.RemovedMaterial)
        };
    }

    private void RestoreUndoState(ShopUndoSaveData data)
    {
        ClearUndo();
        if (data == null) return;
        ShopOffer offer = data.offerIndex >= 0 && data.offerIndex < offers.Count ? offers[data.offerIndex] : null;
        MagicModel previous = null;
        if (data.previousMagicNumericId > 0)
        {
            MagicData previousData = services.FindMagic(data.previousMagicNumericId);
            previous = previousData != null ? MagicFactory.Create(previousData, data.magicSlotIndex) : null;
            previous?.RestoreSellPriceState(data.previousMagicSellPriceState);
            if (previous != null && !string.IsNullOrEmpty(data.previousMagicModifierId) && GameDataDatabase.TryGetMagicModifierData(data.previousMagicModifierId, out MagicModifierData modifierData))
                previous.AddModifier(MagicModifierFactory.Create(modifierData));
        }
        MaterialModel added = RuleMaterialCodec.Create(data.addedMaterial);
        MaterialModel removed = RuleMaterialCodec.Create(data.removedMaterial);
        if (offer == null && added == null && removed == null && data.magicSlotIndex < 0) return;
        undo = new ShopTransaction
        {
            Offer = offer, GoldBefore = data.gold, Price = data.price >= 0 ? data.price : (offer != null ? offer.price : RemoveArrowPrice),
            CountedPaidArrowRemoval = data.countedPaidArrowRemoval,
            PaidArrowRemovalCountBefore = Math.Max(0, data.paidArrowRemovalCountBefore),
            MagicSlotIndex = data.magicSlotIndex, PreviousMagic = previous,
            AcquiredMagic = data.magicSlotIndex >= 0 ? Owner.GetMagicAtSlot(data.magicSlotIndex) : null,
            AddedMaterial = ResolveDeckMaterial(added) ?? added, RemovedMaterial = removed,
            UsedRemoveAction = offer == null && removed != null
        };
    }

    private static MaterialCardSaveData ExportMaterial(MaterialModel card)
    {
        return RuleMaterialCodec.Export(card);
    }
}
