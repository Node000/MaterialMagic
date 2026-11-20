using System;
using System.Collections.Generic;

/// <summary>A copied reward definition: later edits to the source data cannot change an active grid.</summary>
public sealed class BonusGridReward
{
    public BonusRewardType RewardType { get; }
    public string RewardName { get; }
    public int Amount { get; }
    public string TexturePath { get; }

    internal BonusGridReward(BonusRewardData data)
    {
        RewardType = data.rewardType;
        RewardName = data.rewardName;
        Amount = data.amount;
        TexturePath = data.texturePath;
    }

    public BonusRewardData ToData()
    {
        return new BonusRewardData
        {
            rewardType = RewardType,
            rewardName = RewardName,
            amount = Amount,
            texturePath = TexturePath
        };
    }
}

public readonly struct BonusGridPosition : IEquatable<BonusGridPosition>
{
    public int X { get; }
    public int Y { get; }

    public BonusGridPosition(int x, int y) { X = x; Y = y; }
    public bool Equals(BonusGridPosition other) => X == other.X && Y == other.Y;
    public override bool Equals(object obj) => obj is BonusGridPosition other && Equals(other);
    public override int GetHashCode() => unchecked(X * 397 ^ Y);
    public override string ToString() => "(" + X + ", " + Y + ")";
}

/// <summary>Immutable observation of one cell, including its collection state at the time it was read.</summary>
public sealed class BonusGridCellState
{
    public int Index { get; }
    public BonusGridPosition Position { get; }
    public BonusGridReward Reward { get; }
    public bool Collected { get; }

    internal BonusGridCellState(int index, BonusGridPosition position, BonusGridReward reward, bool collected)
    {
        Index = index;
        Position = position;
        Reward = reward;
        Collected = collected;
    }
}

/// <summary>
/// Committed board transition. RewardRules.Apply can submit its newly collected reward once;
/// presentation may read this result repeatedly without moving or collecting again.
/// </summary>
public sealed class BonusGridStepResult
{
    public MaterialEnum Material { get; }
    public BonusGridPosition Before { get; }
    public BonusGridPosition After { get; }
    public bool IsDirection { get; }
    public bool Moved { get; }
    public bool EdgeStopped => IsDirection && !Moved;
    public BonusGridCellState CellBefore { get; }
    public BonusGridCellState CellAfter { get; }
    public bool CollectedNow { get; }
    public BonusGridReward CollectedReward => CollectedNow ? CellAfter.Reward : null;
    public BonusGridRewardResult AppliedReward { get; internal set; }
    internal bool IsApplyingReward { get; set; }

    internal BonusGridStepResult(MaterialEnum material, BonusGridPosition before, BonusGridPosition after,
        bool isDirection, BonusGridCellState cellBefore, BonusGridCellState cellAfter, bool collectedNow)
    {
        Material = material;
        Before = before;
        After = after;
        IsDirection = isDirection;
        Moved = !before.Equals(after);
        CellBefore = cellBefore;
        CellAfter = cellAfter;
        CollectedNow = collectedNow;
    }
}

/// <summary>
/// Authoritative bonus-grid rules. No views, coroutines, clock, Unity random or scene state.
/// Cells are sampled in y-then-x order, once per non-center cell whenever the pool is nonempty.
/// Step commits only one arrow, allowing production UI to keep its existing step-driven pacing.
/// </summary>
public sealed class BonusGridSession
{
    private readonly BonusGridReward[] rewards;
    private readonly bool[] collected;
    public int Radius { get; }
    public int GridSize { get; }
    public int CenterIndex => Radius;
    public int DrawCount { get; }
    public int CellCount => rewards.Length;
    public BonusGridPosition Position { get; private set; }
    public int RandomCalls { get; private set; }

    public BonusGridSession(BonusLevelData data, int seed, int randomStep = 0)
        : this(data, CreateSeededRandom(seed, randomStep)) { }

    public BonusGridSession(BonusLevelData data, Func<int, int, int> nextRandomInt)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (nextRandomInt == null) throw new ArgumentNullException(nameof(nextRandomInt));
        Radius = Math.Max(1, data.radius);
        GridSize = checked(Radius * 2 + 1);
        DrawCount = data.drawCount > 0 ? data.drawCount : 5;
        Position = new BonusGridPosition(Radius, Radius);
        rewards = new BonusGridReward[checked(GridSize * GridSize)];
        collected = new bool[rewards.Length];
        BonusRewardData[] pool = data.rewards;
        for (int y = 0; y < GridSize; y++)
        {
            for (int x = 0; x < GridSize; x++)
            {
                if (x == CenterIndex && y == CenterIndex || pool == null || pool.Length == 0)
                    continue;
                int choice = nextRandomInt(0, pool.Length);
                RandomCalls++;
                BonusRewardData reward = pool[choice];
                if (reward != null)
                    rewards[GetIndex(x, y)] = new BonusGridReward(reward);
            }
        }
    }

    public bool Contains(int x, int y) => x >= 0 && x < GridSize && y >= 0 && y < GridSize;

    public int GetIndex(int x, int y)
    {
        if (!Contains(x, y)) throw new ArgumentOutOfRangeException(nameof(x), "Cell is outside the bonus grid.");
        return y * GridSize + x;
    }

    public BonusGridCellState GetCell(int x, int y) => GetCell(GetIndex(x, y));

    public BonusGridCellState GetCell(int index)
    {
        if (index < 0 || index >= CellCount) throw new ArgumentOutOfRangeException(nameof(index));
        return new BonusGridCellState(index, new BonusGridPosition(index % GridSize, index / GridSize),
            rewards[index], collected[index]);
    }

    public BonusGridStepResult Step(MaterialEnum material)
    {
        BonusGridPosition before = Position;
        BonusGridPosition direction = GetDirection(material);
        bool isDirection = direction.X != 0 || direction.Y != 0;
        if (isDirection)
        {
            Position = new BonusGridPosition(
                Math.Max(0, Math.Min(GridSize - 1, before.X + direction.X)),
                Math.Max(0, Math.Min(GridSize - 1, before.Y + direction.Y)));
        }

        int index = GetIndex(Position.X, Position.Y);
        BonusGridCellState cellBefore = GetCell(index);
        bool collectedNow = !before.Equals(Position) && !collected[index] && rewards[index] != null;
        if (collectedNow)
            collected[index] = true;
        return new BonusGridStepResult(material, before, Position, isDirection, cellBefore, GetCell(index), collectedNow);
    }

    /// <summary>Headless convenience; UI intentionally calls Step one arrow at a time instead.</summary>
    public IReadOnlyList<BonusGridStepResult> ResolvePath(IEnumerable<MaterialEnum> path)
    {
        if (path == null) throw new ArgumentNullException(nameof(path));
        var results = new List<BonusGridStepResult>();
        foreach (MaterialEnum material in path)
            results.Add(Step(material));
        return results.AsReadOnly();
    }

    public static BonusGridPosition GetDirection(MaterialEnum material)
    {
        switch (material)
        {
            case MaterialEnum.Fire: return new BonusGridPosition(0, 1);
            case MaterialEnum.Wind: return new BonusGridPosition(-1, 0);
            case MaterialEnum.Water: return new BonusGridPosition(0, -1);
            case MaterialEnum.Earth: return new BonusGridPosition(1, 0);
            default: return new BonusGridPosition(0, 0);
        }
    }

    private static Func<int, int, int> CreateSeededRandom(int seed, int randomStep)
    {
        int step = Math.Max(0, randomStep);
        return (min, max) => RunRandom.Range(seed, step++, min, max);
    }
}
