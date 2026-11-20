using System;
using System.Collections.Generic;
using NUnit.Framework;

public class GameSessionTests
{
    private static GameSession Create(int seed) => new GameSession(CreateOptions(seed));

    private static GameSessionOptions CreateOptions(int seed)
    {
        var levels = new Dictionary<int, LevelData>
        {
            { 1, new LevelData { numericId = 1, id = "battle", levelType = LevelType.Battle, enemyIds = new[] { 901001 } } },
            { 2, new LevelData { numericId = 2, id = "event", levelType = LevelType.Event } },
            { 3, new LevelData { numericId = 3, id = "rest", levelType = LevelType.Rest } },
            { 4, new LevelData { numericId = 4, id = "grid", levelType = LevelType.Reward } },
            { 5, new LevelData { numericId = 5, id = "shop", levelType = LevelType.Shop } },
            { 6, new LevelData { numericId = 6, id = "boss", levelType = LevelType.Elite, enemyIds = new[] { 901001 } } },
            { 401, new LevelData { numericId = 401, id = "automatic_shop", levelType = LevelType.Shop } }
        };
        var enemy = new EnemyData { numericId = 901001, id = "test_enemy", maxHealth = 1, baseAttack = 0 };
        var bonus = new BonusLevelData { radius = 1, drawCount = 2, rewards = new[] { new BonusRewardData { rewardType = BonusRewardType.Gold, amount = 1 } } };
        var economy = new EconomyConfigData { battleGoldMin = 2, shopMaterialPrice = 1, shopRefreshPrice = 1 };
        var content = new GameSessionContent
        {
            Version = "whole-run-fixture-1", Economy = economy, AutomaticShopLevel = levels[401],
            FindLevel = id => levels.TryGetValue(id, out LevelData level) ? level : null,
            FindEnemy = id => id == enemy.numericId ? enemy : null,
            FindEvent = level => new EventData
            {
                id = "fixture_event", startNodeId = "start", drawCount = 2,
                nodes = new[] { new EventNodeData { id = "start", options = new[] { new EventOptionData
                {
                    id = "leave", isExitOption = true, effects = new[] { new EventEffectData { rewardType = EventRewardType.GainGold, amount = 3 } }
                } } } }
            },
            FindBonus = level => bonus,
            NodePolicy = (run, level) => new RunNodePolicy { IsBoss = level.numericId == 6, CompleteRunOnBattleWin = level.numericId == 6 },
            ModifyBonusGold = value => value,
            RewardServices = player => new RewardGenerationServices
            {
                NextRandomInt = player.NextRunRandomInt, AllMagic = () => Array.Empty<MagicData>(),
                FindMagic = id => null, FindRewardPool = id => null, IsMagicUnlocked = data => true,
                EliteLegendaryGuarantee = () => false, MaterialModifierChoices = count => Array.Empty<MaterialModifierData>(),
                ModifyGold = value => value, ModifyGoldChoiceCount = value => value
            },
            EventServices = player => new EventRuleServices(player)
            {
                NextRandomInt = player.NextRunRandomInt, MagicChoices = count => Array.Empty<MagicData>(),
                MagicModifierChoices = count => Array.Empty<MagicModifierData>(), MaterialModifierChoices = count => Array.Empty<MaterialModifierData>(),
                FindMagic = id => null, FindMagicModifier = id => null, FindMaterialModifier = id => null
            },
            ShopServices = player => new ShopGenerationServices
            {
                NextRandomInt = player.NextRunRandomInt, AllMagic = () => Array.Empty<MagicData>(),
                AllProductPools = () => Array.Empty<ShopProductPoolData>(), IsMagicUnlocked = data => true, IsModifierUnlocked = data => true,
                FindMagic = id => null, FindRewardPool = id => null, FindProductPool = id => null, FindModifier = id => null,
                ModifierPriceDelta = id => 0, ModifyPrice = value => value, QuoteMagic = data => 1
            },
            ShopLayers = level => new[] { new ShopLayer(new Dictionary<ShopSlotEnum, float> { { ShopSlotEnum.Arrow, 1 } }, 2) }
        };
        return new GameSessionOptions
        {
            Seed = seed, Content = content, StartingGold = 20, Services = PlayerStateServices.Simulation,
            Difficulty = new RunDifficultySaveData(),
            RuleContent = new RuleContentSnapshot().Freeze(), Profile = RuleProfilePolicy.AllUnlocked,
            InitializePlayer = player => { for (int i = 0; i < 8; i++) player.AddDeckMaterial(MaterialEnum.Fire); },
            ConfigureRun = run =>
            {
                var nodes = new List<RunMapNodeModel>();
                for (int i = 1; i <= 6; i++) nodes.Add(new RunMapNodeModel { leftLevel = levels[i], rightLevel = levels[i], fixedSingleChoice = true });
                run.AttachMapNodes(nodes);
            }
        };
    }

