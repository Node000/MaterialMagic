using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SaveSlotSelectionPanelUI : MonoBehaviour
{
    [SerializeField] private Button[] slotButtons = Array.Empty<Button>();
    [SerializeField] private TMP_Text[] slotTexts = Array.Empty<TMP_Text>();
    [SerializeField] private Button[] deleteButtons = Array.Empty<Button>();
    [SerializeField] private Button closeButton;

    private Action<int> onSlotSelected;

    private void Awake()
    {
        ResolveReferences();
        BindButtons();
        LocalizationSystem.LanguageChanged += HandleLanguageChanged;
    }

    private void OnDestroy()
    {
        LocalizationSystem.LanguageChanged -= HandleLanguageChanged;
    }

    public void Show(Action<int> slotSelected)
    {
        onSlotSelected = slotSelected;
        Refresh();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public bool Contains(Transform hit)
    {
        return hit != null && hit.IsChildOf(transform);
    }

    private void ResolveReferences()
    {
        if (slotButtons == null || slotButtons.Length < 3)
            slotButtons = GetComponentsInChildren<Button>(true);

        if (slotTexts == null || slotTexts.Length < 3)
        {
            slotTexts = new TMP_Text[3];
            for (int i = 0; i < 3; i++)
            {
                Transform slot = transform.Find("Slot" + (i + 1));
                slotTexts[i] = slot != null ? slot.GetComponentInChildren<TMP_Text>(true) : null;
            }
        }

        if (deleteButtons == null || deleteButtons.Length < 3)
        {
            deleteButtons = new Button[3];
            for (int i = 0; i < 3; i++)
                deleteButtons[i] = transform.Find("DeleteSlot" + (i + 1))?.GetComponent<Button>();
        }

        if (closeButton == null)
            closeButton = transform.Find("CloseButton")?.GetComponent<Button>();
    }

    private void BindButtons()
    {
        for (int i = 0; i < slotButtons.Length; i++)
        {
            int index = i;
            if (slotButtons[i] == null || slotButtons[i] == closeButton || IsDeleteButton(slotButtons[i]))
                continue;

            slotButtons[i].onClick.RemoveAllListeners();
            slotButtons[i].onClick.AddListener(() => SelectSlot(index + 1));
        }

        for (int i = 0; i < deleteButtons.Length; i++)
        {
            int slotIndex = i + 1;
            if (deleteButtons[i] == null)
                continue;

            deleteButtons[i].onClick.RemoveAllListeners();
            deleteButtons[i].onClick.AddListener(() => DeleteSlot(slotIndex));
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Hide);
        }
    }

    private void SelectSlot(int slotIndex)
    {
        onSlotSelected?.Invoke(slotIndex);
        Hide();
    }

    private void DeleteSlot(int slotIndex)
    {
        RunSaveSystem.ClearSlot(slotIndex);
        if (RunSaveSystem.CurrentSlotIndex == slotIndex)
            onSlotSelected?.Invoke(slotIndex);
        Refresh();
    }

    private bool IsDeleteButton(Button button)
    {
        for (int i = 0; i < deleteButtons.Length; i++)
        {
            if (deleteButtons[i] == button)
                return true;
        }
        return false;
    }

    private void Refresh()
    {
        for (int i = 0; i < 3; i++)
        {
            TMP_Text text = i < slotTexts.Length ? slotTexts[i] : null;
            if (text == null)
                continue;

            RunSaveData data = RunSaveSystem.LoadSummary(i + 1);
            bool isActive = RunSaveSystem.CurrentSlotIndex == i + 1;
            string titleTemplate = LocalizationSystem.GetText(isActive ? "ui.save_slot.current_title" : "ui.save_slot.title", isActive ? "▶ 当前存档 {0}" : "存档 {0}");
            string slotTitle = string.Format(titleTemplate, i + 1);
            string summaryTemplate = LocalizationSystem.GetText("ui.save_slot.summary", "{0}\n通关次数：{1}\n游戏时间：{2}分钟\n最后游玩：{3}");
            string noneText = LocalizationSystem.GetText("ui.common.none", "无");
            if (data == null)
            {
                text.text = string.Format(summaryTemplate, slotTitle, 0, 0, noneText);
                continue;
            }

            int minutes = Mathf.FloorToInt(data.totalPlaySeconds / 60f);
            string lastPlayed = string.IsNullOrEmpty(data.lastPlayedAtUtc) ? noneText : data.lastPlayedAtUtc;
            text.text = string.Format(summaryTemplate, slotTitle, data.victoryCount, minutes, lastPlayed);
        }
    }

    private void HandleLanguageChanged()
    {
        if (gameObject.activeSelf)
            Refresh();
    }
}
