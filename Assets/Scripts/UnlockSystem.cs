using System;
using System.Collections.Generic;

public static class UnlockSystem
{
    public const string TargetStartConfig = "StartConfig";
    public const string TargetMagic = "Magic";
    public const string TargetMaterialModifier = "MaterialModifier";
    public const string TargetMagicModifier = "MagicModifier";

    private const string ConditionNormalEndCount = "NormalEndCount";
    private const string ConditionVictoryCount = "VictoryCount";
    private const string ConditionDefeatCount = "DefeatCount";
    private const string ConditionStartConfigNormalEndCount = "StartConfigNormalEndCount";
    private const string ConditionStartConfigVictoryCount = "StartConfigVictoryCount";
    private const string ConditionStartConfigDefeatCount = "StartConfigDefeatCount";
    private const string ConditionHasMagicAtRunEnd = "HasMagicAtRunEnd";
    private const string ConditionHasMaterialModifierAtRunEnd = "HasMaterialModifierAtRunEnd";
    private const string ConditionHasMagicModifierAtRunEnd = "HasMagicModifierAtRunEnd";
    private const string ConditionGoldAtRunEndAtLeast = "GoldAtRunEndAtLeast";
    private const string ConditionHealthAtRunEndAtLeast = "HealthAtRunEndAtLeast";

    private static List<UnlockData> unlocks;

    public static IReadOnlyList<UnlockData> Unlocks
    {
        get
        {
            EnsureLoaded();
            return unlocks;
        }
    }

    public static bool IsUnlocked(string targetType, string targetId)
    {
        if (string.IsNullOrEmpty(targetType) || string.IsNullOrEmpty(targetId))
            return true;

        EnsureLoaded();
        string key = GetUnlockKey(targetType, targetId);
        if (!HasRuleForKey(key))
            return true;

        UnlockProgressData progress = UnlockProgressSaveSystem.LoadCurrent();
        return Contains(progress.unlockedIds, key);
    }

    public static bool IsStartConfigUnlocked(PlayerStartConfigData data)
    {
        return data == null || IsUnlocked(TargetStartConfig, data.id);
    }

    public static bool IsMagicUnlocked(MagicData data)
    {
        if (data == null)
            return true;

        EnsureLoaded();
        if (!string.IsNullOrEmpty(data.id) && HasRuleForKey(GetUnlockKey(TargetMagic, data.id)))
            return IsUnlocked(TargetMagic, data.id);
        return data.numericId <= 0 || IsUnlocked(TargetMagic, data.numericId.ToString());
    }

    public static bool IsMaterialModifierUnlocked(MaterialModifierData data)
    {
        return data == null || IsUnlocked(TargetMaterialModifier, data.id);
    }

    public static bool IsMagicModifierUnlocked(MagicModifierData data)
    {
        return data == null || IsUnlocked(TargetMagicModifier, data.id);
    }

    public static bool GrantUnlock(string targetType, string targetId, bool queueMessage)
    {
        if (string.IsNullOrEmpty(targetType) || string.IsNullOrEmpty(targetId))
            return false;

        EnsureLoaded();
        UnlockProgressData progress = UnlockProgressSaveSystem.LoadCurrent();
        string key = GetUnlockKey(targetType, targetId);
        if (Contains(progress.unlockedIds, key))
            return false;

        progress.unlockedIds = AddUnique(progress.unlockedIds, key);
        if (queueMessage)
            progress.pendingUnlockMessages = AddPendingMessage(progress.pendingUnlockMessages, FindUnlock(targetType, targetId));
        UnlockProgressSaveSystem.SaveCurrent(progress);
        return true;
    }

