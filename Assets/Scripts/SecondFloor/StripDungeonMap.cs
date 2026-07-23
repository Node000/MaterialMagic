using System;
using System.Collections.Generic;
using UnityEngine;

public enum StripDungeonOrientation
{
    Horizontal,
    Vertical
}

public enum StripDungeonCellKind
{
    Empty,
    Path,
    Boss
}

public class StripDungeonMap
{
    public int width;
    public int height;
    public int seed;
    public Vector2Int startPosition;
    public Vector2Int bossPosition;
    public Vector2Int bossEntrancePosition;
    public int bossHostStripId;
    public bool bossRevealed;
    public readonly List<StripDungeonStrip> strips = new List<StripDungeonStrip>();
    public readonly List<StripDungeonCell> cells = new List<StripDungeonCell>();

    private readonly Dictionary<Vector2Int, StripDungeonCell> cellsByPosition = new Dictionary<Vector2Int, StripDungeonCell>();

    public void AddCell(StripDungeonCell cell)
    {
        if (cell == null)
            return;

        cells.Add(cell);
        cellsByPosition[cell.position] = cell;
    }

    public StripDungeonCell GetCell(Vector2Int position)
    {
        cellsByPosition.TryGetValue(position, out StripDungeonCell cell);
        return cell;
    }

    public bool IsPathCell(Vector2Int position)
    {
        StripDungeonCell cell = GetCell(position);
        return cell != null && cell.kind == StripDungeonCellKind.Path;
    }

    public bool IsBossVisible => bossRevealed;

    public void RevealBossIfOnHostStrip(Vector2Int position)
    {
        StripDungeonCell cell = GetCell(position);
        if (cell != null && cell.stripIds.Contains(bossHostStripId))
            bossRevealed = true;
    }
}

public class StripDungeonStrip
{
    public int id;
    public StripDungeonOrientation orientation;
    public Vector2Int start;
    public int length;
    public readonly List<Vector2Int> cells = new List<Vector2Int>();

    public Vector2Int End => cells.Count > 0 ? cells[cells.Count - 1] : start;
}

public class StripDungeonCell
{
    public Vector2Int position;
    public StripDungeonCellKind kind;
    public bool isStart;
    public bool isContent;
    public bool isBossEntrance;
    public LevelType levelType;
    public readonly List<int> stripIds = new List<int>();
}
