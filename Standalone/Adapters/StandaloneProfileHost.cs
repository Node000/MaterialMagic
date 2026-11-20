#if MATERIALMAGIC_STANDALONE
using System;
using Newtonsoft.Json;

/// <summary>
/// Explicit process-level read-only progress snapshot for legacy outer-profile entry points.
/// Unlock/difficulty rules remain linked production code; world profiles take precedence there.
/// No user save file is read, and every persistent mutation fails explicitly.
/// </summary>
public static class StandaloneProfileHost
{
    private static UnlockProgressData progress = new UnlockProgressData();

    /// <summary>Configure before creating worlds; the progress DTO is detached from its caller.</summary>
    public static void Configure(UnlockProgressData snapshot = null)
    {
        progress = snapshot == null ? new UnlockProgressData() : Copy(snapshot);
        RuleProfilePersistence.Configure(ReadProgress, RejectSave);
    }

    public static void Reset() => Configure();
    internal static UnlockProgressData ReadProgress() => Copy(progress);
    private static UnlockProgressData Copy(UnlockProgressData source)
        => JsonConvert.DeserializeObject<UnlockProgressData>(JsonConvert.SerializeObject(source));
    private static void RejectSave(UnlockProgressData data) => throw PersistenceUnsupported();
    internal static NotSupportedException PersistenceUnsupported()
        => new NotSupportedException("Standalone rules do not write unlock/profile progress. Supply an explicit host persistence implementation outside the rules assembly.");
}
#endif