    private static void Execute(GameSession session, GameCommand command)
    {
        GameCommandLogEntry result = session.Execute(command);
        Assert.That(result.accepted, Is.True, result.rejection);
    }
    private static GameCommand Card(GameSession session, BattleCommandKind kind)
    {
        foreach (GameCommand command in session.GetLegalCommands())
            if (command.battle != null && command.battle.kind == kind) return command;
        Assert.Fail("No legal card command " + kind + " in " + session.Run.State);
        return null;
    }
    private static void FinishBattle(GameSession session)
    {
        Execute(session, Card(session, BattleCommandKind.Play));
        Execute(session, Card(session, BattleCommandKind.EndTurn));
    }

    [Test]
    public void FixedSeedFullRunUsesAllProductionNodeSessionsAndReplays()
    {
        GameSession session = Create(771);
        Execute(session, new GameCommand { kind = GameCommandKind.EnterNode, levelId = 1 });
        FinishBattle(session);
        Assert.That(session.Run.State, Is.EqualTo(RunFlowState.Reward));
        Assert.That(session.Player.Hand, Is.Empty);
        Assert.That(session.Player.PlayZone, Is.Empty);
        Assert.That(session.Player.DiscardPile, Is.Empty);
        Assert.That(session.Player.DrawPile.Count, Is.EqualTo(session.Player.Deck.Count), "Battle exit uses the production node deck reset before reward.");
        PendingChoice reward = session.Choice.PendingChoice;
        Execute(session, new GameCommand { kind = GameCommandKind.SubmitChoice, choiceId = reward.Id, candidateIndexes = new[] { 0 } });
        Assert.That(session.Run.State, Is.EqualTo(RunFlowState.Shop));
        Assert.That(session.Run.CurrentMapNodeIndex, Is.Zero);
        Execute(session, new GameCommand { kind = GameCommandKind.LeaveShop });
        Assert.That(session.Run.CurrentMapNodeIndex, Is.EqualTo(1));

        Execute(session, new GameCommand { kind = GameCommandKind.EnterNode, levelId = 2 });
        Execute(session, Card(session, BattleCommandKind.EndTurn)); // Exit option uses shared matching/fallback rules.
        Assert.That(session.Run.CurrentMapNodeIndex, Is.EqualTo(2));
        Execute(session, new GameCommand { kind = GameCommandKind.EnterNode, levelId = 3 });
        Execute(session, Card(session, BattleCommandKind.EndTurn));
        Assert.That(session.Event.CurrentNode.id, Is.EqualTo("rest_result"));
        Execute(session, new GameCommand { kind = GameCommandKind.ContinueEvent });
        Assert.That(session.Run.CurrentMapNodeIndex, Is.EqualTo(3));

        Execute(session, new GameCommand { kind = GameCommandKind.EnterNode, levelId = 4 });
        Execute(session, Card(session, BattleCommandKind.Play));
        Execute(session, Card(session, BattleCommandKind.EndTurn));
        Assert.That(session.Run.CurrentMapNodeIndex, Is.EqualTo(4));
        Execute(session, new GameCommand { kind = GameCommandKind.EnterNode, levelId = 5 });
        Execute(session, new GameCommand { kind = GameCommandKind.ShopPurchase, offerIndex = 0 });
        Execute(session, new GameCommand { kind = GameCommandKind.ShopUndo });
        Execute(session, new GameCommand { kind = GameCommandKind.ShopRefresh });
        Execute(session, new GameCommand { kind = GameCommandKind.LeaveShop });
        Execute(session, new GameCommand { kind = GameCommandKind.EnterNode, levelId = 6 });
        FinishBattle(session);
        Assert.That(session.Run.State, Is.EqualTo(RunFlowState.Victory));
        Assert.That(GameSessionReplay.Replay(session.Log, Create).Matched, Is.True);
    }

