using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 地图阶段四张方向牌（火=上、风=左、水=下、土=右）的点击与悬停反馈。
/// 悬停时按预制体自身缩放做轻微放大，并弹出统一详情浮框（内容来自 UnifiedDetailContentBuilder.BuildMapMove）；
/// 移出、被禁用或对象销毁时复位并收起浮框。
/// 悬停参数只在本脚本暴露，不改动美术预制体上的既有数值。
/// </summary>
public class MapDirectionCardView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("悬停表现")]
    [SerializeField] private float hoverScale = 1.08f;
    [SerializeField] private float hoverDuration = 0.14f;
    [SerializeField] private Ease hoverEase = Ease.OutBack;
    [SerializeField] private bool showHoverDetail = true;

    [SerializeField] private Image iconImage;

    private ChapterGridPanelUI owner;
    private UIManager uiManager;
    private MaterialEnum material;
    private RectTransform rectTransform;
    private Button button;
    private Tween hoverTween;
    private Vector3 baseScale = Vector3.one;

    public void Initialize(ChapterGridPanelUI owner, MaterialEnum material)
    {
        this.owner = owner;
        this.material = material;
        uiManager = owner != null ? owner.GetUIManager() : null;
        CacheReferences();
        RefreshVisual();
    }

    public void SetInteractable(bool interactable)
    {
        CacheReferences();
        if (button != null)
            button.interactable = interactable;

        if (!interactable)
            ClearHover(true);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left || eventData.button == PointerEventData.InputButton.Right)
            owner?.HandleDirectionClicked(material);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        CacheReferences();
        if (button != null && !button.interactable)
            return;

        PlayHoverMotion(true);
        if (showHoverDetail && uiManager != null)
            uiManager.ShowUnifiedDetailPopup(this, UnifiedDetailContentBuilder.BuildMapMove(material));
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ClearHover(false);
    }

    public void RefreshVisual()
    {
        if (iconImage != null)
            iconImage.sprite = MaterialCardView.GetMaterialIcon(material);
    }

    private void OnDisable()
    {
        ClearHover(true);
    }

    private void OnDestroy()
    {
        hoverTween?.Kill(false);
        hoverTween = null;
    }

    private void ClearHover(bool instant)
    {
        HideHoverDetail();

        if (instant)
        {
            hoverTween?.Kill(false);
            hoverTween = null;
            if (rectTransform != null)
                rectTransform.localScale = baseScale;
            return;
        }

        PlayHoverMotion(false);
    }

    private void HideHoverDetail()
    {
        if (uiManager == null)
            return;

        UnifiedDetailPopupUI popup = uiManager.UnifiedDetailPopup;
        if (popup != null)
            popup.Hide(this);
    }

    private void PlayHoverMotion(bool hovering)
    {
        if (rectTransform == null)
            return;

        hoverTween?.Kill(false);
        hoverTween = null;
        Vector3 target = hovering ? baseScale * Mathf.Max(0.01f, hoverScale) : baseScale;
        if (hoverDuration <= 0f)
        {
            rectTransform.localScale = target;
            return;
        }

        hoverTween = rectTransform.DOScale(target, hoverDuration).SetEase(hoverEase).SetTarget(this);
    }

    private void CacheReferences()
    {
        if (rectTransform == null)
        {
            rectTransform = transform as RectTransform;
            if (rectTransform != null)
                baseScale = rectTransform.localScale;
        }

        if (button == null)
            button = GetComponent<Button>();
    }
}
