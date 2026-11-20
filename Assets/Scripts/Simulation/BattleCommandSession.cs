using System;
using System.Collections.Generic;

/// <summary>
/// Authoritative decision boundary. Busy/dragging/animation are presentation gates and never enter this class.
/// Uses existing PlayerState primitives, retaining clamp, join/discard hooks and refresh payment order.
/// </summary>
public sealed class BattleCommandSession
{
    public PlayerState Player { get; }
    public BattleManager Battle { get; }
    public TutorialPolicy Tutorial { get; }
    public bool RefreshUsedThisTurn { get; private set; }
    public bool TurnResolving { get; private set; }
    public int RemainingRefreshChances => (!RefreshUsedThisTurn && Player.RefreshLimitReductionThisTurn <= 0 ? 1 : 0)
        + Player.RemainingPermanentRefreshChancesThisTurn + Player.ExtraRefreshChancesThisTurn;

    public BattleCommandSession(PlayerState player, BattleManager battle = null, TutorialPolicy tutorial = null)
    {
        Player = player ?? throw new ArgumentNullException(nameof(player));
        if (battle != null && !ReferenceEquals(battle.PlayerState, player))
            throw new ArgumentException("Battle and command session must own the same player.", nameof(battle));
        Battle = battle;
        Tutorial = tutorial;
    }

    /// <summary>Called once after the host has applied turn-start rules. Does not duplicate those rules.</summary>
    public void BeginPlayerTurn(bool resetRefresh = true)
    {
        TurnResolving = false;
        if (resetRefresh) RefreshUsedThisTurn = false;
    }
    public void RestoreRefreshUsedThisTurn(bool used) { RefreshUsedThisTurn = used; }
    public void RestoreDecisionState(bool refreshUsed, bool turnResolving)
    {
        RefreshUsedThisTurn = refreshUsed;
        TurnResolving = turnResolving;
    }
    public void CompleteTurn() { TurnResolving = false; }

    /// <summary>Pure legality query: never builds arrow reads, picks a target or draws a card.</summary>
    public BattleCommandRejection Query(BattleCommand command)
    {
        using (Player.Rules.Enter()) return QueryCore(command);
    }

