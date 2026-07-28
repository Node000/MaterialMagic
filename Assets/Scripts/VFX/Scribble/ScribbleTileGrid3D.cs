using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
[AddComponentMenu("Scribble/Scribble Tile Grid 3D")]
public class ScribbleTileGrid3D : MonoBehaviour
{
    private const float TwoPi = 6.28318530718f;

    [Header("Grid Layout")]
    [SerializeField] private Rect layoutArea = new Rect(1.65f, -8.5f, 4.1f, 1.85f);
    [SerializeField, Range(1, 32)] private int columns = 6;
    [SerializeField, Range(1, 32)] private int rows = 2;
    [SerializeField, Range(0f, 1f)] private float baseTileChance = 0.9f;
    [SerializeField] private AnimationCurve tileChanceOverRows = AnimationCurve.Linear(0f, 1f, 1f, 1f);
    [SerializeField] private Vector2 tileSizeMin = new Vector2(0.45f, 0.65f);
    [SerializeField] private Vector2 tileSizeMax = new Vector2(0.6f, 0.8f);
    [SerializeField] private Vector2 tilePositionJitter = new Vector2(0.04f, 0.04f);
    [SerializeField] private float surfaceHeight = -1.2f;

    [Header("Tile Scribble")]
    [SerializeField, Range(1, 96)] private int lineCountMin = 6;
    [SerializeField, Range(1, 96)] private int lineCountMax = 12;
    [SerializeField, Range(2, 128)] private int samplesPerLine = 40;
    [SerializeField, Min(0.001f)] private float lineWidth = 0.02f;
    [SerializeField, Min(0f)] private float inset;
    [SerializeField] private Vector2 lineAngleRange = new Vector2(-66f, 31f);
    [SerializeField] private Vector2 wobbleAmplitudeRange = new Vector2(0.35f, 0.86f);
    [SerializeField] private Vector2 wobbleFrequencyRange = new Vector2(1.2f, 3.5f);
    [SerializeField] private Color tileColor = new Color(0.462f, 0.761f, 0.465f, 1f);
    [SerializeField, Range(0f, 1f)] private float colorValueVariation = 0.12f;

    [Header("Variation")]
    [SerializeField] private int seed = 763;

    private readonly List<Vector3> vertices = new List<Vector3>(8192);
    private readonly List<Vector3> normals = new List<Vector3>(8192);
    private readonly List<Vector4> tangents = new List<Vector4>(8192);
    private readonly List<Vector2> uvs = new List<Vector2>(8192);
    private readonly List<Color> colors = new List<Color>(8192);
    private readonly List<int> triangles = new List<int>(16384);
    private readonly List<Vector2> linePoints = new List<Vector2>(128);

    private MeshFilter meshFilter;
    private Mesh generatedMesh;
    private int generatedTileCount;

    public Rect LayoutArea => NormalizeRect(layoutArea);
    public int GeneratedTileCount => generatedTileCount;

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

