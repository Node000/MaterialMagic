using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
[AddComponentMenu("Scribble/Scribble Spotlight Lines")]
public class ScribbleSpotlightLines : MonoBehaviour
{
    private const float TwoPi = 6.28318530718f;

    [Header("Beam Shape")]
    [SerializeField, Min(0.01f)] private float beamLength = 3f;
    [SerializeField, Min(0.01f)] private float baseRadius = 1.35f;
    [SerializeField, Range(0.1f, 2f)] private float baseDepthAspect = 0.55f;
    [SerializeField, Range(1f, 360f)] private float raySpreadDegrees = 150f;

    [Header("Sparse Beam Lines")]
    [SerializeField, Range(1, 24)] private int rayCount = 6;
    [SerializeField, Range(2, 64)] private int raySamples = 16;
    [SerializeField, Min(0.001f)] private float rayLineWidth = 0.025f;
    [SerializeField, Min(0f)] private float rayStartRadius = 0.035f;

    [Header("Ground Hatching")]
    [SerializeField, Range(0, 8)] private int baseRingCount = 2;
    [SerializeField, Range(8, 64)] private int baseRingSamples = 28;
    [SerializeField, Range(0, 16)] private int baseHatchLineCount = 6;
    [SerializeField, Range(2, 64)] private int baseHatchSamples = 18;
    [SerializeField, Min(0.001f)] private float baseLineWidth = 0.02f;
    [SerializeField, Range(1f, 360f)] private float baseArcDegrees = 310f;

    [Header("Hand Drawn Style")]
    [SerializeField, Min(0f)] private float wobbleAmplitude = 0.035f;
    [SerializeField, Min(0f)] private float wobbleFrequency = 3.5f;
    [SerializeField] private Color vertexColor = new Color(1f, 0.76f, 0.04f, 0.9f);
    [SerializeField] private int seed = 801;

    private readonly List<Vector3> vertices = new List<Vector3>(4096);
    private readonly List<Vector3> normals = new List<Vector3>(4096);
    private readonly List<Vector4> tangents = new List<Vector4>(4096);
    private readonly List<Vector2> uvs = new List<Vector2>(4096);
    private readonly List<Color> colors = new List<Color>(4096);
    private readonly List<int> triangles = new List<int>(8192);
    private readonly List<Vector3> points = new List<Vector3>(128);

    private MeshFilter meshFilter;
    private Mesh generatedMesh;

    private void OnEnable()
    {
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

        AddBeamRays();
        AddBaseRings();
        AddBaseHatching();

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
    }

    private void AddBeamRays()
    {
        float spread = raySpreadDegrees * Mathf.Deg2Rad;
        for (int rayIndex = 0; rayIndex < rayCount; rayIndex++)
        {
            float t = rayCount == 1 ? 0.5f : rayIndex / (float)(rayCount - 1);
            float angle = Mathf.Lerp(-spread * 0.5f, spread * 0.5f, t);
            angle += GetSignedRandom(rayIndex * 7.31f) * spread * 0.035f;
            float radius = baseRadius * Mathf.Lerp(0.8f, 1f, Hash01(seed + rayIndex * 3.71f));
            Vector3 start = GetBasePoint(angle, rayStartRadius);
            Vector3 end = GetBasePoint(angle, radius);
            end.y = -beamLength;
            BuildWobblyLine(start, end, raySamples, rayIndex);
            AddRibbon(points, rayLineWidth);
        }
    }

    private void AddBaseRings()
    {
        if (baseRingCount == 0)
            return;

        float arc = baseArcDegrees * Mathf.Deg2Rad;
        for (int ringIndex = 0; ringIndex < baseRingCount; ringIndex++)
        {
            float radius = baseRadius * (1f - ringIndex * 0.09f);
            points.Clear();
            for (int sampleIndex = 0; sampleIndex < baseRingSamples; sampleIndex++)
            {
                float t = sampleIndex / (float)(baseRingSamples - 1);
                float angle = Mathf.Lerp(-arc * 0.5f, arc * 0.5f, t);
                Vector3 point = GetBasePoint(angle, radius);
                point.y = -beamLength;
                Vector3 horizontalDirection = new Vector3(point.x, 0f, point.z);
                ApplyWobble(ref point, horizontalDirection.sqrMagnitude > 0.0001f ? horizontalDirection.normalized : Vector3.right, t, 100 + ringIndex);
                points.Add(point);
            }
            AddRibbon(points, baseLineWidth);
        }
    }

    private void AddBaseHatching()
    {
        for (int hatchIndex = 0; hatchIndex < baseHatchLineCount; hatchIndex++)
        {
            float t = baseHatchLineCount == 1 ? 0.5f : hatchIndex / (float)(baseHatchLineCount - 1);
            float z = Mathf.Lerp(-baseRadius * baseDepthAspect, baseRadius * baseDepthAspect, t);
            float normalizedZ = z / (baseRadius * baseDepthAspect);
            float halfWidth = baseRadius * Mathf.Sqrt(Mathf.Max(0f, 1f - normalizedZ * normalizedZ));
            Vector3 start = new Vector3(-halfWidth, -beamLength, z);
            Vector3 end = new Vector3(halfWidth, -beamLength, z);
            BuildWobblyLine(start, end, baseHatchSamples, 200 + hatchIndex);
            AddRibbon(points, baseLineWidth);
        }
    }