    private BattleCommandRejection QueryCore(BattleCommand command)
    {
        if (command == null) return BattleCommandRejection.InvalidCommand;
        if (TurnResolving) return BattleCommandRejection.TurnResolving;
        if (Battle != null && Battle.CurrentPhase != BattlePhase.PlayerTurn) return BattleCommandRejection.WrongPhase;
        if (Battle != null && Player.CurrentHealth <= 0) return BattleCommandRejection.PlayerDefeated;
        MaterialModel card = command.Card;
        switch (command.Kind)
        {
            case BattleCommandKind.Play:
                if (card == null) return BattleCommandRejection.MissingCard;
                if (!Player.Hand.Contains(card)) return BattleCommandRejection.CardNotInHand;
                if ((!command.AllowDisabled || Battle != null) && Player.IsMaterialDisabled(card)) return BattleCommandRejection.DisabledCard;
                if (Battle != null && !Player.CanPlayCardFromHand(card)) return BattleCommandRejection.PlayLimitReached;
                return Tutorial != null && !Tutorial.CanMoveCardToPlay(card, Player.PlayZone)
                    ? BattleCommandRejection.TutorialBlocked : BattleCommandRejection.None;
            case BattleCommandKind.Withdraw:
                if (card == null) return BattleCommandRejection.MissingCard;
                if (!Player.PlayZone.Contains(card)) return BattleCommandRejection.CardNotInPlayZone;
                return Tutorial != null && !Tutorial.CanMovePlayCardToHand(card)
                    ? BattleCommandRejection.TutorialBlocked : BattleCommandRejection.None;
            case BattleCommandKind.ReorderHand:
            case BattleCommandKind.ReorderPlay:
                if (card == null) return BattleCommandRejection.MissingCard;
                List<MaterialModel> cards = command.Kind == BattleCommandKind.ReorderHand ? Player.Hand : Player.PlayZone;
                int index = cards.IndexOf(card);
                if (index < 0) return command.Kind == BattleCommandKind.ReorderHand
                    ? BattleCommandRejection.CardNotInHand : BattleCommandRejection.CardNotInPlayZone;
                if (Tutorial != null && !Tutorial.CanReorderCards) return BattleCommandRejection.TutorialBlocked;
                return index == Clamp(command.TargetIndex, 0, cards.Count - 1)
                    ? BattleCommandRejection.NoChange : BattleCommandRejection.None;
            case BattleCommandKind.Refresh:
                if (Player.PlayZone.Count == 0) return BattleCommandRejection.EmptyPlayZone;
                if (!command.IgnoreRefreshLimit && RemainingRefreshChances <= 0) return BattleCommandRejection.NoRefreshChances;
                return Tutorial != null && (!Tutorial.AllowsRefresh || !Tutorial.CanReplacePlayZone(Player.PlayZone.Count))
                    ? BattleCommandRejection.TutorialBlocked : BattleCommandRejection.None;
            case BattleCommandKind.ToggleFocus:
                if (Battle == null || command.TargetEnemy == null || command.TargetEnemy.IsDead || !ContainsEnemy(command.TargetEnemy))
                    return BattleCommandRejection.InvalidTarget;
                return Tutorial != null && !Tutorial.CanToggleFocus(command.TargetEnemy)
                    ? BattleCommandRejection.TutorialBlocked : BattleCommandRejection.None;
            case BattleCommandKind.ReorderMagic:
                int occupiedSpan = 0;
                for (int i = 0; i < Player.MagicBook.Count; i++)
                    if (Player.MagicBook[i] != null) occupiedSpan = Math.Max(occupiedSpan, Player.MagicBook[i].SlotIndex + 1);
                if (command.SlotCount < occupiedSpan || command.SlotCount <= 0 || command.SourceIndex < 0 || command.SourceIndex >= command.SlotCount ||
                    command.TargetIndex < 0 || command.TargetIndex >= command.SlotCount || Player.GetMagicAtSlot(command.SourceIndex) == null)
                    return BattleCommandRejection.InvalidTarget;
                return command.SourceIndex == command.TargetIndex ? BattleCommandRejection.NoChange : BattleCommandRejection.None;
            case BattleCommandKind.EndTurn:
                return Tutorial != null && (!Tutorial.AllowsEndTurn || !Tutorial.CanEndTurn(Player.PlayZone))
                    ? BattleCommandRejection.TutorialBlocked : BattleCommandRejection.None;
            default: return BattleCommandRejection.InvalidCommand;
        }
    }

    public BattleCommandResult Execute(BattleCommand command)
    {
        using (Player.Rules.Enter()) return ExecuteCore(command);
    }

