using System;

/// <summary>Node-flow policy supplied by the map/chapter adapter, never inferred from a view.</summary>
public sealed class RunNodePolicy
{
    public bool IsBoss { get; set; }
    public bool CompleteRunOnBattleWin { get; set; }
    public bool AdvanceProgress { get; set; } = true;

    internal RunNodePolicy Copy() => new RunNodePolicy
    {
        IsBoss = IsBoss,
        CompleteRunOnBattleWin = CompleteRunOnBattleWin,
        AdvanceProgress = AdvanceProgress
    };
}

public enum RunTransitionRejection
{
    None,
    MissingLevel,
    RunEnded,
    NodeAlreadyActive,
    NoActiveNode,
    WrongPhase
}

/// <summary>Committed flow result. UI plays/opens the destination; it must not commit it again.</summary>
public sealed class RunTransition
{
    public bool Accepted => Rejection == RunTransitionRejection.None;
    public RunTransitionRejection Rejection { get; }
    public RunFlowState From { get; }
    public RunFlowState To { get; }
    public LevelData CompletedLevel { get; }
    public LevelData NextLevel { get; }
    public bool AdvancedNode { get; }
    public bool IsTerminal => To == RunFlowState.Victory || To == RunFlowState.Defeat;

    internal RunTransition(RunFlowState from, RunFlowState to, RunTransitionRejection rejection = RunTransitionRejection.None,
        LevelData completedLevel = null, LevelData nextLevel = null, bool advancedNode = false)
    {
        From = from;
        To = to;
        Rejection = rejection;
        CompletedLevel = completedLevel;
        NextLevel = nextLevel;
        AdvancedNode = advancedNode;
    }
}

/// <summary>
/// The production run's flow owner. Rules, rewards and shop transactions are separate sessions;
/// this session commits their node transitions and returns presentation-independent destinations.
/// The automatic post-battle shop belongs to the original node and never advances it twice.
/// </summary>
public sealed class RunSession
{
    private readonly RunManager owner;
    private RunNodePolicy policy = new RunNodePolicy();
    private LevelData nodeLevel;
    private bool nodeCompleted;
    private int chapterLength = int.MaxValue;
    private bool tutorialMap;

    public RunFlowState State { get; private set; } = RunFlowState.MapSelection;
    public LevelData CurrentLevel { get; private set; }
    public LevelData NodeLevel => nodeLevel;
    public bool IsBossNode => policy.IsBoss;
    public bool IsAutomaticShop { get; private set; }
    public bool IsTerminal => State == RunFlowState.Victory || State == RunFlowState.Defeat;

    public bool RequiresBossMapActivation => State == RunFlowState.MapSelection && RunLevelRules.ShouldActivateBossMap(owner, chapterLength, tutorialMap);

    public void ConfigureMapProgress(int effectiveChapterLength, bool isTutorial)
    {
        chapterLength = Math.Max(1, effectiveChapterLength);
        tutorialMap = isTutorial;
    }

    public bool ActivatePendingBossMap()
    {
        if (!RequiresBossMapActivation) return false;
        owner.ActivateBossMap();
        return true;
    }

