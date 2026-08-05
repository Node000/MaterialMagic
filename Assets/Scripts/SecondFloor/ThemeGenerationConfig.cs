using UnityEngine;

[CreateAssetMenu(fileName = "GeneralThemeGenConfig", menuName = "Config/Second Floor/General Theme Gen Config")]
public class GeneralThemeGenConfig : ScriptableObject
{
    [Header("天花板")]
    [SerializeField, Min(0.1f)] private float ceilingHeight = 3.2f;
    [SerializeField, Min(0.05f)] private float ceilingThickness = 0.2f;
    [SerializeField] private GameObject[] ceilingPrefabs;

    [Header("墙体 Prefab 基准")]
    [SerializeField, Min(0.01f)] private float wallPrefabWidth = 3f;
    [SerializeField, Min(0.01f)] private float wallPrefabHeight = 3.2f;

    public float CeilingHeight => ceilingHeight;
    public float CeilingThickness => ceilingThickness;
    public GameObject[] CeilingPrefabs => ceilingPrefabs;
    public float WallPrefabWidth => wallPrefabWidth;
    public float WallPrefabHeight => wallPrefabHeight;

    public float GetCeilingHeight(ThemeUniqueGenConfig uniqueConfig)
    {
        return uniqueConfig != null && uniqueConfig.OverrideCeilingHeight
            ? uniqueConfig.CeilingHeight
            : ceilingHeight;
    }

    public float GetCeilingThickness(ThemeUniqueGenConfig uniqueConfig)
    {
        return uniqueConfig != null && uniqueConfig.OverrideCeilingThickness
            ? uniqueConfig.CeilingThickness
            : ceilingThickness;
    }

    public GameObject[] GetCeilingPrefabs(ThemeUniqueGenConfig uniqueConfig)
    {
        return uniqueConfig != null && uniqueConfig.OverrideCeilingPrefabs
            ? uniqueConfig.CeilingPrefabs
            : ceilingPrefabs;
    }

    public float GetWallPrefabWidth(ThemeUniqueGenConfig uniqueConfig)
    {
        return uniqueConfig != null && uniqueConfig.OverrideWallPrefabWidth
            ? uniqueConfig.WallPrefabWidth
            : wallPrefabWidth;
    }

    public float GetWallPrefabHeight(ThemeUniqueGenConfig uniqueConfig)
    {
        return uniqueConfig != null && uniqueConfig.OverrideWallPrefabHeight
            ? uniqueConfig.WallPrefabHeight
            : wallPrefabHeight;
    }

    public bool TryValidate(out string error)
    {
        if (ceilingHeight <= 0f || ceilingThickness <= 0f || wallPrefabWidth <= 0f || wallPrefabHeight <= 0f)
        {
            error = "默认主题生成配置的尺寸必须大于 0。";
            return false;
        }

        if (!HasPrefab(ceilingPrefabs))
        {
            error = "默认主题生成配置至少需要一个天花板 Prefab。";
            return false;
        }

        error = null;
        return true;
    }

    private static bool HasPrefab(GameObject[] prefabs)
    {
        if (prefabs == null)
            return false;

        for (int i = 0; i < prefabs.Length; i++)
        {
            if (prefabs[i] != null)
                return true;
        }

        return false;
    }
}

[CreateAssetMenu(fileName = "ThemeUniqueGenConfig", menuName = "Config/Second Floor/Theme Unique Gen Config")]
public class ThemeUniqueGenConfig : ScriptableObject
{
    [Header("天花板")]
    [SerializeField] private bool overrideCeilingHeight;
    [SerializeField, Min(0.1f)] private float ceilingHeight = 3.2f;
    [SerializeField] private bool overrideCeilingThickness;
    [SerializeField, Min(0.05f)] private float ceilingThickness = 0.2f;
    [SerializeField] private bool overrideCeilingPrefabs;
    [SerializeField] private GameObject[] ceilingPrefabs;

    [Header("墙体 Prefab 基准")]
    [SerializeField] private bool overrideWallPrefabWidth;
    [SerializeField, Min(0.01f)] private float wallPrefabWidth = 3f;
    [SerializeField] private bool overrideWallPrefabHeight;
    [SerializeField, Min(0.01f)] private float wallPrefabHeight = 3.2f;

    public bool OverrideCeilingHeight => overrideCeilingHeight;
    public float CeilingHeight => ceilingHeight;
    public bool OverrideCeilingThickness => overrideCeilingThickness;
    public float CeilingThickness => ceilingThickness;
    public bool OverrideCeilingPrefabs => overrideCeilingPrefabs;
    public GameObject[] CeilingPrefabs => ceilingPrefabs;
    public bool OverrideWallPrefabWidth => overrideWallPrefabWidth;
    public float WallPrefabWidth => wallPrefabWidth;
    public bool OverrideWallPrefabHeight => overrideWallPrefabHeight;
    public float WallPrefabHeight => wallPrefabHeight;

    public bool TryValidate(out string error)
    {
        if (overrideCeilingHeight && ceilingHeight <= 0f)
        {
            error = "主题天花板覆盖高度必须大于 0。";
            return false;
        }

        if (overrideCeilingThickness && ceilingThickness <= 0f)
        {
            error = "主题天花板覆盖厚度必须大于 0。";
            return false;
        }

        if (overrideCeilingPrefabs && !HasPrefab(ceilingPrefabs))
        {
            error = "主题覆盖天花板 Prefab 时至少需要配置一个 Prefab。";
            return false;
        }

        if (overrideWallPrefabWidth && wallPrefabWidth <= 0f || overrideWallPrefabHeight && wallPrefabHeight <= 0f)
        {
            error = "主题覆盖墙体 Prefab 尺寸时，尺寸必须大于 0。";
            return false;
        }

        error = null;
        return true;
    }

    private static bool HasPrefab(GameObject[] prefabs)
    {
        if (prefabs == null)
            return false;

        for (int i = 0; i < prefabs.Length; i++)
        {
            if (prefabs[i] != null)
                return true;
        }

        return false;
    }
}
