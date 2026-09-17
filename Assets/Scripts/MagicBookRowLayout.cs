using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 移动端（PE）道具栏布局：固定 N 行，一列从上到下排满后开新的一列；
/// 列按槽位逻辑编号**从左到右**排列，整块**右对齐**——所以只有 1 列时它落在最右侧，
/// 新列出现时整块向左推进一列（最新的列始终停在最右），最早的列溢出到左侧，不裁切、不隐藏。
///
/// 关键点：摆位依据是槽位的逻辑编号（<see cref="MagicItemView.GetSlotIndex"/>），**不是子物体顺序**，
/// 所以 hover 提层（SetAsLastSibling）不会引起整排重排，避免指针进入/离开时的抖动闪烁。
/// 视觉阅读顺序（行从上到下、行内从左到右）仍由 <see cref="MagicBookVisualOrder"/> 按实际位置算出，
/// 战斗结算“同一起点多个道具”的触发顺序继续跟随布局。
/// </summary>
public class MagicBookRowLayout : LayoutGroup
{
    [Header("格子")]
    [Tooltip("单个槽位的尺寸（与之前 GridLayoutGroup 的 Cell Size 一致）。")]
    [SerializeField] private Vector2 cellSize = new Vector2(212.7f, 87.7f);
    [Tooltip("槽位间距（与之前 GridLayoutGroup 的 Spacing 一致）。")]
    [SerializeField] private Vector2 spacing = new Vector2(19.1f, 33.2f);
    [Tooltip("固定行数：一列从第 1 行排到第 N 行，再开新的一列。")]
    [SerializeField, Min(1)] private int rowCount = 4;

    private readonly List<MagicItemView> orderedViews = new List<MagicItemView>();

    /// <summary>当前可见槽位按逻辑编号排好的顺序（只读用途）。</summary>
    public IReadOnlyList<MagicItemView> OrderedViews => orderedViews;

    public override void CalculateLayoutInputHorizontal()
    {
        base.CalculateLayoutInputHorizontal();
        int columns = Mathf.Max(1, Mathf.CeilToInt((float)CountVisibleViews() / Mathf.Max(1, rowCount)));
        float width = columns * cellSize.x + Mathf.Max(0, columns - 1) * spacing.x + padding.horizontal;
        SetLayoutInputForAxis(width, -1f, 0f, 0);
    }

    public override void CalculateLayoutInputVertical()
    {
        int visible = Mathf.Max(1, CountVisibleViews());
        int rows = Mathf.Min(visible, Mathf.Max(1, rowCount));
        float height = rows * cellSize.y + Mathf.Max(0, rows - 1) * spacing.y + padding.vertical;
        SetLayoutInputForAxis(height, -1f, 0f, 1);
    }

    public override void SetLayoutHorizontal() => Arrange();

    public override void SetLayoutVertical() => Arrange();

    /// <summary>供 HandSystemUI 在重建槽位后立刻刷新布局（避免同帧读位置时读到旧值）。</summary>
    public void RefreshLayoutImmediate()
    {
        if (!isActiveAndEnabled)
            return;

        CollectVisibleViews();
        Arrange();
        LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
    }

    private int CountVisibleViews()
    {
        int count = 0;
        for (int i = 0; i < rectTransform.childCount; i++)
        {
            Transform child = rectTransform.GetChild(i);
            if (child.gameObject.activeSelf && child.GetComponent<MagicItemView>() != null)
                count++;
        }
        return count;
    }

    private void Arrange()
    {
        CollectVisibleViews();

        int count = orderedViews.Count;
        if (count == 0)
            return;

        int rows = Mathf.Max(1, rowCount);
        int columns = Mathf.Max(1, Mathf.CeilToInt((float)count / rows));
        float pitchX = cellSize.x + spacing.x;
        float pitchY = cellSize.y + spacing.y;

        // 右对齐：最后一列（槽位编号最大的那一列）贴容器右边缘，列号越大越靠右；
        // 列数增加时整块自然向左推进，最早的列溢出到左侧。
        // 子物体用 (0.5,0.5) 锚点：anchoredPosition 与容器中心坐标系一致，
        // 这样拖拽落点判定/视觉顺序读取的位置与实际鼠标坐标在同一空间（旧的 GridLayoutGroup 用 (0,1) 锚点会带固定偏移）。
        float rightCenterX = rectTransform.rect.xMax - padding.right - cellSize.x * 0.5f;
        float topCenterY = rectTransform.rect.yMax - padding.top - cellSize.y * 0.5f;

        for (int i = 0; i < count; i++)
        {
            MagicItemView view = orderedViews[i];
            RectTransform child = view.transform as RectTransform;
            if (child == null)
                continue;

            int column = i / rows;
            int row = i % rows;

            child.anchorMin = new Vector2(0.5f, 0.5f);
            child.anchorMax = new Vector2(0.5f, 0.5f);
            child.pivot = new Vector2(0.5f, 0.5f);
            child.sizeDelta = cellSize;
            child.anchoredPosition = new Vector2(
                rightCenterX - (columns - 1 - column) * pitchX,
                topCenterY - row * pitchY);
        }
    }

    /// <summary>
    /// 按槽位逻辑编号收集可见槽位；未绑定编号（&lt;0）的排到最后，同级按层级顺序兜底。
    /// </summary>
    private void CollectVisibleViews()
    {
        orderedViews.Clear();
        for (int i = 0; i < rectTransform.childCount; i++)
        {
            Transform child = rectTransform.GetChild(i);
            if (!child.gameObject.activeSelf)
                continue;

            MagicItemView view = child.GetComponent<MagicItemView>();
            if (view != null)
                orderedViews.Add(view);
        }

        orderedViews.Sort((a, b) =>
        {
            int indexA = a != null ? a.GetSlotIndex() : -1;
            int indexB = b != null ? b.GetSlotIndex() : -1;
            if (indexA < 0) indexA = int.MaxValue;
            if (indexB < 0) indexB = int.MaxValue;
            if (indexA != indexB)
                return indexA.CompareTo(indexB);
            return a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex());
        });
    }
}
