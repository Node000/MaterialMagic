using UnityEngine;

[CreateAssetMenu(fileName = "MaterialVisualPalette", menuName = "Config/Material Visual Palette")]
public class MaterialVisualPalette : ScriptableObject
{
    private const string ResourcePath = "Config/MaterialVisualPalette";

    private static MaterialVisualPalette cached;

    [Header("基础材质颜色")]
    [SerializeField] private Color noneColor = Color.gray;
    [SerializeField] private Color fireColor = new Color(0.9f, 0.22f, 0.12f, 1f);
    [SerializeField] private Color windColor = new Color(0.25f, 0.85f, 0.45f, 1f);
    [SerializeField] private Color waterColor = new Color(0.2f, 0.5f, 1f, 1f);
    [SerializeField] private Color earthColor = new Color(0.62f, 0.42f, 0.2f, 1f);
    [SerializeField] private Color wildColor = new Color(0.8f, 0.45f, 1f, 1f);

    [Header("法术槽背景")]
    [SerializeField] private Color magicBackgroundBaseColor = new Color(0.08f, 0.08f, 0.12f, 1f);
    [SerializeField] [Range(0f, 1f)] private float magicBackgroundBlend = 0.42f;

    [Header("投射物颜色派生")]
    [SerializeField] [Range(0f, 2f)] private float trailBrightness = 1f;
    [SerializeField] [Range(-1f, 1f)] private float trailAdd = 0f;
    [SerializeField] [Range(0f, 1f)] private float trailAlpha = 1f;
    [SerializeField] [Range(0f, 2f)] private float impactBrightness = 1f;
    [SerializeField] [Range(-1f, 1f)] private float impactAdd = 0.18f;
    [SerializeField] [Range(0f, 1f)] private float impactAlpha = 1f;

    public static MaterialVisualPalette Active
    {
        get
        {
            if (cached == null)
            {
                cached = Resources.Load<MaterialVisualPalette>(ResourcePath);
                if (cached == null)
                {
                    cached = CreateInstance<MaterialVisualPalette>();
                    cached.hideFlags = HideFlags.HideAndDontSave;
                }
            }

            return cached;
        }
    }

    public Color GetMaterialColor(MaterialEnum material)
    {
        switch (material)
        {
            case MaterialEnum.Fire:
                return fireColor;
            case MaterialEnum.Wind:
                return windColor;
            case MaterialEnum.Water:
                return waterColor;
            case MaterialEnum.Earth:
                return earthColor;
            case MaterialEnum.Wild:
                return wildColor;
            default:
                return noneColor;
        }
    }

    public Color GetTrailColor(MaterialEnum material)
    {
        return GetTrailColor(GetMaterialColor(material));
    }

    public Color GetTrailColor(Color sourceColor)
    {
        return ApplyColorAdjustment(sourceColor, trailBrightness, trailAdd, trailAlpha);
    }

    public Color GetImpactColor(MaterialEnum material)
    {
        return GetImpactColor(GetMaterialColor(material));
    }

    public Color GetImpactColor(Color sourceColor)
    {
        return ApplyColorAdjustment(sourceColor, impactBrightness, impactAdd, impactAlpha);
    }

    public Color GetMagicBackgroundColor(MaterialEnum material)
    {
        Color color = Color.Lerp(magicBackgroundBaseColor, GetMaterialColor(material), Mathf.Clamp01(magicBackgroundBlend));
        color.a = 1f;
        return color;
    }

    private static Color ApplyColorAdjustment(Color sourceColor, float brightness, float add, float alpha)
    {
        brightness = Mathf.Max(0f, brightness);
        return new Color(
            Mathf.Clamp01(sourceColor.r * brightness + add),
            Mathf.Clamp01(sourceColor.g * brightness + add),
            Mathf.Clamp01(sourceColor.b * brightness + add),
            Mathf.Clamp01(sourceColor.a * alpha));
    }
}
