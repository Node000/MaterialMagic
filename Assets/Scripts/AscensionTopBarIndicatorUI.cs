using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class AscensionTopBarIndicatorUI : MonoBehaviour
{
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private Image iconImage;
    [SerializeField] private SpriteRenderer iconRenderer;
    [SerializeField] private AscensionDetailPanelUI detailPanel;
    [SerializeField] private Sprite iconSprite;
    [SerializeField] private UIManager uiManager;
    [SerializeField] private Button button;

    private bool buttonBound;
    private UnifiedDetailTriggerUI detailTrigger;

    private void Awake()
    {
        ResolveDependencies();
        BindButton();
        LocalizationSystem.LanguageChanged += HandleLanguageChanged;
        // 详情面板统一由 UnifiedDetailTriggerUI 负责（PC 悬停显示 / PE 长按看详情），内容按当前进阶等级实时构建。
        UnifiedDetailTriggerUI trigger = EnsureDetailTrigger();
        trigger.SetAnchor(this);
        trigger.SetContentProvider(BuildDetailContent);
    }

    private void Start()
    {
        Refresh();
    }

    private void OnEnable()
    {
        Refresh();
    }

    private void OnDisable()
    {
        // 详情由 UnifiedDetailTriggerUI 在自己的 OnDisable 里收起。
    }

    private void OnDestroy()
    {
        LocalizationSystem.LanguageChanged -= HandleLanguageChanged;
        if (button != null && buttonBound)
            button.onClick.RemoveListener(ToggleDetail);
    }

    /// <summary>进阶按钮的说明详情：标题里的等级会变，所以用 Provider 注入；强调色沿用面板上美术设的颜色。</summary>
    private UnifiedDetailContent BuildDetailContent()
    {
        int level = DifficultyUpgradeSystem.CurrentAscensionLevel;
        return new UnifiedDetailContent
        {
            Title = string.Format(LocalizationSystem.GetText("ui.ascension.button_detail.title", "进阶{0}"), level),
            Body = LocalizationSystem.GetText("ui.ascension.button_detail.body", "点击查看难度变化"),
            Icon = iconSprite
        };
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

    public void Refresh()
    {
        int level = DifficultyUpgradeSystem.CurrentAscensionLevel;
        Sprite sprite = iconSprite;
        if (iconImage != null)
        {
            iconImage.sprite = sprite;
            iconImage.enabled = sprite != null;
        }
        if (iconRenderer != null)
            iconRenderer.sprite = sprite;
        if (levelText != null)
            levelText.text = level.ToString();
    }

    private void ToggleDetail()
    {
        ResolveDependencies();
        if (detailPanel == null)
            return;

        if (detailPanel.IsShowing)
            detailPanel.Hide();
        else
            detailPanel.Show(DifficultyUpgradeSystem.CurrentAscensionLevel);
    }

    private void ResolveDependencies()
    {
        if (uiManager == null)
            uiManager = GetComponentInParent<UIManager>();
        if (detailPanel == null)
            detailPanel = GetComponentInParent<UIManager>()?.GetComponentInChildren<AscensionDetailPanelUI>(true);
    }

    private void BindButton()
    {
        if (button == null || buttonBound)
            return;

        button.onClick.AddListener(ToggleDetail);
        buttonBound = true;
    }

    private void HandleLanguageChanged()
    {
        Refresh();
    }
}