    private Vector3 GetBasePoint(float angle, float radius)
    {
        return new Vector3(Mathf.Sin(angle) * radius, 0f, Mathf.Cos(angle) * radius * baseDepthAspect);
    }

    private void BuildWobblyLine(Vector3 start, Vector3 end, int sampleCount, int lineId)
    {
        points.Clear();
        Vector3 direction = end - start;
        Vector3 perpendicular = Vector3.Cross(direction.normalized, Vector3.forward);
        if (perpendicular.sqrMagnitude < 0.0001f)
            perpendicular = Vector3.right;
        perpendicular.Normalize();

        for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
        {
            float t = sampleIndex / (float)(sampleCount - 1);
            Vector3 point = Vector3.Lerp(start, end, t);
            ApplyWobble(ref point, perpendicular, t, lineId);
            points.Add(point);
        }
    }

    private void ApplyWobble(ref Vector3 point, Vector3 direction, float t, int lineId)
    {
        if (wobbleAmplitude <= 0f || (t <= 0f || t >= 1f))
            return;

        float phase = Hash01(seed + lineId * 17.39f);
        float primary = Mathf.Sin((t * wobbleFrequency + phase) * TwoPi);
        float secondary = Mathf.Sin((t * (wobbleFrequency * 1.87f + 0.5f) - phase * 0.61f) * TwoPi);
        point += direction * (primary + secondary * 0.4f) * wobbleAmplitude;
    }

    private void AddRibbon(IList<Vector3> linePoints, float width)
    {
        if (linePoints.Count < 2 || width <= 0f)
            return;

        float totalLength = 0f;
        for (int index = 0; index < linePoints.Count - 1; index++)
            totalLength += Vector3.Distance(linePoints[index], linePoints[index + 1]);

        float distance = 0f;
        float halfWidth = width * 0.5f;
        int vertexStart = vertices.Count;
        for (int index = 0; index < linePoints.Count; index++)
        {
            Vector3 tangent = GetPathTangent(linePoints, index);
            Vector3 perpendicular = Vector3.Cross(tangent, Vector3.up);
            if (perpendicular.sqrMagnitude < 0.0001f)
                perpendicular = Vector3.Cross(tangent, Vector3.forward);
            perpendicular.Normalize();
            float u = totalLength > 0.0001f ? distance / totalLength : index / (float)(linePoints.Count - 1);

            AddRibbonVertex(linePoints[index] + perpendicular * halfWidth, tangent, new Vector2(u, 0f));
            AddRibbonVertex(linePoints[index] - perpendicular * halfWidth, tangent, new Vector2(u, 1f));

            if (index < linePoints.Count - 1)
                distance += Vector3.Distance(linePoints[index], linePoints[index + 1]);
        }

        for (int index = 0; index < linePoints.Count - 1; index++)
        {
            int current = vertexStart + index * 2;
            int next = current + 2;
            triangles.Add(current);
            triangles.Add(current + 1);
            triangles.Add(next + 1);
            triangles.Add(current);
            triangles.Add(next + 1);
            triangles.Add(next);
        }
    }

    private void AddRibbonVertex(Vector3 position, Vector3 tangent, Vector2 uv)
    {
        vertices.Add(position);
        normals.Add(Vector3.up);
        tangents.Add(new Vector4(tangent.x, tangent.y, tangent.z, 1f));
        uvs.Add(uv);
        colors.Add(vertexColor);
    }

    private static Vector3 GetPathTangent(IList<Vector3> linePoints, int index)
    {
        Vector3 tangent;
        if (index == 0)
            tangent = linePoints[1] - linePoints[0];
        else if (index == linePoints.Count - 1)
            tangent = linePoints[index] - linePoints[index - 1];
        else
            tangent = linePoints[index + 1] - linePoints[index - 1];

        return tangent.sqrMagnitude > 0.000001f ? tangent.normalized : Vector3.right;
    }

    private void NormalizeSettings()
    {
        beamLength = Mathf.Max(0.01f, beamLength);
        baseRadius = Mathf.Max(0.01f, baseRadius);
        baseDepthAspect = Mathf.Clamp(baseDepthAspect, 0.1f, 2f);
        raySpreadDegrees = Mathf.Clamp(raySpreadDegrees, 1f, 360f);
        rayCount = Mathf.Clamp(rayCount, 1, 24);
        raySamples = Mathf.Clamp(raySamples, 2, 64);
        rayLineWidth = Mathf.Max(0.001f, rayLineWidth);
        rayStartRadius = Mathf.Max(0f, rayStartRadius);
        baseRingCount = Mathf.Clamp(baseRingCount, 0, 8);
        baseRingSamples = Mathf.Clamp(baseRingSamples, 8, 64);
        baseHatchLineCount = Mathf.Clamp(baseHatchLineCount, 0, 16);
        baseHatchSamples = Mathf.Clamp(baseHatchSamples, 2, 64);
        baseLineWidth = Mathf.Max(0.001f, baseLineWidth);
        baseArcDegrees = Mathf.Clamp(baseArcDegrees, 1f, 360f);
        wobbleAmplitude = Mathf.Max(0f, wobbleAmplitude);
        wobbleFrequency = Mathf.Max(0f, wobbleFrequency);
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

    private float GetSignedRandom(float value)
    {
        return Hash01(seed + value) * 2f - 1f;
    }

    private static float Hash01(float value)
    {
        return Mathf.Repeat(Mathf.Sin(value * 12.9898f) * 43758.5453f, 1f);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        RebuildMesh();
    }
#endif
}
