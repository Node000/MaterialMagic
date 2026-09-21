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

    [Header("Detail Panel Press (PE)")]
    [Tooltip("长按多久算“看详情”（秒）；PE 上松手时超过这个时长就不触发点击动作。")]
    [SerializeField] private float detailLongPressThreshold = 0.6f;
    [Tooltip("按压期间指针位移超过这个像素数就取消本次按压（滑动/拖拽优先）。")]
    [SerializeField] private float detailPressMoveSlop = 20f;
    [Tooltip("长按松手后是否保留详情面板（点面板外才收起）；取消则松手即收。")]
    [SerializeField] private bool keepDetailAfterLongPress = true;

    public float HandCardDoubleClickPlayInterval => Mathf.Max(0f, handCardDoubleClickPlayInterval);
    public float DragSwipeMinDistance => Mathf.Max(0f, dragSwipeMinDistance);
    public float DragSwipeVerticalRatio => Mathf.Max(0f, dragSwipeVerticalRatio);
    public float CardQueueSpreadExtraSpacing => Mathf.Max(0f, cardQueueSpreadExtraSpacing);
    public float CardQueueSpreadFalloffPower => Mathf.Max(0f, cardQueueSpreadFalloffPower);
    public float CardQueueZoneSwitchScreenDistance => Mathf.Max(0f, cardQueueZoneSwitchScreenDistance);
    public Vector2 CardQueueDropScreenPadding => new Vector2(Mathf.Max(0f, cardQueueDropScreenPadding.x), Mathf.Max(0f, cardQueueDropScreenPadding.y));
    public float DetailLongPressThreshold => Mathf.Max(0.05f, detailLongPressThreshold);
    public float DetailPressMoveSlop => Mathf.Max(0f, detailPressMoveSlop);
    public bool KeepDetailAfterLongPress => keepDetailAfterLongPress;
}
