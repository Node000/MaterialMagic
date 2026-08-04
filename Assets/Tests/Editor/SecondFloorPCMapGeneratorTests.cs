using NUnit.Framework;
using UnityEngine;

public class SecondFloorPCMapGeneratorTests
{
    [Test]
    public void ConfiguredSeeds_CreateFourRegionsWithEliteBoundaryConnectionsAndOppositeBoss()
    {
        SecondFloorPCMapConfig config = Resources.Load<SecondFloorPCMapConfig>("Config/SecondFloorPCMapConfig");
        Assert.That(config, Is.Not.Null);

        LevelData battle = new LevelData { numericId = 1, id = "battle", levelType = LevelType.Battle };
        LevelData elite = new LevelData { numericId = 2, id = "elite", levelType = LevelType.Elite };
        LevelData boss = new LevelData { numericId = 3, id = "boss", levelType = LevelType.Battle };

        for (int seed = 1; seed <= 100; seed++)
        {
            bool generated = SecondFloorPCMapGenerator.TryGenerate(config, seed, type => type == LevelType.Elite ? elite : battle, boss, out RunMapGridModel grid, out string error);

            Assert.That(generated, Is.True, $"Seed {seed}: {error}");
            Assert.That(grid, Is.Not.Null);
            Assert.That(grid.cells.Count, Is.GreaterThan(0));

            AssertEliteConnector(grid, new Vector2Int(config.RegionWidth, config.RegionHeight / 2), elite, seed);
            AssertEliteConnector(grid, new Vector2Int(config.RegionWidth, config.RegionHeight + 1 + config.RegionHeight / 2), elite, seed);
            AssertEliteConnector(grid, new Vector2Int(config.RegionWidth / 2, config.RegionHeight), elite, seed);
            AssertEliteConnector(grid, new Vector2Int(config.RegionWidth + 1 + config.RegionWidth / 2, config.RegionHeight), elite, seed);

            int startRegion = GetRegion(grid.playerX, grid.playerY, config);
            RunMapCellModel bossCell = null;
            for (int i = 0; i < grid.cells.Count; i++)
            {
                if (grid.cells[i] != null && grid.cells[i].isBoss)
                {
                    bossCell = grid.cells[i];
                    break;
                }
            }
            Assert.That(bossCell, Is.Not.Null);
            Assert.That(GetRegion(bossCell.x, bossCell.y, config), Is.EqualTo(OppositeRegion(startRegion)));

            for (int i = 0; i < grid.cells.Count; i++)
                Assert.That(grid.IsCellReachable(grid.cells[i].x, grid.cells[i].y), Is.True, $"Seed {seed} 存在不可达格子。");
        }
    }

    private static void AssertEliteConnector(RunMapGridModel grid, Vector2Int position, LevelData elite, int seed)
    {
        RunMapCellModel cell = grid.GetCell(position.x, position.y);
        Assert.That(cell, Is.Not.Null, $"Seed {seed} 缺少边界连接格 {position}。");
        Assert.That(cell.level, Is.SameAs(elite), $"Seed {seed} 的边界连接格 {position} 必须为精英。");
    }

    private static int GetRegion(int x, int y, SecondFloorPCMapConfig config)
    {
        int regionX = x > config.RegionWidth ? 1 : 0;
        int regionY = y > config.RegionHeight ? 1 : 0;
        return regionY * 2 + regionX;
    }

    private static int OppositeRegion(int region)
    {
        return region == 0 ? 3 : region == 1 ? 2 : region == 2 ? 1 : 0;
    }
}
