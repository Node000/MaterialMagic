using System.Collections;
using System.Collections.Generic;
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
    private const float CellSpacing = 8f;
    private const float DefaultGridSide = 350f;

    [SerializeField] private RectTransform gridRoot;
    [SerializeField] private RectTransform playerMarker;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text hintText;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private BonusRewardIconUI rewardIconPrefab;

    private readonly List<RewardCell> cells = new List<RewardCell>();

    private BonusLevelData currentData;
    private RectTransform rewardTooltip;
    private CanvasGroup rewardTooltipCanvasGroup;
    private TMP_Text rewardTooltipTitle;
    private TMP_Text rewardTooltipDescription;
    private int gridSize = 5;
    private int centerIndex = 2;
    private int playerX = 2;
    private int playerY = 2;

    public int DrawCount => currentData != null && currentData.drawCount > 0 ? currentData.drawCount : 5;

    public void Initialize(HandSystemUI owner)
    {
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
            hintText.text = "打出素材，按火上 / 风左 / 水下 / 土右移动。";
        if (resultText != null)
            resultText.text = "从中心出发。";
        gameObject.SetActive(true);
        RefreshAllCells();
        MoveMarkerToCurrentCell(false);
    }

    public void ShowNewGrid(PlayerState playerState)
    {
        ShowNewGrid(CreateFallbackData(), playerState);
    }

    public void Hide()
    {
        HideRewardTooltip();
        gameObject.SetActive(false);
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
            ApplyCellReward(playerX, playerY, playerState);
            MoveMarkerToCurrentCell(true);
            yield return new WaitForSeconds(StepDelay);
        }
    }

    public void ShowRewardTooltip(RectTransform anchor, BonusRewardData rewardData)
    {
        if (anchor == null || rewardData == null)
            return;

        EnsureRewardTooltip();
        if (rewardTooltip == null)
            return;

        if (rewardTooltipTitle != null)
            rewardTooltipTitle.text = GetRewardSummary(rewardData);
        if (rewardTooltipDescription != null)
            rewardTooltipDescription.text = GetRewardDescription(rewardData);

        rewardTooltip.gameObject.SetActive(true);
        rewardTooltipCanvasGroup.alpha = 1f;
        rewardTooltip.position = anchor.position + new Vector3(0f, 42f, 0f);
        PopupLayerUtility.ApplyTo(rewardTooltip);
        rewardTooltip.SetAsLastSibling();
    }

    public void HideRewardTooltip()
    {
        if (rewardTooltip == null)
            return;

        rewardTooltip.gameObject.SetActive(false);
    }

    private void CacheReferences()
    {
        if (gridRoot == null)
            gridRoot = UIManager.FindChildRect(transform, "GridRoot");
        if (playerMarker == null)
            playerMarker = UIManager.FindChildRect(transform, "PlayerMarker");
        if (titleText == null)
            titleText = UIManager.FindChildComponent<TMP_Text>(transform, "Title");
        if (hintText == null)
            hintText = UIManager.FindChildComponent<TMP_Text>(transform, "Hint");
        if (resultText == null)
            resultText = UIManager.FindChildComponent<TMP_Text>(transform, "ResultText");
    }

    private void BuildGrid()
    {
        if (gridRoot == null)
            return;

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
        float size = Mathf.Clamp(cellSize * 0.72f, 20f, 36f);
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
        background.color = new Color(1f, 1f, 1f, 0.08f);

        GameObject iconImageObject = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconImageObject.transform.SetParent(iconObject.transform, false);
        RectTransform iconRect = iconImageObject.GetComponent<RectTransform>();
        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;
        iconImageObject.GetComponent<Image>().raycastTarget = false;

        GameObject amountObject = new GameObject("AmountText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        amountObject.transform.SetParent(iconObject.transform, false);
        RectTransform amountRect = amountObject.GetComponent<RectTransform>();
        amountRect.anchorMin = new Vector2(0f, 0f);
        amountRect.anchorMax = new Vector2(1f, 0.45f);
        amountRect.offsetMin = Vector2.zero;
        amountRect.offsetMax = Vector2.zero;
        TMP_Text text = amountObject.GetComponent<TMP_Text>();
        text.font = GetDefaultFont();
        text.fontSize = 12;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
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

        return rewards[UnityEngine.Random.Range(0, rewards.Length)];
    }

    private void ApplyCellReward(int x, int y, PlayerState playerState)
    {
        int index = GetIndex(x, y);
        if (index < 0 || index >= cells.Count)
            return;

        RewardCell cell = cells[index];
        if (cell.Collected || cell.Reward == null)
            return;

        cell.Collected = true;
        ApplyReward(cell.Reward, playerState);
        AppendResult(GetRewardSummary(cell.Reward));
        RefreshCell(cell);
        HideRewardTooltip();
    }

    private static void ApplyReward(BonusRewardData rewardData, PlayerState playerState)
    {
        switch (rewardData.rewardType)
        {
            case BonusRewardType.Gold:
                playerState.AddGold(rewardData.amount);
                break;
            case BonusRewardType.Heal:
                playerState.Heal(rewardData.amount);
                break;
        }
    }

    private void RefreshAllCells()
    {
        for (int i = 0; i < cells.Count; i++)
            RefreshCell(cells[i]);
    }

    private static void RefreshCell(RewardCell cell)
    {
        if (cell.Background != null)
            cell.Background.color = cell.Collected ? new Color(0.08f, 0.07f, 0.12f, 0.74f) : new Color(0.16f, 0.11f, 0.22f, 0.9f);
        if (cell.Icon != null)
            cell.Icon.gameObject.SetActive(!cell.Collected);
    }

    private void MoveMarkerToCurrentCell(bool animate)
    {
        if (playerMarker == null || gridRoot == null)
            return;

        Vector2 target = GetCellAnchoredPosition(playerX, playerY);
        if (!animate)
        {
            playerMarker.anchoredPosition = target;
            return;
        }

        playerMarker.anchoredPosition = target;
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
        image.transform.SetParent(transform, false);
        image.color = new Color(0.03f, 0.025f, 0.06f, 0.96f);
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
                new BonusRewardData { rewardType = BonusRewardType.Gold, rewardName = "金币", amount = 1, texturePath = "Images/UI/大点" },
                new BonusRewardData { rewardType = BonusRewardType.Gold, rewardName = "金币", amount = 2, texturePath = "Images/UI/大点" },
                new BonusRewardData { rewardType = BonusRewardType.Heal, rewardName = "生命值", amount = 2, texturePath = "Images/UI/大点" },
                new BonusRewardData { rewardType = BonusRewardType.Heal, rewardName = "生命值", amount = 3, texturePath = "Images/UI/大点" }
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
