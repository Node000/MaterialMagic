using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Unity playback adapters for committed transactions and the shared battle rule schedule.</summary>
public partial class HandSystemUI
{
    public IEnumerator PlayCommittedGoldGain(int before, int after, RectTransform source)
    {
        int displayed = before;
        if (after > before) playerState?.Services.OnGoldGained();
        GoldDisplayUI display = GetUIManager().GoldDisplay;
        if (display != null && after > before)
            yield return display.PlayGain(after - before, source, () => displayed++, () => displayed);
        RefreshStaticUI();
    }

    public void PlayCommittedRewardHeal(int before, int after)
    {
        PlayRewardHealFeedback(after - before);
        RefreshStaticUI();
    }

    public void NotifyShopTransactionCommitted(MagicData data, int slotIndex, MagicModel previous, bool magicChanged)
    {
        if (data != null && magicChanged)
        {
            EmitMagicReplaced(previous, data);
            EmitMagicAcquire(data, slotIndex, previous);
        }
        RefreshMaterialListPanel();
        RefreshStaticUI();
        SaveRunProgressForce();
    }

    public void PlayCommittedShopMagicAcquisition(MagicData data, int slotIndex, int resultingCount,
        RectTransform source, Action done)
    {
        StartCoroutine(PlayCommittedShopMagicAcquisitionRoutine(data, slotIndex, resultingCount, source, done));
    }

    private IEnumerator PlayCommittedShopMagicAcquisitionRoutine(MagicData data, int slotIndex, int resultingCount,
        RectTransform source, Action done)
    {
        // The model already contains the item. Never calculate Count + 1 during this flight.
        try
        {
            yield return PlayMagicAcquireAnimation(data, slotIndex, source, resultingCount, true, true);
        }
        finally
        {
            CreateMagicViews();
            done?.Invoke();
        }
    }

    public void PlayCommittedShopMaterialAcquisition(MaterialEnum material, RectTransform source, Action done)
    {
        StartCoroutine(PlayCommittedShopMaterialAcquisitionRoutine(material, source, done));
    }

    private IEnumerator PlayCommittedShopMaterialAcquisitionRoutine(MaterialEnum material, RectTransform source, Action done)
    {
        try { yield return PlayMaterialAcquireAnimation(material, source); }
        finally { RefreshMaterialListPanel(); RefreshStaticUI(); done?.Invoke(); }
    }

    private MagicItemView FindMagicPresentation(MagicModel magic)
    {
        for (int i = 0; i < magicViews.Count; i++)
            if (magicViews[i] != null && ReferenceEquals(magicViews[i].Magic, magic))
                return magicViews[i];
        return null;
    }

    private void CaptureMagicReadingOrder()
    {
        magicMatchCandidates.Clear();
        magicSlotPositions.Clear();
        // Preserve the currently displayed order as explicit input to the rule session.
        // With no views, scripts and recovery still execute from the player's actual book.
        if (magicViews.Count == 0)
        {
            magicMatchCandidates.AddRange(playerState.MagicBook);
            magicSlotVisualRanks.Clear();
            magicSlotVisualRanks.AddRange(playerState.CaptureMagicReadingRanks(magicMatchCandidates));
            return;
        }
        for (int i = 0; i < magicViews.Count; i++)
        {
            magicMatchCandidates.Add(magicViews[i] != null ? magicViews[i].Magic : null);
            RectTransform rect = magicViews[i] != null ? magicViews[i].transform as RectTransform : null;
            magicSlotPositions.Add(rect != null ? rect.anchoredPosition : Vector2.zero);
        }
        MagicBookVisualOrder.ComputeRanks(magicBookArea, magicSlotPositions, magicSlotVisualRanks);
        var order = new List<SlotReadingRank>();
        for (int i = 0; i < magicSlotVisualRanks.Count; i++)
            order.Add(new SlotReadingRank(magicMatchCandidates[i]?.SlotIndex ?? i, magicSlotVisualRanks[i]));
        playerState.SetMagicReadingOrder(order);
    }

    private BattleStateSnapshot presentationState;
    private bool recordingBattleResolution;
    private BattleTurnResult committedTurn;
    private readonly Dictionary<EnemyModel, MagicDamageHitResult> displayedMagicHits = new Dictionary<EnemyModel, MagicDamageHitResult>();

