using System.Collections.Generic;

/// <summary>
/// 战斗内“以同一个箭头（Token）为起点”匹配到多个道具时的触发顺序规则。
///
/// 设计约定（见 <c>Assets/Docs/教程文本与顺序审阅.md</c> 7-2：“以同一个箭头为起点，若满足多个道具的要求，
/// 则会按从左到右的顺序依次触发道具效果”）：
/// 触发顺序 = 道具栏布局从左到右的顺序（slotIndex 升序），**与配方长度无关**。
/// 因此配方更长的道具不会插队，排在更左侧的短配方道具先触发。
/// 道具栏顺序本身就是玩家可拖拽调整的 slotIndex（<see cref="PlayerState.RearrangeMagicSlotsByVisualOrder"/>）。
/// </summary>
public static class MagicMatchOrderUtility
{
    /// <summary>
    /// 收集 <paramref name="magicsInLayoutOrder"/> 中所有能从 <paramref name="startIndex"/> 开始匹配 <paramref name="tokens"/> 的道具，
    /// 并把它们的下标按道具栏从左到右的顺序写入 <paramref name="matchedIndices"/>。
    /// 排序以 <see cref="MagicModel.SlotIndex"/> 为准；slotIndex 无效（&lt; 0）时退回传入下标，保持稳定。
    /// </summary>
    public static void CollectMatchedMagicIndices(
        IReadOnlyList<MagicModel> magicsInLayoutOrder,
        IReadOnlyList<ArrowReadToken> tokens,
        int startIndex,
        List<int> matchedIndices)
    {
        if (matchedIndices == null)
            return;

        matchedIndices.Clear();
        if (magicsInLayoutOrder == null)
            return;

        for (int i = 0; i < magicsInLayoutOrder.Count; i++)
        {
            MagicModel magic = magicsInLayoutOrder[i];
            if (magic == null || !magic.IsMatch(tokens, startIndex))
                continue;

            int layoutOrder = GetLayoutOrder(magic, i);
            int insertIndex = matchedIndices.Count;
            for (int j = 0; j < matchedIndices.Count; j++)
            {
                int matchedIndex = matchedIndices[j];
                if (layoutOrder < GetLayoutOrder(magicsInLayoutOrder[matchedIndex], matchedIndex))
                {
                    insertIndex = j;
                    break;
                }
            }

            matchedIndices.Insert(insertIndex, i);
        }
    }

    /// <summary>道具栏布局位置：以 slotIndex 为准，无效时退回传入下标（只用于兜底，避免出现空洞时顺序抖动）。</summary>
    private static int GetLayoutOrder(MagicModel magic, int fallbackOrder)
    {
        return magic != null && magic.SlotIndex >= 0 ? magic.SlotIndex : fallbackOrder;
    }
}
