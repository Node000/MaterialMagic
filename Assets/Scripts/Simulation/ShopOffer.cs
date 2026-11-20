using System;

// Field names remain compatible with existing shop views and save DTOs.
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

public enum ShopTransactionFailure
{
    None,
    InvalidOffer,
    AlreadyPurchased,
    NotEnoughGold,
    NoMagicSlot,
    InvalidMaterial,
    RemoveAlreadyUsed,
    NothingToUndo,
    InventoryChanged
}

/// <summary>A committed transaction, available immediately and independent of any animation.</summary>
public sealed class ShopTransaction
{
    public ShopOffer Offer { get; internal set; }
    public int GoldBefore { get; internal set; }
    public int Price { get; internal set; }
    public int MagicSlotIndex { get; internal set; } = -1;
    public int ResultingMagicCount { get; internal set; }
    public MagicModel PreviousMagic { get; internal set; }
    public MagicModel AcquiredMagic { get; internal set; }
    public MaterialModel AddedMaterial { get; internal set; }
    public MaterialModel RemovedMaterial { get; internal set; }
    public bool UsedRemoveAction { get; internal set; }
    public bool CountedPaidArrowRemoval { get; internal set; }
    public int PaidArrowRemovalCountBefore { get; internal set; }
}
