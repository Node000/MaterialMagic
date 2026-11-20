using System;
using System.Collections.Generic;

/// <summary>
/// Final outcome after this turn's committed rules and their death barriers.
/// </summary>
public enum BattleTurnOutcome
{
    NextPlayerTurn,
    Victory,
    Defeat
}

/// <summary>One enemy intent submission. Multi-hit attacks have one entry per hit, never value * times.</summary>
public sealed class BattleEnemyIntentResolution
{
    public EnemyModel Enemy { get; }
    public int IntentIndex { get; }
    public int HitIndex { get; }
    public BattleActionResult Result { get; }

    internal BattleEnemyIntentResolution(EnemyModel enemy, int intentIndex, int hitIndex, BattleActionResult result)
    {
        Enemy = enemy;
        IntentIndex = intentIndex;
        HitIndex = hitIndex;
        Result = result;
    }
}

public sealed class BattleTurnResult
{
    public BattleTurnOutcome Outcome { get; internal set; }
    public IReadOnlyList<BattleResolutionStep> Steps { get; internal set; }
    public IReadOnlyList<BattleEnemyIntentResolution> EnemyIntents { get; internal set; }
    public IReadOnlyList<MaterialModel> RemovedTemporaryCards { get; internal set; }
    public BattleActionResult PlayerTurnEnd { get; internal set; }
    public BattleActionResult NextPlayerTurnStart { get; internal set; }
    public BattleStateSnapshot InitialState { get; internal set; }
    public BattleStateSnapshot FinalState { get; internal set; }
}