    public void SetAppearanceMaterial(Material material)
    {
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer != null)
            renderer.sharedMaterial = material;
    }

    public void RandomizeSeed()
    {
        seed = Random.Range(int.MinValue, int.MaxValue);
        RebuildMesh();
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
        generatedTileCount = 0;

        Rect area = LayoutArea;
        float cellWidth = area.width / columns;
        float cellHeight = area.height / rows;
        for (int row = 0; row < rows; row++)
        {
            float rowT = rows == 1 ? 0.5f : row / (float)(rows - 1);
            float chance = Mathf.Clamp01(baseTileChance * Mathf.Clamp01(tileChanceOverRows.Evaluate(rowT)));
            for (int column = 0; column < columns; column++)
            {
                int tileIndex = row * columns + column;
                if (Hash01(seed + tileIndex * 43.17f) > chance)
                    continue;

                BuildTile(area, cellWidth, cellHeight, row, column, tileIndex);
                generatedTileCount++;
            }
        }

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

    private void BuildTile(Rect area, float cellWidth, float cellHeight, int row, int column, int tileIndex)
    {
        float randomBase = tileIndex * 101.39f;
        float width = Mathf.Min(cellWidth, GetRange(tileSizeMin.x, tileSizeMax.x, randomBase + 3.1f));
        float height = Mathf.Min(cellHeight, GetRange(tileSizeMin.y, tileSizeMax.y, randomBase + 5.7f));
        float centerX = area.xMin + (column + 0.5f) * cellWidth + GetSignedRandom(randomBase + 7.3f) * tilePositionJitter.x;
        float centerZ = area.yMin + (row + 0.5f) * cellHeight + GetSignedRandom(randomBase + 11.9f) * tilePositionJitter.y;
        Rect tileArea = Rect.MinMaxRect(centerX - width * 0.5f, centerZ - height * 0.5f, centerX + width * 0.5f, centerZ + height * 0.5f);
        tileArea.xMin += inset;
        tileArea.xMax -= inset;
        tileArea.yMin += inset;
        tileArea.yMax -= inset;
        if (tileArea.width <= 0f || tileArea.height <= 0f)
            return;

        int lineCount = Mathf.RoundToInt(GetRange(lineCountMin, lineCountMax, randomBase + 13.7f));
        int pointCount = samplesPerLine;
        float angleRadians = GetRange(lineAngleRange.x, lineAngleRange.y, randomBase + 17.3f) * Mathf.Deg2Rad;
        float wobbleAmplitude = GetRange(wobbleAmplitudeRange.x, wobbleAmplitudeRange.y, randomBase + 19.1f);
        float wobbleFrequency = GetRange(wobbleFrequencyRange.x, wobbleFrequencyRange.y, randomBase + 23.9f);
        float valueOffset = GetSignedRandom(randomBase + 29.7f) * colorValueVariation;
        Color color = new Color(
            Mathf.Clamp01(tileColor.r + valueOffset),
            Mathf.Clamp01(tileColor.g + valueOffset),
            Mathf.Clamp01(tileColor.b + valueOffset),
            tileColor.a);
        Vector2 direction = new Vector2(Mathf.Cos(angleRadians), Mathf.Sin(angleRadians));
        Vector2 perpendicular = new Vector2(-direction.y, direction.x);
        Vector2 halfSize = tileArea.size * 0.5f;
        float lineExtent = Mathf.Abs(direction.x) * halfSize.x + Mathf.Abs(direction.y) * halfSize.y;
        float spreadExtent = Mathf.Abs(perpendicular.x) * halfSize.x + Mathf.Abs(perpendicular.y) * halfSize.y;
        for (int lineIndex = 0; lineIndex < lineCount; lineIndex++)
        {
            float lineT = lineCount == 1 ? 0.5f : lineIndex / (float)(lineCount - 1);
            Vector2 lineCenter = tileArea.center + perpendicular * Mathf.Lerp(-spreadExtent, spreadExtent, lineT);
            Vector2 lineStart = lineCenter - direction * lineExtent;
            Vector2 lineEnd = lineCenter + direction * lineExtent;
            if (!TryClipLineToRect(tileArea, ref lineStart, ref lineEnd))
                continue;

            float phase = Hash01(seed + randomBase + lineIndex * 31.73f);
            bool reverse = (lineIndex & 1) == 1;
            linePoints.Clear();
            for (int sampleIndex = 0; sampleIndex < pointCount; sampleIndex++)
            {
                float sampleT = sampleIndex / (float)(pointCount - 1);
                Vector2 point = Vector2.Lerp(lineStart, lineEnd, reverse ? 1f - sampleT : sampleT);
                float primary = Mathf.Sin((sampleT * wobbleFrequency + phase) * TwoPi);
                float secondary = Mathf.Sin((sampleT * (wobbleFrequency * 1.91f + 1f) - phase * 0.67f) * TwoPi);
                point += perpendicular * (primary + secondary * 0.45f) * wobbleAmplitude;
                point.x = Mathf.Clamp(point.x, tileArea.xMin, tileArea.xMax);
                point.y = Mathf.Clamp(point.y, tileArea.yMin, tileArea.yMax);
                linePoints.Add(point);
            }

            AddRibbon(linePoints, color);
        }
    }

    private void AddRibbon(IList<Vector2> points, Color color)
    {
        if (points.Count < 2)
            return;

        float totalLength = 0f;
        for (int index = 0; index < points.Count - 1; index++)
            totalLength += Vector2.Distance(points[index], points[index + 1]);

        float distance = 0f;
        float halfWidth = lineWidth * 0.5f;
        int vertexStart = vertices.Count;
        for (int index = 0; index < points.Count; index++)
        {
            Vector2 tangent = GetPathTangent(points, index);
            Vector2 perpendicular = new Vector2(-tangent.y, tangent.x) * halfWidth;
            float u = totalLength > 0.0001f ? distance / totalLength : index / (float)(points.Count - 1);
            AddRibbonVertex(points[index] + perpendicular, tangent, new Vector2(u, 0f), color);
            AddRibbonVertex(points[index] - perpendicular, tangent, new Vector2(u, 1f), color);
            if (index < points.Count - 1)
                distance += Vector2.Distance(points[index], points[index + 1]);
        }

        for (int index = 0; index < points.Count - 1; index++)
        {
            int current = vertexStart + index * 2;
            int next = current + 2;
            triangles.Add(current);
            triangles.Add(next + 1);
            triangles.Add(current + 1);
            triangles.Add(current);
            triangles.Add(next);
            triangles.Add(next + 1);
        }
    }

    private void AddRibbonVertex(Vector2 point, Vector2 tangent, Vector2 uv, Color color)
    {
        vertices.Add(new Vector3(point.x, surfaceHeight, point.y));
        normals.Add(Vector3.up);
        tangents.Add(new Vector4(tangent.x, 0f, tangent.y, 1f));
        uvs.Add(uv);
        colors.Add(color);
    }

    private static Vector2 GetPathTangent(IList<Vector2> points, int index)
    {
        Vector2 tangent = index == 0
            ? points[1] - points[0]
            : index == points.Count - 1
                ? points[index] - points[index - 1]
                : points[index + 1] - points[index - 1];
        return tangent.sqrMagnitude > 0.000001f ? tangent.normalized : Vector2.right;
    }

    private void NormalizeSettings()
    {
        layoutArea = NormalizeRect(layoutArea);
        layoutArea.width = Mathf.Max(0.001f, layoutArea.width);
        layoutArea.height = Mathf.Max(0.001f, layoutArea.height);
        columns = Mathf.Clamp(columns, 1, 32);
        rows = Mathf.Clamp(rows, 1, 32);
        baseTileChance = Mathf.Clamp01(baseTileChance);
        if (tileChanceOverRows == null)
            tileChanceOverRows = AnimationCurve.Linear(0f, 1f, 1f, 1f);
        tileSizeMin.x = Mathf.Max(0.001f, tileSizeMin.x);
        tileSizeMin.y = Mathf.Max(0.001f, tileSizeMin.y);
        tileSizeMax.x = Mathf.Max(tileSizeMin.x, tileSizeMax.x);
        tileSizeMax.y = Mathf.Max(tileSizeMin.y, tileSizeMax.y);
        tilePositionJitter.x = Mathf.Max(0f, tilePositionJitter.x);
        tilePositionJitter.y = Mathf.Max(0f, tilePositionJitter.y);
        lineCountMin = Mathf.Clamp(lineCountMin, 1, 96);
        lineCountMax = Mathf.Clamp(lineCountMax, lineCountMin, 96);
        samplesPerLine = Mathf.Clamp(samplesPerLine, 2, 128);
        lineWidth = Mathf.Max(0.001f, lineWidth);
        inset = Mathf.Max(0f, inset);
        wobbleAmplitudeRange.x = Mathf.Max(0f, wobbleAmplitudeRange.x);
        wobbleAmplitudeRange.y = Mathf.Max(wobbleAmplitudeRange.x, wobbleAmplitudeRange.y);
        wobbleFrequencyRange.x = Mathf.Max(0f, wobbleFrequencyRange.x);
        wobbleFrequencyRange.y = Mathf.Max(wobbleFrequencyRange.x, wobbleFrequencyRange.y);
        colorValueVariation = Mathf.Clamp01(colorValueVariation);
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
                name = name + " Scribble Tile Grid Mesh",
                hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild
            };
        }

        if (meshFilter.sharedMesh != generatedMesh)
            meshFilter.sharedMesh = generatedMesh;
    }

    private float GetRange(float min, float max, float hashSeed)
    {
        return Mathf.Lerp(min, max, Hash01(seed + hashSeed));
    }

    private float GetSignedRandom(float hashSeed)
    {
        return Hash01(seed + hashSeed) * 2f - 1f;
    }

    private static Rect NormalizeRect(Rect area)
    {
        return Rect.MinMaxRect(
            Mathf.Min(area.xMin, area.xMax),
            Mathf.Min(area.yMin, area.yMax),
            Mathf.Max(area.xMin, area.xMax),
            Mathf.Max(area.yMin, area.yMax));
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
