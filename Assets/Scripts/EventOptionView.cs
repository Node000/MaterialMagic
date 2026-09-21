using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class EventOptionView : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private TMP_Text recipeText;
    [SerializeField] private TMP_Text optionText;

    private EventPanelUI owner;
    private EventOptionData option;
    private UnifiedDetailTriggerUI detailTrigger;

    public TMP_Text RecipeText => recipeText != null ? recipeText : recipeText = FindText("Recipe");
    public TMP_Text OptionText => optionText != null ? optionText : optionText = FindText("Text");

    public void Bind(EventPanelUI owner, EventOptionData option)
    {
        this.owner = owner;
        this.option = option;
        // 详情面板统一由 UnifiedDetailTriggerUI 负责（PC 悬停显示 / PE 长按看详情），内容按当前选项实时构建。
        UnifiedDetailTriggerUI trigger = EnsureDetailTrigger();
        trigger.SetAnchor((RectTransform)transform);
        trigger.SetContentProvider(BuildDetailContent);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
            EnsureDetailTrigger().PinDetailNow();
    }

    /// <summary>详情内容随当前选项变化，所以用 Provider 注入；未绑定时不弹面板。</summary>
    private UnifiedDetailContent BuildDetailContent()
    {
        return option != null ? UnifiedDetailContentBuilder.Build(option) : default;
    }

    /// <summary>没挂组件时兜底补上（美术资源漏挂也能正常工作）。</summary>
    private UnifiedDetailTriggerUI EnsureDetailTrigger()
    {
        if (detailTrigger == null)
        {
            detailTrigger = GetComponent<UnifiedDetailTriggerUI>();
            if (detailTrigger == null)
                detailTrigger = gameObject.AddComponent<UnifiedDetailTriggerUI>();
        }

        return detailTrigger;
    }

    private TMP_Text FindText(string childName)
    {
        Transform child = transform.Find(childName);
        return child != null ? child.GetComponent<TMP_Text>() : null;
    }
}
