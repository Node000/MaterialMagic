using UnityEngine;

[CreateAssetMenu(fileName = "EnemyDefinition", menuName = "Content/Enemy Definition")]
public sealed class EnemyDefinition : ScriptableObject
{
    [SerializeField] private EnemyData data = new EnemyData();

    public int NumericId => data != null ? data.numericId : 0;
    public string Id => data != null ? data.Id : string.Empty;

    public EnemyData CreateRuntimeData()
    {
        return CloneData(data);
    }

    public void SetData(EnemyData value)
    {
        data = CloneData(value);
    }

    private static EnemyData CloneData(EnemyData source)
    {
        return source == null ? null : JsonUtility.FromJson<EnemyData>(JsonUtility.ToJson(source));
    }
}
