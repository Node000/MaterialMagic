using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StartExitConfirmPanelUI : MonoBehaviour
{
    [SerializeField] private RectTransform panelRect;
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private string promptKey = "ui.start_menu.exit_prompt";
    [SerializeField] private string prompt = "是否退出游戏？";
    [SerializeField] private float moveRightDistance = 900f;
    [SerializeField] private float moveDuration = 0.32f;
    [SerializeField] private Ease moveEase = Ease.OutCubic;

    private Tween moveTween;
    private Vector2 hiddenPosition;
    private string currentPromptKey;
    private string currentPromptFallback;

    public bool IsShowing => gameObject.activeSelf;

    private void Awake()
    {
        ResolveReferences();
        LocalizationSystem.LanguageChanged += RefreshPromptText;
        if (string.IsNullOrEmpty(currentPromptKey))
            SetCurrentPrompt(promptKey, prompt);
        RefreshPromptText();
        if (panelRect != null)
        {
            hiddenPosition = panelRect.anchoredPosition;
            panelRect.anchoredPosition = hiddenPosition;
        }
    }

    private void OnDestroy()
    {
        LocalizationSystem.LanguageChanged -= RefreshPromptText;
        moveTween?.Kill(false);
    }

    public void Show()
    {
        ShowLocalized(promptKey, prompt);
    }

    public void ShowLocalized(string key, string fallback)
    {
        SetCurrentPrompt(key, fallback);
        Show(LocalizationSystem.GetText(currentPromptKey, currentPromptFallback));
    }

    public void Show(string message)
    {
        if (panelRect == null)
            return;

        if (promptText != null)
            promptText.text = message;
        gameObject.SetActive(true);
        moveTween?.Kill(false);
        panelRect.anchoredPosition = hiddenPosition;
        moveTween = panelRect.DOAnchorPos(hiddenPosition + Vector2.right * moveRightDistance, moveDuration)
            .SetEase(moveEase)
            .SetUpdate(true)
            .SetTarget(this);
    }

    public void Hide()
    {
        if (!gameObject.activeSelf || panelRect == null)
            return;

        moveTween?.Kill(false);
        moveTween = panelRect.DOAnchorPos(hiddenPosition, moveDuration)
            .SetEase(moveEase)
            .SetUpdate(true)
            .SetTarget(this)
            .OnComplete(() => gameObject.SetActive(false));
    }

    public bool Contains(Transform hit)
    {
        return hit != null && hit.IsChildOf(transform);
    }

    private void RefreshPromptText()
    {
        if (promptText != null && gameObject.activeSelf)
            promptText.text = LocalizationSystem.GetText(currentPromptKey, currentPromptFallback);
    }

    private void SetCurrentPrompt(string key, string fallback)
    {
        currentPromptKey = key;
        currentPromptFallback = fallback;
    }

    private void ResolveReferences()
    {
        if (panelRect == null)
            panelRect = transform as RectTransform;
        if (promptText == null)
            promptText = transform.Find("Text")?.GetComponent<TMP_Text>();
    }
}
