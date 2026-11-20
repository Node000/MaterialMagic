using NUnit.Framework;

public class RunSessionTests
{
    private static RunManager Create()
    {
        var player = new PlayerStatus(50, 0, PlayerStateServices.Simulation, new RunDifficultySaveData());
        player.SetRunRandomState(173, 0);
        return new RunManager(player);
    }
    private static LevelData Level(int id, LevelType type) => new LevelData { numericId = id, levelType = type };

    [Test]
    public void BattleDeckDrawOrderSurvivesSnapshotAndIgnoresUnrelatedRandomRolls()
    {
        var player = Create().PlayerStatus;
        player.DrawOrderVersion = 1;
        for (int i = 0; i < 8; i++)
            player.Deck.Add(new MaterialModel("arrow_" + i, i % 2 == 0 ? MaterialEnum.Fire : MaterialEnum.Water));
        RunLevelRules.ResetNodeDeck(player);
        new BattleManager(player).BeginBattleRules();
        string firstWater = player.DrawPile.Find(card => card.material == MaterialEnum.Water).instanceId;
        var restored = RuleRunSnapshot.RestoreSnapshot(RuleRunSnapshot.ExportSnapshot(player, null));
        restored.NextRunRandomInt(0, 120);
        Assert.That(player.DrawCardToHand(false, card => card.material == MaterialEnum.Water).instanceId, Is.EqualTo(firstWater));
        Assert.That(restored.DrawCardToHand(false, card => card.material == MaterialEnum.Water).instanceId, Is.EqualTo(firstWater));
        foreach (PlayerStatus owner in new[] { player, restored })
        {
            owner.DiscardPile.AddRange(owner.DrawPile);
            owner.DrawPile.Clear();
            Assert.That(owner.DrawCardsToPlayZoneTail(2), Is.EqualTo(2));
        }
        Assert.That(restored.PlayZone[0].instanceId, Is.EqualTo(player.PlayZone[0].instanceId));
        Assert.That(restored.PlayZone[1].instanceId, Is.EqualTo(player.PlayZone[1].instanceId));
    }

    [Test]
    public void TieredBattlesRefillOnlyTheirOwnPoolAndFilterLegacyIds()
    {
        var catalog = new RuleContentSnapshot().AddNumeric(new System.Collections.Generic.Dictionary<int, LevelData>
        {
            [1] = Level(1, LevelType.Battle), [2] = Level(2, LevelType.Battle), [3] = Level(3, LevelType.Battle)
        }).Freeze();
        var player = new PlayerStatus(50, 0, PlayerStateServices.Simulation, new RunDifficultySaveData(), catalog);
        player.SetRunRandomState(173, 0);
        var run = new RunManager(player);
        var chapter = new ChapterData { BeginPool = new[] { 1 }, MidPool = new[] { 2 }, NormalPool = new[] { 3 } };
        run.SetActiveChapter(chapter);
        run.RestorePoolState(new RunPoolSaveData { remainingBeginPool = new[] { 2, -1 } });
        using (player.Rules.Enter())
            foreach (int id in new[] { 1, 1, 2, 2, 2, 3, 3 })
                Assert.That(run.DrawNextBattleLevel(chapter)?.numericId, Is.EqualTo(id));
        Assert.That(run.BattleCount, Is.EqualTo(7));
    }

    [Test]
    public void MaterialModifierDropUsesWeightsAndRequiresEligibleTarget()
    {
        var light = new MaterialModifierData { id = "a", script = nameof(ChargeModifier), inArrowModifierRewardPool = true, weight = 1 };
        var heavy = new MaterialModifierData { id = "b", script = nameof(SturdyModifier), inArrowModifierRewardPool = true, weight = 4 };
        var excluded = new MaterialModifierData { id = "c", script = nameof(KindlingModifier), inArrowModifierRewardPool = true, weight = 0 };
        var catalog = new RuleContentSnapshot().AddNamed(new System.Collections.Generic.Dictionary<string, MaterialModifierData>
        {
            [heavy.id] = heavy, [excluded.id] = excluded, [light.id] = light
        }).Freeze();
        var player = new PlayerStatus(50, 0, PlayerStateServices.Simulation, new RunDifficultySaveData(), catalog, RuleProfilePolicy.AllUnlocked);
        Assert.That(ModifierDropRules.GetMaterialChoices(player, 10, (min, max) => max - 1), Is.Empty);
        var arrow = new MaterialModel("arrow", MaterialEnum.Fire);
        player.Deck.Add(arrow);
        for (int roll = 0; roll < 5; roll++)
        {
            int firstRoll = roll;
            Assert.That(ModifierDropRules.GetMaterialChoices(player, 1, (min, max) => firstRoll)[0].id, Is.EqualTo(roll == 0 ? "a" : "b"));
        }
        var all = ModifierDropRules.GetMaterialChoices(player, 10, (min, max) => max - 1);
        Assert.That(all.Count, Is.EqualTo(2));
        Assert.That(all[0].id, Is.EqualTo("b"));
        Assert.That(all[1].id, Is.EqualTo("a"));
        arrow.AddModifier(new SturdyModifier());
        Assert.That(ModifierDropRules.GetMaterialChoices(player, 10, (min, max) => min).Count, Is.EqualTo(1));
    }

    [Test]
    public void ProductionManagerEntryPointsDelegateToSameSession()
    {
        RunManager run = Create();
        LevelData level = Level(1, LevelType.Event);
        run.SelectCurrentNodeLevel(level);
        Assert.That(run.Session.State, Is.EqualTo(RunFlowState.BeforeNode));
        run.BeginLevel(level);
        Assert.That(run.State, Is.EqualTo(RunFlowState.Event));
        Assert.That(run.Session.CurrentLevel, Is.SameAs(level));
        run.ClearCurrentLevel();
        Assert.That(run.Session.CurrentLevel, Is.Null);
        Assert.That(run.State, Is.EqualTo(RunFlowState.MapSelection));
    }

