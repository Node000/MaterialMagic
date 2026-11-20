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

    /// <summary>箭头层保底出现在最后一格的强附魔箭头数量（其它格子按普通/弱附魔池生成）。</summary>
    public int guaranteedEnchantedArrowCount;

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
                // 1.5：标准 6 格预算下恰好放 4 个箭头（9.7 改版：商店箭头 6 → 4）。
                return 1.5f;
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
        ShopLayer layer = new ShopLayer(new Dictionary<ShopSlotEnum, float> { { ShopSlotEnum.Arrow, 1f } });
        layer.guaranteedEnchantedArrowCount = 1;
        return layer;
    }

    public static ShopLayer CreateRelicLayer()
    {
        return new ShopLayer(new Dictionary<ShopSlotEnum, float> { { ShopSlotEnum.Relic, 1f } });
    }
}
