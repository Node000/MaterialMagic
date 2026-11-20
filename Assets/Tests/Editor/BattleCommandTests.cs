using System;
using NUnit.Framework;

/// <summary>Tests the production command path, never a duplicate card/refresh implementation.</summary>
public sealed class BattleCommandTests
{
    private BattleManager battle;
    [TearDown] public void TearDown() { BattleManager.ClearInstance(battle); }

    private BattleCommandSession Create(out PlayerStatus player)
    {
        player = new PlayerStatus(50, 0, PlayerStateServices.Simulation, new RunDifficultySaveData());
        player.SetRunRandomState(71234, 0);
        battle = BattleManager.Create(player);
        battle.BeginPlayerTurn();
        return new BattleCommandSession(player, battle);
    }
    private static MaterialModel Hand(PlayerState player, string id, MaterialEnum material = MaterialEnum.Earth)
    {
        var card = new MaterialModel(id, material);
        player.Hand.Add(card);
        return card;
    }

    [Test] public void QueryAndLegalCommands_DoNotConsumeRngOrMutatePiles()
    {
        BattleCommandSession commands = Create(out PlayerStatus player);
        var card = Hand(player, "first");
        int step = player.RunRandomStep;
        UnityEngine.Random.State unityState = UnityEngine.Random.state;
        for (int i = 0; i < 8; i++)
        {
            Assert.That(commands.Query(BattleCommand.Play(card, 0)), Is.EqualTo(BattleCommandRejection.None));
            Assert.That(commands.GetLegalCommands().Count, Is.GreaterThan(0));
        }
        Assert.That(player.RunRandomStep, Is.EqualTo(step));
        Assert.That(UnityEngine.Random.state, Is.EqualTo(unityState));
        Assert.That(player.Hand, Is.EqualTo(new[] { card }));
        Assert.That(player.PlayZone, Is.Empty);
        Assert.That(player.PlayedCardCountThisTurn, Is.Zero);
    }

    [Test] public void PlayWithdrawAndReorder_UseExistingPlayLimitAndRefundRules()
    {
        BattleCommandSession commands = Create(out PlayerStatus player);
        player.MaxPlayCount = 1;
        var first = Hand(player, "first");
        var second = Hand(player, "second");
        Assert.That(commands.Execute(BattleCommand.Play(first, 50)).Accepted, Is.True, "Existing index clamping is retained.");
        Assert.That(commands.Execute(BattleCommand.Play(second, 0)).Rejection, Is.EqualTo(BattleCommandRejection.PlayLimitReached));
        Assert.That(commands.Execute(BattleCommand.Withdraw(first, 0)).Accepted, Is.True);
        Assert.That(player.PlayedCardCountThisTurn, Is.Zero);
        Assert.That(commands.Execute(BattleCommand.ReorderHand(first, 1)).Accepted, Is.True);
        Assert.That(player.Hand, Is.EqualTo(new[] { second, first }));
        Assert.That(commands.Execute(BattleCommand.ReorderHand(first, 1)).Rejection, Is.EqualTo(BattleCommandRejection.NoChange));
        Assert.That(commands.Execute(BattleCommand.Play(second, 0)).Accepted, Is.True);
    }

    [Test] public void DisabledAndForeignCards_AreRejectedWithoutJoinHooks()
    {
        BattleCommandSession commands = Create(out PlayerStatus player);
        var card = Hand(player, "disabled");
        card.AddModifier(new DisabledArrowModifier());
        Assert.That(commands.Execute(BattleCommand.Play(card, 0, true)).Rejection, Is.EqualTo(BattleCommandRejection.DisabledCard),
            "Nonbattle selection override must not bypass battle legality.");
        var foreign = new MaterialModel("foreign", MaterialEnum.Fire);
        Assert.That(commands.Execute(BattleCommand.Play(foreign, 0)).Rejection, Is.EqualTo(BattleCommandRejection.CardNotInHand));
        Assert.That(player.PlayedCardCountThisTurn, Is.Zero);
    }

