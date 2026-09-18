using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 箭头附魔图标：两层 <see cref="Image"/>（上层 + 底色）合成一个图标。
/// 两层颜色来自 <see cref="EnchantColorConfig"/>（<c>Assets/Resources/Config/Enchant_Color.asset</c>）；
/// 没配到颜色的附魔保留预制体上美术设置的颜色，代码不写死任何颜色。
///
/// 视觉效果（<see cref="applyVisualEffect"/>，默认开）：把该附魔的视觉材质
/// （<c>Assets/Resources/Materials/MaterialModifiers/*.mat</c>，Shader 家族 <c>UI/MaterialModifiers/*</c>，
/// 与箭头卡上用的是同一份）套到两层图片上。做法是「一个组件管两层」：
/// 在图标根物体上按附魔建一份材质实例，两层图片共用它（<c>_MainTex</c> 由 UGUI 按各自 Sprite 传，
/// 材质里是 <c>[PerRendererData] _MainTex</c>），每层颜色仍由 <c>Image.color</c> 提供（Shader 里乘 vertexColor），
/// 所以颜色配置照旧生效，效果参数/动画也与卡上完全一致。没有配视觉材质的附魔保持原样。
///
/// 预制体：<c>Assets/Prefabs/UI/EnchantIcon.prefab</c>。
/// </summary>
public class EnchantIconUI : MonoBehaviour
{
    [Tooltip("底色层（画在下层）")]
    [SerializeField] private Image baseLayer;

    [Tooltip("上层（画在上层）")]
    [SerializeField] private Image topLayer;

    [Tooltip("是否把该附魔的视觉效果材质套到两层图片上（与箭头卡同一套 UI/MaterialModifiers Shader）；没配视觉材质的附魔保持原样。")]
    [SerializeField] private bool applyVisualEffect = true;

    private string appliedModifierId;
    private Material effectMaterial;

    /// <summary>按附魔数据刷新两层颜色与视觉效果。</summary>
    public void Apply(MaterialModifierData data)
    {
        Apply(data != null ? data.id : null);
    }

    /// <summary>按附魔 id 刷新两层颜色（未配置颜色时保持预制体颜色）与视觉效果材质。</summary>
    public void Apply(string modifierId)
    {
        ApplyColors(modifierId);
        ApplyVisualEffect(modifierId);
    }

    private void ApplyColors(string modifierId)
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

    private void ApplyVisualEffect(string modifierId)
    {
        if (!applyVisualEffect)
        {
            ReleaseVisualEffect();
            appliedModifierId = null;
            return;
        }

        // 面板/悬停会反复 Apply 同一个附魔，只在附魔变化时动材质。
        if (appliedModifierId == modifierId && effectMaterial != null)
            return;

        appliedModifierId = modifierId;
        ReleaseVisualEffect();
        if (string.IsNullOrEmpty(modifierId))
            return;
        if (!MaterialModifierDisplayDatabase.TryGetVisualMaterial(modifierId, out Material template)
            || template == null
            || template.shader == null)
        {
            return;
        }

        // new Material(template) 会继承材质上美术调好的全部参数（_AuraColor / _EffectSpeed / _EffectStrength 等），
        // 动画由 Shader 自己按 _Time 驱动。
        effectMaterial = new Material(template)
        {
            name = "EnchantIconEffect_" + modifierId,
            hideFlags = HideFlags.DontSave
        };
        AssignLayerMaterial(baseLayer, effectMaterial);
        AssignLayerMaterial(topLayer, effectMaterial);
    }

    private static void AssignLayerMaterial(Image layer, Material material)
    {
        if (layer != null)
            layer.material = material;
    }

    private void ReleaseVisualEffect()
    {
        if (effectMaterial == null)
            return;

        if (baseLayer != null && baseLayer.material == effectMaterial)
            baseLayer.material = null;
        if (topLayer != null && topLayer.material == effectMaterial)
            topLayer.material = null;

        if (Application.isPlaying)
            Destroy(effectMaterial);
        else
            DestroyImmediate(effectMaterial);
        effectMaterial = null;
    }

    private void OnDestroy()
    {
        ReleaseVisualEffect();
    }
}
