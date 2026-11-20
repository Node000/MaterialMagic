using System.Collections.Generic;
using NUnit.Framework;

public sealed class TutorialPolicyTests
{
    [Test] public void NoSceneTutorial_ContinuesOldReadingSequenceThenPermitsPlay()
    {
        var policy = new TutorialPolicy();
        policy.Initialize(true);
        policy.BeginBattle();
        var player = new PlayerStatus(50, 0, PlayerStateServices.Simulation, new RunDifficultySaveData(),
            content: new RuleContentSnapshot().Freeze(), profilePolicy: RuleProfilePolicy.AllUnlocked);
        player.SetRunRandomState(1234, 0);
        Assert.That(policy.TryApplyFixedTurnHand(player), Is.True);
        Assert.That(player.Hand.Count, Is.EqualTo(2));
        Assert.That(player.Hand[0].material, Is.EqualTo(MaterialEnum.Fire));
        Assert.That(policy.CanMoveCardToPlay(player.Hand[0], player.PlayZone), Is.False);
        var expected = new[] { TutorialStep.BattleEnemyInfo, TutorialStep.BattleArrowBase,
            TutorialStep.BattleMagicBook, TutorialStep.BattlePlayLimit, TutorialStep.BattlePlay };
        foreach (TutorialStep step in expected)
        {
            Assert.That(policy.Continue(), Is.True);
            Assert.That(policy.State.CurrentStep, Is.EqualTo(step));
        }
        Assert.That(policy.Continue(), Is.False, "An action page is not skipped by Continue.");
        Assert.That(policy.CanMoveCardToPlay(player.Hand[0], player.PlayZone), Is.True);
        Assert.That(policy.AllowsEndTurn, Is.True);
        Assert.That(policy.AllowsRefresh, Is.False);
        Assert.That(player.RunRandomStep, Is.Zero);
    }

    [Test] public void SharedCommands_SecondTurnRefreshUsesScriptedSequenceAndUnlocksImmediately()
    {
        var policy = new TutorialPolicy();
        policy.Initialize(true);
        policy.BeginBattle();
        var player = new PlayerState(50, 0, PlayerStateServices.Simulation, new RunDifficultySaveData(),
            content: new RuleContentSnapshot().Freeze(), profilePolicy: RuleProfilePolicy.AllUnlocked);
        policy.TryApplyFixedTurnHand(player);
        for (int i = 0; i < 5; i++) Assert.That(policy.Continue(), Is.True);
        Assert.That(policy.State.WaitingForContinue, Is.False);
        policy.OnBattleEndTurnStarted();
        Assert.That(policy.TryApplyFixedTurnHand(player), Is.True);
        Assert.That(policy.State.CurrentStep, Is.EqualTo(TutorialStep.BattleRefresh));
        Assert.That(player.Hand.Count, Is.EqualTo(3));
        var commands = new BattleCommandSession(player, tutorial: policy);
        Assert.That(commands.Query(BattleCommand.EndTurn()), Is.EqualTo(BattleCommandRejection.TutorialBlocked));
        for (int i = 0; i < 3; i++) Assert.That(commands.Execute(BattleCommand.Play(player.Hand[0], player.PlayZone.Count)).Accepted, Is.True);
        Assert.That(player.Hand, Is.Empty);
        BattleCommandResult result = commands.Execute(BattleCommand.Refresh());
        Assert.That(result.Accepted, Is.True);
        Assert.That(player.Hand.Count, Is.EqualTo(3));
        Assert.That(player.Hand[0].material, Is.EqualTo(MaterialEnum.Fire));
        Assert.That(player.Hand[1].material, Is.EqualTo(MaterialEnum.Fire));
        Assert.That(player.Hand[2].material, Is.EqualTo(MaterialEnum.Water));
        Assert.That(policy.State.CurrentStep, Is.EqualTo(TutorialStep.None));
        Assert.That(policy.AllowsEndTurn, Is.True);
        Assert.That(policy.ShouldKillTutorialEnemyAfterPlayerTurn, Is.True);
    }

    [Test] public void ParagraphsGateActions_WithoutOwningNextStepRules()
    {
        var policy = new TutorialPolicy();
        policy.Initialize(true);
        policy.BeginBattle();
        policy.Show(TutorialStep.BattleRefresh, false);
        policy.ConfigureParagraphCount(3);
        Assert.That(policy.AllowsRefresh, Is.False);
        policy.Continue();
        Assert.That(policy.State.ParagraphIndex, Is.EqualTo(1));
        policy.Continue();
        Assert.That(policy.State.CurrentStep, Is.EqualTo(TutorialStep.BattleRefresh));
        Assert.That(policy.AllowsRefresh, Is.True);
        Assert.That(policy.Continue(), Is.False);
    }

    [Test] public void ForcedRefreshCyclesAndNonTutorialDoesNotOverride()
    {
        var policy = new TutorialPolicy();
        var result = new List<MaterialEnum> { MaterialEnum.Earth };
        Assert.That(policy.TryGetForcedRefreshMaterialsForPlayZone(4, result), Is.False);
        Assert.That(result, Is.Empty);
        policy.Initialize(true);
        policy.BeginBattle();
        policy.Show(TutorialStep.BattleRefresh, false);
        Assert.That(policy.TryGetForcedRefreshMaterialsForPlayZone(4, result), Is.True);
        Assert.That(result, Is.EqualTo(new[] { MaterialEnum.Fire, MaterialEnum.Fire, MaterialEnum.Water, MaterialEnum.Fire }));
    }

    [Test] public void MapShopRewardAndEventCanContinueWithoutMouseOrGameObjects()
    {
        var policy = new TutorialPolicy();
        policy.Initialize(true);
        policy.OnLevelSelectShown(0);
        Assert.That(policy.State.IsMapTutorialBlockingInput, Is.True);
        for (int i = 0; i < 3; i++) Assert.That(policy.Continue(), Is.True);
        Assert.That(policy.State.CurrentStep, Is.EqualTo(TutorialStep.None));
        policy.OnLevelSelectShown(0);
        Assert.That(policy.State.CurrentStep, Is.EqualTo(TutorialStep.None));
        policy.OnShopPanelShown();
        Assert.That(policy.Continue(), Is.True);
        policy.OnShopPanelShown();
        Assert.That(policy.State.CurrentStep, Is.EqualTo(TutorialStep.None));
        policy.OnEventOptionsShown();
        policy.OnEventOptionResolved();
        Assert.That(policy.State.EventTutorialShown, Is.True);
        policy.OnRewardPanelShown();
        Assert.That(policy.State.CurrentStep, Is.EqualTo(TutorialStep.RewardClaim));
        policy.Continue();
        policy.Complete();
        Assert.That(policy.State.IsCompleted, Is.True);
        Assert.That(policy.State.MainTutorialRunning, Is.False);
    }
}
