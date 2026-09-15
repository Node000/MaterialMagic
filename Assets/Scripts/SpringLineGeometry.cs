using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 抖动手绘线（弹簧线）的纯几何工具：<see cref="SpringLineHighlightUI"/> 与教程高亮框
/// （<see cref="TutorialCutoutMaskUI"/>）共用同一套数学，保证两处观感一致。
/// 只做计算与 VertexHelper 写入、不持有状态；数值口径与 SpringLineHighlightUI 的历史实现完全一致。
/// </summary>
public static class SpringLineGeometry
{
    public const int MinLineCount = 1;
    public const int MaxLineCount = 8;
    public const int MinSamplesPerLine = 16;
    public const int MaxSamplesPerLine = 256;

    private const float TwoPi = 6.28318530718f;

    /// <summary>闭环形状。</summary>
    public enum Shape
    {
        RoundedRect = 0,
        Ellipse = 1
    }

    /// <summary>
    /// 抖动相关参数（形状、采样、噪声）。线宽、圈数、圈间距、外扩由调用方决定，不在这里。
    /// </summary>
    public struct Settings
    {
        public Shape shape;
        public int samplesPerLine;
        public float sharpness;
        public float wobbleAmplitude;
        public int waveCount;
        public float scribbleAmount;
        public float tangentWobble;
        public float linePhaseOffset;
        public int seed;

        /// <summary>组件默认值（与 SpringLineHighlightUI 字段默认一致）。</summary>
        public static Settings Default
        {
            get
            {
                return new Settings
                {
                    shape = Shape.RoundedRect,
                    samplesPerLine = 120,
                    sharpness = 5f,
                    wobbleAmplitude = 8f,
                    waveCount = 7,
                    scribbleAmount = 3f,
                    tangentWobble = 0.18f,
                    linePhaseOffset = 0.045f,
                    seed = 17
                };
            }
        }

    }

    /// <summary>矩形按四边外扩（负值为内收）。</summary>
    public static Rect Expand(Rect rect, float amount)
    {
        return new Rect(rect.xMin - amount, rect.yMin - amount, rect.width + amount * 2f, rect.height + amount * 2f);
    }

    /// <summary>
    /// 生成一圈抖动折线（首尾不闭合，由 <see cref="AddClosedStroke"/> 连回起点）。
    /// </summary>
    /// <param name="rect">该圈线所在的基准矩形（线的中心线贴着它的边）。</param>
    /// <param name="lineIndex">第几条（用于错开相位，1 条以上时不要传相同值）。</param>
    /// <param name="animating">是否随时间流动；false 时用 time=0 的静止形状。</param>
    public static void BuildLoop(Rect rect, in Settings settings, int lineIndex, bool animating, float time, float flowSpeed, float pulseAmount, float pulseSpeed, List<Vector2> points)
    {
        if (points == null)
            return;

        points.Clear();

        int sampleCount = Mathf.Clamp(settings.samplesPerLine, MinSamplesPerLine, MaxSamplesPerLine);
        float phase = animating ? time * flowSpeed : 0f;
        float pulse = 1f;
        if (animating && pulseAmount > 0f && pulseSpeed > 0f)
            pulse += Mathf.Sin(time * pulseSpeed + lineIndex * 0.91f) * pulseAmount;

        float amplitude = Mathf.Max(0f, settings.wobbleAmplitude * Mathf.Max(0f, pulse));
        float waveCount = Mathf.Max(1f, settings.waveCount);
        float scribbleAmount = Mathf.Max(0f, settings.scribbleAmount);
        float lineOffset = lineIndex * settings.linePhaseOffset + settings.seed * 0.0017f;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleCount;
            float sampleT = Mathf.Repeat(t + lineOffset, 1f);
            GetShapeSample(rect, settings.shape, settings.sharpness, sampleT, sampleCount, out Vector2 point, out Vector2 tangent, out Vector2 normal);

            float wave = Mathf.Sin((t * waveCount + phase + lineIndex * 0.23f) * TwoPi);
            float secondaryWave = Mathf.Sin((t * (waveCount * 0.47f + 1f) - phase * 1.63f + lineIndex * 0.41f + settings.seed * 0.013f) * TwoPi);
            float scribble = Mathf.Sin((t * 19.7f + lineIndex * 1.31f + settings.seed * 0.071f) * TwoPi);
            scribble += Mathf.Sin((t * 37.3f - lineIndex * 0.73f + settings.seed * 0.029f) * TwoPi) * 0.5f;

            float normalOffset = amplitude * (wave * 0.68f + secondaryWave * 0.32f) + scribble * scribbleAmount;
            float tangentOffset = normalOffset * settings.tangentWobble * Mathf.Sin((t * 13f + lineIndex * 0.37f + settings.seed * 0.011f) * TwoPi);
            points.Add(point + normal * normalOffset + tangent * tangentOffset);
        }
    }

    private static void GetShapeSample(Rect rect, Shape shape, float sharpness, float t, int sampleCount, out Vector2 point, out Vector2 tangent, out Vector2 normal)
    {
        point = GetShapePoint(rect, shape, sharpness, t);
        Vector2 next = GetShapePoint(rect, shape, sharpness, t + 1f / (sampleCount * 2f));
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

    private static Vector2 GetShapePoint(Rect rect, Shape shape, float sharpness, float t)
    {
        float angle = Mathf.Repeat(t, 1f) * TwoPi;
        float cos = Mathf.Cos(angle);
        float sin = Mathf.Sin(angle);
        Vector2 center = rect.center;
        float halfWidth = rect.width * 0.5f;
        float halfHeight = rect.height * 0.5f;

        if (shape == Shape.Ellipse)
            return center + new Vector2(cos * halfWidth, sin * halfHeight);

        float exponent = 2f / Mathf.Max(2f, sharpness);
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

    /// <summary>把一圈点描成闭合粗线（每条边一个 quad）。</summary>
    public static void AddClosedStroke(VertexHelper vh, List<Vector2> strokePoints, float width, Color32 lineColor)
    {
        if (vh == null || strokePoints == null)
            return;

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
}
