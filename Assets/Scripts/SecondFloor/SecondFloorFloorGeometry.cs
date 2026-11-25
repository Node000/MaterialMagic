using System;
using UnityEngine;

/// <summary>Builds the fixed floor map's structural whitebox on the world X/Z grid.</summary>
[DisallowMultipleComponent]
public sealed class SecondFloorFloorGeometry : MonoBehaviour
{
    [Header("结构主题")]
    [SerializeField] private StripDungeonThemeDefinition theme;
    [SerializeField] private GeneralThemeGenConfig generationConfig;
    [SerializeField, Min(0.01f)] private float floorThickness = 0.2f;
    [SerializeField, Min(0.01f)] private float wallThickness = 0.15f;

    [Header("门框（仅显示，无碰撞）")]
    [SerializeField] private Material doorFrameMaterial;
    [SerializeField, Min(0.01f)] private float doorOpeningHeight = 2.15f;
    [SerializeField, Min(0.01f)] private float doorFrameWidth = 0.15f;
    [SerializeField, Min(0.01f)] private float doorFrameDepth = 0.22f;

    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down
    };

    private Transform generatedRoot;

    public void Rebuild(SecondFloorFloorMap map, float cellWorldSize)
    {
        if (map == null) throw new ArgumentNullException(nameof(map));
        if (!map.HasTile(map.Spawn)) throw new ArgumentException("Floor map is missing its spawn tile.", nameof(map));
        if (!IsPositiveFinite(cellWorldSize)) throw new ArgumentOutOfRangeException(nameof(cellWorldSize));
        if (transform.localToWorldMatrix != Matrix4x4.identity)
            throw new InvalidOperationException("SecondFloorFloorGeometry must be placed at the world origin with identity rotation and scale.");
        if (theme == null || generationConfig == null || doorFrameMaterial == null)
            throw new InvalidOperationException("SecondFloorFloorGeometry requires a theme, generation config, and door frame material.");

        float ceilingHeight = generationConfig.GetCeilingHeight(theme.UniqueGenConfig);
        float ceilingThickness = generationConfig.GetCeilingThickness(theme.UniqueGenConfig);
        if (!IsPositiveFinite(floorThickness) || !IsPositiveFinite(ceilingHeight) || !IsPositiveFinite(ceilingThickness) ||
            !IsPositiveFinite(wallThickness) || wallThickness >= cellWorldSize)
            throw new InvalidOperationException("SecondFloorFloorGeometry has invalid floor, wall, or ceiling dimensions.");
        if (!IsPositiveFinite(doorOpeningHeight) || !IsPositiveFinite(doorFrameWidth) || !IsPositiveFinite(doorFrameDepth) ||
            doorFrameWidth * 2f >= cellWorldSize || doorFrameDepth >= cellWorldSize || doorOpeningHeight + doorFrameWidth > ceilingHeight)
            throw new InvalidOperationException("SecondFloorFloorGeometry door frame must fit inside a cell with a clear opening.");

        BlockArt floorArt = GetBlockArt(theme.FloorPrefabs, "floor");
        BlockArt wallArt = GetBlockArt(theme.WallPrefabs, "wall");
        BlockArt ceilingArt = GetBlockArt(generationConfig.GetCeilingPrefabs(theme.UniqueGenConfig), "ceiling");

        if (generatedRoot != null)
        {
            // Destroy is deferred in Play Mode; disable and detach the old geometry now.
            generatedRoot.gameObject.SetActive(false);
            if (Application.isPlaying)
                generatedRoot.SetParent(null, false);
            DestroyGeneratedRoot();
        }

        generatedRoot = CreateChild("FixedFloorGeometry", transform);
        Transform floors = CreateChild("Floors", generatedRoot);
        Transform ceilings = CreateChild("Ceilings", generatedRoot);
        Transform walls = CreateChild("Walls", generatedRoot);
        Transform doors = CreateChild("Doors", generatedRoot);
        var frameArt = new BlockArt(wallArt.Mesh, doorFrameMaterial);

        foreach (SecondFloorFloorMap.Tile tile in map.Tiles)
        {
            Vector2Int cell = tile.Position;
            Vector3 center = new Vector3(cell.x * cellWorldSize, 0f, cell.y * cellWorldSize);
            string cellName = $"{cell.x}_{cell.y}";
            CreateBlock($"Floor_{cellName}", floors, center - Vector3.up * (floorThickness * 0.5f), Quaternion.identity,
                new Vector3(cellWorldSize, floorThickness, cellWorldSize), floorArt, true);
            CreateBlock($"Ceiling_{cellName}", ceilings, center + Vector3.up * (ceilingHeight + ceilingThickness * 0.5f), Quaternion.identity,
                new Vector3(cellWorldSize, ceilingThickness, cellWorldSize), ceilingArt, false);

            foreach (Vector2Int direction in Directions)
            {
                Vector2Int neighbor = cell + direction;
                // Shared edges belong to the west/south cell; all exterior edges belong to their only tile.
                if ((direction.x < 0 || direction.y < 0) && map.HasTile(neighbor))
                    continue;

                SecondFloorEdgeKind kind = map.GetEdge(cell, neighbor);
                if (kind == SecondFloorEdgeKind.Open)
                    continue;

                string edgeName = $"{cellName}_to_{neighbor.x}_{neighbor.y}";
                Vector3 outward = new Vector3(direction.x, 0f, direction.y);
                Vector3 edgeCenter = center + outward * (cellWorldSize * 0.5f);
                Quaternion edgeRotation = Quaternion.LookRotation(-outward, Vector3.up);
                if (kind == SecondFloorEdgeKind.Door)
                    CreateDoor($"Door_{edgeName}", doors, edgeCenter, edgeRotation, cellWorldSize, frameArt);
                else
                    CreateBlock($"Wall_{edgeName}", walls, edgeCenter + Vector3.up * (ceilingHeight * 0.5f), edgeRotation,
                        new Vector3(cellWorldSize, ceilingHeight, wallThickness), wallArt, true);
            }
        }
    }

    private void CreateDoor(string doorName, Transform parent, Vector3 center, Quaternion rotation, float span, BlockArt art)
    {
        Transform frame = CreateChild(doorName, parent);
        frame.SetLocalPositionAndRotation(center, rotation);
        float jambOffset = (span - doorFrameWidth) * 0.5f;
        var jambSize = new Vector3(doorFrameWidth, doorOpeningHeight, doorFrameDepth);
        CreateBlock("LeftJamb", frame, new Vector3(-jambOffset, doorOpeningHeight * 0.5f, 0f), Quaternion.identity, jambSize, art, false);
        CreateBlock("RightJamb", frame, new Vector3(jambOffset, doorOpeningHeight * 0.5f, 0f), Quaternion.identity, jambSize, art, false);
        CreateBlock("Lintel", frame, new Vector3(0f, doorOpeningHeight + doorFrameWidth * 0.5f, 0f), Quaternion.identity,
            new Vector3(span, doorFrameWidth, doorFrameDepth), art, false);
    }

    private static void CreateBlock(string blockName, Transform parent, Vector3 center, Quaternion rotation, Vector3 size, BlockArt art, bool solid)
    {
        Transform block = CreateChild(blockName, parent);
        Bounds bounds = art.Mesh.bounds;
        Vector3 scale = new Vector3(size.x / bounds.size.x, size.y / bounds.size.y, size.z / bounds.size.z);
        block.SetLocalPositionAndRotation(center - rotation * Vector3.Scale(bounds.center, scale), rotation);
        block.localScale = scale;
        block.gameObject.AddComponent<MeshFilter>().sharedMesh = art.Mesh;
        block.gameObject.AddComponent<MeshRenderer>().sharedMaterial = art.Material;
        if (solid)
        {
            BoxCollider collider = block.gameObject.AddComponent<BoxCollider>();
            collider.center = bounds.center;
            collider.size = bounds.size;
        }
    }

    private static Transform CreateChild(string objectName, Transform parent)
    {
        var child = new GameObject(objectName) { layer = parent.gameObject.layer };
        child.transform.SetParent(parent, false);
        return child.transform;
    }

    private static BlockArt GetBlockArt(GameObject[] prefabs, string purpose)
    {
        // Use the bound theme's primary structural mesh, not its positioned Scribble/decorative children.
        if (prefabs == null || prefabs.Length == 0 || prefabs[0] == null)
            throw new InvalidOperationException($"SecondFloorFloorGeometry requires a primary {purpose} prefab at index 0.");

        GameObject prefab = prefabs[0];
        Mesh mesh = prefab.GetComponent<MeshFilter>()?.sharedMesh;
        Material material = prefab.GetComponent<MeshRenderer>()?.sharedMaterial;
        if (mesh == null || mesh.subMeshCount != 1 || material == null)
            throw new InvalidOperationException($"Primary {purpose} prefab '{prefab.name}' requires a root mesh with one submesh and a material.");

        Vector3 size = mesh.bounds.size;
        if (!IsPositiveFinite(size.x) || !IsPositiveFinite(size.y) || !IsPositiveFinite(size.z))
            throw new InvalidOperationException($"Primary {purpose} prefab '{prefab.name}' requires a solid 3D mesh.");
        return new BlockArt(mesh, material);
    }

    private static bool IsPositiveFinite(float value) => value > 0f && !float.IsInfinity(value);

    private void OnDestroy() => DestroyGeneratedRoot();

    private void DestroyGeneratedRoot()
    {
        if (generatedRoot == null)
            return;

        if (Application.isPlaying)
            Destroy(generatedRoot.gameObject);
        else
            DestroyImmediate(generatedRoot.gameObject);
        generatedRoot = null;
    }

    private readonly struct BlockArt
    {
        public Mesh Mesh { get; }
        public Material Material { get; }

        public BlockArt(Mesh mesh, Material material)
        {
            Mesh = mesh;
            Material = material;
        }
    }
}
