using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StartMenuUI : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "SampleScene_PC";
    [SerializeField] private StartMenuButtonGroupUI buttonGroupUI;
    [SerializeField] private StartConfigSelectionUI startConfigSelectionUI;
    [SerializeField] private AscensionDetailPanelUI ascensionDetailPanelUI;
    [SerializeField] private SaveSlotSelectionPanelUI saveSlotSelectionPanelUI;
    [SerializeField] private StartTutorialPanelUI tutorialPanelUI;
    [SerializeField] private StartForumPanelUI forumPanelUI;
    [SerializeField] private RunHistoryPanelUI historyPanelUI;
    [SerializeField] private StartMagicCodexPanelUI codexPanelUI;
    [SerializeField] private Button tutorialButton;
    [SerializeField] private Button forumButton;
    [SerializeField] private Button historyButton;
    [SerializeField] private Button codexButton;
    [SerializeField] private Button changeSaveButton;
    [SerializeField] private StartSettingsPanelUI settingsPanelUI;
    [SerializeField] private StartExitConfirmPanelUI exitConfirmPanelUI;
    [SerializeField] private BouncingTitleUI bouncingTitleUI;
    [Header("配置选择过渡")]
    [SerializeField] private RectTransform menuRoot;
    [SerializeField] private RectTransform initialButtonsRoot;
    [SerializeField] private RectTransform configActionButtonsRoot;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button backButton;
    [SerializeField] private float configRootShiftDistance = 960f;
    [SerializeField] private float configTransitionDuration = 0.45f;
    [SerializeField] private AnimationCurve configTransitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField, Min(0f)] private float configPanelShowDelay;
    [SerializeField, Min(0f)] private float configRootResetDelay;

    private readonly List<RaycastResult> raycastResults = new List<RaycastResult>();
    private readonly List<GameObject> initialMenuObjects = new List<GameObject>();
    private Vector2 menuRootInitialPosition;
    private Tween configTransitionTween;
    private Tween configDelayTween;
    private PointerEventData pointerEventData;
    private PlayerStartConfigData selectedConfig;
    private bool selectingStartConfig;
    private bool startingTutorial;
    private bool confirmingExit;
    private bool confirmingAbandonRun;

    private void Awake()
    {
        ResolveReferences();
        CacheMenuPresentation();
        startConfigSelectionUI.Prewarm();
        codexPanelUI?.Prewarm();
        buttonGroupUI.StartClicked += HandleStartClicked;
        buttonGroupUI.ContinueClicked += ContinueSavedRun;
        buttonGroupUI.SettingsClicked += OpenSettings;
        buttonGroupUI.ExitClicked += ExitGame;
        startConfigSelectionUI.ConfigSelected += SelectConfig;
        startConfigSelectionUI.Closed += HideStartConfigSelection;
        if (tutorialButton != null)
            tutorialButton.onClick.AddListener(StartTutorial);
        if (forumButton != null)
            forumButton.onClick.AddListener(OpenForum);
        if (historyButton != null)
            historyButton.onClick.AddListener(OpenHistory);
        if (codexButton != null)
            codexButton.onClick.AddListener(OpenCodex);
        if (changeSaveButton != null)
            changeSaveButton.onClick.AddListener(OpenSaveSlotSelection);
        if (confirmButton != null)
            confirmButton.onClick.AddListener(ConfirmStartGame);
        if (backButton != null)
            backButton.onClick.AddListener(HideStartConfigSelection);
    }

    private void OnDestroy()
    {
        if (buttonGroupUI != null)
        {
            buttonGroupUI.StartClicked -= HandleStartClicked;
            buttonGroupUI.ContinueClicked -= ContinueSavedRun;
            buttonGroupUI.SettingsClicked -= OpenSettings;
            buttonGroupUI.ExitClicked -= ExitGame;
        }
        if (startConfigSelectionUI != null)
        {
            startConfigSelectionUI.ConfigSelected -= SelectConfig;
            startConfigSelectionUI.Closed -= HideStartConfigSelection;
        }
        if (tutorialButton != null)
            tutorialButton.onClick.RemoveListener(StartTutorial);
        if (forumButton != null)
            forumButton.onClick.RemoveListener(OpenForum);
        if (historyButton != null)
            historyButton.onClick.RemoveListener(OpenHistory);
        if (codexButton != null)
            codexButton.onClick.RemoveListener(OpenCodex);
        if (changeSaveButton != null)
            changeSaveButton.onClick.RemoveListener(OpenSaveSlotSelection);
        if (confirmButton != null)
            confirmButton.onClick.RemoveListener(ConfirmStartGame);
        if (backButton != null)
            backButton.onClick.RemoveListener(HideStartConfigSelection);
        configTransitionTween?.Kill(false);
        configDelayTween?.Kill(false);
    }

    private void CacheMenuPresentation()
    {
        if (menuRoot == null)
            menuRoot = transform as RectTransform;
        if (initialButtonsRoot == null && buttonGroupUI != null)
            initialButtonsRoot = buttonGroupUI.transform as RectTransform;
        if (configActionButtonsRoot == null)
            configActionButtonsRoot = transform.Find("StartConfigActionButtonGroup") as RectTransform;
        if (confirmButton == null)
            confirmButton = configActionButtonsRoot != null ? configActionButtonsRoot.Find("ConfirmButton")?.GetComponent<Button>() : transform.Find("MenuContentRoot/StartConfigPanel/ActionButtonGroup/ConfirmButton")?.GetComponent<Button>();
        if (backButton == null)
            backButton = configActionButtonsRoot != null ? configActionButtonsRoot.Find("CancelButton")?.GetComponent<Button>() : transform.Find("MenuContentRoot/StartConfigPanel/ActionButtonGroup/CancelButton")?.GetComponent<Button>();

        menuRootInitialPosition = menuRoot != null ? menuRoot.anchoredPosition : Vector2.zero;
        CacheInitialMenuObject(initialButtonsRoot != null ? initialButtonsRoot.gameObject : null);
        CacheInitialMenuObject(tutorialButton != null ? tutorialButton.gameObject : null);
        CacheInitialMenuObject(forumButton != null ? forumButton.gameObject : null);
        CacheInitialMenuObject(historyButton != null ? historyButton.gameObject : null);
        CacheInitialMenuObject(codexButton != null ? codexButton.gameObject : null);
        CacheInitialMenuObject(changeSaveButton != null ? changeSaveButton.gameObject : null);

        if (initialButtonsRoot != null)
        {
            Button[] buttons = initialButtonsRoot.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Image background = buttons[i].GetComponent<Image>();
                if (background != null)
                {
                    Color color = background.color;
                    color.a = 0f;
                    background.color = color;
                }

                JuicyMotion motion = buttons[i].GetComponent<JuicyMotion>();
                if (motion != null)
                {
                    motion.enabled = false;
                }
            }
        }

        SetActionButtonsVisible(false);
    }

    private void CacheInitialMenuObject(GameObject menuObject)
    {
        if (menuObject != null && !initialMenuObjects.Contains(menuObject))
            initialMenuObjects.Add(menuObject);
    }

    private void SetInitialMenuVisible(bool visible)
    {
        for (int i = 0; i < initialMenuObjects.Count; i++)
        {
            GameObject menuObject = initialMenuObjects[i];
            if (menuObject != null)
                menuObject.SetActive(visible);
        }

        if (visible)
            ConfigureTutorialButton();
    }

    private void SetActionButtonsVisible(bool visible)
    {
        if (configActionButtonsRoot != null)
            configActionButtonsRoot.gameObject.SetActive(visible);

        if (confirmButton != null)
            confirmButton.gameObject.SetActive(visible);
        if (backButton != null)
            backButton.gameObject.SetActive(visible);
    }

    private void MoveMenuRoot(bool moveRight, System.Action onComplete = null)
    {
        configTransitionTween?.Kill(false);
        Vector2 targetPosition = moveRight
            ? menuRootInitialPosition + Vector2.right * configRootShiftDistance
            : menuRootInitialPosition;
        if (menuRoot == null)
        {
            onComplete?.Invoke();
            return;
        }

        configTransitionTween = menuRoot.DOAnchorPos(targetPosition, configTransitionDuration)
            .SetEase(configTransitionCurve)
            .SetUpdate(true)
            .SetTarget(this)
            .OnComplete(() => onComplete?.Invoke());
    }

    private void RunConfigDelay(float delay, System.Action onComplete)
    {
        configDelayTween?.Kill(false);
        if (delay <= 0f)
        {
            onComplete?.Invoke();
            return;
        }

        configDelayTween = DOVirtual.DelayedCall(delay, () => onComplete?.Invoke(), true)
            .SetTarget(this);
    }

    private void Update()
    {
        if ((confirmingExit || confirmingAbandonRun || settingsPanelUI.IsShowing || (ascensionDetailPanelUI != null && ascensionDetailPanelUI.IsShowing) || (tutorialPanelUI != null && tutorialPanelUI.IsShowing) || (forumPanelUI != null && forumPanelUI.IsShowing) || (historyPanelUI != null && historyPanelUI.IsShowing) || (codexPanelUI != null && codexPanelUI.IsShowing) || (saveSlotSelectionPanelUI != null && saveSlotSelectionPanelUI.gameObject.activeSelf)) && Input.GetMouseButtonDown(0) && IsOutsideAllPanelsClick())
            HideAllPanels();
    }

    private void ResolveReferences()
    {
        if (buttonGroupUI == null)
            buttonGroupUI = GetComponentInChildren<StartMenuButtonGroupUI>(true);
        if (startConfigSelectionUI == null)
            startConfigSelectionUI = GetComponentInChildren<StartConfigSelectionUI>(true);
        if (ascensionDetailPanelUI == null)
            ascensionDetailPanelUI = GetComponentInChildren<AscensionDetailPanelUI>(true);
        if (saveSlotSelectionPanelUI == null)
            saveSlotSelectionPanelUI = GetComponentInChildren<SaveSlotSelectionPanelUI>(true);
        if (tutorialPanelUI == null)
            tutorialPanelUI = GetComponentInChildren<StartTutorialPanelUI>(true);
        if (forumPanelUI == null)
            forumPanelUI = GetComponentInChildren<StartForumPanelUI>(true);
        if (historyPanelUI == null)
            historyPanelUI = GetComponentInChildren<RunHistoryPanelUI>(true);
        if (codexPanelUI == null)
            codexPanelUI = GetComponentInChildren<StartMagicCodexPanelUI>(true);
        if (tutorialButton == null)
            tutorialButton = UIManager.FindChildComponent<Button>(transform, "MenuContentRoot/ButtonGroup/TutorialButton");
        if (forumButton == null)
            forumButton = UIManager.FindChildComponent<Button>(transform, "ForumButton");
        if (historyButton == null)
            historyButton = UIManager.FindChildComponent<Button>(transform, "HistoryButton");
        if (codexButton == null)
            codexButton = UIManager.FindChildComponent<Button>(transform, "CodexButton");
        if (changeSaveButton == null)
            changeSaveButton = UIManager.FindChildComponent<Button>(transform, "ChangeSaveButton");
        if (settingsPanelUI == null)
            settingsPanelUI = GetComponentInChildren<StartSettingsPanelUI>(true);
        if (exitConfirmPanelUI == null)
            exitConfirmPanelUI = GetComponentInChildren<StartExitConfirmPanelUI>(true);
        if (bouncingTitleUI == null)
            bouncingTitleUI = GetComponentInChildren<BouncingTitleUI>(true);
        buttonGroupUI.RefreshContinueButton(RunSaveSystem.HasCurrentRun());
        ConfigureTutorialButton();
    }

    private void ContinueSavedRun()
    {
        if (!RunSaveSystem.HasCurrentRun())
            return;

        RunSaveData saveData = RunSaveSystem.LoadCurrentRun();
        string sceneName = saveData != null && !string.IsNullOrWhiteSpace(saveData.sceneName) ? saveData.sceneName : gameSceneName;
        PlayerState.ContinueSavedRun = true;
        PlayerState.GameSceneEntryRequested = true;
        if (SceneTransitionManager.Instance != null)
        {
            if (Application.isMobilePlatform)
                SceneTransitionManager.Instance.LoadGameSceneWithTransition(buttonGroupUI.ContinueButtonObject);
            else
                SceneTransitionManager.Instance.LoadSceneWithTransition(sceneName, buttonGroupUI.ContinueButtonObject);
        }
        else
        {
            SceneManager.LoadScene(Application.isMobilePlatform ? "SampleScene_PE" : sceneName);
        }
    }

    private void OpenSaveSlotSelection()
    {
        HideStartConfigSelection();
        HideAscensionDetail();
        HideExitConfirm();
        HideAbandonRunConfirm();
        HideTutorial();
        HideForum();
        HideHistory();
        HideCodex();
        settingsPanelUI.Hide();
        saveSlotSelectionPanelUI.Show(SelectSaveSlot);
    }

    private void SelectSaveSlot(int slotIndex)
    {
        RunSaveSystem.SelectSlot(slotIndex);
        HideAbandonRunConfirm();
        buttonGroupUI.RefreshContinueButton(RunSaveSystem.HasCurrentRun());
        ConfigureTutorialButton();
        codexPanelUI?.RefreshIfShowing();
    }

    private void HandleStartClicked()
    {
        if (!selectingStartConfig && RunSaveSystem.HasCurrentRun())
        {
            if (!confirmingAbandonRun)
            {
                ShowAbandonRunConfirm();
                return;
            }

            AbandonSavedRun();
            return;
        }

        if (!selectingStartConfig)
            ShowStartConfigSelection();
    }

    private void ConfirmStartGame()
    {
        if (!selectingStartConfig || startConfigSelectionUI.IsSwitchingConfig)
            return;

        if (selectedConfig == null)
            selectedConfig = startConfigSelectionUI.SelectedConfig;
        if (selectedConfig == null)
        {
            startConfigSelectionUI.EnsureConfigWindows();
            selectedConfig = startConfigSelectionUI.SelectedConfig;
        }
        if (selectedConfig == null)
            return;

        PlayerState.SelectedStartConfigId = selectedConfig.id;
        PlayerState.ContinueSavedRun = false;
        PlayerState.GameSceneEntryRequested = true;
        if (startingTutorial)
            RunSaveSystem.BeginNewTutorialRun();
        else
            RunSaveSystem.BeginNewRun();

        if (SceneTransitionManager.Instance != null)
            SceneTransitionManager.Instance.LoadGameSceneWithTransition();
        else
            SceneManager.LoadScene(gameSceneName);
    }

    private void ShowStartConfigSelection(bool tutorial = false)
    {
        selectingStartConfig = true;
        startingTutorial = tutorial;
        selectedConfig = null;
        SetActionButtonsVisible(false);
        SetInitialMenuVisible(false);
        bouncingTitleUI?.SetVisible(false);
        HideExitConfirm();
        HideAbandonRunConfirm();
        HideTutorial();
        HideForum();
        HideHistory();
        HideCodex();
        HideAscensionDetail();
        settingsPanelUI.Hide();
        saveSlotSelectionPanelUI.Hide();
        MoveMenuRoot(true, () => RunConfigDelay(configPanelShowDelay, ShowConfigPanel));
    }

    private void ShowConfigPanel()
    {
        if (!selectingStartConfig)
            return;

        if (startingTutorial)
            startConfigSelectionUI.ShowOnly("balanced", () => SetActionButtonsVisible(selectingStartConfig));
        else
            startConfigSelectionUI.Show(() => SetActionButtonsVisible(selectingStartConfig));
    }

    private void HideStartConfigSelection()
    {
        if (!selectingStartConfig)
            return;

        selectingStartConfig = false;
        startingTutorial = false;
        selectedConfig = null;
        SetActionButtonsVisible(false);
        HideAscensionDetail();
        startConfigSelectionUI.Hide(() => RunConfigDelay(configRootResetDelay, ReturnToInitialMenu));
    }

    private void ReturnToInitialMenu()
    {
        MoveMenuRoot(false, () =>
        {
            bouncingTitleUI?.SetVisible(true);
            SetInitialMenuVisible(true);
        });
    }

    private void SelectConfig(PlayerStartConfigData config)
    {
        selectedConfig = config;
    }

    private void StartTutorial()
    {
        if (!selectingStartConfig)
            ShowStartConfigSelection(true);
    }

    private void ConfigureTutorialButton()
    {
        if (tutorialButton == null)
            return;

        tutorialButton.gameObject.SetActive(true);
        LocalizedTMPText localizedText = tutorialButton.GetComponentInChildren<LocalizedTMPText>(true);
        if (localizedText != null)
            localizedText.SetKey("ui.start_menu.tutorial_button", "教程");

    }

    private void OpenSettings()
    {
        HideStartConfigSelection();
        HideAscensionDetail();
        HideExitConfirm();
        HideAbandonRunConfirm();
        HideTutorial();
        HideForum();
        HideHistory();
        HideCodex();
        saveSlotSelectionPanelUI.Hide();
        settingsPanelUI.Show();
    }

    private void HideTutorial()
    {
        if (tutorialPanelUI != null)
            tutorialPanelUI.Hide();
    }

    private void OpenForum()
    {
        if (forumPanelUI == null)
            return;

        HideStartConfigSelection();
        HideAscensionDetail();
        HideExitConfirm();
        HideAbandonRunConfirm();
        HideTutorial();
        HideHistory();
        HideCodex();
        saveSlotSelectionPanelUI.Hide();
        settingsPanelUI.Hide();
        forumPanelUI.Show();
    }

    private void HideForum()
    {
        if (forumPanelUI != null)
            forumPanelUI.Hide();
    }

    private void OpenHistory()
    {
        if (historyPanelUI == null)
            return;

        HideStartConfigSelection();
        HideAscensionDetail();
        HideExitConfirm();
        HideAbandonRunConfirm();
        HideTutorial();
        HideForum();
        HideCodex();
        saveSlotSelectionPanelUI.Hide();
        settingsPanelUI.Hide();
        historyPanelUI.Show();
    }

    private void HideHistory()
    {
        if (historyPanelUI != null)
            historyPanelUI.Hide();
    }

    private void OpenCodex()
    {
        if (codexPanelUI == null)
            return;

        HideStartConfigSelection();
        HideAscensionDetail();
        HideExitConfirm();
        HideAbandonRunConfirm();
        HideTutorial();
        HideForum();
        HideHistory();
        saveSlotSelectionPanelUI.Hide();
        settingsPanelUI.Hide();
        codexPanelUI.Show();
    }

    private void HideCodex()
    {
        if (codexPanelUI != null)
            codexPanelUI.Hide();
    }

    private void HideAscensionDetail()
    {
        if (ascensionDetailPanelUI != null)
            ascensionDetailPanelUI.Hide();
    }

    private void ExitGame()
    {
        if (!confirmingExit)
        {
            HideStartConfigSelection();
            HideAbandonRunConfirm();
            saveSlotSelectionPanelUI.Hide();
            settingsPanelUI.Hide();
            HideTutorial();
            HideForum();
            HideHistory();
            HideCodex();
            HideAscensionDetail();
            ShowExitConfirm();
            return;
        }

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void ShowExitConfirm()
    {
        HideAbandonRunConfirm();
        confirmingExit = true;
        buttonGroupUI.SetExitConfirmMode(true);
        exitConfirmPanelUI.Show();
    }

    private void ShowAbandonRunConfirm()
    {
        HideStartConfigSelection();
        HideAscensionDetail();
        HideExitConfirm();
        HideTutorial();
        HideForum();
        HideHistory();
        HideCodex();
        saveSlotSelectionPanelUI.Hide();
        settingsPanelUI.Hide();
        confirmingAbandonRun = true;
        buttonGroupUI.SetStartAbandonConfirmMode(true);
        exitConfirmPanelUI.ShowLocalized("ui.start_menu.abandon_run_prompt", "是否放弃本局游戏？");
    }

    private void AbandonSavedRun()
    {
        RunSaveSystem.RecordCurrentRunAbandonedAndClearCurrentRun();
        confirmingAbandonRun = false;
        buttonGroupUI.SetStartAbandonConfirmMode(false);
        exitConfirmPanelUI.Hide();
        buttonGroupUI.RefreshContinueButton(RunSaveSystem.HasCurrentRun());
        ConfigureTutorialButton();
    }

    private void HideAbandonRunConfirm()
    {
        if (!confirmingAbandonRun)
            return;

        confirmingAbandonRun = false;
        buttonGroupUI.SetStartAbandonConfirmMode(false);
        if (!confirmingExit)
            exitConfirmPanelUI.Hide();
    }

    private void HideExitConfirm()
    {
        if (!confirmingExit && !exitConfirmPanelUI.IsShowing)
            return;

        confirmingExit = false;
        buttonGroupUI.SetExitConfirmMode(false);
        exitConfirmPanelUI.Hide();
    }

    private void HideAllPanels()
    {
        HideStartConfigSelection();
        saveSlotSelectionPanelUI.Hide();
        settingsPanelUI.Hide();
        HideTutorial();
        HideForum();
        HideHistory();
        HideCodex();
        HideAscensionDetail();
        HideAbandonRunConfirm();
        HideExitConfirm();

    }

    private bool IsOutsideAllPanelsClick()
    {
        if (EventSystem.current == null)
            return true;

        if (pointerEventData == null)
            pointerEventData = new PointerEventData(EventSystem.current);
        pointerEventData.position = Input.mousePosition;
        raycastResults.Clear();
        EventSystem.current.RaycastAll(pointerEventData, raycastResults);
        for (int i = 0; i < raycastResults.Count; i++)
        {
            Transform hit = raycastResults[i].gameObject.transform;
            if (buttonGroupUI.Contains(hit) ||
                (tutorialButton != null && hit.IsChildOf(tutorialButton.transform)) ||
                (forumButton != null && hit.IsChildOf(forumButton.transform)) ||
                (historyButton != null && hit.IsChildOf(historyButton.transform)) ||
                (codexButton != null && hit.IsChildOf(codexButton.transform)) ||
                (changeSaveButton != null && hit.IsChildOf(changeSaveButton.transform)) ||
                (configActionButtonsRoot != null && hit.IsChildOf(configActionButtonsRoot)) ||
                startConfigSelectionUI.Contains(hit) ||
                (ascensionDetailPanelUI != null && ascensionDetailPanelUI.Contains(hit)) ||
                (saveSlotSelectionPanelUI != null && saveSlotSelectionPanelUI.Contains(hit)) ||
                (tutorialPanelUI != null && tutorialPanelUI.Contains(hit)) ||
                (forumPanelUI != null && forumPanelUI.Contains(hit)) ||
                (historyPanelUI != null && historyPanelUI.Contains(hit)) ||
                (codexPanelUI != null && codexPanelUI.Contains(hit)) ||
                exitConfirmPanelUI.Contains(hit) ||
                settingsPanelUI.Contains(hit))
                return false;
        }
        return true;
    }
}
