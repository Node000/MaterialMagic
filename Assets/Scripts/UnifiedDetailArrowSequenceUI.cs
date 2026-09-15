using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 详情面板左下角的“箭头序列”弹簧小线框。
/// 容器、线框与图标槽位都在场景里搭好；运行时只按道具的施法序列刷新图标与显隐。
/// </summary>
public class UnifiedDetailArrowSequenceUI : MonoBehaviour
{
    [Tooltip("整体显示根（含弹簧线框）；无序列时整体隐藏。留空时使用本物体。")]
    [SerializeField] private GameObject visualRoot;
    [Tooltip("图标槽位容器；留空时使用本物体。")]
    [SerializeField] private RectTransform iconRow;
    [Tooltip("场景内预置的图标槽位（按顺序使用，多余的隐藏）。")]
    [SerializeField] private List<Image> iconSlots = new List<Image>();
    [Header("箭头大小 / 间距")]
    [Tooltip("单个箭头图标尺寸（运行时写到图标槽位的 LayoutElement）。")]
    [SerializeField] private Vector2 iconSize = new Vector2(24f, 24f);
    [Tooltip("箭头之间的间距（运行时写到布局组的 spacing）。")]
    [SerializeField] private float iconSpacing = 3f;

    private readonly List<Image> slots = new List<Image>();

    private void Awake()
    {
        ApplyLayoutSettings();
    }

    private void OnValidate()
    {
        // 只在编辑态预览（物体被激活）时同步，避免加载场景就被标记为脏
        if (!isActiveAndEnabled)
            return;
        ApplyLayoutSettings();
    }

    /// <summary>把手动设置的箭头大小与间距写进场景内的布局组与图标槽位。</summary>
    public void ApplyLayoutSettings()
    {
        RectTransform row = iconRow != null ? iconRow : transform as RectTransform;
        if (row != null)
        {
            HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
            if (layout != null)
                layout.spacing = iconSpacing;   // 允许负值：箭头相互重叠（同道具卡序列的 -28/-14）
        }

        Vector2 size = new Vector2(Mathf.Max(1f, iconSize.x), Mathf.Max(1f, iconSize.y));
        CacheSlots();
        for (int i = 0; i < slots.Count; i++)
        {
            Image slot = slots[i];
            if (slot == null)
                continue;

            LayoutElement element = slot.GetComponent<LayoutElement>();
            if (element != null)
            {
                element.preferredWidth = size.x;
                element.preferredHeight = size.y;
            }
        }
    }

    /// <summary>按施法序列刷新显示；序列为空（非道具内容或空道具槽）时隐藏整个线框。</summary>
    public void SetRecipe(IReadOnlyList<MaterialEnum> recipe)
    {
        ApplyLayoutSettings();
        CacheSlots();
        int count = recipe != null ? recipe.Count : 0;
        if (count == 0 || slots.Count == 0)
        {
            SetVisible(false);
            return;
        }

        int visibleCount = Mathf.Min(count, slots.Count);
        for (int i = 0; i < slots.Count; i++)
        {
            Image slot = slots[i];
            if (slot == null)
                continue;

            bool visible = i < visibleCount;
            slot.gameObject.SetActive(visible);
            if (!visible)
                continue;

            slot.sprite = MagicItemView.GetRecipeIcon(recipe[i]);
            slot.preserveAspect = true;
            slot.color = Color.white;
            slot.raycastTarget = false;
        }

        SetVisible(true);
    }

    public void Hide()
    {
        SetVisible(false);
    }

    private void SetVisible(bool visible)
    {
        GameObject target = visualRoot != null ? visualRoot : gameObject;
        if (target != null && target.activeSelf != visible)
            target.SetActive(visible);
    }

    private void CacheSlots()
    {
        if (slots.Count > 0)
            return;

        for (int i = 0; i < iconSlots.Count; i++)
        {
            if (iconSlots[i] != null)
                slots.Add(iconSlots[i]);
        }
        if (slots.Count > 0)
            return;

        // 未在 Inspector 绑定槽位时，按图标容器下的子物体顺序兜底。
        RectTransform row = iconRow != null ? iconRow : transform as RectTransform;
        if (row == null)
            return;

        for (int i = 0; i < row.childCount; i++)
        {
            Image image = row.GetChild(i).GetComponent<Image>();
            if (image != null)
                slots.Add(image);
        }
    }
}
