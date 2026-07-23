using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class EnemyContentImportValidator
{
    private const string DatabaseAssetPath = "Assets/Resources/Config/GameContentDatabase.asset";
    private const string DefinitionsFolder = "Assets/Resources/Config/Enemies";
    private const string EnemyJsonFolder = "Resources/Data/Enemies";
    private const string LocalizationFolder = "Resources/Data/Localization";

    [MenuItem("Tools/Content/Enemies/Import JSON to ScriptableObjects")]
    private static void ImportFromMenu()
    {
        string report = ImportJsonToDefinitions();
        if (report.StartsWith("Enemy import failed", StringComparison.Ordinal))
            Debug.LogError(report);
        else
            Debug.Log(report);
    }

    [MenuItem("Tools/Content/Enemies/Validate ScriptableObjects")]
    private static void ValidateFromMenu()
    {
        string report = ValidateEnemyContent();
        if (report.StartsWith("Enemy content validation failed", StringComparison.Ordinal))
            Debug.LogError(report);
        else
            Debug.Log(report);
    }

    public static string ImportJsonToDefinitions()
    {
        List<string> errors = new List<string>();
        List<EnemySource> sources = LoadEnemySources(errors);
        if (errors.Count > 0)
            return BuildReport("Enemy import failed", errors);

        EnsureFolder(DefinitionsFolder);
        sources.Sort((left, right) => left.data.numericId.CompareTo(right.data.numericId));
        List<EnemyDefinition> definitions = new List<EnemyDefinition>(sources.Count);
        for (int i = 0; i < sources.Count; i++)
        {
            EnemySource source = sources[i];
            string assetPath = DefinitionsFolder + "/Enemy_" + source.data.numericId.ToString("D3") + ".asset";
            EnemyDefinition definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(assetPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<EnemyDefinition>();
                AssetDatabase.CreateAsset(definition, assetPath);
            }

            definition.SetData(source.data);
            EditorUtility.SetDirty(definition);
            definitions.Add(definition);
        }

        GameContentDatabase database = AssetDatabase.LoadAssetAtPath<GameContentDatabase>(DatabaseAssetPath);
        if (database == null)
        {
            database = ScriptableObject.CreateInstance<GameContentDatabase>();
            AssetDatabase.CreateAsset(database, DatabaseAssetPath);
        }

        database.ReplaceEnemyDefinitions(definitions);
        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();

        string validationReport = ValidateEnemyContent();
        return validationReport.StartsWith("Enemy content validation failed", StringComparison.Ordinal)
            ? validationReport
            : $"Imported {definitions.Count} enemy definitions. {validationReport}";
    }

    public static string ValidateEnemyContent()
    {
        GameContentDatabase database = AssetDatabase.LoadAssetAtPath<GameContentDatabase>(DatabaseAssetPath);
        if (database == null)
            return "Enemy content validation failed:\n- Missing " + DatabaseAssetPath;

        List<string> errors = new List<string>();
        HashSet<int> numericIds = new HashSet<int>();
        HashSet<string> stringIds = new HashSet<string>();
        HashSet<string> chineseKeys = LoadLocalizationKeys("zh-CN", errors);
        HashSet<string> englishKeys = LoadLocalizationKeys("en-US", errors);
        IReadOnlyList<EnemyDefinition> definitions = database.Enemies;

        for (int i = 0; i < definitions.Count; i++)
        {
            EnemyDefinition definition = definitions[i];
            string location = $"Database entry {i}";
            if (definition == null)
            {
                errors.Add(location + " is empty.");
                continue;
            }

            EnemyData data = definition.CreateRuntimeData();
            if (data == null)
            {
                errors.Add(definition.name + " has no data.");
                continue;
            }

            ValidateIdentity(data, definition.name, numericIds, stringIds, errors);
            ValidatePresentation(data, definition.name, chineseKeys, englishKeys, errors);
            ValidateEnemyData(data, definition.name, errors);
        }

        for (int i = 0; i < definitions.Count; i++)
        {
            EnemyDefinition definition = definitions[i];
            EnemyData data = definition != null ? definition.CreateRuntimeData() : null;
            if (data != null)
                ValidateSummonReferences(data, definition.name, numericIds, errors);
        }

        return errors.Count == 0
            ? $"Enemy content valid: {definitions.Count} definitions."
            : BuildReport("Enemy content validation failed", errors);
    }

    private static List<EnemySource> LoadEnemySources(List<string> errors)
    {
        string folder = Path.Combine(Application.dataPath, EnemyJsonFolder);
        if (!Directory.Exists(folder))
        {
            errors.Add("Missing JSON folder " + folder + ".");
            return new List<EnemySource>();
        }

        string[] paths = Directory.GetFiles(folder, "*.json", SearchOption.TopDirectoryOnly);
        Array.Sort(paths, StringComparer.Ordinal);
        HashSet<int> ids = new HashSet<int>();
        List<EnemySource> sources = new List<EnemySource>(paths.Length);
        for (int i = 0; i < paths.Length; i++)
        {
            string path = paths[i];
            string text;
            try
            {
                text = File.ReadAllText(path);
            }
            catch (Exception exception)
            {
                errors.Add(Path.GetFileName(path) + " could not be read: " + exception.Message);
                continue;
            }

            if (!LooksLikeJsonObject(text))
            {
                errors.Add(Path.GetFileName(path) + " is not a JSON object.");
                continue;
            }

            EnemyData data;
            try
            {
                data = JsonUtility.FromJson<EnemyData>(text);
            }
            catch (Exception exception)
            {
                errors.Add(Path.GetFileName(path) + " could not be parsed: " + exception.Message);
                continue;
            }

            if (data == null || data.numericId <= 0)
            {
                errors.Add(Path.GetFileName(path) + " has no positive numericId.");
                continue;
            }
            if (!ids.Add(data.numericId))
            {
                errors.Add(Path.GetFileName(path) + " duplicates numericId " + data.numericId + ".");
                continue;
            }

            sources.Add(new EnemySource { data = data });
        }

        return sources;
    }

    private static void ValidateIdentity(EnemyData data, string name, HashSet<int> numericIds, HashSet<string> stringIds, List<string> errors)
    {
        if (data.numericId <= 0)
            errors.Add(name + " has no positive numericId.");
        else if (!numericIds.Add(data.numericId))
            errors.Add(name + " duplicates numericId " + data.numericId + ".");

        if (string.IsNullOrEmpty(data.Id))
            errors.Add(name + " has no string id.");
        else if (!stringIds.Add(data.Id))
            errors.Add(name + " duplicates string id " + data.Id + ".");
    }

    private static void ValidatePresentation(EnemyData data, string name, HashSet<string> chineseKeys, HashSet<string> englishKeys, List<string> errors)
    {
        if (string.IsNullOrEmpty(data.nameKey))
            errors.Add(name + " has no nameKey.");
        else
        {
            if (!chineseKeys.Contains(data.nameKey))
                errors.Add(name + " nameKey is missing from zh-CN: " + data.nameKey + ".");
            if (!englishKeys.Contains(data.nameKey))
                errors.Add(name + " nameKey is missing from en-US: " + data.nameKey + ".");
        }

        if (string.IsNullOrEmpty(data.iconName))
            errors.Add(name + " has no iconName.");
        else if (!ResourceAssetExists(NormalizeResourcePath("Images/Enemies/", data.iconName), ".png"))
            errors.Add(name + " icon is missing: " + data.iconName + ".");

        if (!string.IsNullOrEmpty(data.spriteAnimationPath)
            && !ResourceAssetExists(NormalizeResourcePath("Animations/Enemies/", data.spriteAnimationPath), ".controller")
            && !ResourceAssetExists(NormalizeResourcePath("Animations/Enemies/", data.iconName), ".controller"))
            errors.Add(name + " animator is missing: " + data.spriteAnimationPath + ".");
    }

    private static void ValidateEnemyData(EnemyData data, string name, List<string> errors)
    {
        if (data.maxHealth <= 0)
            errors.Add(name + " maxHealth must be positive.");

        ValidateBuffs(data.initialBuffs, name + " initialBuffs", errors);
        ValidatePlan(data.intentGroups, data.intentLoop, data.actionLoop, name + " root", errors);
        for (int i = 0; data.phases != null && i < data.phases.Length; i++)
        {
            EnemyPhaseData phase = data.phases[i];
            if (phase == null)
            {
                errors.Add(name + " phase " + i + " is empty.");
                continue;
            }
            ValidatePlan(phase.intentGroups, phase.intentLoop, null, name + " phase " + phase.phase, errors);
            ValidateIntentGroups(phase.intentPool, name + " phase " + phase.phase + " pool", errors);
        }
    }

    private static void ValidatePlan(EnemyIntentGroupData[] groups, EnemyIntentLoopData[] loop, EnemyActionData[] actions, string name, List<string> errors)
    {
        HashSet<int> groupIds = ValidateIntentGroups(groups, name, errors);
        for (int i = 0; loop != null && i < loop.Length; i++)
        {
            EnemyIntentLoopData entry = loop[i];
            if (entry == null)
            {
                errors.Add(name + " intent loop " + i + " is empty.");
                continue;
            }

            bool hasGroup = entry.groupId > 0;
            bool hasRandomGroups = entry.randomGroupIds != null && entry.randomGroupIds.Length > 0;
            if (hasGroup == hasRandomGroups)
                errors.Add(name + " intent loop " + i + " must reference exactly one group mode.");
            if (hasGroup && !groupIds.Contains(entry.groupId))
                errors.Add(name + " intent loop " + i + " references missing group " + entry.groupId + ".");
            for (int randomIndex = 0; entry.randomGroupIds != null && randomIndex < entry.randomGroupIds.Length; randomIndex++)
            {
                if (!groupIds.Contains(entry.randomGroupIds[randomIndex]))
                    errors.Add(name + " intent loop " + i + " references missing random group " + entry.randomGroupIds[randomIndex] + ".");
            }
        }

        for (int i = 0; actions != null && i < actions.Length; i++)
            ValidateAction(actions[i], name + " action " + i, errors);
    }

    private static HashSet<int> ValidateIntentGroups(EnemyIntentGroupData[] groups, string name, List<string> errors)
    {
        HashSet<int> groupIds = new HashSet<int>();
        for (int i = 0; groups != null && i < groups.Length; i++)
        {
            EnemyIntentGroupData group = groups[i];
            if (group == null)
            {
                errors.Add(name + " group " + i + " is empty.");
                continue;
            }
            if (group.id <= 0)
                errors.Add(name + " group " + i + " has no positive id.");
            else if (!groupIds.Add(group.id))
                errors.Add(name + " duplicates group id " + group.id + ".");

            for (int intentIndex = 0; group.intents != null && intentIndex < group.intents.Length; intentIndex++)
                ValidateIntent(group.intents[intentIndex], name + " group " + group.id + " intent " + intentIndex, errors);
        }
        return groupIds;
    }

    private static void ValidateIntent(EnemyIntentData intent, string name, List<string> errors)
    {
        if (intent == null)
        {
            errors.Add(name + " is empty.");
            return;
        }
        if (!Enum.IsDefined(typeof(EnemyActionType), intent.actionType) || intent.actionType == EnemyActionType.None)
            errors.Add(name + " has an invalid actionType.");
        if (intent.times < 0)
            errors.Add(name + " times cannot be negative.");
        if (intent.summonCount < 0)
            errors.Add(name + " summonCount cannot be negative.");
        ValidateBuffs(intent.buffs, name + " buffs", errors);
    }

    private static void ValidateAction(EnemyActionData action, string name, List<string> errors)
    {
        if (action == null)
        {
            errors.Add(name + " is empty.");
            return;
        }
        if (!Enum.IsDefined(typeof(EnemyActionType), action.actionType) || action.actionType == EnemyActionType.None)
            errors.Add(name + " has an invalid actionType.");
        if (action.summonCount < 0)
            errors.Add(name + " summonCount cannot be negative.");
        ValidateBuffs(action.buffs, name + " buffs", errors);
    }

    private static void ValidateBuffs(BuffStackData[] buffs, string name, List<string> errors)
    {
        for (int i = 0; buffs != null && i < buffs.Length; i++)
        {
            BuffStackData buff = buffs[i];
            if (buff == null)
            {
                errors.Add(name + " " + i + " is empty.");
                continue;
            }
            if (!Enum.IsDefined(typeof(BuffEnum), buff.buffType) || buff.buffType == BuffEnum.None)
                errors.Add(name + " " + i + " has an invalid buffType.");
            if (buff.stack <= 0)
                errors.Add(name + " " + i + " stack must be positive.");
        }
    }

    private static void ValidateSummonReferences(EnemyData data, string name, HashSet<int> knownIds, List<string> errors)
    {
        ValidateSummonReferences(data.intentGroups, data.actionLoop, name + " root", knownIds, errors);
        for (int i = 0; data.phases != null && i < data.phases.Length; i++)
        {
            EnemyPhaseData phase = data.phases[i];
            if (phase != null)
            {
                ValidateSummonReferences(phase.intentGroups, null, name + " phase " + phase.phase, knownIds, errors);
                ValidateSummonReferences(phase.intentPool, null, name + " phase " + phase.phase + " pool", knownIds, errors);
            }
        }
    }

    private static void ValidateSummonReferences(EnemyIntentGroupData[] groups, EnemyActionData[] actions, string name, HashSet<int> knownIds, List<string> errors)
    {
        for (int groupIndex = 0; groups != null && groupIndex < groups.Length; groupIndex++)
        {
            EnemyIntentGroupData group = groups[groupIndex];
            for (int intentIndex = 0; group != null && group.intents != null && intentIndex < group.intents.Length; intentIndex++)
            {
                EnemyIntentData intent = group.intents[intentIndex];
                if (intent != null && intent.actionType == EnemyActionType.Summon)
                    ValidateSummonTarget(intent.summonEnemyId, name + " group " + group.id + " intent " + intentIndex, knownIds, errors);
            }
        }

        for (int actionIndex = 0; actions != null && actionIndex < actions.Length; actionIndex++)
        {
            EnemyActionData action = actions[actionIndex];
            if (action != null && action.actionType == EnemyActionType.Summon)
                ValidateSummonTarget(action.summonEnemyId, name + " action " + actionIndex, knownIds, errors);
        }
    }

    private static void ValidateSummonTarget(int targetId, string name, HashSet<int> knownIds, List<string> errors)
    {
        if (targetId <= 0)
            errors.Add(name + " has no summonEnemyId.");
        else if (!knownIds.Contains(targetId))
            errors.Add(name + " references missing summoned enemy " + targetId + ".");
    }

    private static HashSet<string> LoadLocalizationKeys(string languageCode, List<string> errors)
    {
        HashSet<string> keys = new HashSet<string>();
        string folder = Path.Combine(Application.dataPath, LocalizationFolder);
        string[] paths = Directory.Exists(folder) ? Directory.GetFiles(folder, languageCode + "*.json", SearchOption.TopDirectoryOnly) : Array.Empty<string>();
        for (int i = 0; i < paths.Length; i++)
        {
            try
            {
                LocalizationTable table = JsonUtility.FromJson<LocalizationTable>(File.ReadAllText(paths[i]));
                for (int entryIndex = 0; table != null && table.items != null && entryIndex < table.items.Count; entryIndex++)
                {
                    LocalizationEntry entry = table.items[entryIndex];
                    if (entry != null && !string.IsNullOrEmpty(entry.key))
                        keys.Add(entry.key);
                }
            }
            catch (Exception exception)
            {
                errors.Add(Path.GetFileName(paths[i]) + " could not be parsed: " + exception.Message);
            }
        }
        return keys;
    }

    private static bool ResourceAssetExists(string resourcePath, string extension)
    {
        return AssetDatabase.LoadMainAssetAtPath("Assets/Resources/" + resourcePath + extension) != null;
    }

    private static string NormalizeResourcePath(string root, string pathOrName)
    {
        return pathOrName.StartsWith(root, StringComparison.Ordinal) ? pathOrName : root + pathOrName;
    }

    private static bool LooksLikeJsonObject(string text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            if (!char.IsWhiteSpace(text[i]))
                return text[i] == '{';
        }
        return false;
    }

    private static void EnsureFolder(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
        if (string.IsNullOrEmpty(parent))
            throw new InvalidOperationException("Cannot create asset folder " + folderPath + ".");

        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folderPath));
    }

    private static string BuildReport(string title, List<string> errors)
    {
        return title + ":\n- " + string.Join("\n- ", errors);
    }

    private struct EnemySource
    {
        public EnemyData data;
    }
}
