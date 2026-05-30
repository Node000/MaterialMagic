using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TutorialCutoutMaskUI : Graphic
{
    [SerializeField] private RectTransform target;
    [SerializeField] private Vector2 padding = new Vector2(18f, 18f);
    [SerializeField] private float borderThickness = 4f;
    [SerializeField] private Color borderColor = new Color(1f, 0.84f, 0.16f, 0.88f);

    private readonly Vector3[] worldCorners = new Vector3[4];
    private readonly List<RectTransform> reusableTargets = new List<RectTransform>();

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    public void SetTarget(RectTransform target)
    {
        this.target = target;
        SetVerticesDirty();
    }

    public void SetTargetByName(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrEmpty(targetName))
        {
            SetTarget(null);
            return;
        }

        reusableTargets.Clear();
        root.GetComponentsInChildren(true, reusableTargets);
        for (int i = 0; i < reusableTargets.Count; i++)
        {
            if (reusableTargets[i] != null && reusableTargets[i].name == targetName)
            {
                SetTarget(reusableTargets[i]);
                return;
            }
        }
        SetTarget(null);
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect full = GetPixelAdjustedRect();
        Rect hole = GetLocalHoleRect(full);
        AddDimMesh(vh, full, hole);
        AddBorderMesh(vh, hole);
    }

    private Rect GetLocalHoleRect(Rect fallback)
    {
        if (target == null)
            return new Rect(fallback.center - new Vector2(110f, 45f), new Vector2(220f, 90f));

        target.GetWorldCorners(worldCorners);
        RectTransform self = rectTransform;
        Vector2 min = Vector2.positiveInfinity;
        Vector2 max = Vector2.negativeInfinity;
        for (int i = 0; i < worldCorners.Length; i++)
        {
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(self, RectTransformUtility.WorldToScreenPoint(canvas != null ? canvas.worldCamera : null, worldCorners[i]), canvas != null ? canvas.worldCamera : null, out localPoint);
            min = Vector2.Min(min, localPoint);
            max = Vector2.Max(max, localPoint);
        }

        min -= padding;
        max += padding;
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private void AddDimMesh(VertexHelper vh, Rect full, Rect hole)
    {
        Color32 dimColor = color;
        AddQuad(vh, new Rect(full.xMin, hole.yMax, full.width, full.yMax - hole.yMax), dimColor);
        AddQuad(vh, new Rect(full.xMin, full.yMin, full.width, hole.yMin - full.yMin), dimColor);
        AddQuad(vh, new Rect(full.xMin, hole.yMin, hole.xMin - full.xMin, hole.height), dimColor);
        AddQuad(vh, new Rect(hole.xMax, hole.yMin, full.xMax - hole.xMax, hole.height), dimColor);
    }

    private void AddBorderMesh(VertexHelper vh, Rect hole)
    {
        Color32 lineColor = borderColor;
        float t = Mathf.Max(1f, borderThickness);
        AddQuad(vh, new Rect(hole.xMin - t, hole.yMax, hole.width + t * 2f, t), lineColor);
        AddQuad(vh, new Rect(hole.xMin - t, hole.yMin - t, hole.width + t * 2f, t), lineColor);
        AddQuad(vh, new Rect(hole.xMin - t, hole.yMin, t, hole.height), lineColor);
        AddQuad(vh, new Rect(hole.xMax, hole.yMin, t, hole.height), lineColor);
    }

    private void AddQuad(VertexHelper vh, Rect rect, Color32 quadColor)
    {
        if (rect.width <= 0f || rect.height <= 0f)
            return;

        int start = vh.currentVertCount;
        vh.AddVert(new Vector3(rect.xMin, rect.yMin), quadColor, Vector2.zero);
        vh.AddVert(new Vector3(rect.xMin, rect.yMax), quadColor, Vector2.zero);
        vh.AddVert(new Vector3(rect.xMax, rect.yMax), quadColor, Vector2.zero);
        vh.AddVert(new Vector3(rect.xMax, rect.yMin), quadColor, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start + 2, start + 3, start);
    }
}
