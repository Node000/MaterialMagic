using System;
using NUnit.Framework;

public class BonusGridSessionTests
{
    private static BonusLevelData Grid(params BonusRewardData[] rewards)
    {
        return new BonusLevelData { radius = 1, drawCount = 1, rewards = rewards };
    }

    private static BonusRewardData Gold(int amount = 3)
        => new BonusRewardData { rewardType = BonusRewardType.Gold, amount = amount, rewardName = "gold" };

    private static PlayerState Player(int health = 10, int gold = 0)
        => new PlayerState(health, gold, PlayerStateServices.Simulation, new RunDifficultySaveData());

    [Test]
    public void SameSeedAndStepGenerateSameGridWithoutAScene()
    {
        var data = Grid(Gold(1), Gold(2), new BonusRewardData { rewardType = BonusRewardType.Heal, amount = 4 });
        data.radius = 2;
        var first = new BonusGridSession(data, 12345, 7);
        var second = new BonusGridSession(data, 12345, 7);
        Assert.That(first.Position, Is.EqualTo(new BonusGridPosition(2, 2)));
        Assert.That(first.RandomCalls, Is.EqualTo(24));
        Assert.That(first.GetCell(2, 2).Reward, Is.Null);
        for (int i = 0; i < first.CellCount; i++)
        {
            Assert.That(first.GetCell(i).Reward?.Amount, Is.EqualTo(second.GetCell(i).Reward?.Amount));
            Assert.That(first.GetCell(i).Reward?.RewardType, Is.EqualTo(second.GetCell(i).Reward?.RewardType));
        }
    }

    [Test]
    public void RandomCallsKeepRowMajorOrderAndSkipOnlyTheCenter()
    {
        var pool = new BonusRewardData[8];
        for (int i = 0; i < pool.Length; i++) pool[i] = Gold(10 + i);
        int calls = 0;
        var session = new BonusGridSession(Grid(pool), (min, max) =>
        {
            Assert.That(min, Is.Zero);
            Assert.That(max, Is.EqualTo(8));
            return calls++;
        });
        Assert.That(calls, Is.EqualTo(8));
        Assert.That(session.GetCell(0, 0).Reward.Amount, Is.EqualTo(10));
        Assert.That(session.GetCell(2, 0).Reward.Amount, Is.EqualTo(12));
        Assert.That(session.GetCell(0, 1).Reward.Amount, Is.EqualTo(13));
        Assert.That(session.GetCell(1, 1).Reward, Is.Null);
        Assert.That(session.GetCell(2, 1).Reward.Amount, Is.EqualTo(14));
        Assert.That(session.GetCell(2, 2).Reward.Amount, Is.EqualTo(17));
        Assert.That(session.GetIndex(2, 1), Is.EqualTo(5));
    }

    [Test]
    public void SingletonAndNullEntriesConsumeRandomButEmptyPoolsDoNot()
    {
        int calls = 0;
        var singleton = new BonusGridSession(Grid(Gold()), (min, max) => { calls++; return 0; });
        Assert.That(calls, Is.EqualTo(8));
        var nullEntry = new BonusGridSession(Grid(new BonusRewardData[] { null }), (min, max) => { calls++; return 0; });
        Assert.That(calls, Is.EqualTo(16));
        Assert.That(nullEntry.Step(MaterialEnum.Fire).CollectedNow, Is.False);
        new BonusGridSession(Grid(), (min, max) => { Assert.Fail("Empty pool must not draw."); return 0; });
        new BonusGridSession(Grid((BonusRewardData[])null), (min, max) => { Assert.Fail("Null pool must not draw."); return 0; });
        Assert.That(singleton.RandomCalls, Is.EqualTo(8));
    }

    [Test]
    public void GenerationConsumesTheSameRunRandomStepsAsTheOldLoop()
    {
        const int seed = 54321;
        int step = 11;
        var data = Grid(Gold(1), Gold(2));
        var session = new BonusGridSession(data, (min, max) => RunRandom.Range(seed, step++, min, max));
        int expectedStep = 11;
        for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
            {
                if (x == 1 && y == 1) continue;
                int expected = RunRandom.Range(seed, expectedStep++, 0, 2) + 1;
                Assert.That(session.GetCell(x, y).Reward.Amount, Is.EqualTo(expected));
            }
        Assert.That(step, Is.EqualTo(19));
        Assert.That(RunRandom.Range(seed, step, 0, 100), Is.EqualTo(RunRandom.Range(seed, expectedStep, 0, 100)));
    }

