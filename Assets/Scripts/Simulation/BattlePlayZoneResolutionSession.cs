using System;
using System.Collections.Generic;

/// <summary>
/// The player play-zone rule schedule, shared by an animated host and a synchronous host.
    /// It has no views or clock. Every synchronous advance enters its owning player's context;
    /// never retain ambient scopes over presentation yields.
/// </summary>
public sealed class BattlePlayZoneResolutionSession : IDisposable
{
    private readonly PlayerState player;
    private readonly BattleManager battle;
    private readonly IReadOnlyList<int> readingRanks;
    private IEnumerator<BattleResolutionStep> iterator;
    private bool disposed;
    private readonly MagicTrigger[] triggers;

    public IReadOnlyList<MaterialModel> Cards { get; }
    public IReadOnlyList<MagicModel> Magics { get; }
    public ArrowReadSequence ReadSequence { get; private set; }
    public BattleResolutionStep Current { get; private set; }
    public bool IsCompleted { get; private set; }

    /// <summary>
    /// Snapshots input lists without reading UI or building a sequence. Pass the current UI's magic
    /// candidates and matching reading ranks, or an explicit script order. Null ranks use the existing
    /// MagicMatchOrderUtility SlotIndex fallback. The host owns Begin/EndPlayerResolveRules.
    /// </summary>
    public BattlePlayZoneResolutionSession(
        PlayerState player,
        BattleManager battle,
        IReadOnlyList<MaterialModel> cards,
        IReadOnlyList<MagicModel> magics,
        IReadOnlyList<int> readingRanks = null)
    {
        this.player = player ?? throw new ArgumentNullException(nameof(player));
        this.battle = battle ?? throw new ArgumentNullException(nameof(battle));
        if (!ReferenceEquals(battle.PlayerState, player))
            throw new ArgumentException("The battle must belong to the supplied player.", nameof(battle));
        Cards = Copy(cards);
        Magics = Copy(magics);
        this.readingRanks = Copy(readingRanks ?? player.CaptureMagicReadingRanks(Magics));
        triggers = new MagicTrigger[Magics.Count];
        for (int i = 0; i < triggers.Length; i++)
            triggers[i] = Magics[i]?.GetCombatTrigger(player) ?? default;
    }

