using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>Views bind the same sessions used by scripts; animation never owns an effect or selection commit.</summary>
public partial class HandSystemUI
{
    private bool battleRewardTransactionActive;
    public int RewardMagicSlotCapacity => MagicSlotCapacity;
    // Includes automatic base gold before candidates are generated; never save a partial reward transaction.
    public bool HasUnpersistedChoiceSession => battleRewardTransactionActive || EventChoiceSession != null || BattleRewardSession != null;
    public void BeginBattleRewardPreparation() { battleRewardTransactionActive = true; }
    public RewardSession BattleRewardSession { get; private set; }
    public EventResolutionSession EventChoiceSession { get; private set; }
    public PendingChoice PendingNodeChoice => EventChoiceSession?.PendingChoice ?? BattleRewardSession?.PendingChoice;

    private RewardOfferGenerator CreateRewardOfferGenerator()
    {
        ChapterData chapter = runManager != null ? runManager.ActiveChapter : GetActiveChapter();
        LevelData level = runManager != null && runManager.CurrentLevel != null ? runManager.CurrentLevel : currentLevel;
        bool boss = currentChapterMapBossLevel || (runManager != null && runManager.CurrentBattle != null && runManager.CurrentBattle.CurrentLevelIsBoss);
        return new RewardOfferGenerator(playerState, level, chapter, boss, GameDataDatabase.GetDefaultEconomyConfig(),
            new RewardGenerationServices { NextRandomInt = NextRunRandomInt,
                MaterialModifierChoices = count => GetArrowModifierChoices(count), ReportInsufficientPool = Debug.LogError });
    }

    public RewardSession BeginBattleRewardSession()
    {
        if (BattleRewardSession != null) return BattleRewardSession;
        using (playerState.Rules.Enter())
            BattleRewardSession = new RewardSession(playerState, new BattleRewardChoices
            {
                GoldAmount = DifficultyUpgradeSystem.ModifyBattleRewardGoldChoiceCount(1) > 0 ? Mathf.Max(0, PendingBattleGoldChoiceReward) : 0,
                Magic = RollBattleRewardMagic(), Arrow = RollBattleRewardArrow()
            }, MagicSlotCapacity);
        return BattleRewardSession;
    }

    public RewardSession EnsureBattleRewardSession() => BattleRewardSession ?? BeginBattleRewardSession();
    public void ClearNodeChoiceSessions() { battleRewardTransactionActive = false; BattleRewardSession = null; EventChoiceSession = null; }

    public EventResolutionSession BeginEventRuleResolution(EventOptionData option, bool rest)
    {
        if (EventChoiceSession != null && !EventChoiceSession.IsComplete) return EventChoiceSession;
        EventChoiceSession = new EventResolutionSession(playerState, currentEvent, option, new EventRuleServices(playerState)
        {
            NextRandomInt = NextRunRandomInt, MagicSlotCapacity = MagicSlotCapacity,
            MagicChoices = count => GetRewardMagicChoices(count),
            MagicModifierChoices = count => GetMagicModifierChoices(count),
            MaterialModifierChoices = count => GetArrowModifierChoices(count),
            FindMaterialModifier = GetMaterialModifierDataById
        }, rest);
        return EventChoiceSession;
    }

    /// <summary>Script bridge; returns a refusal without bypassing validation. It does not wait for presentation.</summary>
    public ChoiceResult SubmitNodeChoice(ChoiceSubmission submission)
    {
        IChoiceSession session = EventChoiceSession != null && EventChoiceSession.PendingChoice != null
            ? (IChoiceSession)EventChoiceSession : BattleRewardSession;
        return session != null ? session.SubmitChoice(submission) : new ChoiceResult(ChoiceFailure.NoPendingChoice);
    }

