using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 道具栏“视觉阅读顺序”计算：把槽位按玩家看到的顺序排列（行从上到下，行内从左到右），
/// 供战斗结算“以同一个箭头为起点匹配多个道具”时的触发顺序使用。
///
/// 顺序按道具栏容器**当前的实际布局形态**判断，所以道具栏增减导致的重排（行数/列数变化）会自动跟随，
/// 不需要为每种槽位数量单独写规则：
/// - 容器带 <see cref="MagicBookCurveLayout"/>（PC 的弧形道具栏）：弧线本身就是单行，槽位顺序即阅读顺序；
/// - 容器带启用的 <see cref="GridLayoutGroup"/>（移动端 PE 的网格道具栏）：按实际 y 聚成行、行内按 x 升序；
/// - 其它情况（例如单元测试里的普通数据）退回槽位顺序，保持稳定。
/// </summary>
public static class MagicBookVisualOrder
{
    /// <summary>同一行判定容差（像素）。网格布局中同一行的 y 完全相同，1px 足以区分相邻行。</summary>
    public const float RowTolerance = 1f;

    /// <summary>
    /// 计算每个槽位的阅读顺序序号（0 = 最先触发），写入 <paramref name="ranks"/>。
    /// <paramref name="area"/> 为道具栏容器（<c>HandSystemUI.magicBookArea</c>），
    /// <paramref name="slotPositions"/> 与槽位顺序一一对应（取 <c>RectTransform.anchoredPosition</c>）。
    /// </summary>
    public static void ComputeRanks(RectTransform area, IReadOnlyList<Vector2> slotPositions, List<int> ranks)
    {
        if (ranks == null)
            return;

        int count = slotPositions != null ? slotPositions.Count : 0;
        FillIdentityRanks(count, ranks);
        if (count <= 1 || area == null)
            return;

        // 弧形道具栏：单行，槽位顺序就是从左到右的阅读顺序。
        if (area.GetComponent<MagicBookCurveLayout>() != null)
            return;

        // 网格道具栏（移动端）：按实际位置算行优先顺序。
        // 行式布局有两类：旧场景的 GridLayoutGroup，以及 PE 现用的 MagicBookRowLayout。
        GridLayoutGroup grid = area.GetComponent<GridLayoutGroup>();
        bool rowBasedLayout = (grid != null && grid.isActiveAndEnabled) || area.GetComponent<MagicBookRowLayout>() != null;
        if (!rowBasedLayout)
            return;

        ComputeRanksFromPositions(slotPositions, ranks);
    }

    /// <summary>
    /// 纯位置版本（便于单元测试）：按“行从上到下、行内从左到右”给出每个槽位的序号。
    /// 行由 y 聚类得到（同一行 y 相同），位置完全相同时保持传入的槽位顺序（稳定排序）。
    /// </summary>
    public static void ComputeRanksFromPositions(IReadOnlyList<Vector2> positions, List<int> ranks)
    {
        if (ranks == null)
            return;

        int count = positions != null ? positions.Count : 0;
        FillIdentityRanks(count, ranks);
        if (count <= 1)
            return;

        List<List<int>> rows = new List<List<int>>();
        GroupRows(positions, -1, rows);

        List<int> order = new List<int>(count);
        for (int r = 0; r < rows.Count; r++)
        {
            List<int> row = rows[r];
            row.Sort((a, b) =>
            {
                int byX = positions[a].x.CompareTo(positions[b].x);
                return byX != 0 ? byX : a.CompareTo(b);
            });
            order.AddRange(row);
        }

        for (int i = 0; i < order.Count; i++)
            ranks[order[i]] = i;
    }

