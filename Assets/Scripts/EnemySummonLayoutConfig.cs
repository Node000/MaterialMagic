using UnityEngine;

[CreateAssetMenu(fileName = "EnemySummonLayoutConfig", menuName = "Config/Enemy Summon Layout Config")]
public class EnemySummonLayoutConfig : ScriptableObject
{
    [SerializeField] private float horizontalSpacing = 180f;
    [SerializeField] private float verticalSpacing = 160f;
    [SerializeField] private float occupiedRadius = 150f;
    [SerializeField] private int sameRowSlotCount = 4;
    [SerializeField] private int maxSearchAttempts = 16;

    public float HorizontalSpacing => Mathf.Max(1f, horizontalSpacing);
    public float VerticalSpacing => Mathf.Max(0f, verticalSpacing);
    public float OccupiedRadius => Mathf.Max(0f, occupiedRadius);
    public int SameRowSlotCount => Mathf.Max(2, sameRowSlotCount);
    public int MaxSearchAttempts => Mathf.Max(1, maxSearchAttempts);
}
