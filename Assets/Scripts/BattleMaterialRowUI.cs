using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public struct BattleMaterialRowSortEntry
{
    public MaterialModel Material;
    public bool Selectable;
    public int OriginalIndex;
}

public class BattleMaterialRowUI : MonoBehaviour, IPointerUpHandler
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text emptyText;
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private RectTransform materialCardPrefab;
    [SerializeField] private Vector2 cardSize = new Vector2(82f, 118f);
    [SerializeField] private float fixedContentWidth;
    [SerializeField] private float hoverSpread = 42f;
    [SerializeField] private float hoverYOffset = 32f;
    [SerializeField] private float hoverCurvePower = 1.35f;
    [SerializeField] private float hoverScale = 1.18f;
    [SerializeField] private float normalScale = 0.72f;
    [SerializeField] private bool hoverSelectionOutlineEnabled = true;
    [SerializeField] private float inactiveAlpha = 0.45f;
    [SerializeField] private float animationDuration = 0.16f;
    [SerializeField] private Ease animationEase = Ease.OutCubic;

    private readonly List<RectTransform> itemRects = new List<RectTransform>();
    private readonly List<MaterialCardView> itemViews = new List<MaterialCardView>();
    private readonly List<MaterialModel> itemMaterials = new List<MaterialModel>();
    private readonly List<bool> itemSelectable = new List<bool>();
    private MaterialListPanelUI ownerPanel;
    private IReadOnlyList<MaterialModel> selectedMaterials;
    private int hoverIndex = -1;
    private bool touchReleaseConfirmActive;

    private struct SortedMaterialEntry
    {
        public MaterialModel Material;
        public bool Selectable;
        public int OriginalIndex;
    }

    public event Action<RectTransform, MaterialModel> MaterialHovered;
    public event Action<MaterialModel> MaterialClicked;
    public event Action MaterialUnhovered;

    public void Configure(RectTransform materialCardPrefab, Vector2 cardSize, float fixedContentWidth)
    {
        this.materialCardPrefab = materialCardPrefab;
        this.cardSize = cardSize;
        this.fixedContentWidth = fixedContentWidth;
    }

    public void ConfigureArrowRowLayout(float totalLength, float defaultScale, float hoverScale)
    {
        ConfigureArrowRowLayout(totalLength, defaultScale, hoverScale, 32f, 1.35f);
    }

    public void ConfigureArrowRowLayout(float totalLength, float defaultScale, float hoverScale, float hoverYOffset, float hoverCurvePower)
    {
        fixedContentWidth = Mathf.Max(0f, totalLength);
        normalScale = Mathf.Max(0.01f, defaultScale);
        this.hoverScale = Mathf.Max(0.01f, hoverScale);
        this.hoverYOffset = Mathf.Max(0f, hoverYOffset);
        this.hoverCurvePower = Mathf.Max(0.01f, hoverCurvePower);
    }

    public void SetHoverSelectionOutlineEnabled(bool enabled)
    {
        hoverSelectionOutlineEnabled = enabled;
    }

    public void Refresh(string title, IReadOnlyList<MaterialModel> materials, Predicate<MaterialModel> selectablePredicate, IReadOnlyList<MaterialModel> selectedMaterials, bool hideUnselectable, string emptyTextKey, string emptyTextFallback)
    {
        CacheReferences();
        ClearItems();
        hoverIndex = -1;
        touchReleaseConfirmActive = false;
        this.selectedMaterials = selectedMaterials;
        if (titleText != null)
            titleText.text = title;
        if (emptyText != null)
            emptyText.text = LocalizationSystem.GetText(emptyTextKey, emptyTextFallback);

        if (contentRoot == null || materialCardPrefab == null)
        {
            SetEmptyActive(true);
            return;
        }

        int itemCount = 0;
        var orderedEntries = new List<BattleMaterialRowSortEntry>();
        for (int i = 0; materials != null && i < materials.Count; i++)
        {
            MaterialModel material = materials[i];
            if (material == null)
                continue;

            bool selectable = selectablePredicate == null || selectablePredicate(material);
            if (hideUnselectable && !selectable)
                continue;

            orderedEntries.Add(new BattleMaterialRowSortEntry
            {
                Material = material,
                Selectable = selectable,
                OriginalIndex = i
            });
        }

        MaterialArrowSortUtility.SortMaterialsByBaseDirection(orderedEntries, entry => entry.Material != null ? entry.Material.material : MaterialEnum.None, entry => entry.OriginalIndex);
        for (int i = 0; i < orderedEntries.Count; i++)
            CreateItem(orderedEntries[i].Material, orderedEntries[i].Selectable, itemCount++);

        SetEmptyActive(itemCount == 0);
        contentRoot.sizeDelta = new Vector2(GetLayoutWidth(), contentRoot.sizeDelta.y);
        ApplyLayout(true);
    }

    public void SetOwnerPanel(MaterialListPanelUI panel)
    {
        ownerPanel = panel;
    }

    public bool ShouldUseMobileInteraction()
    {
        return ownerPanel != null && ownerPanel.ShouldUseMobileInteraction();
    }

    public void BeginTouchReleaseConfirm(int index)
    {
        if (!ShouldUseMobileInteraction() || index < 0 || index >= itemMaterials.Count || !itemSelectable[index])
            return;

        touchReleaseConfirmActive = true;
        SetHover(index);
    }

    public void CompleteTouchReleaseConfirm()
    {
        if (!touchReleaseConfirmActive)
            return;

        touchReleaseConfirmActive = false;
        if (!ShouldUseMobileInteraction() || hoverIndex < 0)
            return;

        HandleItemClicked(hoverIndex);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        CompleteTouchReleaseConfirm();
    }

    public void SetHover(int index)
    {
        if (index < 0 || index >= itemMaterials.Count)
            return;

        hoverIndex = index;
        ApplyLayout(false);
        MaterialHovered?.Invoke(itemRects[index], itemMaterials[index]);
    }

    public void ClearHover(int index)
    {
        if (hoverIndex != index)
            return;

        hoverIndex = -1;
        ApplyLayout(false);
        MaterialUnhovered?.Invoke();
    }

    public void HandleItemClicked(int index)
    {
        if (index < 0 || index >= itemMaterials.Count || !itemSelectable[index])
            return;

        MaterialClicked?.Invoke(itemMaterials[index]);
    }

    public void RefreshSelectionVisuals(IReadOnlyList<MaterialModel> selectedMaterials)
    {
        this.selectedMaterials = selectedMaterials;
        for (int i = 0; i < itemViews.Count; i++)
        {
            if (itemViews[i] != null)
                itemViews[i].SetSelectionVisual(IsSelected(itemMaterials[i]), false);
        }

        ApplyLayout(false);
    }

    private void CacheReferences()
    {
        if (contentRoot == null)
            contentRoot = UIManager.FindChildRect(transform, "Content");
        if (titleText == null)
            titleText = UIManager.FindChildComponent<TMP_Text>(transform, "Title");
        if (emptyText == null)
            emptyText = UIManager.FindChildComponent<TMP_Text>(transform, "EmptyText");
        if (materialCardPrefab == null)
        {
            PrefabReferenceLibrary library = GetComponentInParent<PrefabReferenceLibrary>();
            if (library != null)
                materialCardPrefab = library.MaterialCardPrefab;
        }
    }

    private void CreateItem(MaterialModel material, bool selectable, int index)
    {
        RectTransform item = Instantiate(materialCardPrefab, contentRoot);
        item.gameObject.SetActive(true);
        item.name = "Material_" + (index + 1);
        item.anchorMin = new Vector2(0f, 0.5f);
        item.anchorMax = new Vector2(0f, 0.5f);
        item.pivot = new Vector2(0.5f, 0.5f);
        item.sizeDelta = cardSize;
        item.anchoredPosition = Vector2.zero;
        item.localScale = Vector3.one * normalScale;

        MaterialCardView view = item.GetComponent<MaterialCardView>();
        if (view != null)
        {
            view.Bind(material, !selectable);
            view.SetSelectionVisual(IsSelected(material), true);
            view.SetSpringHighlightEnabled(hoverSelectionOutlineEnabled);
        }

        CanvasGroup canvasGroup = item.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = item.gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = selectable ? 1f : inactiveAlpha;
        canvasGroup.blocksRaycasts = true;

        Graphic[] graphics = item.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
            graphics[i].raycastTarget = false;
        if (view != null)
        {
            view.RefreshRaycastTargets();
        }
        else
        {
            Graphic raycastGraphic = item.GetComponent<Graphic>();
            if (raycastGraphic != null)
                raycastGraphic.raycastTarget = true;
        }

        SpringLineHighlightUI[] highlights = item.GetComponentsInChildren<SpringLineHighlightUI>(true);
        for (int i = 0; i < highlights.Length; i++)
        {
            highlights[i].gameObject.SetActive(false);
            highlights[i].enabled = false;
        }

        BattleMaterialRowItemUI itemUI = item.GetComponent<BattleMaterialRowItemUI>();
        if (itemUI == null)
            itemUI = item.gameObject.AddComponent<BattleMaterialRowItemUI>();
        itemUI.Initialize(this, index);

        JuicyMotion motion = item.GetComponent<JuicyMotion>();
        if (motion != null)
            motion.enabled = false;

        itemRects.Add(item);
        itemViews.Add(view);
        itemMaterials.Add(material);
        itemSelectable.Add(selectable);
    }

    private void ClearItems()
    {
        if (contentRoot != null)
        {
            for (int i = contentRoot.childCount - 1; i >= 0; i--)
                Destroy(contentRoot.GetChild(i).gameObject);
        }

        itemRects.Clear();
        itemViews.Clear();
        itemMaterials.Clear();
        itemSelectable.Clear();
    }

    private void ApplyLayout(bool instant)
    {
        for (int i = 0; i < itemRects.Count; i++)
        {
            RectTransform rect = itemRects[i];
            if (rect == null)
                continue;

            float x = GetLayoutX(i);
            float y = GetLiftedY(i);
            float scale = GetDisplayScale(i);

            rect.DOKill(false);
            if (instant)
            {
                rect.anchoredPosition = new Vector2(x, y);
                rect.localScale = Vector3.one * scale;
            }
            else
            {
                rect.DOAnchorPos(new Vector2(x, y), animationDuration).SetEase(animationEase);
                rect.DOScale(scale, animationDuration).SetEase(animationEase);
            }
        }
    }

    private bool IsSelected(MaterialModel material)
    {
        for (int i = 0; selectedMaterials != null && i < selectedMaterials.Count; i++)
        {
            if (selectedMaterials[i] == material)
                return true;
        }
        return false;
    }

    private float GetLiftedY(int index)
    {
        if (index < 0 || index >= itemMaterials.Count)
            return 0f;

        if (hoverIndex < 0)
            return IsSelected(itemMaterials[index]) ? hoverYOffset : 0f;

        int distance = Mathf.Abs(index - hoverIndex);
        if (distance == 0)
            return hoverYOffset;

        float strength = 1f / Mathf.Pow(distance + 1f, Mathf.Max(0.01f, hoverCurvePower));
        return hoverYOffset * strength;
    }

    private float GetDisplayScale(int index)
    {
        if (index < 0 || index >= itemMaterials.Count || hoverIndex < 0)
            return normalScale;

        int distance = Mathf.Abs(index - hoverIndex);
        if (distance == 0)
            return hoverScale;

        float scaleStrength = 1f / Mathf.Pow(distance + 1f, Mathf.Max(0.01f, hoverCurvePower));
        return Mathf.Lerp(normalScale, hoverScale, scaleStrength);
    }

    private void SetEmptyActive(bool active)
    {
        if (emptyText != null)
            emptyText.gameObject.SetActive(active);
    }

    private float GetLayoutX(int index)
    {
        int count = itemRects.Count;
        float sidePadding = cardSize.x * hoverScale * 0.5f + hoverSpread;
        float minX = sidePadding;
        float maxX = Mathf.Max(minX, GetLayoutWidth() - sidePadding);
        return HoverSpreadLayoutUtility.GetFixedWidthHoverX(index, count, hoverIndex, minX, maxX, GetBaseSpacing(count, sidePadding) + hoverSpread);
    }

    private float GetBaseX(int index)
    {
        int count = itemRects.Count;
        if (count <= 1)
            return GetLayoutWidth() * 0.5f;

        float sidePadding = cardSize.x * hoverScale * 0.5f + hoverSpread;
        return HoverSpreadLayoutUtility.GetBoundedBaseX(index, count, sidePadding, GetLayoutWidth() - sidePadding);
    }

    private float GetBaseSpacing(int count, float sidePadding)
    {
        if (count <= 1)
            return 0f;

        float availableWidth = GetLayoutWidth() - sidePadding * 2f;
        return Mathf.Max(0f, availableWidth / (count - 1));
    }

    private float GetLayoutWidth()
    {
        if (fixedContentWidth > 0f)
            return fixedContentWidth;

        RectTransform parent = contentRoot != null ? contentRoot.parent as RectTransform : null;
        if (parent != null && parent.rect.width > 0f)
            return parent.rect.width;

        return contentRoot != null ? contentRoot.rect.width : 0f;
    }

    private void OnDisable()
    {
        touchReleaseConfirmActive = false;
        for (int i = 0; i < itemRects.Count; i++)
            itemRects[i]?.DOKill(false);
    }
}

