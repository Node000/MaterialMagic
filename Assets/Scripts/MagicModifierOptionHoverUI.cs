using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MagicModifierOptionHoverUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    private MagicModifierSelectionPanelUI owner;
    private int optionIndex;
    private Selectable selectable;
    private bool pressActive;
    private bool pressInside;

    public void Initialize(MagicModifierSelectionPanelUI owner, int optionIndex)
    {
        this.owner = owner;
        this.optionIndex = optionIndex;
        selectable = selectable != null ? selectable : GetComponent<Selectable>();
        pressActive = false;
        pressInside = false;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!IsInteractable())
            return;

        owner?.SetOptionHovered(optionIndex, true);
        owner?.ShowOptionDetail(optionIndex, this);
        if (pressActive && ShouldUseMobileInteraction())
            pressInside = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        owner?.SetOptionHovered(optionIndex, false);
        owner?.HideOptionDetail(this);
        if (pressActive && ShouldUseMobileInteraction())
            pressInside = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!ShouldStartReleaseConfirm(eventData))
            return;

        pressActive = true;
        pressInside = true;
        owner?.SetOptionHovered(optionIndex, true);
        owner?.ShowOptionDetail(optionIndex, this);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!pressActive)
            return;

        bool confirmSelection = pressInside && ShouldUseMobileInteraction() && IsInteractable();
        if (confirmSelection)
            owner?.ShowOptionDetail(optionIndex, this);
        pressActive = false;
        pressInside = false;
        if (confirmSelection)
            owner?.ConfirmTouchOption(optionIndex);
    }

    private void OnDisable()
    {
        pressActive = false;
        pressInside = false;
        owner?.SetOptionHovered(optionIndex, false);
        owner?.HideOptionDetail(this);
    }

    private bool IsInteractable()
    {
        return selectable == null || selectable.interactable;
    }

    private bool ShouldStartReleaseConfirm(PointerEventData eventData)
    {
        if (!ShouldUseMobileInteraction() || !IsInteractable() || eventData == null)
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
