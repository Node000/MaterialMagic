using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MapGridSlotView : MonoBehaviour
{
    [SerializeField] private Image background;
    [SerializeField] private Image icon;
    [SerializeField] private TMP_Text label;
    [SerializeField] private Color normalColor = new Color(0.08f, 0.1f, 0.16f, 0.92f);
    [SerializeField] private Color emptyColor = new Color(0.05f, 0.06f, 0.09f, 0.62f);
    [SerializeField] private Color unavailableColor = new Color(0.02f, 0.025f, 0.035f, 0.32f);
    [SerializeField] private Color fogColor = new Color(0.018f, 0.022f, 0.032f, 0.88f);
    [SerializeField] private Color levelIconColor = Color.white;
    [SerializeField] private Color bossIconColor = Color.white;
    [SerializeField] private Color labelColor = Color.white;

    public Image Icon => icon;
    public TMP_Text Label => label;

    private void Awake()
    {
        CacheReferences();
    }

    public void Initialize(Image fallbackBackground, Image fallbackIcon, TMP_Text fallbackLabel)
    {
        if (background == null)
            background = fallbackBackground;
        if (icon == null)
            icon = fallbackIcon;
        if (label == null)
            label = fallbackLabel;
        CacheReferences();
    }

    public void Apply(RunMapCellModel model, Sprite iconSprite, string labelText)
    {
        CacheReferences();
        if (icon == null)
            return;

        bool unavailable = model == null || !model.isAvailable;
        bool hiddenByFog = !unavailable && !model.isRevealed;
        bool hiddenContent = !unavailable && !hiddenByFog && model.isHidden && model.level != null && !model.isBoss && model.level.levelType != LevelType.Elite;
        bool hasContent = !unavailable && !hiddenByFog && (hiddenContent || model.isBoss || model.level != null);
        if (background != null)
            background.color = unavailable ? unavailableColor : hiddenByFog ? fogColor : model.level == null && !model.isBoss ? emptyColor : normalColor;

        if (!hasContent)
        {
            icon.gameObject.SetActive(false);
            if (label != null)
                label.text = string.Empty;
            return;
        }

        if (hiddenContent)
        {
            Sprite hiddenSprite = UIManager.LoadHiddenLevelSprite();
            icon.gameObject.SetActive(hiddenSprite != null);
            icon.sprite = hiddenSprite;
            icon.color = levelIconColor;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            if (label != null)
            {
                label.text = LocalizationSystem.GetText("ui.level_select.unknown", "未知");
                label.color = labelColor;
                label.raycastTarget = false;
            }
            return;
        }

        icon.gameObject.SetActive(true);
        icon.rectTransform.localScale = Vector3.one;
        icon.sprite = iconSprite;
        icon.color = model.isBoss ? bossIconColor : levelIconColor;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        if (label != null)
        {
            label.text = labelText;
            label.color = labelColor;
            label.raycastTarget = false;
        }
    }

    private void CacheReferences()
    {
        if (background == null)
            background = GetComponent<Image>();
        if (icon == null)
        {
            Transform iconTransform = transform.Find("Icon");
            icon = iconTransform != null ? iconTransform.GetComponent<Image>() : null;
        }
        if (label == null)
        {
            Transform labelTransform = transform.Find("Label");
            label = labelTransform != null ? labelTransform.GetComponent<TMP_Text>() : null;
        }
    }
}
