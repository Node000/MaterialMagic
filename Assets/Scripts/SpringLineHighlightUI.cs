using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
[AddComponentMenu("UI/Spring Line Highlight")]
public class SpringLineHighlightUI : MaskableGraphic
{
    public enum HighlightShape
    {
        RoundedRect,
        Ellipse
    }

    /// <summary>
    /// UI 上通用的弹簧线步进帧率：以道具栏（MagicSlot_PC 的槽位框与 Hover 框）为准，
    /// 结算槽位、道具强化 / 箭头附魔选项等由脚本生成的线框统一取该值，保证各处观感一致。
    /// </summary>
    public const int StandardSteppedFrameRate = 3;

    [Header("形状")]
    [SerializeField] private HighlightShape shape = HighlightShape.RoundedRect;
    [SerializeField, Range(1, 8)] private int lineCount = 4;
    [SerializeField, Range(16, 256)] private int samplesPerLine = 120;
    [SerializeField, Min(0.5f)] private float lineWidth = 4f;
    [SerializeField] private float outset = 14f;
    [SerializeField, Min(0f)] private float lineSpacing = 4f;
    [SerializeField, Range(2f, 12f)] private float roundedRectSharpness = 5f;

    [Header("填充")]
    [SerializeField] private bool fillEnabled;
    [SerializeField] private Color fillColor = Color.white;

    [Header("弹簧线")]
    [SerializeField, Min(0f)] private float wobbleAmplitude = 8f;
    [SerializeField, Range(1, 32)] private int waveCount = 7;
    [SerializeField, Min(0f)] private float scribbleAmount = 3f;
    [SerializeField, Range(0f, 1f)] private float tangentWobble = 0.18f;
    [SerializeField, Range(0f, 0.25f)] private float linePhaseOffset = 0.045f;
    [SerializeField] private int seed = 17;

