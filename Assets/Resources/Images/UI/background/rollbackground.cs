using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(Image))]
public class UITiledVerticalScroller : MonoBehaviour
{
    [Header("Scroll")]
    [Tooltip("正数向上滚动，负数向下滚动。单位是 UI 像素/秒。")]
    public float speedY = 50f;

    [Tooltip("一格平铺图案在 UI 里的高度。比如你的单帧是 128px 高，就先填 128。")]
    public float tileHeight = 128f;

    [Tooltip("暂停游戏时 UI 动画是否继续滚动。")]
    public bool useUnscaledTime = true;

    private RectTransform rectTransform;
    private Vector2 startAnchoredPosition;
    private float offsetY;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        startAnchoredPosition = rectTransform.anchoredPosition;
    }

    private void OnEnable()
    {
        startAnchoredPosition = rectTransform.anchoredPosition;
        offsetY = 0f;
    }

    private void Update()
    {
        if (tileHeight <= 0f)
        {
            return;
        }

        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        offsetY = Mathf.Repeat(offsetY + speedY * deltaTime, tileHeight);

        rectTransform.anchoredPosition = startAnchoredPosition + new Vector2(0f, offsetY);
    }
}