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
    [SerializeField] private Button tutorialButton;
    [SerializeField] private Button changeSaveButton;
    [SerializeField] private StartSettingsPanelUI settingsPanelUI;
    [SerializeField] private StartExitConfirmPanelUI exitConfirmPanelUI;

    private readonly List<RaycastResult> raycastResults = new List<RaycastResult>();
    private PointerEventData pointerEventData;
    private PlayerStartConfigData selectedConfig;
    private bool selectingStartConfig;
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
        if (changeSaveButton != null)
            changeSaveButton.onClick.RemoveListener(OpenSaveSlotSelection);
        if (settingsPanelUI != null)
            settingsPanelUI.Hidden -= HandleSettingsHidden;
    }

    private void Update()
    {
        if ((selectingStartConfig || confirmingExit || settingsPanelUI.IsShowing || (tutorialPanelUI != null && tutorialPanelUI.IsShowing) || (saveSlotSelectionPanelUI != null && saveSlotSelectionPanelUI.gameObject.activeSelf)) && Input.GetMouseButtonDown(0) && IsOutsideAllPanelsClick())
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
        if (tutorialButton == null)
            tutorialButton = transform.Find("TutorialButton")?.GetComponent<Button>();
        if (changeSaveButton == null)
            changeSaveButton = transform.Find("ChangeSaveButton")?.GetComponent<Button>();
        if (settingsPanelUI == null)
            settingsPanelUI = GetComponentInChildren<StartSettingsPanelUI>(true);
        if (exitConfirmPanelUI == null)
            exitConfirmPanelUI = GetComponentInChildren<StartExitConfirmPanelUI>(true);
        buttonGroupUI.RefreshContinueButton(RunSaveSystem.HasCurrentRun());
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
        settingsPanelUI.Hide();
        saveSlotSelectionPanelUI.Show(SelectSaveSlot);
    }

    private void SelectSaveSlot(int slotIndex)
    {
        RunSaveSystem.SelectSlot(slotIndex);
        buttonGroupUI.RefreshContinueButton(RunSaveSystem.HasCurrentRun());
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
        RunSaveSystem.BeginNewRun();

        if (SceneTransitionManager.Instance != null)
            SceneTransitionManager.Instance.LoadSceneWithTransition(gameSceneName);
        else
            SceneManager.LoadScene(gameSceneName);
    }

    private void ShowStartConfigSelection()
    {
        selectingStartConfig = true;
        selectedConfig = null;
        buttonGroupUI.SetStartConfigMode(true, false);
        HideExitConfirm();
        HideTutorial();
        settingsPanelUI.Hide();
        saveSlotSelectionPanelUI.Hide();
        startConfigSelectionUI.Show();
    }

    private void HideStartConfigSelection()
    {
        if (!selectingStartConfig)
            return;

        selectingStartConfig = false;
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

    private void OpenSettings()
    {
        HideStartConfigSelection();
        HideExitConfirm();
        HideTutorial();
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
        saveSlotSelectionPanelUI.Hide();
        settingsPanelUI.Hide();
        tutorialPanelUI.Show();
    }

    private void HideTutorial()
    {
        if (tutorialPanelUI != null)
            tutorialPanelUI.Hide();
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
        HideExitConfirm();
        if (!selectingStartConfig && !confirmingExit && !settingsPanelUI.IsShowing && (tutorialPanelUI == null || !tutorialPanelUI.IsShowing))
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
                (changeSaveButton != null && hit.IsChildOf(changeSaveButton.transform)) ||
                startConfigSelectionUI.Contains(hit) ||
                (saveSlotSelectionPanelUI != null && saveSlotSelectionPanelUI.Contains(hit)) ||
                (tutorialPanelUI != null && tutorialPanelUI.Contains(hit)) ||
                exitConfirmPanelUI.Contains(hit) ||
                settingsPanelUI.Contains(hit))
                return false;
        }
        return true;
    }
}
