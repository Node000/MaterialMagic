using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum JaggedWaveHighlightMode
{
    Jagged = 0,
    Spectrum = 1
}

[AddComponentMenu("UI/Jagged Wave Highlight")]
public class JaggedWaveHighlightUI : MaskableGraphic
{

    [Header("模式")]
    [SerializeField] private JaggedWaveHighlightMode mode = JaggedWaveHighlightMode.Jagged;

    [Header("线框")]
    [SerializeField, Min(1)] private int lineCount = 2;
    [SerializeField, Min(0.5f)] private float lineWidth = 3f;
    [SerializeField, Min(0f)] private float lineSpacing = 7f;
    [SerializeField, Min(0f)] private float inset = 4f;

    [Header("填充")]
    [SerializeField] private bool fillEnabled;
    [SerializeField] private Color fillColor = Color.white;

    [Header("锯齿波形")]
    [SerializeField, Range(1, 80)] private int teethPerSide = 18;
    [SerializeField, Min(0f)] private float amplitude = 6f;
    [SerializeField] private float flowSpeed = 0.8f;
    [SerializeField, Range(0f, 1f)] private float pulseAmount = 0.2f;
    [SerializeField] private float pulseSpeed = 2.5f;
    [SerializeField] private bool animate = true;
    [SerializeField] private bool useUnscaledTime = true;

    [Header("BGM驱动")]
    [SerializeField] private bool driveFromBgm;
    [SerializeField] private AudioSource audioSource;
    [SerializeField, Range(4, 64)] private int spectrumSamples = 16;
    [SerializeField, Range(0, 63)] private int spectrumStartBin = 3;
    [SerializeField, Range(0f, 1f)] private float spectrumCutoff = 0.02f;
    [SerializeField, Min(0f)] private float audioSensitivity = 45f;
    [SerializeField, Min(0f)] private float audioAmplitudeRange = 14f;
    [SerializeField, Min(0f)] private float audioSmoothing = 12f;
    [SerializeField, Range(16, 256)] private int spectrumPointCount = 96;
    [SerializeField, Range(0.25f, 3f)] private float spectrumContrast = 1.25f;

    [Header("Hover绑定")]
    [SerializeField] private RectTransform hoverTarget;
    [SerializeField] private Vector2 hoverPadding = new Vector2(18f, 14f);
    [SerializeField] private bool showOnlyOnTargetHover = true;
    [SerializeField] private bool visibleOnStart;
    [SerializeField] private bool followHoverTargetRect = true;
    [SerializeField] private bool hideWithSetActive;
    [SerializeField] private bool disableOtherGraphics = true;

    private readonly List<Vector2> points = new List<Vector2>(400);
    private readonly List<Graphic> sameObjectGraphics = new List<Graphic>(4);
    private readonly Vector3[] hoverTargetWorldCorners = new Vector3[4];
    private readonly float[] spectrum = new float[64];
    private readonly float[] smoothedSpectrum = new float[64];
    private RectTransform cachedRectTransform;
    private HoverHighlightTargetRelayUI hoverRelay;
    private float phase;
    private float audioLevel;

    protected override void Awake()
    {
        base.Awake();
        cachedRectTransform = rectTransform;
        raycastTarget = false;

        if (!Application.isPlaying)
            return;

        ConfigureSameObjectGraphics();
        RegisterHoverTarget();
        if (showOnlyOnTargetHover && hoverTarget != null)
            SetHoverVisible(visibleOnStart);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        cachedRectTransform = rectTransform;
        if (followHoverTargetRect && hoverTarget != null)
            AlignToHoverTarget();
    }

    protected override void OnDestroy()
    {
        if (hoverRelay != null)
            hoverRelay.Unregister(this);

        base.OnDestroy();
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying)
            return;

