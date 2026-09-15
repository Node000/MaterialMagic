using UnityEngine;

/// <summary>
/// 教程教学页的全局视觉参数：遮罩变暗程度、高亮框样式、洞的外扩、提示框尺寸与停靠位。
/// 资产路径约定与项目其它配置一致：Assets/Resources/Config/TutorialVisualConfig.asset。
/// 说明：只控制“教学”这一套界面，不改动 PopupDragonWindowBlank 等美术预制体。
/// </summary>
[CreateAssetMenu(fileName = "TutorialVisualConfig", menuName = "MaterialMagic/Tutorial Visual Config")]
public class TutorialVisualConfig : ScriptableObject
{
    /// <summary>Resources 下的资产名（不含扩展名）。</summary>
    public const string ResourcePath = "Config/TutorialVisualConfig";

    private static TutorialVisualConfig cached;
    private static bool loadAttempted;

    [Header("遮罩变暗（高亮框以外的区域）")]
    [Tooltip("变暗用的颜色；透明度由 dimAlpha 控制，改这一项即可调节“变暗程度”。")]
    [SerializeField] private Color dimColor = new Color(0f, 0f, 0f, 1f);

    [Tooltip("变暗程度：0 = 不变暗，1 = 全黑。深色背景上建议 0.60-0.72。")]
    [SerializeField, Range(0f, 1f)] private float dimAlpha = 0.58f;

    [Header("高亮框（洞的边框）")]
    [SerializeField] private Color borderColor = new Color(1f, 0.84f, 0.16f, 1f);

    [Tooltip("直角框线宽（像素，画布坐标）；仅 borderStyle = Straight 时使用。")]
    [SerializeField, Min(0f)] private float borderThickness = 4f;

    [Tooltip("高亮框样式：Straight = 直角实线框（旧表现）；Spring = 3 条细弹簧线（当前）；None = 只变暗不描边。")]
    [SerializeField] private TutorialBorderStyle borderStyle = TutorialBorderStyle.Spring;

    [Header("弹簧线框（borderStyle = Spring 时生效）")]
    [Tooltip("细线组相对洞边界向内的偏移（画布像素）：第一圈线的中心线位置。默认贴洞内侧。")]
    [SerializeField, Min(0f)] private float springInset = 14f;

    [Tooltip("细线条数。")]
    [SerializeField, Range(1, 8)] private int springLineCount = 3;

    [Tooltip("单条线宽（画布像素；比道具框的 3 更细）。")]
    [SerializeField, Min(0.5f)] private float springLineWidth = 1.8f;

    [Tooltip("圈间距（画布像素）。")]
    [SerializeField, Min(0f)] private float springLineSpacing = 4f;

    [Tooltip("主抖动幅度（画布像素）；与道具框弹簧线一致取 4。")]
    [SerializeField, Min(0f)] private float springWobbleAmplitude = 4f;

    [Tooltip("每圈波数；与道具框弹簧线一致取 7。")]
    [SerializeField, Range(1, 32)] private int springWaveCount = 7;

    [Tooltip("次级噪声幅度；与道具框弹簧线一致取 3。")]
    [SerializeField, Min(0f)] private float springScribbleAmount = 3f;

    [Tooltip("每圈采样点数。")]
    [SerializeField, Range(16, 256)] private int springSamplesPerLine = 120;

    [Tooltip("转角圆度：2 = 最圆，12 = 接近直角；与道具框弹簧线一致取 5。")]
    [SerializeField, Range(2f, 12f)] private float springSharpness = 5f;

    [Tooltip("是否流动。关闭后只在目标变化时重建，零每帧开销。")]
    [SerializeField] private bool springAnimate = true;

    [Tooltip("流动的步进帧率（与道具框弹簧线一致：12 fps）。")]
    [SerializeField, Range(1, 30)] private int springFps = 12;

    [Tooltip("波形流动速度。")]
    [SerializeField, Min(0f)] private float springFlowSpeed = 0.7f;

    [Tooltip("脉动幅度（各圈错开呼吸感）。")]
    [SerializeField, Range(0f, 1f)] private float springPulseAmount = 0.14f;

    [Tooltip("脉动速度。")]
    [SerializeField, Min(0f)] private float springPulseSpeed = 2.2f;

    [Tooltip("洞相对目标矩形的外扩量（画布像素）。")]
    [SerializeField] private Vector2 holePadding = new Vector2(18f, 18f);

    [Header("提示框")]
    [Tooltip("默认提示框尺寸（画布像素）。")]
    [SerializeField] private Vector2 promptBoxSize = new Vector2(620f, 190f);

    [Tooltip("侧边停靠位使用的窄框尺寸（按页直接摆位时可不使用）。")]
    [SerializeField] private Vector2 sideBoxSize = new Vector2(480f, 190f);

    [Tooltip("上停靠位坐标（画布中心为原点，y 向上）。")]
    [SerializeField] private Vector2 anchorTop = new Vector2(0f, 250f);

    [Tooltip("下停靠位坐标。")]
    [SerializeField] private Vector2 anchorBottom = new Vector2(0f, -250f);

    [Tooltip("左停靠位坐标。")]
    [SerializeField] private Vector2 anchorLeft = new Vector2(-470f, -60f);

