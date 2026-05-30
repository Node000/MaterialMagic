using System;
using UnityEngine;
using UnityEngine.UI;

public class SaveSlotSelectionPanelUI : MonoBehaviour
{
    [SerializeField] private Button[] slotButtons = Array.Empty<Button>();
    [SerializeField] private Text[] slotTexts = Array.Empty<Text>();
    [SerializeField] private Button[] deleteButtons = Array.Empty<Button>();
    [SerializeField] private Button closeButton;

    private Action<int> onSlotSelected;

    private void Awake()
    {
        ResolveReferences();
        BindButtons();
        gameObject.SetActive(false);
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
            slotTexts = new Text[3];
            for (int i = 0; i < 3; i++)
            {
                Transform slot = transform.Find("Slot" + (i + 1));
                slotTexts[i] = slot != null ? slot.GetComponentInChildren<Text>(true) : null;
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
            Text text = i < slotTexts.Length ? slotTexts[i] : null;
            if (text == null)
                continue;

            RunSaveData data = RunSaveSystem.LoadSummary(i + 1);
            string active = RunSaveSystem.CurrentSlotIndex == i + 1 ? "（当前）" : string.Empty;
            if (data == null)
            {
                text.text = $"存档 {i + 1}{active}\n通关次数：0\n游戏时间：0分钟\n最后游玩：无";
                continue;
            }

            int minutes = Mathf.FloorToInt(data.totalPlaySeconds / 60f);
            string lastPlayed = string.IsNullOrEmpty(data.lastPlayedAtUtc) ? "无" : data.lastPlayedAtUtc;
            text.text = $"存档 {i + 1}{active}\n通关次数：{data.victoryCount}\n游戏时间：{minutes}分钟\n最后游玩：{lastPlayed}";
        }
    }
}
