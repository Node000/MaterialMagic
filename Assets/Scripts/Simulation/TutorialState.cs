public enum TutorialStep
{
    None,
    BattleInfo,
    BattleArrowBase,
    BattleMagicBook,
    BattlePlayLimit,
    BattleEnemyInfo,
    BattlePlay,
    BattleRefresh,
    MapPanel,
    MapMovement,
    MapStepLimit,
    RewardClaim,
    ShopBuyHint,
    EventOptions,
    Completed
}

/// <summary>Authoritative tutorial progress. No scene objects or input devices are required.</summary>
public sealed class TutorialState
{
    public TutorialStep CurrentStep { get; internal set; }
    public int StepRevision { get; internal set; }
    public bool IsTutorialRun { get; internal set; }
    public bool MainTutorialRunning { get; internal set; }
    public bool TutorialBattleRunning { get; internal set; }
    public bool BattleInputUnlocked { get; internal set; }
    public int BattleTurnIndex { get; internal set; }
    public bool IsCompleted { get; internal set; }
    public bool MapTutorialShown { get; internal set; }
    public bool ShopTutorialShown { get; internal set; }
    public bool EventTutorialShown { get; internal set; }
    public int ParagraphIndex { get; internal set; }
    public int ParagraphCount { get; internal set; } = 1;
    public bool ContinueAfterLastParagraph { get; internal set; }
    public bool WaitingForContinue => CurrentStep != TutorialStep.None &&
        (ParagraphIndex + 1 < ParagraphCount || ContinueAfterLastParagraph);
    public bool IsMapTutorialBlockingInput => CurrentStep == TutorialStep.MapPanel ||
        CurrentStep == TutorialStep.MapMovement || CurrentStep == TutorialStep.MapStepLimit;
}