    [Test]
    public void BattleRewardAutomaticShopConsumesOneOriginalNode()
    {
        RunManager run = Create();
        run.AttachMapNodes(new System.Collections.Generic.List<RunMapNodeModel> { new RunMapNodeModel(), new RunMapNodeModel() });
        LevelData battle = Level(1, LevelType.Battle);
        LevelData shop = Level(2, LevelType.Shop);
        run.Session.SelectNode(battle);
        run.BeginLevel(battle);
        Assert.That(run.Session.CompleteNode().Accepted, Is.False);
        Assert.That(run.Session.BattleWon().To, Is.EqualTo(RunFlowState.Reward));
        Assert.That(run.Session.CompleteReward(shop).To, Is.EqualTo(RunFlowState.Shop));
        Assert.That(run.Session.NodeLevel, Is.SameAs(battle));
        Assert.That(run.CurrentMapNodeIndex, Is.Zero);
        run.BeginLevel(shop); // Real StartShopLevel remains an idempotent begin adapter.
        RunTransition result = run.Session.CompleteNode();
        Assert.That(result.CompletedLevel, Is.SameAs(battle));
        Assert.That(result.AdvancedNode, Is.True);
        Assert.That(run.CurrentMapNodeIndex, Is.EqualTo(1));
        Assert.That(run.Session.CompleteNode().Rejection, Is.EqualTo(RunTransitionRejection.NoActiveNode));
        Assert.That(run.CurrentMapNodeIndex, Is.EqualTo(1));
    }

    [Test]
    public void MissingAutomaticShopReturnsToMapInsteadOfStrandingReward()
    {
        RunManager run = Create();
        run.BeginLevel(Level(1, LevelType.Battle));
        run.Session.BattleWon();
        Assert.That(run.Session.CompleteReward(null).To, Is.EqualTo(RunFlowState.MapSelection));
        Assert.That(run.CurrentLevel, Is.Null);
    }

    [Test]
    public void FixedBossPathCompletesSequentialCellsThenVictory()
    {
        RunManager run = Create();
        var grid = new RunMapGridModel { width = 1, height = 2 };
        grid.cells.Add(new RunMapCellModel { x = 0, y = 0, isBoss = true, isAvailable = true });
        grid.cells.Add(new RunMapCellModel { x = 0, y = 1, isBoss = true, isEnd = true, isAvailable = true });
        run.AttachMapGrid(grid);
        LevelData boss = Level(2, LevelType.Elite);
        run.Session.SelectNode(boss, new RunNodePolicy { IsBoss = true });
        run.BeginLevel(boss);
        run.Session.BattleWon();
        Assert.That(run.Session.CompleteReward(null).To, Is.EqualTo(RunFlowState.MapSelection));
        Assert.That(grid.cells[0].isCompleted, Is.True);
        Assert.That(run.MoveMapPlayer(MaterialEnum.Fire), Is.SameAs(grid.cells[1]));
        run.Session.SelectNode(boss, new RunNodePolicy { IsBoss = true, CompleteRunOnBattleWin = true });
        run.BeginLevel(boss);
        Assert.That(run.Session.BattleWon().To, Is.EqualTo(RunFlowState.Victory));
        Assert.That(grid.cells[1].isCompleted, Is.True);
        Assert.That(run.Session.SelectNode(Level(9, LevelType.Shop)).Rejection, Is.EqualTo(RunTransitionRejection.RunEnded));
    }

    [TestCase(LevelType.Event)]
    [TestCase(LevelType.Rest)]
    [TestCase(LevelType.Reward)]
    [TestCase(LevelType.Shop)]
    [TestCase(LevelType.RemoveMaterial)]
    [TestCase(LevelType.AddMaterial)]
    public void NonBattleNodeCompletesWithoutUi(LevelType type)
    {
        RunManager run = Create();
        run.BeginLevel(Level(9, type));
        Assert.That(run.Session.CompleteNode().To, Is.EqualTo(RunFlowState.MapSelection));
        Assert.That(run.CurrentMapNodeIndex, Is.EqualTo(1));
    }

    [Test]
    public void SecondFloorDoesNotAdvancePlaceholderNodeAndDefeatIsTerminal()
    {
        RunManager run = Create();
        run.Session.SelectNode(Level(1, LevelType.Rest), new RunNodePolicy { AdvanceProgress = false });
        run.BeginLevel(run.CurrentLevel);
        Assert.That(run.Session.CompleteNode().AdvancedNode, Is.False);
        Assert.That(run.CurrentMapNodeIndex, Is.Zero);
        Assert.That(run.Session.EndRun(false).To, Is.EqualTo(RunFlowState.Defeat));
        Assert.That(run.Session.BeginLevel(Level(2, LevelType.Battle)).Accepted, Is.False);
    }

    [Test]
    public void EnterConsumesNormalGridContentOnceAndNotBossContent()
    {
        RunManager run = Create();
        LevelData level = Level(1, LevelType.Event);
        var grid = new RunMapGridModel { width = 1, height = 1 };
        var cell = new RunMapCellModel { level = level, isHidden = true };
        grid.cells.Add(cell);
        run.AttachMapGrid(grid);
        run.SelectCurrentNodeLevel(level);
        Assert.That(cell.level, Is.Null);
        Assert.That(cell.isHidden, Is.False);
        Assert.That(run.CurrentLevel, Is.SameAs(level));
    }
}