    private BattleCommandResult ExecuteCore(BattleCommand command)
    {
        BattleCommandRejection rejection = QueryCore(command);
        if (rejection != BattleCommandRejection.None) return new BattleCommandResult(this, command, rejection);
        var context = new MaterialModifierContext { PlayerState = Player, BattleManager = Battle };
        MaterialModifierContext previous = MaterialModifierModel.CurrentContext;
        var removed = new List<MaterialModel>();
        int drawn = 0, returned = 0;
        bool changed = true;
        MaterialModifierModel.CurrentContext = context;
        try
        {
            switch (command.Kind)
            {
                case BattleCommandKind.Play:
                    changed = Player.TryMoveHandCardToPlay(command.Card, command.TargetIndex,
                        command.AllowDisabled && Battle == null, Battle != null);
                    break;
                case BattleCommandKind.Withdraw:
                    changed = Player.TryMovePlayCardToHand(command.Card, command.TargetIndex);
                    break;
                case BattleCommandKind.ReorderHand: changed = Player.ReorderHandCard(command.Card, command.TargetIndex); break;
                case BattleCommandKind.ReorderPlay: changed = Player.ReorderPlayCard(command.Card, command.TargetIndex); break;
                case BattleCommandKind.Refresh:
                    var forced = new List<MaterialEnum>();
                    bool useForced = Tutorial != null && Tutorial.TryGetForcedRefreshMaterialsForPlayZone(Player.PlayZone.Count, forced);
                    PlayerState.RefreshHandResult result = Player.RefreshPlayZoneCardsToHand(removed, Battle, useForced ? forced : null);
                    drawn = result.DrawnCount;
                    returned = result.ReturnedCount;
                    changed = drawn > 0 || returned > 0 || removed.Count > 0;
                    if (changed)
                    {
                        if (!command.IgnoreRefreshLimit)
                        {
                            if (!RefreshUsedThisTurn && Player.RefreshLimitReductionThisTurn <= 0) RefreshUsedThisTurn = true;
                            else Player.UseAdditionalRefreshChance();
                        }
                        Tutorial?.OnRefreshCompleted();
                    }
                    break;
                case BattleCommandKind.ToggleFocus:
                    if (ReferenceEquals(Battle.FocusTarget, command.TargetEnemy)) Battle.ClearFocusTarget();
                    else Battle.SetFocusTarget(command.TargetEnemy);
                    break;
                case BattleCommandKind.ReorderMagic:
                    Player.RearrangeMagicSlotsByVisualOrder(command.SourceIndex, command.TargetIndex, command.SlotCount, command.PreserveEmptySlots);
                    break;
                case BattleCommandKind.EndTurn:
                    TurnResolving = true;
                    Tutorial?.OnBattleEndTurnStarted();
                    break;
            }
        }
        finally { MaterialModifierModel.CurrentContext = previous; }
        return new BattleCommandResult(this, command, changed ? BattleCommandRejection.None : BattleCommandRejection.PrimitiveRejected,
            context.EnemyBuffChanged, drawn, returned, removed);
    }

    /// <summary>All currently legal decision payloads, including insertion positions. No hidden random choice.</summary>
    public IReadOnlyList<BattleCommand> GetLegalCommands()
    {
        var commands = new List<BattleCommand>();
        for (int i = 0; i < Player.Hand.Count; i++)
        {
            for (int index = 0; index <= Player.PlayZone.Count; index++) AddIfLegal(commands, BattleCommand.Play(Player.Hand[i], index));
            for (int index = 0; index < Player.Hand.Count; index++) AddIfLegal(commands, BattleCommand.ReorderHand(Player.Hand[i], index));
        }
        for (int i = 0; i < Player.PlayZone.Count; i++)
        {
            for (int index = 0; index <= Player.Hand.Count; index++) AddIfLegal(commands, BattleCommand.Withdraw(Player.PlayZone[i], index));
            for (int index = 0; index < Player.PlayZone.Count; index++) AddIfLegal(commands, BattleCommand.ReorderPlay(Player.PlayZone[i], index));
        }
        int slotCount = 0;
        for (int i = 0; i < Player.MagicBook.Count; i++)
            if (Player.MagicBook[i] != null) slotCount = Math.Max(slotCount, Player.MagicBook[i].SlotIndex + 1);
        for (int from = 0; from < slotCount; from++)
            for (int to = 0; to < slotCount; to++) AddIfLegal(commands, BattleCommand.ReorderMagic(from, to, slotCount));
        AddIfLegal(commands, BattleCommand.Refresh());
        if (Battle != null)
            for (int i = 0; i < Battle.Enemies.Count; i++) AddIfLegal(commands, BattleCommand.ToggleFocus(Battle.Enemies[i]));
        AddIfLegal(commands, BattleCommand.EndTurn());
        return commands.AsReadOnly();
    }

    private void AddIfLegal(List<BattleCommand> commands, BattleCommand command)
    {
        if (Query(command) == BattleCommandRejection.None) commands.Add(command);
    }
    private bool ContainsEnemy(EnemyModel target)
    {
        for (int i = 0; i < Battle.Enemies.Count; i++) if (ReferenceEquals(Battle.Enemies[i], target)) return true;
        return false;
    }
    private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(value, max));
}
