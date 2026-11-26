using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

public class SecondFloorEncounterCatalogTests
{
    [Test]
    public void ShippedCatalogCoversAllFirstFloorFormationsWithAValidDisplayEnemy()
    {
        var catalog = SecondFloorEncounterCatalog.LoadDefault();
        Assert.That(GameDataDatabase.TryGetChapterData(1, out var chapter), Is.True);
        CheckPool(SecondFloorEncounterPool.Begin, chapter.BeginPool, 3);
        CheckPool(SecondFloorEncounterPool.Mid, chapter.MidPool, 4);
        CheckPool(SecondFloorEncounterPool.Normal, chapter.NormalPool, 12);
        CheckPool(SecondFloorEncounterPool.Elite, chapter.ElitePool, 4);
        CheckPool(SecondFloorEncounterPool.Boss, chapter.BossPool, 3);

        void CheckPool(SecondFloorEncounterPool pool, int[] levelIds, int expectedCount)
        {
            var encounters = catalog.GetPool(pool);
            Assert.That(encounters.Count, Is.EqualTo(expectedCount), pool.ToString());
            Assert.That(encounters.Select(e => e.SourceLevelId).Distinct(), Is.EqualTo(levelIds));
            foreach (int levelId in levelIds)
            {
                var source = GameDataDatabase.LevelData[levelId];
                var groups = source.randomEnemyGroups ?? Array.Empty<LevelEnemyGroupData>();
                var formations = encounters.Where(e => e.SourceLevelId == levelId);
                Assert.That(formations.Select(e => e.SourceGroupIndex),
                    Is.EqualTo(groups.Length == 0 ? new[] { -1 } : Enumerable.Range(0, groups.Length).ToArray()));
                foreach (var formation in formations)
                {
                    var level = formation.CreateBattleLevel();
                    var placements = groups.Length == 0 ? source.enemies : groups[formation.SourceGroupIndex].enemies;
                    Assert.That(placements.Select(e => e.enemyId), Does.Contain(formation.DisplayEnemyId));
                    Assert.That(level.enemies.Select(e => (e.enemyId, e.x, e.y)),
                        Is.EqualTo(placements.Select(e => (e.enemyId, e.x, e.y))));
                    Assert.That(level.randomEnemyGroups, Is.Empty);
                    Assert.That(level.numericId, Is.EqualTo(source.numericId));
                    Assert.That(level.id, Is.EqualTo(source.id));
                    Assert.That(level.titleKey, Is.EqualTo(source.titleKey));
                    Assert.That(level.levelType, Is.EqualTo(source.levelType));
                    Assert.That(level.rewardPoolId, Is.EqualTo(source.rewardPoolId));
                    Assert.That(formation.IsBoss, Is.EqualTo(pool == SecondFloorEncounterPool.Boss));
                }
            }
        }
    }

    [TestCase("combat", 1, SecondFloorEncounterPool.Begin)]
    [TestCase("combat", 2, SecondFloorEncounterPool.Begin)]
    [TestCase("combat", 3, SecondFloorEncounterPool.Mid)]
    [TestCase("combat", 4, SecondFloorEncounterPool.Mid)]
    [TestCase("combat", 5, SecondFloorEncounterPool.Normal)]
    [TestCase("combat", 999, SecondFloorEncounterPool.Normal)]
    [TestCase("elite", 1, SecondFloorEncounterPool.Elite)]
    [TestCase("elite", 999, SecondFloorEncounterPool.Elite)]
    [TestCase("boss", 1, SecondFloorEncounterPool.Boss)]
    [TestCase("boss", 999, SecondFloorEncounterPool.Boss)]
    public void ResolvesIntoTheExpectedRankBand(string type, int rank, SecondFloorEncounterPool pool)
    {
        var catalog = SecondFloorEncounterCatalog.LoadDefault();
        Assert.That(catalog.TryResolve(4, Vector2Int.zero, type, rank, out var formation), Is.True);
        Assert.That(formation.Pool, Is.EqualTo(pool));
        Assert.That(catalog.TryResolve(4, Vector2Int.zero, "event", rank, out var nonBattle), Is.False);
        Assert.That(nonBattle, Is.Null);
    }

    [Test]
    public void ResolutionIsStableAcrossCatalogLoadsAndOutOfOrderCalls()
    {
        var first = SecondFloorEncounterCatalog.LoadDefault();
        var second = SecondFloorEncounterCatalog.LoadDefault();
        var position = new Vector2Int(-17, 8);
        const int seed = -7201;
        Assert.That(first.TryResolve(seed, position, "combat", 5, out var original), Is.True);
        Assert.That(second.TryResolve(seed, position, "boss", 1, out var boss), Is.True);
        Assert.That(second.TryResolve(seed, position, "combat", 5, out var repeated), Is.True);
        Assert.That((original.SourceLevelId, original.SourceGroupIndex, original.DisplayEnemyId), Is.EqualTo((10, -1, 12)));
        Assert.That((repeated.SourceLevelId, repeated.SourceGroupIndex, repeated.DisplayEnemyId), Is.EqualTo((10, -1, 12)));
        Assert.That((boss.SourceLevelId, boss.SourceGroupIndex, boss.DisplayEnemyId), Is.EqualTo((23, 2, 24)));
        Assert.That(second.TryResolve(8, position, "combat", 5, out var otherSeed), Is.True);
        Assert.That((otherSeed.SourceLevelId, otherSeed.SourceGroupIndex, otherSeed.DisplayEnemyId), Is.EqualTo((14, 1, 9)));
    }

    [Test]
    public void PinnedBossCloneUsesTheVariantPositionAndIconWithoutMutatingTheSource()
    {
        var catalog = SecondFloorEncounterCatalog.LoadDefault();
        var boss = catalog.GetPool(SecondFloorEncounterPool.Boss).Single(e => e.SourceGroupIndex == 2);
        var source = GameDataDatabase.LevelData[boss.SourceLevelId];
        var sourceGroup = source.randomEnemyGroups[boss.SourceGroupIndex];
        var level = boss.CreateBattleLevel();
        Assert.That(level, Is.Not.SameAs(source));
        Assert.That(level.mapIconPath, Is.EqualTo(sourceGroup.mapIconPath));
        Assert.That(level.enemies[0].enemyId, Is.EqualTo(24));
        Assert.That(level.enemies[0].y, Is.EqualTo(500));
        level.enemies[0].y = -100;
        Assert.That(sourceGroup.enemies[0].y, Is.EqualTo(500));
        Assert.That(boss.CreateBattleLevel().enemies[0].y, Is.EqualTo(500));
    }

    [Test]
    public void RejectsDisplayEnemyThatOccursOnlyInADifferentVariant()
    {
        var asset = Resources.Load<TextAsset>(SecondFloorEncounterCatalog.ResourcePath);
        Assert.That(asset, Is.Not.Null);
        var json = JObject.Parse(asset.text);
        var group = json["entries"].Single(e => (int)e["levelId"] == 23 && (int)e["groupIndex"] == 0);
        group["displayEnemyId"] = 24;
        var exception = Assert.Throws<FormatException>(() => SecondFloorEncounterCatalog.Read(json.ToString(),
            GameDataDatabase.ChapterData[1], GameDataDatabase.LevelData));
        Assert.That(exception.Message, Does.Contain("not a member of level 23, group 0"));
    }
}