        if (followHoverTargetRect && hoverTarget != null)
            AlignToHoverTarget();
    }

    private void Update()
    {
        if (!Application.isPlaying || (!animate && !driveFromBgm))
            return;

        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (driveFromBgm)
            UpdateAudioLevel(deltaTime);

        if (animate)
        {
            phase += deltaTime;
            if (phase > 10000f)
                phase = 0f;
        }

        if (driveFromBgm || flowSpeed != 0f || pulseAmount > 0f)
            SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        Rect rect = GetPixelAdjustedRect();
        if (rect.width <= 0f || rect.height <= 0f || lineWidth <= 0f)
            return;

        Color32 lineColor = color;
        int count = Mathf.Max(1, lineCount);
        for (int i = 0; i < count; i++)
        {
            float lineInset = inset + i * lineSpacing + lineWidth * 0.5f;
            Rect lineRect = new Rect(rect.xMin + lineInset, rect.yMin + lineInset, rect.width - lineInset * 2f, rect.height - lineInset * 2f);
            if (lineRect.width <= lineWidth || lineRect.height <= lineWidth)
                break;

            points.Clear();
            float linePhase = Application.isPlaying ? phase * flowSpeed + i * 0.37f : i * 0.37f;
            if (mode == JaggedWaveHighlightMode.Spectrum)
                AddSpectrumRectPoints(lineRect, linePhase);
            else
                AddJaggedRectPoints(lineRect, GetAnimatedAmplitude(i), linePhase);

            if (fillEnabled && i == 0)
                AddFilledShape(vh, points, lineRect.center, fillColor);

            AddClosedStroke(vh, points, lineWidth, lineColor);
        }
    }

    public void SetAmplitude(float value)
    {
        amplitude = Mathf.Max(0f, value);
        SetVerticesDirty();
    }

    public void SetAnimating(bool value)
    {
        animate = value;
        SetVerticesDirty();
    }

    public void SetDriveFromBgm(bool value)
    {
        driveFromBgm = value;
        SetVerticesDirty();
    }

    public void SetAudioSource(AudioSource source)
    {
        audioSource = source;
        SetVerticesDirty();
    }

    public void SetHoverTarget(RectTransform target)
    {
        if (hoverRelay != null)
        {
            hoverRelay.Unregister(this);
            hoverRelay = null;
        }

        hoverTarget = target;
        if (Application.isPlaying)
            RegisterHoverTarget();
        if (followHoverTargetRect && hoverTarget != null)
            AlignToHoverTarget();
    }

    internal void SetHoverVisibleFromTarget(RectTransform target, bool visible)
    {
        if (!Application.isPlaying || target != hoverTarget)
            return;

        if (visible && followHoverTargetRect)
            AlignToHoverTarget();
        SetHoverVisible(visible);
    }

    private void RegisterHoverTarget()
    {
        if (!Application.isPlaying || !showOnlyOnTargetHover || hoverTarget == null)
            return;

        hoverRelay = hoverTarget.GetComponent<HoverHighlightTargetRelayUI>();
        if (hoverRelay == null)
        {
            hoverRelay = hoverTarget.gameObject.AddComponent<HoverHighlightTargetRelayUI>();
            hoverRelay.hideFlags = HideFlags.HideInInspector;
        }
        hoverRelay.Register(this);
    }

    private void SetHoverVisible(bool visible)
    {
        if (!Application.isPlaying)
            return;

        if (hideWithSetActive)
        {
            if (gameObject.activeSelf != visible)
                gameObject.SetActive(visible);
            return;
        }

        enabled = visible;
    }

    private void AlignToHoverTarget()
    {
        if (cachedRectTransform == null)
            cachedRectTransform = rectTransform;

        RectTransform parent = cachedRectTransform.parent as RectTransform;
        if (parent == null)
            return;

        hoverTarget.GetWorldCorners(hoverTargetWorldCorners);
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        for (int i = 0; i < hoverTargetWorldCorners.Length; i++)
        {
            Vector2 localPoint = parent.InverseTransformPoint(hoverTargetWorldCorners[i]);
            min = Vector2.Min(min, localPoint);
            max = Vector2.Max(max, localPoint);
        }

        cachedRectTransform.anchorMin = parent.pivot;
        cachedRectTransform.anchorMax = parent.pivot;
        cachedRectTransform.pivot = new Vector2(0.5f, 0.5f);
        cachedRectTransform.anchoredPosition = (min + max) * 0.5f;
        cachedRectTransform.sizeDelta = max - min + hoverPadding * 2f;
        SetVerticesDirty();
    }

    private void ConfigureSameObjectGraphics()
    {
        sameObjectGraphics.Clear();
        GetComponents(sameObjectGraphics);
        for (int i = 0; i < sameObjectGraphics.Count; i++)
        {
            Graphic graphic = sameObjectGraphics[i];
            if (graphic == null || graphic == this)
                continue;

            graphic.raycastTarget = false;
            if (disableOtherGraphics && hoverTarget != cachedRectTransform)
                graphic.enabled = false;
        }
    }

    private void UpdateAudioLevel(float deltaTime)
    {
        if (audioSource == null && AudioManager.Instance != null)
            audioSource = AudioManager.Instance.MusicSource;

        float t = audioSmoothing <= 0f ? 1f : 1f - Mathf.Exp(-audioSmoothing * deltaTime);
        bool hasPlayingSource = audioSource != null && audioSource.isPlaying;
        if (hasPlayingSource)
            audioSource.GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris);

        float targetLevel = 0f;
        int startBin = Mathf.Clamp(spectrumStartBin, 0, smoothedSpectrum.Length - 1);
        int count = Mathf.Clamp(spectrumSamples, 1, smoothedSpectrum.Length - startBin);
        for (int i = 0; i < spectrum.Length; i++)
        {
            float target = hasPlayingSource ? ApplySpectrumCutoff(spectrum[i]) : 0f;
            smoothedSpectrum[i] = Mathf.Lerp(smoothedSpectrum[i], target, t);
            if (i >= startBin && i < startBin + count)
                targetLevel += smoothedSpectrum[i];
        }

        targetLevel = Mathf.Clamp01(targetLevel * audioSensitivity);
        audioLevel = Mathf.Lerp(audioLevel, targetLevel, t);
    }

    private float GetAnimatedAmplitude(int lineIndex)
    {
        float baseAmplitude = Mathf.Max(0f, amplitude) + audioLevel * audioAmplitudeRange;
        if (!Application.isPlaying || !animate || pulseAmount <= 0f || pulseSpeed == 0f)
            return baseAmplitude;

        float pulse = 1f + Mathf.Sin(phase * pulseSpeed + lineIndex * 0.9f) * pulseAmount;
        return baseAmplitude * Mathf.Max(0f, pulse);
    }

    private void AddSpectrumRectPoints(Rect rect, float linePhase)
    {
        int pointCount = Mathf.Max(16, spectrumPointCount);
        float spectrumOffset = linePhase * 0.08f;
        float handDrawnOffset = linePhase * 0.35f;
        for (int i = 0; i < pointCount; i++)
        {
            float t = i / (float)pointCount;
            GetRectPointAndNormal(rect, t, out Vector2 point, out Vector2 normal);
            float spectrumWave = GetSpectrumValue(t + spectrumOffset) * audioAmplitudeRange;
            float baseWave = amplitude > 0f ? GetJaggedWave(t, handDrawnOffset) * amplitude : 0f;
            points.Add(point + normal * (spectrumWave + baseWave));
        }
    }

    private void GetRectPointAndNormal(Rect rect, float t, out Vector2 point, out Vector2 normal)
    {
        t -= Mathf.Floor(t);
        float width = Mathf.Max(0.001f, rect.width);
        float height = Mathf.Max(0.001f, rect.height);
        float distance = t * (width + height) * 2f;

        if (distance < width)
        {
            point = new Vector2(rect.xMin + distance, rect.yMax);
            normal = Vector2.up;
        }
        else if (distance < width + height)
        {
            point = new Vector2(rect.xMax, rect.yMax - (distance - width));
            normal = Vector2.right;
        }
        else if (distance < width * 2f + height)
        {
            point = new Vector2(rect.xMax - (distance - width - height), rect.yMin);
            normal = Vector2.down;
        }
        else
        {
            point = new Vector2(rect.xMin, rect.yMin + (distance - width * 2f - height));
            normal = Vector2.left;
        }
    }

    private float GetSpectrumValue(float t)
    {
        int startBin = Mathf.Clamp(spectrumStartBin, 0, smoothedSpectrum.Length - 1);
        int count = Mathf.Clamp(spectrumSamples, 1, smoothedSpectrum.Length - startBin);
        if (count <= 1)
            return audioLevel;

        t -= Mathf.Floor(t);
        float sample = startBin + t * (count - 1);
        int lower = Mathf.Clamp((int)sample, startBin, startBin + count - 1);
        int upper = Mathf.Min(lower + 1, startBin + count - 1);
        float value = Mathf.Lerp(smoothedSpectrum[lower], smoothedSpectrum[upper], sample - lower) * audioSensitivity;
        return Mathf.Pow(Mathf.Clamp01(value), spectrumContrast);
    }

    private float ApplySpectrumCutoff(float value)
    {
        if (spectrumCutoff <= 0f)
            return value;

        if (value <= spectrumCutoff)
            return 0f;

        return (value - spectrumCutoff) / (1f - spectrumCutoff);
    }

    private void AddJaggedRectPoints(Rect rect, float waveAmplitude, float linePhase)
    {
        int subdivisions = Mathf.Max(1, teethPerSide) * 2;
        AppendSide(rect, new Vector2(rect.xMin, rect.yMax), new Vector2(rect.xMax, rect.yMax), Vector2.up, subdivisions, waveAmplitude, linePhase);
        AppendSide(rect, new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMax, rect.yMin), Vector2.right, subdivisions, waveAmplitude, linePhase + 0.25f);
        AppendSide(rect, new Vector2(rect.xMax, rect.yMin), new Vector2(rect.xMin, rect.yMin), Vector2.down, subdivisions, waveAmplitude, linePhase + 0.5f);
        AppendSide(rect, new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMin, rect.yMax), Vector2.left, subdivisions, waveAmplitude, linePhase + 0.75f);
    }

    private void AppendSide(Rect rect, Vector2 from, Vector2 to, Vector2 normal, int subdivisions, float waveAmplitude, float linePhase)
    {
        for (int i = 0; i <= subdivisions; i++)
        {
            if (points.Count > 0 && i == 0)
                continue;

            float t = i / (float)subdivisions;
            float wave = GetJaggedWave(t, linePhase) * waveAmplitude * GetCornerFade(t);
            Vector2 point = Vector2.Lerp(from, to, t) + normal * wave;
            point.x = Mathf.Clamp(point.x, rect.xMin - waveAmplitude, rect.xMax + waveAmplitude);
            point.y = Mathf.Clamp(point.y, rect.yMin - waveAmplitude, rect.yMax + waveAmplitude);
            points.Add(point);
        }
    }

    private float GetJaggedWave(float t, float linePhase)
    {
        float sample = (t * Mathf.Max(1, teethPerSide) + linePhase) * 2f;
        return Mathf.PingPong(sample, 1f) * 2f - 1f;
    }

    private float GetCornerFade(float t)
    {
        float edgeDistance = Mathf.Min(t, 1f - t) * Mathf.Max(1, teethPerSide);
        return Mathf.Clamp01(edgeDistance);
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

    private void AddStrokeSegment(VertexHelper vh, Vector2 start, Vector2 end, float width, Color32 lineColor)
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
        lineCount = Mathf.Max(1, lineCount);
        lineWidth = Mathf.Max(0.5f, lineWidth);
        lineSpacing = Mathf.Max(0f, lineSpacing);
        inset = Mathf.Max(0f, inset);
        fillColor.a = Mathf.Clamp01(fillColor.a);
        amplitude = Mathf.Max(0f, amplitude);
        teethPerSide = Mathf.Clamp(teethPerSide, 1, 80);
        pulseAmount = Mathf.Clamp01(pulseAmount);
        spectrumSamples = Mathf.Clamp(spectrumSamples, 4, 64);
        spectrumStartBin = Mathf.Clamp(spectrumStartBin, 0, 63);
        spectrumCutoff = Mathf.Clamp01(spectrumCutoff);
        audioSensitivity = Mathf.Max(0f, audioSensitivity);
        audioAmplitudeRange = Mathf.Max(0f, audioAmplitudeRange);
        audioSmoothing = Mathf.Max(0f, audioSmoothing);
        spectrumPointCount = Mathf.Clamp(spectrumPointCount, 16, 256);
        spectrumContrast = Mathf.Clamp(spectrumContrast, 0.25f, 3f);
        hoverPadding.x = Mathf.Max(0f, hoverPadding.x);
        hoverPadding.y = Mathf.Max(0f, hoverPadding.y);
        SetVerticesDirty();
    }
#endif
}