    /// <summary>
    /// Returns the next checkpoint. A Before checkpoint has not committed its associated rule yet.
    /// Advancing after BeforeArrow resolves the base effect; after BeforeMagicFill invokes tokens and
    /// locks the target; after BeforeMagicImpact registers one match and calls MagicModel.Cast.
    /// DeathPending is likewise resolved by the following advance. Completed is returned once.
    /// </summary>
    public bool MoveNext(out BattleResolutionStep step)
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(BattlePlayZoneResolutionSession));
        if (IsCompleted)
        {
            step = null;
            return false;
        }
        if (iterator == null)
            iterator = Resolve().GetEnumerator();
        bool advanced;
        using (player.Rules.Enter()) advanced = iterator.MoveNext();
        if (advanced)
        {
            Current = step = iterator.Current;
            if (step.Kind == BattleResolutionStepKind.Completed)
                IsCompleted = true;
            return true;
        }
        IsCompleted = true;
        Current = step = null;
        return false;
    }

    public void RunToCompletion(Action<BattleResolutionStep> onStep = null,
        RuleExecutionScope.RuleFeedbackCapture feedback = null)
    {
        while (MoveNext(out BattleResolutionStep step))
        {
            if (feedback != null) step.FeedbackCues = feedback.Drain();
            onStep?.Invoke(step);
        }
    }

    /// <summary>Abandoning a session does not silently finish unexecuted rules or after-read actions.</summary>
    public void Dispose()
    {
        if (disposed)
            return;
        iterator?.Dispose();
        disposed = true;
    }

    private IEnumerable<BattleResolutionStep> Resolve()
    {
        // Build exactly once: BeforeArrowRead hooks and random choices belong to this execution.
        ReadSequence = ArrowReadSystem.BuildSequence(Cards, player, battle);
        var afterReadSteps = new List<ArrowReadStep>();
        var afterReadCards = new HashSet<MaterialModel>();
        var matchedIndices = new List<int>();
        bool stop = false;
        try
        {
            for (int sequenceIndex = 0; sequenceIndex < ReadSequence.ResolveSequences.Count && !stop; sequenceIndex++)
            {
                ArrowReadSequence sequence = ReadSequence.ResolveSequences[sequenceIndex];
                for (int stepIndex = 0; stepIndex < sequence.Steps.Count && !stop; stepIndex++)
                {
                    ArrowReadStep arrow = sequence.Steps[stepIndex];
                    if (arrow.SourceCard != null)
                    {
                        yield return CreateStep(BattleResolutionStepKind.BeforeArrow, sequenceIndex, stepIndex, sequence, arrow);
                        BattleStateSnapshot before = BattleStateSnapshot.Capture(player, battle);
                        MagicCastResult result = ArrowStepRuleResolver.Resolve(player, battle, arrow, sequenceIndex > 0);
                        if (sequenceIndex == 0 && arrow.RemovesSourceAfterRead && afterReadCards.Add(arrow.SourceCard))
                            afterReadSteps.Add(arrow);
                        BattleResolutionStep resolved = CreateStep(BattleResolutionStepKind.AfterArrow, sequenceIndex, stepIndex, sequence, arrow);
                        resolved.CastResult = result;
                        resolved.Before = before;
                        resolved.After = BattleStateSnapshot.Capture(player, battle);
                        yield return resolved;
                        foreach (BattleResolutionStep death in BattleDeathResolution.Steps(player, battle))
                            yield return death;
                        if (player.CurrentHealth <= 0 || battle.AllEnemiesDead())
                        {
                            stop = true;
                            break;
                        }
                    }

                    if (arrow.FirstTokenIndex < 0)
                        continue;
                    int tokenEnd = arrow.FirstTokenIndex + arrow.Tokens.Count;
                    for (int tokenStart = arrow.FirstTokenIndex; tokenStart < tokenEnd && !stop; tokenStart++)
                    {
                        // One collection per token start, after the base rule and its death barrier.
                        MagicMatchOrderUtility.CollectMatchedMagicIndices(Magics, sequence.Tokens, tokenStart, matchedIndices, readingRanks, triggers);
                        for (int matchIndex = 0; matchIndex < matchedIndices.Count; matchIndex++)
                        {
                            int magicIndex = matchedIndices[matchIndex];
                            MagicModel magic = Magics[magicIndex];
                            MagicTrigger trigger = triggers[magicIndex];
                            int matchLength = trigger.Length;
                            BattleResolutionStep fill = CreateMagicStep(BattleResolutionStepKind.BeforeMagicFill, sequenceIndex, stepIndex, sequence, arrow, tokenStart, matchLength, magicIndex, magic, trigger.Source);
                            // Snapshot the matching requirement before token hooks or the cast can change its inputs.
                            IReadOnlyList<MaterialEnum> triggerMaterials = trigger.Rule == MagicMatchRule.ExactRecipe
                                ? Copy(trigger.Recipe)
                                : MagicModel.TryGetDistinctTriggerElements(sequence.Tokens, tokenStart, out var first, out var second)
                                    ? Array.AsReadOnly(new[] { first, second }) : Array.Empty<MaterialEnum>();
                            yield return fill;

                            // Preserve the production timing: token hooks run after fill, before target lock.
                            BattleStateSnapshot before = BattleStateSnapshot.Capture(player, battle);
                            TriggerMatchedTokens(sequence, arrow, tokenStart, matchLength);
                            EnemyModel target = battle.BeginCastTarget();
                            BattleResolutionStep impact = CreateMagicStep(BattleResolutionStepKind.BeforeMagicImpact, sequenceIndex, stepIndex, sequence, arrow, tokenStart, matchLength, magicIndex, magic, trigger.Source);
                            impact.Target = target;
                            impact.Before = before;
                            impact.After = BattleStateSnapshot.Capture(player, battle);
                            yield return impact;

                            // As in HandSystemUI.CastMagic, skip enemy-target effects without any live enemy.
                            bool castPerformed = battle.GetFirstAliveEnemy() != null || trigger.TargetsPlayer;
                            MagicCastResult result = null;
                            if (castPerformed)
                            {
                                battle.SetFocusTarget(battle.FocusTarget);
                                battle.BeginCastTarget();
                                battle.RegisterMagicCast(); // Once per match, not once per MagicModel replay.
                                result = magic.Cast(player, battle, trigger);
                            }
                            BattleResolutionStep cast = CreateMagicStep(BattleResolutionStepKind.AfterMagic, sequenceIndex, stepIndex, sequence, arrow, tokenStart, matchLength, magicIndex, magic, trigger.Source);
                            cast.Target = target;
                            cast.CastResult = result;
                            cast.CastPerformed = castPerformed;
                            cast.TriggerMaterials = triggerMaterials;
                            if (result != null && result.CreatedHandCards.Count > 0)
                                cast.CreatedHandCards = result.CreatedHandCards.AsReadOnly();
                            cast.ContinuousCastCount = battle.ContinuousCastCount;
                            cast.Before = before;
                            cast.After = BattleStateSnapshot.Capture(player, battle);
                            yield return cast;
                            foreach (BattleResolutionStep death in BattleDeathResolution.Steps(player, battle))
                                yield return death;
                            if (player.CurrentHealth <= 0 || battle.AllEnemiesDead())
                            {
                                stop = true;
                                break;
                            }
                        }
                    }
                }
            }

            yield return new BattleResolutionStep { Kind = BattleResolutionStepKind.BeforeAfterRead };
            BattleStateSnapshot afterReadBefore = BattleStateSnapshot.Capture(player, battle);
            var actions = new List<BattleAfterReadActionResult>(afterReadSteps.Count);
            for (int i = 0; i < afterReadSteps.Count; i++)
            {
                ArrowReadStep arrow = afterReadSteps[i];
                bool applied = player.ApplyArrowReadAfterAction(arrow.SourceCard, arrow.AfterReadAction);
                actions.Add(new BattleAfterReadActionResult(arrow, applied));
            }
            yield return new BattleResolutionStep
            {
                Kind = BattleResolutionStepKind.AfterRead,
                Before = afterReadBefore,
                After = BattleStateSnapshot.Capture(player, battle),
                AfterReadActions = actions.AsReadOnly()
            };
        }
        finally
        {
            // Release any target left locked by a skipped cast or an interrupted presentation.
            battle.EndCastTarget();
        }
        yield return new BattleResolutionStep { Kind = BattleResolutionStepKind.Completed };
    }

    private void TriggerMatchedTokens(ArrowReadSequence sequence, ArrowReadStep arrow, int tokenStart, int matchLength)
    {
        for (int tokenOffset = 0; tokenOffset < matchLength; tokenOffset++)
        {
            ArrowReadToken token = sequence.Tokens[tokenStart + tokenOffset];
            MaterialModifierModel.CurrentContext = new MaterialModifierContext
            {
                PlayerState = player, BattleManager = battle, Token = token, Step = arrow
            };
            token.SourceCard.TriggerOnTokenInvoke(token, arrow);
        }
    }

    private static BattleResolutionStep CreateStep(BattleResolutionStepKind kind, int sequenceIndex, int stepIndex, ArrowReadSequence sequence, ArrowReadStep arrow)
    {
        return new BattleResolutionStep
        {
            Kind = kind,
            SequenceIndex = sequenceIndex,
            StepIndex = stepIndex,
            Sequence = sequence,
            ArrowStep = arrow,
            ArrowDisplayMaterial = arrow != null ? arrow.PrimaryDisplayMaterial : MaterialEnum.None
        };
    }

    private static BattleResolutionStep CreateMagicStep(BattleResolutionStepKind kind, int sequenceIndex, int stepIndex, ArrowReadSequence sequence, ArrowReadStep arrow, int tokenStart, int matchLength, int magicIndex, MagicModel magic, MagicModel effectSource)
    {
        BattleResolutionStep step = CreateStep(kind, sequenceIndex, stepIndex, sequence, arrow);
        step.TokenStart = tokenStart;
        step.MatchLength = matchLength;
        step.MagicIndex = magicIndex;
        step.Magic = magic;
        step.MagicEffectSource = effectSource;
        var display = new List<MaterialEnum>();
        for (int i = 0; i < matchLength; i++) display.Add(sequence.Tokens[tokenStart + i].DisplayMaterial);
        step.TokenDisplayMaterials = display.AsReadOnly();
        return step;
    }

    private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> values)
    {
        var result = new List<T>(values != null ? values.Count : 0);
        for (int i = 0; values != null && i < values.Count; i++)
            result.Add(values[i]);
        return result.AsReadOnly();
    }
}