    [Tooltip("右停靠位坐标。")]
    [SerializeField] private Vector2 anchorRight = new Vector2(470f, -60f);

    /// <summary>变暗颜色（含 dimAlpha）。</summary>
    public Color DimColor => new Color(dimColor.r, dimColor.g, dimColor.b, DimAlpha);

    /// <summary>变暗程度。</summary>
    public float DimAlpha => Mathf.Clamp01(dimAlpha);

    public Color BorderColor => borderColor;

    public float BorderThickness => Mathf.Max(0f, borderThickness);

    /// <summary>高亮框样式。</summary>
    public TutorialBorderStyle BorderStyle => borderStyle;

    /// <summary>弹簧线组相对洞边界的内收（画布像素）。</summary>
    public float SpringInset => Mathf.Max(0f, springInset);

    public int SpringLineCount => Mathf.Clamp(springLineCount, 1, 8);

    public float SpringLineWidth => Mathf.Max(0.5f, springLineWidth);

    public float SpringLineSpacing => Mathf.Max(0f, springLineSpacing);

    public float SpringWobbleAmplitude => Mathf.Max(0f, springWobbleAmplitude);

    public int SpringWaveCount => Mathf.Clamp(springWaveCount, 1, 32);

    public float SpringScribbleAmount => Mathf.Max(0f, springScribbleAmount);

    public int SpringSamplesPerLine => Mathf.Clamp(springSamplesPerLine, SpringLineGeometry.MinSamplesPerLine, SpringLineGeometry.MaxSamplesPerLine);

    public float SpringSharpness => Mathf.Clamp(springSharpness, 2f, 12f);

    public bool SpringAnimate => springAnimate;

    public int SpringFps => Mathf.Clamp(springFps, 1, 30);

    public float SpringFlowSpeed => Mathf.Max(0f, springFlowSpeed);

    public float SpringPulseAmount => Mathf.Clamp01(springPulseAmount);

    public float SpringPulseSpeed => Mathf.Max(0f, springPulseSpeed);

    /// <summary>弹簧线的抖动参数（供 <see cref="SpringLineGeometry"/> 使用）。</summary>
    public SpringLineGeometry.Settings CreateSpringSettings()
    {
        return new SpringLineGeometry.Settings
        {
            shape = SpringLineGeometry.Shape.RoundedRect,
            samplesPerLine = SpringSamplesPerLine,
            sharpness = SpringSharpness,
            wobbleAmplitude = SpringWobbleAmplitude,
            waveCount = SpringWaveCount,
            scribbleAmount = SpringScribbleAmount,
            tangentWobble = 0.18f,
            linePhaseOffset = 0.045f,
            seed = 17
        };
    }

    public Vector2 HolePadding => holePadding;

    public Vector2 PromptBoxSize => promptBoxSize;

    public Vector2 SideBoxSize => sideBoxSize;

    public Vector2 AnchorTop => anchorTop;

    public Vector2 AnchorBottom => anchorBottom;

    public Vector2 AnchorLeft => anchorLeft;

    public Vector2 AnchorRight => anchorRight;

    /// <summary>按停靠位取坐标；未配置时回退到默认值。</summary>
    public Vector2 GetAnchor(TutorialPromptAnchor anchor)
    {
        switch (anchor)
        {
            case TutorialPromptAnchor.Top: return AnchorTop;
            case TutorialPromptAnchor.Bottom: return AnchorBottom;
            case TutorialPromptAnchor.Left: return AnchorLeft;
            case TutorialPromptAnchor.Right: return AnchorRight;
            default: return AnchorTop;
        }
    }

    /// <summary>停靠位是否为侧边窄框。</summary>
    public bool IsSideAnchor(TutorialPromptAnchor anchor)
    {
        return anchor == TutorialPromptAnchor.Left || anchor == TutorialPromptAnchor.Right;
    }

    /// <summary>停靠位对应的提示框尺寸。</summary>
    public Vector2 GetBoxSize(TutorialPromptAnchor anchor)
    {
        return IsSideAnchor(anchor) ? sideBoxSize : promptBoxSize;
    }

    /// <summary>全局实例（Resources 加载并缓存；缺失时返回 null，调用方需回退自身字段）。</summary>
    public static TutorialVisualConfig Instance
    {
        get
        {
            if (!loadAttempted)
            {
                cached = Resources.Load<TutorialVisualConfig>(ResourcePath);
                loadAttempted = true;
            }
            return cached;
        }
    }

    /// <summary>清掉缓存，供调试刷新或重新导入后使用。</summary>
    public static void ClearCache()
    {
        cached = null;
        loadAttempted = false;
    }
}

/// <summary>提示框停靠位。</summary>
public enum TutorialPromptAnchor
{
    Top = 0,
    Bottom = 1,
    Left = 2,
    Right = 3
}

/// <summary>教程高亮框样式。</summary>
public enum TutorialBorderStyle
{
    /// <summary>直角实线框（旧表现）。</summary>
    Straight = 0,

    /// <summary>多条细弹簧线（手绘线）。</summary>
    Spring = 1,

    /// <summary>只变暗、不描边。</summary>
    None = 2
}