    public static void ProcessRunEnded(RunSaveData data, RunHistoryResultType resultType)
    {
        if (data == null || resultType == RunHistoryResultType.Abandon)
            return;

        EnsureLoaded();
        UnlockProgressData progress = UnlockProgressSaveSystem.LoadCurrent();
        if (!string.IsNullOrEmpty(data.runId) && Contains(progress.creditedRunIds, data.runId))
            return;

        progress.normalEndCount++;
        if (resultType == RunHistoryResultType.Victory)
            progress.victoryCount++;
        else if (resultType == RunHistoryResultType.Defeat)
            progress.defeatCount++;

        if (!string.IsNullOrEmpty(data.startConfigId))
        {
            progress.startConfigNormalEndCounts = IncrementCounter(progress.startConfigNormalEndCounts, data.startConfigId);
            if (resultType == RunHistoryResultType.Victory)
                progress.startConfigVictoryCounts = IncrementCounter(progress.startConfigVictoryCounts, data.startConfigId);
            else if (resultType == RunHistoryResultType.Defeat)
                progress.startConfigDefeatCounts = IncrementCounter(progress.startConfigDefeatCounts, data.startConfigId);
        }

        if (!string.IsNullOrEmpty(data.runId))
            progress.creditedRunIds = AddUnique(progress.creditedRunIds, data.runId);

        for (int i = 0; i < unlocks.Count; i++)
        {
            UnlockData unlock = unlocks[i];
            if (unlock == null || string.IsNullOrEmpty(unlock.targetType) || string.IsNullOrEmpty(unlock.targetId))
                continue;

            string key = GetUnlockKey(unlock.targetType, unlock.targetId);
            if (Contains(progress.unlockedIds, key))
                continue;

            if (!AreConditionsMet(unlock, progress, data))
                continue;

            progress.unlockedIds = AddUnique(progress.unlockedIds, key);
            progress.pendingUnlockMessages = AddPendingMessage(progress.pendingUnlockMessages, unlock);
        }

        UnlockProgressSaveSystem.SaveCurrent(progress);
    }

    public static UnlockPendingMessageData[] ConsumePendingUnlockMessages()
    {
        UnlockProgressData progress = UnlockProgressSaveSystem.LoadCurrent();
        UnlockPendingMessageData[] messages = progress.pendingUnlockMessages ?? Array.Empty<UnlockPendingMessageData>();
        if (messages.Length == 0)
            return messages;

        progress.pendingUnlockMessages = Array.Empty<UnlockPendingMessageData>();
        UnlockProgressSaveSystem.SaveCurrent(progress);
        return messages;
    }

    public static string GetUnlockKey(string targetType, string targetId)
    {
        return targetType + ":" + targetId;
    }

    public static string GetTargetTypeName(string targetType)
    {
        switch (targetType)
        {
            case TargetStartConfig:
                return LocalizationSystem.GetText("ui.unlock_popup.type.start_config", "初始卡组");
            case TargetMagic:
                return LocalizationSystem.GetText("ui.unlock_popup.type.magic", "道具");
            case TargetMaterialModifier:
                return LocalizationSystem.GetText("ui.unlock_popup.type.material_modifier", "箭头附魔");
            case TargetMagicModifier:
                return LocalizationSystem.GetText("ui.unlock_popup.type.magic_modifier", "道具强化");
            default:
                return targetType;
        }
    }

    public static string GetTargetName(string targetType, string targetId)
    {
        if (string.IsNullOrEmpty(targetId))
            return string.Empty;

        switch (targetType)
        {
            case TargetStartConfig:
                if (GameDataDatabase.TryGetPlayerStartConfigData(targetId, out PlayerStartConfigData startConfig))
                    return LocalizationSystem.GetText(startConfig.displayNameKey, !string.IsNullOrEmpty(startConfig.displayName) ? startConfig.displayName : targetId);
                break;
            case TargetMagic:
                MagicData magic = FindMagicData(targetId);
                if (magic != null)
                    return LocalizationSystem.GetText(magic.nameKey, magic.id);
                break;
            case TargetMaterialModifier:
                MaterialModifierData materialModifier = FindMaterialModifierData(targetId);
                if (materialModifier != null)
                    return LocalizationSystem.GetText(materialModifier.nameKey, materialModifier.id);
                break;
            case TargetMagicModifier:
                if (GameDataDatabase.TryGetMagicModifierData(targetId, out MagicModifierData magicModifier))
                    return LocalizationSystem.GetText(magicModifier.nameKey, magicModifier.id);
                break;
        }

        return targetId;
    }

