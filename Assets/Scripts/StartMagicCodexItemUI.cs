using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(CanvasGroup))]
public class StartMagicCodexItemUI : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    [SerializeField] private MagicItemView magicItemView;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text newLabelText;
    [SerializeField] private string newLabelKey = "ui.magic_codex.new_label";
    [SerializeField] private float newLabelFontSize = 17f;
    [SerializeField] private Color newLabelColor = new Color(1f, 0.92f, 0.18f, 1f);
    [SerializeField] private Vector2 newLabelAnchoredPosition = new Vector2(-4f, -2f);
    [SerializeField] private Vector2 newLabelSize = new Vector2(96f, 24f);

    private StartMagicCodexPanelUI owner;
    private MagicData magicData;
    private bool unlocked;
    private bool isNew;

    public void ConfigureNewLabel(string localizationKey, float fontSize, Color color, Vector2 anchoredPosition, Vector2 size)
    {
        newLabelKey = localizationKey;
        newLabelFontSize = fontSize;
        newLabelColor = color;
        newLabelAnchoredPosition = anchoredPosition;
        newLabelSize = size;
        ApplyNewLabelSettings();
    }

    public void Bind(MagicData data, bool unlocked, bool isNew, StartMagicCodexPanelUI owner, float unlockedAlpha, float lockedAlpha)
    {
        CacheReferences();
        magicData = data;
        this.owner = owner;

        if (magicItemView != null)
            magicItemView.Bind(MagicFactory.Create(data));

        RefreshState(unlocked, isNew, unlockedAlpha, lockedAlpha);
    }

    public void RefreshState(bool unlocked, bool isNew, float unlockedAlpha, float lockedAlpha)
    {
        CacheReferences();
        this.unlocked = unlocked;
        this.isNew = isNew;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = unlocked ? unlockedAlpha : lockedAlpha;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        RefreshNewLabel();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        DismissNewLabel();
        owner?.ShowMagic(magicData, unlocked);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
            return;

        DismissNewLabel();
        owner?.ShowMagic(magicData, unlocked);
    }

    private void CacheReferences()
    {
        if (magicItemView == null)
            magicItemView = GetComponent<MagicItemView>();
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        EnsureNewLabel();
    }

    private void EnsureNewLabel()
    {
        if (newLabelText != null)
            return;

        Transform existing = transform.Find("NewLabel");
        if (existing != null)
            newLabelText = existing.GetComponent<TMP_Text>();
        if (newLabelText != null)
            return;

        newLabelText = new GameObject("NewLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        newLabelText.transform.SetParent(transform, false);
        TMP_FontAsset font = UIManager.GetDefaultTMPFont();
        if (font != null)
            newLabelText.font = font;
        ApplyNewLabelSettings();
        newLabelText.transform.SetAsLastSibling();
    }

    private void ApplyNewLabelSettings()
    {
        if (newLabelText == null)
            return;

        newLabelText.fontSize = newLabelFontSize;
        newLabelText.fontStyle = FontStyles.Bold;
        newLabelText.alignment = TextAlignmentOptions.TopRight;
        newLabelText.color = newLabelColor;
        newLabelText.raycastTarget = false;

        RectTransform rect = newLabelText.rectTransform;
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = newLabelAnchoredPosition;
        rect.sizeDelta = newLabelSize;
    }

    private void RefreshNewLabel()
    {
        if (newLabelText == null)
            return;

        newLabelText.text = LocalizationSystem.GetText(newLabelKey, "New!!!");
        newLabelText.gameObject.SetActive(unlocked && isNew);
    }

    private void DismissNewLabel()
    {
        if (!isNew)
            return;

        isNew = false;
        if (newLabelText != null)
            newLabelText.gameObject.SetActive(false);
        MagicCodexProgressSystem.MarkMagicSeen(magicData);
    }
}
