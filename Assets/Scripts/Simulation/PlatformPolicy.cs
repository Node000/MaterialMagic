using System;

// Deliberately do not reference GameInitializer, PrivacyConsentGate, PlayerPrefs or any SDK type.
public enum PlatformPolicyBackend { None, TapTap, Steam }
public enum PlatformPolicyConsent { Undecided, Agreed, Declined }

public sealed class PlatformPolicyOptions
{
    public PlatformPolicyBackend Backend { get; set; }
    public bool RequireConsent { get; set; } = true;
    public bool EnableAnalytics { get; set; } = true;
    public bool AutoLogin { get; set; } = true;
}

/// <summary>Value-only policy decision. Execution, persistence, SDK callbacks and delays are adapters.</summary>
public sealed class PlatformPolicyDecision
{
    public bool ShowConsentPrompt { get; internal set; }
    public bool InitializeBackend { get; internal set; }
    public bool CollectionEnabled { get; internal set; }
    public bool AnalyticsEnabled { get; internal set; }
    public bool DiscardPending { get; internal set; }
    public bool RequestAutoLogin { get; internal set; }
    public bool AllowAutoLoginRetry { get; internal set; }
    public bool AttachAnalyticsSink { get; internal set; }
}

/// <summary>Pure shared decisions for startup/settings. Declining never prevents gameplay.</summary>
public static class PlatformPolicy
{
    public static bool NeedsConsentPrompt(PlatformPolicyOptions options, PlatformPolicyConsent consent)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        return options.Backend != PlatformPolicyBackend.None && options.RequireConsent && consent == PlatformPolicyConsent.Undecided;
    }

    public static bool CollectionAllowedAtInitialization(PlatformPolicyOptions options, PlatformPolicyConsent consent)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        return options.Backend != PlatformPolicyBackend.TapTap || !options.RequireConsent || consent == PlatformPolicyConsent.Agreed;
    }

    public static bool CanAutoLogin(PlatformPolicyOptions options, PlatformPolicyConsent consent, bool initialized, bool loggedIn)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        // Existing delayed TapTap login rechecks actual consent even when the init gate is disabled.
        return options.Backend == PlatformPolicyBackend.TapTap && options.AutoLogin && initialized && !loggedIn
            && consent == PlatformPolicyConsent.Agreed;
    }
}

/// <summary>
/// SDK-free startup/settings state machine. Disabling stops our collection and discards pending data,
/// but intentionally does not log out or promise to disable native preconfigured events at runtime.
/// Report asynchronous initialization/login outcomes explicitly; failed init permits retry.
/// </summary>
public sealed class PlatformPolicySession
{
    private readonly PlatformPolicyOptions options;
    public PlatformPolicyConsent Consent { get; private set; }
    public bool InitializationRequested { get; private set; }
    public bool Initialized { get; private set; }
    public bool CollectionEnabledAtInitialization { get; private set; }
    public bool CollectionEnabled { get; private set; }
    public bool LoggedIn { get; private set; }
    public bool AutoLoginAttempted { get; private set; }

    public PlatformPolicySession(PlatformPolicyOptions options, PlatformPolicyConsent consent)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        this.options = new PlatformPolicyOptions
        {
            Backend = options.Backend, RequireConsent = options.RequireConsent,
            EnableAnalytics = options.EnableAnalytics, AutoLogin = options.AutoLogin
        };
        Consent = consent;
    }

    public PlatformPolicyDecision Start()
    {
        if (PlatformPolicy.NeedsConsentPrompt(options, Consent))
            return Decision(showPrompt: true);
        return StartBackend();
    }

    public PlatformPolicyDecision ChangeConsent(PlatformPolicyConsent consent)
    {
        Consent = consent;
        bool agreed = consent == PlatformPolicyConsent.Agreed;
        if (agreed) AutoLoginAttempted = false;
        if (agreed && !Initialized)
        {
            PlatformPolicyDecision start = StartBackend();
            start.AllowAutoLoginRetry = true;
            return start;
        }
        CollectionEnabled = agreed;
        PlatformPolicyDecision result = Decision();
        result.DiscardPending = !agreed;
        result.AllowAutoLoginRetry = agreed;
        result.RequestAutoLogin = TryRequestAutoLogin();
        return result;
    }

    public PlatformPolicyDecision ReportInitialization(bool succeeded)
    {
        Initialized = succeeded;
        InitializationRequested = succeeded;
        PlatformPolicyDecision result = Decision();
        // A consent change while init was in flight must not re-enable our collection/login.
        result.AttachAnalyticsSink = succeeded && options.EnableAnalytics && CollectionEnabledAtInitialization;
        result.RequestAutoLogin = succeeded && TryRequestAutoLogin();
        return result;
    }

    public void ReportLogin(bool loggedIn) => LoggedIn = loggedIn;

    /// <summary>Recheck immediately before executing delayed SDK login, not only when scheduling it.</summary>
    public bool CanExecuteLogin => PlatformPolicy.CanAutoLogin(options, Consent, Initialized, LoggedIn);

    private PlatformPolicyDecision StartBackend()
    {
        if (InitializationRequested || Initialized) return Decision();
        CollectionEnabled = PlatformPolicy.CollectionAllowedAtInitialization(options, Consent);
        CollectionEnabledAtInitialization = CollectionEnabled;
        PlatformPolicyDecision result = Decision();
        result.DiscardPending = Consent != PlatformPolicyConsent.Agreed;
        if (!CollectionEnabled) return result;
        InitializationRequested = true;
        result.InitializeBackend = true;
        return result;
    }

    private bool TryRequestAutoLogin()
    {
        if (AutoLoginAttempted || !CanExecuteLogin) return false;
        AutoLoginAttempted = true;
        return true;
    }

    private PlatformPolicyDecision Decision(bool showPrompt = false) => new PlatformPolicyDecision
    {
        ShowConsentPrompt = showPrompt,
        CollectionEnabled = CollectionEnabled,
        AnalyticsEnabled = options.EnableAnalytics && CollectionEnabled
    };
}

/// <summary>Implement in a real SDK bridge or an in-memory fake; no SDK initialization in policy.</summary>
public interface IPlatformPolicyAdapter
{
    void Apply(PlatformPolicyDecision decision);
}
