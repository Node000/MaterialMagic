using System;
using System.Collections.Generic;

/// <summary>The shared tutorial rules used by both production presentation and scripted commands.</summary>
public sealed class TutorialPolicy
{
    public const int TutorialChapterNumericId = 100;
    public TutorialState State { get; }
    public event Action Changed;
    public bool ShouldKillTutorialEnemyAfterPlayerTurn => State.TutorialBattleRunning && State.BattleTurnIndex >= 2;
    public bool AllowsRefresh => StepAllowsAction(TutorialStep.BattleRefresh);
    public bool AllowsEndTurn => StepAllowsAction(TutorialStep.BattlePlay);
    public bool AllowsUndo => StepAllowsAction(TutorialStep.None);

    public TutorialPolicy(TutorialState state = null) { State = state ?? new TutorialState(); }

    public void Initialize(bool isTutorialRun)
    {
        State.IsTutorialRun = isTutorialRun;
        State.MainTutorialRunning = isTutorialRun;
        State.TutorialBattleRunning = false;
        State.BattleInputUnlocked = false;
        State.BattleTurnIndex = 0;
        State.IsCompleted = false;
        State.MapTutorialShown = State.ShopTutorialShown = State.EventTutorialShown = false;
        Hide();
    }

    public bool StepAllowsAction(TutorialStep step)
    {
        if (State.WaitingForContinue) return false;
        return State.CurrentStep == TutorialStep.None || State.BattleInputUnlocked || State.CurrentStep == step;
    }

    public void BeginBattle()
    {
        State.TutorialBattleRunning = true;
        State.BattleTurnIndex = 0;
        State.BattleInputUnlocked = false;
        Hide();
    }

    public void EndBattle()
    {
        State.TutorialBattleRunning = false;
        State.BattleInputUnlocked = false;
        State.BattleTurnIndex = 0;
        switch (State.CurrentStep)
        {
            case TutorialStep.BattleInfo:
            case TutorialStep.BattleEnemyInfo:
            case TutorialStep.BattleArrowBase:
            case TutorialStep.BattleMagicBook:
            case TutorialStep.BattlePlayLimit:
            case TutorialStep.BattlePlay:
            case TutorialStep.BattleRefresh: Hide(); break;
        }
    }

    public bool TryApplyFixedTurnHand(PlayerState player)
    {
        if (!State.TutorialBattleRunning || player == null) return false;
        using (player.Rules.Enter()) return ApplyFixedTurnHand(player);
    }

    private bool ApplyFixedTurnHand(PlayerState player)
    {
        State.BattleTurnIndex++;
        State.BattleInputUnlocked = false;
        MaterialEnum[] materials;
        if (State.BattleTurnIndex == 1) materials = new[] { MaterialEnum.Fire, MaterialEnum.Fire };
        else if (State.BattleTurnIndex == 2) materials = new[] { MaterialEnum.Earth, MaterialEnum.Earth, MaterialEnum.Earth };
        else return false;
        var removed = new List<MaterialModel>();
        player.ReturnHandCardsToDiscardPile(new List<MaterialModel>(player.Hand), removed);
        player.ReturnPlayZoneCardsToDiscardPile(removed);
        player.DrawSpecificMaterialsToHand(materials, true);
        Show(State.BattleTurnIndex == 1 ? TutorialStep.BattleInfo : TutorialStep.BattleRefresh, State.BattleTurnIndex == 1);
        return true;
    }

    public bool CanMoveCardToPlay(MaterialModel card, IReadOnlyList<MaterialModel> playZone)
    {
        if (!State.TutorialBattleRunning || State.BattleInputUnlocked) return true;
        return !State.WaitingForContinue && card != null &&
            (State.CurrentStep == TutorialStep.BattlePlay || State.CurrentStep == TutorialStep.BattleRefresh);
    }

    public bool CanMovePlayCardToHand(MaterialModel card) => CanMoveCardToPlay(card, null);
    public bool CanReorderCards => !State.TutorialBattleRunning || State.BattleInputUnlocked ||
        (!State.WaitingForContinue && (State.CurrentStep == TutorialStep.BattlePlay || State.CurrentStep == TutorialStep.BattleRefresh));

    public bool CanReplacePlayZone(int count)
    {
        if (!State.TutorialBattleRunning || State.BattleInputUnlocked) return true;
        return !State.WaitingForContinue && State.CurrentStep == TutorialStep.BattleRefresh && count > 0;
    }

