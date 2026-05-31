using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SlotSelectPanelUI : MonoBehaviour
{
    private HandSystemUI owner;
    private MagicData pendingRewardMagic;
    private Action<int> slotChosen;

    public void Initialize(HandSystemUI owner)
    {
        this.owner = owner;
        gameObject.SetActive(false);
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
            title.text = "选择要填入的法术槽";

        for (int i = 0; i < owner.PlayerState.MagicBookSlotCount; i++)
        {
            Button button = UIManager.FindChildComponent<Button>(transform, "Slot" + i);
            if (button == null)
                continue;

            button.gameObject.SetActive(true);
            int slotIndex = i;
            MagicModel magic = owner.PlayerState.GetMagicAtSlot(i);
            TMP_Text text = UIManager.FindChildComponent<TMP_Text>(button.transform, "Text");
            if (text != null)
                text.text = magic != null ? i + 1 + ": " + magic.Name : i + 1 + ": 空槽";

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => ChooseSlot(slotIndex));
        }
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