    /// <summary>
    /// 把槽位按 y 聚成行（上 → 下），每行内的下标按 x 升序写入 <paramref name="rows"/>。
    /// <paramref name="ignoreIndex"/> 用于排除拖拽中的槽位（它的位置跟着指针走，不参与行聚类）。
    /// </summary>
    public static void GroupRows(IReadOnlyList<Vector2> positions, int ignoreIndex, List<List<int>> rows)
    {
        if (rows == null)
            return;

        rows.Clear();
        int count = positions != null ? positions.Count : 0;
        if (count == 0)
            return;

        List<int> order = new List<int>(count);
        for (int i = 0; i < count; i++)
        {
            if (i != ignoreIndex)
                order.Add(i);
        }

        order.Sort((a, b) =>
        {
            int byY = positions[b].y.CompareTo(positions[a].y);
            if (byY != 0)
                return byY;

            int byX = positions[a].x.CompareTo(positions[b].x);
            return byX != 0 ? byX : a.CompareTo(b);
        });

        List<int> current = null;
        float rowY = 0f;
        for (int i = 0; i < order.Count; i++)
        {
            int index = order[i];
            float y = positions[index].y;
            if (current == null || Mathf.Abs(y - rowY) > RowTolerance)
            {
                current = new List<int>();
                rows.Add(current);
                rowY = y;
            }
            current.Add(index);
        }
    }

    /// <summary>
    /// 拖拽落点 → 目标槽位下标（行优先语义）：先按行定位（取离指针最近的行），
    /// 再按行内 x 决定插入位，最后换算成该位置上的槽位下标。
    /// 拖动项自身不参与候选。单行布局（弧形道具栏 / 单行网格）下沿用“按 x 从左到右”的旧插入语义，
    /// 多行布局（移动端 2 列网格）才启用行优先定位。返回 -1 表示无有效目标。
    /// </summary>
    public static int ComputeDropTargetIndex(IReadOnlyList<Vector2> positions, int draggedIndex, Vector2 pointer, IReadOnlyList<int> ranks)
    {
        int count = positions != null ? positions.Count : 0;
        if (count <= 1)
            return -1;

        List<List<int>> rows = new List<List<int>>();
        GroupRows(positions, draggedIndex, rows);
        if (rows.Count == 0)
            return -1;

        // 单行：保持原有语义（指针左侧的槽位数即目标下标），PC 弧形道具栏行为不变。
        if (rows.Count == 1)
        {
            int leftCount = 0;
            List<int> onlyRow = rows[0];
            for (int i = 0; i < onlyRow.Count; i++)
            {
                if (positions[onlyRow[i]].x < pointer.x)
                    leftCount++;
            }
            return Mathf.Clamp(leftCount, 0, count - 1);
        }

        // 多行：指针所在行取“离指针最近的行”（行间距可能为负，不能靠包含式判断）。
        int targetRow = 0;
        float bestDistance = float.MaxValue;
        for (int r = 0; r < rows.Count; r++)
        {
            float rowY = positions[rows[r][0]].y;
            float distance = Mathf.Abs(pointer.y - rowY);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                targetRow = r;
            }
        }

        int insert = 0;
        for (int r = 0; r < targetRow; r++)
            insert += rows[r].Count;

        int inRow = 0;
        List<int> targetRowSlots = rows[targetRow];
        for (int i = 0; i < targetRowSlots.Count; i++)
        {
            if (positions[targetRowSlots[i]].x < pointer.x)
                inRow++;
        }
        insert += Mathf.Clamp(inRow, 0, targetRowSlots.Count - 1);   // 落点夹在目标行内，避免跳到下一行

        List<int> candidates = new List<int>(count - 1);
        for (int i = 0; i < count; i++)
        {
            if (i != draggedIndex)
                candidates.Add(i);
        }
        if (candidates.Count == 0)
            return -1;

        if (ranks != null && ranks.Count == count)
            candidates.Sort((a, b) => ranks[a].CompareTo(ranks[b]));

        insert = Mathf.Clamp(insert, 0, candidates.Count - 1);
        return candidates[insert];
    }

    private static void FillIdentityRanks(int count, List<int> ranks)
    {
        ranks.Clear();
        for (int i = 0; i < count; i++)
            ranks.Add(i);
    }
}
