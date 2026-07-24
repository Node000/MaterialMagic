using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
[AddComponentMenu("Scribble/Scribble Plane 3D")]
public class ScribblePlane3D : MonoBehaviour
{
    public enum PlaneAxes
    {
        XY,
        XZ
    }

    public enum FillMode
    {
        ParallelScan,
        EdgeGuidedStrokes
    }

    public enum FillAreaEdge
    {
        Left,
        Right,
        Bottom,
        Top
    }

    public enum GuideCurveDirection
    {
        StartToEnd,
        EndToStart
    }

    private const float TwoPi = 6.28318530718f;

    [Header("Plane")]
    [SerializeField] private PlaneAxes planeAxes = PlaneAxes.XY;

    [Header("Scribble Fill")]
    [SerializeField] private bool fillEnabled = true;
    [SerializeField] private FillMode fillMode;
    [SerializeField] private Rect fillArea = new Rect(-1.35f, -0.75f, 2.7f, 1.5f);
    [SerializeField, Range(1, 96)] private int fillLineCount = 18;
    [SerializeField, Range(2, 128)] private int fillSamplesPerLine = 24;
    [SerializeField, Min(0.001f)] private float fillLineWidth = 0.035f;
    [SerializeField, Min(0f)] private float fillInset = 0.05f;
    [SerializeField, Range(-90f, 90f)] private float fillAngleDegrees;
    [SerializeField, Min(0f)] private float fillWobbleAmplitude = 0.06f;
    [SerializeField, Min(0f)] private float fillWobbleFrequency = 3.5f;
    [SerializeField] private Color fillVertexColor = new Color(1f, 1f, 1f, 0.55f);

    [Header("Edge Guided Bypass Strokes")]
    [FormerlySerializedAs("guidedStartEdge")]
    [SerializeField] private FillAreaEdge guidedEdge = FillAreaEdge.Left;
    [FormerlySerializedAs("guidedEdgeSpread")]
    [SerializeField, Min(0f)] private float guidedStartOffsetRange = 0.35f;
    [SerializeField, Min(0f)] private float guidedEndOffsetRange = 0.35f;
    [FormerlySerializedAs("guidedEdgeJitter")]
    [SerializeField, Min(0f)] private float guidedEdgeJitter = 0.035f;
    [SerializeField, Range(1, 16)] private int guidedStrokesPerPointMin = 2;
    [SerializeField, Range(1, 16)] private int guidedStrokesPerPointMax = 5;
    [FormerlySerializedAs("guidedLoopRadius")]
    [SerializeField, Min(0.01f)] private float guidedBypassOffset = 0.85f;
    [FormerlySerializedAs("guidedLoopRadiusJitter")]
    [SerializeField, Min(0f)] private float guidedBypassOffsetRange = 0.18f;
    [FormerlySerializedAs("guidedLoopAspect")]
    [SerializeField, Range(0.1f, 2f)] private float guidedBypassAspect = 0.7f;
    [SerializeField] private AnimationCurve guidedWobbleAmplitudeOverPath = AnimationCurve.Linear(0f, 1f, 1f, 1f);
    [SerializeField] private AnimationCurve guidedWobbleFrequencyOverPath = AnimationCurve.Linear(0f, 1f, 1f, 1f);
    [SerializeField] private GuideCurveDirection guidedCurveDirection;

    [Header("Variation")]
    [SerializeField] private int seed = 17;

    private readonly List<Vector3> vertices = new List<Vector3>(4096);
    private readonly List<Vector3> normals = new List<Vector3>(4096);
    private readonly List<Vector4> tangents = new List<Vector4>(4096);
    private readonly List<Vector2> uvs = new List<Vector2>(4096);
    private readonly List<Color> colors = new List<Color>(4096);
    private readonly List<int> triangles = new List<int>(8192);
    private readonly List<Vector2> fillPoints = new List<Vector2>(128);

    private MeshFilter meshFilter;
    private Mesh generatedMesh;

#if UNITY_EDITOR
    private int guidePointTransformHash;
#endif

    public PlaneAxes Axes => planeAxes;
    public FillMode CurrentFillMode => fillMode;
    public int GuidePointCount => transform.childCount;
    public Rect FillArea => GetNormalizedFillArea();

