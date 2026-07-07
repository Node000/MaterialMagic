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
        DiscardPile,
        ConsumedPile,
        CombatPiles
    }

    [SerializeField] private RectTransform waterMaterialRow;
    [SerializeField] private RectTransform fireMaterialRow;
    [SerializeField] private RectTransform windMaterialRow;
    [SerializeField] private RectTransform earthMaterialRow;
    [SerializeField] private RectTransform wildMaterialRow;
    [SerializeField] private RectTransform rowContainer;
    [SerializeField] private BattleMaterialRowUI drawPileRow;
    [SerializeField] private BattleMaterialRowUI discardPileRow;
    [SerializeField] private BattleMaterialRowUI consumedPileRow;
    [SerializeField] private BattleMaterialRowUI selectionRow;
    [Header("箭头行布局")]
    [SerializeField] private MaterialListPanelLayoutConfig layoutConfig;
    [SerializeField] private ArrowSelectionWaveHoverConfig arrowSelectionWaveHoverConfig;
    [Header("动画参数")]
    [SerializeField] private float tooltipFadeDuration = 0.12f;
    [SerializeField] private float tooltipScaleDuration = 0.18f;
    [SerializeField] private Ease tooltipShowEase = Ease.OutBack;
    [SerializeField] private Ease tooltipHideEase = Ease.InBack;
    [SerializeField] private Vector3 tooltipHiddenScale = new Vector3(0.82f, 0.82f, 1f);
    [SerializeField] private float tooltipYOffset = 30f;
    [SerializeField] private RectTransform selectionConfirmButton;
    [SerializeField] private CanvasGroup selectionConfirmCanvasGroup;
    [SerializeField] private Button selectionConfirmButtonComponent;
    [SerializeField] private TMP_Text selectionConfirmButtonText;
    [SerializeField] private float selectionConfirmDisabledAlpha = 0.35f;
    [SerializeField] private string selectionConfirmText = "确认";

    private const string LayoutConfigResourcePath = "Config/MaterialListPanelLayoutConfig";

    private HandSystemUI owner;
    private MaterialListPanelLayoutConfig cachedLayoutConfig;
    private TMP_Text titleText;
    private readonly List<BattleMaterialRowUI> activeRows = new List<BattleMaterialRowUI>();
    private readonly List<MaterialModel> selectionCandidates = new List<MaterialModel>();
    private readonly List<MaterialModel> selectedMaterials = new List<MaterialModel>();
    private Predicate<MaterialModel> selectionPredicate;
    private Action<IReadOnlyList<MaterialModel>> selectionCompleted;
    private Action selectionCancelled;
    private int selectionCount;
    private bool selectionLocked;
    private string selectionTitleOverride;
    private DisplayMode displayMode = DisplayMode.CombatPiles;

    public void Initialize(HandSystemUI owner)
    {
        this.owner = owner;
        CacheReferences();
        BindCloseButton();
    }

    public void Toggle()
    {
        Toggle(DisplayMode.CombatPiles);
    }

    public void Toggle(DisplayMode mode)
    {
        if (selectionLocked)
            return;

        if (mode == DisplayMode.DrawPile || mode == DisplayMode.DiscardPile || mode == DisplayMode.ConsumedPile)
            mode = DisplayMode.CombatPiles;

        if (gameObject.activeSelf && displayMode == mode)
        {
            ClearSelectionMode();
            gameObject.SetActive(false);
            return;
        }

        displayMode = mode;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        Refresh();
    }

    public void BeginSelection(int count, Predicate<MaterialModel> predicate, Action<IReadOnlyList<MaterialModel>> onCompleted)
    {
        BeginSelection(count, predicate, onCompleted, null, null);
    }

    public void BeginSelection(int count, Predicate<MaterialModel> predicate, Action<IReadOnlyList<MaterialModel>> onCompleted, Action onCancelled)
    {
        BeginSelection(count, predicate, onCompleted, onCancelled, null);
    }

    public void BeginSelection(int count, Predicate<MaterialModel> predicate, Action<IReadOnlyList<MaterialModel>> onCompleted, Action onCancelled, string titleOverride)
    {
        selectionCount = Mathf.Max(1, count);
        selectionPredicate = predicate;
        selectionCompleted = onCompleted;
        selectionCancelled = onCancelled;
        selectionTitleOverride = titleOverride;
        selectionLocked = true;
        displayMode = DisplayMode.FullDeck;
        selectedMaterials.Clear();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        Refresh();
    }

    public void OnMaterialCardClicked(MaterialCardView cardView)
    {
        if (cardView != null)
            HandleMaterialClicked(cardView.MaterialModel);
    }

    public bool ShouldUseMobileInteraction()
    {
        return owner != null && owner.ShouldUseMobileInteraction();
    }

    public void Refresh()
    {
        CacheReferences();
        RefreshTitle();
        EnsurePanelBlocksRaycasts();
        EnsureRows();
        HideRows();
        Canvas.ForceUpdateCanvases();

        if (owner == null || owner.PlayerState == null)
            return;

        if (selectionCompleted != null)
        {
            RefreshSelectionRow();
            EnsureSelectionConfirmButton();
            UpdateSelectionConfirmButtonState();
        }
        else
        {
            RefreshCombatPileRows();
        }
    }

    public void ShowModifierTooltip(MaterialCardView cardView, MaterialModel materialModel)
    {
        if (cardView == null)
            return;

        ShowModifierTooltip(cardView.RectTransform, materialModel);
    }

    public void ShowModifierTooltip(RectTransform anchor, MaterialModel materialModel)
    {
        if (anchor == null || materialModel == null || owner == null)
            return;

        owner.GetUIManager().ShowUnifiedDetailPopup(anchor, UnifiedDetailContentBuilder.Build(materialModel));
    }

    public void HideModifierTooltip(MaterialCardView cardView)
    {
        HideModifierTooltip(cardView != null ? cardView.RectTransform : null);
    }

    public void HideModifierTooltip(RectTransform anchor)
    {
        if (owner == null)
            return;

        owner.GetUIManager().HideUnifiedDetailPopup(anchor);
    }

    public void EndSelectionMode()
    {
        ClearSelectionMode();
        gameObject.SetActive(false);
    }

    private void RefreshCombatPileRows()
    {
        PlayerState state = owner.PlayerState;
        RefreshRow(drawPileRow, LocalizationSystem.GetText("ui.material_list.draw_pile", "抽牌堆"), state.DrawPile, null, false);
        RefreshRow(discardPileRow, LocalizationSystem.GetText("ui.material_list.discard_pile", "弃牌堆"), state.DiscardPile, null, false);
        RefreshRow(consumedPileRow, LocalizationSystem.GetText("ui.material_list.consumed_pile", "已消耗"), state.ConsumedPile, null, false);
    }

    private void RefreshSelectionRow()
    {
        BuildSelectionCandidates();
        string title = !string.IsNullOrEmpty(selectionTitleOverride)
            ? selectionTitleOverride
            : LocalizationSystem.GetText("ui.material_list.select_material", "选择素材");
        RefreshRow(selectionRow, title, selectionCandidates, selectionPredicate, true);
    }

    private void RefreshRow(BattleMaterialRowUI row, string title, IReadOnlyList<MaterialModel> materials, Predicate<MaterialModel> predicate, bool hideUnselectable)
    {
        if (row == null)
            return;

        row.gameObject.SetActive(true);
        row.SetOwnerPanel(this);
        activeRows.Add(row);
        row.MaterialHovered -= ShowRowMaterialTooltip;
        row.MaterialUnhovered -= HideRowMaterialTooltip;
        row.MaterialClicked -= HandleMaterialClicked;
        row.MaterialHovered += ShowRowMaterialTooltip;
        row.MaterialUnhovered += HideRowMaterialTooltip;
        row.MaterialClicked += HandleMaterialClicked;
        MaterialListPanelLayoutConfig config = GetLayoutConfig();
        float rowTotalLength = config != null ? config.ArrowRowTotalLength : 780f;
        float defaultScale = arrowSelectionWaveHoverConfig != null ? arrowSelectionWaveHoverConfig.HoverScale : (config != null ? config.ArrowDefaultScale : 0.72f);
        float hoverScale = arrowSelectionWaveHoverConfig != null ? arrowSelectionWaveHoverConfig.HoverScale : (config != null ? config.ArrowHoverScale : 1.18f);
        float hoverYOffset = arrowSelectionWaveHoverConfig != null ? arrowSelectionWaveHoverConfig.HoverYOffset : 32f;
        float hoverCurvePower = arrowSelectionWaveHoverConfig != null ? arrowSelectionWaveHoverConfig.FalloffPower : 1.35f;
        row.ConfigureArrowRowLayout(rowTotalLength, defaultScale, hoverScale, hoverYOffset, hoverCurvePower);
        row.SetHoverSelectionOutlineEnabled(selectionCompleted != null);
        row.Refresh(title, materials, predicate, selectedMaterials, hideUnselectable);
    }

    private MaterialListPanelLayoutConfig GetLayoutConfig()
    {
        if (layoutConfig != null)
            return layoutConfig;

        if (cachedLayoutConfig == null)
            cachedLayoutConfig = Resources.Load<MaterialListPanelLayoutConfig>(LayoutConfigResourcePath);
        return cachedLayoutConfig;
    }

    private void BuildSelectionCandidates()
    {
        selectionCandidates.Clear();
        PlayerState state = owner.PlayerState;
        AddUniqueSelectionCandidates(state.Deck);
        AddUniqueSelectionCandidates(state.Hand);
        AddUniqueSelectionCandidates(state.PlayZone);
        AddUniqueSelectionCandidates(state.DrawPile);
        AddUniqueSelectionCandidates(state.DiscardPile);
        AddUniqueSelectionCandidates(state.ConsumedPile);
    }

    private void AddUniqueSelectionCandidates(IReadOnlyList<MaterialModel> materials)
    {
        for (int i = 0; materials != null && i < materials.Count; i++)
        {
            MaterialModel material = materials[i];
            if (material != null && !selectionCandidates.Contains(material))
                selectionCandidates.Add(material);
        }
    }

    private void HandleMaterialClicked(MaterialModel materialModel)
    {
        if (materialModel == null || owner == null)
            return;

        owner.GetUIManager().PinUnifiedDetailPopup(this, UnifiedDetailContentBuilder.Build(materialModel));
        if (selectionCompleted == null)
            return;

        if (selectionPredicate != null && !selectionPredicate(materialModel))
            return;

        if (selectedMaterials.Contains(materialModel))
        {
            selectedMaterials.Remove(materialModel);
            RefreshSelectionVisuals();
            UpdateSelectionConfirmButtonState();
            return;
        }

        if (selectedMaterials.Count >= selectionCount)
            selectedMaterials.RemoveAt(0);

        selectedMaterials.Add(materialModel);
        RefreshSelectionVisuals();
        UpdateSelectionConfirmButtonState();
    }

    private void ShowRowMaterialTooltip(RectTransform anchor, MaterialModel materialModel)
    {
        ShowModifierTooltip(anchor, materialModel);
    }

    private void HideRowMaterialTooltip()
    {
        HideModifierTooltip((RectTransform)null);
    }

    private void RefreshSelectionVisuals()
    {
        for (int i = 0; i < activeRows.Count; i++)
        {
            if (activeRows[i] != null)
                activeRows[i].RefreshSelectionVisuals(selectedMaterials);
        }
    }

    private void UpdateSelectionConfirmButtonState()
    {
        bool enabled = selectionCompleted != null && selectedMaterials.Count >= selectionCount;
        if (selectionConfirmButtonComponent != null)
            selectionConfirmButtonComponent.interactable = enabled;
        if (selectionConfirmCanvasGroup != null)
        {
            selectionConfirmCanvasGroup.alpha = enabled ? 1f : selectionConfirmDisabledAlpha;
            selectionConfirmCanvasGroup.blocksRaycasts = enabled;
            selectionConfirmCanvasGroup.interactable = enabled;
        }
        if (selectionConfirmButtonText != null)
            selectionConfirmButtonText.text = selectionConfirmText;
    }

    private void HideSelectionConfirmButton()
    {
        if (selectionConfirmButton != null)
            selectionConfirmButton.gameObject.SetActive(false);
    }

    private void EnsureSelectionConfirmButton()
    {
        if (selectionCompleted == null)
            return;

        if (selectionConfirmButton == null)
            selectionConfirmButton = UIManager.FindChildRect(rowContainer, "SelectionConfirmButton");
        if (selectionConfirmCanvasGroup == null && selectionConfirmButton != null)
            selectionConfirmCanvasGroup = selectionConfirmButton.GetComponent<CanvasGroup>();
        if (selectionConfirmButtonComponent == null && selectionConfirmButton != null)
            selectionConfirmButtonComponent = selectionConfirmButton.GetComponent<Button>();
        if (selectionConfirmButtonText == null && selectionConfirmButton != null)
            selectionConfirmButtonText = UIManager.FindChildComponent<TMP_Text>(selectionConfirmButton, "Text");

        if (selectionConfirmButtonComponent != null)
        {
            selectionConfirmButtonComponent.onClick.RemoveAllListeners();
            selectionConfirmButtonComponent.onClick.AddListener(ConfirmSelection);
        }

        if (selectionConfirmButton != null)
            selectionConfirmButton.gameObject.SetActive(true);
    }

    public void ConfirmSelection()
    {
        if (selectionCompleted == null || selectedMaterials.Count < selectionCount)
            return;

        Action<IReadOnlyList<MaterialModel>> completed = selectionCompleted;
        List<MaterialModel> result = new List<MaterialModel>(selectedMaterials);
        ClearSelectionMode();
        gameObject.SetActive(false);
        completed(result);
    }

    private void OnDisable()
    {
        owner?.GetUIManager().HideUnifiedDetailPopup(null);
        if (!selectionLocked)
            ClearSelectionMode();
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
            {
                if (selectionCancelled != null)
                    CancelSelection();
                return;
            }

            ClearSelectionMode();
            gameObject.SetActive(false);
        });
    }

    private void CancelSelection()
    {
        Action cancelled = selectionCancelled;
        ClearSelectionMode();
        gameObject.SetActive(false);
        cancelled?.Invoke();
    }

    private void CacheReferences()
    {
        if (rowContainer == null)
            rowContainer = UIManager.FindChildRect(transform, "RowContainer");
        if (titleText == null)
            titleText = UIManager.FindChildComponent<TMP_Text>(transform, "Title");
    }

    private void EnsureRows()
    {
        if (rowContainer == null)
            return;

        HideLegacyMaterialRows();
        if (drawPileRow == null)
            drawPileRow = FindRow("DrawPileRow");
        if (discardPileRow == null)
            discardPileRow = FindRow("DiscardPileRow");
        if (consumedPileRow == null)
            consumedPileRow = FindRow("ConsumedPileRow");
        if (selectionRow == null)
            selectionRow = FindRow("SelectionRow");
    }

    private BattleMaterialRowUI FindRow(string rowName)
    {
        Transform row = rowContainer != null ? rowContainer.Find(rowName) : null;
        return row != null ? row.GetComponent<BattleMaterialRowUI>() : null;
    }

    private void HideLegacyMaterialRows()
    {
        SetLegacyRowActive(ref waterMaterialRow, "WaterRow", false);
        SetLegacyRowActive(ref fireMaterialRow, "FireRow", false);
        SetLegacyRowActive(ref windMaterialRow, "WindRow", false);
        SetLegacyRowActive(ref earthMaterialRow, "EarthRow", false);
        SetLegacyRowActive(ref wildMaterialRow, "WildRow", false);
    }

    private void SetLegacyRowActive(ref RectTransform row, string name, bool active)
    {
        if (row == null && rowContainer != null)
            row = UIManager.FindChildRect(rowContainer, name);
        if (row != null)
            row.gameObject.SetActive(active);
    }

    private void HideRows()
    {
        activeRows.Clear();
        SetRowActive(drawPileRow, false);
        SetRowActive(discardPileRow, false);
        SetRowActive(consumedPileRow, false);
        SetRowActive(selectionRow, false);
    }

    private void SetRowActive(BattleMaterialRowUI row, bool active)
    {
        if (row != null)
            row.gameObject.SetActive(active);
    }

    private void RefreshTitle()
    {
        if (titleText == null)
            return;

        if (selectionCompleted != null)
        {
            titleText.text = !string.IsNullOrEmpty(selectionTitleOverride)
                ? selectionTitleOverride
                : LocalizationSystem.GetText("ui.material_list.select_material", "选择素材");
            return;
        }

        titleText.text = LocalizationSystem.GetText("ui.material_list.arrow_piles", "箭头牌堆");
    }

    private void EnsurePanelBlocksRaycasts()
    {
        Graphic graphic = GetComponent<Graphic>();
        if (graphic != null)
            graphic.raycastTarget = true;
        CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
        }
        transform.SetAsLastSibling();
    }

    private void ClearSelectionMode()
    {
        selectionPredicate = null;
        selectionCompleted = null;
        selectionCancelled = null;
        selectionTitleOverride = null;
        selectionLocked = false;
        selectionCount = 0;
        HideSelectionConfirmButton();
        RefreshSelectionVisuals();
    }
}
