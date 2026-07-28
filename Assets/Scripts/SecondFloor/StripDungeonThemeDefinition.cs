using UnityEngine;

[CreateAssetMenu(fileName = "StripDungeonTheme", menuName = "Config/Second Floor Strip Dungeon Theme")]
public class StripDungeonThemeDefinition : ScriptableObject
{
    [SerializeField] private string themeId = "default";
    [SerializeField] private GameObject[] floorPrefabs;
    [SerializeField] private GameObject[] wallPrefabs;
    [SerializeField, Range(0, 100)] private int wallDecorationChance;
    [SerializeField] private StripDungeonWallDecorationDefinition[] wallDecorations;

    public string ThemeId => themeId;
    public GameObject[] FloorPrefabs => floorPrefabs;
    public GameObject[] WallPrefabs => wallPrefabs;
    public int WallDecorationChance => wallDecorationChance;

    public bool TryGetWallDecoration(int maxFootprintCells, int variantHash, out StripDungeonWallDecorationDefinition decoration)
    {
        if (wallDecorations == null || wallDecorations.Length == 0)
        {
            decoration = null;
            return false;
        }

        int validCount = 0;
        for (int i = 0; i < wallDecorations.Length; i++)
        {
            StripDungeonWallDecorationDefinition candidate = wallDecorations[i];
            if (candidate != null && candidate.prefab != null && candidate.footprintCells <= maxFootprintCells)
                validCount++;
        }

        if (validCount == 0)
        {
            decoration = null;
            return false;
        }

        int selectedIndex = (variantHash & int.MaxValue) % validCount;
        for (int i = 0; i < wallDecorations.Length; i++)
        {
            StripDungeonWallDecorationDefinition candidate = wallDecorations[i];
            if (candidate == null || candidate.prefab == null || candidate.footprintCells > maxFootprintCells)
                continue;
            if (selectedIndex-- == 0)
            {
                decoration = candidate;
                return true;
            }
        }

        decoration = null;
        return false;
    }

    public bool TryValidate(out string error)
    {
        if (string.IsNullOrWhiteSpace(themeId))
        {
            error = "主题 ID 不能为空。";
            return false;
        }

        if (!HasPrefab(floorPrefabs))
        {
            error = $"主题 {themeId} 至少需要一个地面 Prefab。";
            return false;
        }

        if (!HasPrefab(wallPrefabs))
        {
            error = $"主题 {themeId} 至少需要一个墙壁 Prefab。";
            return false;
        }

        if (wallDecorationChance > 0 && !HasUsableWallDecoration())
        {
            error = $"主题 {themeId} 配置了墙体装饰概率，但没有可用装饰 Prefab。";
            return false;
        }

        if (wallDecorations != null)
        {
            for (int i = 0; i < wallDecorations.Length; i++)
            {
                StripDungeonWallDecorationDefinition decoration = wallDecorations[i];
                if (decoration == null || decoration.prefab == null || decoration.footprintCells < 1 || decoration.surfaceOffset < 0f)
                {
                    error = $"主题 {themeId} 的墙体装饰配置无效。";
                    return false;
                }
            }
        }

        error = null;
        return true;
    }

    private bool HasUsableWallDecoration()
    {
        if (wallDecorations == null)
            return false;

        for (int i = 0; i < wallDecorations.Length; i++)
        {
            if (wallDecorations[i] != null && wallDecorations[i].prefab != null)
                return true;
        }
        return false;
    }

    private static bool HasPrefab(GameObject[] prefabs)
    {
        if (prefabs == null || prefabs.Length == 0)
            return false;

        for (int i = 0; i < prefabs.Length; i++)
        {
            if (prefabs[i] != null)
                return true;
        }
        return false;
    }
}

[System.Serializable]
public class StripDungeonWallDecorationDefinition
{
    public GameObject prefab;
    [Min(1)] public int footprintCells = 1;
    [Min(0f)] public float surfaceOffset;
}
