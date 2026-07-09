using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class TopBarIconTooltipUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private string titleKey;
    [SerializeField] private string bodyKey;
    [SerializeField] private string titleFallback;
    [SerializeField] private string bodyFallback;

    private UIManager uiManager;

    public void Configure(UIManager manager, string tooltipTitleKey, string tooltipBodyKey, string tooltipTitleFallback, string tooltipBodyFallback)
    {
        uiManager = manager;
        titleKey = tooltipTitleKey;
        bodyKey = tooltipBodyKey;
        titleFallback = tooltipTitleFallback;
        bodyFallback = tooltipBodyFallback;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        GetUIManager()?.ShowUnifiedDetailPopup(this, BuildContent());
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        GetUIManager()?.HideUnifiedDetailPopup(this);
    }

    private void OnDisable()
    {
        if (uiManager != null)
            uiManager.HideUnifiedDetailPopup(this);
    }

    private UIManager GetUIManager()
    {
        if (uiManager == null)
            uiManager = GetComponentInParent<UIManager>();
        return uiManager;
    }

    private UnifiedDetailContent BuildContent()
    {
        return new UnifiedDetailContent
        {
            SourceType = UnifiedDetailSourceType.None,
            Title = LocalizationSystem.GetText(titleKey, titleFallback),
            Body = LocalizationSystem.GetText(bodyKey, bodyFallback),
            AccentColor = Color.white,
            Icon = GetIconSprite()
        };
    }

    private Sprite GetIconSprite()
    {
        SpriteRenderer spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        if (spriteRenderer != null && spriteRenderer.sprite != null)
            return spriteRenderer.sprite;

        Image[] images = GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            if (images[i].sprite != null)
                return images[i].sprite;
        }

        return null;
    }
}