    public MaterialModel GetPresentedCardDisplay(MaterialModel identity) => presentationState?.GetCardDisplay(identity) ?? identity;
    private BattleEnemySnapshot PresentedEnemy(EnemyModel enemy) => presentationState?.FindEnemy(enemy);
    private bool PresentedEnemyIsDead(EnemyModel enemy) => presentationState != null
        ? (PresentedEnemy(enemy)?.IsDead ?? true) : enemy == null || enemy.IsDead;
    private int PresentedPlayerHealth => presentationState?.PlayerHealth ?? playerState.CurrentHealth;
    private int PresentedPlayerShield => presentationState?.PlayerShield ?? playerState.Shield;
    private int PresentedComboCount => presentationState?.ContinuousCastCount ?? battleManager?.ContinuousCastCount ?? 0;

    private void ApplyPresentationState(BattleStateSnapshot state, IReadOnlyList<MaterialModel> incomingCards = null,
        RectTransform entryOrigin = null, ISet<MaterialModel> deferredHandViews = null)
    {
        if (state == null) return;
        presentationState = state;
        displayedMagicHits.Clear();
        foreach (BattleEnemySnapshot enemy in state.Enemies)
        {
            if (FindEnemyViewState(enemy.Enemy) != null) continue;
            if (!enemyModels.Contains(enemy.Enemy)) enemyModels.Add(enemy.Enemy);
            if (enemyArea != null && enemyViewPrefab != null)
                CreateEnemyView(enemy.Enemy, enemyViewStates.Count, state.Enemies.Count);
        }
        // A token can draw a new card directly into the play-zone (for example Vortex).
        // Add missing identities at this checkpoint, but leave removed cards for their
        // AfterRead/TurnEnd exit animation instead of rebuilding/recycling the whole list.
        bool addedCards = false;
        Dictionary<MaterialModel, float> entryDelays = null;
        if (incomingCards != null && incomingCards.Count > 0)
        {
            entryDelays = new Dictionary<MaterialModel, float>(incomingCards.Count);
            float stagger = ResolveMotionDuration(Mathf.Min(0.1f, 1f / incomingCards.Count));
            for (int i = 0; i < incomingCards.Count; i++)
                if (incomingCards[i] != null) entryDelays[incomingCards[i]] = i * stagger;
        }
        if (cardPrefab != null && handArea != null && playArea != null)
        {
            foreach (MaterialModel card in state.Hand)
                if (card != null && FindView(card) == null && (deferredHandViews == null || !deferredHandViews.Contains(card)))
                {
                    EnsureCardView(card, false, true, entryDelays != null && entryDelays.ContainsKey(card) ? entryOrigin : null);
                    addedCards = true;
                }
            foreach (MaterialModel card in state.PlayZone)
                if (card != null && FindView(card) == null)
                { EnsureCardView(card, true, true); addedCards = true; }
        }
        foreach (HandCardView view in cardViews) view?.RefreshUpgradeVisual();
        if (addedCards) UpdateLayout(handEntryDelays: entryDelays);
        LayoutEnemyViews();
    }

    private void PlayStepCues(BattleResolutionStep step)
    {
        foreach (Action cue in step.FeedbackCues) cue?.Invoke();
    }

