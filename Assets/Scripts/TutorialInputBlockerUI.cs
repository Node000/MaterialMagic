using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 教程输入拦截器：保证玩家的操作只发生在高亮框内。
/// - <c>blockWholeScreen = true</c>：整屏阻挡（用于“点击推进”的阅读页，点哪里都只推进教程，不会误操作底层 UI）。
/// - <c>blockWholeScreen = false</c>：只阻挡高亮框以外的区域（行为页，框内的操作透传给底层 UI）。
/// 与 <see cref="TutorialCutoutMaskUI"/> 共用同一套洞计算；颜色全透明，只参与射线检测。
///
/// 注意：UGUI 的射线检测按 <c>RectTransform</c> 矩形判定（<c>GraphicRaycaster</c> → <c>RectangleContainsScreenPoint</c>），
/// 不会看网格形状；所以“只挡框外”必须额外实现 <c>ICanvasRaycastFilter.IsRaycastLocationValid</c>，
/// 否则全屏矩形会把框内的点击一起吞掉。
/// </summary>
public class TutorialInputBlockerUI : TutorialCutoutMaskUI, ICanvasRaycastFilter
{
    [Tooltip("勾选后阻挡全屏（阅读页）；取消则只阻挡高亮框以外（行为页）。")]
    [SerializeField] private bool blockWholeScreen = true;

    /// <summary>输入拦截器不画高亮边框。</summary>
    protected override bool DrawBorders => false;

    /// <summary>透明阻挡面：变暗由 Overlay/CutoutMask 负责。</summary>
    protected override Color ResolveDimColor(TutorialVisualConfig config)
    {
        Color c = config.DimColor;
        c.a = 0f;
        return c;
    }

    /// <summary>整屏阻挡 / 只挡框外。</summary>
    public bool BlockWholeScreen
    {
        get { return blockWholeScreen; }
        set
        {
            if (blockWholeScreen == value)
                return;

            blockWholeScreen = value;
            SetVerticesDirty();
        }
    }

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = true;
        Color c = color;
        c.a = 0f;
        color = c;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        if (!blockWholeScreen)
        {
            // 只挡框外：复用遮罩的“洞外变暗”几何，但透明且不画边框。
            base.OnPopulateMesh(vh);
            return;
        }

        vh.Clear();
        AddQuad(vh, GetPixelAdjustedRect(), color);
    }

    /// <summary>
    /// 射线过滤：<c>blockWholeScreen = false</c> 时，落点在高亮洞内的点击判定为无效，
    /// 让事件继续落到洞下方的真实 UI；否则整块全屏矩形会吃掉框内所有点击。
    /// </summary>
    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        if (blockWholeScreen || !isActiveAndEnabled)
            return true;

        CollectHoleRects();
        if (holeRects.Count == 0)
            return true;

        Vector2 localPoint;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out localPoint))
            return true;

        for (int i = 0; i < holeRects.Count; i++)
        {
            if (holeRects[i].Contains(localPoint))
                return false;
        }

        return true;
    }
}
