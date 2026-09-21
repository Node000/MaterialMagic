using UnityEngine;

/// <summary>
/// 顶栏图标按钮的详情配置层。
/// 详情本身（PC 悬停显示、PE 长按看详情、短按放行按钮点击、强调色沿用面板上美术设的颜色）由同物体上的
/// <see cref="UnifiedDetailTriggerUI"/> 负责，本脚本只把“用哪组本地化 key”传给它，不再自己接指针事件。
/// </summary>
public class TopBarIconTooltipUI : MonoBehaviour
{
    [SerializeField] private string titleKey;
    [SerializeField] private string bodyKey;
    [SerializeField] private string titleFallback;
    [SerializeField] private string bodyFallback;

    public void Configure(UIManager manager, string tooltipTitleKey, string tooltipBodyKey, string tooltipTitleFallback, string tooltipBodyFallback)
    {
        titleKey = tooltipTitleKey;
        bodyKey = tooltipBodyKey;
        titleFallback = tooltipTitleFallback;
        bodyFallback = tooltipBodyFallback;

        UnifiedDetailTriggerUI trigger = GetComponent<UnifiedDetailTriggerUI>();
        if (trigger == null)
            trigger = gameObject.AddComponent<UnifiedDetailTriggerUI>();

        // 钉住归属仍用本脚本（与原实现一致）；短按放行按钮自身的点击（设置/地图/关卡进度）。
        trigger.SetAnchor(this);
        trigger.SetShortPressBehavior(UnifiedDetailTriggerUI.ShortPressBehavior.ClickThrough);
        // 图标取自身/子物体上的 Sprite（与旧实现一致），强调色沿用面板上美术设的边框色（代码不写颜色）。
        trigger.SetLocalizedContent(titleKey, bodyKey, titleFallback, bodyFallback);
    }
}
