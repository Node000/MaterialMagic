using System.Globalization;
using System.Text;
using UnityEngine;

public static class InlineIconTextFormatter
{
    private const string ConfigResourcePath = "Config/UnifiedDetailTextConfig";
    private const float DefaultIconScale = 1.5f;
    private const float DefaultIconVerticalOffsetEm = -0.08f;
    private static UnifiedDetailTextConfig cachedTextConfig;

    public static string Format(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return string.Empty;

        StringBuilder builder = null;
        int copyStart = 0;
        int index = 0;
        while (index < raw.Length)
        {
            if (raw[index] != '<')
            {
                index++;
                continue;
            }

            int closeIndex = raw.IndexOf('>', index + 1);
            if (closeIndex <= index + 1)
            {
                index++;
                continue;
            }

            string token = raw.Substring(index + 1, closeIndex - index - 1);
            if (!TryFormatToken(token, out string replacement))
            {
                index = closeIndex + 1;
                continue;
            }

            builder ??= new StringBuilder(raw.Length + 32);
            if (index > copyStart)
                builder.Append(raw, copyStart, index - copyStart);
            builder.Append(replacement);
            index = closeIndex + 1;
            copyStart = index;
        }

        if (builder == null)
            return FormatKeywords(raw);

        if (copyStart < raw.Length)
            builder.Append(raw, copyStart, raw.Length - copyStart);
        return FormatKeywords(builder.ToString());
    }

    private static string FormatKeywords(string raw)
    {
        StringBuilder builder = null;
        int copyStart = 0;
        int index = 0;
        while (index < raw.Length)
        {
            if (raw[index] != '【')
            {
                index++;
                continue;
            }

            int closeIndex = raw.IndexOf('】', index + 1);
            if (closeIndex <= index + 1)
            {
                index++;
                continue;
            }

            builder ??= new StringBuilder(raw.Length + 32);
            if (index > copyStart)
                builder.Append(raw, copyStart, index - copyStart);
            string keyword = raw.Substring(index + 1, closeIndex - index - 1);
            builder.Append("<color=#");
            builder.Append(ColorUtility.ToHtmlStringRGBA(GetKeywordColor()));
            builder.Append(">");
            builder.Append(keyword);
            builder.Append("</color>");
            index = closeIndex + 1;
            copyStart = index;
        }

        if (builder == null)
            return raw;

        if (copyStart < raw.Length)
            builder.Append(raw, copyStart, raw.Length - copyStart);
        return builder.ToString();
    }

    private static bool TryFormatToken(string token, out string replacement)
    {
        replacement = null;
        if (string.IsNullOrEmpty(token))
            return false;

        if (!InlineIconDatabase.TryGet(token, out InlineIconData data) || data == null)
            return false;

        if (!string.IsNullOrEmpty(data.spriteName))
        {
            string spriteTag = data.tint
                ? "<sprite tint=1 name=\"" + data.spriteName + "\">"
                : "<sprite name=\"" + data.spriteName + "\">";
            replacement = "<voffset=" + GetIconVerticalOffsetEmText() + "em><size=" + GetIconSizePercent() + "%>" + spriteTag + "</size></voffset>";
            return true;
        }

        replacement = data.fallbackText ?? string.Empty;
        return true;
    }

    private static int GetIconSizePercent()
    {
        UnifiedDetailTextConfig textConfig = GetTextConfig();
        float scale = textConfig != null ? textConfig.InlineIconScale : DefaultIconScale;
        return Mathf.Max(1, Mathf.RoundToInt(scale * 100f));
    }

    private static string GetIconVerticalOffsetEmText()
    {
        UnifiedDetailTextConfig textConfig = GetTextConfig();
        float offset = textConfig != null ? textConfig.InlineIconVerticalOffsetEm : DefaultIconVerticalOffsetEm;
        return offset.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static Color GetKeywordColor()
    {
        UnifiedDetailTextConfig textConfig = GetTextConfig();
        return textConfig != null ? textConfig.KeywordTextColor : new Color(0.48f, 0.86f, 1f, 1f);
    }

    private static UnifiedDetailTextConfig GetTextConfig()
    {
        if (cachedTextConfig == null)
            cachedTextConfig = Resources.Load<UnifiedDetailTextConfig>(ConfigResourcePath);
        return cachedTextConfig;
    }
}
