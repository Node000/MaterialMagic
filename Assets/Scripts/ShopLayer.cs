using System.Collections.Generic;

public enum ShopSlotEnum
{
    Item,
    Arrow,
    Relic
}

public class ShopLayer
{
    public Dictionary<ShopSlotEnum, float> weights = new Dictionary<ShopSlotEnum, float>();
    public int slotLimit;
    public bool isLastLayer;

    private const int DefaultSlotLimit = 6;

    public ShopLayer()
    {
        slotLimit = DefaultSlotLimit;
    }

    public ShopLayer(Dictionary<ShopSlotEnum, float> weights, int slotLimit = DefaultSlotLimit, bool isLastLayer = false)
    {
        this.weights = weights != null ? weights : new Dictionary<ShopSlotEnum, float>();
        this.slotLimit = slotLimit;
        this.isLastLayer = isLastLayer;
    }

    public bool HasType(ShopSlotEnum type)
    {
        return weights != null && weights.ContainsKey(type);
    }

    public static float GetSlotCost(ShopSlotEnum type)
    {
        switch (type)
        {
            case ShopSlotEnum.Item:
                return 2f;
            case ShopSlotEnum.Arrow:
                return 1f;
            case ShopSlotEnum.Relic:
                return 1.5f;
            default:
                return 1f;
        }
    }

    public static ShopLayer CreateItemLayer()
    {
        return new ShopLayer(new Dictionary<ShopSlotEnum, float> { { ShopSlotEnum.Item, 1f } });
    }

    public static ShopLayer CreateArrowLayer()
    {
        return new ShopLayer(new Dictionary<ShopSlotEnum, float> { { ShopSlotEnum.Arrow, 1f } });
    }

    public static ShopLayer CreateRelicLayer()
    {
        return new ShopLayer(new Dictionary<ShopSlotEnum, float> { { ShopSlotEnum.Relic, 1f } });
    }
}
