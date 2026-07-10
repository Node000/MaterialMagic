using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AscensionDetailPanelUI : MonoBehaviour
{
    [SerializeField] private RectTransform root;
    [SerializeField] private Button closeButton;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private ScrollRect bodyScrollRect;
    [SerializeField] private Sprite iconSprite;

    private int currentLevel;
    private bool closeButtonBound;

    public bool IsShowing => gameObject.activeSelf;

    private void Awake()
    {
        BindCloseButton();
        LocalizationSystem.LanguageChanged += HandleLanguageChanged;
    }

    private void OnDestroy()
    {
        LocalizationSystem.LanguageChanged -= HandleLanguageChanged;
        if (closeButton != null && closeButtonBound)
            closeButton.onClick.RemoveListener(Hide);
    }

    public void Show(int ascensionLevel)
    {
        currentLevel = ascensionLevel;
        BindCloseButton();
        Refresh();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public bool Contains(Transform hit)
    {
        return hit != null && hit.IsChildOf(transform);
    }

    private void Refresh()
    {
        if (iconImage != null)
        {
            iconImage.sprite = iconSprite;
            iconImage.enabled = iconImage.sprite != null;
        }
        if (titleText != null)
            titleText.text = LocalizationSystem.GetText("ui.ascension.detail_title", "进阶详情");
        if (levelText != null)
            levelText.text = AscensionUIUtility.GetLevelName(currentLevel);
        if (statusText != null)
            statusText.text = AscensionUIUtility.BuildSelectorStatusText();
        if (bodyText != null)
        {
            bodyText.richText = true;
            bodyText.text = InlineIconTextFormatter.Format(AscensionUIUtility.BuildDetailBody(currentLevel));
        }
        if (bodyScrollRect != null)
            bodyScrollRect.verticalNormalizedPosition = 1f;
    }

    private void BindCloseButton()
    {
        if (closeButton == null || closeButtonBound)
            return;

        closeButton.onClick.AddListener(Hide);
        closeButtonBound = true;
    }

    private void HandleLanguageChanged()
    {
        if (gameObject.activeSelf)
            Refresh();
    }
}
