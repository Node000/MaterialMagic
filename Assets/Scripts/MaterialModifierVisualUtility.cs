using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public static class MaterialModifierVisualUtility
{
    private const string AuraShaderName = "UI/MaterialModifierAura";
    private const string ShapeShaderName = "UI/MaterialModifierArrowShape";
    private const string ScreenShaderName = "UI/MaterialModifierScreenEffect";
    private const string ElementShaderName = "UI/MaterialModifierElementAura";
    private const string DefaultAuraMaterialPath = "Materials/MaterialModifierAura";
    private const string ModifierMaterialFolder = "Materials/MaterialModifiers";

    private static readonly Dictionary<string, Material> materialCache = new Dictionary<string, Material>();
    private static readonly Dictionary<MaterialEnum, Texture> arrowTextureCache = new Dictionary<MaterialEnum, Texture>();
    private static Material baseMaterial;

    private struct VisualProfile
    {
        public string ShaderName;
        public float Mode;
        public float CopyCount;
        public float Speed;
        public float Strength;
        public bool UsesArrowCycleTextures;
    }

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

        image.material = GetMaterial(modifier, color, card);
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
            if (modifier == null || !TryGetVisualProfile(modifier, out _))
                continue;

            result = modifier;
            if (!MaterialModifierDisplayDatabase.TryGetLineColor(modifier, out color))
                color = GetFallbackColor(modifier);
        }

        return result;
    }

    private static Material GetMaterial(MaterialModifierModel modifier, Color color, MaterialModel card)
    {
        if (!TryGetVisualProfile(modifier, out VisualProfile profile))
            return null;

        float arrowDirection = GetArrowDirection(card);
        string key = GetMaterialKey(modifier, color, profile, arrowDirection);
        if (materialCache.TryGetValue(key, out Material cached))
            return cached;

        Material material = CreateMaterial(modifier, profile, out bool fromAsset);
        if (material == null)
            return null;

        material.name = "MaterialModifierVisual_" + modifier.GetType().Name;
        material.hideFlags = HideFlags.DontSave;
        ConfigureMaterial(material, modifier, color, profile, arrowDirection, fromAsset);
        materialCache.Add(key, material);
        return material;
    }

    private static Material CreateMaterial(MaterialModifierModel modifier, VisualProfile profile, out bool fromAsset)
    {
        fromAsset = false;
        Material template = Resources.Load<Material>(GetModifierMaterialPath(modifier));
        if (template != null)
        {
            fromAsset = true;
            return new Material(template);
        }

        return CreateMaterial(profile.ShaderName);
    }

    private static string GetModifierMaterialPath(MaterialModifierModel modifier)
    {
        return ModifierMaterialFolder + "/" + modifier.GetType().Name;
    }

    private static Material CreateMaterial(string shaderName)
    {
        Shader shader = Shader.Find(shaderName);
        if (shader != null)
            return new Material(shader);

        if (baseMaterial == null)
            baseMaterial = Resources.Load<Material>(DefaultAuraMaterialPath);

        if (baseMaterial != null)
            return new Material(baseMaterial);

        shader = Shader.Find(AuraShaderName);
        return shader != null ? new Material(shader) : null;
    }

    private static void ConfigureMaterial(Material material, MaterialModifierModel modifier, Color color, VisualProfile profile, float arrowDirection, bool fromAsset)
    {
        if (material == null)
            return;

        if (fromAsset)
        {
            ConfigureRuntimeMaterialProperties(material, profile, arrowDirection);
            return;
        }

        color.a = 1f;
        SetColor(material, "_AuraColor", color);

        if (material.HasProperty("_EffectMode"))
        {
            SetFloat(material, "_EffectMode", profile.Mode);
            SetFloat(material, "_EffectSpeed", profile.Speed);
            SetFloat(material, "_EffectStrength", profile.Strength);
            SetFloat(material, "_CopyCount", profile.CopyCount);
            ConfigureRuntimeMaterialProperties(material, profile, arrowDirection);
            return;
        }

        ConfigureAuraMaterial(material, modifier, color);
    }

    private static void ConfigureRuntimeMaterialProperties(Material material, VisualProfile profile, float arrowDirection)
    {
        SetFloat(material, "_ArrowDirection", arrowDirection);

        if (profile.UsesArrowCycleTextures)
            ConfigureArrowCycleTextures(material);
    }

    private static void ConfigureAuraMaterial(Material material, MaterialModifierModel modifier, Color color)
    {
        SetColor(material, "_AuraColor", color);
        SetFloat(material, "_PulseSpeed", GetPulseSpeed(modifier));
        SetFloat(material, "_PulseStrength", GetPulseStrength(modifier));
        SetFloat(material, "_SweepSpeed", GetSweepSpeed(modifier));
        SetFloat(material, "_SweepWidth", GetSweepWidth(modifier));
        SetFloat(material, "_SweepIntensity", GetSweepIntensity(modifier));
        SetFloat(material, "_EdgeIntensity", GetEdgeIntensity(modifier));
    }

    private static void ConfigureArrowCycleTextures(Material material)
    {
        SetTexture(material, "_AltTex1", GetArrowTexture(MaterialEnum.Fire));
        SetTexture(material, "_AltTex2", GetArrowTexture(MaterialEnum.Water));
        SetTexture(material, "_AltTex3", GetArrowTexture(MaterialEnum.Wind));
        SetTexture(material, "_AltTex4", GetArrowTexture(MaterialEnum.Earth));
    }

    private static void SetColor(Material material, string propertyName, Color value)
    {
        if (material.HasProperty(propertyName))
            material.SetColor(propertyName, value);
    }

    private static void SetFloat(Material material, string propertyName, float value)
    {
        if (material.HasProperty(propertyName))
            material.SetFloat(propertyName, value);
    }

    private static void SetTexture(Material material, string propertyName, Texture texture)
    {
        if (texture != null && material.HasProperty(propertyName))
            material.SetTexture(propertyName, texture);
    }

    private static Texture GetArrowTexture(MaterialEnum material)
    {
        if (arrowTextureCache.TryGetValue(material, out Texture cached))
            return cached;

        string path = GetArrowTexturePath(material);
        Texture texture = !string.IsNullOrEmpty(path) ? Resources.Load<Texture>(path) : null;
        arrowTextureCache[material] = texture;
        return texture;
    }

    private static string GetArrowTexturePath(MaterialEnum material)
    {
        switch (material)
        {
            case MaterialEnum.Fire:
                return "Images/UI/1";
            case MaterialEnum.Water:
                return "Images/UI/2";
            case MaterialEnum.Wind:
                return "Images/UI/3";
            case MaterialEnum.Earth:
                return "Images/UI/4";
            default:
                return null;
        }
    }

    private static string GetMaterialKey(MaterialModifierModel modifier, Color color, VisualProfile profile, float arrowDirection)
    {
        Color32 color32 = color;
        return profile.ShaderName + "_" + profile.Mode + "_" + profile.CopyCount + "_" + arrowDirection + "_" + modifier.GetType().Name + "_" + color32.r + "_" + color32.g + "_" + color32.b;
    }

    private static float GetArrowDirection(MaterialModel card)
    {
        MaterialEnum material = card != null ? card.GetArrowDisplayMaterial() : MaterialEnum.None;
        if (material == MaterialEnum.None && card != null)
            material = card.material;

        switch (material)
        {
            case MaterialEnum.Fire:
                return 0f;
            case MaterialEnum.Water:
                return 1f;
            case MaterialEnum.Wind:
                return 2f;
            case MaterialEnum.Earth:
                return 3f;
            default:
                return 0f;
        }
    }

    private static bool TryGetVisualProfile(MaterialModifierModel modifier, out VisualProfile profile)
    {
        profile = new VisualProfile
        {
            ShaderName = AuraShaderName,
            Mode = 0f,
            CopyCount = 1f,
            Speed = 1.6f,
            Strength = 0.35f,
            UsesArrowCycleTextures = false
        };

        if (modifier is HalfArrowModifier)
        {
            profile.ShaderName = ShapeShaderName;
            profile.Mode = 0f;
            profile.Speed = 1.5f;
            profile.Strength = 0.55f;
            return true;
        }
        if (modifier is FragileArrowModifier)
        {
            profile.ShaderName = ShapeShaderName;
            profile.Mode = 1f;
            profile.Speed = 4.2f;
            profile.Strength = 0.7f;
            return true;
        }
        if (modifier is RepeatArrowModifier)
        {
            profile.ShaderName = ShapeShaderName;
            profile.Mode = 2f;
            profile.Speed = 1.2f;
            profile.Strength = 0.4f;
            return true;
        }
        if (modifier is BigArrow2Modifier || modifier is BigArrow3Modifier || modifier is BigArrow4Modifier)
        {
            profile.ShaderName = ShapeShaderName;
            profile.Mode = 3f;
            profile.CopyCount = GetBigArrowCopyCount(modifier);
            profile.Speed = 1f;
            profile.Strength = 0.42f;
            return true;
        }
        if (modifier is ProliferatingArrowModifier)
        {
            profile.ShaderName = ShapeShaderName;
            profile.Mode = 4f;
            profile.Speed = 2.1f;
            profile.Strength = 0.55f;
            return true;
        }
        if (modifier is ReturnArrowModifier)
        {
            profile.ShaderName = ShapeShaderName;
            profile.Mode = 5f;
            profile.Speed = 1.4f;
            profile.Strength = 0.4f;
            return true;
        }
        if (modifier is RandomArrowModifier)
        {
            profile.ShaderName = ShapeShaderName;
            profile.Mode = 6f;
            profile.Speed = 2.3f;
            profile.Strength = 0.5f;
            profile.UsesArrowCycleTextures = true;
            return true;
        }

        if (modifier is RetainedArrowModifier)
        {
            profile.ShaderName = ScreenShaderName;
            profile.Mode = 0f;
            profile.Speed = 1.35f;
            profile.Strength = 0.38f;
            return true;
        }
        if (modifier is EternalArrowModifier)
        {
            profile.ShaderName = ScreenShaderName;
            profile.Mode = 1f;
            profile.Speed = 1.15f;
            profile.Strength = 0.85f;
            return true;
        }
        if (modifier is DoomModifier)
        {
            profile.ShaderName = ScreenShaderName;
            profile.Mode = 2f;
            profile.Speed = 1.1f;
            profile.Strength = 0.7f;
            return true;
        }
        if (modifier is TemporaryModifier)
        {
            profile.ShaderName = ScreenShaderName;
            profile.Mode = 3f;
            profile.Speed = 1.45f;
            profile.Strength = 0.5f;
            return true;
        }
        if (modifier is LazyModifier)
        {
            profile.ShaderName = ScreenShaderName;
            profile.Mode = 4f;
            profile.Speed = 1.0f;
            profile.Strength = 0.6f;
            return true;
        }
        if (modifier is VortexModifier)
        {
            profile.ShaderName = ScreenShaderName;
            profile.Mode = 5f;
            profile.Speed = 1.2f;
            profile.Strength = 0.72f;
            return true;
        }

        if (modifier is SturdyModifier)
        {
            profile.ShaderName = ElementShaderName;
            profile.Mode = 0f;
            profile.Speed = 1f;
            profile.Strength = 0.45f;
            return true;
        }
        if (modifier is KindlingModifier)
        {
            profile.ShaderName = ElementShaderName;
            profile.Mode = 1f;
            profile.Speed = 1.6f;
            profile.Strength = 0.6f;
            return true;
        }
        if (modifier is ChargeModifier)
        {
            profile.ShaderName = ElementShaderName;
            profile.Mode = 2f;
            profile.Speed = 1.8f;
            profile.Strength = 0.65f;
            return true;
        }
        if (modifier is FlowModifier)
        {
            profile.ShaderName = ElementShaderName;
            profile.Mode = 3f;
            profile.Speed = 1.2f;
            profile.Strength = 0.42f;
            return true;
        }
        if (modifier is LiquefyModifier)
        {
            profile.ShaderName = ElementShaderName;
            profile.Mode = 4f;
            profile.Speed = 1.25f;
            profile.Strength = 0.55f;
            return true;
        }

        return MaterialModifierDisplayDatabase.TryGetLineColor(modifier, out _);
    }

    private static float GetBigArrowCopyCount(MaterialModifierModel modifier)
    {
        if (modifier is BigArrow4Modifier)
            return 4f;
        if (modifier is BigArrow3Modifier)
            return 3f;
        return 2f;
    }

    private static Color GetFallbackColor(MaterialModifierModel modifier)
    {
        if (modifier is DoomModifier)
            return new Color(0.45f, 0.45f, 0.48f, 1f);
        if (modifier is ReturnArrowModifier)
            return Color.white;
        return Color.white;
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
        if (modifier is RepeatArrowModifier)
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
