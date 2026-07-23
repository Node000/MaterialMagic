using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "GameContentDatabase", menuName = "Content/Game Content Database")]
public sealed class GameContentDatabase : ScriptableObject
{
    private const string ResourcePath = "Config/GameContentDatabase";

    [SerializeField] private List<EnemyDefinition> enemies = new List<EnemyDefinition>();

    public IReadOnlyList<EnemyDefinition> Enemies => enemies;

    public static GameContentDatabase Load()
    {
        return Resources.Load<GameContentDatabase>(ResourcePath);
    }

    public Dictionary<int, EnemyData> CreateEnemyDataDictionary()
    {
        Dictionary<int, EnemyData> result = new Dictionary<int, EnemyData>(enemies.Count);
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyDefinition definition = enemies[i];
            if (definition == null || definition.NumericId <= 0 || result.ContainsKey(definition.NumericId))
                continue;

            EnemyData data = definition.CreateRuntimeData();
            if (data != null)
                result.Add(data.numericId, data);
        }
        return result;
    }

    public void ReplaceEnemyDefinitions(IList<EnemyDefinition> definitions)
    {
        enemies.Clear();
        if (definitions == null)
            return;

        for (int i = 0; i < definitions.Count; i++)
        {
            if (definitions[i] != null)
                enemies.Add(definitions[i]);
        }
    }
}