    public Vector2 GetGuidePoint(int index)
    {
        return ToPlanePoint(transform.GetChild(index).localPosition);
    }

    public Transform CreateGuidePoint()
    {
        GameObject guidePoint = new GameObject("Guide Point");
        Transform guideTransform = guidePoint.transform;
        guideTransform.SetParent(transform, false);
        guideTransform.localPosition = ToLocalPoint(GetNormalizedFillArea().center);
        RebuildMesh();
        return guideTransform;
    }

    private void OnEnable()
    {
        if (generatedMesh == null)
            RebuildMesh();
    }

    private void OnDestroy()
    {
        if (generatedMesh == null)
            return;

        if (Application.isPlaying)
            Destroy(generatedMesh);
        else
            DestroyImmediate(generatedMesh);
    }

#if UNITY_EDITOR
    private void Update()
    {
        if (Application.isPlaying || fillMode != FillMode.EdgeGuidedStrokes)
            return;

        int currentHash = GetGuidePointTransformHash();
        if (currentHash == guidePointTransformHash)
            return;

        guidePointTransformHash = currentHash;
        RebuildMesh();
    }
#endif

    public void SetFillArea(Rect area)
    {
        fillArea = NormalizeFillArea(area);
        RebuildMesh();
    }

    public Vector3 ToLocalPoint(Vector2 point)
    {
        return planeAxes == PlaneAxes.XY
            ? new Vector3(point.x, point.y, 0f)
            : new Vector3(point.x, 0f, point.y);
    }

    public Vector2 ToPlanePoint(Vector3 localPoint)
    {
        return planeAxes == PlaneAxes.XY
            ? new Vector2(localPoint.x, localPoint.y)
            : new Vector2(localPoint.x, localPoint.z);
    }

    public Vector3 GetLocalPlaneNormal()
    {
        return planeAxes == PlaneAxes.XY ? Vector3.forward : Vector3.up;
    }

    public Vector3 GetLocalPlaneSecondAxis()
    {
        return planeAxes == PlaneAxes.XY ? Vector3.up : Vector3.forward;
    }

    public void RebuildMesh()
    {
        NormalizeSettings();
        EnsureMesh();
        if (generatedMesh == null)
            return;

        vertices.Clear();
        normals.Clear();
        tangents.Clear();
        uvs.Clear();
        colors.Clear();
        triangles.Clear();

        if (fillEnabled)
            AddFillRibbons();

        generatedMesh.Clear();
        if (vertices.Count == 0)
            return;

        generatedMesh.indexFormat = vertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
        generatedMesh.SetVertices(vertices);
        generatedMesh.SetNormals(normals);
        generatedMesh.SetTangents(tangents);
        generatedMesh.SetUVs(0, uvs);
        generatedMesh.SetColors(colors);
        generatedMesh.SetTriangles(triangles, 0, true);
        generatedMesh.RecalculateBounds();

#if UNITY_EDITOR
        guidePointTransformHash = GetGuidePointTransformHash();
#endif
    }

    private void AddFillRibbons()
    {
        Rect area = GetNormalizedFillArea();
        area.xMin += fillInset;
        area.xMax -= fillInset;
        area.yMin += fillInset;
        area.yMax -= fillInset;
        if (area.width <= 0f || area.height <= 0f)
            return;

        if (fillMode == FillMode.EdgeGuidedStrokes)
            AddEdgeGuidedStrokes(area);
        else
            AddParallelFillRibbons(area);
    }

