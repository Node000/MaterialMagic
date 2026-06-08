using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public static class MaterialModifierVisualUtility
{
    private const string FallbackShaderName = "UI/MaterialModifierAura";
    private const string ResourceMaterialPath = "Materials/MaterialModifierAura";
    private static readonly Dictionary<string, Material> materialCache = new Dictionary<string, Material>();
    private static Material baseMaterial;

    public static void ApplyTo(Image image, MaterialModel card)
    {
        if (image == null)
            return;

        MaterialModifierModel modifier = GetVisualModifier(card, out Color color);
        if (modifier == null)
        {
            image.material = null;
            return;
        }

        image.material = GetMaterial(modifier, color);
    }

    private static MaterialModifierModel GetVisualModifier(MaterialModel card, out Color color)
    {
        color = Color.white;
        MaterialModifierModel result = null;
        if (card == null || card.modifiers == null)
            return null;

        for (int i = 0; i < card.modifiers.Count; i++)
        {
            MaterialModifierModel modifier = card.modifiers[i];
            if (modifier != null && MaterialModifierDisplayDatabase.TryGetLineColor(modifier, out Color modifierColor))
            {
                result = modifier;
                color = modifierColor;
            }
        }

        return result;
    }

    private static Material GetMaterial(MaterialModifierModel modifier, Color color)
    {
        string key = GetMaterialKey(modifier, color);
        if (materialCache.TryGetValue(key, out Material cached))
            return cached;

        Material material = CreateMaterial();
        material.name = "MaterialModifierAura_" + modifier.GetType().Name;
        material.hideFlags = HideFlags.DontSave;
        ConfigureMaterial(material, modifier, color);
        materialCache.Add(key, material);
        return material;
    }

    private static Material CreateMaterial()
    {
        if (baseMaterial == null)
            baseMaterial = Resources.Load<Material>(ResourceMaterialPath);

        if (baseMaterial != null)
            return new Material(baseMaterial);

        Shader shader = Shader.Find(FallbackShaderName);
        return shader != null ? new Material(shader) : null;
    }

    private static void ConfigureMaterial(Material material, MaterialModifierModel modifier, Color color)
    {
        if (material == null)
            return;

        color.a = 1f;
        material.SetColor("_AuraColor", color);
        material.SetFloat("_PulseSpeed", GetPulseSpeed(modifier));
        material.SetFloat("_PulseStrength", GetPulseStrength(modifier));
        material.SetFloat("_SweepSpeed", GetSweepSpeed(modifier));
        material.SetFloat("_SweepWidth", GetSweepWidth(modifier));
        material.SetFloat("_SweepIntensity", GetSweepIntensity(modifier));
        material.SetFloat("_EdgeIntensity", GetEdgeIntensity(modifier));
    }

    private static string GetMaterialKey(MaterialModifierModel modifier, Color color)
    {
        Color32 color32 = color;
        return modifier.GetType().Name + "_" + color32.r + "_" + color32.g + "_" + color32.b;
    }

    private static float GetPulseSpeed(MaterialModifierModel modifier)
    {
        if (modifier is VortexModifier || modifier is RandomArrowModifier)
            return 1.45f;
        if (modifier is FragileArrowModifier || modifier is HalfArrowModifier)
            return 4.2f;
        if (modifier is EternalArrowModifier || modifier is RetainedArrowModifier)
            return 1.75f;
        if (modifier is ChargeModifier || modifier is ReturnArrowModifier)
            return 3.1f;
        if (modifier is KindlingModifier)
            return 2.8f;
        return 2.1f;
    }

    private static float GetPulseStrength(MaterialModifierModel modifier)
    {
        if (modifier is TemporaryModifier)
            return 0.08f;
        if (modifier is HalfArrowModifier)
            return 0.06f;
        if (modifier is FragileArrowModifier)
            return 0.24f;
        if (modifier is RetainedArrowModifier || modifier is EternalArrowModifier)
            return 0.14f;
        if (modifier is SturdyModifier || modifier is BigArrow2Modifier || modifier is BigArrow3Modifier || modifier is BigArrow4Modifier)
            return 0.12f;
        if (modifier is KindlingModifier || modifier is ChargeModifier)
            return 0.22f;
        return 0.16f;
    }

    private static float GetSweepSpeed(MaterialModifierModel modifier)
    {
        if (modifier is VortexModifier || modifier is RandomArrowModifier)
            return 0.85f;
        if (modifier is FragileArrowModifier || modifier is HalfArrowModifier)
            return 2.35f;
        if (modifier is EternalArrowModifier || modifier is RetainedArrowModifier)
            return 0.72f;
        if (modifier is FlowModifier || modifier is LiquefyModifier)
            return 1.3f;
        if (modifier is ChargeModifier || modifier is ReturnArrowModifier)
            return 1.85f;
        return 1.1f;
    }

    private static float GetSweepWidth(MaterialModifierModel modifier)
    {
        if (modifier is BigArrow2Modifier || modifier is BigArrow3Modifier || modifier is BigArrow4Modifier)
            return 0.18f;
        if (modifier is HalfArrowModifier)
            return 0.07f;
        if (modifier is FragileArrowModifier)
            return 0.06f;
        if (modifier is EternalArrowModifier || modifier is RetainedArrowModifier)
            return 0.14f;
        if (modifier is HeavyArrowModifier)
            return 0.15f;
        return 0.11f;
    }

    private static float GetSweepIntensity(MaterialModifierModel modifier)
    {
        if (modifier is TemporaryModifier || modifier is SturdyModifier)
            return 0.3f;
        if (modifier is HalfArrowModifier)
            return 0.24f;
        if (modifier is FragileArrowModifier)
            return 0.82f;
        if (modifier is EternalArrowModifier || modifier is RetainedArrowModifier)
            return 0.48f;
        if (modifier is ReturnArrowModifier || modifier is RandomArrowModifier)
            return 0.75f;
        return 0.55f;
    }

    private static float GetEdgeIntensity(MaterialModifierModel modifier)
    {
        if (modifier is BigArrow2Modifier || modifier is BigArrow3Modifier || modifier is BigArrow4Modifier)
            return 0.65f;
        if (modifier is FragileArrowModifier || modifier is HalfArrowModifier)
            return 0.72f;
        if (modifier is EternalArrowModifier || modifier is RetainedArrowModifier)
            return 0.52f;
        if (modifier is ProliferatingArrowModifier)
            return 0.58f;
        if (modifier is TemporaryModifier)
            return 0.25f;
        return 0.45f;
    }
}