internal static class HoverSpreadLayoutUtility
{
    public static float GetCenteredBaseX(int index, int count, float spacing)
    {
        if (count <= 1)
            return 0f;

        return -spacing * (count - 1) * 0.5f + spacing * index;
    }

    public static float GetFixedWidthHoverX(int index, int count, int hoverIndex, float spacing, float hoverExtraSpacing)
    {
        if (count <= 1)
            return 0f;

        float startX = GetCenteredBaseX(0, count, spacing);
        float endX = GetCenteredBaseX(count - 1, count, spacing);
        return GetFixedWidthHoverX(index, count, hoverIndex, startX, endX, spacing + Mathf.Max(0f, hoverExtraSpacing));
    }

    public static float GetBoundedBaseX(int index, int count, float minX, float maxX)
    {
        if (count <= 1)
            return (minX + maxX) * 0.5f;

        return Mathf.Lerp(minX, maxX, index / (float)(count - 1));
    }

    public static float GetFixedWidthHoverX(int index, int count, int hoverIndex, float minX, float maxX, float hoverAdjacentSpacing)
    {
        if (count <= 1)
            return (minX + maxX) * 0.5f;

        if (hoverIndex < 0 || hoverIndex >= count || index == hoverIndex)
            return GetBoundedBaseX(index, count, minX, maxX);

        float hoverX = GetBoundedBaseX(hoverIndex, count, minX, maxX);
        float targetSpacing = Mathf.Max(0f, hoverAdjacentSpacing);
        if (index < hoverIndex)
            return GetLeftSideX(index, hoverIndex, minX, hoverX, targetSpacing);

        return GetRightSideX(index, count, hoverIndex, maxX, hoverX, targetSpacing);
    }

    private static float GetLeftSideX(int index, int hoverIndex, float minX, float hoverX, float targetSpacing)
    {
        int leftCount = hoverIndex;
        if (leftCount <= 1)
            return minX;

        float segmentEnd = Mathf.Clamp(hoverX - targetSpacing, minX, hoverX);
        return Mathf.Lerp(minX, segmentEnd, index / (float)(leftCount - 1));
    }

    private static float GetRightSideX(int index, int count, int hoverIndex, float maxX, float hoverX, float targetSpacing)
    {
        int rightCount = count - hoverIndex - 1;
        if (rightCount <= 1)
            return maxX;

        float segmentStart = Mathf.Clamp(hoverX + targetSpacing, hoverX, maxX);
        return Mathf.Lerp(segmentStart, maxX, (index - hoverIndex - 1) / (float)(rightCount - 1));
    }
}
