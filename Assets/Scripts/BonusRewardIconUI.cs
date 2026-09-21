using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public class BonusRewardIconUI : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text amountText;

    private RewardGridPanelUI owner;
    private BonusRewardData rewardData;
    private RectTransform rectTransform;
    private UnifiedDetailTriggerUI detailTrigger;

    public RectTransform RectTransform => rectTransform != null ? rectTransform : (RectTransform)transform;

    private void Awake()
    {
        rectTransform = (RectTransform)transform;
        CacheReferences();
        // 详情面板统一由 UnifiedDetailTriggerUI 负责（PC 悬停显示 / PE 长按看详情）。
        UnifiedDetailTriggerUI trigger = EnsureDetailTrigger();
        trigger.SetAnchor(RectTransform);
        trigger.SetContentProvider(BuildDetailContent);
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

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
            EnsureDetailTrigger().PinDetailNow();
    }

    /// <summary>详情内容随格子奖励变化，所以用 Provider 注入；没数据时不弹面板。</summary>
    private UnifiedDetailContent BuildDetailContent()
    {
        return rewardData != null ? UnifiedDetailContentBuilder.Build(rewardData) : default;
    }

    /// <summary>没挂组件时兜底补上（美术资源漏挂也能正常工作）。</summary>
    private UnifiedDetailTriggerUI EnsureDetailTrigger()
    {
        if (detailTrigger == null)
        {
            detailTrigger = GetComponent<UnifiedDetailTriggerUI>();
            if (detailTrigger == null)
                detailTrigger = gameObject.AddComponent<UnifiedDetailTriggerUI>();
        }

        return detailTrigger;
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