    [Test] public void Refresh_PaysBaseThenPermanentThenExtra_AndRetainsResultSnapshots()
    {
        BattleCommandSession commands = Create(out PlayerStatus player);
        player.AddPermanentRefreshChance(1);
        player.AddExtraRefreshChances(1);
        var card = Hand(player, "refresh");
        commands.Execute(BattleCommand.Play(card, 0));
        BattleCommandResult first = commands.Execute(BattleCommand.Refresh());
        Assert.That(first.Accepted, Is.True);
        Assert.That(first.RefreshUsedThisTurn, Is.True);
        Assert.That(first.ReturnedCount, Is.EqualTo(1));
        Assert.That(first.RemainingRefreshChances, Is.EqualTo(2));
        Assert.That(player.RemainingPermanentRefreshChancesThisTurn, Is.EqualTo(1));
        commands.Execute(BattleCommand.Play(player.Hand[0], 0));
        Assert.That(commands.Execute(BattleCommand.Refresh()).Accepted, Is.True);
        Assert.That(player.RemainingPermanentRefreshChancesThisTurn, Is.Zero);
        Assert.That(player.ExtraRefreshChancesThisTurn, Is.EqualTo(1));
        commands.Execute(BattleCommand.Play(player.Hand[0], 0));
        Assert.That(commands.Execute(BattleCommand.Refresh()).Accepted, Is.True);
        Assert.That(commands.RemainingRefreshChances, Is.Zero);
        Assert.That(first.RemainingRefreshChances, Is.EqualTo(2), "Past result is immutable.");
        commands.Execute(BattleCommand.Play(player.Hand[0], 0));
        int rng = player.RunRandomStep;
        Assert.That(commands.Execute(BattleCommand.Refresh()).Rejection, Is.EqualTo(BattleCommandRejection.NoRefreshChances));
        Assert.That(player.RunRandomStep, Is.EqualTo(rng));
    }

    [Test] public void EndTurnLocksDecisionsUntilHostStartsNextTurn_AndRejectsWrongPhase()
    {
        BattleCommandSession commands = Create(out PlayerStatus player);
        var card = Hand(player, "first");
        Assert.That(commands.Execute(BattleCommand.EndTurn()).Accepted, Is.True);
        Assert.That(battle.CurrentPhase, Is.EqualTo(BattlePhase.PlayerTurn), "Command does not duplicate host resolution.");
        Assert.That(commands.Execute(BattleCommand.Play(card, 0)).Rejection, Is.EqualTo(BattleCommandRejection.TurnResolving));
        commands.BeginPlayerTurn();
        Assert.That(commands.Execute(BattleCommand.Play(card, 0)).Accepted, Is.True);
        battle.BeginEnemyTurn();
        Assert.That(commands.Query(BattleCommand.Withdraw(card, 0)), Is.EqualTo(BattleCommandRejection.WrongPhase));
    }

    [Test] public void FocusRequiresLivingMember_AndTogglesWithoutRandomSelection()
    {
        BattleCommandSession commands = Create(out PlayerStatus player);
        var enemy = new EnemyModel(new EnemyData { numericId = 99001, maxHealth = 50 });
        var foreign = new EnemyModel(new EnemyData { numericId = 99002, maxHealth = 50 });
        battle.AddEnemy(enemy);
        Assert.That(commands.Query(BattleCommand.ToggleFocus(foreign)), Is.EqualTo(BattleCommandRejection.InvalidTarget));
        Assert.That(commands.Execute(BattleCommand.ToggleFocus(enemy)).FocusTarget, Is.SameAs(enemy));
        Assert.That(commands.Execute(BattleCommand.ToggleFocus(enemy)).FocusTarget, Is.Null);
        Assert.That(player.RunRandomStep, Is.Zero);
    }

    [Test] public void NonBattleCardSelection_PreservesDisabledOverrideAndIgnoresBattlePlayLimit()
    {
        var player = new PlayerState(50, 0, PlayerStateServices.Simulation, new RunDifficultySaveData()) { MaxPlayCount = 1 };
        var commands = new BattleCommandSession(player);
        var first = Hand(player, "one");
        var second = Hand(player, "two");
        second.AddModifier(new DisabledArrowModifier());
        Assert.That(commands.Execute(BattleCommand.Play(first, 0)).Accepted, Is.True);
        Assert.That(commands.Execute(BattleCommand.Play(second, 1, true)).Accepted, Is.True);
        Assert.That(player.PlayedCardCountThisTurn, Is.Zero);
    }
}
