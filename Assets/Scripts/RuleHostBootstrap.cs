#if !MATERIALMAGIC_STANDALONE
using UnityEngine;

/// <summary>
/// Production composition root. Feedback belongs to Presentation; profile IO belongs to Adapters;
/// Core only sees registered services, snapshot readers and storage delegates.
/// </summary>
public static class RuleHostBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForPlaySession()
    {
        // These assignments also run when Enter Play Mode keeps the domain alive. Never accumulate
        // callbacks or retain a previous session's/test host's profile bindings.
        PlayerStateServices.ResetUnityHost();
        RuleProfilePolicyHost.Configure(null);
        RuleProfilePersistence.Reset();
        RegisterFactory();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void BeforeSceneLoad() => EnsureConfigured();

#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
    private static void InitializeEditorHost()
    {
        // Register storage/capture delegates too, so outer-profile reads before the first default
        // player retain production behavior. Creating these bindings performs no profile IO.
        RegisterFactory();
        EnsureConfigured();
    }
#endif

    /// <summary>
    /// Call before production profile/default-player entry points, including EditMode integrations.
    /// Explicit hosts already configured by tests/tools are preserved until the next Play reset.
    /// </summary>
    public static void EnsureConfigured()
    {
        if (!PlayerStateServices.HasUnityHostRegistration) RegisterFactory();
        PlayerStateServices.EnsureConfigured();
    }

    private static void RegisterFactory()
    {
        PlayerStateServices.ConfigureUnityHostFactory(CreateProductionServices);
    }

    private static PlayerStateServices CreateProductionServices()
    {
        UnityRuleProfileHost.Configure();
        RunSaveSystem.ConfigureCodexSlotClear(MagicCodexProgressSystem.Clear);
        return UnityRuleHostBindings.CreateServices(
            (health, shield) => AudioManager.Instance?.PlayDamageResultSfx(health, shield),
            () => AudioManager.Instance?.PlaySfx(GameSfxId.GetCoin),
            amount => RunAnalyticsLedger.AddDamageTaken(amount),
            amount => RunAnalyticsLedger.AddDamageDealt(amount),
            data => MagicCodexProgressSystem.MarkMagicDiscovered(data));
    }
}
#endif
