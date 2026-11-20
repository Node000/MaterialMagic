using System;

public partial class HandSystemUI
{
    private BattleCommandSession commandSession;
    private bool unboundRefreshUsedThisTurn;

    // Replaces the old field without changing any existing save/analytics/reset call sites.
    // A null player during Awake/transition never creates a session or throws.
    private bool refreshUsedThisTurn
    {
        get => GetCommandSession()?.RefreshUsedThisTurn ?? unboundRefreshUsedThisTurn;
        set
        {
            unboundRefreshUsedThisTurn = value;
            BattleCommandSession session = GetCommandSession();
            if (session == null) return;
            if (!value) session.BeginPlayerTurn();
            else session.RestoreRefreshUsedThisTurn(true);
        }
    }

    private BattleCommandSession GetCommandSession()
    {
        if (playerState == null) return null;
        bool battleContext = currentEvent == null &&
            (currentLevel == null || (currentLevel.levelType != LevelType.Rest && currentLevel.levelType != LevelType.Reward)) &&
            (debugBattleActive || IsBattleLevel(currentLevel) || (runManager != null && runManager.State == RunFlowState.Battle));
        BattleManager commandBattle = battleContext ? battleManager : null;
        TutorialPolicy policy = TutorialManager?.RulePolicy;
        if (commandSession == null || !ReferenceEquals(commandSession.Player, playerState) ||
            !ReferenceEquals(commandSession.Battle, commandBattle) || !ReferenceEquals(commandSession.Tutorial, policy))
        {
            bool refreshUsed = commandSession != null && ReferenceEquals(commandSession.Player, playerState)
                ? commandSession.RefreshUsedThisTurn : unboundRefreshUsedThisTurn;
            commandSession = new BattleCommandSession(playerState, commandBattle, policy);
            commandSession.RestoreRefreshUsedThisTurn(refreshUsed);
        }
        return commandSession;
    }

    private bool TryMoveHandCardCommand(MaterialModel card, int index, bool allowDisabled)
    {
        BattleCommandSession session = GetCommandSession();
        if (session == null) return false;
        BattleCommandResult result = session.Execute(BattleCommand.Play(card, index, allowDisabled));
        if (!result.Accepted)
        {
            if (result.Rejection == BattleCommandRejection.DisabledCard) ShowDisabledCardPopup();
            else if (result.Rejection == BattleCommandRejection.PlayLimitReached) ShowPlayLimitPopup();
            return false;
        }
        ClearPlayedCardFeedback(card);
        TutorialManager?.OnBattleCardsPlayed(playerState.PlayZone);
        TutorialManager?.OnBattleReadyToEndTurn(playerState.PlayZone);
        if (result.EnemyBuffChanged) RefreshEnemyUI();
        return true;
    }

    private bool TryWithdrawCardCommand(MaterialModel card, int index)
    {
        BattleCommandSession session = GetCommandSession();
        if (session == null || !session.Execute(BattleCommand.Withdraw(card, index)).Accepted) return false;
        TutorialManager?.OnBattleCardCanceled(playerState.PlayZone);
        RefreshEndTurnButtonText();
        RefreshPlayerAnimationState();
        return true;
    }

    private bool TryReorderCardCommand(MaterialModel card, bool inPlayZone, int index)
    {
        BattleCommandSession session = GetCommandSession();
        return session != null && session.Execute(inPlayZone ? BattleCommand.ReorderPlay(card, index)
            : BattleCommand.ReorderHand(card, index)).Accepted;
    }

    private BattleCommandResult ExecuteRefreshCommand(bool ignoreOncePerTurn)
    {
        return GetCommandSession()?.Execute(BattleCommand.Refresh(ignoreOncePerTurn));
    }

    private bool TryToggleFocusCommand(EnemyModel enemy)
    {
        BattleCommandSession session = GetCommandSession();
        if (session == null || !session.Execute(BattleCommand.ToggleFocus(enemy)).Accepted) return false;
        RefreshEnemyUI();
        TutorialManager?.OnFocusTargetChanged(battleManager.FocusTarget);
        return true;
    }

    private bool TryReorderMagicCommand(int from, int to, int visualCount)
    {
        BattleCommandSession session = GetCommandSession();
        return session != null && session.Execute(BattleCommand.ReorderMagic(from, to, visualCount, UsesFixedMagicSlots)).Accepted;
    }

    private bool TryBeginBattleEndTurnCommand()
    {
        BattleCommandSession session = GetCommandSession();
        return session != null && session.Execute(BattleCommand.EndTurn()).Accepted;
    }
}
