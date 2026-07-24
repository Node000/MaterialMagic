using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
[AddComponentMenu("Scribble/Scribble Path Plane 3D")]
public class ScribblePathPlane3D : MonoBehaviour
{
    public enum PlaneAxes
    {
        XY,
        XZ
    }

    public enum DensityEdge
    {
        Left,
        Right,
        Bottom,
        Top
    }

    private const float TwoPi = 6.28318530718f;

    [Header("Plane")]
    [SerializeField] private PlaneAxes planeAxes = PlaneAxes.XY;

    [Header("Path Strokes")]
    [SerializeField, Range(1, 64)] private int strokeCountMin = 5;
    [SerializeField, Range(1, 64)] private int strokeCountMax = 9;
    [SerializeField, Range(2, 64)] private int samplesPerSegment = 12;
    [SerializeField, Min(0.001f)] private float strokeWidth = 0.035f;
    [SerializeField, Min(0f)] private float pathPointOffsetRange = 0.1f;
    [SerializeField, Min(0f)] private float segmentVariation = 0.12f;
    [SerializeField] private Color strokeColor = new Color(1f, 0.38f, 0.16f, 0.75f);

    [Header("Density Gradient")]
    [SerializeField] private DensityEdge densityEdge = DensityEdge.Left;
    [SerializeField] private AnimationCurve densityOpacityOverDistance = AnimationCurve.Linear(0f, 1f, 1f, 0.3f);

    [Header("Frame")]
    [SerializeField] private bool frameEnabled = true;
    [SerializeField] private Vector2[] frameCorners =
    {
        new Vector2(-2f, -1.1f),
        new Vector2(2f, -0.8f),
        new Vector2(1.65f, 1.15f),
        new Vector2(-1.8f, 0.9f)
    };
    [SerializeField, Min(0.001f)] private float frameWidth = 0.055f;
    [SerializeField] private Color frameColor = new Color(1f, 0.86f, 0.48f, 1f);

    [Header("Appearance")]
    [SerializeField] private Material appearanceMaterial;

    [Header("Variation")]
    [SerializeField] private int seed = 17;

    private readonly List<Vector3> vertices = new List<Vector3>(4096);
    private readonly List<Vector3> normals = new List<Vector3>(4096);
    private readonly List<Vector4> tangents = new List<Vector4>(4096);
    private readonly List<Vector2> uvs = new List<Vector2>(4096);
    private readonly List<Color> colors = new List<Color>(4096);
    private readonly List<int> triangles = new List<int>(8192);
    private readonly List<Vector2> pathPoints = new List<Vector2>(256);

    private MeshFilter meshFilter;
    private Mesh generatedMesh;

#if UNITY_EDITOR
    private int pathPointTransformHash;
#endif

    public PlaneAxes Axes => planeAxes;
    public int PathPointCount => transform.childCount;
    public bool FrameEnabled => frameEnabled;
    public Vector2 GetPathPoint(int index) => ToPlanePoint(transform.GetChild(index).localPosition);
    public Vector2 GetFrameCorner(int index) => frameCorners[index];

    public Transform CreatePathPoint()
    {
        GameObject pathPoint = new GameObject("Path Point");
        Transform pathTransform = pathPoint.transform;
        pathTransform.SetParent(transform, false);
        pathTransform.localPosition = ToLocalPoint(GetFrameCenter());
        RebuildMesh();
        return pathTransform;
    }

