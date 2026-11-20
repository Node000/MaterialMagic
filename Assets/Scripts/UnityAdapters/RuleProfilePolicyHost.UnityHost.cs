#if !MATERIALMAGIC_STANDALONE
using System.Collections.Generic;

/// <summary>Production profile IO and capture registration. Depends only on Core and other adapters.</summary>
public static class UnityRuleProfileHost
{
    /// <summary>Idempotent delegate replacement; registration itself never reads the profile.</summary>
    public static void Configure()
    {
        RuleProfilePersistence.Configure(UnlockProgressSaveSystem.LoadCurrent, UnlockProgressSaveSystem.SaveCurrent);
        RuleProfilePolicyHost.Configure(CaptureHostSnapshot);
    }

    private static RuleProfilePolicy CaptureHostSnapshot()
    {
        // Read raw host records, not ambient predicates: another world's scope may be active while
        // constructing this world. Copy only the values needed by unlock reads, never retain DTOs.
        IReadOnlyList<UnlockData> unlocks = UnlockSystem.Unlocks;
        var keys = new List<string>(unlocks.Count);
        for (int i = 0; i < unlocks.Count; i++)
        {
            UnlockData unlock = unlocks[i];
            if (unlock != null) keys.Add(UnlockSystem.GetUnlockKey(unlock.targetType, unlock.targetId));
        }
        UnlockProgressData progress = UnlockProgressSaveSystem.LoadCurrent();
        return RuleProfilePolicy.Create(keys, progress != null ? progress.unlockedIds : null);
    }
}
#endif
