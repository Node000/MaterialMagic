using UnityEngine;
using UnityEngine.UI;

public class SlotSelectPanelUI : MonoBehaviour
{
    private HandSystemUI owner;
    private MagicData pendingRewardMagic;

    public void Initialize(HandSystemUI owner)
    {
        this.owner = owner;
        gameObject.SetActive(false);
    }

    public void Show(MagicData rewardMagic)
    {
        if (owner == null || rewardMagic == null)
            return;

        pendingRewardMagic = rewardMagic;
        gameObject.SetActive(true);
        Text title = UIManager.FindChildComponent<Text>(transform, "Title");
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
            Text text = UIManager.FindChildComponent<Text>(button.transform, "Text");
            if (text != null)
                text.text = magic != null ? i + 1 + ": " + magic.Name : i + 1 + ": 空槽";

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => ChooseSlot(slotIndex));
        }
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void ChooseSlot(int slotIndex)
    {
        if (pendingRewardMagic == null)
            return;

        owner.SetRewardMagicAtSlot(pendingRewardMagic, slotIndex);
        pendingRewardMagic = null;
        Hide();
    }
}
