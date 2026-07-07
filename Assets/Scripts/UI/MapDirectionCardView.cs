using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MapDirectionCardView : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Image iconImage;

    private ChapterGridPanelUI owner;
    private MaterialEnum material;

    public void Initialize(ChapterGridPanelUI owner, MaterialEnum material)
    {
        this.owner = owner;
        this.material = material;
        RefreshVisual();
    }

    public void SetInteractable(bool interactable)
    {
        Button button = GetComponent<Button>();
        if (button != null)
            button.interactable = interactable;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left || eventData.button == PointerEventData.InputButton.Right)
            owner?.HandleDirectionClicked(material);
    }

    public void RefreshVisual()
    {
        if (iconImage != null)
            iconImage.sprite = MaterialCardView.GetMaterialIcon(material);
    }
}
