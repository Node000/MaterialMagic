using System;
using UnityEngine;

/// <summary>
/// Presentation adapter only. The main HandSystemUI entry points call these commits before
/// clearing their local view fields, then consume the returned destination without repeating rules.
/// </summary>
public partial class HandSystemUI
{
    public RunSession RunSession => runManager != null ? runManager.Session : null;

    [SerializeField] private bool externalSecondFloorBattleHost;
    private Action<bool> secondFloorEncounterFinished;
    private Action secondFloorMapAction;

    public bool IsExternalSecondFloorBattleHost => externalSecondFloorBattleHost;

    /// <summary>Use for both exploration touch targets and keyboard shortcuts.</summary>
    public bool IsSecondFloorExplorationBlocked => externalSecondFloorBattleHost &&
        (runManager == null || runManager.State != RunFlowState.MapSelection ||
        (uiManager != null && (PanelIsVisible(uiManager.SettingsPanel) || PanelIsVisible(uiManager.MaterialListPanel) ||
            PanelIsVisible(uiManager.MaterialSelectionPanel))));

    private static bool PanelIsVisible(Component panel) => panel != null && panel.gameObject.activeInHierarchy;

    /// <summary>Optional native Map button action. A sandbox never opens the first-floor grid.</summary>
    public void SetSecondFloorMapAction(Action showMap) => secondFloorMapAction = showMap;

    internal void ShowSecondFloorMapFromTopBar()
    {
        if (externalSecondFloorBattleHost && !IsSecondFloorExplorationBlocked)
            secondFloorMapAction?.Invoke();
    }

    /// <summary>Starts the exact level supplied by exploration, leaving movement and progress to that controller.</summary>
    /// <param name="onEncounterFinished">True after the original reward is claimed or skipped; false once the original defeat panel is shown.</param>
    public bool TryStartSecondFloorBattle(LevelData level, Action<bool> onEncounterFinished, bool isBoss = false,
        Func<bool> isPresentationReady = null)
    {
        if (!externalSecondFloorBattleHost || !isActiveAndEnabled || runEnded || currentLevel != null ||
            IsSecondFloorExplorationBlocked || !IsBattleLevel(level) ||
            onEncounterFinished == null || !HasSecondFloorEncounterEnemies(level))
            return false;

        // The host's RunManager has an empty legacy grid. The external map owns any real progress,
        // including bosses; the boss presentation flag must not invoke RunSession's grid boss logic.
        RunTransition transition = runManager.Session.SelectNode(level, new RunNodePolicy { AdvanceProgress = false });
        if (!transition.Accepted)
            return false;

        ClearNodeChoiceSessions();
        secondFloorEncounterFinished = onEncounterFinished;
        currentChapterMapBossLevel = isBoss;
        currentLevel = level;
        busy = true;
        HideMagicSellPopup();
        SetButtonsInteractable(false);
        StartBattleLevel(level, isPresentationReady: isPresentationReady);
        return true;
    }

    private static bool HasSecondFloorEncounterEnemies(LevelData level)
    {
        // Match the real spawner's precedence. Every random group must be able to start a battle.
        if (level.randomEnemyGroups != null && level.randomEnemyGroups.Length > 0)
        {
            foreach (LevelEnemyGroupData group in level.randomEnemyGroups)
                if (!HasKnownEnemyPlacement(group?.enemies)) return false;
            return true;
        }
        if (level.enemies != null && level.enemies.Length > 0)
            return HasKnownEnemyPlacement(level.enemies);
        if (level.enemyIds != null)
            foreach (int id in level.enemyIds)
                if (GameDataDatabase.TryGetEnemyData(id, out _)) return true;
        return false;
    }

    private static bool HasKnownEnemyPlacement(LevelEnemyData[] placements)
    {
        if (placements != null)
            foreach (LevelEnemyData placement in placements)
                if (placement != null && GameDataDatabase.TryGetEnemyData(placement.enemyId, out _)) return true;
        return false;
    }

    private static PlayerStatus CreateSecondFloorSandboxPlayer()
    {
        const string configId = "balanced";
        GameDataDatabase.TryGetPlayerStartConfigData(configId, out PlayerStartConfigData config);
        var services = new PlayerStateServices(
            (health, shield) => AudioManager.Instance?.PlayDamageResultSfx(health, shield),
            () => AudioManager.Instance?.PlaySfx(GameSfxId.GetCoin));
        var player = new PlayerStatus(config?.maxHealth ?? 50, config?.gold ?? 0, services,
            new RunDifficultySaveData(), profilePolicy: RuleProfilePolicy.AllUnlocked) { DrawOrderVersion = 1 };
        PlayerState.ApplyStartConfig(player, config);
        return player;
    }

    private void PrimeSecondFloorExploration()
    {
        UIManager manager = GetUIManager();
        manager.HideLevelSelect();
        manager.HideMapPanel();
        manager.HideChapterGridPanel();
        ClearMapDirectionCardsFromHandArea();
        SetPlayLimitDisplayShown(false, true);
        busy = true;
        SetButtonsInteractable(false);
    }

    private void NotifySecondFloorEncounterFinished(bool victory)
    {
        Action<bool> callback = secondFloorEncounterFinished;
        secondFloorEncounterFinished = null;
        callback?.Invoke(victory);
    }

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
