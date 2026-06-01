using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class BonusRewardIconUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text amountText;

    private RewardGridPanelUI owner;
    private BonusRewardData rewardData;
    private RectTransform rectTransform;

    public RectTransform RectTransform => rectTransform != null ? rectTransform : (RectTransform)transform;

    private void Awake()
    {
        rectTransform = (RectTransform)transform;
        CacheReferences();
    }

    public void Bind(RewardGridPanelUI owner, BonusRewardData rewardData)
    {
        this.owner = owner;
        this.rewardData = rewardData;
        CacheReferences();

        if (iconImage != null)
        {
            Sprite sprite = !string.IsNullOrEmpty(rewardData.texturePath) ? Resources.Load<Sprite>(rewardData.texturePath) : null;
            iconImage.sprite = sprite;
            iconImage.color = sprite != null ? Color.white : GetFallbackColor(rewardData.rewardType);
        }

        if (amountText != null)
        {
            amountText.text = string.Empty;
            amountText.gameObject.SetActive(false);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        owner?.ShowRewardTooltip(RectTransform, rewardData);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        owner?.HideRewardTooltip();
    }

    private void CacheReferences()
    {
        if (iconImage == null)
            iconImage = UIManager.FindChildComponent<Image>(transform, "Icon");
        if (amountText == null)
            amountText = UIManager.FindChildComponent<TMP_Text>(transform, "AmountText");
    }

    private static Color GetFallbackColor(BonusRewardType rewardType)
    {
        switch (rewardType)
        {
            case BonusRewardType.Gold: return new Color(1f, 0.88f, 0.22f, 1f);
            case BonusRewardType.Heal: return new Color(0.2f, 1f, 0.78f, 1f);
            default: return Color.white;
        }
    }
}
