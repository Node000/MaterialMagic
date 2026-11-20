using System;

/// <summary>
/// Host effects of a rule operation. A simulation can use the same models without
/// writing the user's codex, playing scene audio, or changing the live analytics ledger.
/// Rule state and relic hooks are deliberately unaffected by this boundary.
/// </summary>
public sealed class PlayerStateServices
{
    public static PlayerStateServices Simulation { get; } = new PlayerStateServices();

    private static PlayerStateServices unityHost;
    private static Func<PlayerStateServices> unityHostFactory;
    private static bool configuringUnityHost;

    /// <summary>
    /// Legacy production default. The outer host must register its services or a lazy factory before
    /// constructing a default player. Standalone without a host retains the exact Simulation singleton.
    /// </summary>
    public static PlayerStateServices UnityHost
    {
        get
        {
            EnsureConfigured();
            return unityHost;
        }
    }

    public static bool IsUnityHostConfigured => unityHost != null;
    public static bool HasUnityHostRegistration => unityHost != null || unityHostFactory != null;

    /// <summary>Explicit host injection. This never replaces or changes Simulation.</summary>
    public static void ConfigureUnityHost(PlayerStateServices services)
    {
        unityHost = services ?? throw new ArgumentNullException(nameof(services));
        unityHostFactory = null;
    }

    /// <summary>Register once at host startup. The factory must not create a default PlayerState.</summary>
    public static void ConfigureUnityHostFactory(Func<PlayerStateServices> factory)
    {
        unityHostFactory = factory ?? throw new ArgumentNullException(nameof(factory));
        unityHost = null;
    }

    /// <summary>Resolve the registered host synchronously before a default player captures its profile.</summary>
    public static void EnsureConfigured()
    {
        if (unityHost != null) return;
        if (configuringUnityHost)
            throw new InvalidOperationException("The player host factory must not recursively construct a default player.");

        if (unityHostFactory == null)
        {
#if MATERIALMAGIC_STANDALONE
            unityHost = Simulation;
            return;
#else
            throw new InvalidOperationException("Player host services are not configured. The production host must register and ensure its rule bindings before constructing a default player.");
#endif
        }

        configuringUnityHost = true;
        try
        {
            PlayerStateServices services = unityHostFactory();
            if (services == null)
                throw new InvalidOperationException("The player host factory returned no services.");
            unityHost = services;
        }
        finally { configuringUnityHost = false; }
    }

    /// <summary>Host lifecycle reset, including Play Mode re-entry with domain reload disabled.</summary>
    public static void ResetUnityHost()
    {
        unityHost = null;
        unityHostFactory = null;
        configuringUnityHost = false;
    }

    private readonly Action<int, int> damageFeedback;
    private readonly Action goldFeedback;
    private readonly Action<int> damageTaken;
    private readonly Action<int> damageDealt;
    private readonly Action<MagicData> magicDiscovered;

    public PlayerStateServices(Action<int, int> damageFeedback = null, Action goldFeedback = null,
        Action<int> damageTaken = null, Action<int> damageDealt = null, Action<MagicData> magicDiscovered = null)
    {
        this.damageFeedback = damageFeedback;
        this.goldFeedback = goldFeedback;
        this.damageTaken = damageTaken;
        this.damageDealt = damageDealt;
        this.magicDiscovered = magicDiscovered;
    }

    public void OnDamage(int health, int shield)
    {
        if (damageFeedback == null) return;
        Action cue = () => damageFeedback(health, shield);
        if (!RuleExecutionScope.DeferFeedback(cue)) cue();
    }
    public void OnGoldGained()
    {
        if (goldFeedback != null && !RuleExecutionScope.DeferFeedback(goldFeedback)) goldFeedback();
    }
    public void OnDamageTaken(int amount) { if (amount > 0) damageTaken?.Invoke(amount); }
    public void OnDamageDealt(int amount) { if (amount > 0) damageDealt?.Invoke(amount); }
    public void OnMagicDiscovered(MagicData data) => magicDiscovered?.Invoke(data);
}
