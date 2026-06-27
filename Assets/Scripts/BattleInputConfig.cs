using UnityEngine;

[CreateAssetMenu(fileName = "BattleInputConfig", menuName = "Config/Battle Input Config")]
public class BattleInputConfig : ScriptableObject
{
    [SerializeField] private float handCardDoubleClickPlayInterval = 0.3f;

    public float HandCardDoubleClickPlayInterval => Mathf.Max(0f, handCardDoubleClickPlayInterval);
}
