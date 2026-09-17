using TMPro;
using UnityEngine;

/// <summary>
/// 统一详情面板里的「箭头附魔」内容项：附魔图标（两层合成，见 <see cref="EnchantIconUI"/>）+ 图标下方的附魔名字。
/// 布局与字号参考「道具强化选择面板」的选项（图标在上、名字在下），具体数值都在预制体上：
/// <c>Assets/Prefabs/UI/EnchantEntry.prefab</c>；本脚本只负责按附魔 id 刷新显示。
/// </summary>
public class EnchantEntryUI : MonoBehaviour
{
    [Tooltip("附魔图标层（Assets/Prefabs/UI/EnchantIcon.prefab）：颜色由 Enchant_Color 配置决定")]
    [SerializeField] private EnchantIconUI icon;

    [Tooltip("图标下方的附魔名字（本地化 nameKey；取不到数据时退回 id）")]
    [SerializeField] private TMP_Text nameText;

    /// <summary>按附魔 id 刷新图标颜色与名字。语言切换时由调用方重新应用即可。</summary>
    public void Apply(string modifierId)
    {
        if (icon != null)
            icon.Apply(modifierId);

        if (nameText != null)
            nameText.text = ResolveName(modifierId);
    }

    private static string ResolveName(string modifierId)
    {
        if (string.IsNullOrEmpty(modifierId))
            return string.Empty;

        if (MaterialModifierDatabase.TryGetData(modifierId, out MaterialModifierData data) && data != null)
            return LocalizationSystem.GetText(data.nameKey, data.id);

        return modifierId;
    }
}