    public void SetFrameCorner(int index, Vector2 point)
    {
        EnsureFrameCorners();
        frameCorners[index] = point;
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

    public ScribblePathStyleSettings GetStyleSettings()
    {
        return new ScribblePathStyleSettings
        {
            strokeCountMin = strokeCountMin,
            strokeCountMax = strokeCountMax,
            samplesPerSegment = samplesPerSegment,
            strokeWidth = strokeWidth,
            pathPointOffsetRange = pathPointOffsetRange,
            segmentVariation = segmentVariation,
            strokeColor = strokeColor,
            densityEdge = densityEdge,
            densityOpacityOverDistance = new AnimationCurve(densityOpacityOverDistance.keys),
            frameEnabled = frameEnabled,
            frameWidth = frameWidth,
            frameColor = frameColor,
            appearanceMaterial = appearanceMaterial,
            seed = seed
        };
    }

    public void ApplyStyleSettings(ScribblePathStyleSettings settings)
    {
        strokeCountMin = settings.strokeCountMin;
        strokeCountMax = settings.strokeCountMax;
        samplesPerSegment = settings.samplesPerSegment;
        strokeWidth = settings.strokeWidth;
        pathPointOffsetRange = settings.pathPointOffsetRange;
        segmentVariation = settings.segmentVariation;
        strokeColor = settings.strokeColor;
        densityEdge = settings.densityEdge;
        densityOpacityOverDistance = settings.densityOpacityOverDistance == null
            ? AnimationCurve.Linear(0f, 1f, 1f, 0.3f)
            : new AnimationCurve(settings.densityOpacityOverDistance.keys);
        frameEnabled = settings.frameEnabled;
        frameWidth = settings.frameWidth;
        frameColor = settings.frameColor;
        appearanceMaterial = settings.appearanceMaterial;
        seed = settings.seed;
        ApplyAppearanceMaterial();
        RebuildMesh();
    }

    public void SetAppearanceMaterial(Material material)
    {
        appearanceMaterial = material;
        ApplyAppearanceMaterial();
    }

    private void OnEnable()
    {
        ApplyAppearanceMaterial();
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
        if (Application.isPlaying)
            return;

        int currentHash = GetPathPointTransformHash();
        if (currentHash == pathPointTransformHash)
            return;

        pathPointTransformHash = currentHash;
        RebuildMesh();
    }
#endif

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

        AddPathStrokes();
        if (frameEnabled)
            AddFrame();

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
        pathPointTransformHash = GetPathPointTransformHash();
#endif
    }

    private void AddPathStrokes()
    {
        if (PathPointCount < 2)
            return;

        int strokeCount = GetRandomRangeInt(strokeCountMin, strokeCountMax, 11.7f);
        for (int strokeIndex = 0; strokeIndex < strokeCount; strokeIndex++)
        {
            pathPoints.Clear();
            for (int segmentIndex = 0; segmentIndex < PathPointCount - 1; segmentIndex++)
            {
                Vector2 from = GetStrokePathPoint(strokeIndex, segmentIndex);
                Vector2 to = GetStrokePathPoint(strokeIndex, segmentIndex + 1);
                Vector2 direction = GetSafeDirection(to - from);
                Vector2 perpendicular = new Vector2(-direction.y, direction.x);
                float offset = GetSignedRandom(strokeIndex * 71.3f + segmentIndex * 19.7f) * segmentVariation;
                for (int sampleIndex = segmentIndex == 0 ? 0 : 1; sampleIndex < samplesPerSegment; sampleIndex++)
                {
                    float t = sampleIndex / (float)(samplesPerSegment - 1);
                    Vector2 point = Vector2.Lerp(from, to, t);
                    point += perpendicular * (Mathf.Sin(t * Mathf.PI) * offset);
                    pathPoints.Add(point);
                }
            }

            AddRibbon(pathPoints, strokeWidth, strokeColor, true);
        }
    }

    private Vector2 GetStrokePathPoint(int strokeIndex, int pointIndex)
    {
        Vector2 center = GetPathPoint(pointIndex);
        if (pathPointOffsetRange <= 0f)
            return center;

        float angle = Hash01(seed + strokeIndex * 71.3f + pointIndex * 19.7f) * TwoPi;
        float radius = Mathf.Sqrt(Hash01(seed + strokeIndex * 37.1f + pointIndex * 53.9f)) * pathPointOffsetRange;
        return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
    }

    private void AddFrame()
    {
        for (int index = 0; index < frameCorners.Length; index++)
        {
            Vector2 from = frameCorners[index];
            Vector2 to = frameCorners[(index + 1) % frameCorners.Length];
            pathPoints.Clear();
            pathPoints.Add(from);
            pathPoints.Add(to);
            AddRibbon(pathPoints, frameWidth, frameColor, false);
        }
    }

