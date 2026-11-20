#if UNITY_EDITOR || (DEVELOPMENT_BUILD && MATERIALMAGIC_QA_UNLOCK)
using System;
using System.Collections.Generic;

/// <summary>QA-only grant of profile progress, independent of the active run and its history.</summary>
internal static class DeveloperUnlockService
{
    internal static bool TryUnlockCurrentSlot(int confirmedSlot)
    {
        if (confirmedSlot != RunSaveSystem.CurrentSlotIndex || RunSaveSystem.IsTutorialRunActive())
            return false;

        UnlockProgressData progress = UnlockProgressSaveSystem.Load(confirmedSlot);
        var keys = new List<string>(progress.unlockedIds);
        var knownKeys = new HashSet<string>(keys, StringComparer.Ordinal);
        void AddKey(string type, string id)
        {
            string key = UnlockSystem.GetUnlockKey(type, id);
            if (knownKeys.Add(key))
                keys.Add(key);
        }

        foreach (UnlockData rule in UnlockSystem.Unlocks)
        {
            if (rule != null && !string.IsNullOrEmpty(rule.targetType) && !string.IsNullOrEmpty(rule.targetId))
                AddKey(rule.targetType, rule.targetId);
        }
        AddKey(UnlockSystem.TargetFeature, "ascension");
        int maxLevel = AscensionSystem.MaxAscensionLevel;
        if (keys.Count != progress.unlockedIds.Length || progress.highestAscensionUnlocked != maxLevel)
        {
            progress.unlockedIds = keys.ToArray();
            progress.highestAscensionUnlocked = maxLevel;
            UnlockProgressSaveSystem.Save(confirmedSlot, progress);
        }

        MagicCodexProgressSystem.DiscoverAllForQA(confirmedSlot);
        return true;
    }
}
#endif
