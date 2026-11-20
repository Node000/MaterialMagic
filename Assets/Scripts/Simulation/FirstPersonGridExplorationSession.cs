using System;
using System.Collections.Generic;

/// <summary>Value-only grid coordinate; no Unity map/config/view objects cross the rule boundary.</summary>
public readonly struct ExplorationGridPosition : IEquatable<ExplorationGridPosition>
{
    public int X { get; }
    public int Y { get; }
    public ExplorationGridPosition(int x, int y) { X = x; Y = y; }
    public bool Equals(ExplorationGridPosition other) => X == other.X && Y == other.Y;
    public override bool Equals(object other) => other is ExplorationGridPosition position && Equals(position);
    public override int GetHashCode() => unchecked(X * 397 ^ Y);
}

/// <summary>
/// The strip first-person prototype's actual movement/reveal rules. Visibility is the current cell
/// plus the straight walkable ray ahead, not accumulated fog or the RunMap cross. Moving onto the
/// boss host strip reveals the boss BEFORE refreshing the ray. Turns never reveal the boss.
/// </summary>
public sealed class FirstPersonGridExplorationSession
{
    private readonly int width;
    private readonly int height;
    private readonly int bossX;
    private readonly int bossY;
    private readonly Func<int, int, bool> isPath;
    private readonly Func<int, int, bool> isBossHostStrip;
    private readonly List<ExplorationGridPosition> visible = new List<ExplorationGridPosition>();
    public int X { get; private set; }
    public int Y { get; private set; }
    public SecondFloorFacing Facing { get; private set; }
    public bool BossVisible { get; private set; }
    public int FacingX => Facing == SecondFloorFacing.East ? 1 : Facing == SecondFloorFacing.West ? -1 : 0;
    public int FacingY => Facing == SecondFloorFacing.North ? 1 : Facing == SecondFloorFacing.South ? -1 : 0;
    public IReadOnlyList<ExplorationGridPosition> VisiblePositions => visible.AsReadOnly();

    public FirstPersonGridExplorationSession(int width, int height, int startX, int startY,
        int bossX, int bossY, bool bossVisible, Func<int, int, bool> isPath, Func<int, int, bool> isBossHostStrip)
    {
        this.width = width; this.height = height; X = startX; Y = startY;
        this.bossX = bossX; this.bossY = bossY; BossVisible = bossVisible;
        this.isPath = isPath ?? throw new ArgumentNullException(nameof(isPath));
        this.isBossHostStrip = isBossHostStrip ?? throw new ArgumentNullException(nameof(isBossHostStrip));
        Facing = SecondFloorFacing.North;
        // Preserve North/East/South/West preference and path-only initial-facing selection.
        for (int i = 0; i < 4; i++)
        {
            var candidate = (SecondFloorFacing)i;
            int dx = candidate == SecondFloorFacing.East ? 1 : candidate == SecondFloorFacing.West ? -1 : 0;
            int dy = candidate == SecondFloorFacing.North ? 1 : candidate == SecondFloorFacing.South ? -1 : 0;
            if (!isPath(X + dx, Y + dy)) continue;
            Facing = candidate;
            break;
        }
        RefreshVisibility();
    }

    public bool IsWalkable(int x, int y) => x >= 0 && y >= 0 && x < width && y < height
        && (isPath(x, y) || BossVisible && x == bossX && y == bossY);

    public void Turn(int clockwiseQuarterTurns)
    {
        Facing = (SecondFloorFacing)(((int)Facing + clockwiseQuarterTurns % 4 + 4) % 4);
        RefreshVisibility();
    }

    public bool MoveForward()
    {
        int x = X + FacingX, y = Y + FacingY;
        if (!IsWalkable(x, y)) return false;
        X = x; Y = y;
        if (isBossHostStrip(x, y)) BossVisible = true;
        RefreshVisibility();
        return true;
    }

    public void RefreshVisibility()
    {
        visible.Clear();
        visible.Add(new ExplorationGridPosition(X, Y));
        for (int x = X + FacingX, y = Y + FacingY; IsWalkable(x, y); x += FacingX, y += FacingY)
            visible.Add(new ExplorationGridPosition(x, y));
    }
}