    private void AddRibbon(IList<Vector2> points, float width, Color color, bool applyDensityOpacity)
    {
        if (points.Count < 2)
            return;

        float totalLength = 0f;
        for (int index = 0; index < points.Count - 1; index++)
            totalLength += Vector2.Distance(points[index], points[index + 1]);

        float distance = 0f;
        float halfWidth = width * 0.5f;
        int vertexStart = vertices.Count;
        for (int index = 0; index < points.Count; index++)
        {
            Vector2 tangent = GetPathTangent(points, index);
            Vector2 perpendicular = new Vector2(-tangent.y, tangent.x) * halfWidth;
            float u = totalLength > 0.0001f ? distance / totalLength : 0f;
            Color vertexColor = color;
            if (applyDensityOpacity)
                vertexColor.a *= GetDensityOpacity(points[index]);
            AddRibbonVertex(points[index] + perpendicular, tangent, new Vector2(u, 0f), vertexColor);
            AddRibbonVertex(points[index] - perpendicular, tangent, new Vector2(u, 1f), vertexColor);
            if (index < points.Count - 1)
                distance += Vector2.Distance(points[index], points[index + 1]);
        }

        for (int index = 0; index < points.Count - 1; index++)
        {
            int current = vertexStart + index * 2;
            int next = current + 2;
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

    private float GetDensityOpacity(Vector2 point)
    {
        float min = float.PositiveInfinity;
        float max = float.NegativeInfinity;
        for (int index = 0; index < frameCorners.Length; index++)
        {
            float value = densityEdge == DensityEdge.Left || densityEdge == DensityEdge.Right ? frameCorners[index].x : frameCorners[index].y;
            min = Mathf.Min(min, value);
            max = Mathf.Max(max, value);
        }

        float coordinate = densityEdge == DensityEdge.Left || densityEdge == DensityEdge.Right ? point.x : point.y;
        float distance = densityEdge == DensityEdge.Left || densityEdge == DensityEdge.Bottom
            ? Mathf.InverseLerp(min, max, coordinate)
            : Mathf.InverseLerp(max, min, coordinate);
        return Mathf.Max(0f, densityOpacityOverDistance.Evaluate(distance));
    }

    private Vector2 GetFrameCenter()
    {
        Vector2 center = Vector2.zero;
        for (int index = 0; index < frameCorners.Length; index++)
            center += frameCorners[index];
        return center / frameCorners.Length;
    }

    private void ApplyAppearanceMaterial()
    {
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer != null && appearanceMaterial != null)
            renderer.sharedMaterial = appearanceMaterial;
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
                name = name + " Path Scribble Mesh",
                hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild
            };
        }

        if (meshFilter.sharedMesh != generatedMesh)
            meshFilter.sharedMesh = generatedMesh;
    }

    private void EnsureFrameCorners()
    {
        if (frameCorners != null && frameCorners.Length == 4)
            return;

        frameCorners = new[]
        {
            new Vector2(-1f, -1f), new Vector2(1f, -1f), new Vector2(1f, 1f), new Vector2(-1f, 1f)
        };
    }

    private void NormalizeSettings()
    {
        EnsureFrameCorners();
        strokeCountMin = Mathf.Clamp(strokeCountMin, 1, 64);
        strokeCountMax = Mathf.Clamp(strokeCountMax, strokeCountMin, 64);
        samplesPerSegment = Mathf.Clamp(samplesPerSegment, 2, 64);
        strokeWidth = Mathf.Max(0.001f, strokeWidth);
        pathPointOffsetRange = Mathf.Max(0f, pathPointOffsetRange);
        segmentVariation = Mathf.Max(0f, segmentVariation);
        frameWidth = Mathf.Max(0.001f, frameWidth);
        if (densityOpacityOverDistance == null)
            densityOpacityOverDistance = AnimationCurve.Linear(0f, 1f, 1f, 0.3f);
    }

    private int GetRandomRangeInt(int min, int max, float variation)
    {
        return min + Mathf.FloorToInt(Hash01(seed + variation) * (max - min + 1));
    }

    private float GetSignedRandom(float value)
    {
        return Hash01(seed + value) * 2f - 1f;
    }

    private static float Hash01(float value)
    {
        return Mathf.Repeat(Mathf.Sin(value * 12.9898f) * 43758.5453f, 1f);
    }

    private static Vector2 GetSafeDirection(Vector2 value)
    {
        return value.sqrMagnitude > 0.000001f ? value.normalized : Vector2.right;
    }

    private static Vector2 GetPathTangent(IList<Vector2> points, int index)
    {
        if (index == 0)
            return GetSafeDirection(points[1] - points[0]);
        if (index == points.Count - 1)
            return GetSafeDirection(points[index] - points[index - 1]);
        return GetSafeDirection(points[index + 1] - points[index - 1]);
    }

#if UNITY_EDITOR
    private int GetPathPointTransformHash()
    {
        unchecked
        {
            int hash = transform.childCount;
            for (int index = 0; index < transform.childCount; index++)
            {
                Transform pathPoint = transform.GetChild(index);
                hash = hash * 31 + pathPoint.GetSiblingIndex();
                hash = hash * 31 + pathPoint.localPosition.GetHashCode();
            }
            return hash;
        }
    }

    private void OnValidate()
    {
        ApplyAppearanceMaterial();
        RebuildMesh();
    }
#endif
}
