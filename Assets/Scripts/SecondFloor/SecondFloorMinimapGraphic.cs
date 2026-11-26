using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Draws the legacy strip board or the heading-up fixed floor grid.</summary>
public sealed class SecondFloorMinimapGraphic : MaskableGraphic
{
    private const float PathTileSize = 0.58f;
    private const float FloorTileSize = 0.94f;
    private const float FloorMarkerSize = 0.72f;
    private const float FloorEdgeThickness = 0.08f;
    // Seven cell centers per axis, cropped through the centers of the outer cells.
    private const int FloorWindowRadius = 3;
    private static readonly Vector2Int[] FloorDirections =
        { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };

    [SerializeField] private Color connectorColor;
    [SerializeField] private Color roomColor;
    [SerializeField] private Color bossColor;
    [SerializeField] private Color portalColor;
    [SerializeField] private Color playerColor;
    [SerializeField, Min(1f)] private float maximumCellSize = 32f;
    [SerializeField] private TMP_Text areaA;
    [SerializeField] private TMP_Text areaB;
    [SerializeField] private TMP_Text areaC;
    [SerializeField] private TMP_Text areaU;
    [SerializeField, Range(0f, 1f)] private float discoveredOpacity = 0.45f;
    [SerializeField] private Color wallColor;

    private SecondFloorMinimapSession session;
    private SecondFloorFloorMap floor;
    private readonly HashSet<Vector2Int> discoveredFloor = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> visibleFloor = new HashSet<Vector2Int>();
    // A quad clipped by the four sides of the window has at most eight vertices.
    private readonly Vector2[] floorClipA = new Vector2[8];
    private readonly Vector2[] floorClipB = new Vector2[8];
    private Image floorPlayerMarker;
    private Vector2Int floorPlayer;
    private Vector2 floorFacing = Vector2.up;
    private bool hasFloorPlayer;
    private bool showFloorAll;
    private Vector2 min;
    private Vector2 max;
    private SecondFloorMinimapBoard drawnBoard;

