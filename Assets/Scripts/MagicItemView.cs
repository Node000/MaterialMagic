using System.Collections.Generic;
using System.Text;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MagicItemView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Text magicNameText;
    [SerializeField] private RectTransform recipeRoot;
    [SerializeField] private RectTransform tooltipRoot;
    [SerializeField] private Text tooltipNameText;
    [SerializeField] private Text tooltipDescriptionText;
    [SerializeField] private Text tooltipEffectText;
    [SerializeField] private Image modifierMarkerImage;
    [SerializeField] private RectTransform tagTooltipRoot;
    [SerializeField] private Text tagTooltipText;
    [SerializeField] private float tagTooltipXOffset = 12f;
    [SerializeField] private float tagTooltipSlideDistance = 28f;
    [SerializeField] private Vector2 tagTooltipSize = new Vector2(230f, 120f);
    [SerializeField] private float tagTooltipLineHeight = 22f;
    [SerializeField] private float tagTooltipVerticalPadding = 20f;
    [SerializeField] private float tooltipFadeDuration = 0.12f;
    [SerializeField] private float tooltipScaleDuration = 0.18f;
    [SerializeField] private Ease tooltipEase = Ease.OutBack;
    [Header("动画参数")]
    [SerializeField] private Vector3 tooltipHiddenScale = new Vector3(0.82f, 0.82f, 1f);
    [SerializeField] private float recipeHighlightPunchScale = 0.25f;
    [SerializeField] private float recipeHighlightDuration = 0.18f;
    [SerializeField] private int recipeHighlightVibrato = 6;
    [SerializeField] private float recipeHighlightElasticity = 0.6f;
    [SerializeField] private float castPulseScale = 0.16f;
    [SerializeField] private float castPulseDuration = 0.28f;
    [SerializeField] private int castPulseVibrato = 8;
    [SerializeField] private float castPulseElasticity = 0.65f;

    private readonly List<Image> recipeBlocks = new List<Image>();
    private readonly Color emptyBackgroundColor = new Color(0.08f, 0.08f, 0.12f, 0.86f);
    private readonly Color tooltipTitleColor = new Color(1f, 0.92f, 0.62f, 1f);
    private MagicModel magic;
    private CanvasGroup tooltipCanvasGroup;
    private CanvasGroup tagTooltipCanvasGroup;
    private Tween tooltipTween;
    private Tween tagTooltipTween;
    private Tween pulseTween;
    private Tween modifierMarkerTween;
    private bool warnedMissingBackgroundImage;

    public MagicModel Magic => magic;

    private void Awake()
    {
        if (tooltipRoot != null)
        {
            tooltipCanvasGroup = tooltipRoot.GetComponent<CanvasGroup>();
            if (tooltipCanvasGroup == null)
                tooltipCanvasGroup = tooltipRoot.gameObject.AddComponent<CanvasGroup>();

            tooltipRoot.pivot = new Vector2(0.5f, 0.5f);
            tooltipRoot.anchoredPosition += new Vector2(0f, tooltipRoot.sizeDelta.y * 0.5f);
            tooltipRoot.localScale = tooltipHiddenScale;
            tooltipRoot.gameObject.SetActive(false);
            tooltipCanvasGroup.alpha = 0f;
        }

        EnsureTagTooltip();
    }

    private void OnDisable()
    {
        HideTooltip(true);
        pulseTween?.Kill(false);
        modifierMarkerTween?.Kill(false);
    }

    private void OnDestroy()
    {
        tooltipTween?.Kill(false);
        tagTooltipTween?.Kill(false);
        pulseTween?.Kill(false);
        modifierMarkerTween?.Kill(false);
    }

    public void Bind(MagicModel magic)
    {
        if (magic == null)
        {
            this.magic = null;
            CacheMissingReferences();

            if (iconImage != null)
                iconImage.color = new Color(0.12f, 0.12f, 0.16f, 0.45f);

            if (backgroundImage != null)
                backgroundImage.color = emptyBackgroundColor;

            if (magicNameText != null)
                magicNameText.text = "空槽";

            if (tooltipNameText != null)
                tooltipNameText.text = "空法术槽";

            if (tooltipDescriptionText != null)
                tooltipDescriptionText.text = "选择奖励法术后，可以填入或覆盖此位置。";

            if (tagTooltipText != null)
                tagTooltipText.text = string.Empty;

            SetModifierMarkerVisible(false);
            RebuildRecipe();
            return;
        }

        this.magic = magic;
        CacheMissingReferences();

        if (iconImage != null)
        {
            iconImage.sprite = LoadMagicIcon(magic.Data.iconName);
            iconImage.color = iconImage.sprite != null ? Color.white : GetMagicElementColor(magic.Data.element);
        }

        if (backgroundImage != null)
            backgroundImage.color = GetMagicBackgroundColor(magic.Data.element);

        if (magicNameText != null)
            magicNameText.text = magic.Name;

        if (tooltipNameText != null)
            tooltipNameText.text = magic.Name;

        if (tooltipDescriptionText != null)
            tooltipDescriptionText.text = BuildDescriptionText(magic);

        if (tagTooltipText != null)
            tagTooltipText.text = BuildTagTooltipText(magic.Data.tagIds);

        SetModifierMarkerVisible(magic.HasModifier);
        RebuildRecipe();
    }

    public void ResetRecipeHighlights()
    {
        for (int i = 0; i < recipeBlocks.Count; i++)
            SetBlockAlpha(recipeBlocks[i], 0.35f);
    }

    public void HighlightRecipeSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= recipeBlocks.Count)
            return;

        SetBlockAlpha(recipeBlocks[slotIndex], 1f);
        recipeBlocks[slotIndex].transform.DOKill(false);
        recipeBlocks[slotIndex].transform.DOPunchScale(Vector3.one * recipeHighlightPunchScale, recipeHighlightDuration, recipeHighlightVibrato, recipeHighlightElasticity).SetTarget(this);
    }

    public void PulseCast()
    {
        pulseTween?.Kill(false);
        transform.localScale = Vector3.one;
        pulseTween = transform.DOPunchScale(Vector3.one * castPulseScale, castPulseDuration, castPulseVibrato, castPulseElasticity).SetTarget(this);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        ShowTooltip();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        HideTooltip(false);
    }

    private void CacheMissingReferences()
    {
        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();

        if (tagTooltipText != null)
            tagTooltipText.supportRichText = true;

        if (tooltipRoot != null)
        {
            Transform effectText = tooltipRoot.Find("EffectText");
            if (effectText != null)
                effectText.gameObject.SetActive(false);
        }

        EnsureModifierMarker();

        if (backgroundImage == null && !warnedMissingBackgroundImage)
        {
            warnedMissingBackgroundImage = true;
            GameLog.Data($"MagicItemView missing background image on {name}");
        }
    }

    private string BuildDescriptionText(MagicModel magic)
    {
        if (magic == null)
            return string.Empty;

        if (!magic.HasModifier || magic.PrimaryModifier == null || string.IsNullOrEmpty(magic.PrimaryModifier.Description))
            return magic.Description;

        return magic.Description + "\n*" + magic.PrimaryModifier.Description;
    }

    private void EnsureModifierMarker()
    {
        if (modifierMarkerImage == null)
        {
            Transform existing = transform.Find("ModifierMarker");
            if (existing != null)
                modifierMarkerImage = existing.GetComponent<Image>();
        }

        if (modifierMarkerImage == null)
        {
            modifierMarkerImage = new GameObject("ModifierMarker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            modifierMarkerImage.transform.SetParent(transform, false);
            modifierMarkerImage.raycastTarget = false;
            RectTransform rect = modifierMarkerImage.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(14f, -14f);
            rect.sizeDelta = new Vector2(18f, 18f);
        }

        modifierMarkerImage.color = new Color(1f, 0.88f, 0.38f, 1f);
        Shader shader = Shader.Find("UI/MagicModifierBreath");
        if (shader != null && modifierMarkerImage.material == null)
            modifierMarkerImage.material = new Material(shader);
    }

    private void SetModifierMarkerVisible(bool visible)
    {
        EnsureModifierMarker();
        if (modifierMarkerImage == null)
            return;

        modifierMarkerTween?.Kill(false);
        modifierMarkerImage.gameObject.SetActive(visible);
        if (!visible)
            return;

        Color baseColor = modifierMarkerImage.color;
        modifierMarkerImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0.58f);
        modifierMarkerTween = modifierMarkerImage.DOFade(1f, 0.86f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetTarget(this);
    }

    private void RebuildRecipe()
    {
        if (recipeRoot == null)
            return;

        recipeBlocks.Clear();
        for (int i = recipeRoot.childCount - 1; i >= 0; i--)
            Destroy(recipeRoot.GetChild(i).gameObject);

        if (magic == null || magic.Data.recipe == null)
            return;

        for (int i = 0; i < magic.Data.recipe.Length; i++)
        {
            Image block = new GameObject("MaterialBlock", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            block.transform.SetParent(recipeRoot, false);
            block.color = MaterialCardView.GetMaterialColor(magic.Data.recipe[i]);
            SetBlockAlpha(block, 0.35f);
            recipeBlocks.Add(block);

            RectTransform blockRect = (RectTransform)block.transform;
            blockRect.anchorMin = new Vector2(0f, 1f);
            blockRect.anchorMax = new Vector2(0f, 1f);
            blockRect.pivot = new Vector2(0f, 1f);
            blockRect.anchoredPosition = new Vector2((i % 4) * 22f, -(i / 4) * 22f);
            blockRect.sizeDelta = new Vector2(18f, 18f);
        }
    }

    private void ShowTooltip()
    {
        if (tooltipRoot == null || tooltipCanvasGroup == null)
            return;

        EnsureTagTooltip();
        tooltipTween?.Kill(false);
        tagTooltipTween?.Kill(false);
        tooltipRoot.gameObject.SetActive(true);
        PopupLayerUtility.ApplyTo(tooltipRoot);
        tooltipRoot.localScale = tooltipHiddenScale;

        Sequence sequence = DOTween.Sequence().SetTarget(this);
        sequence.Join(tooltipCanvasGroup.DOFade(1f, tooltipFadeDuration));
        sequence.Join(tooltipRoot.DOScale(Vector3.one, tooltipScaleDuration).SetEase(tooltipEase));
        tooltipTween = sequence;

        ShowTagTooltip();
    }

    private void ShowTagTooltip()
    {
        if (tagTooltipRoot == null || tagTooltipCanvasGroup == null || tagTooltipText == null || string.IsNullOrEmpty(tagTooltipText.text))
            return;

        tagTooltipText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, tagTooltipSize.x - 24f);
        tagTooltipText.text = tagTooltipText.text;
        Canvas.ForceUpdateCanvases();
        tagTooltipRoot.sizeDelta = GetTagTooltipSize();
        Vector2 shownPosition = GetTagTooltipShownPosition();
        tagTooltipRoot.gameObject.SetActive(true);
        PopupLayerUtility.ApplyTo(tagTooltipRoot);
        tagTooltipRoot.SetAsLastSibling();
        tagTooltipCanvasGroup.alpha = 0f;
        tagTooltipRoot.localScale = Vector3.one;
        tagTooltipRoot.anchoredPosition = shownPosition - new Vector2(tagTooltipSlideDistance, 0f);

        Sequence sequence = DOTween.Sequence().SetTarget(this);
        sequence.Join(tagTooltipCanvasGroup.DOFade(1f, tooltipFadeDuration));
        sequence.Join(tagTooltipRoot.DOAnchorPos(shownPosition, tooltipScaleDuration).SetEase(tooltipEase));
        tagTooltipTween = sequence;
    }

    private void HideTooltip(bool instant)
    {
        if (tooltipRoot == null || tooltipCanvasGroup == null)
            return;

        tooltipTween?.Kill(false);
        tagTooltipTween?.Kill(false);
        if (instant)
        {
            tooltipCanvasGroup.alpha = 0f;
            tooltipRoot.localScale = tooltipHiddenScale;
            tooltipRoot.gameObject.SetActive(false);
            if (tagTooltipRoot != null && tagTooltipCanvasGroup != null)
            {
                tagTooltipCanvasGroup.alpha = 0f;
                tagTooltipRoot.gameObject.SetActive(false);
            }
            return;
        }

        Sequence sequence = DOTween.Sequence().SetTarget(this);
        sequence.Join(tooltipCanvasGroup.DOFade(0f, tooltipFadeDuration));
        sequence.Join(tooltipRoot.DOScale(tooltipHiddenScale, tooltipScaleDuration).SetEase(Ease.InBack));
        tooltipTween = sequence.OnComplete(() => tooltipRoot.gameObject.SetActive(false));
        HideTagTooltip();
    }

    private void HideTagTooltip()
    {
        if (tagTooltipRoot == null || tagTooltipCanvasGroup == null || !tagTooltipRoot.gameObject.activeSelf)
            return;

        Vector2 hiddenPosition = GetTagTooltipShownPosition() - new Vector2(tagTooltipSlideDistance, 0f);
        Sequence sequence = DOTween.Sequence().SetTarget(this);
        sequence.Join(tagTooltipCanvasGroup.DOFade(0f, tooltipFadeDuration));
        sequence.Join(tagTooltipRoot.DOAnchorPos(hiddenPosition, tooltipScaleDuration).SetEase(Ease.InBack));
        tagTooltipTween = sequence.OnComplete(() => tagTooltipRoot.gameObject.SetActive(false));
    }

    private void EnsureTagTooltip()
    {
        if (tooltipRoot == null)
            return;

        if (tagTooltipRoot == null)
        {
            Transform existing = tooltipRoot.Find("TagTooltip");
            if (existing != null)
                tagTooltipRoot = (RectTransform)existing;
        }

        if (tagTooltipRoot == null)
        {
            tagTooltipRoot = new GameObject("TagTooltip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup)).GetComponent<RectTransform>();
            tagTooltipRoot.SetParent(tooltipRoot.parent, false);
            tagTooltipRoot.anchorMin = tooltipRoot.anchorMin;
            tagTooltipRoot.anchorMax = tooltipRoot.anchorMax;
            tagTooltipRoot.pivot = new Vector2(0f, 1f);
            tagTooltipRoot.sizeDelta = tagTooltipSize;
            Image image = tagTooltipRoot.GetComponent<Image>();
            image.color = new Color(0.03f, 0.03f, 0.04f, 0.96f);
            image.raycastTarget = false;
        }

        tagTooltipCanvasGroup = tagTooltipRoot.GetComponent<CanvasGroup>();
        if (tagTooltipCanvasGroup == null)
            tagTooltipCanvasGroup = tagTooltipRoot.gameObject.AddComponent<CanvasGroup>();
        PopupLayerUtility.ApplyTo(tagTooltipRoot);
        tagTooltipCanvasGroup.alpha = 0f;
        tagTooltipCanvasGroup.blocksRaycasts = false;
        tagTooltipRoot.gameObject.SetActive(false);

        if (tagTooltipText == null)
        {
            Transform textTransform = tagTooltipRoot.Find("Text");
            if (textTransform != null)
                tagTooltipText = textTransform.GetComponent<Text>();
        }

        if (tagTooltipText == null)
        {
            tagTooltipText = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)).GetComponent<Text>();
            tagTooltipText.transform.SetParent(tagTooltipRoot, false);
            tagTooltipText.font = tooltipDescriptionText != null && tooltipDescriptionText.font != null ? tooltipDescriptionText.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tagTooltipText.fontSize = 16;
            tagTooltipText.alignment = TextAnchor.UpperLeft;
            tagTooltipText.color = new Color(1f, 0.88f, 0.58f, 1f);
            tagTooltipText.raycastTarget = false;
            tagTooltipText.supportRichText = true;
            tagTooltipText.horizontalOverflow = HorizontalWrapMode.Wrap;
            tagTooltipText.verticalOverflow = VerticalWrapMode.Overflow;
            RectTransform textRect = (RectTransform)tagTooltipText.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12f, 10f);
            textRect.offsetMax = new Vector2(-12f, -10f);
        }
    }

    private Vector2 GetTagTooltipShownPosition()
    {
        if (tooltipRoot == null)
            return Vector2.zero;

        return tooltipRoot.anchoredPosition + new Vector2(tooltipRoot.sizeDelta.x * (1f - tooltipRoot.pivot.x) + tagTooltipXOffset, tooltipRoot.sizeDelta.y * (1f - tooltipRoot.pivot.y));
    }

    private Vector2 GetTagTooltipSize()
    {
        if (tagTooltipText == null || string.IsNullOrEmpty(tagTooltipText.text))
            return new Vector2(tagTooltipSize.x, tagTooltipVerticalPadding + tagTooltipLineHeight);

        float height = tagTooltipText.preferredHeight + tagTooltipVerticalPadding;
        return new Vector2(tagTooltipSize.x, height);
    }

    private static Color GetMagicElementColor(MaterialEnum element)
    {
        return element != MaterialEnum.None ? MaterialCardView.GetMaterialColor(element) : Color.gray;
    }

    private static Color GetMagicBackgroundColor(MaterialEnum element)
    {
        Color color = GetMagicElementColor(element);
        color = Color.Lerp(new Color(0.08f, 0.08f, 0.12f, 0.86f), color, 0.42f);
        color.a = 0.86f;
        return color;
    }

    private string BuildTagTooltipText(string[] tagIds)
    {
        if (tagIds == null || tagIds.Length == 0)
            return string.Empty;

        StringBuilder builder = null;
        for (int i = 0; i < tagIds.Length; i++)
        {
            string tagId = tagIds[i];
            if (string.IsNullOrEmpty(tagId) || !GameDataDatabase.TryGetTagData(tagId, out TagData tag))
                continue;

            string name = LocalizationKeys.GetTagName(tag);
            string description = LocalizationKeys.GetTagDescription(tag);
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(description))
                continue;

            if (builder == null)
                builder = new StringBuilder();
            else
                builder.Append("\n\n");

            builder.Append("<color=#FFE99E>");
            builder.Append(name);
            builder.Append("：</color>\n");
            builder.Append(description);
        }

        return builder != null ? builder.ToString() : string.Empty;
    }

    private static Sprite LoadMagicIcon(string iconName)
    {
        if (string.IsNullOrEmpty(iconName))
            return null;

        return Resources.Load<Sprite>("Images/Magics/" + iconName);
    }

    private static void SetBlockAlpha(Image block, float alpha)
    {
        Color color = block.color;
        color.a = alpha;
        block.color = color;
    }
}
