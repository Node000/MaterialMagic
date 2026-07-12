using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StartAscensionSelectionUI : MonoBehaviour
{
    [SerializeField] private Button toggleButton;
    [SerializeField] private Button decreaseButton;
    [SerializeField] private Button increaseButton;
    [SerializeField] private Image iconImage;
    [SerializeField] private Image drawerIconImage;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text drawerLevelText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private ScrollRect bodyScrollRect;
    [SerializeField] private float bodyBottomPadding = 16f;
    [SerializeField] private Sprite iconSprite;
    [SerializeField] private Color lockedTextColor = new Color(0.68f, 0.68f, 0.72f, 1f);
    [SerializeField] private Color unlockedTextColor = Color.white;
    [Header("抽屉")]
    [SerializeField] private RectTransform drawerPanel;
    [SerializeField] private CanvasGroup drawerCanvasGroup;
    [SerializeField] private Vector2 drawerHiddenAnchoredPosition = new Vector2(0f, -230f);
    [SerializeField] private Vector2 drawerShownAnchoredPosition = Vector2.zero;
    [SerializeField, Min(0f)] private float drawerAnimationDuration = 0.22f;
    [SerializeField] private bool startExpanded;

    private bool buttonsBound;
    private bool drawerOpen;
    private Tween drawerTween;

    private void Awake()
    {
        BindButtons();
        SetDrawerImmediate(startExpanded);
        LocalizationSystem.LanguageChanged += HandleLanguageChanged;
    }

    private void OnEnable()
    {
        Refresh();
    }

    private void OnDisable()
    {
        SetDrawerImmediate(false);
    }

    private void OnDestroy()
    {
        LocalizationSystem.LanguageChanged -= HandleLanguageChanged;
        drawerTween?.Kill(false);
        if (!buttonsBound)
            return;
        if (toggleButton != null)
            toggleButton.onClick.RemoveListener(ToggleDetail);
        if (decreaseButton != null)
            decreaseButton.onClick.RemoveListener(DecreaseLevel);
        if (increaseButton != null)
            increaseButton.onClick.RemoveListener(IncreaseLevel);
    }

    public void Refresh()
    {
        int level = AscensionSystem.SelectedAscensionLevel;
        bool unlocked = AscensionSystem.IsUnlocked();
        Sprite sprite = iconSprite;
        if (iconImage != null)
        {
            iconImage.sprite = sprite;
            iconImage.enabled = sprite != null;
        }
        if (drawerIconImage != null)
        {
            drawerIconImage.sprite = sprite;
            drawerIconImage.enabled = sprite != null;
        }
        if (levelText != null)
        {
            levelText.text = Mathf.Max(0, level).ToString();
            levelText.color = unlocked ? unlockedTextColor : lockedTextColor;
        }
        if (drawerLevelText != null)
        {
            drawerLevelText.text = AscensionUIUtility.GetLevelName(level);
            drawerLevelText.color = unlocked ? unlockedTextColor : lockedTextColor;
        }
        if (statusText != null)
        {
            statusText.text = AscensionUIUtility.BuildSelectorStatusText();
            statusText.color = unlocked ? unlockedTextColor : lockedTextColor;
        }
        if (bodyText != null)
        {
            bodyText.richText = true;
            bodyText.text = InlineIconTextFormatter.Format(AscensionUIUtility.BuildDetailBody(level));
            RefreshBodyScroll();
        }
        if (decreaseButton != null)
            decreaseButton.interactable = unlocked && level > 0;
        if (increaseButton != null)
            increaseButton.interactable = unlocked && level < AscensionSystem.HighestUnlockedLevel;
    }

    private void RefreshBodyScroll()
    {
        if (bodyScrollRect == null || bodyScrollRect.content == null || bodyScrollRect.viewport == null || bodyText == null)
            return;

        Canvas.ForceUpdateCanvases();
        float textWidth = bodyText.rectTransform.rect.width;
        float textHeight = bodyText.GetPreferredValues(bodyText.text, textWidth, 0f).y;
        float contentHeight = Mathf.Max(bodyScrollRect.viewport.rect.height, Mathf.Ceil(textHeight) + bodyBottomPadding);
        Vector2 size = bodyScrollRect.content.sizeDelta;
        size.y = contentHeight;
        bodyScrollRect.content.sizeDelta = size;
        bodyScrollRect.verticalNormalizedPosition = 1f;
    }

    private void DecreaseLevel()
    {
        SetLevel(AscensionSystem.SelectedAscensionLevel - 1);
    }

    private void IncreaseLevel()
    {
        SetLevel(AscensionSystem.SelectedAscensionLevel + 1);
    }

    private void SetLevel(int level)
    {
        if (!AscensionSystem.IsLevelUnlocked(level))
            return;

        AscensionSystem.SelectedAscensionLevel = level;
        Refresh();
    }

    private void ToggleDetail()
    {
        if (drawerPanel == null)
            return;

        SetDrawerOpen(!drawerOpen);
    }

    private void SetDrawerOpen(bool open)
    {
        if (drawerPanel == null)
            return;

        drawerOpen = open;
        drawerTween?.Kill(false);
        drawerPanel.gameObject.SetActive(true);
        Refresh();
        if (drawerCanvasGroup != null)
        {
            drawerCanvasGroup.interactable = open;
            drawerCanvasGroup.blocksRaycasts = open;
        }

        float duration = Mathf.Max(0f, drawerAnimationDuration);
        if (duration <= 0f)
        {
            SetDrawerImmediate(open);
            return;
        }

        Sequence sequence = DOTween.Sequence().SetUpdate(true).SetTarget(this);
        sequence.Join(drawerPanel.DOAnchorPos(open ? drawerShownAnchoredPosition : drawerHiddenAnchoredPosition, duration).SetEase(open ? Ease.OutCubic : Ease.InCubic));
        if (drawerCanvasGroup != null)
            sequence.Join(drawerCanvasGroup.DOFade(open ? 1f : 0f, duration));
        sequence.OnComplete(() =>
        {
            if (!open && drawerPanel != null)
                drawerPanel.gameObject.SetActive(false);
            else
                RefreshBodyScroll();
        });
        drawerTween = sequence;
    }

    private void SetDrawerImmediate(bool open)
    {
        drawerTween?.Kill(false);
        drawerOpen = open;
        if (drawerPanel == null)
            return;

        drawerPanel.anchoredPosition = open ? drawerShownAnchoredPosition : drawerHiddenAnchoredPosition;
        drawerPanel.gameObject.SetActive(open);
        if (open)
            Refresh();
        if (drawerCanvasGroup != null)
        {
            drawerCanvasGroup.alpha = open ? 1f : 0f;
            drawerCanvasGroup.interactable = open;
            drawerCanvasGroup.blocksRaycasts = open;
        }
    }

    private void BindButtons()
    {
        if (buttonsBound)
            return;

        if (toggleButton != null)
            toggleButton.onClick.AddListener(ToggleDetail);
        if (decreaseButton != null)
            decreaseButton.onClick.AddListener(DecreaseLevel);
        if (increaseButton != null)
            increaseButton.onClick.AddListener(IncreaseLevel);
        buttonsBound = true;
    }

    private void HandleLanguageChanged()
    {
        Refresh();
    }
}
