using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TutorialCutoutMaskUI : Graphic
{
    [Tooltip("高亮洞的目标列表（支持 1–3 个，例如“出牌区 + 出手按钮”）。")]
    [SerializeField] private List<RectTransform> targets = new List<RectTransform>();

    [SerializeField] private Vector2 padding = new Vector2(18f, 18f);
    [SerializeField] private float borderThickness = 4f;
    [SerializeField] private Color borderColor = new Color(1f, 0.84f, 0.16f, 1f);

    [Header("高亮框样式（跟随 TutorialVisualConfig）")]
    [Tooltip("Straight = 直角实线框；Spring = 3 条细弹簧线；None = 只变暗不描边。")]
    [SerializeField] private TutorialBorderStyle borderStyle = TutorialBorderStyle.Spring;

    [Tooltip("弹簧线组相对洞边界向内的偏移（画布像素）。")]
    [SerializeField, Min(0f)] private float springInset = 14f;

    [SerializeField, Range(1, 8)] private int springLineCount = 3;
    [SerializeField, Min(0.5f)] private float springLineWidth = 1.8f;
    [SerializeField, Min(0f)] private float springLineSpacing = 4f;
    [SerializeField, Min(0f)] private float springWobbleAmplitude = 4f;
    [SerializeField, Range(1, 32)] private int springWaveCount = 7;
    [SerializeField, Min(0f)] private float springScribbleAmount = 3f;
    [SerializeField, Range(16, 256)] private int springSamplesPerLine = 120;
    [SerializeField, Range(2f, 12f)] private float springSharpness = 5f;
    [SerializeField] private bool springAnimate = true;
    [SerializeField, Range(1, 30)] private int springFps = 12;
    [SerializeField, Min(0f)] private float springFlowSpeed = 0.7f;
    [SerializeField, Range(0f, 1f)] private float springPulseAmount = 0.14f;
    [SerializeField, Min(0f)] private float springPulseSpeed = 2.2f;

    [Header("全局配置")]
    [Tooltip("教程全局视觉配置；留空时从 Resources/Config/TutorialVisualConfig 读取。")]
    [SerializeField] private TutorialVisualConfig visualConfig;

    [Tooltip("勾选后忽略全局配置，使用本实例上的颜色/线宽/外扩（个别页面需要例外时才开）。")]
    [SerializeField] private bool useInstanceOverrides;

    private readonly Vector3[] worldCorners = new Vector3[4];
    private readonly List<RectTransform> reusableTargets = new List<RectTransform>();
    protected readonly List<Rect> holeRects = new List<Rect>();
    private readonly List<float> xEdges = new List<float>();
    private readonly List<Vector2> coveredSpans = new List<Vector2>();
    private readonly List<Vector2> springPoints = new List<Vector2>(256);
    private float springTime;
    private float springRedrawTimer;

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
        ApplyVisualConfig();
    }

    /// <summary>
    /// 应用教程全局视觉参数（变暗程度、高亮框颜色/线宽、洞外扩）。
    /// 未绑定配置或勾选了 useInstanceOverrides 时保留本实例序列化值。
    /// </summary>
    public void ApplyVisualConfig()
    {
        if (useInstanceOverrides)
            return;

        TutorialVisualConfig config = visualConfig != null ? visualConfig : TutorialVisualConfig.Instance;
        if (config == null)
            return;

        color = ResolveDimColor(config);
        borderColor = config.BorderColor;
        borderThickness = config.BorderThickness;
        padding = config.HolePadding;
        borderStyle = config.BorderStyle;
        springInset = config.SpringInset;
        springLineCount = config.SpringLineCount;
        springLineWidth = config.SpringLineWidth;
        springLineSpacing = config.SpringLineSpacing;
        springWobbleAmplitude = config.SpringWobbleAmplitude;
        springWaveCount = config.SpringWaveCount;
        springScribbleAmount = config.SpringScribbleAmount;
        springSamplesPerLine = config.SpringSamplesPerLine;
        springSharpness = config.SpringSharpness;
        springAnimate = config.SpringAnimate;
        springFps = config.SpringFps;
        springFlowSpeed = config.SpringFlowSpeed;
        springPulseAmount = config.SpringPulseAmount;
        springPulseSpeed = config.SpringPulseSpeed;
        SetVerticesDirty();
    }

    /// <summary>弹簧线的步进重绘：只在启用弹簧线且处于播放时运行（输入拦截器等 DrawBorders=false 的派生类不参与）。</summary>
    private void Update()
    {
        if (!Application.isPlaying || !DrawBorders || borderStyle != TutorialBorderStyle.Spring || !springAnimate)
            return;

        float deltaTime = Time.unscaledDeltaTime;
        springTime += deltaTime;
        if (springTime > 10000f)
            springTime = 0f;

        float frameInterval = 1f / Mathf.Max(1, springFps);
        springRedrawTimer += deltaTime;
        if (springRedrawTimer < frameInterval)
            return;

        springRedrawTimer %= frameInterval;
        SetVerticesDirty();
    }

    /// <summary>调试/预览用：临时切换边框样式（不改配置资产）。</summary>
    public void SetBorderStyle(TutorialBorderStyle style)
    {
        borderStyle = style;
        SetVerticesDirty();
    }

    /// <summary>遮罩实际使用的变暗颜色；输入拦截器覆写成全透明（只挡射线不显示）。</summary>
    protected virtual Color ResolveDimColor(TutorialVisualConfig config)
    {
        return config.DimColor;
    }

    /// <summary>供调参或运行时改配置后刷新；直接改颜色时也可调用。</summary>
    public void RefreshFromConfig()
    {
        ApplyVisualConfig();
    }

    [ContextMenu("应用教程全局视觉配置")]
    private void ApplyVisualConfigFromContextMenu()
    {
        ApplyVisualConfig();
    }

    public void SetTarget(RectTransform target)
    {
        targets.Clear();
        if (target != null)
            targets.Add(target);
        SetVerticesDirty();
    }

    /// <summary>设置 1–3 个洞目标（空元素会被忽略）。</summary>
    public void SetTargets(params RectTransform[] newTargets)
    {
        targets.Clear();
        if (newTargets != null)
        {
            for (int i = 0; i < newTargets.Length; i++)
            {
                if (newTargets[i] != null)
                    targets.Add(newTargets[i]);
            }
        }
        SetVerticesDirty();
    }

    /// <summary>设置多个洞目标（列表版，供仅收集到集合的调用方使用）。</summary>
    public void SetTargetList(IList<RectTransform> newTargets)
    {
        targets.Clear();
        if (newTargets != null)
        {
            for (int i = 0; i < newTargets.Count; i++)
            {
                if (newTargets[i] != null)
                    targets.Add(newTargets[i]);
            }
        }
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
        BuildHoleGeometry(vh, full, DrawBorders);
    }

    /// <summary>是否绘制高亮边框；输入拦截器覆写为 false（只需要可射线检测的阻挡面）。</summary>
    protected virtual bool DrawBorders => true;

    /// <summary>按当前目标构建“洞 + 变暗”几何（输入拦截器复用同一套洞计算）。</summary>
    protected void BuildHoleGeometry(VertexHelper vh, Rect full, bool drawBorders)
    {
        CollectHoleRects();
        if (holeRects.Count == 0)
        {
            // 没有目标：退化成“中间一个默认洞”，保持旧行为。
            holeRects.Add(new Rect(full.center - new Vector2(110f, 45f), new Vector2(220f, 90f)));
        }

        Rect bbox = GetHoleBounds(full);
        AddDimMesh(vh, full, bbox);
        if (holeRects.Count > 1)
            AddInnerDimMesh(vh, bbox);
        if (!drawBorders)
            return;

        for (int i = 0; i < holeRects.Count; i++)
            AddBorderMesh(vh, holeRects[i]);
    }

    /// <summary>所有洞的包围盒（无洞时退化成传入的整屏矩形）。</summary>
    protected Rect GetHoleBounds(Rect fallback)
    {
        if (holeRects.Count == 0)
            return fallback;

        Rect bbox = holeRects[0];
        for (int i = 1; i < holeRects.Count; i++) bbox = Rect.MinMaxRect(
            Mathf.Min(bbox.xMin, holeRects[i].xMin), Mathf.Min(bbox.yMin, holeRects[i].yMin),
            Mathf.Max(bbox.xMax, holeRects[i].xMax), Mathf.Max(bbox.yMax, holeRects[i].yMax));
        return bbox;
    }

    /// <summary>把目标矩形换算到本物体的局部坐标（外扩 padding）。</summary>
    protected void CollectHoleRects()
    {
        holeRects.Clear();
        for (int i = 0; i < targets.Count; i++)
            AddHoleRect(targets[i]);
    }

    private void AddHoleRect(RectTransform rt)
    {
        if (rt == null || !rt.gameObject.activeInHierarchy)
            return;

        Rect rect = GetLocalRect(rt);
        rect.xMin -= padding.x;
        rect.xMax += padding.x;
        rect.yMin -= padding.y;
        rect.yMax += padding.y;
        holeRects.Add(rect);
    }

    /// <summary>把世界矩形投影到本物体的局部平面，得到轴对齐包围盒。</summary>
    protected Rect GetLocalRect(RectTransform rt)
    {
        rt.GetWorldCorners(worldCorners);
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
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    /// <summary>多个洞时，把包围盒内部、不属于任何洞的区域也填上变暗色。</summary>
    protected void AddInnerDimMesh(VertexHelper vh, Rect bbox)
    {
        xEdges.Clear();
        xEdges.Add(bbox.xMin);
        xEdges.Add(bbox.xMax);
        for (int i = 0; i < holeRects.Count; i++)
        {
            xEdges.Add(Mathf.Clamp(holeRects[i].xMin, bbox.xMin, bbox.xMax));
            xEdges.Add(Mathf.Clamp(holeRects[i].xMax, bbox.xMin, bbox.xMax));
        }
        xEdges.Sort();

        Color32 dimColor = color;
        for (int i = 0; i < xEdges.Count - 1; i++)
        {
            float xa = xEdges[i];
            float xb = xEdges[i + 1];
            if (xb - xa <= 0.01f)
                continue;

            coveredSpans.Clear();
            for (int h = 0; h < holeRects.Count; h++)
            {
                Rect hole = holeRects[h];
                if (hole.xMin <= xa + 0.01f && hole.xMax >= xb - 0.01f)
                    coveredSpans.Add(new Vector2(Mathf.Max(hole.yMin, bbox.yMin), Mathf.Min(hole.yMax, bbox.yMax)));
            }
            coveredSpans.Sort((a, b) => a.x.CompareTo(b.x));

            float cursor = bbox.yMin;
            for (int s = 0; s < coveredSpans.Count; s++)
            {
                if (coveredSpans[s].x > cursor)
                    AddQuad(vh, new Rect(xa, cursor, xb - xa, coveredSpans[s].x - cursor), dimColor);
                cursor = Mathf.Max(cursor, coveredSpans[s].y);
            }
            if (cursor < bbox.yMax)
                AddQuad(vh, new Rect(xa, cursor, xb - xa, bbox.yMax - cursor), dimColor);
        }
    }

    protected void AddDimMesh(VertexHelper vh, Rect full, Rect hole)
    {
        Color32 dimColor = color;
        AddQuad(vh, new Rect(full.xMin, hole.yMax, full.width, full.yMax - hole.yMax), dimColor);
        AddQuad(vh, new Rect(full.xMin, full.yMin, full.width, hole.yMin - full.yMin), dimColor);
        AddQuad(vh, new Rect(full.xMin, hole.yMin, hole.xMin - full.xMin, hole.height), dimColor);
        AddQuad(vh, new Rect(hole.xMax, hole.yMin, full.xMax - hole.xMax, hole.height), dimColor);
    }

    private void AddBorderMesh(VertexHelper vh, Rect hole)
    {
        if (borderStyle == TutorialBorderStyle.None)
            return;

        if (borderStyle == TutorialBorderStyle.Spring)
        {
            AddSpringBorder(vh, hole);
            return;
        }

        Color line = borderColor;
        line.a = 1f;
        Color32 lineColor = line;
        float t = Mathf.Max(1f, borderThickness);
        AddQuad(vh, new Rect(hole.xMin - t, hole.yMax, hole.width + t * 2f, t), lineColor);
        AddQuad(vh, new Rect(hole.xMin - t, hole.yMin - t, hole.width + t * 2f, t), lineColor);
        AddQuad(vh, new Rect(hole.xMin - t, hole.yMin, t, hole.height), lineColor);
        AddQuad(vh, new Rect(hole.xMax, hole.yMin, t, hole.height), lineColor);
    }

    /// <summary>
    /// 弹簧线框：以洞边界为基准向内收 springInset，再按 springLineSpacing 排 springLineCount 圈抖动细线。
    /// 抖动参数与道具框弹簧线一致（见 TutorialVisualConfig）。
    /// </summary>
    private void AddSpringBorder(VertexHelper vh, Rect hole)
    {
        int count = Mathf.Clamp(springLineCount, 1, 8);
        float width = Mathf.Max(0.5f, springLineWidth);
        SpringLineGeometry.Settings settings = CreateSpringSettings();

        bool animating = Application.isPlaying && springAnimate;
        float time = animating ? springTime : 0f;

        Color line = borderColor;
        line.a = 1f;
        Color32 lineColor = line;

        for (int i = 0; i < count; i++)
        {
            float offset = -Mathf.Max(0f, springInset) + i * Mathf.Max(0f, springLineSpacing);
            Rect loopRect = SpringLineGeometry.Expand(hole, offset);
            if (loopRect.width <= 0f || loopRect.height <= 0f)
                continue;

            SpringLineGeometry.BuildLoop(loopRect, settings, i, animating, time, springFlowSpeed, springPulseAmount, springPulseSpeed, springPoints);
            SpringLineGeometry.AddClosedStroke(vh, springPoints, width, lineColor);
        }
    }

    private SpringLineGeometry.Settings CreateSpringSettings()
    {
        return new SpringLineGeometry.Settings
        {
            shape = SpringLineGeometry.Shape.RoundedRect,
            samplesPerLine = Mathf.Clamp(springSamplesPerLine, SpringLineGeometry.MinSamplesPerLine, SpringLineGeometry.MaxSamplesPerLine),
            sharpness = Mathf.Clamp(springSharpness, 2f, 12f),
            wobbleAmplitude = Mathf.Max(0f, springWobbleAmplitude),
            waveCount = Mathf.Clamp(springWaveCount, 1, 32),
            scribbleAmount = Mathf.Max(0f, springScribbleAmount),
            tangentWobble = 0.18f,
            linePhaseOffset = 0.045f,
            seed = 17
        };
    }

    protected void AddQuad(VertexHelper vh, Rect rect, Color32 quadColor)
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
