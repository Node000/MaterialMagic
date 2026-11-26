using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Applies each strip's art kit without adding gameplay geometry or changing map edges.</summary>
[DisallowMultipleComponent]
public sealed class SecondFloorStripArtView : MonoBehaviour
{
    [SerializeField] private SecondFloorStripArtKit defaultKit;
    [SerializeField] private SecondFloorStripArtKit[] additionalKits;
    [Header("验收显示（更改后点击 Reload）")]
    [SerializeField] private bool showFloorArt = true;
    [SerializeField] private bool showWallArt = true;
    [SerializeField] private bool showDecorations = true;

    private static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
    private Transform generated;

    public void Rebuild(SecondFloorFloorMap map, int seed, float cellSize, float wallThickness, float ceilingHeight)
    {
        Clear();
        if (defaultKit == null)
            throw new InvalidOperationException("Assign a default strip art kit.");

        generated = Child("Generated Strip Art", transform);
        var orderedTiles = map.Tiles.OrderBy(t => t.Position.x).ThenBy(t => t.Position.y);
        // Shared snapshot cells are rendered once and use the neutral/default kit; current pools don't overlap.
        var groups = orderedTiles.GroupBy(t => t.StripIds.Count == 1 && !string.IsNullOrEmpty(t.StripIds[0]) ? t.StripIds[0] : null);
        foreach (var group in groups)
        {
            var stripId = group.Key;
            var type = map.GetStripVisualType(stripId);
            var kit = additionalKits?.FirstOrDefault(candidate => candidate != null && candidate.VisualTypeId == type) ?? defaultKit;
            var root = Child($"Strip {stripId ?? "Shared"} [{kit.VisualTypeId}]", generated);
            var tiles = group.ToArray();
            var cells = new HashSet<Vector2Int>(tiles.Select(t => t.Position));
            var stripHash = Hash(seed, stripId);
            if (showFloorArt) BuildFloor(map, tiles, cells, kit, Child("Floor", root), stripHash, cellSize);
            if (showWallArt) BuildWalls(map, tiles, cells, kit, Child("Walls", root), stripHash, cellSize, wallThickness);
            if (showDecorations && stripId != null)
                BuildDecoration(map, tiles, cells.Contains(map.Spawn), kit, Child("Decoration", root), stripHash,
                    cellSize, wallThickness, ceilingHeight);
        }
    }

