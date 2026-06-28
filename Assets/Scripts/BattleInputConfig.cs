using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "BattleInputConfig", menuName = "Config/Battle Input Config")]
public class BattleInputConfig : ScriptableObject
{
    [SerializeField] private float handCardDoubleClickPlayInterval = 0.3f;
    [SerializeField] private float dragSwipeMinDistance = 90f;
    [SerializeField] private float dragSwipeVerticalRatio = 1.2f;
    [SerializeField, FormerlySerializedAs("dragPreviewExtraSpacing")] private float cardQueueSpreadExtraSpacing = 42f;
    [SerializeField] private float cardQueueSpreadFalloffPower = 1.35f;
    [SerializeField] private float cardQueueZoneSwitchScreenDistance = 90f;
    [SerializeField] private Vector2 cardQueueDropScreenPadding = new Vector2(80f, 70f);

    public float HandCardDoubleClickPlayInterval => Mathf.Max(0f, handCardDoubleClickPlayInterval);
    public float DragSwipeMinDistance => Mathf.Max(0f, dragSwipeMinDistance);
    public float DragSwipeVerticalRatio => Mathf.Max(0f, dragSwipeVerticalRatio);
    public float CardQueueSpreadExtraSpacing => Mathf.Max(0f, cardQueueSpreadExtraSpacing);
    public float CardQueueSpreadFalloffPower => Mathf.Max(0f, cardQueueSpreadFalloffPower);
    public float CardQueueZoneSwitchScreenDistance => Mathf.Max(0f, cardQueueZoneSwitchScreenDistance);
    public Vector2 CardQueueDropScreenPadding => new Vector2(Mathf.Max(0f, cardQueueDropScreenPadding.x), Mathf.Max(0f, cardQueueDropScreenPadding.y));
}