    private IEnumerator ResolveCommittedBattleTurn()
    {
        busy = true;
        SetButtonsInteractable(false);
        ResetMagicHighlights();
        yield return WaitResolveTransitionLocks();
        CaptureMagicReadingOrder();
        GetUIManager().HidePileHoverPanel();
        GetUIManager().MaterialListPanel?.EndSelectionMode();
        var cards = new List<MaterialModel>(playerState.PlayZone);
        try
        {
            recordingBattleResolution = true;
            try
            {
                committedTurn = BattleTurnDriver.ResolveTurn(battleManager, magicMatchCandidates,
                    magicSlotVisualRanks.Count == magicMatchCandidates.Count ? magicSlotVisualRanks : null,
                    tutorial: TutorialManager?.RulePolicy);
            }
            finally { recordingBattleResolution = false; }
            ApplyPresentationState(committedTurn.InitialState);
            yield return PlayBattleRuleResolution(cards, committedTurn.Steps);
            BattleTurnResult turn = committedTurn;
            if (turn.Outcome == BattleTurnOutcome.Defeat)
            {
                presentationState = null;
                CheckPlayerDefeated();
                yield break;
            }
            if (turn.Outcome == BattleTurnOutcome.Victory)
            {
                ApplyPresentationState(turn.FinalState);
                yield return FinishBattleRoutine();
                yield break;
            }
            presentationState = null;
            committedTurn = null;
            refreshUsedThisTurn = false;
            ResetMagicHighlights();
            GetUIManager().PlayArea?.HideResolveIndicator();
            ResetContinuousCastCounterUI();
            suppressEnemyIntentRefresh = false;
            RefreshEnemyUI(); RefreshStaticUI(); RefreshMaterialListPanel();
            RebuildCards(animateFromCurrent: true);
            GetUIManager().PlayerFeedback?.UpdateVignetteRange(playerState);
            GetUIManager().TurnBanner?.Show(LocalizationSystem.GetText("ui.battle.turn_banner.player", "你的回合"));
            busy = false;
            SetButtonsInteractable(true);
        }
        finally
        {
            // Disposing playback never rolls rules back or retains a stale display/save guard.
            recordingBattleResolution = false;
            presentationState = null;
            committedTurn = null;
            displayedMagicHits.Clear();
            deathPipelineRunning = false;
            suppressEnemyIntentRefresh = false;
            GetCommandSession()?.CompleteTurn();
        }
    }

    private IEnumerator PlayBattleRuleResolution(List<MaterialModel> cards)
    {
        if (playerState == null || battleManager == null) yield break;
        CaptureMagicReadingOrder();
        var steps = new List<BattleResolutionStep>();
        using (var session = new BattlePlayZoneResolutionSession(playerState, battleManager, cards,
            magicMatchCandidates, magicSlotVisualRanks.Count == magicMatchCandidates.Count ? magicSlotVisualRanks : null))
        using (var feedback = RuleExecutionScope.CaptureFeedback())
        {
            recordingBattleResolution = true;
            try { session.RunToCompletion(steps.Add, feedback); }
            finally { recordingBattleResolution = false; }
        }
        try { yield return PlayBattleRuleResolution(cards, steps); }
        finally { presentationState = null; displayedMagicHits.Clear(); }
    }

