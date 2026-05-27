using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class MaterialListPanelUI : MonoBehaviour
{
    [SerializeField] private RectTransform waterMaterialRow;
    [SerializeField] private RectTransform fireMaterialRow;
    [SerializeField] private RectTransform windMaterialRow;
    [SerializeField] private RectTransform earthMaterialRow;
    [SerializeField] private RectTransform materialCardPrefab;
    [SerializeField] private float materialRowMinSpacing = 16f;
    [SerializeField] private float materialRowMaxSpacing = 140f;
    [Header("动画参数")]
    [SerializeField] private float tooltipFadeDuration = 0.12f;
    [SerializeField] private float tooltipScaleDuration = 0.18f;
    [SerializeField] private Ease tooltipShowEase = Ease.OutBack;
    [SerializeField] private Ease tooltipHideEase = Ease.InBack;
    [SerializeField] private Vector3 tooltipHiddenScale = new Vector3(0.82f, 0.82f, 1f);
    [SerializeField] private float tooltipYOffset = 72f;

    private HandSystemUI owner;
    private RectTransform modifierTooltip;
    private RectTransform modifierTooltipContent;
    private CanvasGroup modifierTooltipCanvasGroup;
    private Tween modifierTooltipTween;
    private readonly List<Text> modifierTooltipTexts = new List<Text>();

    public void Initialize(HandSystemUI owner)
    {
        this.owner = owner;
        CacheReferences();
        BindCloseButton();
    }

    public void Toggle()
    {
        bool show = !gameObject.activeSelf;
        gameObject.SetActive(show);
        if (show)
            Refresh();
    }

    public void Refresh()
    {
        CacheReferences();
        RefreshRow(waterMaterialRow, MaterialEnum.Water);
        RefreshRow(fireMaterialRow, MaterialEnum.Fire);
        RefreshRow(windMaterialRow, MaterialEnum.Wind);
        RefreshRow(earthMaterialRow, MaterialEnum.Earth);
    }

    public void ShowModifierTooltip(MaterialCardView cardView, MaterialModel materialModel)
    {
        if (cardView == null || materialModel == null || materialModel.modifiers.Count == 0)
            return;

        EnsureModifierTooltip();
        if (modifierTooltip == null || modifierTooltipCanvasGroup == null)
            return;

        RebuildModifierTooltip(materialModel);
        modifierTooltip.gameObject.SetActive(true);
        modifierTooltip.SetAsLastSibling();
        modifierTooltip.position = cardView.RectTransform.position + new Vector3(0f, tooltipYOffset, 0f);
        modifierTooltip.localScale = tooltipHiddenScale;
        modifierTooltipCanvasGroup.alpha = 0f;
        modifierTooltipTween?.Kill(false);
        Sequence sequence = DOTween.Sequence().SetTarget(this);
        sequence.Join(modifierTooltipCanvasGroup.DOFade(1f, tooltipFadeDuration));
        sequence.Join(modifierTooltip.DOScale(Vector3.one, tooltipScaleDuration).SetEase(tooltipShowEase));
        modifierTooltipTween = sequence;
    }

    public void HideModifierTooltip(MaterialCardView cardView)
    {
        if (modifierTooltip == null || !modifierTooltip.gameObject.activeSelf)
            return;

        modifierTooltipTween?.Kill(false);
        Sequence sequence = DOTween.Sequence().SetTarget(this);
        sequence.Join(modifierTooltipCanvasGroup.DOFade(0f, tooltipFadeDuration));
        sequence.Join(modifierTooltip.DOScale(tooltipHiddenScale, tooltipScaleDuration).SetEase(tooltipHideEase));
        sequence.OnComplete(() => modifierTooltip.gameObject.SetActive(false));
        modifierTooltipTween = sequence;
    }

    private void OnDisable()
    {
        modifierTooltipTween?.Kill(false);
        if (modifierTooltip != null)
            modifierTooltip.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        modifierTooltipTween?.Kill(false);
    }

    private void BindCloseButton()
    {
        Button closeButton = UIManager.FindChildComponent<Button>(transform, "CloseButton");
        if (closeButton == null)
            return;

        closeButton.onClick.RemoveAllListeners();
        closeButton.onClick.AddListener(() => gameObject.SetActive(false));
    }

    private void CacheReferences()
    {
        if (waterMaterialRow == null)
            waterMaterialRow = UIManager.FindChildRect(transform, "WaterRow");
        if (fireMaterialRow == null)
            fireMaterialRow = UIManager.FindChildRect(transform, "FireRow");
        if (windMaterialRow == null)
            windMaterialRow = UIManager.FindChildRect(transform, "WindRow");
        if (earthMaterialRow == null)
            earthMaterialRow = UIManager.FindChildRect(transform, "EarthRow");
        if (materialCardPrefab == null)
        {
            PrefabReferenceLibrary library = GetComponentInParent<PrefabReferenceLibrary>();
            if (library != null)
                materialCardPrefab = library.MaterialCardPrefab;
        }
    }

    private void RefreshRow(RectTransform row, MaterialEnum material)
    {
        if (row == null || materialCardPrefab == null || owner == null || owner.PlayerState == null)
            return;

        for (int i = row.childCount - 1; i >= 0; i--)
            Destroy(row.GetChild(i).gameObject);

        int materialCount = 0;
        for (int i = 0; i < owner.PlayerState.Deck.Count; i++)
        {
            MaterialModel materialModel = owner.PlayerState.Deck[i];
            if (materialModel != null && materialModel.material == material)
                materialCount++;
        }

        ApplyRowSpacing(row, materialCount);

        for (int i = 0; i < owner.PlayerState.Deck.Count; i++)
        {
            MaterialModel materialModel = owner.PlayerState.Deck[i];
            if (materialModel != null && materialModel.material == material)
                CreateMaterialCard(row, materialModel);
        }
    }

    private void CreateMaterialCard(RectTransform parent, MaterialModel materialModel)
    {
        RectTransform cardRect = Instantiate(materialCardPrefab, parent);
        cardRect.gameObject.SetActive(true);
        MaterialCardView view = cardRect.GetComponent<MaterialCardView>();
        if (view != null)
        {
            view.Initialize(this);
            view.Bind(materialModel, IsConsumedInCurrentBattle(materialModel));
        }

        JuicyMotion motion = cardRect.GetComponent<JuicyMotion>();
        if (motion == null)
            motion = cardRect.gameObject.AddComponent<JuicyMotion>();
        motion.enabled = true;
    }

    private bool IsConsumedInCurrentBattle(MaterialModel materialModel)
    {
        return owner != null && owner.IsDeckCardConsumedInCurrentBattle(materialModel);
    }

    private void EnsureModifierTooltip()
    {
        if (modifierTooltip != null)
            return;

        Transform parent = transform.parent != null ? transform.parent : transform;
        Image background = new GameObject("MaterialModifierTooltip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup)).GetComponent<Image>();
        background.transform.SetParent(parent, false);
        background.color = new Color(0.055f, 0.05f, 0.065f, 0.94f);
        background.raycastTarget = false;
        modifierTooltip = background.rectTransform;
        modifierTooltip.sizeDelta = new Vector2(330f, 92f);
        modifierTooltip.anchorMin = new Vector2(0.5f, 0.5f);
        modifierTooltip.anchorMax = new Vector2(0.5f, 0.5f);
        modifierTooltip.pivot = new Vector2(0.5f, 0f);
        modifierTooltipCanvasGroup = background.GetComponent<CanvasGroup>();
        modifierTooltipCanvasGroup.alpha = 0f;

        modifierTooltipContent = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
        modifierTooltipContent.SetParent(modifierTooltip, false);
        modifierTooltipContent.anchorMin = new Vector2(0f, 0f);
        modifierTooltipContent.anchorMax = new Vector2(1f, 1f);
        modifierTooltipContent.offsetMin = new Vector2(14f, 10f);
        modifierTooltipContent.offsetMax = new Vector2(-14f, -10f);
        VerticalLayoutGroup layout = modifierTooltipContent.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 6f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = modifierTooltipContent.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        modifierTooltip.gameObject.SetActive(false);
    }

    private void RebuildModifierTooltip(MaterialModel materialModel)
    {
        int count = materialModel.modifiers.Count;
        for (int i = 0; i < modifierTooltipTexts.Count; i++)
            modifierTooltipTexts[i].gameObject.SetActive(i < count);

        for (int i = 0; i < count; i++)
        {
            Text text = GetModifierTooltipText(i);
            MaterialModifierModel modifier = materialModel.modifiers[i];
            string description = LocalizationKeys.GetModifierDescription(modifier);
            text.text = string.IsNullOrEmpty(description)
                ? LocalizationKeys.GetModifierName(modifier)
                : LocalizationKeys.GetModifierName(modifier) + "：" + description;
        }

        float height = Mathf.Clamp(20f + count * 48f, 72f, 220f);
        modifierTooltip.sizeDelta = new Vector2(modifierTooltip.sizeDelta.x, height);
    }

    private Text GetModifierTooltipText(int index)
    {
        while (modifierTooltipTexts.Count <= index)
        {
            Text text = new GameObject("ModifierText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)).GetComponent<Text>();
            text.transform.SetParent(modifierTooltipContent, false);
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 15;
            text.alignment = TextAnchor.UpperLeft;
            text.color = Color.white;
            text.raycastTarget = false;
            RectTransform rect = text.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0f, 42f);
            modifierTooltipTexts.Add(text);
        }

        Text result = modifierTooltipTexts[index];
        result.gameObject.SetActive(true);
        return result;
    }

    private void ApplyRowSpacing(RectTransform row, int materialCount)
    {
        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        if (layout == null || materialCount <= 1 || materialCardPrefab == null)
            return;

        float availableSpacing = (row.rect.width - materialCardPrefab.rect.width * materialCount) / (materialCount - 1);
        layout.spacing = Mathf.Clamp(availableSpacing, materialRowMinSpacing, materialRowMaxSpacing);
    }
}