/// <summary>
/// Synchronous production-rule driver: player resolution, end-turn, serial enemy actions, then the
/// next player turn or terminal battle. No scene, animation, frame, or time dependency.
/// Every rule call runs in the owning player's synchronous context. The returned trace is fully
/// committed before playback starts; model references in it are identity handles, not display values.
/// Confirming discards the old hand before reading arrows; turn end clears only the play zone.
/// Death barriers precede terminal decisions.
/// </summary>
public static class BattleTurnDriver
{
    public static BattleTurnResult ResolveTurn(
        BattleManager battle,
        IReadOnlyList<MagicModel> magics = null,
        IReadOnlyList<int> readingRanks = null,
        int? nextTurnDrawCount = null,
        Action<BattleResolutionStep> onStep = null,
        TutorialPolicy tutorial = null)
    {
        if (battle == null)
            throw new ArgumentNullException(nameof(battle));
        if (battle.PlayerState == null)
            throw new ArgumentException("A player is required.", nameof(battle));
        if (battle.CurrentPhase != BattlePhase.PlayerTurn)
            throw new InvalidOperationException("ResolveTurn requires an active player turn.");

        PlayerState player = battle.PlayerState;
        using (player.Rules.Enter())
        using (var feedback = RuleExecutionScope.CaptureFeedback())
        {
        var steps = new List<BattleResolutionStep>();
        var intents = new List<BattleEnemyIntentResolution>();
        var removed = new List<MaterialModel>();
        var result = new BattleTurnResult { InitialState = BattleStateSnapshot.Capture(player, battle) };
        Action<BattleResolutionStep> record = step =>
        {
            if (step.After == null) step.After = BattleStateSnapshot.Capture(player, battle);
            step.FeedbackCues = feedback.Drain();
            steps.Add(step);
            onStep?.Invoke(step);
        };

        battle.BeginPlayerResolveRules();
        record(new BattleResolutionStep { Kind = BattleResolutionStepKind.PlayerResolveStarted, Before = result.InitialState });
        bool hadPlayZoneCards = player.PlayZone.Count > 0;
        try
        {
            BattleStateSnapshot discardBefore = BattleStateSnapshot.Capture(player, battle);
            player.DiscardHandOnConfirm(removed);
            record(new BattleResolutionStep
            {
                Kind = BattleResolutionStepKind.HandDiscardedOnConfirm, Before = discardBefore,
                RemovedTemporaryCards = new List<MaterialModel>(removed).AsReadOnly()
            });
            BattleDeathResolution.Drain(player, battle, record);

            if (hadPlayZoneCards && player.CurrentHealth > 0 && !battle.AllEnemiesDead())
            {
                using (var session = new BattlePlayZoneResolutionSession(player, battle,
                    player.PlayZone, magics ?? player.MagicBook, readingRanks))
                {
                    session.RunToCompletion(record);
                }
                battle.ResetContinuousCastCount(); // UI resets after the play-zone presentation, before turn-end hooks.
            }

            BattleDeathResolution.Drain(player, battle, record);
            if (player.CurrentHealth <= 0)
            {
                FinishDefeat(player, battle);
                result.Outcome = BattleTurnOutcome.Defeat;
            }
            else if (battle.AllEnemiesDead())
            {
                battle.EndPlayerResolveRules();
                FinishVictory(player, battle, removed, record);
                result.Outcome = player.CurrentHealth > 0 ? BattleTurnOutcome.Victory : BattleTurnOutcome.Defeat;
            }
            else
            {
                BattleStateSnapshot turnEndBefore = BattleStateSnapshot.Capture(player, battle);
                int removedBefore = removed.Count;
                result.PlayerTurnEnd = battle.EndPlayerTurnRules(removed);
                record(new BattleResolutionStep
                {
                    Kind = BattleResolutionStepKind.PlayerTurnEnded, Before = turnEndBefore,
                    RemovedTemporaryCards = removed.GetRange(removedBefore, removed.Count - removedBefore).AsReadOnly()
                });
                BattleDeathResolution.Drain(player, battle, record);
                if (player.CurrentHealth <= 0)
                {
                    FinishDefeat(player, battle);
                    result.Outcome = BattleTurnOutcome.Defeat;
                }
                else if (battle.AllEnemiesDead() || tutorial != null && tutorial.ShouldKillTutorialEnemyAfterPlayerTurn)
                {
                    battle.EndPlayerResolveRules();
                    if (!battle.AllEnemiesDead())
                        foreach (EnemyModel enemy in battle.Enemies) enemy?.Kill(new CombatantModel(player));
                    FinishVictory(player, battle, removed, record);
                    result.Outcome = player.CurrentHealth > 0 ? BattleTurnOutcome.Victory : BattleTurnOutcome.Defeat;
                }
                else
                {
                battle.BeginEnemyTurn();
                record(new BattleResolutionStep { Kind = BattleResolutionStepKind.EnemyTurnStarted });
                int actionIndex = 0;
                // Keep the UI rule schedule: a started enemy completes its entire group before deaths
                // are drained, even if a hit kills it or the player. Summons are visited but cannot act.
                for (int enemyIndex = 0; enemyIndex < battle.Enemies.Count; enemyIndex++)
                {
                    EnemyModel enemy = battle.Enemies[enemyIndex];
                    if (enemy == null || enemy.IsDead || !enemy.CanActThisEnemyTurn)
                        continue;

                    BattleStateSnapshot actionBefore = BattleStateSnapshot.Capture(player, battle);
                    battle.BeginEnemyAction(enemy);
                    record(new BattleResolutionStep { Kind = BattleResolutionStepKind.EnemyActionStarted,
                        ActingEnemy = enemy, EnemyActionIndex = actionIndex, Before = actionBefore });
                    for (int intentIndex = 0; intentIndex < enemy.CurrentIntents.Count; intentIndex++)
                    {
                        EnemyIntentData intent = enemy.CurrentIntents[intentIndex];
                        int hitCount = enemy.GetIntentHitCount(intent);
                        for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
                        {
                            BattleStateSnapshot hitBefore = BattleStateSnapshot.Capture(player, battle);
                            record(new BattleResolutionStep { Kind = BattleResolutionStepKind.BeforeEnemyIntent,
                                ActingEnemy = enemy, Intent = BattleSnapshotValues.CopyIntent(intent),
                                IntentIndex = intentIndex, HitIndex = hitIndex, HitCount = hitCount,
                                EnemyActionIndex = actionIndex, Before = hitBefore });
                            BattleActionResult hit = hitCount > 1
                                ? battle.ResolveEnemyIntentHitAt(enemy, intentIndex, hitIndex)
                                : battle.ResolveEnemyIntentAt(enemy, intentIndex);
                            intents.Add(new BattleEnemyIntentResolution(enemy, intentIndex, hitIndex, hit));
                            record(new BattleResolutionStep { Kind = BattleResolutionStepKind.EnemyIntentResolved,
                                ActingEnemy = enemy, Intent = BattleSnapshotValues.CopyIntent(intent),
                                IntentIndex = intentIndex, HitIndex = hitIndex, HitCount = hitCount,
                                EnemyActionIndex = actionIndex, Before = hitBefore });
                        }
                    }
                    BattleStateSnapshot actionEndBefore = BattleStateSnapshot.Capture(player, battle);
                    battle.EndEnemyAction(enemy);
                    record(new BattleResolutionStep { Kind = BattleResolutionStepKind.EnemyActionEnded,
                        ActingEnemy = enemy, EnemyActionIndex = actionIndex++, Before = actionEndBefore });
                    BattleDeathResolution.Drain(player, battle, record);
                    if (player.CurrentHealth <= 0)
                        break;
                }
                BattleStateSnapshot enemyTurnEndBefore = BattleStateSnapshot.Capture(player, battle);
                battle.EndEnemyTurn();
                record(new BattleResolutionStep { Kind = BattleResolutionStepKind.EnemyTurnEnded, Before = enemyTurnEndBefore });
                BattleDeathResolution.Drain(player, battle, record);

                if (player.CurrentHealth <= 0)
                {
                    FinishDefeat(player, battle);
                    result.Outcome = BattleTurnOutcome.Defeat;
                }
                else if (battle.AllEnemiesDead())
                {
                    FinishVictory(player, battle, removed, record);
                    result.Outcome = player.CurrentHealth > 0 ? BattleTurnOutcome.Victory : BattleTurnOutcome.Defeat;
                }
                else
                {
                    BattleStateSnapshot turnStartBefore = BattleStateSnapshot.Capture(player, battle);
                    result.NextPlayerTurnStart = battle.BeginPlayerTurnStartRules(nextTurnDrawCount ?? player.DrawCount,
                        () => tutorial?.TryApplyFixedTurnHand(player) ?? false);
                    battle.ResetContinuousCastCount();
                    record(new BattleResolutionStep { Kind = BattleResolutionStepKind.PlayerTurnStarted, Before = turnStartBefore });
                    BattleDeathResolution.Drain(player, battle, record);
                    if (player.CurrentHealth <= 0)
                    {
                        FinishDefeat(player, battle);
                        result.Outcome = BattleTurnOutcome.Defeat;
                    }
                    else if (battle.AllEnemiesDead())
                    {
                        FinishVictory(player, battle, removed, record);
                        result.Outcome = player.CurrentHealth > 0 ? BattleTurnOutcome.Victory : BattleTurnOutcome.Defeat;
                    }
                    else result.Outcome = BattleTurnOutcome.NextPlayerTurn;
                }
                }
            }
            if (result.Outcome != BattleTurnOutcome.NextPlayerTurn)
                record(new BattleResolutionStep { Kind = BattleResolutionStepKind.BattleFinished,
                    RemovedTemporaryCards = new List<MaterialModel>(removed).AsReadOnly() });
        }
        finally
        {
            battle.EndCastTarget();
            battle.EndPlayerResolveRules();
        }
        result.Steps = steps.AsReadOnly();
        result.EnemyIntents = intents.AsReadOnly();
        result.RemovedTemporaryCards = removed.AsReadOnly();
        result.FinalState = BattleStateSnapshot.Capture(player, battle);
        return result;
        }
    }

    private static void FinishDefeat(PlayerState player, BattleManager battle)
    {
        player.Relics.EndBattle(false);
        battle.ResetContinuousCastCount();
        battle.FinishBattle();
    }

    private static void FinishVictory(PlayerState player, BattleManager battle,
        List<MaterialModel> removed, Action<BattleResolutionStep> onStep)
    {
        BattleDeathResolution.Drain(player, battle, onStep);
        if (player.CurrentHealth <= 0)
        {
            FinishDefeat(player, battle);
            return;
        }
        if (battle.KillAliveMinionsForVictory())
            BattleDeathResolution.Drain(player, battle, onStep);
        if (player.CurrentHealth <= 0)
            FinishDefeat(player, battle);
        else
            battle.FinishBattleRules(removed);
    }
}
