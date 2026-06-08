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

    public void ClampPosition(ref int x, ref int y)
    {
        int safeWidth = Mathf.Max(1, width);
        int safeHeight = Mathf.Max(1, height);
        x = Mathf.Clamp(x, 0, safeWidth - 1);
        y = Mathf.Clamp(y, 0, safeHeight - 1);
    }
}