    internal RunSession(RunManager owner)
    {
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    public RunTransition SelectNode(LevelData level, RunNodePolicy nodePolicy = null)
        => SelectNodeCore(level, nodePolicy, restoring: false);

    internal RunTransition RestoreNode(LevelData level) => SelectNodeCore(level, null, restoring: true);

    private RunTransition SelectNodeCore(LevelData level, RunNodePolicy nodePolicy, bool restoring)
    {
        RunFlowState from = State;
        if (IsTerminal) return Reject(from, RunTransitionRejection.RunEnded);
        if (level == null) return Reject(from, RunTransitionRejection.MissingLevel);
        if (CurrentLevel != null && !ReferenceEquals(CurrentLevel, level))
            return Reject(from, RunTransitionRejection.NodeAlreadyActive);
        if (CurrentLevel != null)
            return new RunTransition(from, State, nextLevel: CurrentLevel);

        CurrentLevel = nodeLevel = level;
        nodeCompleted = false;
        IsAutomaticShop = false;
        policy = (nodePolicy ?? InferPolicy()).Copy();
        State = RunFlowState.BeforeNode;
        owner.ConsumeCurrentMapCellLevel();
        owner.RecordSelectedNode(level);
        if (!restoring && owner.PlayerStatus is PlayerStatus player)
        {
            using (player.Rules.Enter())
                foreach (MagicModel magic in player.MagicBook.ToArray())
                    magic?.OnRoomEntered(player);
        }
        return new RunTransition(from, State, nextLevel: level);
    }

    /// <summary>Apply chapter/tutorial facts before BeginLevel; does not consume RNG or start UI.</summary>
    public void ConfigureNode(RunNodePolicy nodePolicy)
    {
        if (nodePolicy != null && !IsTerminal)
            policy = nodePolicy.Copy();
    }

    public RunTransition BeginLevel(LevelData level)
    {
        RunFlowState from = State;
        if (IsTerminal) return Reject(from, RunTransitionRejection.RunEnded);
        if (level == null) return Reject(from, RunTransitionRejection.MissingLevel);
        if (CurrentLevel == null)
        {
            RunTransition selected = SelectNode(level);
            if (!selected.Accepted) return selected;
        }
        else if (!ReferenceEquals(CurrentLevel, level))
            return Reject(from, RunTransitionRejection.NodeAlreadyActive);
        if (State != RunFlowState.BeforeNode && State != StateForLevel(level))
            return Reject(from, RunTransitionRejection.WrongPhase);

        State = StateForLevel(level);
        return new RunTransition(from, State, nextLevel: level);
    }

    public RunTransition BattleWon(bool completesRun = false)
    {
        RunFlowState from = State;
        if (IsTerminal) return Reject(from, RunTransitionRejection.RunEnded);
        if (State != RunFlowState.Battle) return Reject(from, RunTransitionRejection.WrongPhase);
        if (completesRun || policy.CompleteRunOnBattleWin)
        {
            if (policy.IsBoss) owner.CompleteCurrentBossCell();
            PublishNodeCompletion();
            State = RunFlowState.Victory;
            return new RunTransition(from, State, completedLevel: nodeLevel);
        }
        State = RunFlowState.Reward;
        return new RunTransition(from, State, nextLevel: CurrentLevel);
    }

    /// <summary>Called only after the reward session has committed a choice or an explicit skip.</summary>
    public RunTransition CompleteReward(LevelData automaticShopLevel)
    {
        RunFlowState from = State;
        if (IsTerminal) return Reject(from, RunTransitionRejection.RunEnded);
        if (State != RunFlowState.Reward) return Reject(from, RunTransitionRejection.WrongPhase);
        if (automaticShopLevel == null)
            return CompleteNode(); // Missing optional shop must not strand a completed reward.
        if (automaticShopLevel.levelType != LevelType.Shop)
            return Reject(from, RunTransitionRejection.WrongPhase);
        CurrentLevel = automaticShopLevel;
        IsAutomaticShop = true;
        State = RunFlowState.Shop;
        owner.SetBattle(null);
        return new RunTransition(from, State, nextLevel: automaticShopLevel);
    }

    public RunTransition CompleteNode()
    {
        RunFlowState from = State;
        if (IsTerminal) return Reject(from, RunTransitionRejection.RunEnded);
        if (CurrentLevel == null || nodeCompleted) return Reject(from, RunTransitionRejection.NoActiveNode);
        if (State == RunFlowState.BeforeNode || State == RunFlowState.Battle)
            return Reject(from, RunTransitionRejection.WrongPhase);

        LevelData completed = nodeLevel;
        bool victory = false;
        RunMapGridModel grid = owner.MapGrid;
        bool hasGrid = grid != null && grid.CellCount > 0;
        if (policy.IsBoss && hasGrid)
        {
            bool endCell = owner.CompleteCurrentBossCell();
            victory = !grid.HasFixedBossPath || endCell;
        }
        bool advance = !victory && policy.AdvanceProgress;
        if (advance)
        {
            owner.SetCurrentMapNodeIndex(owner.CurrentMapNodeIndex + 1);
            if (!hasGrid && owner.MapNodes != null && owner.MapNodes.Count > 0)
                victory = owner.CurrentMapNodeIndex >= owner.MapNodes.Count;
        }
        CurrentLevel = nodeLevel = null;
        IsAutomaticShop = false;
        owner.SetBattle(null);
        State = victory ? RunFlowState.Victory : RunFlowState.MapSelection;
        PublishNodeCompletion(completed);
        return new RunTransition(from, State, completedLevel: completed, advancedNode: advance);
    }

    public RunTransition EndRun(bool victory)
    {
        RunFlowState from = State;
        if (IsTerminal)
            return Reject(from, RunTransitionRejection.RunEnded);
        if (victory) PublishNodeCompletion();
        State = victory ? RunFlowState.Victory : RunFlowState.Defeat;
        return new RunTransition(from, State, completedLevel: victory ? nodeLevel : null);
    }

    // Compatibility for legacy adapters which still perform advancement separately.
    internal void ClearCurrentLevel()
    {
        if (IsTerminal) return;
        LevelData completed = nodeLevel;
        CurrentLevel = nodeLevel = null;
        IsAutomaticShop = false;
        owner.SetBattle(null);
        State = RunFlowState.MapSelection;
        PublishNodeCompletion(completed);
    }

    internal void AdvanceMapNode()
    {
        if (IsTerminal) return;
        owner.SetCurrentMapNodeIndex(owner.CurrentMapNodeIndex + 1);
        CurrentLevel = nodeLevel = null;
        IsAutomaticShop = false;
        owner.SetBattle(null);
        State = owner.MapNodes != null && owner.MapNodes.Count > 0 && owner.CurrentMapNodeIndex >= owner.MapNodes.Count
            ? RunFlowState.Victory : RunFlowState.MapSelection;
    }

    private void PublishNodeCompletion(LevelData completed = null)
    {
        LevelData level = completed ?? nodeLevel;
        if (nodeCompleted || level == null) return;
        nodeCompleted = true;
        owner.PublishNodeCompleted(level);
    }

    private RunNodePolicy InferPolicy()
    {
        RunMapCellModel cell = owner.MapGrid != null ? owner.MapGrid.GetCurrentCell() : null;
        return new RunNodePolicy { IsBoss = cell != null && (cell.isBoss || owner.MapGrid.bossMapActive) };
    }

    private RunTransition Reject(RunFlowState from, RunTransitionRejection reason) => new RunTransition(from, State, reason);

    internal static RunFlowState StateForLevel(LevelData level)
    {
        if (level == null) return RunFlowState.MapSelection;
        switch (level.levelType)
        {
            case LevelType.Battle:
            case LevelType.Elite: return RunFlowState.Battle;
            case LevelType.Event:
            case LevelType.RemoveMaterial:
            case LevelType.AddMaterial: return RunFlowState.Event;
            case LevelType.Rest: return RunFlowState.Rest;
            case LevelType.Shop: return RunFlowState.Shop;
            case LevelType.Reward: return RunFlowState.Reward;
            default: return RunFlowState.BeforeNode;
        }
    }
}
