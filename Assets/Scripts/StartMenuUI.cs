using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StartMenuUI : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "SampleScene";
    [SerializeField] private StartMenuButtonGroupUI buttonGroupUI;
    [SerializeField] private StartConfigSelectionUI startConfigSelectionUI;
    [SerializeField] private SaveSlotSelectionPanelUI saveSlotSelectionPanelUI;
    [SerializeField] private StartTutorialPanelUI tutorialPanelUI;
    [SerializeField] private StartForumPanelUI forumPanelUI;
    [SerializeField] private Button tutorialButton;
    [SerializeField] private Button skipTutorialButton;
    [SerializeField] private Button forumButton;
    [SerializeField] private Button changeSaveButton;
    [SerializeField] private StartSettingsPanelUI settingsPanelUI;
    [SerializeField] private StartExitConfirmPanelUI exitConfirmPanelUI;

    private readonly List<RaycastResult> raycastResults = new List<RaycastResult>();
    private PointerEventData pointerEventData;
    private PlayerStartConfigData selectedConfig;
    private bool selectingStartConfig;
    private bool startingTutorial;
    private bool confirmingExit;

    private void Awake()
    {
        ResolveReferences();
        startConfigSelectionUI.Prewarm();
        buttonGroupUI.StartClicked += HandleStartClicked;
        buttonGroupUI.ContinueClicked += ContinueSavedRun;
        buttonGroupUI.SettingsClicked += OpenSettings;
        buttonGroupUI.ExitClicked += ExitGame;
        startConfigSelectionUI.ConfigSelected += SelectConfig;
        if (tutorialButton != null)
            tutorialButton.onClick.AddListener(OpenTutorial);
        if (skipTutorialButton != null)
            skipTutorialButton.onClick.AddListener(SkipTutorial);
        if (forumButton != null)
            forumButton.onClick.AddListener(OpenForum);
        if (changeSaveButton != null)
            changeSaveButton.onClick.AddListener(OpenSaveSlotSelection);
        settingsPanelUI.Hidden += HandleSettingsHidden;
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
            startConfigSelectionUI.ConfigSelected -= SelectConfig;
        if (tutorialButton != null)
            tutorialButton.onClick.RemoveListener(OpenTutorial);
        if (skipTutorialButton != null)
            skipTutorialButton.onClick.RemoveListener(SkipTutorial);
        if (forumButton != null)
            forumButton.onClick.RemoveListener(OpenForum);
        if (changeSaveButton != null)
            changeSaveButton.onClick.RemoveListener(OpenSaveSlotSelection);
        if (settingsPanelUI != null)
            settingsPanelUI.Hidden -= HandleSettingsHidden;
    }

    private void Update()
    {
        if ((selectingStartConfig || confirmingExit || settingsPanelUI.IsShowing || (tutorialPanelUI != null && tutorialPanelUI.IsShowing) || (forumPanelUI != null && forumPanelUI.IsShowing) || (saveSlotSelectionPanelUI != null && saveSlotSelectionPanelUI.gameObject.activeSelf)) && Input.GetMouseButtonDown(0) && IsOutsideAllPanelsClick())
            HideAllPanels();
    }

    private void ResolveReferences()
    {
        if (buttonGroupUI == null)
            buttonGroupUI = GetComponentInChildren<StartMenuButtonGroupUI>(true);
        if (startConfigSelectionUI == null)
            startConfigSelectionUI = GetComponentInChildren<StartConfigSelectionUI>(true);
        if (saveSlotSelectionPanelUI == null)
            saveSlotSelectionPanelUI = GetComponentInChildren<SaveSlotSelectionPanelUI>(true);
        if (tutorialPanelUI == null)
            tutorialPanelUI = GetComponentInChildren<StartTutorialPanelUI>(true);
        if (forumPanelUI == null)
            forumPanelUI = GetComponentInChildren<StartForumPanelUI>(true);
        if (tutorialButton == null)
            tutorialButton = UIManager.FindChildComponent<Button>(transform, "TutorialButton");
        if (skipTutorialButton == null)
            skipTutorialButton = UIManager.FindChildComponent<Button>(transform, "SkipTutorialButton");
        if (forumButton == null)
            forumButton = UIManager.FindChildComponent<Button>(transform, "ForumButton");
        if (changeSaveButton == null)
            changeSaveButton = UIManager.FindChildComponent<Button>(transform, "ChangeSaveButton");
        if (settingsPanelUI == null)
            settingsPanelUI = GetComponentInChildren<StartSettingsPanelUI>(true);
        if (exitConfirmPanelUI == null)
            exitConfirmPanelUI = GetComponentInChildren<StartExitConfirmPanelUI>(true);
        buttonGroupUI.RefreshContinueButton(RunSaveSystem.HasCurrentRun());
        RefreshTutorialStartState();
    }

    private void ContinueSavedRun()
    {
        if (!RunSaveSystem.HasCurrentRun())
            return;

        PlayerState.ContinueSavedRun = true;
        if (SceneTransitionManager.Instance != null)
            SceneTransitionManager.Instance.LoadSceneWithTransition(gameSceneName);
        else
            SceneManager.LoadScene(gameSceneName);
    }

    private void OpenSaveSlotSelection()
    {
        HideStartConfigSelection();
        HideExitConfirm();
        HideTutorial();
        HideForum();
        settingsPanelUI.Hide();
        saveSlotSelectionPanelUI.Show(SelectSaveSlot);
    }

    private void SelectSaveSlot(int slotIndex)
    {
        RunSaveSystem.SelectSlot(slotIndex);
        buttonGroupUI.RefreshContinueButton(RunSaveSystem.HasCurrentRun());
        RefreshTutorialStartState();
    }

    private void HandleStartClicked()
    {
        if (!selectingStartConfig)
        {
            ShowStartConfigSelection();
            return;
        }

        bool hasAllConfigWindows = startConfigSelectionUI.HasExpectedConfigWindows;
        if (!hasAllConfigWindows)
        {
            startConfigSelectionUI.EnsureConfigWindows();
            buttonGroupUI.SetStartConfigSelected(selectedConfig != null);
            return;
        }

        if (selectedConfig == null)
        {
            buttonGroupUI.SetStartConfigSelected(false);
            return;
        }

        PlayerState.SelectedStartConfigId = selectedConfig.id;

        PlayerState.ContinueSavedRun = false;
        if (startingTutorial)
            RunSaveSystem.BeginNewTutorialRun();
        else
            RunSaveSystem.BeginNewRun();

        if (SceneTransitionManager.Instance != null)
            SceneTransitionManager.Instance.LoadSceneWithTransition(gameSceneName);
        else
            SceneManager.LoadScene(gameSceneName);
    }

    private void ShowStartConfigSelection()
    {
        selectingStartConfig = true;
        startingTutorial = RunSaveSystem.ShouldShowTutorialEntry();
        selectedConfig = null;
        buttonGroupUI.SetStartConfigMode(true, false);
        HideExitConfirm();
        HideTutorial();
        HideForum();
        settingsPanelUI.Hide();
        saveSlotSelectionPanelUI.Hide();
        if (startingTutorial)
            startConfigSelectionUI.ShowOnly("balanced");
        else
            startConfigSelectionUI.Show();
    }

    private void HideStartConfigSelection()
    {
        if (!selectingStartConfig)
            return;

        selectingStartConfig = false;
        startingTutorial = false;
        selectedConfig = null;
        buttonGroupUI.SetStartConfigMode(false, false);
        startConfigSelectionUI.Hide();
    }

    private void SelectConfig(PlayerStartConfigData config)
    {
        selectedConfig = config;
        if (selectingStartConfig)
            buttonGroupUI.SetStartConfigSelected(selectedConfig != null);
    }

    private void SkipTutorial()
    {
        HideStartConfigSelection();
        RunSaveSystem.SetTutorialCompleted(true);
        RefreshTutorialStartState();
        buttonGroupUI.ClearActiveOption();
    }

    private void RefreshTutorialStartState()
    {
        bool tutorialPending = RunSaveSystem.ShouldShowTutorialEntry();
        buttonGroupUI.SetTutorialStartMode(tutorialPending);
        if (skipTutorialButton != null)
            skipTutorialButton.gameObject.SetActive(tutorialPending);
    }

    private void OpenSettings()
    {
        HideStartConfigSelection();
        HideExitConfirm();
        HideTutorial();
        HideForum();
        saveSlotSelectionPanelUI.Hide();
        buttonGroupUI.SetSettingsMode(true);
        settingsPanelUI.Show();
    }

    private void OpenTutorial()
    {
        if (tutorialPanelUI == null)
            return;

        HideStartConfigSelection();
        HideExitConfirm();
        HideForum();
        saveSlotSelectionPanelUI.Hide();
        settingsPanelUI.Hide();
        tutorialPanelUI.Show();
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
        HideExitConfirm();
        HideTutorial();
        saveSlotSelectionPanelUI.Hide();
        settingsPanelUI.Hide();
        forumPanelUI.Show();
    }

    private void HideForum()
    {
        if (forumPanelUI != null)
            forumPanelUI.Hide();
    }

    private void HandleSettingsHidden()
    {
        if (!selectingStartConfig && !confirmingExit)
            buttonGroupUI.SetSettingsMode(false);
    }

    private void ExitGame()
    {
        if (!confirmingExit)
        {
            HideStartConfigSelection();
            saveSlotSelectionPanelUI.Hide();
            settingsPanelUI.Hide();
            HideTutorial();
            HideForum();
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
        confirmingExit = true;
        buttonGroupUI.SetExitConfirmMode(true);
        exitConfirmPanelUI.Show();
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
        HideExitConfirm();
        if (!selectingStartConfig && !confirmingExit && !settingsPanelUI.IsShowing && (tutorialPanelUI == null || !tutorialPanelUI.IsShowing) && (forumPanelUI == null || !forumPanelUI.IsShowing))
            buttonGroupUI.ClearActiveOption();
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
                (skipTutorialButton != null && hit.IsChildOf(skipTutorialButton.transform)) ||
                (forumButton != null && hit.IsChildOf(forumButton.transform)) ||
                (changeSaveButton != null && hit.IsChildOf(changeSaveButton.transform)) ||
                startConfigSelectionUI.Contains(hit) ||
                (saveSlotSelectionPanelUI != null && saveSlotSelectionPanelUI.Contains(hit)) ||
                (tutorialPanelUI != null && tutorialPanelUI.Contains(hit)) ||
                (forumPanelUI != null && forumPanelUI.Contains(hit)) ||
                exitConfirmPanelUI.Contains(hit) ||
                settingsPanelUI.Contains(hit))
                return false;
        }
        return true;
    }
}
