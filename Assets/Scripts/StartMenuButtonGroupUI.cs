using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StartMenuButtonGroupUI : MonoBehaviour
{
    [SerializeField] private Button startButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button exitButton;
    [SerializeField] private string startTextKey = "ui.start_menu.start";
    [SerializeField] private string abandonRunTextKey = "ui.start_menu.abandon_run";
    [SerializeField] private string confirmAbandonRunTextKey = "ui.start_menu.confirm_abandon_run";
    [SerializeField] private string exitTextKey = "ui.start_menu.exit";
    [SerializeField] private string confirmExitTextKey = "ui.start_menu.confirm_exit";
    [Header("继续游戏")]
    [SerializeField] private Color continueDisabledTextColor = new Color(0.35f, 0.35f, 0.35f, 1f);

    private TMP_Text startButtonText;
    private TMP_Text continueButtonText;
    private TMP_Text exitButtonText;
    private Color continueButtonTextColor;
    private string originalStartText;
    private string originalExitText;
    private string abandonRunText;
    private string confirmAbandonRunText;
    private string confirmExitText;
    private bool hasCurrentRun;
    private bool startAbandonConfirmMode;
    private bool exitConfirmMode;

    public event Action StartClicked;
    public event Action ContinueClicked;
    public event Action SettingsClicked;
    public event Action ExitClicked;

    public GameObject ContinueButtonObject => continueButton != null ? continueButton.gameObject : null;

    private void Awake()
    {
        ResolveReferences();
        startButton.onClick.AddListener(HandleStartClicked);
        if (continueButton != null)
            continueButton.onClick.AddListener(HandleContinueClicked);
        settingsButton.onClick.AddListener(HandleSettingsClicked);
        exitButton.onClick.AddListener(HandleExitClicked);

        startButtonText = startButton.GetComponentInChildren<TMP_Text>(true);
        continueButtonText = continueButton != null ? continueButton.GetComponentInChildren<TMP_Text>(true) : null;
        if (continueButtonText != null)
            continueButtonTextColor = continueButtonText.color;
        exitButtonText = exitButton.GetComponentInChildren<TMP_Text>(true);
        RefreshLocalizedTextCache();
        LocalizationSystem.LanguageChanged += HandleLanguageChanged;
        RefreshButtonText();
    }

    private void OnDestroy()
    {
        if (startButton != null)
            startButton.onClick.RemoveListener(HandleStartClicked);
        if (continueButton != null)
            continueButton.onClick.RemoveListener(HandleContinueClicked);
        if (settingsButton != null)
            settingsButton.onClick.RemoveListener(HandleSettingsClicked);
        if (exitButton != null)
            exitButton.onClick.RemoveListener(HandleExitClicked);
        LocalizationSystem.LanguageChanged -= HandleLanguageChanged;
    }

    public bool Contains(Transform hit)
    {
        return hit != null && hit.IsChildOf(transform);
    }

    public void SetExitConfirmMode(bool confirming)
    {
        exitConfirmMode = confirming;
        RefreshButtonText();
    }

    public void SetStartAbandonConfirmMode(bool confirming)
    {
        startAbandonConfirmMode = confirming && hasCurrentRun;
        RefreshButtonText();
    }

    public void RefreshContinueButton(bool hasRun)
    {
        hasCurrentRun = hasRun;
        if (!hasCurrentRun)
            startAbandonConfirmMode = false;
        RefreshButtonText();
    }

    private void HandleLanguageChanged()
    {
        RefreshLocalizedTextCache();
        RefreshButtonText();
    }

    private void RefreshLocalizedTextCache()
    {
        originalStartText = LocalizationSystem.GetText(startTextKey, startButtonText != null && !string.IsNullOrEmpty(startButtonText.text) ? startButtonText.text : "开始游戏");
        abandonRunText = LocalizationSystem.GetText(abandonRunTextKey, "放弃本局游戏");
        confirmAbandonRunText = LocalizationSystem.GetText(confirmAbandonRunTextKey, "确认放弃");
        originalExitText = LocalizationSystem.GetText(exitTextKey, exitButtonText != null && !string.IsNullOrEmpty(exitButtonText.text) ? exitButtonText.text : "退出游戏");
        confirmExitText = LocalizationSystem.GetText(confirmExitTextKey, "确认退出");
    }

    private void RefreshButtonText()
    {
        if (startButtonText != null)
            startButtonText.text = hasCurrentRun ? (startAbandonConfirmMode ? confirmAbandonRunText : abandonRunText) : originalStartText;
        if (exitButtonText != null)
            exitButtonText.text = exitConfirmMode ? confirmExitText : originalExitText;
        ApplyContinueButtonState();
    }

    private void ApplyContinueButtonState()
    {
        if (continueButton != null)
            continueButton.interactable = hasCurrentRun;
        if (continueButtonText != null)
            continueButtonText.color = hasCurrentRun ? continueButtonTextColor : continueDisabledTextColor;
    }

    private void ResolveReferences()
    {
        if (startButton == null)
            startButton = transform.Find("StartButton")?.GetComponent<Button>();
        if (continueButton == null)
            continueButton = transform.Find("ContinueButton")?.GetComponent<Button>();
        if (settingsButton == null)
            settingsButton = transform.Find("SettingsButton")?.GetComponent<Button>();
        if (exitButton == null)
            exitButton = transform.Find("ExitButton")?.GetComponent<Button>();
    }

    private void HandleStartClicked()
    {
        StartClicked?.Invoke();
    }

    private void HandleContinueClicked()
    {
        ContinueClicked?.Invoke();
    }

    private void HandleSettingsClicked()
    {
        SettingsClicked?.Invoke();
    }

    private void HandleExitClicked()
    {
        ExitClicked?.Invoke();
    }
}
