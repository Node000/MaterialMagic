using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
[AddComponentMenu("VFX/Model Edge Scribble")]
public class ModelEdgeScribble : MonoBehaviour
{
    [SerializeField, Range(0f, 180f)] private float creaseAngle = 8f;
    [SerializeField] private bool includeBoundaryEdges = true;
    [SerializeField, Min(0.001f)] private float edgeRadius = 0.018f;
    [SerializeField, Range(1, 32)] private int edgeSegments = 12;
    [SerializeField] private Material edgeMaterial;
    [SerializeField] private bool hideSourceFaces = true;

    private MeshFilter sourceFilter;
    private MeshRenderer sourceRenderer;
    private MeshFilter edgeFilter;
    private MeshRenderer edgeRenderer;
    private Mesh generatedMesh;

    public void RebuildEdges()
    {
        EnsureReferences();
        if (sourceFilter == null || sourceFilter.sharedMesh == null)
            return;

        Mesh sourceMesh = sourceFilter.sharedMesh;
        Vector3[] sourceVertices = sourceMesh.vertices;
        int[] sourceTriangles = sourceMesh.triangles;
        Dictionary<EdgeKey, EdgeInfo> edges = new Dictionary<EdgeKey, EdgeInfo>(sourceTriangles.Length);
        for (int triangleIndex = 0; triangleIndex < sourceTriangles.Length; triangleIndex += 3)
        {
            Vector3 a = sourceVertices[sourceTriangles[triangleIndex]];
            Vector3 b = sourceVertices[sourceTriangles[triangleIndex + 1]];
            Vector3 c = sourceVertices[sourceTriangles[triangleIndex + 2]];
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            AddTriangleEdge(edges, a, b, normal);
            AddTriangleEdge(edges, b, c, normal);
            AddTriangleEdge(edges, c, a, normal);
        }

        List<Vector3> vertices = new List<Vector3>(edges.Count * 8);
        List<Vector3> normals = new List<Vector3>(edges.Count * 8);
        List<Vector4> tangents = new List<Vector4>(edges.Count * 8);
        List<Vector2> uvs = new List<Vector2>(edges.Count * 8);
        List<Color> colors = new List<Color>(edges.Count * 8);
        List<int> triangles = new List<int>(edges.Count * 24);
        float minimumDot = Mathf.Cos(creaseAngle * Mathf.Deg2Rad);
        foreach (EdgeInfo edge in edges.Values)
        {
            bool isBoundary = edge.normalCount == 1;
            bool isCrease = edge.normalCount > 1 && Vector3.Dot(edge.firstNormal, edge.secondNormal) < minimumDot;
            if ((isBoundary && includeBoundaryEdges) || isCrease || edge.normalCount > 2)
                AddEdgeTube(edge.a, edge.b, vertices, normals, tangents, uvs, colors, triangles);
        }

        EnsureGeneratedMesh();
        generatedMesh.Clear();
        generatedMesh.indexFormat = vertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
        generatedMesh.SetVertices(vertices);
        generatedMesh.SetNormals(normals);
        generatedMesh.SetTangents(tangents);
        generatedMesh.SetUVs(0, uvs);
        generatedMesh.SetColors(colors);
        generatedMesh.SetTriangles(triangles, 0, true);
        generatedMesh.RecalculateBounds();
    }

