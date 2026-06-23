using UnityEngine;
using UnityEngine.EventSystems;

public class MagicSlotClickHandler : MonoBehaviour, IPointerClickHandler
{
    private HandSystemUI owner;
    private int slotIndex;

    public void Bind(HandSystemUI owner, int slotIndex)
    {
        this.owner = owner;
        this.slotIndex = slotIndex;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        if (owner != null)
        {
            TutorialManagerUI tutorialManager = owner.GetUIManager().TutorialManager;
            if (tutorialManager != null && tutorialManager.ConsumeBlockingTutorialClick(eventData))
                return;
        }

        if (owner != null && owner.HasPendingMagicModifier)
            owner.TryApplyPendingMagicModifier(slotIndex);
        else if (owner != null && owner.HasPendingShopMagic)
            owner.TryPlacePendingShopMagic(slotIndex);
        else
            owner?.TryPlacePendingRewardMagic(slotIndex);
    }
}
