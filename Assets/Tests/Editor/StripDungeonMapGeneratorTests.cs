using NUnit.Framework;
using UnityEngine;

public class StripDungeonMapGeneratorTests
{
    [Test]
    public void ConfiguredSeeds_MeetAllStripDungeonConstraints()
    {
        StripDungeonMapConfig config = Resources.Load<StripDungeonMapConfig>("Config/StripDungeonMapConfig");
        Assert.That(config, Is.Not.Null);

        for (int seed = 1; seed <= 100; seed++)
        {
            bool generated = StripDungeonMapGenerator.TryGenerate(config, seed, out StripDungeonMap map, out string error);

            Assert.That(generated, Is.True, $"Seed {seed}: {error}");
            Assert.That(StripDungeonMapGenerator.Validate(map, config, out error), Is.True, $"Seed {seed}: {error}");
            Assert.That(map.IsBossVisible, Is.False);
            for (int stripIndex = 0; stripIndex < map.strips.Count; stripIndex++)
            {
                StripDungeonStrip strip = map.strips[stripIndex];
                StripDungeonCell first = map.GetCell(strip.cells[0]);
                StripDungeonCell last = map.GetCell(strip.cells[strip.cells.Count - 1]);
                Assert.That(
                    first.stripIds.Count == 1 && first.isContent || last.stripIds.Count == 1 && last.isContent,
                    Is.True,
                    $"Seed {seed} 的条带 {stripIndex} 缺少非重叠端点内容。");
            }

            StripDungeonCell nonHostCell = null;
            for (int i = 0; i < map.cells.Count; i++)
            {
                StripDungeonCell cell = map.cells[i];
                if (cell != null && cell.kind == StripDungeonCellKind.Path && !cell.stripIds.Contains(map.bossHostStripId))
                {
                    nonHostCell = cell;
                    break;
                }
            }

            Assert.That(nonHostCell, Is.Not.Null, $"Seed {seed} 缺少非 Boss 所属条带。");
            map.RevealBossIfOnHostStrip(nonHostCell.position);
            Assert.That(map.IsBossVisible, Is.False, $"Seed {seed} 不应由其他条带揭示 Boss。");

            map.RevealBossIfOnHostStrip(map.bossEntrancePosition);
            Assert.That(map.IsBossVisible, Is.True, $"Seed {seed} 进入 Boss 所属条带后应揭示 Boss。");
        }
    }
}
