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
        if (eventData.button == PointerEventData.InputButton.Left)
            owner?.TryPlacePendingRewardMagic(slotIndex);
    }
}
