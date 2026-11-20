using System;

/// <summary>
/// Host-owned storage for outer-profile unlock and ascension rules. A world keeps its own detached
/// RuleProfilePolicy; configuring storage never replaces a profile already captured by a world.
/// </summary>
public static class RuleProfilePersistence
{
    private static Func<UnlockProgressData> loadCurrent;
    private static Action<UnlockProgressData> saveCurrent;

    /// <summary>Both delegates are required. Registration performs no IO.</summary>
    public static void Configure(Func<UnlockProgressData> load, Action<UnlockProgressData> save)
    {
        if (load == null) throw new ArgumentNullException(nameof(load));
        if (save == null) throw new ArgumentNullException(nameof(save));
        loadCurrent = load;
        saveCurrent = save;
    }

    /// <summary>Unconfigured hosts read fresh empty progress, never a real user profile.</summary>
    public static UnlockProgressData LoadCurrent()
        => loadCurrent != null ? loadCurrent() ?? new UnlockProgressData() : new UnlockProgressData();

    /// <summary>A missing writer must fail visibly, not pretend a persistent mutation succeeded.</summary>
    public static void SaveCurrent(UnlockProgressData data)
    {
        if (saveCurrent == null)
            throw new NotSupportedException("No profile persistence writer is configured. The outer host must explicitly supply profile storage.");
        saveCurrent(data);
    }

    /// <summary>Restore the explicit empty-read/unsupported-write host boundary.</summary>
    public static void Reset()
    {
        loadCurrent = null;
        saveCurrent = null;
    }
}
