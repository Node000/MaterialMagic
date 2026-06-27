using UnityEngine;
using UnityEngine.EventSystems;

public class BattleMaterialRowItemUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
{
    private BattleMaterialRowUI owner;
    private int index;

    public void Initialize(BattleMaterialRowUI owner, int index)
    {
        this.owner = owner;
        this.index = index;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (ShouldUseMobileInteraction())
            return;

        if (eventData == null || eventData.button == PointerEventData.InputButton.Left)
            owner?.HandleItemClicked(index);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!ShouldStartReleaseConfirm(eventData))
            return;

        owner?.BeginTouchReleaseConfirm(index);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        owner?.SetHover(index);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        owner?.ClearHover(index);
    }

    private bool ShouldStartReleaseConfirm(PointerEventData eventData)
    {
        if (!ShouldUseMobileInteraction() || eventData == null)
            return false;

        bool touchPointer = Input.touchCount > 0 || eventData.pointerId >= 0;
        if (touchPointer)
            return true;

        return eventData.button == PointerEventData.InputButton.Left;
    }

    private bool ShouldUseMobileInteraction()
    {
        return owner != null && owner.ShouldUseMobileInteraction();
    }
}