    [Test]
    public void EdgesStopWithoutCollectingAgainAndUnsupportedDirectionsDoNothing()
    {
        var session = new BonusGridSession(Grid(Gold()), 123);
        var up = session.Step(MaterialEnum.Fire);
        Assert.That(up.Before, Is.EqualTo(new BonusGridPosition(1, 1)));
        Assert.That(up.After, Is.EqualTo(new BonusGridPosition(1, 2)));
        Assert.That(up.CellBefore.Collected, Is.False);
        Assert.That(up.CellAfter.Collected, Is.True);
        var edge = session.Step(MaterialEnum.Fire);
        Assert.That(edge.EdgeStopped, Is.True);
        Assert.That(edge.Moved, Is.False);
        Assert.That(edge.CollectedNow, Is.False);
        foreach (MaterialEnum material in new[] { MaterialEnum.None, (MaterialEnum)9999 })
        {
            var ignored = session.Step(material);
            Assert.That(ignored.IsDirection, Is.False);
            Assert.That(ignored.EdgeStopped, Is.False);
            Assert.That(ignored.After, Is.EqualTo(edge.After));
        }
        Assert.That(session.Contains(-1, 1), Is.False);
        Assert.That(session.Contains(3, 1), Is.False);
        Assert.Throws<ArgumentOutOfRangeException>(() => session.GetCell(3, 1));
    }

    [Test]
    public void AllFourDirectionsClampAtTheirOwnEdges()
    {
        foreach (MaterialEnum material in new[] { MaterialEnum.Fire, MaterialEnum.Wind, MaterialEnum.Water, MaterialEnum.Earth })
        {
            var session = new BonusGridSession(Grid(Gold()), 123);
            var first = session.Step(material);
            var blocked = session.Step(material);
            Assert.That(first.Moved, Is.True);
            Assert.That(blocked.EdgeStopped, Is.True);
            Assert.That(blocked.After, Is.EqualTo(first.After));
        }
    }

    [Test]
    public void SplitPathsKeepPositionAndPerCellCollectionWithoutDrawCountLimit()
    {
        var data = Grid(Gold());
        var session = new BonusGridSession(data, 123);
        session.ResolvePath(new[] { MaterialEnum.Fire, MaterialEnum.Water });
        var second = session.ResolvePath(new[] { MaterialEnum.Fire, MaterialEnum.Earth });
        Assert.That(second[0].Moved, Is.True);
        Assert.That(second[0].CollectedNow, Is.False);
        Assert.That(second[1].CollectedNow, Is.True);
        Assert.That(session.Position, Is.EqualTo(new BonusGridPosition(2, 2)));
        Assert.That(session.GetCell(1, 1).Collected, Is.False);
        Assert.That(session.DrawCount, Is.EqualTo(1));
    }

    [Test]
    public void RepeatedRewardDefinitionsStillBelongToSeparateCellsAndSnapshotInput()
    {
        BonusRewardData shared = Gold(2);
        var session = new BonusGridSession(Grid(shared), 123);
        shared.amount = 100;
        var first = session.Step(MaterialEnum.Fire);
        var second = session.Step(MaterialEnum.Earth);
        Assert.That(first.CollectedNow, Is.True);
        Assert.That(second.CollectedNow, Is.True);
        Assert.That(first.CollectedReward.Amount, Is.EqualTo(2));
        BonusRewardData copy = first.CollectedReward.ToData();
        copy.amount = 200;
        Assert.That(first.CollectedReward.Amount, Is.EqualTo(2));
    }

