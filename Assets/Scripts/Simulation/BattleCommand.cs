using System;
using System.Collections.Generic;

public enum BattleCommandKind { Play, Withdraw, ReorderHand, ReorderPlay, Refresh, ToggleFocus, EndTurn, ReorderMagic }
public enum BattleCommandRejection
{
    None, MissingPlayer, WrongPhase, TurnResolving, PlayerDefeated, MissingCard,
    CardNotInHand, CardNotInPlayZone, DisabledCard, PlayLimitReached, TutorialBlocked,
    EmptyPlayZone, NoRefreshChances, InvalidTarget, NoChange, InvalidCommand, PrimitiveRejected
}

/// <summary>Immutable command payload. Model references identify entities, not snapshots of their values.</summary>
public sealed class BattleCommand
{
    public BattleCommandKind Kind { get; }
    public MaterialModel Card { get; }
    public EnemyModel TargetEnemy { get; }
    public int TargetIndex { get; }
    public int SourceIndex { get; }
    public int SlotCount { get; }
    public bool PreserveEmptySlots { get; }
    public bool AllowDisabled { get; }
    public bool IgnoreRefreshLimit { get; }
    private BattleCommand(BattleCommandKind kind, MaterialModel card = null, int index = 0,
        EnemyModel enemy = null, bool allowDisabled = false, bool ignoreRefreshLimit = false, int sourceIndex = 0, int slotCount = 0, bool preserveEmptySlots = false)
    {
        Kind = kind; Card = card; TargetIndex = index; TargetEnemy = enemy;
        SourceIndex = sourceIndex; SlotCount = slotCount; PreserveEmptySlots = preserveEmptySlots;
        AllowDisabled = allowDisabled; IgnoreRefreshLimit = ignoreRefreshLimit;
    }
    public static BattleCommand Play(MaterialModel card, int index, bool allowDisabled = false) =>
        new BattleCommand(BattleCommandKind.Play, card, index, allowDisabled: allowDisabled);
    public static BattleCommand Withdraw(MaterialModel card, int index) => new BattleCommand(BattleCommandKind.Withdraw, card, index);
    public static BattleCommand ReorderHand(MaterialModel card, int index) => new BattleCommand(BattleCommandKind.ReorderHand, card, index);
    public static BattleCommand ReorderPlay(MaterialModel card, int index) => new BattleCommand(BattleCommandKind.ReorderPlay, card, index);
    public static BattleCommand Refresh(bool ignoreLimit = false) => new BattleCommand(BattleCommandKind.Refresh, ignoreRefreshLimit: ignoreLimit);
    public static BattleCommand ToggleFocus(EnemyModel enemy) => new BattleCommand(BattleCommandKind.ToggleFocus, enemy: enemy);
    public static BattleCommand EndTurn() => new BattleCommand(BattleCommandKind.EndTurn);
    public static BattleCommand ReorderMagic(int from, int to, int slotCount, bool preserveEmptySlots = false) =>
        new BattleCommand(BattleCommandKind.ReorderMagic, index: to, sourceIndex: from, slotCount: slotCount, preserveEmptySlots: preserveEmptySlots);
}

/// <summary>Committed scalar values and read-only identity sequences. Later mutations never alter these values.</summary>
public sealed class BattleCommandResult
{
    public bool Accepted => Rejection == BattleCommandRejection.None;
    public BattleCommandRejection Rejection { get; }
    public BattleCommand Command { get; }
    public bool EnemyBuffChanged { get; }
    public int DrawnCount { get; }
    public int ReturnedCount { get; }
    public IReadOnlyList<MaterialModel> RemovedTemporaryCards { get; }
    public IReadOnlyList<MaterialModel> Hand { get; }
    public IReadOnlyList<MaterialModel> PlayZone { get; }
    public bool RefreshUsedThisTurn { get; }
    public int RemainingRefreshChances { get; }
    public EnemyModel FocusTarget { get; }
    public int PlayedCardCountThisTurn { get; }

    internal BattleCommandResult(BattleCommandSession session, BattleCommand command,
        BattleCommandRejection rejection, bool enemyBuffChanged = false, int drawnCount = 0,
        int returnedCount = 0, IList<MaterialModel> removed = null)
    {
        Command = command; Rejection = rejection; EnemyBuffChanged = enemyBuffChanged;
        DrawnCount = drawnCount; ReturnedCount = returnedCount;
        RemovedTemporaryCards = new List<MaterialModel>(removed ?? Array.Empty<MaterialModel>()).AsReadOnly();
        Hand = new List<MaterialModel>(session.Player.Hand).AsReadOnly();
        PlayZone = new List<MaterialModel>(session.Player.PlayZone).AsReadOnly();
        RefreshUsedThisTurn = session.RefreshUsedThisTurn;
        RemainingRefreshChances = session.RemainingRefreshChances;
        FocusTarget = session.Battle?.FocusTarget;
        PlayedCardCountThisTurn = session.Player.PlayedCardCountThisTurn;
    }
}