    private void OnEnable()
    {
        EnsureReferences();
        RebuildEdges();
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

    private void OnValidate()
    {
        RebuildEdges();
    }

    private void EnsureReferences()
    {
        if (sourceFilter == null)
            sourceFilter = GetComponent<MeshFilter>();
        if (sourceRenderer == null)
            sourceRenderer = GetComponent<MeshRenderer>();
        if (sourceRenderer != null)
            sourceRenderer.enabled = !hideSourceFaces;

        Transform edgeChild = transform.Find("Generated Scribble Edges");
        if (edgeChild == null)
        {
            GameObject edgeObject = new GameObject("Generated Scribble Edges", typeof(MeshFilter), typeof(MeshRenderer));
            edgeObject.transform.SetParent(transform, false);
            edgeChild = edgeObject.transform;
        }

        edgeFilter = edgeChild.GetComponent<MeshFilter>();
        edgeRenderer = edgeChild.GetComponent<MeshRenderer>();
        if (edgeRenderer != null && edgeMaterial != null)
            edgeRenderer.sharedMaterial = edgeMaterial;
    }

    private void EnsureGeneratedMesh()
    {
        if (generatedMesh == null)
        {
            generatedMesh = new Mesh
            {
                name = name + " Scribble Edges",
                hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild
            };
        }
        if (edgeFilter.sharedMesh != generatedMesh)
            edgeFilter.sharedMesh = generatedMesh;
    }

    private void AddEdgeTube(
        Vector3 from,
        Vector3 to,
        List<Vector3> vertices,
        List<Vector3> normals,
        List<Vector4> tangents,
        List<Vector2> uvs,
        List<Color> colors,
        List<int> triangles)
    {
        Vector3 direction = (to - from).normalized;
        Vector3 reference = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.9f ? Vector3.right : Vector3.up;
        Vector3 side = Vector3.Cross(direction, reference).normalized * edgeRadius;
        Vector3 up = Vector3.Cross(side, direction).normalized * edgeRadius;
        int segmentCount = Mathf.Max(1, edgeSegments);
        int vertexStart = vertices.Count;
        for (int segmentIndex = 0; segmentIndex <= segmentCount; segmentIndex++)
        {
            float u = segmentIndex / (float)segmentCount;
            Vector3 center = Vector3.Lerp(from, to, u);
            AddEdgeRing(center, direction, side, up, u, vertices, normals, tangents, uvs, colors);
        }

        for (int segmentIndex = 0; segmentIndex < segmentCount; segmentIndex++)
        {
            int ringStart = vertexStart + segmentIndex * 4;
            int nextRingStart = ringStart + 4;
            for (int sideIndex = 0; sideIndex < 4; sideIndex++)
            {
                int nextSide = (sideIndex + 1) % 4;
                int a = ringStart + sideIndex;
                int b = ringStart + nextSide;
                int c = nextRingStart + nextSide;
                int d = nextRingStart + sideIndex;
                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(c);
                triangles.Add(a);
                triangles.Add(c);
                triangles.Add(d);
            }
        }
    }

    private static void AddEdgeRing(
        Vector3 center,
        Vector3 direction,
        Vector3 side,
        Vector3 up,
        float u,
        List<Vector3> vertices,
        List<Vector3> normals,
        List<Vector4> tangents,
        List<Vector2> uvs,
        List<Color> colors)
    {
        Vector3 radial = side.normalized;
        vertices.Add(center + side);
        normals.Add(radial);
        tangents.Add(new Vector4(direction.x, direction.y, direction.z, 1f));
        uvs.Add(new Vector2(u, 0f));
        colors.Add(Color.white);

        radial = up.normalized;
        vertices.Add(center + up);
        normals.Add(radial);
        tangents.Add(new Vector4(direction.x, direction.y, direction.z, 1f));
        uvs.Add(new Vector2(u, 0.25f));
        colors.Add(Color.white);

        radial = -side.normalized;
        vertices.Add(center - side);
        normals.Add(radial);
        tangents.Add(new Vector4(direction.x, direction.y, direction.z, 1f));
        uvs.Add(new Vector2(u, 0.5f));
        colors.Add(Color.white);

        radial = -up.normalized;
        vertices.Add(center - up);
        normals.Add(radial);
        tangents.Add(new Vector4(direction.x, direction.y, direction.z, 1f));
        uvs.Add(new Vector2(u, 0.75f));
        colors.Add(Color.white);
    }

    private static void AddTriangleEdge(Dictionary<EdgeKey, EdgeInfo> edges, Vector3 a, Vector3 b, Vector3 normal)
    {
        EdgeKey key = new EdgeKey(a, b);
        if (edges.TryGetValue(key, out EdgeInfo edge))
        {
            edge.AddNormal(normal);
            edges[key] = edge;
            return;
        }
        edges.Add(key, new EdgeInfo(a, b, normal));
    }

    private readonly struct PositionKey : IEquatable<PositionKey>, IComparable<PositionKey>
    {
        private const float Precision = 10000f;
        private readonly int x;
        private readonly int y;
        private readonly int z;

        public PositionKey(Vector3 value)
        {
            x = Mathf.RoundToInt(value.x * Precision);
            y = Mathf.RoundToInt(value.y * Precision);
            z = Mathf.RoundToInt(value.z * Precision);
        }

        public int CompareTo(PositionKey other)
        {
            int xComparison = x.CompareTo(other.x);
            if (xComparison != 0)
                return xComparison;
            int yComparison = y.CompareTo(other.y);
            return yComparison != 0 ? yComparison : z.CompareTo(other.z);
        }

        public bool Equals(PositionKey other) => x == other.x && y == other.y && z == other.z;
        public override bool Equals(object obj) => obj is PositionKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(x, y, z);
    }

    private readonly struct EdgeKey : IEquatable<EdgeKey>
    {
        private readonly PositionKey first;
        private readonly PositionKey second;

        public EdgeKey(Vector3 a, Vector3 b)
        {
            PositionKey firstKey = new PositionKey(a);
            PositionKey secondKey = new PositionKey(b);
            if (firstKey.CompareTo(secondKey) <= 0)
            {
                first = firstKey;
                second = secondKey;
            }
            else
            {
                first = secondKey;
                second = firstKey;
            }
        }

        public bool Equals(EdgeKey other) => first.Equals(other.first) && second.Equals(other.second);
        public override bool Equals(object obj) => obj is EdgeKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(first, second);
    }

    private struct EdgeInfo
    {
        public Vector3 a;
        public Vector3 b;
        public Vector3 firstNormal;
        public Vector3 secondNormal;
        public int normalCount;

        public EdgeInfo(Vector3 from, Vector3 to, Vector3 normal)
        {
            a = from;
            b = to;
            firstNormal = normal;
            secondNormal = normal;
            normalCount = 1;
        }

        public void AddNormal(Vector3 normal)
        {
            if (normalCount == 1)
                secondNormal = normal;
            normalCount++;
        }
    }
}
