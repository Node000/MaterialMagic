using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class AscensionTopBarIndicatorUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private Image iconImage;
    [SerializeField] private SpriteRenderer iconRenderer;
    [SerializeField] private AscensionDetailPanelUI detailPanel;
    [SerializeField] private Sprite iconSprite;
    [SerializeField] private UIManager uiManager;
    [SerializeField] private Button button;

    private bool buttonBound;

    private void Awake()
    {
        BindButton();
        LocalizationSystem.LanguageChanged += HandleLanguageChanged;
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
        if (uiManager != null)
            uiManager.HideUnifiedDetailPopup(this);
    }

    private void OnDestroy()
    {
        LocalizationSystem.LanguageChanged -= HandleLanguageChanged;
        if (button != null && buttonBound)
            button.onClick.RemoveListener(ToggleDetail);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (uiManager != null)
            uiManager.ShowUnifiedDetailPopup(this, AscensionUIUtility.BuildUnifiedDetailContent(DifficultyUpgradeSystem.CurrentAscensionLevel, iconSprite));
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (uiManager != null)
            uiManager.HideUnifiedDetailPopup(this);
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
            levelText.text = Mathf.Max(0, level).ToString();
    }

    private void ToggleDetail()
    {
        if (detailPanel == null)
            return;

        if (detailPanel.IsShowing)
            detailPanel.Hide();
        else
            detailPanel.Show(DifficultyUpgradeSystem.CurrentAscensionLevel);
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
