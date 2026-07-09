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

    private const float TwoPi = 6.28318530718f;

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
    [SerializeField, Range(1, 30)] private int animationFramesPerSecond = 12;
    [SerializeField, Min(0f)] private float redrawInterval = 0f;

    [Header("Hover绑定")]
    [SerializeField] private GameObject hoverTarget;
    [SerializeField] private bool bindHoverTarget = true;
    [SerializeField] private bool hideOnAwake = true;

    private readonly List<Vector2> points = new List<Vector2>(256);
    private float animationTime;
    private float visibleAnimationTime;
    private float redrawTimer;

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;

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

        Rect rect = GetPixelAdjustedRect();
        if (rect.width <= 0f || rect.height <= 0f || lineWidth <= 0f)
            return;

        Color32 lineColor = color;
        int count = Mathf.Clamp(lineCount, 1, 8);
        for (int i = 0; i < count; i++)
        {
            float loopOutset = outset + i * lineSpacing;
            Rect loopRect = Expand(rect, loopOutset + lineWidth * 0.5f);
            if (loopRect.width <= 0f || loopRect.height <= 0f)
                continue;

            BuildLoopPoints(loopRect, i);
            if (fillEnabled && i == 0)
                AddFilledShape(vh, points, loopRect.center, fillColor);

            AddClosedStroke(vh, points, lineWidth, lineColor);
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

    public void SetFill(bool enabled, Color value)
    {
        fillEnabled = enabled;
        fillColor = value;
        SetVerticesDirty();
    }

    public void SetFillEnabled(bool enabled)
    {
        fillEnabled = enabled;
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

    private static Rect Expand(Rect rect, float amount)
    {
        return new Rect(rect.xMin - amount, rect.yMin - amount, rect.width + amount * 2f, rect.height + amount * 2f);
    }

    private void BuildLoopPoints(Rect rect, int lineIndex)
    {
        points.Clear();

        int sampleCount = Mathf.Clamp(samplesPerLine, 16, 256);
        bool runtimeAnimating = Application.isPlaying && animate;
        float frameTime = runtimeAnimating ? visibleAnimationTime : 0f;
        float phase = frameTime * flowSpeed;
        float pulse = 1f;
        if (runtimeAnimating && pulseAmount > 0f && pulseSpeed > 0f)
            pulse += Mathf.Sin(frameTime * pulseSpeed + lineIndex * 0.91f) * pulseAmount;

        float amplitude = Mathf.Max(0f, wobbleAmplitude * Mathf.Max(0f, pulse));
        float lineOffset = lineIndex * linePhaseOffset + seed * 0.0017f;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleCount;
            float sampleT = Mathf.Repeat(t + lineOffset, 1f);
            GetShapeSample(rect, sampleT, sampleCount, out Vector2 point, out Vector2 tangent, out Vector2 normal);

            float wave = Mathf.Sin((t * waveCount + phase + lineIndex * 0.23f) * TwoPi);
            float secondaryWave = Mathf.Sin((t * (waveCount * 0.47f + 1f) - phase * 1.63f + lineIndex * 0.41f + seed * 0.013f) * TwoPi);
            float scribble = Mathf.Sin((t * 19.7f + lineIndex * 1.31f + seed * 0.071f) * TwoPi);
            scribble += Mathf.Sin((t * 37.3f - lineIndex * 0.73f + seed * 0.029f) * TwoPi) * 0.5f;

            float normalOffset = amplitude * (wave * 0.68f + secondaryWave * 0.32f) + scribble * scribbleAmount;
            float tangentOffset = normalOffset * tangentWobble * Mathf.Sin((t * 13f + lineIndex * 0.37f + seed * 0.011f) * TwoPi);
            points.Add(point + normal * normalOffset + tangent * tangentOffset);
        }
    }

    private void GetShapeSample(Rect rect, float t, int sampleCount, out Vector2 point, out Vector2 tangent, out Vector2 normal)
    {
        point = GetShapePoint(rect, t);
        Vector2 next = GetShapePoint(rect, t + 1f / (sampleCount * 2f));
        tangent = next - point;
        if (tangent.sqrMagnitude <= 0.0001f)
            tangent = Vector2.up;
        else
            tangent.Normalize();

        normal = new Vector2(tangent.y, -tangent.x);
        if (normal.sqrMagnitude <= 0.0001f)
            normal = Vector2.right;
        else
            normal.Normalize();
    }

    private Vector2 GetShapePoint(Rect rect, float t)
    {
        float angle = Mathf.Repeat(t, 1f) * TwoPi;
        float cos = Mathf.Cos(angle);
        float sin = Mathf.Sin(angle);
        Vector2 center = rect.center;
        float halfWidth = rect.width * 0.5f;
        float halfHeight = rect.height * 0.5f;

        if (shape == HighlightShape.Ellipse)
            return center + new Vector2(cos * halfWidth, sin * halfHeight);

        float exponent = 2f / Mathf.Max(2f, roundedRectSharpness);
        float x = SignedPower(cos, exponent) * halfWidth;
        float y = SignedPower(sin, exponent) * halfHeight;
        return center + new Vector2(x, y);
    }

    private static float SignedPower(float value, float exponent)
    {
        if (value == 0f)
            return 0f;

        return Mathf.Sign(value) * Mathf.Pow(Mathf.Abs(value), exponent);
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

    private void AddClosedStroke(VertexHelper vh, List<Vector2> strokePoints, float width, Color32 lineColor)
    {
        int pointCount = strokePoints.Count;
        if (pointCount < 2)
            return;

        for (int i = 0; i < pointCount; i++)
        {
            Vector2 start = strokePoints[i];
            Vector2 end = strokePoints[(i + 1) % pointCount];
            AddStrokeSegment(vh, start, end, width, lineColor);
        }
    }

    private static void AddStrokeSegment(VertexHelper vh, Vector2 start, Vector2 end, float width, Color32 lineColor)
    {
        Vector2 direction = end - start;
        if (direction.sqrMagnitude <= 0.0001f)
            return;

        direction.Normalize();
        Vector2 normal = new Vector2(-direction.y, direction.x) * (width * 0.5f);
        int vertexIndex = vh.currentVertCount;
        vh.AddVert(start - normal, lineColor, Vector2.zero);
        vh.AddVert(start + normal, lineColor, Vector2.zero);
        vh.AddVert(end + normal, lineColor, Vector2.zero);
        vh.AddVert(end - normal, lineColor, Vector2.zero);
        vh.AddTriangle(vertexIndex, vertexIndex + 1, vertexIndex + 2);
        vh.AddTriangle(vertexIndex + 2, vertexIndex + 3, vertexIndex);
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
