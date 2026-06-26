using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RunHistoryPanelUI : MonoBehaviour
{
    [SerializeField] private Button closeButton;
    [SerializeField] private RectTransform recordListRoot;
    [SerializeField] private RunHistoryRecordItemUI recordItemPrefab;
    [SerializeField] private TMP_Text emptyText;
    [SerializeField] private TMP_Text detailText;
    [SerializeField] private TMP_Text buildListText;
    [SerializeField] private TMP_Text arrowDetailText;
    [SerializeField] private RectTransform magicRoot;
    [SerializeField] private MagicItemView magicViewPrefab;
    [SerializeField] private RunHistoryArrowRowUI arrowRowUI;
    [SerializeField] private int defaultMagicSlotCount = 8;

    private readonly List<RunHistoryRecordItemUI> recordItems = new List<RunHistoryRecordItemUI>();
    private readonly List<RunHistoryRecordData> recordItemRecords = new List<RunHistoryRecordData>();
    private readonly List<MagicItemView> magicViews = new List<MagicItemView>();
    private RunHistoryRecordData selectedRecord;

    public bool IsShowing => gameObject.activeSelf;

    private void Awake()
    {
        CacheReferences();
        BindButtons();
    }

    public void Show()
    {
        CacheReferences();
        BindButtons();
        Refresh();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public bool Contains(Transform hit)
    {
        return hit != null && hit.IsChildOf(transform);
    }

    private void Refresh()
    {
        RunHistoryData history = RunSaveSystem.LoadHistory(RunSaveSystem.CurrentSlotIndex);
        ClearRecordItems();
        selectedRecord = null;
        RunHistoryRecordData[] records = history != null && history.records != null ? history.records : Array.Empty<RunHistoryRecordData>();
        if (emptyText != null)
            emptyText.gameObject.SetActive(records.Length == 0);

        for (int i = 0; i < records.Length; i++)
        {
            RunHistoryRecordData record = records[i];
            if (record == null)
                continue;

            RunHistoryRecordItemUI item = CreateRecordItem();
            if (item == null)
                continue;

            bool selected = selectedRecord == null;
            if (selected)
                selectedRecord = record;
            item.Bind(record, selected, SelectRecord);
            recordItems.Add(item);
            recordItemRecords.Add(record);
        }

        RefreshSelectedRecord();
    }

    private RunHistoryRecordItemUI CreateRecordItem()
    {
        if (recordListRoot == null)
            return null;
        if (recordItemPrefab != null)
        {
            RunHistoryRecordItemUI item = Instantiate(recordItemPrefab, recordListRoot);
            item.gameObject.SetActive(true);
            return item;
        }

        GameObject itemObject = new GameObject("HistoryRecord", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(RunHistoryRecordItemUI));
        itemObject.transform.SetParent(recordListRoot, false);
        RectTransform rect = itemObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(340f, 72f);
        TMP_Text text = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        text.transform.SetParent(itemObject.transform, false);
        text.font = UIManager.GetDefaultTMPFont();
        text.fontSize = 18f;
        text.alignment = TextAlignmentOptions.Left;
        text.color = Color.white;
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12f, 6f);
        textRect.offsetMax = new Vector2(-12f, -6f);
        return itemObject.GetComponent<RunHistoryRecordItemUI>();
    }

    private void SelectRecord(RunHistoryRecordData record)
    {
        selectedRecord = record;
        for (int i = 0; i < recordItems.Count; i++)
            recordItems[i].SetSelected(i < recordItemRecords.Count && ReferenceEquals(recordItemRecords[i], record));
        RefreshSelectedRecord();
    }

    private void RefreshSelectedRecord()
    {
        if (detailText != null)
            detailText.text = selectedRecord != null ? BuildDetailText(selectedRecord) : "暂无历史记录";
        if (buildListText != null)
        {
            buildListText.text = string.Empty;
            buildListText.gameObject.SetActive(false);
        }
        RefreshMagicViews(selectedRecord);
        RefreshArrowViews(selectedRecord);
    }

    private void RefreshMagicViews(RunHistoryRecordData record)
    {
        ClearMagicViews();
        if (magicRoot == null || magicViewPrefab == null || record == null)
            return;

        int slotCount = Mathf.Max(defaultMagicSlotCount, GetMaxMagicSlotIndex(record.magicBook) + 1);
        for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
        {
            MagicItemView view = Instantiate(magicViewPrefab, magicRoot);
            view.gameObject.SetActive(true);
            view.Bind(CreateMagicForSlot(record.magicBook, slotIndex));
            magicViews.Add(view);
        }
    }

    private void RefreshArrowViews(RunHistoryRecordData record)
    {
        if (arrowRowUI == null)
            return;

        arrowRowUI.ArrowHovered -= ShowArrowDetail;
        arrowRowUI.ArrowUnhovered -= ClearArrowDetail;
        arrowRowUI.ArrowHovered += ShowArrowDetail;
        arrowRowUI.ArrowUnhovered += ClearArrowDetail;
        arrowRowUI.Refresh(record != null ? record.deck : Array.Empty<MaterialCardSaveData>());
        ClearArrowDetail();
    }

    private void ShowArrowDetail(MaterialModel material)
    {
        if (arrowDetailText == null || material == null)
            return;

        UnifiedDetailContent content = UnifiedDetailContentBuilder.Build(material);
        arrowDetailText.richText = true;
        arrowDetailText.text = InlineIconTextFormatter.Format(content.Body);
    }

    private void ClearArrowDetail()
    {
        if (arrowDetailText != null)
            arrowDetailText.text = selectedRecord != null && selectedRecord.deck != null && selectedRecord.deck.Length > 0 ? "悬停箭头查看详情" : "没有箭头记录";
    }

    private static MagicModel CreateMagicForSlot(MagicSlotSaveData[] magicBook, int slotIndex)
    {
        for (int i = 0; magicBook != null && i < magicBook.Length; i++)
        {
            MagicSlotSaveData slot = magicBook[i];
            if (slot != null && slot.slotIndex == slotIndex)
                return RunSaveSystem.CreateMagic(slot);
        }
        return null;
    }

    private static int GetMaxMagicSlotIndex(MagicSlotSaveData[] magicBook)
    {
        int max = -1;
        for (int i = 0; magicBook != null && i < magicBook.Length; i++)
        {
            if (magicBook[i] != null && magicBook[i].slotIndex > max)
                max = magicBook[i].slotIndex;
        }
        return max;
    }

    private void CacheReferences()
    {
        if (closeButton == null)
            closeButton = UIManager.FindChildComponent<Button>(transform, "CloseButton");
        if (recordListRoot == null)
            recordListRoot = UIManager.FindChildRect(transform, "RecordListContent");
        if (emptyText == null)
            emptyText = FindChildComponentRecursive<TMP_Text>(transform, "EmptyText");
        if (detailText == null)
            detailText = FindChildComponentRecursive<TMP_Text>(transform, "DetailText");
        if (buildListText == null)
            buildListText = FindChildComponentRecursive<TMP_Text>(transform, "BuildListText");
        if (arrowDetailText == null)
            arrowDetailText = FindChildComponentRecursive<TMP_Text>(transform, "ArrowDetailText");
        if (magicRoot == null)
            magicRoot = FindChildRectRecursive(transform, "MagicRoot");
        if (arrowRowUI == null)
            arrowRowUI = GetComponentInChildren<RunHistoryArrowRowUI>(true);
    }

    private void BindButtons()
    {
        if (closeButton == null)
            return;

        closeButton.onClick.RemoveAllListeners();
        closeButton.onClick.AddListener(Hide);
    }

    private void ClearRecordItems()
    {
        for (int i = recordItems.Count - 1; i >= 0; i--)
        {
            if (recordItems[i] != null)
                Destroy(recordItems[i].gameObject);
        }
        recordItems.Clear();
        recordItemRecords.Clear();

        if (recordListRoot == null)
            return;
        for (int i = recordListRoot.childCount - 1; i >= 0; i--)
            Destroy(recordListRoot.GetChild(i).gameObject);
    }

    private void ClearMagicViews()
    {
        for (int i = magicViews.Count - 1; i >= 0; i--)
        {
            if (magicViews[i] != null)
                Destroy(magicViews[i].gameObject);
        }
        magicViews.Clear();

        if (magicRoot == null)
            return;
        for (int i = magicRoot.childCount - 1; i >= 0; i--)
            Destroy(magicRoot.GetChild(i).gameObject);
    }

    private static string BuildDetailText(RunHistoryRecordData record)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine($"日期：{FormatDate(record.endedAtUtc)}");
        builder.AppendLine($"结果：{GetResultText(record.resultType)}");
        builder.AppendLine($"用时：{FormatPlayTime(record.playSeconds)}");
        builder.AppendLine($"进度：{record.progressText}");
        builder.AppendLine($"生命：{record.currentHealth}/{record.maxHealth}");
        builder.AppendLine($"金币：{record.gold}");
        builder.AppendLine($"版本：{record.gameVersion} / 记录v{record.historyVersion}");
        return builder.ToString();
    }

    private static string BuildListText(RunHistoryRecordData record)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine(record.buildSummary);
        builder.Append("道具：");
        int magicCount = 0;
        for (int i = 0; record.magicBook != null && i < record.magicBook.Length; i++)
        {
            MagicModel magic = RunSaveSystem.CreateMagic(record.magicBook[i]);
            if (magic == null)
                continue;
            if (magicCount > 0)
                builder.Append("、");
            builder.Append(magic.Name);
            magicCount++;
        }
        if (magicCount == 0)
            builder.Append("无");
        builder.AppendLine();
        builder.Append("箭头：");
        builder.Append(record.deck != null ? record.deck.Length : 0);
        builder.Append("张");
        return builder.ToString();
    }

    private static string GetResultText(string resultType)
    {
        switch (resultType)
        {
            case "Victory": return "通关";
            case "Defeat": return "失败";
            case "Abandon": return "放弃";
            default: return resultType;
        }
    }

    private static string FormatDate(string utc)
    {
        if (DateTime.TryParse(utc, null, DateTimeStyles.RoundtripKind, out DateTime dateTime))
            return dateTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        return "未知时间";
    }

    private static string FormatPlayTime(float seconds)
    {
        int totalSeconds = Mathf.Max(0, Mathf.FloorToInt(seconds));
        int hours = totalSeconds / 3600;
        int minutes = totalSeconds % 3600 / 60;
        int second = totalSeconds % 60;
        if (hours > 0)
            return $"{hours}小时{minutes}分{second}秒";
        if (minutes > 0)
            return $"{minutes}分{second}秒";
        return $"{second}秒";
    }

    private static RectTransform FindChildRectRecursive(Transform root, string name)
    {
        Transform child = FindChildRecursive(root, name);
        return child as RectTransform;
    }

    private static T FindChildComponentRecursive<T>(Transform root, string name) where T : Component
    {
        Transform child = FindChildRecursive(root, name);
        return child != null ? child.GetComponent<T>() : null;
    }

    private static Transform FindChildRecursive(Transform root, string name)
    {
        if (root == null)
            return null;

        Transform direct = root.Find(name);
        if (direct != null)
            return direct;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildRecursive(root.GetChild(i), name);
            if (found != null)
                return found;
        }
        return null;
    }
}
