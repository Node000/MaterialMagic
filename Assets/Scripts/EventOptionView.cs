using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class EventOptionView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Text recipeText;
    [SerializeField] private Text optionText;

    private EventPanelUI owner;
    private EventOptionData option;

    public Text RecipeText => recipeText != null ? recipeText : recipeText = FindText("Recipe");
    public Text OptionText => optionText != null ? optionText : optionText = FindText("Text");

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

    private Text FindText(string childName)
    {
        Transform child = transform.Find(childName);
        return child != null ? child.GetComponent<Text>() : null;
    }
}
