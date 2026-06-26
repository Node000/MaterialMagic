using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BattleMaterialRowUI : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text emptyText;
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private RectTransform materialCardPrefab;
    [SerializeField] private Vector2 cardSize = new Vector2(82f, 118f);
    [SerializeField] private float fixedContentWidth;
    [SerializeField] private float hoverSpread = 42f;
    [SerializeField] private float hoverYOffset = 32f;
    [SerializeField] private float hoverScale = 1.18f;
    [SerializeField] private float normalScale = 0.72f;
    [SerializeField] private float inactiveAlpha = 0.45f;
    [SerializeField] private float animationDuration = 0.16f;
    [SerializeField] private Ease animationEase = Ease.OutCubic;

    private readonly List<RectTransform> itemRects = new List<RectTransform>();
    private readonly List<MaterialCardView> itemViews = new List<MaterialCardView>();
    private readonly List<MaterialModel> itemMaterials = new List<MaterialModel>();
    private readonly List<bool> itemSelectable = new List<bool>();
    private IReadOnlyList<MaterialModel> selectedMaterials;
    private int hoverIndex = -1;

    public event Action<RectTransform, MaterialModel> MaterialHovered;
    public event Action<MaterialModel> MaterialClicked;
    public event Action MaterialUnhovered;

    public void Configure(RectTransform materialCardPrefab, Vector2 cardSize, float fixedContentWidth)
    {
        this.materialCardPrefab = materialCardPrefab;
        this.cardSize = cardSize;
        this.fixedContentWidth = fixedContentWidth;
    }

    public void Refresh(string title, IReadOnlyList<MaterialModel> materials, Predicate<MaterialModel> selectablePredicate, IReadOnlyList<MaterialModel> selectedMaterials, bool hideUnselectable)
    {
        CacheReferences();
        ClearItems();
        hoverIndex = -1;
        this.selectedMaterials = selectedMaterials;
        if (titleText != null)
            titleText.text = title;

        if (contentRoot == null || materialCardPrefab == null)
        {
            SetEmptyActive(true);
            return;
        }

        int itemCount = 0;
        for (int i = 0; materials != null && i < materials.Count; i++)
        {
            MaterialModel material = materials[i];
            if (material == null)
                continue;

            bool selectable = selectablePredicate == null || selectablePredicate(material);
            if (hideUnselectable && !selectable)
                continue;

            CreateItem(material, selectable, itemCount);
            itemCount++;
        }

        SetEmptyActive(itemCount == 0);
        contentRoot.sizeDelta = new Vector2(GetLayoutWidth(), contentRoot.sizeDelta.y);
        ApplyLayout(true);
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
        }

        CanvasGroup canvasGroup = item.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = item.gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = selectable ? 1f : inactiveAlpha;
        canvasGroup.blocksRaycasts = true;

        Graphic[] graphics = item.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
            graphics[i].raycastTarget = false;
        Graphic raycastGraphic = item.GetComponent<Graphic>();
        if (raycastGraphic != null)
            raycastGraphic.raycastTarget = true;

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

            float x = GetBaseX(i);
            float y = 0f;
            float scale = normalScale;
            if (hoverIndex >= 0)
            {
                if (i < hoverIndex)
                    x -= hoverSpread;
                else if (i > hoverIndex)
                    x += hoverSpread;
                else
                {
                    y = hoverYOffset;
                    scale = hoverScale;
                }
            }

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

    private void SetEmptyActive(bool active)
    {
        if (emptyText != null)
            emptyText.gameObject.SetActive(active);
    }

    private float GetBaseX(int index)
    {
        int count = itemRects.Count;
        if (count <= 1)
            return GetLayoutWidth() * 0.5f;

        float sidePadding = cardSize.x * hoverScale * 0.5f + hoverSpread;
        return sidePadding + index * GetBaseSpacing(count, sidePadding);
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
        for (int i = 0; i < itemRects.Count; i++)
            itemRects[i]?.DOKill(false);
    }
}