    [Header("动画")]
    [SerializeField] private bool animate = true;
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField, Min(0f)] private float flowSpeed = 0.7f;
    [SerializeField, Range(0f, 1f)] private float pulseAmount = 0.14f;
    [SerializeField, Min(0f)] private float pulseSpeed = 2.2f;
    [SerializeField] private bool steppedAnimation = true;
    [Tooltip("步进帧率。运行时统一收敛为 StandardSteppedFrameRate（3fps，与道具栏一致），此值仅作编辑器预览。")]
    [SerializeField, Range(1, 30)] private int animationFramesPerSecond = 3;
    [SerializeField, Min(0f)] private float redrawInterval = 0f;

    [Header("Hover绑定")]
    [SerializeField] private GameObject hoverTarget;
    [SerializeField] private bool bindHoverTarget = true;
    [SerializeField] private bool hideOnAwake = true;

    private readonly List<Vector2> points = new List<Vector2>(256);
    private float animationTime;
    private float visibleAnimationTime;
    private float redrawTimer;
    private bool renderingEnabled = true;

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;

        // 全项目 UI 线框步进帧率统一为 3fps（以道具栏 MagicSlot_PC 为准）：
        // 场景 / 预制体里存的历史值（12、8、4）不再决定表现，避免各处快慢不一。
        if (Application.isPlaying && steppedAnimation)
            animationFramesPerSecond = StandardSteppedFrameRate;

        if (!Application.isPlaying)
            return;

        BindHoverTarget();

        if (hideOnAwake)
            gameObject.SetActive(false);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (!Application.isPlaying)
        {
            animationTime = 0f;
            visibleAnimationTime = 0f;
        }
        else
        {
            visibleAnimationTime = animationTime;
        }

        redrawTimer = 0f;
        SetVerticesDirty();
    }

    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        SetVerticesDirty();
    }

    private void Reset()
    {
        raycastTarget = false;
        color = Color.white;
        hoverTarget = transform.parent != null ? transform.parent.gameObject : null;
    }

    private void Update()
    {
        if (!Application.isPlaying || !animate)
            return;

        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        animationTime += deltaTime;
        if (animationTime > 10000f)
        {
            animationTime = 0f;
            visibleAnimationTime = 0f;
        }

        if (steppedAnimation)
        {
            float frameInterval = 1f / Mathf.Max(1, animationFramesPerSecond);
            redrawTimer += deltaTime;
            if (redrawTimer < frameInterval)
                return;

            redrawTimer %= frameInterval;
            visibleAnimationTime = animationTime;
            SetVerticesDirty();
            return;
        }

        visibleAnimationTime = animationTime;
        if (redrawInterval <= 0f)
        {
            SetVerticesDirty();
            return;
        }

        redrawTimer += deltaTime;
        if (redrawTimer >= redrawInterval)
        {
            redrawTimer = 0f;
            SetVerticesDirty();
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (!renderingEnabled)
            return;

        Rect rect = GetPixelAdjustedRect();
        if (rect.width <= 0f || rect.height <= 0f || lineWidth <= 0f)
            return;

        Color32 lineColor = color;
        int count = Mathf.Clamp(lineCount, 1, 8);
        for (int i = 0; i < count; i++)
        {
            float loopOutset = outset + i * lineSpacing;
            Rect loopRect = SpringLineGeometry.Expand(rect, loopOutset + lineWidth * 0.5f);
            if (loopRect.width <= 0f || loopRect.height <= 0f)
                continue;

            BuildLoopPoints(loopRect, i);
            if (fillEnabled && i == 0)
                AddFilledShape(vh, points, loopRect.center, fillColor);

            SpringLineGeometry.AddClosedStroke(vh, points, lineWidth, lineColor);
        }
    }

    public void SetAnimating(bool value)
    {
        animate = value;
        SetVerticesDirty();
    }

    public void SetShape(HighlightShape value)
    {
        shape = value;
        SetVerticesDirty();
    }

    public void SetOutset(float value)
    {
        outset = value;
        SetVerticesDirty();
    }

    public void SetWobbleAmplitude(float value)
    {
        wobbleAmplitude = Mathf.Max(0f, value);
        SetVerticesDirty();
    }

    public void SetLineCount(int value)
    {
        lineCount = Mathf.Clamp(value, 1, 8);
        SetVerticesDirty();
    }

    public void SetSamplesPerLine(int value)
    {
        samplesPerLine = Mathf.Clamp(value, 16, 256);
        SetVerticesDirty();
    }

    public void SetLineWidth(float value)
    {
        lineWidth = Mathf.Max(0.5f, value);
        SetVerticesDirty();
    }

    public void SetLineSpacing(float value)
    {
        lineSpacing = Mathf.Max(0f, value);
        SetVerticesDirty();
    }

    /// <summary>步进动画帧率（仅在 steppedAnimation 为真时生效）。</summary>
    public void SetAnimationFramesPerSecond(int value)
    {
        animationFramesPerSecond = Mathf.Clamp(value, 1, 30);
        SetVerticesDirty();
    }

    /// <summary>当前线条数量（只读），供按比例派生的小线框使用。</summary>
    public int LineCount => lineCount;

    public void SetFill(bool enabled, Color value)
    {
        fillEnabled = enabled;
        fillColor = value;
        SetVerticesDirty();
    }

    public bool FillEnabled => fillEnabled;

    public void SetRenderingEnabled(bool value)
    {
        renderingEnabled = value;
        SetVerticesDirty();
    }

    public void SetFillEnabled(bool enabled)
    {
        fillEnabled = enabled;
        SetVerticesDirty();
    }

    public void CopyVisualSettingsFrom(SpringLineHighlightUI source)
    {
        shape = source.shape;
        lineCount = source.lineCount;
        samplesPerLine = source.samplesPerLine;
        lineWidth = source.lineWidth;
        outset = source.outset;
        lineSpacing = source.lineSpacing;
        roundedRectSharpness = source.roundedRectSharpness;
        fillEnabled = source.fillEnabled;
        fillColor = source.fillColor;
        wobbleAmplitude = source.wobbleAmplitude;
        waveCount = source.waveCount;
        scribbleAmount = source.scribbleAmount;
        tangentWobble = source.tangentWobble;
        linePhaseOffset = source.linePhaseOffset;
        seed = source.seed;
        animate = source.animate;
        useUnscaledTime = source.useUnscaledTime;
        flowSpeed = source.flowSpeed;
        pulseAmount = source.pulseAmount;
        pulseSpeed = source.pulseSpeed;
        steppedAnimation = source.steppedAnimation;
        animationFramesPerSecond = source.animationFramesPerSecond;
        redrawInterval = source.redrawInterval;
        color = source.color;
        material = source.material;
        maskable = source.maskable;
        raycastTarget = false;
        hoverTarget = null;
        bindHoverTarget = false;
        hideOnAwake = false;
        SetVerticesDirty();
    }

    public void SetHideOnAwake(bool value)
    {
        hideOnAwake = value;
    }

    public void SetBindHoverTarget(bool value)
    {
        bindHoverTarget = value;
    }

    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void SetHoverTarget(GameObject target)
    {
        hoverTarget = target;
        BindHoverTarget();
    }

    public void BindHoverTarget()
    {
        if (!Application.isPlaying || !bindHoverTarget || hoverTarget == null)
            return;

        HoverHighlightTargetRelayUI relay = hoverTarget.GetComponent<HoverHighlightTargetRelayUI>();
        if (relay == null)
        {
            relay = hoverTarget.AddComponent<HoverHighlightTargetRelayUI>();
            relay.hideFlags = HideFlags.HideInInspector;
        }

        relay.Register(gameObject);
    }

    private SpringLineGeometry.Settings CreateGeometrySettings()
    {
        return new SpringLineGeometry.Settings
        {
            shape = shape == HighlightShape.Ellipse ? SpringLineGeometry.Shape.Ellipse : SpringLineGeometry.Shape.RoundedRect,
            samplesPerLine = samplesPerLine,
            sharpness = roundedRectSharpness,
            wobbleAmplitude = wobbleAmplitude,
            waveCount = waveCount,
            scribbleAmount = scribbleAmount,
            tangentWobble = tangentWobble,
            linePhaseOffset = linePhaseOffset,
            seed = seed
        };
    }

    private void BuildLoopPoints(Rect rect, int lineIndex)
    {
        points.Clear();

        bool runtimeAnimating = Application.isPlaying && animate;
        float frameTime = runtimeAnimating ? visibleAnimationTime : 0f;
        SpringLineGeometry.BuildLoop(rect, CreateGeometrySettings(), lineIndex, runtimeAnimating, frameTime, flowSpeed, pulseAmount, pulseSpeed, points);
    }

    private void AddFilledShape(VertexHelper vh, List<Vector2> shapePoints, Vector2 center, Color32 shapeFillColor)
    {
        int pointCount = shapePoints.Count;
        if (pointCount < 3)
            return;

        int centerIndex = vh.currentVertCount;
        vh.AddVert(center, shapeFillColor, Vector2.zero);
        for (int i = 0; i < pointCount; i++)
            vh.AddVert(shapePoints[i], shapeFillColor, Vector2.zero);

        for (int i = 0; i < pointCount; i++)
        {
            int current = centerIndex + 1 + i;
            int next = centerIndex + 1 + ((i + 1) % pointCount);
            vh.AddTriangle(centerIndex, current, next);
        }
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        lineCount = Mathf.Clamp(lineCount, 1, 8);
        samplesPerLine = Mathf.Clamp(samplesPerLine, 16, 256);
        lineWidth = Mathf.Max(0.5f, lineWidth);
        lineSpacing = Mathf.Max(0f, lineSpacing);
        roundedRectSharpness = Mathf.Clamp(roundedRectSharpness, 2f, 12f);
        fillColor.a = Mathf.Clamp01(fillColor.a);
        wobbleAmplitude = Mathf.Max(0f, wobbleAmplitude);
        waveCount = Mathf.Clamp(waveCount, 1, 32);
        scribbleAmount = Mathf.Max(0f, scribbleAmount);
        tangentWobble = Mathf.Clamp01(tangentWobble);
        linePhaseOffset = Mathf.Clamp(linePhaseOffset, 0f, 0.25f);
        pulseAmount = Mathf.Clamp01(pulseAmount);
        pulseSpeed = Mathf.Max(0f, pulseSpeed);
        animationFramesPerSecond = Mathf.Clamp(animationFramesPerSecond, 1, 30);
        redrawInterval = Mathf.Max(0f, redrawInterval);
        SetVerticesDirty();
    }
#endif
}