    private void AddParallelFillRibbons(Rect area)
    {
        float angleRadians = fillAngleDegrees * Mathf.Deg2Rad;
        Vector2 direction = new Vector2(Mathf.Cos(angleRadians), Mathf.Sin(angleRadians));
        Vector2 perpendicular = new Vector2(-direction.y, direction.x);
        Vector2 center = area.center;
        Vector2 halfSize = area.size * 0.5f;
        float lineExtent = Mathf.Abs(direction.x) * halfSize.x + Mathf.Abs(direction.y) * halfSize.y;
        float spreadExtent = Mathf.Abs(perpendicular.x) * halfSize.x + Mathf.Abs(perpendicular.y) * halfSize.y;
        int lineCount = Mathf.Clamp(fillLineCount, 1, 96);
        int sampleCount = Mathf.Clamp(fillSamplesPerLine, 2, 128);
        for (int lineIndex = 0; lineIndex < lineCount; lineIndex++)
        {
            float lineT = lineCount == 1 ? 0.5f : lineIndex / (float)(lineCount - 1);
            float offset = Mathf.Lerp(-spreadExtent, spreadExtent, lineT);
            Vector2 lineCenter = center + perpendicular * offset;
            Vector2 lineStart = lineCenter - direction * lineExtent;
            Vector2 lineEnd = lineCenter + direction * lineExtent;
            if (!TryClipLineToRect(area, ref lineStart, ref lineEnd))
                continue;

            float phase = Hash01(seed + lineIndex * 31.73f);
            bool reverse = (lineIndex & 1) == 1;
            fillPoints.Clear();
            for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
            {
                float t = sampleIndex / (float)(sampleCount - 1);
                Vector2 point = Vector2.Lerp(lineStart, lineEnd, reverse ? 1f - t : t);
                float primaryWave = Mathf.Sin((t * fillWobbleFrequency + phase) * TwoPi);
                float secondaryWave = Mathf.Sin((t * (fillWobbleFrequency * 1.91f + 1f) - phase * 0.67f) * TwoPi);
                point += perpendicular * (primaryWave + secondaryWave * 0.45f) * fillWobbleAmplitude;
                point.x = Mathf.Clamp(point.x, area.xMin, area.xMax);
                point.y = Mathf.Clamp(point.y, area.yMin, area.yMax);
                fillPoints.Add(point);
            }

            AddRibbon(fillPoints, false, fillLineWidth, fillVertexColor);
        }
    }

    private void AddEdgeGuidedStrokes(Rect area)
    {
        for (int guideIndex = 0; guideIndex < GuidePointCount; guideIndex++)
        {
            int strokeCount = GetRandomRangeInt(guidedStrokesPerPointMin, guidedStrokesPerPointMax, guideIndex * 41.73f + 3.17f);
            for (int strokeIndex = 0; strokeIndex < strokeCount; strokeIndex++)
            {
                BuildGuidedBypassStroke(area, guideIndex, strokeIndex);
                if (fillPoints.Count >= 2)
                    AddRibbon(fillPoints, false, fillLineWidth, fillVertexColor);
            }
        }
    }

    private void BuildGuidedBypassStroke(Rect area, int guideIndex, int strokeIndex)
    {
        float variationSeed = guideIndex * 101.39f + strokeIndex * 17.31f;
        Vector2 guidePoint = GetGuidePoint(guideIndex);
        float startT = GetGuidedEdgeT(area, guidePoint, guidedStartOffsetRange, variationSeed + 11.7f);
        float endT = GetGuidedEdgeT(area, guidePoint, guidedEndOffsetRange, variationSeed + 19.3f);
        Vector2 start = GetEdgePoint(area, guidedEdge, startT);
        Vector2 end = GetEdgePoint(area, guidedEdge, endT);
        Vector2 inward = GetEdgeInwardDirection(guidedEdge);
        Vector2 edgeTangent = new Vector2(-inward.y, inward.x);
        float bypassDepth = Mathf.Max(0.01f, guidedBypassOffset + GetSignedRandom(variationSeed + 29.1f) * guidedBypassOffsetRange);
        float bypassHalfWidth = bypassDepth * guidedBypassAspect;
        float bypassSide = GetSignedRandom(variationSeed + 31.7f) < 0f ? -1f : 1f;
        Vector2 entry = guidePoint - edgeTangent * (bypassHalfWidth * bypassSide);
        Vector2 exit = guidePoint + edgeTangent * (bypassHalfWidth * bypassSide);

        fillPoints.Clear();
        AddLinePoints(start, entry, 8, true);
        int bypassSamples = Mathf.Max(12, fillSamplesPerLine);
        for (int sampleIndex = 1; sampleIndex < bypassSamples; sampleIndex++)
        {
            float t = sampleIndex / (float)(bypassSamples - 1);
            float depth = Mathf.Sin(t * Mathf.PI) * bypassDepth;
            float lateral = Mathf.Lerp(-bypassHalfWidth, bypassHalfWidth, t) * bypassSide;
            fillPoints.Add(guidePoint + inward * depth + edgeTangent * lateral);
        }
        AddLinePoints(exit, end, 8, false);
        ApplyGuidedWobble(guideIndex * 31 + strokeIndex);
    }