    [Test]
    public void GoldRulesApplyDifficultyAndRelicOnceAndAreIdempotentForTheStep()
    {
        var player = Player(gold: 10);
        var relic = new GoldBonusRelicModel(new RelicData { numericId = 99971, value = 1 });
        Assert.That(player.Relics.TryAcquire(relic), Is.EqualTo(RelicAcquireResult.Acquired));
        int goldFacts = 0;
        player.Relics.Resolved += fact => { if (fact.Kind == RelicEventKind.GoldChanged) goldFacts++; };
        var session = new BonusGridSession(Grid(Gold(3)), 123);
        var step = session.Step(MaterialEnum.Fire);
        Assert.That(player.Gold, Is.EqualTo(10), "Collecting a grid cell alone does not implicitly apply its reward.");
        int difficultyCalls = 0;
        BonusGridRewardResult result = BonusGridRewardRules.Apply(step, player, amount => { difficultyCalls++; return amount * 2; });
        Assert.That(result.GoldBefore, Is.EqualTo(10));
        Assert.That(result.GoldAfter, Is.EqualTo(17));
        Assert.That(result.GoldGained, Is.EqualTo(7));
        Assert.That(result.HealthGained, Is.Zero);
        Assert.That(BonusGridRewardRules.Apply(step, player, amount => { Assert.Fail("Cannot modify a committed reward twice."); return amount; }), Is.SameAs(result));
        Assert.That(difficultyCalls, Is.EqualTo(1));
        Assert.That(goldFacts, Is.EqualTo(1));
        session.Step(MaterialEnum.Water);
        BonusGridRewardRules.Apply(session.Step(MaterialEnum.Fire), player, amount => amount);
        Assert.That(player.Gold, Is.EqualTo(17));
        BonusGridRewardRules.Apply(session.Step(MaterialEnum.Earth), player, amount => amount);
        Assert.That(player.Gold, Is.EqualTo(21), "A second cell is one separate logical gain.");
    }

    [Test]
    public void GoldFeedbackCanBeDeferredUntilPresentationWithoutAnotherLogicalGain()
    {
        int feedbacks = 0;
        var services = new PlayerStateServices(goldFeedback: () => feedbacks++);
        var player = new PlayerState(10, 2, services, new RunDifficultySaveData());
        var step = new BonusGridSession(Grid(Gold(3)), 123).Step(MaterialEnum.Fire);
        var result = BonusGridRewardRules.Apply(step, player, amount => amount, playGoldFeedback: false);
        Assert.That(result.GoldBefore, Is.EqualTo(2));
        Assert.That(result.GoldAfter, Is.EqualTo(5));
        Assert.That(feedbacks, Is.Zero);
        BonusGridRewardRules.Apply(step, player, amount => amount);
        Assert.That(feedbacks, Is.Zero, "Replaying submission must not implicitly play deferred feedback.");
        services.OnGoldGained(); // The presentation cue, independent of rule submission.
        Assert.That(feedbacks, Is.EqualTo(1));
        Assert.That(player.Gold, Is.EqualTo(5));
    }

    [Test]
    public void SynchronousGoldObserversCannotReenterTheSameReward()
    {
        var player = Player();
        var step = new BonusGridSession(Grid(Gold(3)), 123).Step(MaterialEnum.Fire);
        int attempts = 0;
        player.Relics.Resolved += fact =>
        {
            if (fact.Kind != RelicEventKind.GoldChanged) return;
            attempts++;
            Assert.Throws<InvalidOperationException>(() => BonusGridRewardRules.Apply(step, player, amount => amount));
        };
        var result = BonusGridRewardRules.Apply(step, player, amount => amount);
        Assert.That(attempts, Is.EqualTo(1));
        Assert.That(player.Gold, Is.EqualTo(3));
        Assert.That(result.GoldGained, Is.EqualTo(3));
    }

    [Test]
    public void HostFeedbackFailureAfterGoldMutationCannotMakeTheSameStepPayAgain()
    {
        var services = new PlayerStateServices(goldFeedback: () => { throw new InvalidOperationException("host feedback failed"); });
        var player = new PlayerState(10, 0, services, new RunDifficultySaveData());
        var step = new BonusGridSession(Grid(Gold(3)), 123).Step(MaterialEnum.Fire);
        Assert.Throws<InvalidOperationException>(() => BonusGridRewardRules.Apply(step, player, amount => amount));
        Assert.That(player.Gold, Is.EqualTo(3));
        var result = BonusGridRewardRules.Apply(step, player, amount => amount);
        Assert.That(player.Gold, Is.EqualTo(3));
        Assert.That(result.GoldGained, Is.EqualTo(3));
    }