    private IEnumerator ResolveNodeChoicesRoutine(bool rest)
    {
        busy = true; SetButtonsInteractable(false);
        int hpBefore = playerState.CurrentHealth, goldBefore = playerState.Gold;
        int magicBefore = playerState.MagicBook.Count, deckBefore = playerState.Deck.Count;
        ArrowReadSequence sequence = ArrowReadSystem.BuildSequence(playerState.PlayZone, playerState, battleManager);
        EventOptionData option = null;
        bool matched = currentEvent != null && currentEvent.TryGetMatchedOption(sequence.Tokens, out option);
        if (matched && !rest) TutorialManager?.OnEventOptionResolved();
        if (!matched && currentEvent != null) matched = currentEvent.TryGetExitOption(out option);
        if (!matched && !rest && currentEvent != null) matched = currentEvent.TryGetDefaultEndOption(out option);
        if (!rest) RunAnalyticsLedger.SetEventPlay(RunAnalyticsLedger.DescribeTokens(sequence.Tokens), refreshUsedThisTurn ? 1 : 0);
        RectTransform source = null;
        if (matched && eventPanel != null)
        {
            yield return eventPanel.PlayOptionChosen(option);
            source = eventPanel.MatchedOptionRect;
            foreach (ArrowReadToken token in sequence.Tokens)
            {
                HandCardView view = FindView(token.SourceCard);
                if (view != null && source != null) PlayMaterialFillParticle(view, source, token.DisplayMaterial);
            }
            yield return new WaitForSeconds(GetParticleArrivalWait());
        }
        if (currentEvent == null) yield break;
        EventResolutionSession session = BeginEventRuleResolution(option, rest);
        yield return PlayEventRuleResolution(session, source);
        if (session.PlayerDefeated || CheckPlayerDefeated()) yield break;
        if (!rest)
        {
            if (matched) EmitEventOptionResolved(option, hpBefore, goldBefore, magicBefore, deckBefore);
            else EmitEventNoMatch("no_match");
        }
        if (session.NodeAdvanced)
        {
            EventChoiceSession = null;
            if (eventPanel != null) yield return eventPanel.ShowCurrentNodeRoutine();
            SaveRunProgress();
            busy = false; SetButtonsInteractable(true);
        }
        else if (rest) FinishRestLevel();
        else FinishEventLevel();
    }

    private IEnumerator PlayEventRuleResolution(EventResolutionSession session, RectTransform source)
    {
        while (!session.IsComplete)
        {
            if (session.PendingChoice != null)
            {
                yield return PresentNodeChoice(session, source);
                continue;
            }
            // Keep view identities before the authoritative discard changes the model.
            List<HandCardView> beforeViews = new List<HandCardView>(cardViews);
            EventResolutionStep step = session.Advance();
            if (step == null) break;
            if (step.Kind == EventResolutionStepKind.Discard)
            {
                bool returned = false;
                var removed = step.RemovedTemporaryCards != null ? new List<MaterialModel>(step.RemovedTemporaryCards) : new List<MaterialModel>();
                AnimateReturningViews(beforeViews, removed, GetDiscardPileArea(), () => returned = true);
                while (!returned) yield return null;
                refreshUsedThisTurn = false;
                RebuildCards(animateFromCurrent: true);
            }
            else if (step.Kind == EventResolutionStepKind.Effect)
            {
                yield return PresentCommittedChoiceResult(step.Result, source);
                if (step.AcquiredMagics != null && step.AcquiredMagics.Count > 0)
                {
                    int remaining = step.AcquiredMagics.Count;
                    int resultingCount = playerState.MagicBook.Count;
                    MagicBookCurveLayout curve = magicBookArea != null ? magicBookArea.GetComponent<MagicBookCurveLayout>() : null;
                    curve?.ArrangeExistingSlotsForCount(resultingCount, AcquireMagicAnimationDuration);
                    foreach (MagicModel magic in step.AcquiredMagics)
                        StartCoroutine(PlayCommittedEventMagicPackMember(magic, source, resultingCount, () => remaining--));
                    while (remaining > 0) yield return null;
                    foreach (MagicModel magic in step.AcquiredMagics)
                        if (magic != null) EmitMagicAcquire(magic.Data, magic.SlotIndex, null);
                }
                CreateMagicViews(); RefreshMaterialListPanel(); RebuildCards(animateFromCurrent: true);
            }
            RefreshStaticUI(); SaveRunProgress();
        }
    }

