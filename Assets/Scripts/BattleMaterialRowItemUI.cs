using UnityEngine;
using UnityEngine.EventSystems;

public class BattleMaterialRowItemUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
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
        if (eventData == null || eventData.button == PointerEventData.InputButton.Left)
            owner?.HandleItemClicked(index);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        owner?.SetHover(index);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        owner?.ClearHover(index);
    }
}
