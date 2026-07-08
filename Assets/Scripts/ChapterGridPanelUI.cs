using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ChapterGridPanelUI : MonoBehaviour
{
    private class CellView
    {
        public RunMapCellModel Model;
        public RectTransform Rect;
        public MapGridSlotView SlotView;
        public Image Icon;
        public TMP_Text Label;
    }

    private class DirectionButtonHandler : MonoBehaviour, IPointerClickHandler
    {
        private ChapterGridPanelUI owner;
        private MaterialEnum material;

        public void Initialize(ChapterGridPanelUI owner, MaterialEnum material)
        {
            this.owner = owner;
            this.material = material;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left || eventData.button == PointerEventData.InputButton.Right)
                owner?.HandleDirectionClicked(material);
        }
    }

    private const float CellSpacing = 8f;
    private const float DefaultGridSide = 420f;
    private const string DirectionCardPrefix = "MapDirection_";

    [SerializeField] private RectTransform gridRoot;
    [SerializeField] private RectTransform playerMarker;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text actionPowerText;
    [SerializeField] private RectTransform directionRoot;
    [SerializeField] private RectTransform directionCardPrefab;
    [SerializeField] private RectTransform cellPrefab;
    [SerializeField] private float panelShowDuration = 0.24f;
    [SerializeField] private Ease panelShowEase = Ease.OutCubic;
    [SerializeField] private float panelHideDuration = 0.18f;
    [SerializeField] private Ease panelHideEase = Ease.InCubic;
    [SerializeField] private Vector2 panelSlideOffset = new Vector2(0f, 96f);
    [SerializeField] private float markerMoveDuration = 0.32f;
    [SerializeField] private Ease markerMoveEase = Ease.InOutSine;
    [SerializeField] private float enterLevelDelayAfterMove = 0.2f;
    [SerializeField] private float bossWaveInterval = 0.045f;
    [SerializeField] private float bossIconShrinkDuration = 0.12f;
    [SerializeField] private Ease bossIconShrinkEase = Ease.InBack;
    [SerializeField] private float bossIconGrowDuration = 0.22f;
    [SerializeField] private Ease bossIconGrowEase = Ease.OutBack;
    [SerializeField] private float directionCardSize = 92f;
    [SerializeField] private float directionCardSpacing = 118f;
    [SerializeField] private float directionCardIconSize = 68f;
    [SerializeField] private float directionCardY = 0f;
    [SerializeField] private float directionCardLocalZ = -231.7424f;
    [SerializeField] private float gridScale = 1f;
    [SerializeField] private float verticalScrollFollowDuration = 0.24f;
    [SerializeField] private Ease verticalScrollFollowEase = Ease.OutQuad;
    [SerializeField] private Color cellColor = new Color(0.08f, 0.1f, 0.16f, 0.92f);
    [SerializeField] private Color emptyCellColor = new Color(0.05f, 0.06f, 0.09f, 0.62f);
    [SerializeField] private Color unavailableCellColor = new Color(0.02f, 0.025f, 0.035f, 0.32f);
    [SerializeField] private Color levelIconColor = Color.white;
    [SerializeField] private Color bossIconColor = Color.white;
    [SerializeField] private Color labelColor = Color.white;
    [SerializeField] private Color playerMarkerColor = Color.white;
    [SerializeField] private Color fallbackDirectionCardColor = new Color(0.08f, 0.1f, 0.16f, 0.92f);

    private readonly List<CellView> cells = new List<CellView>();
    private readonly Dictionary<MaterialEnum, RectTransform> directionButtons = new Dictionary<MaterialEnum, RectTransform>();
    private HandSystemUI owner;
    private RunMapGridModel currentGrid;
    private RectTransform rectTransform;
    private RectTransform contentRoot;
    private RectTransform scrollContent;
    private ScrollRect scrollRect;
    private Tween scrollTween;
    private Tween panelTween;
    private Tween markerTween;
    private Sequence bossSequence;
    private Vector2 shownAnchoredPosition;
    private bool shownPositionCached;
    private bool inputLocked;
    private int ignoreOutsideClickFrame = -1;
    private readonly List<RaycastResult> pointerRaycastResults = new List<RaycastResult>();

    public float EnterLevelDelayAfterMove => enterLevelDelayAfterMove;

    public void Initialize(HandSystemUI owner)
    {
        this.owner = owner;
        CacheReferences();
        gameObject.SetActive(false);
    }

    public void SetDirectionRoot(RectTransform root)
    {
        if (root != null)
            directionRoot = root;
    }

    public void SetDirectionCardSource(RectTransform root, RectTransform prefab, float spacing)
    {
        if (root != null)
            directionRoot = root;
        directionCardPrefab = prefab;
        if (spacing > 0f)
            directionCardSpacing = spacing;
    }

    public void Toggle(RunMapGridModel grid)
    {
        if (gameObject.activeSelf)
            Hide();
        else
            Show(grid, false);
    }

    public void Show(RunMapGridModel grid)
    {
        Show(grid, true);
    }

    public void Show(RunMapGridModel grid, bool showDirectionCards)
    {
        CacheReferences();
        ResetScrollMotion();
        currentGrid = grid;
        inputLocked = false;
        ignoreOutsideClickFrame = Time.frameCount;
        gameObject.SetActive(true);
        BuildGrid();
        RefreshTexts();
        if (showDirectionCards)
            RefreshDirectionButtons();
        else
            ClearDirectionButtons();
        MoveMarkerToCurrentCell(false);
        PlayPanelShow();
    }

    public void Hide()
    {
        HideAnimated();
    }

    public Tween HideAnimated()
    {
        inputLocked = true;
        ResetScrollMotion();
        scrollTween?.Kill(false);
        markerTween?.Kill(false);
        bossSequence?.Kill(false);
        ClearDirectionButtons();
        owner?.GetUIManager()?.UnifiedDetailPopup?.HideImmediate();
        if (!gameObject.activeSelf)
            return null;

        panelTween?.Kill(false);
        CanvasGroup canvasGroup = GetOrAddCanvasGroup();
        rectTransform.anchoredPosition = shownAnchoredPosition;
        panelTween = DOTween.Sequence().SetTarget(this)
            .Join(canvasGroup.DOFade(0f, panelHideDuration).SetEase(panelHideEase))
            .Join(rectTransform.DOAnchorPos(shownAnchoredPosition + panelSlideOffset, panelHideDuration).SetEase(panelHideEase))
            .OnComplete(() =>
            {
                gameObject.SetActive(false);
                canvasGroup.alpha = 1f;
                rectTransform.anchoredPosition = shownAnchoredPosition;
            });
        return panelTween;
    }

    public void SetInputLocked(bool locked)
    {
        inputLocked = locked;
        if (!gameObject.activeInHierarchy || !IsMapSelectionActive())
        {
            ClearDirectionButtons();
            return;
        }

        RefreshDirectionButtons();
    }

    public void RefreshCellVisuals()
    {
        for (int i = 0; i < cells.Count; i++)
            ApplyCellVisual(cells[i]);
    }

    public Tween MoveMarkerToCurrentCell(bool animated)
    {
        if (playerMarker == null || currentGrid == null)
            return null;

        Vector2 position = GetMarkerPosition(currentGrid.playerX, currentGrid.playerY);
        markerTween?.Kill(false);
        if (!animated)
        {
            playerMarker.anchoredPosition = position;
            UpdateScrollToMarker(true);
            return null;
        }

        markerTween = playerMarker.DOAnchorPos(position, markerMoveDuration).SetEase(markerMoveEase).SetTarget(this);
        markerTween.OnUpdate(() => UpdateScrollToMarker(false));
        markerTween.OnComplete(() => UpdateScrollToMarker(false));
        return markerTween;
    }

    public Tween PlayBossTransform(Action onComplete)
    {
        if (currentGrid == null)
        {
            onComplete?.Invoke();
            return null;
        }

        inputLocked = true;
        RefreshDirectionButtons();
        bossSequence?.Kill(false);
        bossSequence = DOTween.Sequence().SetTarget(this);
        for (int y = currentGrid.height - 1; y >= 0; y--)
        {
            for (int x = 0; x < currentGrid.width; x++)
            {
                CellView cell = FindCell(x, y);
                if (cell == null || cell.Icon == null || cell.Model == null || !currentGrid.IsCellReachable(x, y))
                    continue;

                float delay = ((currentGrid.height - 1 - y) + x) * bossWaveInterval;
                Image icon = cell.Icon;
                TMP_Text label = cell.Label;
                bossSequence.Insert(delay, icon.rectTransform.DOScale(Vector3.zero, bossIconShrinkDuration).SetEase(bossIconShrinkEase).OnComplete(() =>
                {
                    icon.gameObject.SetActive(true);
                    icon.sprite = Resources.Load<Sprite>("Images/UI/Boss");
                    icon.color = bossIconColor;
                    if (label != null)
                        label.text = "Boss";
                }));
                bossSequence.Insert(delay + bossIconShrinkDuration, icon.rectTransform.DOScale(Vector3.one, bossIconGrowDuration).SetEase(bossIconGrowEase));
            }
        }
        bossSequence.OnComplete(() =>
        {
            inputLocked = false;
            RefreshDirectionButtons();
            onComplete?.Invoke();
        });
        return bossSequence;
    }

    public void RefreshTexts()
    {
        if (titleText != null)
            titleText.text = "地图";
        if (actionPowerText != null)
            actionPowerText.gameObject.SetActive(false);
    }

    public void HandleDirectionClicked(MaterialEnum material)
    {
        if (inputLocked)
            return;

        owner?.OnChapterMapDirectionClicked(material);
    }

    private void LateUpdate()
    {
        if (!gameObject.activeSelf || inputLocked || Time.frameCount == ignoreOutsideClickFrame)
            return;

        if (IsMapSelectionActive())
            return;

        if (!TryGetPointerDownPosition(out Vector2 pointerPosition))
            return;

        if (IsPointerInsidePanel(pointerPosition) || IsPointerOnDirectionButton(pointerPosition))
            return;

        Hide();
    }

    private bool TryGetPointerDownPosition(out Vector2 pointerPosition)
    {
        if (Input.GetMouseButtonDown(0))
        {
            pointerPosition = Input.mousePosition;
            return true;
        }

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.phase == TouchPhase.Began)
            {
                pointerPosition = touch.position;
                return true;
            }
        }

        pointerPosition = default;
        return false;
    }

    private bool IsPointerInsidePanel(Vector2 pointerPosition)
    {
        CacheReferences();
        Canvas canvas = GetComponentInParent<Canvas>();
        Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        return rectTransform != null && RectTransformUtility.RectangleContainsScreenPoint(rectTransform, pointerPosition, eventCamera);
    }

    private bool IsPointerOnDirectionButton(Vector2 pointerPosition)
    {
        if (EventSystem.current == null || directionButtons.Count == 0)
            return false;

        PointerEventData eventData = new PointerEventData(EventSystem.current)
        {
            position = pointerPosition
        };
        pointerRaycastResults.Clear();
        EventSystem.current.RaycastAll(eventData, pointerRaycastResults);
        for (int i = 0; i < pointerRaycastResults.Count; i++)
        {
            Transform hit = pointerRaycastResults[i].gameObject.transform;
            foreach (RectTransform button in directionButtons.Values)
            {
                if (button != null && (hit == button || hit.IsChildOf(button)))
                    return true;
            }
        }

        return false;
    }

    private bool IsMapSelectionActive()
    {
        RunManager currentRun = RunManager.Current;
        return currentRun != null && currentRun.State == RunFlowState.MapSelection;
    }

    private void CacheReferences()
    {
        if (rectTransform == null)
            rectTransform = (RectTransform)transform;
        if (!shownPositionCached)
        {
            shownAnchoredPosition = rectTransform.anchoredPosition;
            shownPositionCached = true;
        }

        ScrollRect existingScrollRect = GetComponent<ScrollRect>();
        scrollRect = existingScrollRect;
        RectTransform viewport = null;
        Transform oldViewport = transform.Find("Viewport");
        if (oldViewport != null)
        {
            viewport = oldViewport as RectTransform;
            oldViewport.gameObject.SetActive(true);
        }

        if (viewport != null)
        {
            Transform existingContent = viewport.Find("Content");
            scrollContent = existingContent as RectTransform;
            if (scrollContent == null)
            {
                scrollContent = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
                scrollContent.SetParent(viewport, false);
            }
        }

        if (scrollRect != null)
        {
            scrollRect.enabled = true;
            scrollRect.viewport = viewport;
            scrollRect.content = scrollContent;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
            scrollRect.inertia = true;
            EnsureScrollDragArea(viewport);
        }

        if (contentRoot == null)
        {
            Transform existing = transform.Find("ChapterGridContent");
            contentRoot = existing as RectTransform;
            if (contentRoot == null && scrollContent != null)
            {
                existing = scrollContent.Find("ChapterGridContent");
                contentRoot = existing as RectTransform;
            }
        }
        if (contentRoot == null)
        {
            contentRoot = new GameObject("ChapterGridContent", typeof(RectTransform)).GetComponent<RectTransform>();
            contentRoot.SetParent(scrollContent != null ? scrollContent : transform, false);
        }
        else if (scrollContent != null && contentRoot.parent != scrollContent)
        {
            contentRoot.SetParent(scrollContent, false);
        }
        contentRoot.anchorMin = new Vector2(0.5f, 1f);
        contentRoot.anchorMax = new Vector2(0.5f, 1f);
        contentRoot.pivot = new Vector2(0.5f, 1f);
        contentRoot.anchoredPosition = Vector2.zero;
        if (!gameObject.activeSelf)
            contentRoot.sizeDelta = scrollRect != null && scrollRect.viewport != null ? scrollRect.viewport.rect.size : rectTransform.rect.size;
        contentRoot.SetAsLastSibling();
        ConfigureScrollContent();

        if (titleText == null)
            titleText = FindOrCreateText("Title", new Vector2(0f, -34f), new Vector2(320f, 48f), 28, TextAlignmentOptions.Center);
        if (actionPowerText == null)
        {
            Transform existingActionPower = contentRoot != null ? contentRoot.Find("ActionPowerText") : transform.Find("ActionPowerText");
            actionPowerText = existingActionPower != null ? existingActionPower.GetComponent<TMP_Text>() : null;
        }
        if (actionPowerText != null)
            actionPowerText.gameObject.SetActive(false);
        if (gridRoot == null)
            gridRoot = FindOrCreateRect("GridRoot", new Vector2(0f, -278f), new Vector2(430f, 430f));
        EnsurePlayerMarkerRoot();
    }

    private void EnsureScrollDragArea(RectTransform viewport)
    {
        if (viewport == null)
            return;

        Image image = viewport.GetComponent<Image>();
        if (image == null)
            image = viewport.gameObject.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = true;
    }

    private void ConfigureScrollContent()
    {
        if (scrollContent == null)
            return;

        Vector2 viewportSize = scrollRect != null && scrollRect.viewport != null ? scrollRect.viewport.rect.size : rectTransform.rect.size;
        scrollContent.anchorMin = new Vector2(0f, 1f);
        scrollContent.anchorMax = new Vector2(1f, 1f);
        scrollContent.pivot = new Vector2(0.5f, 1f);
        if (!gameObject.activeSelf)
        {
            scrollContent.anchoredPosition = Vector2.zero;
            scrollContent.sizeDelta = new Vector2(0f, viewportSize.y);
        }
    }

    private void EnsurePlayerMarkerRoot()
    {
        if (playerMarker == null)
        {
            playerMarker = FindOrCreatePlayerMarker();
            return;
        }

        playerMarker.SetParent(contentRoot, false);
        playerMarker.anchorMin = new Vector2(0.5f, 1f);
        playerMarker.anchorMax = new Vector2(0.5f, 1f);
        playerMarker.pivot = new Vector2(0.5f, 0.5f);
        playerMarker.localScale = Vector3.one;
        playerMarker.localRotation = Quaternion.identity;
        playerMarker.gameObject.SetActive(true);

        Image image = playerMarker.GetComponent<Image>();
        if (image == null)
            image = playerMarker.gameObject.AddComponent<Image>();
        image.sprite = Resources.Load<Sprite>("Images/UI/PlayerSmall");
        image.color = playerMarkerColor;
        image.preserveAspect = true;
        image.raycastTarget = false;
        playerMarker.SetAsLastSibling();
    }

    private RectTransform FindOrCreateRect(string objectName, Vector2 anchoredPosition, Vector2 size)
    {
        Transform existing = contentRoot.Find(objectName);
        RectTransform rect = existing as RectTransform;
        if (rect == null)
        {
            rect = new GameObject(objectName, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(contentRoot, false);
        }
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        return rect;
    }

    private TMP_Text FindOrCreateText(string objectName, Vector2 anchoredPosition, Vector2 size, int fontSize, TextAlignmentOptions alignment)
    {
        RectTransform rect = FindOrCreateRect(objectName, anchoredPosition, size);
        TMP_Text text = rect.GetComponent<TMP_Text>();
        if (text == null)
            text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = UIManager.GetDefaultTMPFont();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private RectTransform FindOrCreatePlayerMarker()
    {
        RectTransform marker = FindOrCreateRect("PlayerMarker", Vector2.zero, new Vector2(42f, 42f));
        marker.SetParent(contentRoot, false);
        Image image = marker.GetComponent<Image>();
        if (image == null)
            image = marker.gameObject.AddComponent<Image>();
        image.sprite = Resources.Load<Sprite>("Images/UI/PlayerSmall");
        image.color = playerMarkerColor;
        image.preserveAspect = true;
        image.raycastTarget = false;
        marker.SetAsLastSibling();
        return marker;
    }

    private void BuildGrid()
    {
        ClearGrid();
        if (currentGrid == null || gridRoot == null)
            return;

        float viewportHeight = scrollRect != null && scrollRect.viewport != null ? scrollRect.viewport.rect.height : rectTransform.rect.height;
        Vector2 cellSize = GetCellSize();
        float stepX = cellSize.x + CellSpacing;
        float stepY = cellSize.y + CellSpacing;
        float gridWidth = stepX * (currentGrid.width - 1) + cellSize.x;
        float gridHeight = stepY * (currentGrid.height - 1) + cellSize.y;
        gridRoot.anchorMin = new Vector2(0.5f, 1f);
        gridRoot.anchorMax = new Vector2(0.5f, 1f);
        gridRoot.pivot = new Vector2(0.5f, 0.5f);
        gridRoot.sizeDelta = new Vector2(gridWidth, gridHeight);
        gridRoot.anchoredPosition = new Vector2(0f, -80f - gridHeight * 0.5f + cellSize.y * 0.5f);
        float firstX = -gridWidth * 0.5f + cellSize.x * 0.5f;
        float firstY = -gridHeight * 0.5f + cellSize.y * 0.5f;
        float contentHeight = GetRequiredContentHeight(viewportHeight, cellSize, firstY, stepY);
        if (scrollContent != null)
            scrollContent.sizeDelta = new Vector2(0f, contentHeight);
        contentRoot.sizeDelta = new Vector2(contentRoot.sizeDelta.x, contentHeight);

        for (int i = 0; i < currentGrid.cells.Count; i++)
        {
            RunMapCellModel model = currentGrid.cells[i];
            if (model == null)
                continue;

            CellView cell = CreateCell(model, new Vector2(firstX + model.x * stepX, firstY + model.y * stepY), cellSize);
            cells.Add(cell);
        }
        playerMarker.SetAsLastSibling();
    }

    private Vector2 GetCellSize()
    {
        Vector2 size = cellPrefab != null ? cellPrefab.sizeDelta : Vector2.zero;
        if (size.x <= 0f || size.y <= 0f)
        {
            float baseGridSide = gridRoot.rect.width > 1f ? gridRoot.rect.width : DefaultGridSide;
            float scaledGridSide = baseGridSide * Mathf.Max(1f, gridScale);
            float fallbackSize = Mathf.Max(24f, (scaledGridSide - CellSpacing * (currentGrid.width - 1)) / currentGrid.width);
            if (size.x <= 0f)
                size.x = fallbackSize;
            if (size.y <= 0f)
                size.y = fallbackSize;
        }
        return size;
    }

    private float GetRequiredContentHeight(float viewportHeight, Vector2 cellSize, float firstY, float stepY)
    {
        float minCellY = gridRoot.anchoredPosition.y + firstY;
        float maxCellY = minCellY + stepY * (currentGrid.height - 1);
        float topPadding = Mathf.Max(80f, maxCellY + cellSize.y * 0.5f);
        float bottomExtent = Mathf.Max(0f, -minCellY + cellSize.y * 0.5f);
        return Mathf.Max(viewportHeight, topPadding + bottomExtent + viewportHeight * 0.5f);
    }

    private CellView CreateCell(RunMapCellModel model, Vector2 anchoredPosition, Vector2 cellSize)
    {
        RectTransform rect = cellPrefab != null
            ? Instantiate(cellPrefab, gridRoot)
            : new GameObject($"Cell_{model.x}_{model.y}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<RectTransform>();
        rect.name = $"Cell_{model.x}_{model.y}";
        rect.SetParent(gridRoot, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        if (cellPrefab == null)
            rect.sizeDelta = cellSize;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.gameObject.SetActive(true);

        Image background = rect.GetComponent<Image>();
        if (background == null)
            background = rect.gameObject.AddComponent<Image>();
        background.raycastTarget = false;

        Image icon = FindChildImage(rect, "Icon");
        if (icon == null)
            icon = CreateFallbackIcon(rect, Mathf.Min(cellSize.x, cellSize.y));
        icon.raycastTarget = false;
        icon.preserveAspect = true;

        TMP_Text label = FindChildText(rect, "Label");
        if (label == null)
            label = CreateFallbackLabel(rect, Mathf.Min(cellSize.x, cellSize.y));
        label.font = UIManager.GetDefaultTMPFont();
        label.raycastTarget = false;

        MapGridSlotView slotView = rect.GetComponent<MapGridSlotView>();
        if (slotView == null)
            slotView = rect.gameObject.AddComponent<MapGridSlotView>();
        slotView.Initialize(background, icon, label);

        CellView cell = new CellView { Model = model, Rect = rect, SlotView = slotView, Icon = icon, Label = label };
        ApplyCellVisual(cell);
        return cell;
    }

    private Image FindChildImage(RectTransform parent, string childName)
    {
        Transform child = parent.Find(childName);
        return child != null ? child.GetComponent<Image>() : null;
    }

    private TMP_Text FindChildText(RectTransform parent, string childName)
    {
        Transform child = parent.Find(childName);
        return child != null ? child.GetComponent<TMP_Text>() : null;
    }

    private Image CreateFallbackIcon(RectTransform parent, float cellSize)
    {
        Image icon = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        icon.transform.SetParent(parent, false);
        RectTransform iconRect = (RectTransform)icon.transform;
        iconRect.anchorMin = new Vector2(0.5f, 0.5f);
        iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = new Vector2(0f, 8f);
        iconRect.sizeDelta = new Vector2(cellSize * 0.46f, cellSize * 0.46f);
        return icon;
    }

    private TMP_Text CreateFallbackLabel(RectTransform parent, float cellSize)
    {
        TMP_Text label = new GameObject("Label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        label.transform.SetParent(parent, false);
        RectTransform labelRect = (RectTransform)label.transform;
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 0f);
        labelRect.pivot = new Vector2(0.5f, 0f);
        labelRect.anchoredPosition = new Vector2(0f, 6f);
        labelRect.sizeDelta = new Vector2(0f, 24f);
        label.fontSize = Mathf.Clamp(cellSize * 0.18f, 13f, 18f);
        label.alignment = TextAlignmentOptions.Center;
        return label;
    }

    private void ApplyCellVisual(CellView cell)
    {
        if (cell == null || cell.Icon == null)
            return;

        Sprite iconSprite = null;
        string labelText = string.Empty;
        if (cell.Model != null && cell.Model.isBoss)
        {
            iconSprite = Resources.Load<Sprite>("Images/UI/Boss");
            labelText = "Boss";
        }
        else if (cell.Model != null && cell.Model.level != null)
        {
            LevelType type = cell.Model.level.levelType;
            iconSprite = UIManager.LoadLevelTypeSprite(type);
            labelText = UIManager.GetLevelTypeName(type);
        }

        if (cell.SlotView != null)
        {
            cell.SlotView.Apply(cell.Model, iconSprite, labelText);
            return;
        }

        bool unavailable = cell.Model == null || !cell.Model.isAvailable;
        bool hiddenByFog = !unavailable && !cell.Model.isRevealed;
        bool hasContent = !unavailable && !hiddenByFog && (cell.Model.isBoss || cell.Model.level != null);
        if (!hasContent)
        {
            cell.Icon.gameObject.SetActive(false);
            if (cell.Label != null)
                cell.Label.text = string.Empty;
            return;
        }

        cell.Icon.gameObject.SetActive(true);
        cell.Icon.rectTransform.localScale = Vector3.one;
        if (cell.Label != null)
            cell.Label.color = labelColor;
        cell.Icon.sprite = iconSprite;
        cell.Icon.color = cell.Model != null && cell.Model.isBoss ? bossIconColor : levelIconColor;
        if (cell.Label != null)
            cell.Label.text = labelText;
    }

    private void ClearGrid()
    {
        cells.Clear();
        if (gridRoot == null)
            return;

        for (int i = gridRoot.childCount - 1; i >= 0; i--)
            Destroy(gridRoot.GetChild(i).gameObject);
    }

    private void RefreshDirectionButtons()
    {
        if (directionRoot == null)
            return;

        EnsureDirectionButton(MaterialEnum.Fire, 0);
        EnsureDirectionButton(MaterialEnum.Wind, 1);
        EnsureDirectionButton(MaterialEnum.Water, 2);
        EnsureDirectionButton(MaterialEnum.Earth, 3);
        foreach (RectTransform button in directionButtons.Values)
        {
            Button unityButton = button.GetComponent<Button>();
            if (unityButton != null)
                unityButton.interactable = !inputLocked;
        }
    }

    private void EnsureDirectionButton(MaterialEnum material, int index)
    {
        if (directionButtons.TryGetValue(material, out RectTransform rect) && rect != null)
        {
            ApplyDirectionCardLayout(rect, index);
            ApplyDirectionCardTooltip(rect, material);
            return;
        }

        rect = CreateDirectionCard(material, index);
        directionButtons[material] = rect;
    }

    private RectTransform CreateDirectionCard(MaterialEnum material, int index)
    {
        RectTransform rect = directionCardPrefab != null
            ? Instantiate(directionCardPrefab, directionRoot)
            : new GameObject(DirectionCardPrefix + material, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(JuicyMotion)).GetComponent<RectTransform>();
        rect.name = DirectionCardPrefix + material;
        rect.gameObject.SetActive(true);
        ApplyDirectionCardLayout(rect, index);

        MapDirectionCardView mapDirectionCardView = rect.GetComponent<MapDirectionCardView>();
        if (mapDirectionCardView != null)
        {
            mapDirectionCardView.Initialize(this, material);
            mapDirectionCardView.SetInteractable(!inputLocked);
        }
        else
        {
            DirectionButtonHandler handler = rect.GetComponent<DirectionButtonHandler>();
            if (handler == null)
                handler = rect.gameObject.AddComponent<DirectionButtonHandler>();
            handler.Initialize(this, material);
        }

        return rect;
    }

    private void ApplyDirectionCardTooltip(RectTransform rect, MaterialEnum material)
    {
        MapDirectionCardView mapDirectionCardView = rect != null ? rect.GetComponent<MapDirectionCardView>() : null;
        if (mapDirectionCardView != null)
            mapDirectionCardView.Initialize(this, material);
        else
        {
            DirectionButtonHandler handler = rect != null ? rect.GetComponent<DirectionButtonHandler>() : null;
            if (handler == null && rect != null)
                handler = rect.gameObject.AddComponent<DirectionButtonHandler>();
            handler?.Initialize(this, material);
        }
    }

    private void ApplyDirectionCardLayout(RectTransform rect, int index)
    {
        rect.SetParent(directionRoot, false);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = GetDirectionCardPosition(index);
        Vector3 localPosition = rect.localPosition;
        localPosition.z = directionCardLocalZ;
        rect.localPosition = localPosition;
        if (directionCardPrefab == null)
            rect.sizeDelta = new Vector2(directionCardSize, directionCardSize);
    }

    private Vector2 GetDirectionCardPosition(int index)
    {
        return new Vector2((index - 1.5f) * directionCardSpacing, directionCardY);
    }

    private void ClearDirectionButtons()
    {
        foreach (RectTransform rect in directionButtons.Values)
        {
            if (rect != null)
                Destroy(rect.gameObject);
        }
        directionButtons.Clear();

        if (directionRoot == null)
            return;
        for (int i = directionRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = directionRoot.GetChild(i);
            if (child.name.StartsWith(DirectionCardPrefix))
                Destroy(child.gameObject);
        }
    }

    private CellView FindCell(int x, int y)
    {
        for (int i = 0; i < cells.Count; i++)
        {
            CellView cell = cells[i];
            if (cell != null && cell.Model != null && cell.Model.x == x && cell.Model.y == y)
                return cell;
        }
        return null;
    }

    private Vector2 GetCellPosition(int x, int y)
    {
        CellView cell = FindCell(x, y);
        return cell != null && cell.Rect != null ? cell.Rect.anchoredPosition : Vector2.zero;
    }

    private Vector2 GetMarkerPosition(int x, int y)
    {
        return gridRoot != null ? gridRoot.anchoredPosition + GetCellPosition(x, y) : GetCellPosition(x, y);
    }

    private void UpdateScrollToMarker(bool instant)
    {
        if (scrollRect == null || scrollContent == null || scrollRect.viewport == null || playerMarker == null)
            return;

        float scrollableHeight = Mathf.Max(0f, scrollContent.rect.height - scrollRect.viewport.rect.height);
        scrollRect.StopMovement();
        scrollRect.velocity = Vector2.zero;
        if (scrollableHeight <= 0f)
        {
            scrollContent.anchoredPosition = Vector2.zero;
            return;
        }

        float markerCenterFromTop = Mathf.Max(0f, -playerMarker.anchoredPosition.y);
        float targetY = Mathf.Clamp(markerCenterFromTop - scrollRect.viewport.rect.height * 0.5f, 0f, scrollableHeight);
        scrollTween?.Kill(false);
        if (instant)
        {
            scrollContent.anchoredPosition = new Vector2(scrollContent.anchoredPosition.x, targetY);
            Canvas.ForceUpdateCanvases();
            scrollRect.StopMovement();
            scrollRect.velocity = Vector2.zero;
            return;
        }

        scrollTween = scrollContent.DOAnchorPosY(targetY, verticalScrollFollowDuration).SetEase(verticalScrollFollowEase).SetTarget(this);
    }

    private void ResetScrollMotion()
    {
        scrollTween?.Kill(false);
        rectTransform.DOKill(false);
        if (scrollContent != null)
            scrollContent.DOKill(false);
        if (contentRoot != null)
            contentRoot.DOKill(false);
        if (scrollRect == null)
            return;

        scrollRect.StopMovement();
        scrollRect.velocity = Vector2.zero;
    }

    private CanvasGroup GetOrAddCanvasGroup()
    {
        CanvasGroup canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        return canvasGroup;
    }

    private void PlayPanelShow()
    {
        CanvasGroup canvasGroup = GetOrAddCanvasGroup();
        panelTween?.Kill(false);
        canvasGroup.alpha = 0f;
        rectTransform.anchoredPosition = shownAnchoredPosition + panelSlideOffset;
        panelTween = DOTween.Sequence().SetTarget(this)
            .Join(canvasGroup.DOFade(1f, panelShowDuration).SetEase(panelShowEase))
            .Join(rectTransform.DOAnchorPos(shownAnchoredPosition, panelShowDuration).SetEase(panelShowEase));
    }
}

