using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 挂在战斗界面的抽牌/弃牌/已消耗堆图标上，把 hover 转成 PileHoverPanelUI 的显示请求。
/// 与 TopBarIconTooltipUI（详情浮窗）并存，两者互不干扰。
/// </summary>
public class PileHoverTriggerUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private PileHoverPanelUI.PileKind pileKind = PileHoverPanelUI.PileKind.Draw;

    private HandSystemUI owner;

    public void Configure(HandSystemUI handSystemUI, PileHoverPanelUI.PileKind kind)
    {
        owner = handSystemUI;
        pileKind = kind;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (owner == null)
            owner = GetComponentInParent<HandSystemUI>();

        if (owner == null)
            return;

        owner.ShowPileHover(pileKind, transform as RectTransform);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        owner?.HidePileHover(pileKind);
    }

    private void OnDisable()
    {
        owner?.HidePileHover(pileKind);
    }
}