    public void Refresh(SecondFloorMinimapSession value)
    {
        floor = null;
        if (floorPlayerMarker != null) floorPlayerMarker.enabled = false;
        floorPlayerMarker = null;
        session = value;
        if (drawnBoard != value.CurrentBoard)
        {
            drawnBoard = value.CurrentBoard;
            min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (var tile in drawnBoard.Tiles)
            {
                Vector2 point = Project(tile.Position);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
        }
        LayoutLabels();
        SetVerticesDirty();
    }

    internal void RebuildFloor(SecondFloorFloorMap map, bool showAll, Image playerMarker)
    {
        floor = map ?? throw new ArgumentNullException(nameof(map));
        floorPlayerMarker = playerMarker;
        if (session != null)
        {
            if (areaA != null) areaA.gameObject.SetActive(false);
            if (areaB != null) areaB.gameObject.SetActive(false);
            if (areaC != null) areaC.gameObject.SetActive(false);
            if (areaU != null) areaU.gameObject.SetActive(false);
        }
        session = null;
        drawnBoard = null;
        showFloorAll = showAll;
        hasFloorPlayer = false;
        floorFacing = Vector2.up;
        discoveredFloor.Clear();
        visibleFloor.Clear();
        LayoutFloorPlayerMarker();
        SetVerticesDirty();
    }

    internal void RefreshFloor(Vector2Int playerCell, Vector2Int facingDirection,
        IReadOnlyCollection<Vector2Int> discovered, IReadOnlyCollection<Vector2Int> visible)
    {
        if (floor == null) throw new InvalidOperationException("Rebuild the fixed floor minimap before refreshing it.");
        if (!floor.HasTile(playerCell)) throw new ArgumentOutOfRangeException(nameof(playerCell));
        if (facingDirection != Vector2Int.up && facingDirection != Vector2Int.right &&
            facingDirection != Vector2Int.down && facingDirection != Vector2Int.left)
            throw new ArgumentOutOfRangeException(nameof(facingDirection), "Expected a cardinal facing direction.");
        if (discovered == null) throw new ArgumentNullException(nameof(discovered));
        if (visible == null) throw new ArgumentNullException(nameof(visible));
        discoveredFloor.Clear();
        discoveredFloor.UnionWith(discovered);
        visibleFloor.Clear();
        visibleFloor.UnionWith(visible);
        floorPlayer = playerCell;
        floorFacing = facingDirection;
        hasFloorPlayer = true;
        LayoutFloorPlayerMarker();
        SetVerticesDirty();
    }

    internal void SetFloorVisualHeading(float clockwiseDegrees)
    {
        float radians = clockwiseDegrees * Mathf.Deg2Rad;
        floorFacing = new Vector2(Mathf.Sin(radians), Mathf.Cos(radians));
        SetVerticesDirty();
    }

    internal void SetShowFloorAll(bool value)
    {
        showFloorAll = value;
        if (floor != null) SetVerticesDirty();
    }

    private Vector2 Project(Vector2Int point) => new Vector2(point.x,
        point.y * (session.Part == SecondFloorPart.Lower ? session.Map.LowerYScale : 1f));

    // Inverse world heading: the camera's forward vector maps to UI up.
    private Vector2 RotateFloor(Vector2 point) => new Vector2(
        point.x * floorFacing.y - point.y * floorFacing.x,
        point.x * floorFacing.x + point.y * floorFacing.y);

    private float FloorCellSize
    {
        get
        {
            Rect rect = rectTransform.rect;
            return Mathf.Max(0f, Mathf.Min(rect.width, rect.height) / (2 * FloorWindowRadius));
        }
    }

    private float CellSize
    {
        get
        {
            Vector2 span = max - min + new Vector2(3f, 5f);
            Rect rect = rectTransform.rect;
            float fit = Mathf.Min(rect.width / span.x, rect.height / span.y);
            return Mathf.Max(0f, Mathf.Min(maximumCellSize, fit));
        }
    }

    private Vector2 ToLocal(Vector2 position, float scale) => rectTransform.rect.center + (position - (min + max) * 0.5f) * scale;

    private void LayoutFloorPlayerMarker()
    {
        if (floorPlayerMarker == null) return;
        float scale = FloorCellSize;
        floorPlayerMarker.enabled = hasFloorPlayer && scale > 0f;
        if (!floorPlayerMarker.enabled) return;
        RectTransform marker = floorPlayerMarker.rectTransform;
        // A centered child anchor shares the same origin as the grid's rect center.
        marker.anchoredPosition = Vector2.zero;
        marker.sizeDelta = Vector2.one * (scale * FloorMarkerSize);
    }

    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        if (session != null) LayoutLabels();
        else if (floor != null) LayoutFloorPlayerMarker();
    }

    private void LayoutLabels()
    {
        bool lower = session.Part == SecondFloorPart.Lower;
        float scale = CellSize;
        foreach (var region in session.Map.Regions)
        {
            var label = region.Id == "A" ? areaA : region.Id == "B" ? areaB : region.Id == "C" ? areaC : areaU;
            if (label == null) continue;
            bool current = region.Part == session.Part;
            if (label.gameObject.activeSelf != current) label.gameObject.SetActive(current);
            if (!current) continue;
            float x = lower ? region.WorldCenter.x : 0f;
            // Place labels clear of their local corridors and the connector routes.
            float y = lower && region.Id != "C" ? min.y - 1.6f : max.y + 1.6f;
            label.rectTransform.anchoredPosition = ToLocal(new Vector2(x, y), scale);
        }
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (floor != null)
        {
            DrawFloor(mesh, FloorCellSize);
            return;
        }
        if (session == null) return;
        float scale = CellSize;
        var board = session.CurrentBoard;
        foreach (var tile in board.Tiles)
        {
            Vector2 center = ToLocal(Project(tile.Position), scale);
            if (board.CanStep(tile.Position, tile.Position + Vector2Int.right))
                AddBridge(mesh, tile, center, Vector2Int.right, scale);
            if (board.CanStep(tile.Position, tile.Position + Vector2Int.up))
                AddBridge(mesh, tile, center, Vector2Int.up, scale);
        }
        foreach (var tile in board.Tiles)
        {
            Vector2 center = ToLocal(Project(tile.Position), scale);
            float size = scale * (tile.Kind == MinimapTileKind.Path || tile.Kind == MinimapTileKind.Connector ? PathTileSize : 0.83f);
            AddRect(mesh, center, Vector2.one * size, TileColor(tile.Kind));
        }
        DrawPlayer(mesh, ToLocal(Project(session.Position), scale), session.Direction, scale);
    }

