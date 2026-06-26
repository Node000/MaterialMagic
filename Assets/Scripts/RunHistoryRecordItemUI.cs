using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RunHistoryRecordItemUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text text;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Color normalColor = new Color(0.08f, 0.08f, 0.12f, 0.94f);
    [SerializeField] private Color selectedColor = new Color(0.34f, 0.22f, 0.46f, 1f);

    private RunHistoryRecordData record;
    private Action<RunHistoryRecordData> clicked;

    public void Bind(RunHistoryRecordData record, bool selected, Action<RunHistoryRecordData> clicked)
    {
        this.record = record;
        this.clicked = clicked;
        CacheReferences();
        if (text != null)
            text.text = BuildLabel(record);
        SetSelected(selected);
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClicked);
        }
    }

    public void SetSelected(bool selected)
    {
        CacheReferences();
        if (backgroundImage != null)
            backgroundImage.color = selected ? selectedColor : normalColor;
    }

    private void CacheReferences()
    {
        if (button == null)
            button = GetComponent<Button>();
        if (text == null)
            text = GetComponentInChildren<TMP_Text>(true);
        if (backgroundImage == null)
            backgroundImage = GetComponent<Image>();
    }

    private void OnClicked()
    {
        clicked?.Invoke(record);
    }

    private static string BuildLabel(RunHistoryRecordData record)
    {
        if (record == null)
            return string.Empty;

        return $"{FormatDate(record.endedAtUtc)}  {GetResultText(record.resultType)}\n{FormatPlayTime(record.playSeconds)}  {record.progressText}";
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
        if (DateTime.TryParse(utc, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime dateTime))
            return dateTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
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
}
