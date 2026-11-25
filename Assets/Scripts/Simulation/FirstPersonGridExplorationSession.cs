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
/// Cardinal movement and a straight walkable ray ahead. The legacy strip constructor preserves
/// its bounds and boss reveal rules; fixed floor maps supply their own directed step query.
/// </summary>
public sealed class FirstPersonGridExplorationSession
{
    private readonly int width;
    private readonly int height;
    private readonly int bossX;
    private readonly int bossY;
    private readonly Func<int, int, bool> isPath;
    private readonly Func<int, int, bool> isBossHostStrip;
    private readonly Func<ExplorationGridPosition, ExplorationGridPosition, bool> canStep;
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
        canStep = (_, to) => IsWalkable(to.X, to.Y);
        // Legacy spawn facing checks paths only, even when the boss is already visible.
        InitializeFacing((_, to) => isPath(to.X, to.Y));
    }

    public FirstPersonGridExplorationSession(int startX, int startY, Func<int, int, bool> hasTile,
        Func<ExplorationGridPosition, ExplorationGridPosition, bool> canStep)
    {
        X = startX;
        Y = startY;
        isPath = hasTile ?? throw new ArgumentNullException(nameof(hasTile));
        this.canStep = canStep ?? throw new ArgumentNullException(nameof(canStep));
        if (!isPath(X, Y)) throw new ArgumentException("Exploration must begin on a floor tile.", nameof(hasTile));
        InitializeFacing(canStep);
    }

    private void InitializeFacing(Func<ExplorationGridPosition, ExplorationGridPosition, bool> initialStep)
    {
        var start = new ExplorationGridPosition(X, Y);
        for (int i = 0; i < 4; i++)
        {
            Facing = (SecondFloorFacing)i;
            if (initialStep(start, new ExplorationGridPosition(X + FacingX, Y + FacingY))) break;
            Facing = SecondFloorFacing.North;
        }
        RefreshVisibility();
    }

    public bool IsWalkable(int x, int y) => isBossHostStrip == null ? isPath(x, y) :
        x >= 0 && y >= 0 && x < width && y < height && (isPath(x, y) || BossVisible && x == bossX && y == bossY);

    public void Turn(int clockwiseQuarterTurns)
    {
        Facing = (SecondFloorFacing)(((int)Facing + clockwiseQuarterTurns % 4 + 4) % 4);
        RefreshVisibility();
    }

    public bool MoveForward()
    {
        int x = X + FacingX, y = Y + FacingY;
        if (!canStep(new ExplorationGridPosition(X, Y), new ExplorationGridPosition(x, y))) return false;
        X = x; Y = y;
        if (isBossHostStrip?.Invoke(x, y) == true) BossVisible = true;
        RefreshVisibility();
        return true;
    }

    public void RefreshVisibility()
    {
        visible.Clear();
        var from = new ExplorationGridPosition(X, Y);
        visible.Add(from);
        while (true)
        {
            var to = new ExplorationGridPosition(from.X + FacingX, from.Y + FacingY);
            if (!canStep(from, to)) break;
            visible.Add(to);
            from = to;
        }
    }
}
