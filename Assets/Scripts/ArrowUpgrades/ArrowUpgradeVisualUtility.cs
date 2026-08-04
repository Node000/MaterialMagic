using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public static class ArrowUpgradeVisualUtility
{
    private const string ConfigResourcePath = "Config/ArrowUpgradeVisualConfig";
    private const string OutlineShaderName = "Style/Sprite/Outline";

    private static readonly Dictionary<ArrowUpgradeDirection, Material> materials = new Dictionary<ArrowUpgradeDirection, Material>();
    private static ArrowUpgradeVisualConfig config;

    public static void ApplyTo(Image image, MaterialModel card, PlayerState player, Vector3 baseScale)
    {
        if (image == null)
            return;

        if (card == null || player == null || !ArrowUpgradeSystem.IsDirectionRootUnlocked(player, card.material, out ArrowUpgradeDirection direction))
        {
            image.enabled = false;
            image.rectTransform.localScale = baseScale;
            return;
        }

        ArrowUpgradeVisualProfile profile = GetConfig()?.GetProfile(direction);
        if (profile == null)
        {
            image.enabled = false;
            image.rectTransform.localScale = baseScale;
            return;
        }

        Sprite sprite = MaterialCardView.GetMaterialIcon(card.GetArrowDisplayMaterial());
        if (sprite == null)
        {
            image.enabled = false;
            image.rectTransform.localScale = baseScale;
            return;
        }

        image.sprite = sprite;
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.material = GetMaterial(direction, profile);
        image.rectTransform.localScale = baseScale * profile.iconScale;
        image.enabled = image.material != null;
    }

    private static ArrowUpgradeVisualConfig GetConfig()
    {
        if (config == null)
            config = Resources.Load<ArrowUpgradeVisualConfig>(ConfigResourcePath);
        return config;
    }

    private static Material GetMaterial(ArrowUpgradeDirection direction, ArrowUpgradeVisualProfile profile)
    {
        if (!materials.TryGetValue(direction, out Material material))
        {
            Shader shader = Shader.Find(OutlineShaderName);
            if (shader == null)
                return null;

            material = new Material(shader)
            {
                name = "ArrowUpgradeOutline_" + direction,
                hideFlags = HideFlags.DontSave
            };
            materials.Add(direction, material);
        }

        material.SetColor("_OutlineColor", profile.outlineColor);
        material.SetFloat("_OutlineWidth", profile.outlineWidth);
        material.SetFloat("_PulseSpeed", profile.pulseSpeed);
        material.SetFloat("_PulseAmount", profile.pulseAmount);
        material.SetFloat("_UseOuterGlow", profile.useOuterGlow ? 1f : 0f);
        material.SetColor("_OuterGlowColor", profile.outerGlowColor);
        return material;
    }
}