    private float GetGuidedEdgeT(Rect area, Vector2 guidePoint, float offsetRange, float variationSeed)
    {
        float guideT = GetEdgeParameter(area, guidedEdge, guidePoint);
        float randomOffset = GetSignedRandom(variationSeed) * offsetRange;
        float jitter = GetSignedRandom(variationSeed + 5.3f) * guidedEdgeJitter;
        return Mathf.Clamp01(guideT + (randomOffset + jitter) / GetEdgeLength(area, guidedEdge));
    }

    private void AddLinePoints(Vector2 from, Vector2 to, int sampleCount, bool includeFirst)
    {
        int firstSample = includeFirst ? 0 : 1;
        for (int sampleIndex = firstSample; sampleIndex < sampleCount; sampleIndex++)
        {
            float t = sampleIndex / (float)(sampleCount - 1);
            fillPoints.Add(Vector2.Lerp(from, to, t));
        }
    }

    private static Vector2 GetEdgeInwardDirection(FillAreaEdge edge)
    {
        switch (edge)
        {
            case FillAreaEdge.Left:
                return Vector2.right;
            case FillAreaEdge.Right:
                return Vector2.left;
            case FillAreaEdge.Bottom:
                return Vector2.up;
            default:
                return Vector2.down;
        }
    }

    private void ApplyGuidedWobble(int strokeId)
    {
        if (fillPoints.Count < 3 || fillWobbleAmplitude <= 0f)
            return;

        float phase = Hash01(seed + strokeId * 31.73f);
        int lastIndex = fillPoints.Count - 1;
        for (int pointIndex = 1; pointIndex < lastIndex; pointIndex++)
        {
            float pathT = pointIndex / (float)lastIndex;
            float curveT = guidedCurveDirection == GuideCurveDirection.StartToEnd ? pathT : 1f - pathT;
            float amplitude = fillWobbleAmplitude * Mathf.Max(0f, guidedWobbleAmplitudeOverPath.Evaluate(curveT));
            float frequency = fillWobbleFrequency * Mathf.Max(0f, guidedWobbleFrequencyOverPath.Evaluate(curveT));
            phase += frequency / lastIndex;
            Vector2 tangent = GetPathTangent(fillPoints, fillPoints.Count, pointIndex, false);
            Vector2 perpendicular = new Vector2(-tangent.y, tangent.x);
            float primaryWave = Mathf.Sin(phase * TwoPi);
            float secondaryWave = Mathf.Sin((phase * 1.91f - strokeId * 0.67f) * TwoPi);
            fillPoints[pointIndex] += perpendicular * (primaryWave + secondaryWave * 0.45f) * amplitude;
        }
    }

    private Vector2 GetEdgePoint(Rect area, FillAreaEdge edge, float edgeT)
    {
        switch (edge)
        {
            case FillAreaEdge.Left:
                return new Vector2(area.xMin, Mathf.Lerp(area.yMin, area.yMax, edgeT));
            case FillAreaEdge.Right:
                return new Vector2(area.xMax, Mathf.Lerp(area.yMin, area.yMax, edgeT));
            case FillAreaEdge.Bottom:
                return new Vector2(Mathf.Lerp(area.xMin, area.xMax, edgeT), area.yMin);
            default:
                return new Vector2(Mathf.Lerp(area.xMin, area.xMax, edgeT), area.yMax);
        }
    }

    private static float GetEdgeLength(Rect area, FillAreaEdge edge)
    {
        return edge == FillAreaEdge.Left || edge == FillAreaEdge.Right ? area.height : area.width;
    }

    private static float GetEdgeParameter(Rect area, FillAreaEdge edge, Vector2 point)
    {
        switch (edge)
        {
            case FillAreaEdge.Left:
            case FillAreaEdge.Right:
                return Mathf.InverseLerp(area.yMin, area.yMax, point.y);
            default:
                return Mathf.InverseLerp(area.xMin, area.xMax, point.x);
        }
    }

