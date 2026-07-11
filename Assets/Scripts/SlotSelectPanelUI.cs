using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SlotSelectPanelUI : MonoBehaviour
{
    private HandSystemUI owner;
    private MagicData pendingRewardMagic;
    private Action<int> slotChosen;
    private readonly List<Button> slotButtons = new List<Button>();
    private Vector2 initialPanelSize;

    private const float SlotButtonSpacingX = 120f;
    private const float SlotButtonSpacingY = 72f;
    private const float SlotButtonBaseY = -24f;
    private const int MaxSlotButtonsPerRow = 6;

    public void Initialize(HandSystemUI owner)
    {
        this.owner = owner;
        RectTransform rect = transform as RectTransform;
        if (rect != null)
            initialPanelSize = rect.sizeDelta;
        LocalizationSystem.LanguageChanged -= RefreshLocalizedContent;
        LocalizationSystem.LanguageChanged += RefreshLocalizedContent;
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        LocalizationSystem.LanguageChanged -= RefreshLocalizedContent;
    }

    public void Show(MagicData rewardMagic)
    {
        Show(rewardMagic, null);
    }

    public void Show(MagicData rewardMagic, Action<int> onSlotChosen)
    {
        if (owner == null || rewardMagic == null)
            return;

        pendingRewardMagic = rewardMagic;
        slotChosen = onSlotChosen;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        TMP_Text title = UIManager.FindChildComponent<TMP_Text>(transform, "Title");
        if (title != null)
            title.text = LocalizationSystem.GetText("ui.slot_select.title", "选择要填入的道具槽");

        EnsureSlotButtons(owner.MagicSlotViewCount);
        LayoutSlotButtons(owner.MagicSlotViewCount);

        for (int i = 0; i < slotButtons.Count; i++)
        {
            Button button = slotButtons[i];
            if (button == null)
                continue;

            bool visible = i < owner.MagicSlotViewCount;
            button.gameObject.SetActive(visible);
            if (!visible)
                continue;

            int slotIndex = i;
            MagicModel magic = owner.PlayerState.GetMagicAtSlot(i);
            TMP_Text text = UIManager.FindChildComponent<TMP_Text>(button.transform, "Text");
            if (text != null)
            {
                string slotName = magic != null ? magic.Name : LocalizationSystem.GetText("ui.slot_select.empty_slot", "空槽");
                text.text = string.Format(LocalizationSystem.GetText("ui.slot_select.slot_format", "{0}: {1}"), i + 1, slotName);
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => ChooseSlot(slotIndex));
        }
    }

    private void EnsureSlotButtons(int slotCount)
    {
        slotButtons.Clear();
        for (int i = 0; ; i++)
        {
            Button button = UIManager.FindChildComponent<Button>(transform, "Slot" + i);
            if (button == null)
                break;

            slotButtons.Add(button);
        }

        if (slotButtons.Count == 0)
            return;

        Button template = slotButtons[0];
        Transform parent = template.transform.parent;
        for (int i = slotButtons.Count; i < slotCount; i++)
        {
            Button button = Instantiate(template, parent);
            button.name = "Slot" + i;
            slotButtons.Add(button);
        }
    }

    private void LayoutSlotButtons(int slotCount)
    {
        int columns = Mathf.Min(MaxSlotButtonsPerRow, Mathf.Max(1, slotCount));
        int rows = Mathf.Max(1, Mathf.CeilToInt(slotCount / (float)columns));
        RectTransform panelRect = transform as RectTransform;
        if (panelRect != null)
        {
            if (initialPanelSize == Vector2.zero)
                initialPanelSize = panelRect.sizeDelta;
            panelRect.sizeDelta = new Vector2(initialPanelSize.x, Mathf.Max(initialPanelSize.y, 118f + rows * SlotButtonSpacingY));
        }

        for (int i = 0; i < slotButtons.Count; i++)
        {
            RectTransform rect = slotButtons[i] != null ? slotButtons[i].transform as RectTransform : null;
            if (rect == null || i >= slotCount)
                continue;

            int row = i / columns;
            int column = i % columns;
            float x = (column - (columns - 1) * 0.5f) * SlotButtonSpacingX;
            float y = SlotButtonBaseY - (row - (rows - 1) * 0.5f) * SlotButtonSpacingY;
            rect.anchoredPosition = new Vector2(x, y);
        }
    }

    private void RefreshLocalizedContent()
    {
        if (this == null)
            return;

        if (gameObject.activeInHierarchy && pendingRewardMagic != null)
            Show(pendingRewardMagic, slotChosen);
    }

    public void Hide()
    {
        pendingRewardMagic = null;
        slotChosen = null;
        gameObject.SetActive(false);
    }

    private void ChooseSlot(int slotIndex)
    {
        if (pendingRewardMagic == null)
            return;

        MagicData rewardMagic = pendingRewardMagic;
        Action<int> chosen = slotChosen;
        pendingRewardMagic = null;
        slotChosen = null;
        if (chosen != null)
            chosen(slotIndex);
        else
            owner.SetRewardMagicAtSlot(rewardMagic, slotIndex);
        Hide();
    }
}
