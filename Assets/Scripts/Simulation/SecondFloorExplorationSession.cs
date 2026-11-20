using System;
using UnityEngine;

public enum SecondFloorFacing { North, East, South, West }

/// <summary>
/// Grid-only movement state shared by the real RunManager map path (including the second-floor grid).
/// Production input remains absolute Fire/Up, Wind/Left, Water/Down, Earth/Right. Facing/Turn are
/// optional script operations, not an integration of the first-person prototype or new gameplay.
/// </summary>
public sealed class SecondFloorExplorationSession
{
    public RunMapGridModel Grid { get; }
    public SecondFloorFacing Facing { get; private set; }
    public int X => Grid.playerX;
    public int Y => Grid.playerY;

    public SecondFloorExplorationSession(RunMapGridModel grid, SecondFloorFacing facing = SecondFloorFacing.North)
    {
        Grid = grid ?? throw new ArgumentNullException(nameof(grid));
        Facing = Normalize((int)facing);
    }

    public bool CanMove(int deltaX, int deltaY)
    {
        if (Math.Abs(deltaX) + Math.Abs(deltaY) != 1 || Grid.width <= 0 || Grid.height <= 0)
            return false;
        int x = X + deltaX;
        int y = Y + deltaY;
        return x >= 0 && x < Grid.width && y >= 0 && y < Grid.height && Grid.CanEnterCell(Grid.GetCell(x, y));
    }

    public RunMapCellModel Move(int deltaX, int deltaY)
    {
        if (!CanMove(deltaX, deltaY)) return null;
        Grid.playerX += deltaX;
        Grid.playerY += deltaY;
        RevealCurrentNeighbors();
        return Grid.GetCurrentCell();
    }

    public RunMapCellModel Move(MaterialEnum material)
    {
        Vector2Int direction = RunManager.GetMapDirection(material);
        if (Grid.width <= 0 || Grid.height <= 0) return null;
        // Preserve the existing no-direction behavior; no reveal or turn is implied.
        return direction == Vector2Int.zero ? Grid.GetCurrentCell() : Move(direction.x, direction.y);
    }

    public void Turn(int clockwiseQuarterTurns) => Facing = Normalize((int)Facing + clockwiseQuarterTurns % 4);

    public RunMapCellModel MoveForward()
    {
        switch (Facing)
        {
            case SecondFloorFacing.East: return Move(1, 0);
            case SecondFloorFacing.South: return Move(0, -1);
            case SecondFloorFacing.West: return Move(-1, 0);
            default: return Move(0, 1);
        }
    }

    public void RevealCurrentNeighbors()
    {
        Reveal(X, Y);
        Reveal(X, Y + 1);
        Reveal(X, Y - 1);
        Reveal(X - 1, Y);
        Reveal(X + 1, Y);
    }

    public bool Reveal(int x, int y)
    {
        RunMapCellModel cell = Grid.GetCell(x, y);
        if (cell == null || !cell.isAvailable) return false;
        bool changed = !cell.isRevealed;
        cell.isRevealed = true;
        return changed;
    }

    private static SecondFloorFacing Normalize(int value) => (SecondFloorFacing)((value % 4 + 4) % 4);
}
