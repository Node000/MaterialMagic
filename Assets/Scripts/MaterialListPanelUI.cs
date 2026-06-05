using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MaterialListPanelUI : MonoBehaviour
{
    public enum DisplayMode
    {
        FullDeck,
        DrawPile,
        DiscardPile
    }

    [SerializeField] private RectTransform waterMaterialRow;
    [SerializeField] private RectTransform fireMaterialRow;
    [SerializeField] private RectTransform windMaterialRow;
    [SerializeField] private RectTransform earthMaterialRow;
    [SerializeField] private RectTransform materialCardPrefab;
    [SerializeField] private float materialRowMinSpacing = 16f;
    [SerializeField] private float materialRowMaxSpacing = 140f;
    [SerializeField] private float compactMaterialRowMinSpacing = 4f;
    [SerializeField] private float compactMaterialRowMaxSpacing = 72f;
    [SerializeField] private float compactMaterialCardScale = 0.72f;
    [Header("动画参数")]
    [SerializeField] private float tooltipFadeDuration = 0.12f;
    [SerializeField] private float tooltipScaleDuration = 0.18f;
    [SerializeField] private Ease tooltipShowEase = Ease.OutBack;
    [SerializeField] private Ease tooltipHideEase = Ease.InBack;
    [SerializeField] private Vector3 tooltipHiddenScale = new Vector3(0.82f, 0.82f, 1f);
    [SerializeField] private float tooltipYOffset = 30f;

    private HandSystemUI owner;
    private TMP_Text titleText;
    private RectTransform modifierTooltip;
    private RectTransform modifierTooltipContent;
    private CanvasGroup modifierTooltipCanvasGroup;
    private Tween modifierTooltipTween;
    private readonly List<TMP_Text> modifierTooltipTexts = new List<TMP_Text>();
    private readonly Vector3[] tooltipAnchorCorners = new Vector3[4];
    private readonly List<MaterialModel> selectedMaterials = new List<MaterialModel>();
    private Predicate<MaterialModel> selectionPredicate;
    private Action<IReadOnlyList<MaterialModel>> selectionCompleted;
    private int selectionCount;
    private bool selectionLocked;
    private DisplayMode displayMode = DisplayMode.DrawPile;

    public void Initialize(HandSystemUI owner)
    {
        this.owner = owner;
        CacheReferences();
        BindCloseButton();
    }

    public void Toggle()
    {
        Toggle(DisplayMode.DrawPile);
    }

    public void Toggle(DisplayMode mode)
    {
        if (selectionLocked)
            return;

        if (gameObject.activeSelf && displayMode == mode)
        {
            ClearSelectionMode();
            gameObject.SetActive(false);
            return;
        }

        displayMode = mode;
        gameObject.SetActive(true);
        Refresh();
    }

    public void BeginSelection(int count, Predicate<MaterialModel> predicate, Action<IReadOnlyList<MaterialModel>> onCompleted)
    {
        selectionCount = Mathf.Max(1, count);
        selectionPredicate = predicate;
        selectionCompleted = onCompleted;
        selectionLocked = true;
        displayMode = DisplayMode.FullDeck;
        selectedMaterials.Clear();
        gameObject.SetActive(true);
        Refresh();
    }

    public void OnMaterialCardClicked(MaterialCardView cardView)
    {
        if (selectionCompleted == null || cardView == null || cardView.MaterialModel == null)
            return;

        MaterialModel materialModel = cardView.MaterialModel;
        if (selectionPredicate != null && !selectionPredicate(materialModel))
            return;

        if (selectedMaterials.Contains(materialModel))
            return;

        selectedMaterials.Add(materialModel);
        if (selectedMaterials.Count < selectionCount)
            return;

        Action<IReadOnlyList<MaterialModel>> completed = selectionCompleted;
        List<MaterialModel> result = new List<MaterialModel>(selectedMaterials);
        ClearSelectionMode();
        gameObject.SetActive(false);
        completed(result);
    }

    public void Refresh()
    {
        CacheReferences();
        RefreshTitle();
        RefreshRow(waterMaterialRow, MaterialEnum.Water);
        RefreshRow(fireMaterialRow, MaterialEnum.Fire);
        RefreshRow(windMaterialRow, MaterialEnum.Wind);
        RefreshRow(earthMaterialRow, MaterialEnum.Earth);
    }

    public void ShowModifierTooltip(MaterialCardView cardView, MaterialModel materialModel)
    {
        if (cardView == null)
            return;

        ShowModifierTooltip(cardView.RectTransform, materialModel);
    }

    public void ShowModifierTooltip(RectTransform anchor, MaterialModel materialModel)
    {
        if (anchor == null || materialModel == null)
            return;

        EnsureModifierTooltip();
        if (modifierTooltip == null || modifierTooltipCanvasGroup == null)
            return;

        RebuildModifierTooltip(materialModel);
        modifierTooltip.gameObject.SetActive(true);
        PopupLayerUtility.ApplyTo(modifierTooltip);
        modifierTooltip.SetAsLastSibling();
        modifierTooltip.anchoredPosition = GetModifierTooltipAnchoredPosition(anchor);
        modifierTooltip.localScale = tooltipHiddenScale;
        modifierTooltipCanvasGroup.alpha = 0f;
        modifierTooltipTween?.Kill(false);
        Sequence sequence = DOTween.Sequence().SetTarget(this);
        sequence.Join(modifierTooltipCanvasGroup.DOFade(1f, tooltipFadeDuration));
        sequence.Join(modifierTooltip.DOScale(Vector3.one, tooltipScaleDuration).SetEase(tooltipShowEase));
        modifierTooltipTween = sequence;
    }

    private Vector2 GetModifierTooltipAnchoredPosition(RectTransform anchor)
    {
        RectTransform parent = modifierTooltip != null ? modifierTooltip.parent as RectTransform : null;
        if (anchor == null || parent == null)
            return Vector2.zero;

        anchor.GetWorldCorners(tooltipAnchorCorners);
        Vector3 cardCenter = (tooltipAnchorCorners[0] + tooltipAnchorCorners[2]) * 0.5f;
        Vector3 localPoint = parent.InverseTransformPoint(cardCenter);
        return new Vector2(localPoint.x, localPoint.y + tooltipYOffset);
    }

    public void HideModifierTooltip(MaterialCardView cardView)
    {
        HideModifierTooltip(cardView != null ? cardView.RectTransform : null);
    }

    public void HideModifierTooltip(RectTransform anchor)
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
        if (!selectionLocked)
            ClearSelectionMode();
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
        closeButton.onClick.AddListener(() =>
        {
            if (selectionLocked)
                return;

            ClearSelectionMode();
            gameObject.SetActive(false);
        });
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
        if (titleText == null)
            titleText = UIManager.FindChildComponent<TMP_Text>(transform, "Title");
        if (materialCardPrefab == null)
        {
            PrefabReferenceLibrary library = GetComponentInParent<PrefabReferenceLibrary>();
            if (library != null)
                materialCardPrefab = library.MaterialCardPrefab;
        }
    }

    private void RefreshTitle()
    {
        if (titleText == null)
            return;

        if (selectionCompleted != null)
        {
            titleText.text = "选择素材";
            return;
        }

        switch (displayMode)
        {
            case DisplayMode.DiscardPile:
                titleText.text = "弃牌堆";
                break;
            case DisplayMode.DrawPile:
                titleText.text = "抽牌堆";
                break;
            default:
                titleText.text = "素材列表";
                break;
        }
    }

    private void RefreshRow(RectTransform row, MaterialEnum material)
    {
        if (row == null || materialCardPrefab == null || owner == null || owner.PlayerState == null)
            return;

        for (int i = row.childCount - 1; i >= 0; i--)
            Destroy(row.GetChild(i).gameObject);

        int materialCount = CountMaterialsForDisplay(material);
        ApplyRowSpacing(row, materialCount);
        CreateMaterialCardsForDisplay(row, material);
    }

    private int CountMaterialsForDisplay(MaterialEnum material)
    {
        PlayerState state = owner.PlayerState;
        int count = 0;
        if (selectionCompleted == null && displayMode == DisplayMode.DrawPile)
        {
            for (int i = 0; i < state.DrawPile.Count; i++)
                count += CountMaterialRows(state.DrawPile[i], material);
            return count;
        }

        if (selectionCompleted == null && displayMode == DisplayMode.DiscardPile)
        {
            for (int i = 0; i < state.DiscardPile.Count; i++)
                count += CountMaterialRows(state.DiscardPile[i], material);
            return count;
        }

        for (int i = 0; i < state.Deck.Count; i++)
            count += CountMaterialRows(state.Deck[i], material);
        for (int i = 0; i < state.Hand.Count; i++)
        {
            MaterialModel materialModel = state.Hand[i];
            if (materialModel != null && !state.Deck.Contains(materialModel))
                count += CountMaterialRows(materialModel, material);
        }
        for (int i = 0; i < state.PlayZone.Count; i++)
        {
            MaterialModel materialModel = state.PlayZone[i];
            if (materialModel != null && !state.Deck.Contains(materialModel))
                count += CountMaterialRows(materialModel, material);
        }
        return count;
    }

    private void CreateMaterialCardsForDisplay(RectTransform row, MaterialEnum material)
    {
        PlayerState state = owner.PlayerState;
        if (selectionCompleted == null && displayMode == DisplayMode.DrawPile)
        {
            for (int i = 0; i < state.DrawPile.Count; i++)
                CreateMaterialCardsForRow(row, state.DrawPile[i], material);
            return;
        }

        if (selectionCompleted == null && displayMode == DisplayMode.DiscardPile)
        {
            for (int i = 0; i < state.DiscardPile.Count; i++)
                CreateMaterialCardsForRow(row, state.DiscardPile[i], material);
            return;
        }

        for (int i = 0; i < state.Deck.Count; i++)
            CreateMaterialCardsForRow(row, state.Deck[i], material);
        for (int i = 0; i < state.Hand.Count; i++)
        {
            MaterialModel materialModel = state.Hand[i];
            if (materialModel != null && !state.Deck.Contains(materialModel))
                CreateMaterialCardsForRow(row, materialModel, material);
        }
        for (int i = 0; i < state.PlayZone.Count; i++)
        {
            MaterialModel materialModel = state.PlayZone[i];
            if (materialModel != null && !state.Deck.Contains(materialModel))
                CreateMaterialCardsForRow(row, materialModel, material);
        }
    }

    private int CountMaterialRows(MaterialModel materialModel, MaterialEnum material)
    {
        if (materialModel == null)
            return 0;

        int count = materialModel.material == material ? 1 : 0;
        if (materialModel.alternateMaterial == material)
            count++;
        for (int i = 0; i < materialModel.modifiers.Count; i++)
        {
            if (materialModel.modifiers[i] != null && materialModel.modifiers[i].CanActAs(material))
                count++;
        }
        return count;
    }

    private void CreateMaterialCardsForRow(RectTransform row, MaterialModel materialModel, MaterialEnum material)
    {
        if (materialModel == null)
            return;

        if (materialModel.material == material)
            CreateMaterialCard(row, materialModel);
        if (materialModel.alternateMaterial == material)
            CreateMaterialCard(row, materialModel);
        for (int i = 0; i < materialModel.modifiers.Count; i++)
        {
            if (materialModel.modifiers[i] != null && materialModel.modifiers[i].CanActAs(material))
                CreateMaterialCard(row, materialModel);
        }
    }

    private void CreateMaterialCard(RectTransform parent, MaterialModel materialModel)
    {
        RectTransform cardRect = Instantiate(materialCardPrefab, parent);
        cardRect.gameObject.SetActive(true);
        ApplyCardDisplaySize(cardRect);
        MaterialCardView view = cardRect.GetComponent<MaterialCardView>();
        if (view != null)
        {
            view.Initialize(this);
            bool inactive = selectionCompleted != null ? !IsSelectableInCurrentSelection(materialModel) : displayMode == DisplayMode.FullDeck && IsConsumedInCurrentBattle(materialModel);
            view.Bind(materialModel, inactive);
        }

        JuicyMotion motion = cardRect.GetComponent<JuicyMotion>();
        if (motion == null)
            motion = cardRect.gameObject.AddComponent<JuicyMotion>();
        motion.enabled = true;
    }

    private void ApplyCardDisplaySize(RectTransform cardRect)
    {
        if (cardRect == null || !IsCompactDisplay())
            return;

        float scale = Mathf.Clamp(compactMaterialCardScale, 0.4f, 1f);
        Vector2 prefabSize = materialCardPrefab.rect.size;
        if (prefabSize.x <= 0f || prefabSize.y <= 0f)
            prefabSize = materialCardPrefab.sizeDelta;
        cardRect.sizeDelta = prefabSize * scale;
        cardRect.localScale = Vector3.one;
    }

    private bool IsCompactDisplay()
    {
        return selectionCompleted == null && displayMode != DisplayMode.FullDeck;
    }

    private bool IsConsumedInCurrentBattle(MaterialModel materialModel)
    {
        return owner != null && owner.IsDeckCardConsumedInCurrentBattle(materialModel);
    }

    private bool IsSelectableInCurrentSelection(MaterialModel materialModel)
    {
        return selectionCompleted == null || (materialModel != null && (selectionPredicate == null || selectionPredicate(materialModel)));
    }

    private void ClearSelectionMode()
    {
        selectionPredicate = null;
        selectionCompleted = null;
        selectionLocked = false;
        selectionCount = 0;
        selectedMaterials.Clear();
    }

    private void EnsureModifierTooltip()
    {
        if (modifierTooltip != null)
            return;

        Transform parent = transform.parent != null ? transform.parent : transform;
        Image background = new GameObject("MaterialModifierTooltip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup)).GetComponent<Image>();
        background.transform.SetParent(parent, false);
        background.color = new Color(0.055f, 0.05f, 0.065f, 1f);
        background.raycastTarget = false;
        modifierTooltip = background.rectTransform;
        modifierTooltip.sizeDelta = new Vector2(330f, 92f);
        modifierTooltip.anchorMin = new Vector2(0.5f, 0.5f);
        modifierTooltip.anchorMax = new Vector2(0.5f, 0.5f);
        modifierTooltip.pivot = new Vector2(0.5f, 0f);
        modifierTooltipCanvasGroup = background.GetComponent<CanvasGroup>();
        PopupLayerUtility.ApplyTo(modifierTooltip);
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
        int modifierCount = materialModel.modifiers.Count;
        int count = modifierCount + 1;
        for (int i = 0; i < modifierTooltipTexts.Count; i++)
            modifierTooltipTexts[i].gameObject.SetActive(i < count);

        TMP_Text effectText = GetModifierTooltipText(0);
        effectText.text = GetMaterialSinglePlayEffectText(materialModel.material);

        for (int i = 0; i < modifierCount; i++)
        {
            TMP_Text text = GetModifierTooltipText(i + 1);
            MaterialModifierModel modifier = materialModel.modifiers[i];
            string description = LocalizationKeys.GetModifierDescription(modifier);
            text.text = string.IsNullOrEmpty(description)
                ? LocalizationKeys.GetModifierName(modifier)
                : LocalizationKeys.GetModifierName(modifier) + "：" + description;
        }

        float height = Mathf.Clamp(20f + count * 48f, 72f, 260f);
        modifierTooltip.sizeDelta = new Vector2(modifierTooltip.sizeDelta.x, height);
    }

    private static string GetMaterialSinglePlayEffectText(MaterialEnum material)
    {
        switch (material)
        {
            case MaterialEnum.Fire:
                return "单独打出：对敌方造成3点伤害";
            case MaterialEnum.Water:
                return "单独打出：对敌方施加2层虚弱";
            case MaterialEnum.Wind:
                return "单独打出：下回合抽牌+1";
            case MaterialEnum.Earth:
                return "单独打出：友方获得3点护盾";
            default:
                return "单独打出：无基础效果";
        }
    }

    private TMP_Text GetModifierTooltipText(int index)
    {
        while (modifierTooltipTexts.Count <= index)
        {
            TMP_Text text = new GameObject("ModifierText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
            text.transform.SetParent(modifierTooltipContent, false);
            text.font = UIManager.GetDefaultTMPFont();
            text.fontSize = 15;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.color = Color.white;
            text.raycastTarget = false;
            RectTransform rect = text.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0f, 42f);
            modifierTooltipTexts.Add(text);
        }

        TMP_Text result = modifierTooltipTexts[index];
        result.gameObject.SetActive(true);
        return result;
    }

    private void ApplyRowSpacing(RectTransform row, int materialCount)
    {
        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        if (layout == null || materialCardPrefab == null)
            return;

        float minSpacing = IsCompactDisplay() ? compactMaterialRowMinSpacing : materialRowMinSpacing;
        if (materialCount <= 1)
        {
            layout.spacing = minSpacing;
            return;
        }

        float maxSpacing = IsCompactDisplay() ? compactMaterialRowMaxSpacing : materialRowMaxSpacing;
        float availableSpacing = (row.rect.width - GetCardDisplayWidth() * materialCount) / (materialCount - 1);
        layout.spacing = Mathf.Clamp(availableSpacing, minSpacing, maxSpacing);
    }

    private float GetCardDisplayWidth()
    {
        float width = materialCardPrefab.rect.width;
        if (width <= 0f)
            width = materialCardPrefab.sizeDelta.x;
        return IsCompactDisplay() ? width * Mathf.Clamp(compactMaterialCardScale, 0.4f, 1f) : width;
    }
}
