using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RewardGridPanelUI : MonoBehaviour
{
    private class RewardCell
    {
        public BonusRewardData Reward;
        public bool Collected;
        public RectTransform Rect;
        public Image Background;
        public BonusRewardIconUI Icon;
    }

    private const float StepDelay = 0.16f;
    private const float MarkerMoveDuration = 0.32f;
    private const Ease MarkerMoveEase = Ease.InOutSine;
    private const float CellSpacing = 8f;
    private const float DefaultGridSide = 350f;
    private const string PanelContentRootName = "PanelContent";

    [SerializeField] private RectTransform gridRoot;
    [SerializeField] private RectTransform playerMarker;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text hintText;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private BonusRewardIconUI rewardIconPrefab;
    [SerializeField] private float panelOpenDuration = 0.28f;
    [SerializeField] private float panelCloseDuration = 0.24f;
    [SerializeField] private Ease panelOpenEase = Ease.OutCubic;
    [SerializeField] private Ease panelCloseEase = Ease.InCubic;
    [SerializeField] private Vector2 panelSlideOffset = new Vector2(28f, -28f);
    [SerializeField] private float cellPopupStartDelay = 0.08f;
    [SerializeField] private float cellPopupInterval = 0.06f;
    [SerializeField] private float cellPopupDuration = 0.32f;
    [SerializeField] private float cellPopupOvershootScale = 1.16f;

    private readonly List<RewardCell> cells = new List<RewardCell>();
    private readonly List<Tween> cellPopupTweens = new List<Tween>();

    private HandSystemUI owner;
    private Tween markerMoveTween;
    private Sequence panelTransitionSequence;
    private RectMask2D panelMask;
    private RectTransform panelContentRoot;
    private Image panelContentBackground;
    private bool panelContentConfigured;
    private BonusLevelData currentData;
    private RectTransform rewardTooltip;
    private CanvasGroup rewardTooltipCanvasGroup;
    private TMP_Text rewardTooltipTitle;
    private TMP_Text rewardTooltipDescription;
    private int gridSize = 5;
    private int centerIndex = 2;
    private int playerX = 2;
    private int playerY = 2;
    private bool panelOpenStateCached;
    private Vector2 panelOriginalAnchoredPosition;
    private Vector4 panelOriginalPadding;

    public int DrawCount => currentData != null && currentData.drawCount > 0 ? currentData.drawCount : 5;

    public void Initialize(HandSystemUI owner)
    {
        this.owner = owner;
        CacheReferences();
        gameObject.SetActive(false);
    }

    public void ShowNewGrid(BonusLevelData bonusData, PlayerState playerState)
    {
        CacheReferences();
        currentData = bonusData ?? CreateFallbackData();
        BuildGrid();
        playerX = centerIndex;
        playerY = centerIndex;
        if (titleText != null)
            titleText.text = "奖励关卡";
        if (hintText != null)
            hintText.text = "打出箭头，按上 / 左 / 下 / 右移动。";
        if (resultText != null)
            resultText.text = "从中心出发。";
        gameObject.SetActive(true);
        RefreshAllCells();
        MoveMarkerToCurrentCell(false);
        PlayOpenAnimation();
        PlayCellPopupAnimation();
    }

    public void ShowNewGrid(PlayerState playerState)
    {
        ShowNewGrid(CreateFallbackData(), playerState);
    }

    public void Hide()
    {
        HideAnimated();
    }

    public IEnumerator HideRoutine()
    {
        Tween tween = HideAnimated();
        if (tween != null)
            yield return tween.WaitForCompletion();
    }

    private Tween HideAnimated()
    {
        if (!gameObject.activeInHierarchy)
        {
            HideImmediate();
            return null;
        }

        markerMoveTween?.Kill(false);
        markerMoveTween = null;
        KillCellPopupTweens();
        HideRewardTooltip();
        return PlayCloseAnimation();
    }

    private void HideImmediate()
    {
        markerMoveTween?.Kill(false);
        markerMoveTween = null;
        KillPanelTransition(true);
        KillCellPopupTweens();
        HideRewardTooltip();
        if (gameObject.activeSelf)
            gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        markerMoveTween?.Kill(false);
        markerMoveTween = null;
        KillPanelTransition(true);
        KillCellPopupTweens();
    }

    public IEnumerator ResolvePathRoutine(IReadOnlyList<MaterialModel> pathCards, PlayerState playerState)
    {
        if (pathCards == null || playerState == null)
            yield break;

        if (resultText != null)
            resultText.text = string.Empty;

        for (int i = 0; i < pathCards.Count; i++)
        {
            MaterialModel card = pathCards[i];
            Vector2Int direction = GetDirection(card != null ? card.material : MaterialEnum.None);
            if (direction == Vector2Int.zero)
                continue;

            int nextX = Mathf.Clamp(playerX + direction.x, 0, gridSize - 1);
            int nextY = Mathf.Clamp(playerY + direction.y, 0, gridSize - 1);
            if (nextX == playerX && nextY == playerY)
            {
                AppendResult("边缘停步");
                yield return new WaitForSeconds(StepDelay);
                continue;
            }

            playerX = nextX;
            playerY = nextY;
            Tween moveTween = MoveMarkerToCurrentCell(true);
            if (moveTween != null)
                yield return moveTween.WaitForCompletion();

            yield return ApplyCellRewardRoutine(playerX, playerY, playerState);
            yield return new WaitForSeconds(StepDelay);
        }
    }

    public void ShowRewardTooltip(RectTransform anchor, BonusRewardData rewardData)
    {
        if (anchor == null || rewardData == null || owner == null)
            return;

        owner.GetUIManager().ShowUnifiedDetailPopup(anchor, UnifiedDetailContentBuilder.Build(rewardData));
    }

    public void HideRewardTooltip()
    {
        owner?.GetUIManager().HideUnifiedDetailPopup(null);
    }

    public void PinRewardTooltip(RectTransform anchor, BonusRewardData rewardData)
    {
        if (anchor == null || rewardData == null || owner == null)
            return;

        owner.GetUIManager().PinUnifiedDetailPopup(anchor, UnifiedDetailContentBuilder.Build(rewardData));
    }

    private void CacheReferences()
    {
        EnsurePanelContentRoot();
        if (gridRoot == null)
            gridRoot = FindPanelChildRect("GridRoot");
        if (playerMarker == null)
            playerMarker = FindPanelChildRect("PlayerMarker");
        if (titleText == null)
            titleText = FindPanelChildComponent<TMP_Text>("Title");
        if (hintText == null)
            hintText = FindPanelChildComponent<TMP_Text>("Hint");
        if (resultText == null)
            resultText = FindPanelChildComponent<TMP_Text>("ResultText");
    }

    private void EnsurePanelContentRoot()
    {
        RectTransform panelRect = transform as RectTransform;
        if (panelRect == null)
            return;

        if (panelMask == null)
        {
            panelMask = GetComponent<RectMask2D>();
            if (panelMask == null)
                panelMask = gameObject.AddComponent<RectMask2D>();
        }

        if (panelContentRoot == null)
        {
            Transform existing = transform.Find(PanelContentRootName);
            panelContentRoot = existing as RectTransform;
        }

        if (panelContentRoot == null)
        {
            GameObject contentObject = new GameObject(PanelContentRootName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panelContentRoot = contentObject.GetComponent<RectTransform>();
            panelContentRoot.SetParent(transform, false);
        }

        panelContentRoot.anchorMin = Vector2.zero;
        panelContentRoot.anchorMax = Vector2.one;
        panelContentRoot.offsetMin = Vector2.zero;
        panelContentRoot.offsetMax = Vector2.zero;
        panelContentRoot.pivot = panelRect.pivot;
        panelContentRoot.localScale = Vector3.one;
        panelContentRoot.localRotation = Quaternion.identity;
        panelContentRoot.SetAsFirstSibling();

        Image blockerImage = GetComponent<Image>();
        if (panelContentBackground == null)
            panelContentBackground = panelContentRoot.GetComponent<Image>();
        if (panelContentBackground == null)
            panelContentBackground = panelContentRoot.gameObject.AddComponent<Image>();

        if (!panelContentConfigured && blockerImage != null)
        {
            CopyImage(blockerImage, panelContentBackground);
            Color blockerColor = blockerImage.color;
            blockerColor.a = 0f;
            blockerImage.color = blockerColor;
            blockerImage.raycastTarget = true;
            panelContentConfigured = true;
        }
        panelContentBackground.raycastTarget = false;

        List<Transform> childrenToMove = null;
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child == panelContentRoot || child == rewardTooltip)
                continue;

            if (childrenToMove == null)
                childrenToMove = new List<Transform>();
            childrenToMove.Add(child);
        }

        if (childrenToMove == null)
            return;

        for (int i = 0; i < childrenToMove.Count; i++)
            childrenToMove[i].SetParent(panelContentRoot, false);
    }

    private static void CopyImage(Image source, Image target)
    {
        target.sprite = source.sprite;
        target.type = source.type;
        target.preserveAspect = source.preserveAspect;
        target.fillCenter = source.fillCenter;
        target.fillMethod = source.fillMethod;
        target.fillAmount = source.fillAmount;
        target.fillClockwise = source.fillClockwise;
        target.fillOrigin = source.fillOrigin;
        target.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
        target.material = source.material;
        target.color = source.color;
    }

    private RectTransform FindPanelChildRect(string objectName)
    {
        Transform child = transform.Find(objectName);
        if (child == null && panelContentRoot != null)
            child = panelContentRoot.Find(objectName);
        return child as RectTransform;
    }

    private T FindPanelChildComponent<T>(string objectName) where T : Component
    {
        Transform child = transform.Find(objectName);
        if (child == null && panelContentRoot != null)
            child = panelContentRoot.Find(objectName);
        return child != null ? child.GetComponent<T>() : null;
    }

    private void BuildGrid()
    {
        if (gridRoot == null)
            return;

        KillCellPopupTweens();
        ClearGridChildren();
        cells.Clear();
        int radius = Mathf.Max(1, currentData.radius);
        gridSize = radius * 2 + 1;
        centerIndex = radius;
        float gridSide = gridRoot.rect.width > 1f ? gridRoot.rect.width : (gridRoot.sizeDelta.x > 1f ? gridRoot.sizeDelta.x : DefaultGridSide);
        float cellSize = Mathf.Max(24f, (gridSide - CellSpacing * (gridSize - 1)) / gridSize);
        float step = cellSize + CellSpacing;
        float first = -step * (gridSize - 1) * 0.5f;
        for (int y = 0; y < gridSize; y++)
        {
            for (int x = 0; x < gridSize; x++)
            {
                int index = GetIndex(x, y);
                RewardCell cell = CreateCell(index, new Vector2(first + x * step, first + y * step), cellSize);
                if (x != centerIndex || y != centerIndex)
                {
                    cell.Reward = CreateRandomReward();
                    if (cell.Reward != null)
                        cell.Icon = CreateRewardIcon(cell.Rect, cell.Reward, cellSize);
                }
                cells.Add(cell);
            }
        }
    }

    private RewardCell CreateCell(int index, Vector2 anchoredPosition, float cellSize)
    {
        GameObject cellObject = new GameObject("Cell" + index, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        cellObject.transform.SetParent(gridRoot, false);
        RectTransform rect = cellObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(cellSize, cellSize);
        Image background = cellObject.GetComponent<Image>();
        background.raycastTarget = false;
        return new RewardCell { Rect = rect, Background = background };
    }

    private BonusRewardIconUI CreateRewardIcon(RectTransform parent, BonusRewardData rewardData, float cellSize)
    {
        BonusRewardIconUI icon = rewardIconPrefab != null ? Instantiate(rewardIconPrefab, parent) : CreateRuntimeRewardIcon(parent);
        RectTransform rect = icon.RectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        float size = Mathf.Clamp(cellSize * 1.44f, 40f, 72f);
        rect.sizeDelta = new Vector2(size, size);
        icon.Bind(this, rewardData);
        return icon;
    }

    private BonusRewardIconUI CreateRuntimeRewardIcon(RectTransform parent)
    {
        GameObject iconObject = new GameObject("RewardIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(BonusRewardIconUI));
        iconObject.transform.SetParent(parent, false);
        Image background = iconObject.GetComponent<Image>();
        background.raycastTarget = true;
        background.color = new Color(1f, 1f, 1f, 1f);

        GameObject iconImageObject = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconImageObject.transform.SetParent(iconObject.transform, false);
        RectTransform iconRect = iconImageObject.GetComponent<RectTransform>();
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;
        iconImageObject.GetComponent<Image>().raycastTarget = false;

        return iconObject.GetComponent<BonusRewardIconUI>();
    }

    private void ClearGridChildren()
    {
        for (int i = gridRoot.childCount - 1; i >= 0; i--)
        {
            GameObject child = gridRoot.GetChild(i).gameObject;
            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }
    }

    private BonusRewardData CreateRandomReward()
    {
        BonusRewardData[] rewards = currentData != null ? currentData.rewards : null;
        if (rewards == null || rewards.Length == 0)
            return null;

        return rewards[NextRunRandomInt(0, rewards.Length)];
    }

    private int NextRunRandomInt(int minInclusive, int maxExclusive)
    {
        return owner != null && owner.RunManager != null ? owner.RunManager.NextRandomInt(minInclusive, maxExclusive) : UnityEngine.Random.Range(minInclusive, maxExclusive);
    }

    private IEnumerator ApplyCellRewardRoutine(int x, int y, PlayerState playerState)
    {
        int index = GetIndex(x, y);
        if (index < 0 || index >= cells.Count)
            yield break;

        RewardCell cell = cells[index];
        if (cell.Collected || cell.Reward == null)
            yield break;

        cell.Collected = true;
        yield return ApplyRewardRoutine(cell, playerState);
        AppendResult(GetRewardSummary(cell.Reward));
        RefreshCell(cell);
        HideRewardTooltip();
    }

    private IEnumerator ApplyRewardRoutine(RewardCell cell, PlayerState playerState)
    {
        BonusRewardData rewardData = cell.Reward;
        switch (rewardData.rewardType)
        {
            case BonusRewardType.Gold:
                RectTransform sourceRect = cell.Icon != null ? cell.Icon.RectTransform : cell.Rect;
                if (owner != null)
                    yield return owner.GainGoldAnimated(rewardData.amount, sourceRect);
                else
                    playerState.AddGold(rewardData.amount);
                break;
            case BonusRewardType.Heal:
                if (owner != null)
                    owner.ApplyRewardHeal(rewardData.amount);
                else
                    playerState.Heal(rewardData.amount);
                break;
        }
    }

    private void RefreshAllCells()
    {
        for (int i = 0; i < cells.Count; i++)
            RefreshCell(cells[i]);
    }

    private void PlayOpenAnimation()
    {
        RectTransform panelRect = transform as RectTransform;
        if (panelRect == null)
            return;

        EnsurePanelContentRoot();
        KillPanelTransition(true);
        panelOriginalAnchoredPosition = panelRect.anchoredPosition;
        panelOriginalPadding = panelMask != null ? panelMask.padding : Vector4.zero;
        panelOpenStateCached = true;

        Vector2 panelSize = GetPanelSize(panelRect);
        Vector4 startPadding = new Vector4(0f, panelSize.y, panelSize.x, 0f);
        panelRect.anchoredPosition = panelOriginalAnchoredPosition - panelSlideOffset;
        if (panelMask != null)
            panelMask.padding = startPadding;

        float duration = Mathf.Max(0.01f, panelOpenDuration);
        panelTransitionSequence = DOTween.Sequence().SetTarget(this);
        panelTransitionSequence.Join(panelRect.DOAnchorPos(panelOriginalAnchoredPosition, duration).SetEase(panelOpenEase));
        if (panelMask != null)
        {
            panelTransitionSequence.Join(DOVirtual.Float(0f, 1f, duration, value =>
            {
                panelMask.padding = Vector4.LerpUnclamped(startPadding, Vector4.zero, value);
            }).SetEase(panelOpenEase));
        }
        panelTransitionSequence.OnComplete(() =>
        {
            panelTransitionSequence = null;
            RestorePanelTransitionState();
        });
    }

    private Tween PlayCloseAnimation()
    {
        RectTransform panelRect = transform as RectTransform;
        if (panelRect == null)
            return null;

        EnsurePanelContentRoot();
        Vector2 basePosition = panelOpenStateCached ? panelOriginalAnchoredPosition : panelRect.anchoredPosition;
        Vector4 startPadding = panelMask != null ? panelMask.padding : Vector4.zero;
        KillPanelTransition(false);
        panelOriginalAnchoredPosition = basePosition;
        panelOriginalPadding = Vector4.zero;
        panelOpenStateCached = true;

        Vector2 panelSize = GetPanelSize(panelRect);
        Vector4 targetPadding = new Vector4(panelSize.x, 0f, 0f, panelSize.y);
        Vector2 targetPosition = basePosition + panelSlideOffset;
        float duration = Mathf.Max(0.01f, panelCloseDuration);

        panelTransitionSequence = DOTween.Sequence().SetTarget(this);
        panelTransitionSequence.Join(panelRect.DOAnchorPos(targetPosition, duration).SetEase(panelCloseEase));
        if (panelMask != null)
        {
            panelTransitionSequence.Join(DOVirtual.Float(0f, 1f, duration, value =>
            {
                panelMask.padding = Vector4.LerpUnclamped(startPadding, targetPadding, value);
            }).SetEase(panelCloseEase));
        }
        panelTransitionSequence.OnComplete(() =>
        {
            panelTransitionSequence = null;
            RestorePanelTransitionState();
            gameObject.SetActive(false);
        });
        return panelTransitionSequence;
    }

    private void KillPanelTransition(bool restoreState)
    {
        if (panelTransitionSequence != null)
        {
            panelTransitionSequence.Kill(false);
            panelTransitionSequence = null;
        }

        if (restoreState)
            RestorePanelTransitionState();
    }

    private void RestorePanelTransitionState()
    {
        if (!panelOpenStateCached)
            return;

        RectTransform panelRect = transform as RectTransform;
        if (panelRect != null)
            panelRect.anchoredPosition = panelOriginalAnchoredPosition;
        if (panelMask != null)
            panelMask.padding = panelOriginalPadding;

        panelOpenStateCached = false;
    }

    private static Vector2 GetPanelSize(RectTransform rect)
    {
        Vector2 size = rect.rect.size;
        if (size.x <= 1f)
            size.x = rect.sizeDelta.x;
        if (size.y <= 1f)
            size.y = rect.sizeDelta.y;
        size.x = Mathf.Max(1f, size.x);
        size.y = Mathf.Max(1f, size.y);
        return size;
    }

    private void PlayCellPopupAnimation()
    {
        KillCellPopupTweens();
        if (cells.Count == 0)
            return;

        float duration = Mathf.Max(0.01f, cellPopupDuration);
        float firstStepDuration = duration * 0.64f;
        float secondStepDuration = duration - firstStepDuration;
        float interval = Mathf.Max(0f, cellPopupInterval);
        float startDelay = Mathf.Max(0f, panelOpenDuration) + Mathf.Max(0f, cellPopupStartDelay);
        float overshootScale = Mathf.Max(1f, cellPopupOvershootScale);

        for (int i = 0; i < cells.Count; i++)
        {
            RectTransform cellRect = cells[i].Rect;
            if (cellRect == null)
                continue;

            int y = i / gridSize;
            int x = i - y * gridSize;
            int rowFromTop = gridSize - 1 - y;
            float delay = startDelay + (x + rowFromTop) * interval;
            cellRect.localScale = Vector3.zero;

            Sequence sequence = DOTween.Sequence().SetTarget(this);
            sequence.SetDelay(delay);
            sequence.Append(cellRect.DOScale(Vector3.one * overshootScale, firstStepDuration).SetEase(Ease.OutCubic));
            sequence.Append(cellRect.DOScale(Vector3.one, secondStepDuration).SetEase(Ease.InOutSine));
            sequence.OnKill(() =>
            {
                if (cellRect != null)
                    cellRect.localScale = Vector3.one;
            });
            cellPopupTweens.Add(sequence);
        }
    }

    private void KillCellPopupTweens()
    {
        for (int i = 0; i < cellPopupTweens.Count; i++)
        {
            Tween tween = cellPopupTweens[i];
            if (tween != null && tween.IsActive())
                tween.Kill(false);
        }
        cellPopupTweens.Clear();

        for (int i = 0; i < cells.Count; i++)
        {
            if (cells[i].Rect != null)
                cells[i].Rect.localScale = Vector3.one;
        }
    }

    private static void RefreshCell(RewardCell cell)
    {
        if (cell.Background != null)
            cell.Background.color = cell.Collected ? new Color(0.08f, 0.07f, 0.12f, 1f) : new Color(0.16f, 0.11f, 0.22f, 1f);
        if (cell.Icon != null)
            cell.Icon.gameObject.SetActive(!cell.Collected);
    }

    private Tween MoveMarkerToCurrentCell(bool animate)
    {
        if (playerMarker == null || gridRoot == null)
            return null;

        Vector2 target = GetCellAnchoredPosition(playerX, playerY);
        markerMoveTween?.Kill(false);
        markerMoveTween = null;
        if (!animate || !gameObject.activeInHierarchy)
        {
            playerMarker.anchoredPosition = target;
            return null;
        }

        markerMoveTween = playerMarker.DOAnchorPos(target, MarkerMoveDuration).SetEase(MarkerMoveEase).SetTarget(this);
        return markerMoveTween;
    }

    private Vector2 GetCellAnchoredPosition(int x, int y)
    {
        int index = GetIndex(x, y);
        if (index < 0 || index >= cells.Count || cells[index].Rect == null || playerMarker == null)
            return Vector2.zero;

        RectTransform cell = cells[index].Rect;
        RectTransform markerParent = playerMarker.parent as RectTransform;
        if (markerParent == null || cell.parent == markerParent)
            return cell.anchoredPosition;

        Vector3 worldPosition = cell.TransformPoint(cell.rect.center);
        Vector3 localPosition = markerParent.InverseTransformPoint(worldPosition);
        return new Vector2(localPosition.x, localPosition.y);
    }

    private void EnsureRewardTooltip()
    {
        if (rewardTooltip != null)
            return;

        Image image = new GameObject("RewardTooltip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup)).GetComponent<Image>();
        Transform tooltipParent = transform.parent != null ? transform.parent : transform;
        image.transform.SetParent(tooltipParent, false);
        image.color = new Color(0.03f, 0.025f, 0.06f, 1f);
        image.raycastTarget = false;
        rewardTooltip = image.rectTransform;
        rewardTooltip.sizeDelta = new Vector2(180f, 66f);
        rewardTooltip.anchorMin = new Vector2(0.5f, 0.5f);
        rewardTooltip.anchorMax = new Vector2(0.5f, 0.5f);
        rewardTooltip.pivot = new Vector2(0.5f, 0f);
        rewardTooltipCanvasGroup = rewardTooltip.GetComponent<CanvasGroup>();
        rewardTooltipTitle = CreateTooltipText(rewardTooltip, "Title", 17, FontStyles.Bold, new Vector2(0f, 15f), new Vector2(150f, 24f));
        rewardTooltipDescription = CreateTooltipText(rewardTooltip, "Description", 14, FontStyles.Normal, new Vector2(0f, -12f), new Vector2(150f, 24f));
        PopupLayerUtility.ApplyTo(rewardTooltip);
        rewardTooltip.gameObject.SetActive(false);
    }

    private TMP_Text CreateTooltipText(RectTransform parent, string objectName, int fontSize, FontStyles fontStyle, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.font = GetDefaultFont();
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private TMP_FontAsset GetDefaultFont()
    {
        if (titleText != null && titleText.font != null)
            return titleText.font;
        return UIManager.GetDefaultTMPFont();
    }

    private static string GetRewardSummary(BonusRewardData rewardData)
    {
        return GetRewardName(rewardData) + "x" + rewardData.amount;
    }

    private static string GetRewardDescription(BonusRewardData rewardData)
    {
        switch (rewardData.rewardType)
        {
            case BonusRewardType.Gold: return "获得金币";
            case BonusRewardType.Heal: return "恢复生命";
            default: return "获得奖励";
        }
    }

    private static string GetRewardName(BonusRewardData rewardData)
    {
        return !string.IsNullOrEmpty(rewardData.rewardName) ? rewardData.rewardName : rewardData.rewardType.ToString();
    }

    private static Vector2Int GetDirection(MaterialEnum material)
    {
        switch (material)
        {
            case MaterialEnum.Fire: return Vector2Int.up;
            case MaterialEnum.Wind: return Vector2Int.left;
            case MaterialEnum.Water: return Vector2Int.down;
            case MaterialEnum.Earth: return Vector2Int.right;
            default: return Vector2Int.zero;
        }
    }

    private int GetIndex(int x, int y)
    {
        return y * gridSize + x;
    }

    private static BonusLevelData CreateFallbackData()
    {
        return new BonusLevelData
        {
            numericId = 301,
            id = "bonus_reward_001",
            radius = 2,
            drawCount = 5,
            rewards = new[]
            {
                new BonusRewardData { rewardType = BonusRewardType.Gold, rewardName = "金币", amount = 1, texturePath = "Images/Bonus/coin1" },
                new BonusRewardData { rewardType = BonusRewardType.Gold, rewardName = "金币", amount = 2, texturePath = "Images/Bonus/coin2" },
                new BonusRewardData { rewardType = BonusRewardType.Heal, rewardName = "生命值", amount = 2, texturePath = "Images/Bonus/pill1" },
                new BonusRewardData { rewardType = BonusRewardType.Heal, rewardName = "生命值", amount = 3, texturePath = "Images/Bonus/pill2" }
            }
        };
    }

    private void AppendResult(string line)
    {
        if (resultText == null || string.IsNullOrEmpty(line))
            return;

        if (string.IsNullOrEmpty(resultText.text))
            resultText.text = line;
        else
            resultText.text += "\n" + line;
    }
}