    private void DrawFloor(VertexHelper mesh, float scale)
    {
        if (!hasFloorPlayer || scale <= 0f) return;
        Vector2 mapCenter = rectTransform.rect.center;
        Vector2 halfWindow = Vector2.one * (scale * FloorWindowRadius);
        Rect window = Rect.MinMaxRect(mapCenter.x - halfWindow.x, mapCenter.y - halfWindow.y,
            mapCenter.x + halfWindow.x, mapCenter.y + halfWindow.y);
        Vector2 tileHalf = Vector2.one * (scale * FloorTileSize * 0.5f);
        // Bound the inverse-rotated window in world cells, including tiles intersecting its edges.
        int radius = Mathf.FloorToInt(FloorWindowRadius * (Mathf.Abs(floorFacing.x) + Mathf.Abs(floorFacing.y)) + FloorTileSize * 0.5f);
        for (int y = -radius; y <= radius; y++)
        for (int x = -radius; x <= radius; x++)
        {
            var offset = new Vector2Int(x, y);
            var cell = floorPlayer + offset;
            float opacity = FloorOpacity(cell);
            if (opacity <= 0f || !floor.HasTile(cell)) continue;
            Vector2 center = mapCenter + RotateFloor(offset) * scale;
            AddClippedFloorRect(mesh, window, center, tileHalf, WithOpacity(color, opacity));
            foreach (var direction in FloorDirections)
                DrawFloorEdge(mesh, window, cell, center, direction, scale, opacity);
        }
    }

    private float FloorOpacity(Vector2Int cell) => showFloorAll || visibleFloor.Contains(cell) ? 1f :
        discoveredFloor.Contains(cell) ? discoveredOpacity : 0f;

    private void DrawFloorEdge(VertexHelper mesh, Rect window, Vector2Int cell, Vector2 center, Vector2Int direction, float scale, float opacity)
    {
        Vector2Int neighbor = cell + direction;
        if (!floor.HasTile(neighbor)) return;
        var edge = floor.GetEdge(cell, neighbor);
        if (edge == SecondFloorEdgeKind.Open) return;
        opacity = Mathf.Min(opacity, FloorOpacity(neighbor));
        if (opacity <= 0f) return;

        // Wall and door strips sit just inside the cell edge. Nothing spans the grid gap.
        float length = edge == SecondFloorEdgeKind.Wall ? 0.84f : 0.48f;
        Vector2 size = direction.x != 0 ? new Vector2(FloorEdgeThickness, length) : new Vector2(length, FloorEdgeThickness);
        Vector2 inset = RotateFloor(direction) * ((FloorTileSize - FloorEdgeThickness) * 0.5f * scale);
        Color tint = edge == SecondFloorEdgeKind.Wall ? wallColor : connectorColor;
        AddClippedFloorRect(mesh, window, center + inset, size * (scale * 0.5f), WithOpacity(tint, opacity));
    }

    private void AddClippedFloorRect(VertexHelper mesh, Rect window, Vector2 center, Vector2 halfSize, Color tint)
    {
        Vector2 right = RotateFloor(new Vector2(halfSize.x, 0f));
        Vector2 up = RotateFloor(new Vector2(0f, halfSize.y));
        floorClipA[0] = center - right - up;
        floorClipA[1] = center - right + up;
        floorClipA[2] = center + right + up;
        floorClipA[3] = center + right - up;

        // Clip the rotated polygon against the fixed UI window, preserving its triangle winding.
        int count = ClipFloorPolygon(floorClipA, floorClipB, 4, 0, window.xMin, true);
        count = ClipFloorPolygon(floorClipB, floorClipA, count, 0, window.xMax, false);
        count = ClipFloorPolygon(floorClipA, floorClipB, count, 1, window.yMin, true);
        count = ClipFloorPolygon(floorClipB, floorClipA, count, 1, window.yMax, false);
        if (count < 3) return;

        int index = mesh.currentVertCount;
        for (int i = 0; i < count; i++) mesh.AddVert(floorClipA[i], tint, Vector2.zero);
        for (int i = 1; i < count - 1; i++) mesh.AddTriangle(index, index + i, index + i + 1);
    }

