using UnityEngine;
using UnityEngine.EventSystems;

public class MagicSlotClickHandler : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private HandSystemUI owner;
    private int slotIndex;
    private bool dragActive;
    private bool suppressClick;

    public void Bind(HandSystemUI owner, int slotIndex)
    {
        this.owner = owner;
        this.slotIndex = slotIndex;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        suppressClick = false;
        if (eventData == null || eventData.button != PointerEventData.InputButton.Left)
            return;

        if (owner == null || !owner.CanBeginMagicBookReorder || !owner.IsMagicSlotOccupied(slotIndex))
            return;

        dragActive = owner.BeginMagicBookDrag(slotIndex, (RectTransform)transform, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragActive || owner == null)
            return;

        owner.MoveMagicBookDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!dragActive)
            return;

        dragActive = false;
        suppressClick = true;
        if (owner != null)
            owner.EndMagicBookDrag(eventData);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (suppressClick)
        {
            suppressClick = false;
            return;
        }

        if (eventData != null && eventData.button == PointerEventData.InputButton.Right)
        {
            owner?.ShowDebugMagicReplacementDropdown(slotIndex, eventData.position);
            return;
        }

        if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
            return;

        if (owner != null)
        {
            TutorialManagerUI tutorialManager = owner.GetUIManager().TutorialManager;
            if (tutorialManager != null && tutorialManager.ConsumeBlockingTutorialClick(eventData))
                return;
        }

        if (owner != null && owner.HasPendingMagicModifier)
            owner.TryApplyPendingMagicModifier(slotIndex);
        else if (owner != null && owner.HasPendingMaterialModifier)
            owner.TryApplyPendingMaterialModifierToSelectedHandCard(slotIndex);
        else if (owner != null && owner.HasPendingShopMagic)
            owner.TryPlacePendingShopMagic(slotIndex);
        else if (owner != null && owner.HasPendingRewardMagic)
            owner.TryPlacePendingRewardMagic(slotIndex);
        else
            owner?.ShowMagicSellPopup(GetComponent<MagicItemView>(), slotIndex);
    }
}