    [Test]
    public void NoneEdgeAndEmptyCellsProduceNoRewardWhenSubmittedDirectly()
    {
        var player = Player(gold: 2);
        var session = new BonusGridSession(Grid(), 123);
        var results = session.ResolvePath(new[] { MaterialEnum.None, MaterialEnum.Fire, MaterialEnum.Fire, MaterialEnum.Water });
        foreach (var step in results)
        {
            var result = BonusGridRewardRules.Apply(step, player);
            Assert.That(step.CollectedNow, Is.False);
            Assert.That(result.Reward, Is.Null);
            Assert.That(result.GoldGained, Is.Zero);
            Assert.That(result.HealthGained, Is.Zero);
        }
        Assert.That(player.Gold, Is.EqualTo(2));
    }

    [Test]
    public void HealUsesActualCappedDeltaAndCannotRepeatOnRevisit()
    {
        var player = Player();
        player.TakeDirectDamage(2);
        int healedFacts = 0;
        player.Relics.Resolved += fact => { if (fact.Kind == RelicEventKind.Healed) healedFacts++; };
        var session = new BonusGridSession(Grid(new BonusRewardData { rewardType = BonusRewardType.Heal, amount = 5 }), 123);
        var step = session.Step(MaterialEnum.Fire);
        var result = BonusGridRewardRules.Apply(step, player);
        Assert.That(result.HealthBefore, Is.EqualTo(8));
        Assert.That(result.HealthAfter, Is.EqualTo(10));
        Assert.That(result.HealthGained, Is.EqualTo(2));
        BonusGridRewardRules.Apply(step, player);
        session.Step(MaterialEnum.Water);
        BonusGridRewardRules.Apply(session.Step(MaterialEnum.Fire), player);
        BonusGridRewardRules.Apply(session.Step(MaterialEnum.Earth), player);
        Assert.That(player.CurrentHealth, Is.EqualTo(10));
        Assert.That(healedFacts, Is.EqualTo(1));
    }

    [TestCase(0)]
    [TestCase(-2)]
    public void NonPositiveGoldStillCollectsButProductionRewardPolicyDoesNotSpend(int amount)
    {
        var player = Player(gold: 10);
        var session = new BonusGridSession(Grid(Gold(amount)), 123);
        var step = session.Step(MaterialEnum.Fire);
        BonusGridRewardRules.Apply(step, player, value => value);
        Assert.That(step.CollectedNow, Is.True);
        Assert.That(player.Gold, Is.EqualTo(10));
    }

    [Test]
    public void LegacyOwnerlessNegativeGoldPolicyIsExplicitAndDoesNotReceiveRelicBonus()
    {
        var player = Player(gold: 1);
        player.Relics.TryAcquire(new GoldBonusRelicModel(new RelicData { numericId = 99971, value = 1 }));
        var session = new BonusGridSession(Grid(Gold(-3)), 123);
        var result = BonusGridRewardRules.Apply(session.Step(MaterialEnum.Fire), player, value => value, true);
        Assert.That(result.GoldBefore, Is.EqualTo(1));
        Assert.That(result.GoldAfter, Is.Zero);
    }

    [Test]
    public void UnknownRewardsAreConsumedWithoutPlayerChangesAndRadiusDrawDefaultsAreStable()
    {
        var data = Grid(new BonusRewardData { rewardType = (BonusRewardType)999, amount = 5 });
        data.radius = 0;
        data.drawCount = 0;
        var player = Player(gold: 2);
        var session = new BonusGridSession(data, 123);
        var step = session.Step(MaterialEnum.Fire);
        var result = BonusGridRewardRules.Apply(step, player);
        Assert.That(session.GridSize, Is.EqualTo(3));
        Assert.That(session.DrawCount, Is.EqualTo(5));
        Assert.That(step.CollectedNow, Is.True);
        Assert.That(result.GoldGained, Is.Zero);
        Assert.That(result.HealthGained, Is.Zero);
        Assert.Throws<InvalidOperationException>(() => BonusGridRewardRules.Apply(step, Player()));
    }
}
