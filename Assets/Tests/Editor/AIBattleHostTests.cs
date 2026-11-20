using System;
using System.Collections.Generic;
using NUnit.Framework;

public class AIBattleHostTests
{
    private static AIBattleHost Create(int seed)
    {
        var player = new PlayerStatus(50, 0, PlayerStateServices.Simulation, new RunDifficultySaveData(),
            content: new RuleContentSnapshot().Freeze(), profilePolicy: RuleProfilePolicy.AllUnlocked);
        player.SetRunRandomState(seed, 0);
        for (int i = 0; i < 5; i++)
        {
            // GUID identity is intentionally different on replay; the host assigns stable session IDs.
            var card = new MaterialModel(Guid.NewGuid().ToString("N"), i % 2 == 0 ? MaterialEnum.Fire : MaterialEnum.Water);
            player.Deck.Add(card);
            player.DrawPile.Add(card);
        }
        player.DrawCards(2);
        return new AIBattleHost(new BattleCommandSession(player), "test-content-1");
    }

    private static AICommandData Find(AIBattleHost host, BattleCommandKind kind)
    {
        IReadOnlyList<AICommandData> legal = host.GetLegalCommands();
        for (int i = 0; i < legal.Count; i++) if (legal[i].kind == kind) return legal[i];
        Assert.Fail("Expected legal command " + kind);
        return null;
    }

    [Test]
    public void ObservationAndLegalQueriesAreCopiedAndDoNotAdvanceRunRng()
    {
        AIBattleHost host = Create(912);
        int step = ((PlayerStatus)host.Commands.Player).RunRandomStep;
        AIBattleObservation before = host.Observe();
        AIBattleDiagnosticSnapshot diagnostic = host.CaptureDiagnostics();
        host.GetLegalCommands(); host.Observe(); host.CaptureDiagnostics();
        Assert.That(((PlayerStatus)host.Commands.Player).RunRandomStep, Is.EqualTo(step));
        Assert.That(before.DrawPileCount, Is.EqualTo(3));
        Assert.That(typeof(AIBattleObservation).GetProperty("HiddenDrawPile"), Is.Null);
        Assert.That(typeof(AIBattleObservation).GetProperty("RandomStep"), Is.Null);
        Assert.That(diagnostic.HiddenDrawPile.Count, Is.EqualTo(3));
        host.Execute(Find(host, BattleCommandKind.Play));
        Assert.That(before.Hand.Count, Is.EqualTo(2), "Observation is not a live list.");
        Assert.That(host.Observe().Hand.Count, Is.EqualTo(1));
    }

    [Test]
    public void FixedSeedReplayUsesLegalCommandsAndNotRandomGuidIdentity()
    {
        AIBattleHost host = Create(815);
        Assert.That(host.Execute(Find(host, BattleCommandKind.Play)).CommandResult.Accepted, Is.True);
        Assert.That(host.Execute(Find(host, BattleCommandKind.Refresh)).CommandResult.Accepted, Is.True);
        Assert.That(host.Execute(Find(host, BattleCommandKind.Play)).CommandResult.Accepted, Is.True);
        Assert.That(host.Execute(Find(host, BattleCommandKind.Withdraw)).CommandResult.Accepted, Is.True);
        Assert.That(AIBattleReplay.Replay(host.Log, Create).Matched, Is.True);
        host.Log.entries[0].after += "corrupted";
        AIReplayResult mismatch = AIBattleReplay.Replay(host.Log, Create);
        Assert.That(mismatch.Matched, Is.False);
        Assert.That(mismatch.DivergedAt, Is.Zero);
    }

    [Test]
    public void StaleIdentityAndPrivilegedFlagsAreRejectedAndLogged()
    {
        AIBattleHost host = Create(931);
        AIBattleStepResult invalid = host.Execute(new AICommandData { kind = BattleCommandKind.Play, cardId = "card:missing" });
        Assert.That(invalid.CommandResult.Rejection, Is.EqualTo(BattleCommandRejection.MissingCard));
        AICommandData play = Find(host, BattleCommandKind.Play);
        play.allowDisabled = true;
        Assert.That(host.Execute(play).CommandResult.Rejection, Is.EqualTo(BattleCommandRejection.InvalidCommand));
        Assert.That(host.Commands.Player.PlayZone.Count, Is.Zero);
        Assert.That(host.Log.entries.Count, Is.EqualTo(2));
        Assert.That(AIBattleReplay.Replay(host.Log, Create).Matched, Is.True);
    }

    [Test]
    public void BatchRetainsBoundedFailureSamplesAndReplayLog()
    {
        IReadOnlyList<AIBatchResult> results = AIBattleBatch.Run(new[] { 11, 12 }, Create, seed => new FirstLegalPolicy(), 3);
        Assert.That(results.Count, Is.EqualTo(2));
        foreach (AIBatchResult result in results)
        {
            Assert.That(result.Finished, Is.False);
            Assert.That(result.Failure, Is.EqualTo("Command budget exhausted."));
            Assert.That(result.Log.entries.Count, Is.EqualTo(3));
            Assert.That(AIBattleReplay.Replay(result.Log, Create).Matched, Is.True);
        }
    }

    private sealed class FirstLegalPolicy : IAICommandPolicy
    {
        public int Choose(AIBattleObservation observation, IReadOnlyList<AICommandData> commands) => 0;
    }
}