    private IEnumerator PlayBattleRuleResolution(List<MaterialModel> cards, IReadOnlyList<BattleResolutionStep> steps)
    {
        if (playerState == null || battleManager == null)
            yield break;
        PlayAreaUI area = GetUIManager().PlayArea;
        bool animate = area != null && area.ResolveIndicator != null;
        if (animate) area.ShowResolveIndicator();
        BattleResolutionStepKind pendingFeedback = BattleResolutionStepKind.Completed;
        var deaths = new DeathPlaybackState();
        try
        {
            bool enemyBannerShown = false;
            nextEnemyRuleResolveIndex = 0;
            nextEnemyPresentationIndex = 0;
            for (int traceIndex = 0; traceIndex < steps.Count; traceIndex++)
            {
                BattleResolutionStep step = steps[traceIndex];
                if (step.Kind == BattleResolutionStepKind.EnemyActionStarted)
                {
                    suppressEnemyIntentRefresh = true;
                    if (!enemyBannerShown)
                    {
                        enemyBannerShown = true;
                        GetUIManager().TurnBanner?.Show(LocalizationSystem.GetText("ui.battle.turn_banner.enemy", "敌方回合"));
                    }
                    var group = new List<BattleResolutionStep> { step };
                    while (traceIndex + 1 < steps.Count && steps[traceIndex + 1].Kind != BattleResolutionStepKind.EnemyActionStarted &&
                        steps[traceIndex + 1].Kind != BattleResolutionStepKind.EnemyTurnEnded)
                        group.Add(steps[++traceIndex]);
                    EnemyActionPresentationRunner.Start(PlayCommittedEnemyGroup(group, step.EnemyActionIndex));
                    continue;
                }
                if (step.Kind == BattleResolutionStepKind.EnemyTurnEnded)
                    yield return EnemyActionPresentationRunner.WaitForAll();

                bool deathStep = step.Kind == BattleResolutionStepKind.DeathPending || step.Kind == BattleResolutionStepKind.DeathResolved;
                if (!deathStep)
                {
                    if (deaths.Started)
                    {
                        yield return DeathPresentationRunner.WaitForAll();
                        deaths.Started = false;
                        deathPipelineRunning = false;
                    }
                    // This is the same post-rule pause as the previous iterator, after its death barrier.
                    if (pendingFeedback != BattleResolutionStepKind.Completed)
                    {
                        if (!AllEnemiesDead()) yield return new WaitForSeconds(postMagicResolveDelay);
                        if (pendingFeedback == BattleResolutionStepKind.AfterMagic) ResetMagicHighlights();
                        pendingFeedback = BattleResolutionStepKind.Completed;
                    }
                }

                if (step.Kind != BattleResolutionStepKind.AfterArrow && step.Kind != BattleResolutionStepKind.AfterMagic &&
                    step.Kind != BattleResolutionStepKind.BeforeMagicImpact)
                {
                    ApplyPresentationState(step.After ?? step.Before);
                    PlayStepCues(step);
                }
                switch (step.Kind)
                {
                    case BattleResolutionStepKind.BeforeArrow:
                        if (!animate) break;
                        ResetMagicHighlights();
                        MoveIndicatorToReadStep(cards, step.ArrowStep, step.SequenceIndex == 0 && step.StepIndex == 0);
                        yield return new WaitForSeconds(ResolveMotionDuration(layoutDuration) * 0.35f);
                        PunchReadCard(step.ArrowStep.SourceCard);
                        playerCastAnimator?.PlayMaterialAction(step.ArrowDisplayMaterial);
                        break;
                    case BattleResolutionStepKind.AfterArrow:
                        yield return PlayCommittedCastFeedback(step);
                        pendingFeedback = step.Kind;
                        break;
                    case BattleResolutionStepKind.BeforeMagicFill:
                    {
                        if (!animate) break;
                        ResetMagicHighlights();
                        MagicItemView view = FindMagicPresentation(step.Magic);
                        MoveIndicatorToTokenRange(cards, step.Sequence.Tokens, step.TokenStart, step.MatchLength, false);
                        yield return new WaitForSeconds(ResolveMotionDuration(layoutDuration) * 0.65f);
                        for (int i = 0; i < step.MatchLength; i++)
                        {
                            ArrowReadToken token = step.Sequence.Tokens[step.TokenStart + i];
                            HandCardView card = FindView(token.SourceCard);
                            PunchReadCard(token.SourceCard);
                            if (card != null && view != null) PlayMaterialFillParticle(card, view, step.TokenDisplayMaterials[i]);
                        }
                        yield return new WaitForSeconds(GetParticleArrivalWait());
                        break;
                    }
                    case BattleResolutionStepKind.BeforeMagicImpact:
                    {
                        ApplyPresentationState(step.After ?? step.Before);
                        PlayStepCues(step);
                        if (!animate) break;
                        MagicItemView view = FindMagicPresentation(step.Magic);
                        if (view == null) break;
                        for (int i = 0; i < step.MatchLength; i++) view.HighlightRecipeSlot(i);
                        bool arrived = false;
                        float wait = PlayMagicCastParticle(view, step.MagicEffectSource ?? step.Magic, step.Target, () => arrived = true);
                        float elapsed = 0f;
                        while (!arrived && elapsed < wait) { elapsed += Time.deltaTime; yield return null; }
                        break;
                    }
                    case BattleResolutionStepKind.AfterMagic:
                        if (step.CastPerformed)
                        {
                            MagicItemView source = FindMagicPresentation(step.Magic);
                            source?.PlayTriggerFeedback(step.TriggerMaterials);
                            UpdateContinuousCastCounter(step.ContinuousCastCount, false);
                            yield return PlayCommittedCastFeedback(step, source != null ? source.transform as RectTransform : null);
                            // Card arrivals use the existing stagger. Trigger rings never take part in this wait.
                            if (step.CreatedHandCards.Count > 0)
                            {
                                float stagger = Mathf.Min(0.1f, 1f / step.CreatedHandCards.Count);
                                yield return new WaitForSeconds((step.CreatedHandCards.Count - 1) * ResolveMotionDuration(stagger)
                                    + ResolveMotionDuration(layoutDuration));
                            }
                        }
                        else { ApplyPresentationState(step.After); PlayStepCues(step); }
                        pendingFeedback = step.Kind;
                        break;
                    case BattleResolutionStepKind.DeathPending:
                    case BattleResolutionStepKind.DeathResolved:
                        yield return PlayCommittedDeathCheckpoint(step, deaths);
                        break;
                    case BattleResolutionStepKind.AfterRead:
                        yield return PlayCommittedAfterReadActions(step.AfterReadActions);
                        break;
                    case BattleResolutionStepKind.PlayerResolveStarted:
                        RefreshStaticUI(); RefreshEnemyUI();
                        yield return WaitResolveTransitionLocks();
                        break;
                    case BattleResolutionStepKind.HandDiscardedOnConfirm:
                        yield return PlayTurnCardExits(step.Before.Hand, step);
                        break;
                    case BattleResolutionStepKind.PlayerTurnEnded:
                        GetUIManager().PlayArea?.HideResolveIndicator();
                        HideContinuousCastCounterUI();
                        yield return PlayTurnCardExits(step.Before.PlayZone, step);
                        break;
                    case BattleResolutionStepKind.EnemyActionStarted:
                        suppressEnemyIntentRefresh = true;
                        GetUIManager().TurnBanner?.Show(LocalizationSystem.GetText("ui.battle.turn_banner.enemy", "敌方回合"));
                        break;
                    case BattleResolutionStepKind.BeforeEnemyIntent:
                    {
                        EnemyViewState state = FindEnemyViewState(step.ActingEnemy);
                        if (step.HitIndex == 0)
                        {
                            EnemyIntentView view = state != null && step.IntentIndex < state.intentViews.Count ? state.intentViews[step.IntentIndex] : null;
                            Tween ripple = view != null ? view.PlayRipple(enemyIntentRippleDuration) : null;
                            if (ripple != null) yield return ripple.WaitForCompletion();
                            else yield return new WaitForSeconds(enemyIntentRippleDuration);
                            yield return new WaitForSeconds(enemyIntentPrePerformDelay);
                        }
                        yield return PlayEnemyIntentPerformance(state, step.Intent);
                        break;
                    }
                    case BattleResolutionStepKind.EnemyIntentResolved:
                    {
                        EnemyViewState state = FindEnemyViewState(step.ActingEnemy);
                        PlayPlayerDamageFeedbackIfNeeded(step.Before.PlayerHealth, step.Before.PlayerShield);
                        PlayEnemyShieldFeedbackIfNeeded(state, step.ActingEnemy, step.Before.FindEnemy(step.ActingEnemy)?.Shield ?? 0);
                        RefreshStaticUI(); RefreshEnemyUI(state, false);
                        if (step.HitIndex + 1 < step.HitCount)
                            yield return new WaitForSeconds(step.ActingEnemy.GetIntentHitInterval(step.Intent));
                        else
                        {
                            EnemyIntentView view = state != null && step.IntentIndex < state.intentViews.Count ? state.intentViews[step.IntentIndex] : null;
                            Tween fade = view != null ? view.PlayFadeOut(enemyIntentFadeOutDuration) : null;
                            if (fade != null) yield return fade.WaitForCompletion();
                            yield return new WaitForSeconds(enemyIntentBetweenDelay);
                        }
                        break;
                    }
                    case BattleResolutionStepKind.EnemyActionEnded:
                        RefreshEnemyUI();
                        break;
                    case BattleResolutionStepKind.EnemyTurnEnded:
                        RefreshStaticUI();
                        if (enemyTurnEndDelay > 0f) yield return new WaitForSeconds(enemyTurnEndDelay);
                        break;
                    case BattleResolutionStepKind.PlayerTurnStarted:
                        RefreshStaticUI();
                        break;
                }
            }
        }
        finally
        {
            deathPipelineRunning = false;
        }
    }

