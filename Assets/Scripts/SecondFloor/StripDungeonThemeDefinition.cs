using UnityEngine;

[CreateAssetMenu(fileName = "StripDungeonTheme", menuName = "Config/Second Floor Strip Dungeon Theme")]
public class StripDungeonThemeDefinition : ScriptableObject
{
    [SerializeField] private string themeId = "default";
    [SerializeField] private GameObject[] floorPrefabs;
    [SerializeField] private GameObject[] wallPrefabs;

    public string ThemeId => themeId;
    public GameObject[] FloorPrefabs => floorPrefabs;
    public GameObject[] WallPrefabs => wallPrefabs;

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

        error = null;
        return true;
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
