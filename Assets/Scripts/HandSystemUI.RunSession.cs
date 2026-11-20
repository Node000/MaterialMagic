/// <summary>
/// Presentation adapter only. The main HandSystemUI entry points call these commits before
/// clearing their local view fields, then consume the returned destination without repeating rules.
/// </summary>
public partial class HandSystemUI
{
    public RunSession RunSession => runManager != null ? runManager.Session : null;

    private void ConfigureRunNode(LevelData level, bool isBoss)
    {
        if (runManager == null || level == null) return;
        ConfigureRunMapProgress();
        runManager.Session.ConfigureNode(new RunNodePolicy
        {
            IsBoss = isBoss,
            CompleteRunOnBattleWin = isBoss && ShouldCompleteRunAfterCurrentBattle(),
            AdvanceProgress = !secondFloorRun
        });
    }

    private void ConfigureRunMapProgress()
    {
        if (runManager == null) return;
        ChapterData chapter = activeChapter ?? GetActiveChapter();
        runManager.Session.ConfigureMapProgress(GetActiveChapterLength(), chapter != null && chapter.numericId == TutorialManagerUI.TutorialChapterNumericId);
    }

    private RunTransition CommitRunBattleWin()
    {
        return runManager != null ? runManager.Session.BattleWon(ShouldCompleteRunAfterCurrentBattle()) : null;
    }

    private RunTransition CommitRunNodeCompletion(bool autoOpenBattleShop, bool debugLevel = false)
    {
        if (runManager == null) return null;
        if (debugLevel)
            runManager.Session.ConfigureNode(new RunNodePolicy { AdvanceProgress = false });
        return autoOpenBattleShop && !debugLevel
            ? runManager.Session.CompleteReward(GetLevelData(AutoShopLevelId))
            : runManager.Session.CompleteNode();
    }

    private RunTransition CommitRunResult(bool victory)
    {
        return runManager != null ? runManager.Session.EndRun(victory) : null;
    }
}
