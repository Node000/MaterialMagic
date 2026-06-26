using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class RunHistoryArrowRowUI : MonoBehaviour
{
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private RectTransform materialCardPrefab;
    [SerializeField] private Vector2 cardSize = new Vector2(82f, 118f);
    [SerializeField] private float fixedContentWidth;
    [SerializeField] private float hoverSpread = 40f;
    [SerializeField] private float hoverYOffset = 34f;
    [SerializeField] private float hoverScale = 1.22f;
    [SerializeField] private float normalScale = 0.78f;
    [SerializeField] private float animationDuration = 0.16f;
    [SerializeField] private Ease animationEase = Ease.OutCubic;

    private readonly List<RectTransform> itemRects = new List<RectTransform>();
    private int hoverIndex = -1;

    public event Action<MaterialModel> ArrowHovered;
    public event Action ArrowUnhovered;

    public void Refresh(MaterialCardSaveData[] cards)
    {
        CacheReferences();
        ClearItems();
        hoverIndex = -1;
        if (contentRoot == null || materialCardPrefab == null || cards == null)
            return;

        for (int i = 0; i < cards.Length; i++)
        {
            MaterialModel material = RunSaveSystem.CreateMaterialCard(cards[i]);
            RectTransform item = Instantiate(materialCardPrefab, contentRoot);
            item.gameObject.SetActive(true);
            item.name = "Arrow_" + (i + 1);
            item.anchorMin = new Vector2(0f, 0.5f);
            item.anchorMax = new Vector2(0f, 0.5f);
            item.pivot = new Vector2(0.5f, 0.5f);
            item.sizeDelta = cardSize;
            item.anchoredPosition = Vector2.zero;
            item.localScale = Vector3.one * normalScale;

            Graphic raycastGraphic = item.GetComponent<Graphic>();
            if (raycastGraphic != null)
                raycastGraphic.raycastTarget = true;

            MaterialCardView view = item.GetComponent<MaterialCardView>();
            if (view != null)
            {
                view.Bind(material);
                view.enabled = false;
            }
            DisableSpringHighlights(item);
            JuicyMotion motion = item.GetComponent<JuicyMotion>();
            if (motion != null)
                motion.enabled = false;

            RunHistoryArrowItemUI itemUI = item.GetComponent<RunHistoryArrowItemUI>();
            if (itemUI == null)
                itemUI = item.gameObject.AddComponent<RunHistoryArrowItemUI>();
            itemUI.Initialize(this, i, material);
            itemRects.Add(item);
        }

        if (contentRoot != null)
            contentRoot.sizeDelta = new Vector2(GetLayoutWidth(), contentRoot.sizeDelta.y);
        ApplyLayout(true);
    }

    public void SetHover(int index, MaterialModel material)
    {
        hoverIndex = index;
        ApplyLayout(false);
        ArrowHovered?.Invoke(material);
    }

    public void ClearHover(int index)
    {
        if (hoverIndex != index)
            return;

        hoverIndex = -1;
        ApplyLayout(false);
        ArrowUnhovered?.Invoke();
    }

    private void CacheReferences()
    {
        if (contentRoot == null)
            contentRoot = transform as RectTransform;
    }

    private void ClearItems()
    {
        if (contentRoot == null)
            return;

        for (int i = contentRoot.childCount - 1; i >= 0; i--)
            Destroy(contentRoot.GetChild(i).gameObject);
        itemRects.Clear();
    }

    private void ApplyLayout(bool instant)
    {
        if (itemRects.Count == 0)
            return;

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

    private static void DisableSpringHighlights(RectTransform item)
    {
        SpringLineHighlightUI[] highlights = item.GetComponentsInChildren<SpringLineHighlightUI>(true);
        for (int i = 0; i < highlights.Length; i++)
        {
            highlights[i].gameObject.SetActive(false);
            highlights[i].enabled = false;
        }
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
