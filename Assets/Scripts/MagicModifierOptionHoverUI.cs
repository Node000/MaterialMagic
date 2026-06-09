using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MagicModifierOptionHoverUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private MagicModifierSelectionPanelUI owner;
    private int optionIndex;
    private Selectable selectable;

    public void Initialize(MagicModifierSelectionPanelUI owner, int optionIndex)
    {
        this.owner = owner;
        this.optionIndex = optionIndex;
        selectable = selectable != null ? selectable : GetComponent<Selectable>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (selectable != null && !selectable.interactable)
            return;

        owner?.SetOptionHovered(optionIndex, true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        owner?.SetOptionHovered(optionIndex, false);
    }

    private void OnDisable()
    {
        owner?.SetOptionHovered(optionIndex, false);
    }
}
