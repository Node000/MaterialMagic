using NUnit.Framework;

public class SecondFloorExplorationSessionTests
{
    private static RunMapGridModel Grid()
    {
        var grid = new RunMapGridModel { width = 3, height = 3, playerX = 1, playerY = 1 };
        for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++) grid.cells.Add(new RunMapCellModel { x = x, y = y });
        return grid;
    }

    [Test]
    public void RevealUsesProductionCrossNotPrototypeRayOrStrip()
    {
        var grid = Grid();
        var session = new SecondFloorExplorationSession(grid);
        session.RevealCurrentNeighbors();
        Assert.That(grid.cells.FindAll(c => c.isRevealed).Count, Is.EqualTo(5));
        Assert.That(grid.GetCell(0, 0).isRevealed, Is.False);
        session.Turn(1);
        Assert.That(session.Facing, Is.EqualTo(SecondFloorFacing.East));
        Assert.That(grid.cells.FindAll(c => c.isRevealed).Count, Is.EqualTo(5));
        Assert.That(session.X, Is.EqualTo(1));
        Assert.That(session.Y, Is.EqualTo(1));
    }

    [Test]
    public void AbsoluteMaterialMoveDoesNotDependOnFacingOrConsumeContent()
    {
        var grid = Grid();
        var level = new LevelData { numericId = 9, levelType = LevelType.Event };
        grid.GetCell(1, 2).level = level;
        grid.GetCell(1, 2).isHidden = true;
        var session = new SecondFloorExplorationSession(grid, SecondFloorFacing.South);
        Assert.That(session.Move(MaterialEnum.Fire), Is.SameAs(grid.GetCell(1, 2)));
        Assert.That(session.Y, Is.EqualTo(2));
        Assert.That(session.Facing, Is.EqualTo(SecondFloorFacing.South));
        Assert.That(grid.GetCell(1, 2).level, Is.SameAs(level));
        Assert.That(grid.GetCell(1, 2).isHidden, Is.True);
        Assert.That(session.Move(MaterialEnum.None), Is.SameAs(grid.GetCell(1, 2)));
    }

    [Test]
    public void RejectedMovePreservesPositionAndFogAndBossGate()
    {
        var grid = Grid();
        grid.GetCell(1, 2).isAvailable = false;
        var session = new SecondFloorExplorationSession(grid);
        Assert.That(session.Move(0, 1), Is.Null);
        Assert.That(session.Move(1, 1), Is.Null);
        Assert.That(grid.cells.Exists(c => c.isRevealed), Is.False);
        Assert.That(session.X, Is.EqualTo(1));
        grid.GetCell(1, 2).isAvailable = true;
        grid.GetCell(1, 2).isBoss = true;
        grid.GetCell(1, 2).isEnd = true;
        grid.GetCell(1, 0).isBoss = true;
        Assert.That(session.CanMove(0, 1), Is.False);
        grid.GetCell(1, 0).isCompleted = true;
        Assert.That(session.Move(0, 1), Is.Not.Null);
    }

    [Test]
    public void FirstPersonPrototypeUsesFacingRayAndHostStripBossReveal()
    {
        var session = new FirstPersonGridExplorationSession(5, 4, 1, 1, 2, 2, false,
            (x, y) => x == 1 && y >= 1 && y <= 3,
            (x, y) => x == 1 && y == 2);
        Assert.That(session.Facing, Is.EqualTo(SecondFloorFacing.North));
        Assert.That(session.VisiblePositions.Count, Is.EqualTo(3), "Current cell plus full forward ray, not a cross.");
        Assert.That(session.BossVisible, Is.False);
        session.Turn(1);
        Assert.That(session.MoveForward(), Is.False, "Hidden boss/empty cells are not walkable.");
        Assert.That(session.X, Is.EqualTo(1));
        Assert.That(session.VisiblePositions.Count, Is.EqualTo(1), "Turning replaces visibility; it does not accumulate fog.");
        session.Turn(-1);
        Assert.That(session.MoveForward(), Is.True);
        Assert.That(session.BossVisible, Is.True, "Only entering the host strip reveals the boss.");
        session.Turn(1);
        Assert.That(session.VisiblePositions.Count, Is.EqualTo(2), "Revealed boss participates in the facing ray.");
        Assert.That(session.MoveForward(), Is.True);
        Assert.That(session.X, Is.EqualTo(2));
        Assert.That(session.Y, Is.EqualTo(2));
        Assert.That(session.MoveForward(), Is.False);
    }

    [Test]
    public void RunManagerRebindsSessionWhenAttachingGrid()
    {
        var run = new RunManager(new PlayerStatus(50, 0, PlayerStateServices.Simulation, new RunDifficultySaveData()));
        var first = Grid();
        run.AttachMapGrid(first);
        Assert.That(run.MoveMapPlayer(MaterialEnum.Earth), Is.SameAs(first.GetCell(2, 1)));
        var second = Grid();
        run.AttachMapGrid(second);
        Assert.That(run.Exploration.Grid, Is.SameAs(second));
        Assert.That(run.MoveMapPlayer(MaterialEnum.Water), Is.SameAs(second.GetCell(1, 0)));
        Assert.That(first.playerX, Is.EqualTo(2));
    }
}
