using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

/// <summary>
/// 箭头附魔颜色表（<c>Assets/Resources/Config/Enchant_Color.asset</c>）的美术向 Inspector：
/// 列表表头直接显示「附魔中文名 (id)」，底部可一键刷新中文名注释、补齐缺失附魔、校验配置。
/// 中文名只是注释（给美术对照用），运行逻辑仍只读 modifierId / topColor / baseColor。
/// </summary>
[CustomEditor(typeof(EnchantColorConfig))]
public sealed class EnchantColorConfigEditor : Editor
{
    public const string ConfigAssetPath = "Assets/Resources/Config/Enchant_Color.asset";
    private const string ZhModifierTablePath = "Assets/Resources/Data/Localization/zh-CN_Modifier.json";

    private SerializedProperty entriesProperty;
    private ReorderableList entriesList;

    private void OnEnable()
    {
        entriesProperty = serializedObject.FindProperty("entries");
        entriesList = new ReorderableList(serializedObject, entriesProperty, true, true, true, true)
        {
            drawHeaderCallback = DrawListHeader,
            drawElementCallback = DrawElement,
            elementHeight = (EditorGUIUtility.singleLineHeight + 2f) * 5f + 4f
        };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.HelpBox(
            "箭头附魔图标的两层颜色：一个附魔一条（modifierId 与 Assets/Resources/EnchantConfig/MaterialModifiers/<id>.asset 的 id 一致）。\n" +
            "没有配置的附魔会保持 EnchantIcon.prefab 自身的颜色；颜色全部由美术在这张表里设，代码不写任何颜色。",
            MessageType.Info);
        EditorGUILayout.LabelField($"颜色条目 {entriesProperty.arraySize} 条 / 附魔共 {MaterialModifierDatabase.RuntimeData.Count} 个");
        entriesList.DoLayoutList();
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space(4f);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("刷新附魔中文名"))
                RefreshNames((EnchantColorConfig)target, true);
            if (GUILayout.Button("补齐缺失附魔"))
                AddMissingEntries((EnchantColorConfig)target, true);
            if (GUILayout.Button("校验"))
                Validate((EnchantColorConfig)target, true);
        }
    }

    private void DrawListHeader(Rect rect)
    {
        EditorGUI.LabelField(rect, $"附魔颜色（{entriesProperty.arraySize} 条）");
    }

    private void DrawElement(Rect rect, int index, bool isActive, bool isFocused)
    {
        SerializedProperty entry = entriesProperty.GetArrayElementAtIndex(index);
        float lineHeight = EditorGUIUtility.singleLineHeight;
        rect.y += 2f;
        rect.height = lineHeight;

        // 表头行直接写「中文名（id）」：滚动时一眼能看出这条是哪个附魔
        string id = entry.FindPropertyRelative("modifierId").stringValue;
        string displayName = entry.FindPropertyRelative("displayName").stringValue;
        string header = string.IsNullOrEmpty(displayName)
            ? (string.IsNullOrEmpty(id) ? "(未填 id)" : id)
            : $"{displayName}（{id}）";
        EditorGUI.LabelField(rect, header, EditorStyles.boldLabel);
        rect.y += lineHeight + 2f;

        EditorGUI.PropertyField(rect, entry.FindPropertyRelative("displayName"), new GUIContent("附魔中文名"));
        rect.y += lineHeight + 2f;
        EditorGUI.PropertyField(rect, entry.FindPropertyRelative("modifierId"), new GUIContent("附魔 id"));
        rect.y += lineHeight + 2f;
        EditorGUI.PropertyField(rect, entry.FindPropertyRelative("topColor"), new GUIContent("上层颜色"));
        rect.y += lineHeight + 2f;
        EditorGUI.PropertyField(rect, entry.FindPropertyRelative("baseColor"), new GUIContent("底色层颜色"));
    }

    [MenuItem("Tools/Content/Enchant/刷新附魔颜色表（补齐缺失 + 刷新中文名 + 校验）")]
    private static void RefreshFromMenu()
    {
        EnchantColorConfig config = AssetDatabase.LoadAssetAtPath<EnchantColorConfig>(ConfigAssetPath);
        if (config == null)
        {
            Debug.LogError($"[附魔颜色表] 找不到 {ConfigAssetPath}");
            return;
        }

        AddMissingEntries(config, false);
        RefreshNames(config, false);
        Debug.Log(Validate(config, false));
    }

    /// <summary>按本地化表（简体中文）刷新每条的中文名注释。</summary>
    public static string RefreshNames(EnchantColorConfig config, bool log)
    {
        if (config == null)
            return "[附魔颜色表] 颜色表为空。";

        Dictionary<string, string> chineseNames = LoadChineseModifierNames();
        SerializedObject serializedConfig = new SerializedObject(config);
        SerializedProperty entries = serializedConfig.FindProperty("entries");
        List<string> unknownIds = new List<string>();
        int updated = 0;
        for (int i = 0; i < entries.arraySize; i++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(i);
            string id = entry.FindPropertyRelative("modifierId").stringValue;
            if (!TryResolveName(chineseNames, id, out string displayName))
            {
                unknownIds.Add(string.IsNullOrEmpty(id) ? "(空 id)" : id);
                continue;
            }

            SerializedProperty nameProperty = entry.FindPropertyRelative("displayName");
            if (nameProperty.stringValue != displayName)
            {
                nameProperty.stringValue = displayName;
                updated++;
            }
        }

        serializedConfig.ApplyModifiedPropertiesWithoutUndo();
        Save(config);
        string report = $"[附魔颜色表] 中文名已刷新：更新 {updated} 条，共 {entries.arraySize} 条。"
            + (unknownIds.Count > 0
                ? $" 这些 id 在附魔表里找不到（检查拼写）：{string.Join("、", unknownIds)}"
                : " 所有 id 都能对上附魔表。");
        if (log)
            Debug.Log(report);
        return report;
    }

    /// <summary>把还没进颜色表的附魔补上（两层颜色先用该附魔 SO 的 lineColor 播种，美术再改）。</summary>
    public static string AddMissingEntries(EnchantColorConfig config, bool log)
    {
        if (config == null)
            return "[附魔颜色表] 颜色表为空。";

        Dictionary<string, string> chineseNames = LoadChineseModifierNames();
        SerializedObject serializedConfig = new SerializedObject(config);
        SerializedProperty entries = serializedConfig.FindProperty("entries");
        HashSet<string> existingIds = new HashSet<string>();
        for (int i = 0; i < entries.arraySize; i++)
            existingIds.Add(entries.GetArrayElementAtIndex(i).FindPropertyRelative("modifierId").stringValue);

        List<string> added = new List<string>();
        foreach (MaterialModifierData data in MaterialModifierDatabase.RuntimeData)
        {
            if (data == null || string.IsNullOrEmpty(data.id) || existingIds.Contains(data.id))
                continue;

            int index = entries.arraySize;
            entries.InsertArrayElementAtIndex(index);
            SerializedProperty entry = entries.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("modifierId").stringValue = data.id;
            entry.FindPropertyRelative("displayName").stringValue = ResolveNameOrId(chineseNames, data.id);
            Color seed = Color.white;
            if (!string.IsNullOrEmpty(data.lineColor))
                ColorUtility.TryParseHtmlString(data.lineColor, out seed);
            entry.FindPropertyRelative("topColor").colorValue = seed;
            entry.FindPropertyRelative("baseColor").colorValue = seed;
            existingIds.Add(data.id);
            added.Add($"{ResolveNameOrId(chineseNames, data.id)}({data.id})");
        }

        serializedConfig.ApplyModifiedPropertiesWithoutUndo();
        Save(config);
        string report = added.Count > 0
            ? $"[附魔颜色表] 已补齐 {added.Count} 条（两层颜色先用附魔自身 lineColor 播种）：{string.Join("、", added)}"
            : "[附魔颜色表] 没有缺失的附魔，未做改动。";
        if (log)
            Debug.Log(report);
        return report;
    }

    /// <summary>校验：缺失附魔 / 表里多余的 id / 重复 id。</summary>
    public static string Validate(EnchantColorConfig config, bool log)
    {
        if (config == null)
            return "[附魔颜色表] 颜色表为空。";

        SerializedObject serializedConfig = new SerializedObject(config);
        SerializedProperty entries = serializedConfig.FindProperty("entries");
        HashSet<string> tableIds = new HashSet<string>();
        List<string> duplicates = new List<string>();
        List<string> emptyIds = new List<string>();
        for (int i = 0; i < entries.arraySize; i++)
        {
            string id = entries.GetArrayElementAtIndex(i).FindPropertyRelative("modifierId").stringValue;
            if (string.IsNullOrEmpty(id))
            {
                emptyIds.Add($"第 {i} 条");
                continue;
            }
            if (!tableIds.Add(id))
                duplicates.Add(id);
        }

        HashSet<string> enchantIds = new HashSet<string>();
        List<string> missing = new List<string>();
        foreach (MaterialModifierData data in MaterialModifierDatabase.RuntimeData)
        {
            if (data == null || string.IsNullOrEmpty(data.id))
                continue;
            enchantIds.Add(data.id);
            if (!tableIds.Contains(data.id))
                missing.Add(data.id);
        }

        List<string> extra = new List<string>();
        foreach (string id in tableIds)
        {
            if (!enchantIds.Contains(id))
                extra.Add(id);
        }

        List<string> problems = new List<string>();
        if (missing.Count > 0)
            problems.Add($"缺 {missing.Count} 个附魔：{string.Join("、", missing)}");
        if (extra.Count > 0)
            problems.Add($"表里多 {extra.Count} 个 id（附魔表里没有）：{string.Join("、", extra)}");
        if (duplicates.Count > 0)
            problems.Add($"重复 id：{string.Join("、", duplicates)}");
        if (emptyIds.Count > 0)
            problems.Add($"空 id：{string.Join("、", emptyIds)}");

        string report = problems.Count == 0
            ? $"[附魔颜色表] 校验通过：{entries.arraySize} 条覆盖全部 {enchantIds.Count} 个附魔，无重复。"
            : $"[附魔颜色表] 校验发现问题 → {string.Join("；", problems)}";
        if (log)
            Debug.Log(report);
        return report;
    }

    private static bool TryResolveName(Dictionary<string, string> chineseNames, string modifierId, out string displayName)
    {
        displayName = string.Empty;
        if (string.IsNullOrEmpty(modifierId))
            return false;
        if (!MaterialModifierDatabase.TryGetData(modifierId, out MaterialModifierData data) || data == null)
            return false;

        displayName = ResolveNameOrId(chineseNames, data.id);
        return true;
    }

    private static string ResolveNameOrId(Dictionary<string, string> chineseNames, string modifierId)
    {
        if (MaterialModifierDatabase.TryGetData(modifierId, out MaterialModifierData data) && data != null
            && !string.IsNullOrEmpty(data.nameKey)
            && chineseNames.TryGetValue(data.nameKey, out string name)
            && !string.IsNullOrEmpty(name))
        {
            return name;
        }

        return modifierId;
    }

    /// <summary>直接读简体中文的附魔文本表（不切换当前运行语言）。</summary>
    private static Dictionary<string, string> LoadChineseModifierNames()
    {
        Dictionary<string, string> names = new Dictionary<string, string>();
        TextAsset table = AssetDatabase.LoadAssetAtPath<TextAsset>(ZhModifierTablePath);
        if (table == null)
            return names;

        LocalizationTable parsed = JsonUtility.FromJson<LocalizationTable>(table.text);
        if (parsed?.items == null)
            return names;

        for (int i = 0; i < parsed.items.Count; i++)
        {
            LocalizationEntry item = parsed.items[i];
            if (item == null || string.IsNullOrEmpty(item.key) || names.ContainsKey(item.key))
                continue;
            names[item.key] = item.text;
        }
        return names;
    }

    private static void Save(EnchantColorConfig config)
    {
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();
    }
}