    private static int ClipFloorPolygon(Vector2[] input, Vector2[] output, int count, int axis, float edge, bool keepAbove)
    {
        if (count == 0) return 0;
        int outputCount = 0;
        Vector2 previous = input[count - 1];
        float previousDistance = keepAbove ? previous[axis] - edge : edge - previous[axis];
        for (int i = 0; i < count; i++)
        {
            Vector2 current = input[i];
            float distance = keepAbove ? current[axis] - edge : edge - current[axis];
            if ((previousDistance < 0f && distance > 0f) || (previousDistance > 0f && distance < 0f))
            {
                Vector2 intersection = Vector2.LerpUnclamped(previous, current, previousDistance / (previousDistance - distance));
                intersection[axis] = edge;
                output[outputCount++] = intersection;
            }
            if (distance >= 0f) output[outputCount++] = current;
            previous = current;
            previousDistance = distance;
        }
        return outputCount;
    }

    private static Color WithOpacity(Color tint, float opacity)
    {
        tint.a *= opacity;
        return tint;
    }

    private Color TileColor(MinimapTileKind kind) => kind switch
    {
        MinimapTileKind.Connector => connectorColor,
        MinimapTileKind.Room => roomColor,
        MinimapTileKind.BossRoom => bossColor,
        MinimapTileKind.Portal => portalColor,
        _ => color
    };

    private void AddBridge(VertexHelper mesh, MinimapTile tile, Vector2 from, Vector2Int direction, float scale)
    {
        var neighbor = session.CurrentBoard.GetTile(tile.Position + direction);
        Vector2 to = ToLocal(Project(neighbor.Position), scale);
        AddBridge(mesh, from, to, direction.x != 0, scale,
            tile.Kind == MinimapTileKind.Connector || neighbor.Kind == MinimapTileKind.Connector ? connectorColor : color);
    }

    private static void AddBridge(VertexHelper mesh, Vector2 from, Vector2 to, bool horizontal, float scale, Color tint)
    {
        float thickness = scale * 0.26f;
        var size = horizontal ? new Vector2(to.x - from.x, thickness) : new Vector2(thickness, to.y - from.y);
        AddRect(mesh, (from + to) * 0.5f, size, tint);
    }

    private void DrawPlayer(VertexHelper mesh, Vector2 center, Vector2 forward, float scale)
    {
        Vector2 side = new Vector2(forward.y, -forward.x);
        int index = mesh.currentVertCount;
        mesh.AddVert(center + forward * (scale * 0.78f), playerColor, Vector2.zero);
        mesh.AddVert(center - forward * (scale * 0.49f) + side * (scale * 0.5f), playerColor, Vector2.zero);
        mesh.AddVert(center - forward * (scale * 0.49f) - side * (scale * 0.5f), playerColor, Vector2.zero);
        mesh.AddTriangle(index, index + 1, index + 2);
    }

    private static void AddRect(VertexHelper mesh, Vector2 center, Vector2 size, Color tint)
    {
        Vector2 half = size * 0.5f;
        int index = mesh.currentVertCount;
        mesh.AddVert(center - half, tint, Vector2.zero);
        mesh.AddVert(center + new Vector2(-half.x, half.y), tint, Vector2.zero);
        mesh.AddVert(center + half, tint, Vector2.zero);
        mesh.AddVert(center + new Vector2(half.x, -half.y), tint, Vector2.zero);
        mesh.AddTriangle(index, index + 1, index + 2);
        mesh.AddTriangle(index, index + 2, index + 3);
    }
}
