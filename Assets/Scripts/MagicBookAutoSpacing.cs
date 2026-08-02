using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(GridLayoutGroup))]
public class MagicBookAutoSpacing : MonoBehaviour
{
    [SerializeField] private float maxHeight = 545.1f;

    private GridLayoutGroup grid;

    private void Awake()
    {
        grid = GetComponent<GridLayoutGroup>();
    }

    private void OnEnable()
    {
        RefreshSpacing();
    }

    private void OnTransformChildrenChanged()
    {
        RefreshSpacing();
    }

    public void RefreshSpacing()
    {
        if (grid == null)
            grid = GetComponent<GridLayoutGroup>();

        int visibleCount = 0;
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child.gameObject.activeSelf && child.GetComponent<MagicItemView>() != null)
                visibleCount++;
        }

        if (visibleCount < 2)
            return;

        Vector2 spacing = grid.spacing;
        spacing.y = (maxHeight - visibleCount * grid.cellSize.y) / (visibleCount - 1);
        grid.spacing = spacing;
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)transform);
    }
}