    [Test]
    public void CheckpointReplaysToPendingRewardThenContinuesWithoutHostServices()
    {
        GameSession original = Create(774);
        Execute(original, new GameCommand { kind = GameCommandKind.EnterNode, levelId = 1 });
        FinishBattle(original);
        Assert.That(original.Choice.PendingChoice, Is.Not.Null);
        GameSessionCheckpoint checkpoint = GameSessionCheckpoint.FromJson(GameSessionCheckpoint.Capture(original).ToJson());
        GameSession restored = checkpoint.Restore(CreateOptions);
        Assert.That(restored.DiagnosticFingerprint(), Is.EqualTo(original.DiagnosticFingerprint()));
        Assert.That(restored.Choice.PendingChoice.Kind, Is.EqualTo(ChoiceKind.BattleReward));
        Assert.That(restored.Player.Services, Is.SameAs(PlayerStateServices.Simulation));
        Execute(restored, new GameCommand { kind = GameCommandKind.SubmitChoice, choiceId = restored.Choice.PendingChoice.Id, candidateIndexes = new[] { 0 } });
        Assert.That(restored.Run.State, Is.EqualTo(RunFlowState.Shop));
        Assert.That(original.Run.State, Is.EqualTo(RunFlowState.Reward));
    }

    [Test]
    public void CheckpointRestoresConstructorInputsAndBuildsContentFromArchivedCatalog()
    {
        GameSessionOptions initial = CreateOptions(775);
        initial.RuleContent = new RuleContentSnapshot().AddNumeric(new Dictionary<int, EconomyConfigData>
        {
            { 1, new EconomyConfigData { numericId = 1, battleGoldMin = 7 } }
        }).Freeze();
        var original = new GameSession(initial);
        GameSessionCheckpoint checkpoint = GameSessionCheckpoint.FromJson(GameSessionCheckpoint.Capture(original).ToJson());
        EconomyConfigData restoredEconomy = null;
        GameSession restored = checkpoint.Restore(seed =>
        {
            GameSessionOptions options = CreateOptions(seed);
            options.MaxHealth = 9;
            options.StartingGold = 999;
            options.RuleContent = new RuleContentSnapshot().AddNumeric(new Dictionary<int, EconomyConfigData>
            {
                { 1, new EconomyConfigData { numericId = 1, battleGoldMin = 999 } }
            }).Freeze();
            GameSessionContent adapter = options.Content;
            options.CreateContent = player =>
            {
                restoredEconomy = player.Rules.Content.Numeric<EconomyConfigData>()[1];
                return adapter;
            };
            return options;
        });
        Assert.That(restored.Player.MaxHealth, Is.EqualTo(original.Player.MaxHealth));
        Assert.That(restored.Player.Gold, Is.EqualTo(original.Player.Gold));
        Assert.That(restoredEconomy.battleGoldMin, Is.EqualTo(7));
        Assert.That(restored.DiagnosticFingerprint(), Is.EqualTo(original.DiagnosticFingerprint()));
    }

    private static GameSessionOptions TutorialOptions(int seed)
    {
        GameSessionOptions options = CreateOptions(seed);
        options.Tutorial = new TutorialPolicy();
        options.Tutorial.Initialize(true);
        options.Content.IsTutorialBattle = level => true;
        options.Content.FindEnemy = id => new EnemyData { numericId = id, id = "tutorial_target", maxHealth = 100, baseAttack = 0 };
        return options;
    }