    private IEnumerator PlayCommittedEnemyGroup(IReadOnlyList<BattleResolutionStep> group, int actionIndex)
    {
        while (nextEnemyPresentationIndex != actionIndex) yield return null;
        if (enemyActionOverlapDelay > 0f) yield return new WaitForSeconds(enemyActionOverlapDelay);
        bool released = false, started = false;
        var deaths = new DeathPlaybackState();
        BattleResolutionStep actionStart = group[0];
        for (int i = 1; i < group.Count; i++)
        {
            BattleResolutionStep step = group[i];
            if (step.Kind == BattleResolutionStepKind.BeforeEnemyIntent)
            {
                EnemyViewState state = FindEnemyViewState(step.ActingEnemy);
                if (step.HitIndex == 0)
                {
                    EnemyIntentView view = state != null && step.IntentIndex < state.intentViews.Count ? state.intentViews[step.IntentIndex] : null;
                    Tween ripple = view != null ? view.PlayRipple(enemyIntentRippleDuration) : null;
                    if (ripple != null) yield return ripple.WaitForCompletion();
                    else yield return new WaitForSeconds(enemyIntentRippleDuration);
                    yield return new WaitForSeconds(enemyIntentPrePerformDelay);
                }
                bool last = true;
                for (int j = i + 1; j < group.Count; j++)
                    if (group[j].Kind == BattleResolutionStepKind.BeforeEnemyIntent) { last = false; break; }
                if (last) { ReleaseEnemyPresentation(actionIndex); released = true; }
                yield return PlayEnemyIntentPerformance(state, step.Intent);
                while (nextEnemyRuleResolveIndex != actionIndex) yield return null;
                PlayStepCues(step);
                continue;
            }
            while (nextEnemyRuleResolveIndex != actionIndex) yield return null;
            if (!started)
            {
                started = true;
                ApplyPresentationState(actionStart.After); PlayStepCues(actionStart);
            }
            ApplyPresentationState(step.After ?? step.Before); PlayStepCues(step);
            if (step.Kind == BattleResolutionStepKind.EnemyIntentResolved)
            {
                EnemyViewState state = FindEnemyViewState(step.ActingEnemy);
                PlayPlayerDamageFeedbackIfNeeded(step.Before.PlayerHealth, step.Before.PlayerShield);
                PlayEnemyShieldFeedbackIfNeeded(state, step.ActingEnemy, step.Before.FindEnemy(step.ActingEnemy)?.Shield ?? 0);
                RefreshStaticUI(); RefreshEnemyUI(state, false);
                if (step.HitIndex + 1 < step.HitCount)
                    yield return new WaitForSeconds(step.ActingEnemy.GetIntentHitInterval(step.Intent));
                else
                {
                    EnemyIntentView view = state != null && step.IntentIndex < state.intentViews.Count ? state.intentViews[step.IntentIndex] : null;
                    Tween fade = view != null ? view.PlayFadeOut(enemyIntentFadeOutDuration) : null;
                    if (fade != null) yield return fade.WaitForCompletion();
                    yield return new WaitForSeconds(enemyIntentBetweenDelay);
                }
            }
            else if (step.Kind == BattleResolutionStepKind.DeathPending || step.Kind == BattleResolutionStepKind.DeathResolved)
                yield return PlayCommittedDeathCheckpoint(step, deaths);
            else RefreshEnemyUI();
        }
        if (deaths.Started) yield return DeathPresentationRunner.WaitForAll();
        deathPipelineRunning = false;
        if (!released) ReleaseEnemyPresentation(actionIndex);
        nextEnemyRuleResolveIndex++;
    }

