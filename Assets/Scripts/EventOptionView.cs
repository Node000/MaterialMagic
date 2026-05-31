using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class EventOptionView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
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

    private TMP_Text FindText(string childName)
    {
        Transform child = transform.Find(childName);
        return child != null ? child.GetComponent<TMP_Text>() : null;
    }
}
