using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class EventOptionView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private TMP_Text recipeText;
    [SerializeField] private TMP_Text optionText;

    private EventPanelUI owner;
    private EventOptionData option;

    public TMP_Text RecipeText => recipeText != null ? recipeText : recipeText = FindText("Recipe");
    public TMP_Text OptionText => optionText != null ? optionText : optionText = FindText("Text");

    public void Bind(EventPanelUI owner, EventOptionData option)
    {
        this.owner = owner;
        this.option = option;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        owner?.ShowOptionTooltip((RectTransform)transform, option);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        owner?.HideOptionTooltip();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
            owner?.PinOptionTooltip((RectTransform)transform, option);
    }

    private TMP_Text FindText(string childName)
    {
        Transform child = transform.Find(childName);
        return child != null ? child.GetComponent<TMP_Text>() : null;
    }
}