    public static UnlockData FindUnlock(string targetType, string targetId)
    {
        EnsureLoaded();
        for (int i = 0; i < unlocks.Count; i++)
        {
            UnlockData unlock = unlocks[i];
            if (unlock != null && unlock.targetType == targetType && unlock.targetId == targetId)
                return unlock;
        }
        return null;
    }

    private static void EnsureLoaded()
    {
        if (unlocks != null)
            return;

        DataTable<UnlockData> table = GameDataReader.LoadTable<UnlockData>("UnlockData");
        unlocks = table != null && table.items != null ? table.items : new List<UnlockData>();
    }

    private static bool HasRuleForKey(string key)
    {
        if (unlocks == null)
            EnsureLoaded();

        for (int i = 0; i < unlocks.Count; i++)
        {
            UnlockData unlock = unlocks[i];
            if (unlock != null && GetUnlockKey(unlock.targetType, unlock.targetId) == key)
                return true;
        }
        return false;
    }

    private static bool AreConditionsMet(UnlockData unlock, UnlockProgressData progress, RunSaveData run)
    {
        if (unlock.conditions == null || unlock.conditions.Length == 0)
            return true;

        for (int i = 0; i < unlock.conditions.Length; i++)
        {
            if (!IsConditionMet(unlock.conditions[i], progress, run))
                return false;
        }
        return true;
    }

    private static bool IsConditionMet(UnlockConditionData condition, UnlockProgressData progress, RunSaveData run)
    {
        if (condition == null || string.IsNullOrEmpty(condition.type))
            return true;

        int required = Math.Max(1, condition.value);
        switch (condition.type)
        {
            case ConditionNormalEndCount:
                return progress.normalEndCount >= required;
            case ConditionVictoryCount:
                return progress.victoryCount >= required;
            case ConditionDefeatCount:
                return progress.defeatCount >= required;
            case ConditionStartConfigNormalEndCount:
                return GetCounter(progress.startConfigNormalEndCounts, condition.targetId) >= required;
            case ConditionStartConfigVictoryCount:
                return GetCounter(progress.startConfigVictoryCounts, condition.targetId) >= required;
            case ConditionStartConfigDefeatCount:
                return GetCounter(progress.startConfigDefeatCounts, condition.targetId) >= required;
            case ConditionHasMagicAtRunEnd:
                return HasMagicAtRunEnd(run, condition.targetId);
            case ConditionHasMaterialModifierAtRunEnd:
                return HasMaterialModifierAtRunEnd(run, condition.targetId);
            case ConditionHasMagicModifierAtRunEnd:
                return HasMagicModifierAtRunEnd(run, condition.targetId);
            case ConditionGoldAtRunEndAtLeast:
                return run != null && run.player != null && run.player.gold >= condition.value;
            case ConditionHealthAtRunEndAtLeast:
                return run != null && run.player != null && run.player.currentHealth >= condition.value;
            default:
                return false;
        }
    }

    private static bool HasMagicAtRunEnd(RunSaveData run, string targetId)
    {
        if (run == null || run.player == null || run.player.magicBook == null || string.IsNullOrEmpty(targetId))
            return false;

        int numericId;
        bool numericTarget = int.TryParse(targetId, out numericId);
        for (int i = 0; i < run.player.magicBook.Length; i++)
        {
            MagicSlotSaveData slot = run.player.magicBook[i];
            if (slot == null || slot.magicNumericId <= 0)
                continue;

            if (numericTarget && slot.magicNumericId == numericId)
                return true;
            if (GameDataDatabase.TryGetMagicData(slot.magicNumericId, out MagicData data) && data != null && data.id == targetId)
                return true;
        }
        return false;
    }

    private static bool HasMaterialModifierAtRunEnd(RunSaveData run, string targetId)
    {
        if (run == null || run.player == null || run.player.deck == null || string.IsNullOrEmpty(targetId))
            return false;

        for (int i = 0; i < run.player.deck.Length; i++)
        {
            if (HasMaterialModifier(run.player.deck[i], targetId))
                return true;
        }
        return false;
    }