    private int GetRandomRangeInt(int min, int max, float variationSeed)
    {
        return min + Mathf.FloorToInt(Hash01(seed + variationSeed) * (max - min + 1));
    }

    private static Vector2 GetSafeDirection(Vector2 value)
    {
        return value.sqrMagnitude > 0.000001f ? value.normalized : Vector2.right;
    }

    private float GetSignedRandom(float value)
    {
        return Hash01(seed + value) * 2f - 1f;
    }

    private static bool TryClipLineToRect(Rect rect, ref Vector2 start, ref Vector2 end)
    {
        Vector2 delta = end - start;
        float enter = 0f;
        float exit = 1f;
        if (!ClipLine(-delta.x, start.x - rect.xMin, ref enter, ref exit) ||
            !ClipLine(delta.x, rect.xMax - start.x, ref enter, ref exit) ||
            !ClipLine(-delta.y, start.y - rect.yMin, ref enter, ref exit) ||
            !ClipLine(delta.y, rect.yMax - start.y, ref enter, ref exit))
            return false;

        Vector2 originalStart = start;
        start = originalStart + delta * enter;
        end = originalStart + delta * exit;
        return true;
    }

    private static bool ClipLine(float p, float q, ref float enter, ref float exit)
    {
        if (Mathf.Abs(p) < 0.000001f)
            return q >= 0f;

        float value = q / p;
        if (p < 0f)
        {
            if (value > exit)
                return false;
            if (value > enter)
                enter = value;
        }
        else
        {
            if (value < enter)
                return false;
            if (value < exit)
                exit = value;
        }

        return true;
    }

    private void AddRibbon(IList<Vector2> points, bool closed, float width, Color vertexColor)
    {
        if (points == null || width <= 0f)
            return;

        int pointCount = points.Count;
        if (closed && pointCount > 3 && (points[0] - points[pointCount - 1]).sqrMagnitude <= 0.000001f)
            pointCount--;

        if (pointCount < (closed ? 3 : 2))
            return;

        float totalLength = 0f;
        int segmentCount = closed ? pointCount : pointCount - 1;
        for (int index = 0; index < segmentCount; index++)
            totalLength += Vector2.Distance(points[index], points[(index + 1) % pointCount]);

        float distance = 0f;
        float halfWidth = width * 0.5f;
        int vertexStart = vertices.Count;
        for (int index = 0; index < pointCount; index++)
        {
            Vector2 tangent = GetPathTangent(points, pointCount, index, closed);
            Vector2 perpendicular = new Vector2(-tangent.y, tangent.x) * halfWidth;
            float u = totalLength > 0.0001f ? distance / totalLength : index / (float)Mathf.Max(1, pointCount - 1);

            AddRibbonVertex(points[index] + perpendicular, tangent, new Vector2(u, 0f), vertexColor);
            AddRibbonVertex(points[index] - perpendicular, tangent, new Vector2(u, 1f), vertexColor);

            if (index < segmentCount)
                distance += Vector2.Distance(points[index], points[(index + 1) % pointCount]);
        }

        for (int index = 0; index < segmentCount; index++)
        {
            int current = vertexStart + index * 2;
            int next = vertexStart + ((index + 1) % pointCount) * 2;
            if (planeAxes == PlaneAxes.XY)
            {
                triangles.Add(current);
                triangles.Add(current + 1);
                triangles.Add(next + 1);
                triangles.Add(current);
                triangles.Add(next + 1);
                triangles.Add(next);
            }
            else
            {
                triangles.Add(current);
                triangles.Add(next + 1);
                triangles.Add(current + 1);
                triangles.Add(current);
                triangles.Add(next);
                triangles.Add(next + 1);
            }
        }
    }

    private void AddRibbonVertex(Vector2 planePoint, Vector2 planeTangent, Vector2 uv, Color vertexColor)
    {
        Vector3 localTangent = ToLocalPoint(planeTangent) - ToLocalPoint(Vector2.zero);
        vertices.Add(ToLocalPoint(planePoint));
        normals.Add(GetLocalPlaneNormal());
        tangents.Add(new Vector4(localTangent.x, localTangent.y, localTangent.z, 1f));
        uvs.Add(uv);
        colors.Add(vertexColor);
    }

