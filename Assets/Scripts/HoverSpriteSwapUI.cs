using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 悬停时把同一 GameObject 上 Image 的显示贴图换成 hoverSprite（改 overrideSprite，不动基础 sprite），
/// 移出后恢复。按压的亮度变化继续由 Selectable 的 ColorTint 负责，两者互不冲突。
/// 说明：CrayonUIEdge 读取的是 image.overrideSprite != null ? overrideSprite : sprite，
/// 因此换图后描边会跟随悬停贴图，样式保持统一。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public class HoverSpriteSwapUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Sprite hoverSprite;

    private Image targetImage;
    private Selectable selectable;

    private void Awake()
    {
        targetImage = GetComponent<Image>();
        selectable = GetComponent<Selectable>();
    }

    private void OnDisable()
    {
        SetHover(false);
    }

    /// <summary>运行时替换悬停贴图（美术资源可在 Inspector 绑定，也可由代码指定）。</summary>
    public void SetHoverSprite(Sprite sprite)
    {
        hoverSprite = sprite;
        SetHover(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (selectable != null && !selectable.interactable)
            return;

        SetHover(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SetHover(false);
    }

    private void SetHover(bool hovering)
    {
        if (targetImage == null)
            return;

        targetImage.overrideSprite = hovering ? hoverSprite : null;
    }
}
