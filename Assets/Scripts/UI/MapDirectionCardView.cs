using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class MapDirectionCardView : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Image iconImage;
    [SerializeField] private float iconSize = 68f;

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
        if (backgroundImage != null)
            backgroundImage.color = new Color(0.08f, 0.1f, 0.16f, 0.92f);
        if (iconImage != null)
        {
            iconImage.sprite = MaterialCardView.GetMaterialIcon(material);
            iconImage.color = Color.white;
            RectTransform rectTransform = iconImage.rectTransform;
            if (rectTransform != null)
                rectTransform.sizeDelta = new Vector2(iconSize, iconSize);
        }
    }
}
