using System;
using System.Collections.Generic;

public static class MaterialArrowSortUtility
{
    public static void SortMaterialsByBaseDirection<T>(List<T> items, Func<T, MaterialEnum> materialSelector, Func<T, int> stableIndexSelector)
    {
        if (items == null || materialSelector == null || stableIndexSelector == null)
            return;

        items.Sort((left, right) =>
        {
            int orderCompare = GetBaseDirectionSortOrder(materialSelector(left)).CompareTo(GetBaseDirectionSortOrder(materialSelector(right)));
            return orderCompare != 0 ? orderCompare : stableIndexSelector(left).CompareTo(stableIndexSelector(right));
        });
    }

    public static int GetBaseDirectionSortOrder(MaterialEnum material)
    {
        switch (material)
        {
            case MaterialEnum.Fire:
                return 0;
            case MaterialEnum.Water:
                return 1;
            case MaterialEnum.Wind:
                return 2;
            case MaterialEnum.Earth:
                return 3;
            default:
                return 4;
        }
    }
}