    private IEnumerator PlayCommittedEventMagicPackMember(MagicModel magic, RectTransform source, int resultingCount, Action completed)
    {
        try
        {
            if (magic != null) yield return PlayMagicAcquireAnimation(magic.Data, magic.SlotIndex, source, resultingCount, false, false);
        }
        finally { completed?.Invoke(); }
    }

    /// <summary>Presentation-only receipt playback shared by reward and event panels.</summary>
    public IEnumerator PresentCommittedChoiceResult(ChoiceResult receipt, RectTransform source)
    {
        if (receipt == null || !receipt.Accepted) yield break;
        int gained = receipt.GoldAfter - receipt.GoldBefore;
        if (gained > 0)
            yield return PlayCommittedGoldGain(receipt.GoldBefore, receipt.GoldAfter, source);
        if (receipt.HealthAfter > receipt.HealthBefore) PlayRewardHealFeedback(receipt.HealthAfter - receipt.HealthBefore);
        else if (receipt.HealthAfter < receipt.HealthBefore) ShowPlayerFloatingText("-" + (receipt.HealthBefore - receipt.HealthAfter), FloatingTextType.Damage);
        if (receipt.AcquiredMagic != null)
        {
            TutorialManager?.OnRewardMagicSelected();
            yield return PlayMagicAcquireAnimation(receipt.AcquiredMagic.Data, receipt.MagicSlotIndex, source, receipt.ResultingMagicCount, true, true);
            EmitMagicAcquire(receipt.AcquiredMagic.Data, receipt.MagicSlotIndex, null);
            CreateMagicViews();
        }
        if (receipt.AddedMaterial != null)
            yield return PlayMaterialAcquireAnimation(receipt.AddedMaterial.material, source);
        RefreshMaterialListPanel(); RefreshStaticUI(); SaveRunProgress();
    }