    private IEnumerator PlayTurnCardExits(IReadOnlyList<MaterialModel> candidates, BattleResolutionStep step)
    {
        var remaining = new HashSet<MaterialModel>(step.After.Hand);
        remaining.UnionWith(step.After.PlayZone);
        var exits = new List<HandCardView>();
        foreach (MaterialModel card in candidates)
            if (card != null && !remaining.Contains(card) && FindView(card) is HandCardView view && view != null)
                exits.Add(view);

        bool returned = false;
        AnimateReturningViews(exits, new List<MaterialModel>(step.RemovedTemporaryCards), GetDiscardPileArea(),
            () => returned = true, forceReturnViews: true);
        RefreshStaticUI();
        while (!returned) yield return null;
        UpdateLayout();
    }

    private IEnumerator PlayCommittedCastFeedback(BattleResolutionStep step, RectTransform entryOrigin = null)
    {
        // Per-hit values keep AoE and repeated damage from jumping directly to the final HP.
        ApplyPresentationState(step.Before ?? step.After);
        if (step.CastResult != null && step.Before != null)
        {
            foreach (MagicDamageHitResult hit in step.CastResult.enemyDamageHits)
                if (hit?.target != null && !displayedMagicHits.ContainsKey(hit.target))
                {
                    BattleEnemySnapshot before = step.Before.FindEnemy(hit.target);
                    if (before != null) displayedMagicHits[hit.target] = new MagicDamageHitResult(hit.target, 0, 0, -1, before.Health, before.Shield);
                }
        }
        PlayStepCues(step);
        if (step.CastResult != null)
        {
            MagicCastResult result = step.CastResult;
            // An Arc hit can capture the generated arrows before the cast's final checkpoint.
            // Leave their views unbuilt during hit playback so they still enter from the magic slot.
            var deferredHandViews = step.CreatedHandCards.Count == 0 ? null : new HashSet<MaterialModel>(step.CreatedHandCards);
            yield return PlayMagicDamageFeedbackRoutine(result.enemyDamageHits, deferredHandViews);
            ApplyPresentationState(step.After, step.CreatedHandCards, entryOrigin);
            PlayMagicCastFeedback(result, false);
        }
        else ApplyPresentationState(step.After);
        displayedMagicHits.Clear();
        RefreshStaticUI(); RefreshEnemyUI();
    }

