using System;

/// <summary>
/// Captures one detached profile per new world. Host adapters supply the outer snapshot; without
/// an adapter (standalone by default) the policy is explicitly empty. Never retain capture over await.
/// </summary>
public static class RuleProfilePolicyHost
{
    [ThreadStatic] private static bool capturing;
    private static Func<RuleProfilePolicy> snapshotProvider;
    internal static bool IsCapturing => capturing;

    /// <summary>Register a synchronous outer-profile reader; null restores the explicit empty default.</summary>
    public static void Configure(Func<RuleProfilePolicy> provider)
    {
        snapshotProvider = provider;
    }

    public static RuleProfilePolicy CaptureForNewWorld()
    {
        if (capturing) return RuleProfilePolicy.Empty;
        capturing = true;
        try
        {
            RuleProfilePolicy snapshot = snapshotProvider != null ? snapshotProvider() : RuleProfilePolicy.Empty;
            return snapshot != null ? RuleProfilePolicy.Restore(snapshot.Export()) : RuleProfilePolicy.Empty;
        }
        finally { capturing = false; }
    }

}