    public bool TryGetForcedRefreshMaterialsForPlayZone(int count, List<MaterialEnum> materials)
    {
        if (materials == null) return false;
        materials.Clear();
        if (!State.TutorialBattleRunning || State.CurrentStep != TutorialStep.BattleRefresh || count <= 0) return false;
        for (int i = 0; i < count; i++) materials.Add(i % 3 == 2 ? MaterialEnum.Water : MaterialEnum.Fire);
        return true;
    }

    public bool CanEndTurn(IReadOnlyList<MaterialModel> playZone)
    {
        return !State.TutorialBattleRunning || State.BattleInputUnlocked ||
            (!State.WaitingForContinue && State.CurrentStep == TutorialStep.BattlePlay);
    }

    public bool CanToggleFocus(EnemyModel enemy) => true;
    public void OnRefreshCompleted()
    {
        if (!State.TutorialBattleRunning || State.CurrentStep != TutorialStep.BattleRefresh) return;
        State.BattleInputUnlocked = true;
        Hide();
    }
    public void OnBattleEndTurnStarted()
    {
        if (State.TutorialBattleRunning && State.CurrentStep == TutorialStep.BattlePlay) Hide();
    }
    public void OnLevelSelectShown(int nodeIndex)
    {
        if (!State.MainTutorialRunning || State.MapTutorialShown || nodeIndex != 0) return;
        State.MapTutorialShown = true;
        Show(TutorialStep.MapPanel, true);
    }
    public void OnRewardPanelShown()
    {
        if (State.MainTutorialRunning && !State.IsCompleted) Show(TutorialStep.RewardClaim, true);
    }
    public void OnShopPanelShown()
    {
        if (!State.IsTutorialRun || State.ShopTutorialShown) return;
        State.ShopTutorialShown = true;
        Show(TutorialStep.ShopBuyHint, true);
    }
    public void OnEventOptionsShown()
    {
        if (State.IsTutorialRun && !State.EventTutorialShown) Show(TutorialStep.EventOptions, true);
    }
    public void OnEventOptionResolved()
    {
        if (State.CurrentStep != TutorialStep.EventOptions) return;
        State.EventTutorialShown = true;
        Hide();
    }
    public void Complete()
    {
        if (!State.IsTutorialRun) return;
        State.IsCompleted = true;
        State.MainTutorialRunning = State.TutorialBattleRunning = false;
        Hide();
    }

    /// <summary>Explicit scripted continuation. Paragraph count defaults to one without presentation.</summary>
    public bool Continue()
    {
        if (!State.WaitingForContinue) return false;
        if (State.ParagraphIndex + 1 < State.ParagraphCount)
        {
            State.ParagraphIndex++;
            NotifyChanged();
            return true;
        }
        switch (State.CurrentStep)
        {
            case TutorialStep.BattleInfo: Show(TutorialStep.BattleEnemyInfo, true); break;
            case TutorialStep.BattleEnemyInfo: Show(TutorialStep.BattleArrowBase, true); break;
            case TutorialStep.BattleArrowBase: Show(TutorialStep.BattleMagicBook, true); break;
            case TutorialStep.BattleMagicBook: Show(TutorialStep.BattlePlayLimit, true); break;
            case TutorialStep.BattlePlayLimit: Show(TutorialStep.BattlePlay, false); break;
            case TutorialStep.MapPanel: Show(TutorialStep.MapMovement, true); break;
            case TutorialStep.MapMovement: Show(TutorialStep.MapStepLimit, true); break;
            default: Hide(); break;
        }
        return true;
    }

    public void Show(TutorialStep step, bool continueAfterLastParagraph)
    {
        State.CurrentStep = step;
        State.StepRevision++;
        State.ParagraphIndex = 0;
        State.ParagraphCount = 1;
        State.ContinueAfterLastParagraph = continueAfterLastParagraph;
        NotifyChanged();
    }
    private void NotifyChanged()
    {
        Action notify = () => Changed?.Invoke();
        if (!RuleExecutionScope.DeferFeedback(notify)) notify();
    }

    public void Hide() => Show(TutorialStep.None, false);

    /// <summary>Presentation supplies localized paragraph count, never decides the next rule step.</summary>
    public void ConfigureParagraphCount(int count)
    {
        State.ParagraphCount = Math.Max(1, count);
        State.ParagraphIndex = Math.Min(State.ParagraphIndex, State.ParagraphCount - 1);
    }
}