    [Test]
    public void TutorialWholeRunCommandsReachRefreshAndCheckpointPendingReward()
    {
        var session = new GameSession(TutorialOptions(776));
        Assert.That(session.Tutorial.State.CurrentStep, Is.EqualTo(TutorialStep.MapPanel));
        Assert.That(session.Execute(new GameCommand { kind = GameCommandKind.EnterNode, nodeChoiceIndex = 0 }).rejection, Is.EqualTo("TutorialBlocked"));
        for (int i = 0; i < 3; i++) Execute(session, new GameCommand { kind = GameCommandKind.TutorialContinue });
        Execute(session, new GameCommand { kind = GameCommandKind.EnterNode, nodeChoiceIndex = 0 });
        Assert.That(session.Tutorial.State.CurrentStep, Is.EqualTo(TutorialStep.BattleInfo));
        for (int i = 0; i < 5; i++)
        {
            IReadOnlyList<GameCommand> legal = session.GetLegalCommands();
            Assert.That(legal.Count, Is.EqualTo(1));
            Assert.That(legal[0].kind, Is.EqualTo(GameCommandKind.TutorialContinue));
            Execute(session, legal[0]);
        }
        Assert.That(session.Tutorial.State.CurrentStep, Is.EqualTo(TutorialStep.BattlePlay));
        Execute(session, Card(session, BattleCommandKind.Play));
        Execute(session, Card(session, BattleCommandKind.EndTurn));
        Assert.That(session.Tutorial.State.CurrentStep, Is.EqualTo(TutorialStep.BattleRefresh));
        Assert.That(session.Player.Hand.Count, Is.EqualTo(3));
        for (int i = 0; i < 3; i++) Execute(session, Card(session, BattleCommandKind.Play));
        Assert.That(session.Battle.Commands.Query(BattleCommand.EndTurn()), Is.EqualTo(BattleCommandRejection.TutorialBlocked));
        Execute(session, Card(session, BattleCommandKind.Refresh));
        Assert.That(session.Tutorial.State.BattleInputUnlocked, Is.True);
        Execute(session, Card(session, BattleCommandKind.EndTurn));
        Assert.That(session.Run.State, Is.EqualTo(RunFlowState.Reward));
        GameSessionCheckpoint checkpoint = GameSessionCheckpoint.FromJson(GameSessionCheckpoint.Capture(session).ToJson());
        GameSession restored = checkpoint.Restore(TutorialOptions);
        Assert.That(restored.Choice.PendingChoice.Kind, Is.EqualTo(ChoiceKind.BattleReward));
        Assert.That(restored.DiagnosticFingerprint(), Is.EqualTo(session.DiagnosticFingerprint()));
        Assert.That(restored.Tutorial.State.CurrentStep, Is.EqualTo(TutorialStep.RewardClaim));
        Execute(restored, new GameCommand { kind = GameCommandKind.TutorialContinue });
        Execute(restored, new GameCommand { kind = GameCommandKind.SubmitChoice,
            choiceId = restored.Choice.PendingChoice.Id, candidateIndexes = new[] { 0 } });
        Assert.That(restored.Run.State, Is.EqualTo(RunFlowState.Shop));
    }

    [Test]
    public void WholeRunLegalObservationAndCommandsDoNotAdvanceRngOrRevealHiddenContent()
    {
        GameSession session = Create(772);
        var node = session.Run.MapNodes[0]; node.leftHidden = node.rightHidden = true;
        int step = session.Player.RunRandomStep;
        GameSessionObservation observation = session.Observe();
        Assert.That(observation.LeftLevelId, Is.Null);
        Assert.That(observation.RightLevelId, Is.Null);
        foreach (GameCommand command in session.GetLegalCommands())
            Assert.That(command.levelId, Is.Zero, "Hidden node legal actions use branch indexes, not content IDs.");
        Assert.That(session.Player.RunRandomStep, Is.EqualTo(step));
        Execute(session, new GameCommand { kind = GameCommandKind.EnterNode, nodeChoiceIndex = 0 });
        Assert.That(session.Run.State, Is.EqualTo(RunFlowState.Battle));
    }
}
