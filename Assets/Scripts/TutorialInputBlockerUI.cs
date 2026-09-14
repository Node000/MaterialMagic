using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 教程输入拦截器：保证玩家的操作只发生在高亮框内。
/// - <c>blockWholeScreen = true</c>：整屏阻挡（用于“点击推进”的阅读页，点哪里都只推进教程，不会误操作底层 UI）。
/// - <c>blockWholeScreen = false</c>：只阻挡高亮框以外的区域（行为页，框内的操作透传给底层 UI）。
/// 与 <see cref="TutorialCutoutMaskUI"/> 共用同一套洞计算；颜色全透明，只参与射线检测。
/// </summary>
public class TutorialInputBlockerUI : TutorialCutoutMaskUI
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
}