    private static bool HasMaterialModifier(MaterialCardSaveData card, string targetId)
    {
        if (card == null)
            return false;

        for (int i = 0; card.modifierIds != null && i < card.modifierIds.Length; i++)
        {
            if (card.modifierIds[i] == targetId)
                return true;
        }

        for (int i = 0; card.linkedCards != null && i < card.linkedCards.Length; i++)
        {
            if (HasMaterialModifier(card.linkedCards[i], targetId))
                return true;
        }
        return false;
    }

    private static bool HasMagicModifierAtRunEnd(RunSaveData run, string targetId)
    {
        if (run == null || run.player == null || run.player.magicBook == null || string.IsNullOrEmpty(targetId))
            return false;

        for (int i = 0; i < run.player.magicBook.Length; i++)
        {
            MagicSlotSaveData slot = run.player.magicBook[i];
            if (slot != null && slot.modifierId == targetId)
                return true;
        }
        return false;
    }

    private static UnlockCounterData[] IncrementCounter(UnlockCounterData[] counters, string id)
    {
        if (string.IsNullOrEmpty(id))
            return counters ?? Array.Empty<UnlockCounterData>();

        List<UnlockCounterData> list = ToCounterList(counters);
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null && list[i].id == id)
            {
                list[i].count++;
                return list.ToArray();
            }
        }

        list.Add(new UnlockCounterData { id = id, count = 1 });
        return list.ToArray();
    }

    private static int GetCounter(UnlockCounterData[] counters, string id)
    {
        if (string.IsNullOrEmpty(id))
            return 0;

        for (int i = 0; counters != null && i < counters.Length; i++)
        {
            UnlockCounterData counter = counters[i];
            if (counter != null && counter.id == id)
                return counter.count;
        }
        return 0;
    }

    private static List<UnlockCounterData> ToCounterList(UnlockCounterData[] counters)
    {
        List<UnlockCounterData> list = new List<UnlockCounterData>();
        for (int i = 0; counters != null && i < counters.Length; i++)
        {
            if (counters[i] != null && !string.IsNullOrEmpty(counters[i].id))
                list.Add(counters[i]);
        }
        return list;
    }

    private static bool Contains(string[] values, string value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        for (int i = 0; values != null && i < values.Length; i++)
        {
            if (values[i] == value)
                return true;
        }
        return false;
    }

    private static string[] AddUnique(string[] values, string value)
    {
        if (Contains(values, value))
            return values ?? Array.Empty<string>();

        List<string> list = new List<string>();
        for (int i = 0; values != null && i < values.Length; i++)
        {
            if (!string.IsNullOrEmpty(values[i]))
                list.Add(values[i]);
        }
        list.Add(value);
        return list.ToArray();
    }

    private static UnlockPendingMessageData[] AddPendingMessage(UnlockPendingMessageData[] messages, UnlockData unlock)
    {
        if (unlock == null)
            return messages ?? Array.Empty<UnlockPendingMessageData>();

        List<UnlockPendingMessageData> list = new List<UnlockPendingMessageData>();
        for (int i = 0; messages != null && i < messages.Length; i++)
        {
            if (messages[i] != null)
                list.Add(messages[i]);
        }
        list.Add(new UnlockPendingMessageData
        {
            targetType = unlock.targetType,
            targetId = unlock.targetId,
            messageKey = unlock.messageKey
        });
        return list.ToArray();
    }

    private static MagicData FindMagicData(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        int numericId;
        if (int.TryParse(id, out numericId) && GameDataDatabase.TryGetMagicData(numericId, out MagicData numericData))
            return numericData;

        foreach (MagicData data in GameDataDatabase.MagicData.Values)
        {
            if (data != null && data.id == id)
                return data;
        }
        return null;
    }

    private static MaterialModifierData FindMaterialModifierData(string id)
    {
        DataTable<MaterialModifierData> table = GameDataReader.LoadTable<MaterialModifierData>("MaterialModifierData");
        for (int i = 0; table != null && table.items != null && i < table.items.Count; i++)
        {
            MaterialModifierData data = table.items[i];
            if (data != null && data.id == id)
                return data;
        }
        return null;
    }
}
