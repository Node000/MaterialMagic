using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Draws only the current board and its explicit walking connections.</summary>
public sealed class SecondFloorMinimapGraphic : MaskableGraphic
{
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
    private SecondFloorMinimapSession session;
    private Vector2 min;
    private Vector2 max;
    private SecondFloorMinimapBoard drawnBoard;

    public void Refresh(SecondFloorMinimapSession value)
    {
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

    private Vector2 Project(Vector2Int point) => new Vector2(point.x,
        point.y * (session.Part == SecondFloorPart.Lower ? session.Map.LowerYScale : 1f));

    private float CellSize
    {
        get
        {
            Vector2 span = max - min + new Vector2(3f, 5f);
            return Mathf.Max(0f, Mathf.Min(maximumCellSize, rectTransform.rect.width / span.x, rectTransform.rect.height / span.y));
        }
    }

    private Vector2 ToLocal(Vector2 position, float scale) => rectTransform.rect.center + (position - (min + max) * 0.5f) * scale;

    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        if (session != null) LayoutLabels();
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
            float size = scale * (tile.Kind == MinimapTileKind.Path || tile.Kind == MinimapTileKind.Connector ? 0.58f : 0.83f);
            AddRect(mesh, center, Vector2.one * size, TileColor(tile.Kind));
        }
        DrawPlayer(mesh, scale);
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
        float thickness = scale * 0.26f;
        var size = direction.x != 0 ? new Vector2(to.x - from.x, thickness) : new Vector2(thickness, to.y - from.y);
        AddRect(mesh, (from + to) * 0.5f, size,
            tile.Kind == MinimapTileKind.Connector || neighbor.Kind == MinimapTileKind.Connector ? connectorColor : color);
    }

    private void DrawPlayer(VertexHelper mesh, float scale)
    {
        Vector2 center = ToLocal(Project(session.Position), scale);
        Vector2 forward = session.Direction;
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
