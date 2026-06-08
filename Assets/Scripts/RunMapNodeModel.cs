using System.Collections.Generic;
using UnityEngine;

public class RunMapNodeModel
{
    public LevelData leftLevel;
    public LevelData rightLevel;
    public LevelData selectedLevel;
    public bool fixedSingleChoice;
    public bool leftHidden;
    public bool rightHidden;
}

public class RunMapCellModel
{
    public int x;
    public int y;
    public LevelData level;
    public bool isBoss;
    public bool isAvailable = true;
    public bool isRevealed;
    public bool reachableSearchVisited;

    public LevelType DisplayType => isBoss ? LevelType.Elite : level != null ? level.levelType : LevelType.Battle;
}

public class RunMapGridModel
{
    public int width;
    public int height;
    public int playerX;
    public int playerY;
    public bool bossMapActive;
    public readonly List<RunMapCellModel> cells = new List<RunMapCellModel>();
    private readonly Queue<RunMapCellModel> reachableQueue = new Queue<RunMapCellModel>();

    public int CellCount => cells.Count;

    public RunMapCellModel GetCell(int x, int y)
    {
        for (int i = 0; i < cells.Count; i++)
        {
            RunMapCellModel cell = cells[i];
            if (cell != null && cell.x == x && cell.y == y)
                return cell;
        }
        return null;
    }

    public RunMapCellModel GetCurrentCell()
    {
        return GetCell(playerX, playerY);
    }

    public bool IsCellReachable(int x, int y)
    {
        RunMapCellModel target = GetCell(x, y);
        if (target == null || !target.isAvailable)
            return false;

        RunMapCellModel start = GetCurrentCell();
        if (start == null || !start.isAvailable)
            return false;

        for (int i = 0; i < cells.Count; i++)
        {
            RunMapCellModel cell = cells[i];
            if (cell != null)
                cell.reachableSearchVisited = false;
        }

        reachableQueue.Clear();
        start.reachableSearchVisited = true;
        reachableQueue.Enqueue(start);
        while (reachableQueue.Count > 0)
        {
            RunMapCellModel cell = reachableQueue.Dequeue();
            if (cell == target)
                return true;

            EnqueueReachableNeighbor(cell.x, cell.y + 1);
            EnqueueReachableNeighbor(cell.x, cell.y - 1);
            EnqueueReachableNeighbor(cell.x - 1, cell.y);
            EnqueueReachableNeighbor(cell.x + 1, cell.y);
        }
        return false;
    }

    private void EnqueueReachableNeighbor(int x, int y)
    {
        RunMapCellModel cell = GetCell(x, y);
        if (cell == null || !cell.isAvailable || cell.reachableSearchVisited)
            return;

        cell.reachableSearchVisited = true;
        reachableQueue.Enqueue(cell);
    }

    public void ClampPosition(ref int x, ref int y)
    {
        int safeWidth = Mathf.Max(1, width);
        int safeHeight = Mathf.Max(1, height);
        x = Mathf.Clamp(x, 0, safeWidth - 1);
        y = Mathf.Clamp(y, 0, safeHeight - 1);
    }
}