    private IEnumerator PresentNodeChoice(EventResolutionSession session, RectTransform source)
    {
        PendingChoice choice = session.PendingChoice;
        if (choice.Kind == ChoiceKind.MagicReward)
        {
            RewardPanelUI reward = GetUIManager().RewardPanel;
            if (reward != null) reward.ShowSession(session, null);
        }
        else if (choice.Kind == ChoiceKind.MagicModifier)
        {
            var choices = new List<MagicModifierData>();
            foreach (ChoiceCandidate candidate in choice.Candidates) choices.Add(candidate.MagicModifier);
            MagicModifierSelectionPanelUI panel = GetUIManager().MagicModifierSelectionPanel;
            if (panel != null) panel.Show(choices, null, () =>
            {
                PendingChoice active = session.PendingChoice;
                if (active != null) session.SubmitChoice(ChoiceSubmission.CancelChoice(active.Id));
            });
        }
        else if (choice.Kind == ChoiceKind.MagicTarget)
        {
            // A script may submit the modifier stage without clicking its view; restore target-click binding.
            if (choice.Candidates.Count > 0) pendingMagicModifier = choice.Candidates[0].MagicModifier;
            var selected = new List<MagicModifierData>();
            if (pendingMagicModifier != null) selected.Add(pendingMagicModifier);
            MagicModifierSelectionPanelUI panel = GetUIManager().MagicModifierSelectionPanel;
            if (panel != null) panel.Show(selected, null, () => session.SubmitChoice(ChoiceSubmission.CancelChoice(choice.Id)));
        }
        else if (choice.Kind == ChoiceKind.MaterialModifier)
        {
            var choices = new List<MaterialModifierData>();
            foreach (ChoiceCandidate candidate in choice.Candidates) choices.Add(candidate.MaterialModifier);
            MagicModifierSelectionPanelUI panel = GetUIManager().MagicModifierSelectionPanel;
            if (panel != null) panel.ShowMaterialModifierChoices(choices, data =>
            {
                foreach (ChoiceCandidate candidate in choice.Candidates)
                    if (candidate.MaterialModifier == data) { session.SubmitChoice(new ChoiceSubmission(choice.Id, candidate.Id)); panel.Hide(); break; }
            }, null, () => session.SubmitChoice(ChoiceSubmission.CancelChoice(choice.Id)));
        }
        else if (choice.Kind == ChoiceKind.RemoveMaterial || choice.Kind == ChoiceKind.MaterialTarget || choice.Kind == ChoiceKind.LegacyMaterial)
        {
            MaterialListPanelUI panel = GetUIManager().MaterialSelectionPanel;
            if (panel != null)
            {
                Predicate<MaterialModel> eligible = card => FindMaterialCandidate(choice, card) != null;
                panel.BeginSelection(choice.MaxSelections, eligible, selected =>
                {
                    var ids = new List<string>();
                    foreach (MaterialModel card in selected)
                    { ChoiceCandidate candidate = FindMaterialCandidate(choice, card); if (candidate != null) ids.Add(candidate.Id); }
                    session.SubmitChoice(new ChoiceSubmission(choice.Id, ids.ToArray()));
                }, choice.CanCancel ? (Action)(() => session.SubmitChoice(ChoiceSubmission.CancelChoice(choice.Id))) : null);
            }
        }
        // No panel: remain answerable by SubmitNodeChoice; do not silently cancel/complete the rule.
        while (session.PendingChoice == choice) yield return null;
        if (choice.Kind == ChoiceKind.MagicReward)
        {
            RewardPanelUI reward = GetUIManager().RewardPanel;
            while (reward != null && reward.IsPresentationInProgress) yield return null;
            reward?.Hide();
        }
        GetUIManager().MaterialSelectionPanel?.EndSelectionMode();
        if (session.PendingChoice?.Kind != ChoiceKind.MagicTarget) pendingMagicModifier = null;
        pendingMaterialModifier = null;
        CreateMagicViews(); RefreshMaterialListPanel(); RebuildCards(animateFromCurrent: true); RefreshStaticUI(); SaveRunProgress();
    }

    private static ChoiceCandidate FindMaterialCandidate(PendingChoice choice, MaterialModel card)
    {
        foreach (ChoiceCandidate candidate in choice.Candidates) if (candidate.Material == card) return candidate;
        return null;
    }

    // Main file TryApplyPendingMagicModifier must call this before its legacy direct AddModifier path.
    private bool TrySubmitEventMagicTarget(int slotIndex, out bool applied)
    {
        applied = false;
        EventResolutionSession session = EventChoiceSession;
        PendingChoice choice = session?.PendingChoice;
        if (choice == null || (choice.Kind != ChoiceKind.MagicModifier && choice.Kind != ChoiceKind.MagicTarget)) return false;
        if (choice.Kind == ChoiceKind.MagicModifier)
        {
            foreach (ChoiceCandidate candidate in choice.Candidates)
                if (candidate.MagicModifier == pendingMagicModifier)
                { if (!session.SubmitChoice(new ChoiceSubmission(choice.Id, candidate.Id)).Accepted) return true; break; }
            choice = session.PendingChoice;
        }
        if (choice == null || choice.Kind != ChoiceKind.MagicTarget) return true;
        MagicModel magic = playerState.GetMagicAtSlot(slotIndex);
        foreach (ChoiceCandidate candidate in choice.Candidates)
            if (candidate.Magic == magic)
            {
                applied = session.SubmitChoice(new ChoiceSubmission(choice.Id, candidate.Id)).Accepted;
                if (applied)
                {
                    EmitUpgradeChoose("magic_modifier", magic.Id, candidate.MagicModifier?.id);
                    GetUIManager().MagicModifierSelectionPanel?.CompleteSelection();
                }
                break;
            }
        return true;
    }
}
