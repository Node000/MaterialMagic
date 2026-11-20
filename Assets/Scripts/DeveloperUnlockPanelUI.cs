using UnityEngine;

/// <summary>Scene marker for the QA-only settings subtree, also used by build stripping.</summary>
public sealed class DeveloperUnlockPanelUI : MonoBehaviour
{
#if UNITY_EDITOR || (DEVELOPMENT_BUILD && MATERIALMAGIC_QA_UNLOCK)
    [SerializeField] private UnityEngine.UI.Button openButton;
    [SerializeField] private GameObject confirmationRoot;
    [SerializeField] private TMPro.TMP_Text confirmationText;
    [SerializeField] private UnityEngine.UI.Button confirmButton;
    [SerializeField] private UnityEngine.UI.Button cancelButton;
    [SerializeField] private TMPro.TMP_Text feedbackText;

    private int confirmedSlot;
    private string feedbackKey;
    private string feedbackFallback;
    private int feedbackSlot;

    private void OnEnable()
    {
        Cancel();
        SetFeedback(null, null, 0);
        openButton.onClick.AddListener(Open);
        confirmButton.onClick.AddListener(Confirm);
        cancelButton.onClick.AddListener(Cancel);
        LocalizationSystem.LanguageChanged += RefreshText;
    }

    private void OnDisable()
    {
        openButton.onClick.RemoveListener(Open);
        confirmButton.onClick.RemoveListener(Confirm);
        cancelButton.onClick.RemoveListener(Cancel);
        LocalizationSystem.LanguageChanged -= RefreshText;
        Cancel();
    }

    private void Open()
    {
        confirmedSlot = RunSaveSystem.CurrentSlotIndex;
        SetFeedback(null, null, 0);
        confirmationRoot.SetActive(true);
        RefreshText();
    }

    private void Cancel()
    {
        confirmedSlot = 0;
        confirmationRoot.SetActive(false);
    }

    private void Confirm()
    {
        int slot = confirmedSlot;
        if (slot == 0)
            return;

        Cancel();
        try
        {
            if (DeveloperUnlockService.TryUnlockCurrentSlot(slot))
            {
                SetFeedback("ui.developer_unlock.success", "存档 {0} 已全解锁", slot);
                return;
            }

            SetFeedback("ui.developer_unlock.unavailable", "当前无法解锁，请重新打开确认。", slot);
        }
        catch (System.Exception ex) when (ex is System.IO.IOException || ex is System.UnauthorizedAccessException || ex is System.ArgumentException)
        {
            Debug.LogException(ex);
            SetFeedback("ui.developer_unlock.error", "未能完成解锁，可重试。", slot);
        }
    }

    private void SetFeedback(string key, string fallback, int slot)
    {
        feedbackKey = key;
        feedbackFallback = fallback;
        feedbackSlot = slot;
        RefreshText();
    }

    private void RefreshText()
    {
        if (confirmedSlot != 0)
            confirmationText.text = string.Format(LocalizationSystem.GetText("ui.developer_unlock.prompt",
                "将解锁存档 {0} 的全部内容并写入存档。\n不会更改进行中的一局。"), confirmedSlot);
        feedbackText.text = feedbackKey == null ? string.Empty :
            string.Format(LocalizationSystem.GetText(feedbackKey, feedbackFallback), feedbackSlot);
    }
#else
    // Fail closed if a build bypasses normal scene processing.
    private void Awake() => gameObject.SetActive(false);
#endif
}