    private static Vector2 GetPathTangent(IList<Vector2> points, int pointCount, int index, bool closed)
    {
        Vector2 tangent;
        if (closed)
        {
            Vector2 previous = points[(index + pointCount - 1) % pointCount];
            Vector2 next = points[(index + 1) % pointCount];
            tangent = next - previous;
        }
        else if (index == 0)
        {
            tangent = points[1] - points[0];
        }
        else if (index == pointCount - 1)
        {
            tangent = points[pointCount - 1] - points[pointCount - 2];
        }
        else
        {
            tangent = points[index + 1] - points[index - 1];
        }

        return tangent.sqrMagnitude > 0.000001f ? tangent.normalized : Vector2.right;
    }

    private Rect GetNormalizedFillArea()
    {
        return NormalizeFillArea(fillArea);
    }

    private static Rect NormalizeFillArea(Rect area)
    {
        float xMin = Mathf.Min(area.xMin, area.xMax);
        float xMax = Mathf.Max(area.xMin, area.xMax);
        float yMin = Mathf.Min(area.yMin, area.yMax);
        float yMax = Mathf.Max(area.yMin, area.yMax);
        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    private void NormalizeSettings()
    {
        fillLineWidth = Mathf.Max(0.001f, fillLineWidth);
        fillInset = Mathf.Max(0f, fillInset);
        fillWobbleAmplitude = Mathf.Max(0f, fillWobbleAmplitude);
        fillWobbleFrequency = Mathf.Max(0f, fillWobbleFrequency);
        fillLineCount = Mathf.Clamp(fillLineCount, 1, 96);
        fillSamplesPerLine = Mathf.Clamp(fillSamplesPerLine, 2, 128);
        guidedStartOffsetRange = Mathf.Max(0f, guidedStartOffsetRange);
        guidedEndOffsetRange = Mathf.Max(0f, guidedEndOffsetRange);
        guidedEdgeJitter = Mathf.Max(0f, guidedEdgeJitter);
        guidedStrokesPerPointMin = Mathf.Clamp(guidedStrokesPerPointMin, 1, 16);
        guidedStrokesPerPointMax = Mathf.Clamp(guidedStrokesPerPointMax, guidedStrokesPerPointMin, 16);
        guidedBypassOffset = Mathf.Max(0.01f, guidedBypassOffset);
        guidedBypassOffsetRange = Mathf.Max(0f, guidedBypassOffsetRange);
        guidedBypassAspect = Mathf.Clamp(guidedBypassAspect, 0.1f, 2f);
        if (guidedWobbleAmplitudeOverPath == null)
            guidedWobbleAmplitudeOverPath = AnimationCurve.Linear(0f, 1f, 1f, 1f);
        if (guidedWobbleFrequencyOverPath == null)
            guidedWobbleFrequencyOverPath = AnimationCurve.Linear(0f, 1f, 1f, 1f);

        Rect area = GetNormalizedFillArea();
        area.width = Mathf.Max(0.001f, area.width);
        area.height = Mathf.Max(0.001f, area.height);
        fillArea = area;

    }

    private void EnsureMesh()
    {
        if (meshFilter == null)
            meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null)
            return;

        if (generatedMesh == null)
        {
            generatedMesh = new Mesh
            {
                name = name + " Scribble Mesh",
                hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild
            };
        }

        if (meshFilter.sharedMesh != generatedMesh)
            meshFilter.sharedMesh = generatedMesh;
    }

    private static float Hash01(float value)
    {
        return Mathf.Repeat(Mathf.Sin(value * 12.9898f) * 43758.5453f, 1f);
    }

#if UNITY_EDITOR
    private int GetGuidePointTransformHash()
    {
        unchecked
        {
            int hash = transform.childCount;
            for (int index = 0; index < transform.childCount; index++)
            {
                Transform guidePoint = transform.GetChild(index);
                hash = hash * 31 + guidePoint.GetSiblingIndex();
                hash = hash * 31 + guidePoint.localPosition.GetHashCode();
                hash = hash * 31 + guidePoint.localRotation.GetHashCode();
                hash = hash * 31 + guidePoint.localScale.GetHashCode();
            }
            return hash;
        }
    }

#endif

#if UNITY_EDITOR
    private void OnValidate()
    {
        RebuildMesh();
    }
#endif
}