    private sealed class DeathPlaybackState
    {
        public bool Started;
        public Coroutine Visual;
    }

    // Presentation only: the shared iterator commits ResolveEnemyDeath between these checkpoints.
    private IEnumerator PlayCommittedDeathCheckpoint(BattleResolutionStep step, DeathPlaybackState playback)
    {
        if (step.Kind == BattleResolutionStepKind.DeathPending)
        {
            if (!playback.Started && debugBattleActive && AllEnemiesDead())
                GetUIManager().PvOnlyBossEndCinematic?.ShowCrashImpact();
            if (playback.Started && enemyDeathOverlap && enemyDeathStagger > 0f)
                yield return new WaitForSeconds(enemyDeathStagger);
            playback.Started = true;
            deathPipelineRunning = true;
            DeathPresentationRunner.StartInterval = Mathf.Max(0f, enemyDeathStagger);
            EnemyViewState state = FindEnemyViewState(step.DeathEntry.Enemy);
            playback.Visual = state != null ? DeathPresentationRunner.Start(PlayEnemyExplosion(state)) : null;
            yield break;
        }

        PlayPlayerDamageFeedbackIfNeeded(step.Before.PlayerHealth, step.Before.PlayerShield);
        RefreshStaticUI();
        List<EnemyDamageFeedbackItem> damage = BuildEnemyDamageFeedback(step.Before);
        Coroutine feedback = damage.Count > 0
            ? DeathPresentationRunner.Start(PlayEnemyDamageFeedbackSequence(damage)) : null;
        if (!enemyDeathOverlap)
        {
            if (playback.Visual != null) yield return playback.Visual;
            if (feedback != null) yield return feedback;
        }
    }

    private void PunchReadCard(MaterialModel card)
    {
        HandCardView view = FindView(card);
        if (view == null) return;
        view.RectTransform.DOPunchPosition(Vector3.up * materialCardPunchStrength,
            ResolveMotionDuration(materialCardPunchDuration), materialCardPunchVibrato, materialCardPunchElasticity, false)
            .SetTarget(view.RectTransform);
    }

    private List<EnemyDamageFeedbackItem> BuildEnemyDamageFeedback(BattleStateSnapshot before)
    {
        var snapshots = new List<EnemyFeedbackSnapshot>();
        if (before != null)
            foreach (BattleEnemySnapshot enemy in before.Enemies)
                snapshots.Add(new EnemyFeedbackSnapshot { model = enemy.Enemy, health = enemy.Health, shield = enemy.Shield });
        return BuildEnemyDamageFeedback(snapshots);
    }

    private IEnumerator PlayCommittedAfterReadActions(IReadOnlyList<BattleAfterReadActionResult> actions)
    {
        var consumed = new List<HandCardView>();
        var returned = new List<HandCardView>();
        foreach (BattleAfterReadActionResult action in actions)
        {
            HandCardView view = FindView(action.Card);
            if (!action.Applied || view == null) continue;
            if (action.Action == ArrowReadAfterReadAction.ReturnNextTurn) returned.Add(view);
            else consumed.Add(view);
        }
        RefreshStaticUI();
        RefreshMaterialListPanel();
        yield return AnimateRemovedReadViews(returned, deckPileArea != null ? deckPileArea : GetDiscardPileArea());
        yield return AnimateRemovedReadViews(consumed, GetConsumedPileArea());
    }
}
