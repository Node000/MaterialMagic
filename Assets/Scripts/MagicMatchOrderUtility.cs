using System.Collections.Generic;

/// <summary>
/// 战斗内“以同一个箭头（Token）为起点”匹配到多个道具时的触发顺序规则。
///
/// 设计约定（见 <c>Assets/Docs/教程文本与顺序审阅.md</c> 7-2：“以同一个箭头为起点，若满足多个道具的要求，
/// 则会按从左到右的顺序依次触发道具效果”）：
/// 触发顺序 = 道具栏布局的**视觉阅读顺序**（行从上到下、行内从左到右），**与配方长度无关**。
/// 视觉顺序由 <see cref="MagicBookVisualOrder"/> 依据道具栏当前真实布局算出（弧形单行 = 从左到右；
/// 移动端网格 = 行优先），未提供时退回 <see cref="MagicModel.SlotIndex"/> 升序（槽位索引）。
/// 因此配方更长的道具不会插队，排在阅读顺序更前的道具先触发。
/// </summary>
public static class MagicMatchOrderUtility
{
    /// <summary>
    /// 收集 <paramref name="magicsInLayoutOrder"/> 中所有能从 <paramref name="startIndex"/> 开始匹配 <paramref name="tokens"/> 的道具，
    /// 并把它们的下标按道具栏视觉阅读顺序写入 <paramref name="matchedIndices"/>。
    /// <paramref name="visualRanks"/> 为每个槽位的阅读序号（下标与传入列表一一对应，来自 <see cref="MagicBookVisualOrder"/>），
    /// 为空时退回 <see cref="MagicModel.SlotIndex"/>，slotIndex 也无效（&lt; 0）时保持传入顺序。
    /// </summary>
    public static void CollectMatchedMagicIndices(
        IReadOnlyList<MagicModel> magicsInLayoutOrder,
        IReadOnlyList<ArrowReadToken> tokens,
        int startIndex,
        List<int> matchedIndices,
        IReadOnlyList<int> visualRanks = null)
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

            int layoutOrder = GetLayoutOrder(magicsInLayoutOrder, visualRanks, i);
            int insertIndex = matchedIndices.Count;
            for (int j = 0; j < matchedIndices.Count; j++)
            {
                int matchedIndex = matchedIndices[j];
                if (layoutOrder < GetLayoutOrder(magicsInLayoutOrder, visualRanks, matchedIndex))
                {
                    insertIndex = j;
                    break;
                }
            }

            matchedIndices.Insert(insertIndex, i);
        }
    }

    /// <summary>
    /// 道具栏布局位置：优先用视觉阅读序号（<paramref name="visualRanks"/>，按下标对应），
    /// 否则用 slotIndex，再无效时退回传入下标（只用于兜底，避免出现空洞时顺序抖动）。
    /// </summary>
    private static int GetLayoutOrder(IReadOnlyList<MagicModel> magics, IReadOnlyList<int> visualRanks, int index)
    {
        if (visualRanks != null && index >= 0 && index < visualRanks.Count)
            return visualRanks[index];

        MagicModel magic = magics != null && index >= 0 && index < magics.Count ? magics[index] : null;
        return magic != null && magic.SlotIndex >= 0 ? magic.SlotIndex : index;
    }
}
