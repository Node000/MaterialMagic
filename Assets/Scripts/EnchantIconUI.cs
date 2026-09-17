using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 箭头附魔图标：两层 <see cref="Image"/>（上层 + 底色）合成一个图标。
/// 两层颜色来自 <see cref="EnchantColorConfig"/>（<c>Assets/Resources/Config/Enchant_Color.asset</c>）；
/// 没配到颜色的附魔保留预制体上美术设置的颜色，代码不写死任何颜色。
/// 预制体：<c>Assets/Prefabs/UI/EnchantIcon.prefab</c>。
/// </summary>
public class EnchantIconUI : MonoBehaviour
{
    [Tooltip("底色层（画在下层）")]
    [SerializeField] private Image baseLayer;

    [Tooltip("上层（画在上层）")]
    [SerializeField] private Image topLayer;

    /// <summary>按附魔数据刷新两层颜色。</summary>
    public void Apply(MaterialModifierData data)
    {
        Apply(data != null ? data.id : null);
    }

    /// <summary>按附魔 id 刷新两层颜色；未配置颜色时保持预制体颜色不变。</summary>
    public void Apply(string modifierId)
    {
        Color topColor;
        Color baseColor;
        if (!EnchantColorConfig.TryGetColors(modifierId, out topColor, out baseColor))
            return;

        if (baseLayer != null)
            baseLayer.color = baseColor;
        if (topLayer != null)
            topLayer.color = topColor;
    }
}