    private static void BuildFloor(SecondFloorFloorMap map, SecondFloorFloorMap.Tile[] tiles, HashSet<Vector2Int> cells,
        SecondFloorStripArtKit kit, Transform root, uint hash, float size)
    {
        var patches = kit.FloorPatches;
        if (patches == null || patches.Length == 0) return;
        foreach (var tile in tiles)
        {
            var cell = tile.Position;
            uint variation = Hash(hash, cell, 0);
            var patch = patches[variation % (uint)patches.Length];
            if (patch == null) continue;
            Vector2Int heading = Directions.FirstOrDefault(d => cells.Contains(cell + d) && map.GetEdge(cell, cell + d) == SecondFloorEdgeKind.Open);
            // Opposite travel directions share a paving axis.
            var rotation = heading.x == 0 ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f);
            var instance = Instantiate(patch, World(cell) * size, rotation, root);
            instance.name = $"Patch {cell.x}_{cell.y}";
            // Kit floor patches are authored on a 3m grid, with stroke meshes inside the patch.
            instance.transform.localScale *= size / 3f;
        }
    }

    private static void BuildWalls(SecondFloorFloorMap map, SecondFloorFloorMap.Tile[] tiles, HashSet<Vector2Int> cells,
        SecondFloorStripArtKit kit, Transform root, uint hash, float size, float thickness)
    {
        Transform accentWall = null;
        int accentLength = 0;
        uint accentRank = 0;
        foreach (var tile in tiles)
        foreach (var facing in Directions)
        {
            var tangent = facing.x == 0 ? Vector2Int.right : Vector2Int.up;
            bool HasWall(Vector2Int cell) => cells.Contains(cell) && map.GetEdge(cell, cell + facing) == SecondFloorEdgeKind.Wall;
            bool Continues(Vector2Int from, Vector2Int to) => HasWall(to) && map.GetEdge(from, to) == SecondFloorEdgeKind.Open;
            var start = tile.Position;
            if (!HasWall(start) || Continues(start, start - tangent)) continue;
            int count = 1;
            while (Continues(start + (count - 1) * tangent, start + count * tangent)) count++;

            var center = World(start) * size + World(tangent) * (size * (count - 1) * 0.5f);
            var run = Child($"Wall {start.x}_{start.y} {facing.x}_{facing.y} ({count})", root);
            run.SetPositionAndRotation(center + World(facing) * (size * 0.5f - thickness * 0.5f - 0.025f),
                Quaternion.LookRotation(-World(facing)));
            float width = count * size - thickness - 0.06f;
            // Configure before activation: Scribble authoring components otherwise build at OnEnable.
            run.gameObject.SetActive(false);
            if (kit.WallBands != null)
                foreach (var prefab in kit.WallBands)
                {
                    if (prefab == null) continue;
                    var band = Instantiate(prefab, run, false);
                    Rect rect = prefab.FillArea;
                    float ratio = width / Mathf.Max(0.01f, rect.width);
                    // For angled/vertical marks, the horizontal range primarily controls the number of strokes.
                    float spread = Mathf.Abs(Mathf.Sin(prefab.FillAngleDegrees * Mathf.Deg2Rad));
                    band.SetFillArea(new Rect(-width * 0.5f, rect.y, width, rect.height),
                        Mathf.RoundToInt(prefab.FillLineCount * Mathf.Lerp(1f, ratio, spread)),
                        Mathf.RoundToInt(prefab.FillSamplesPerLine * Mathf.Lerp(ratio, 1f, spread)));
                }
            run.gameObject.SetActive(true);

            uint rank = Hash(hash, start, facing.x * 3 + facing.y);
            if (count >= kit.AccentMinWallCells && (count > accentLength || count == accentLength && rank > accentRank))
            {
                accentWall = run;
                accentLength = count;
                accentRank = rank;
            }
        }
        if (accentWall != null && kit.WallAccent != null)
            Instantiate(kit.WallAccent, accentWall, false).name = "Wall Accent";
    }

    private static void BuildDecoration(SecondFloorFloorMap map, SecondFloorFloorMap.Tile[] tiles, bool spawnStrip,
        SecondFloorStripArtKit kit, Transform root, uint hash, float cellSize, float thickness, float ceilingHeight)
    {
        if (kit.Decorations == null) return;
        // One grouped focus per strip. On entry, prefer a lamp and a clear view along the spawn corridor.
        var options = kit.Decorations.Select((prop, i) => (prop, i))
            .Where(x => x.prop != null && x.prop.Prefab != null)
            .OrderByDescending(x => spawnStrip && x.prop.PreferAtSpawn)
            .ThenBy(x => Hash(hash, Vector2Int.zero, x.i));
        foreach (var option in options)
        {
            var prop = option.prop;
            var bounds = prop.Size;
            if (bounds.x <= 0 || bounds.y <= 0 || bounds.z <= 0 || bounds.x > cellSize - thickness - 0.3f ||
                bounds.y >= ceilingHeight - 0.08f || bounds.z > cellSize * 0.5f - thickness * 0.5f - 0.25f) continue;
            var sites = tiles.Where(t => t.Type == "empty").SelectMany(t => Directions.Select(facing => (cell: t.Position, facing)))
                .Where(x => map.GetEdge(x.cell, x.cell + x.facing) == SecondFloorEdgeKind.Wall &&
                    Directions.All(d => map.GetEdge(x.cell, x.cell + d) != SecondFloorEdgeKind.Door ||
                        d == -x.facing || bounds.x <= cellSize * 0.4f))
                .OrderByDescending(x => spawnStrip && IsAlongSpawnSight(map, x.cell, x.facing))
                .ThenBy(x => Hash(hash, x.cell, x.facing.x * 3 + x.facing.y));
            foreach (var site in sites)
            {
                var outward = World(site.facing);
                var position = World(site.cell) * cellSize + outward * (cellSize * 0.5f - thickness * 0.5f - 0.04f);
                var instance = Instantiate(prop.Prefab, position, Quaternion.LookRotation(-outward), root);
                instance.name = $"{prop.Prefab.name} {site.cell.x}_{site.cell.y}";
                return;
            }
        }
    }

    private static bool IsAlongSpawnSight(SecondFloorFloorMap map, Vector2Int cell, Vector2Int facing)
    {
        var delta = cell - map.Spawn;
        if (delta.x != 0 && delta.y != 0 || delta == Vector2Int.zero) return false;
        var direction = new Vector2Int(Math.Sign(delta.x), Math.Sign(delta.y));
        if (direction == facing || direction == -facing) return false;
        for (var cursor = map.Spawn; cursor != cell; cursor += direction)
            if (map.GetEdge(cursor, cursor + direction) != SecondFloorEdgeKind.Open) return false;
        return true;
    }

    private static Vector3 World(Vector2Int cell) => new Vector3(cell.x, 0f, cell.y);
    private static Transform Child(string name, Transform parent)
    {
        var child = new GameObject(name) { layer = parent.gameObject.layer };
        child.transform.SetParent(parent, false);
        return child.transform;
    }

    private static uint Hash(int seed, string id)
    {
        uint hash = unchecked((uint)seed) ^ 2166136261u;
        if (id != null)
            foreach (char c in id) hash = unchecked((hash ^ c) * 16777619u);
        return hash;
    }

    private static uint Hash(uint hash, Vector2Int cell, int salt)
    {
        unchecked
        {
            hash = (hash ^ (uint)cell.x) * 16777619u;
            hash = (hash ^ (uint)cell.y) * 16777619u;
            hash = (hash ^ (uint)salt) * 16777619u;
            hash ^= hash >> 16;
            hash *= 0x7feb352du;
            return hash ^ (hash >> 15);
        }
    }

    private void OnDestroy() => Clear();
    private void Clear()
    {
        if (generated == null) return;
        generated.gameObject.SetActive(false);
        if (Application.isPlaying)
        {
            generated.SetParent(null, false);
            Destroy(generated.gameObject);
        }
        else DestroyImmediate(generated.gameObject);
        generated = null;
    }
}
