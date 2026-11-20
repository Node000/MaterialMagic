using NUnit.Framework;

public class PlatformPolicyTests
{
    private sealed class FakeAdapter : IPlatformPolicyAdapter
    {
        public int Initializations;
        public int LoginRequests;
        public int Discards;
        public bool Collection;
        public void Apply(PlatformPolicyDecision decision)
        {
            if (decision.InitializeBackend) Initializations++;
            if (decision.RequestAutoLogin) LoginRequests++;
            if (decision.DiscardPending) Discards++;
            Collection = decision.CollectionEnabled;
        }
    }
    private static PlatformPolicyOptions Tap() => new PlatformPolicyOptions { Backend = PlatformPolicyBackend.TapTap };

    [Test]
    public void UndecidedPromptsWithoutInitializingAndDeclineDoesNotBlockGame()
    {
        var session = new PlatformPolicySession(Tap(), PlatformPolicyConsent.Undecided);
        PlatformPolicyDecision prompt = session.Start();
        Assert.That(prompt.ShowConsentPrompt, Is.True);
        Assert.That(prompt.InitializeBackend, Is.False);
        var adapter = new FakeAdapter();
        adapter.Apply(session.ChangeConsent(PlatformPolicyConsent.Declined));
        adapter.Apply(session.Start());
        Assert.That(adapter.Initializations, Is.Zero);
        Assert.That(adapter.LoginRequests, Is.Zero);
        Assert.That(adapter.Discards, Is.GreaterThan(0));
        Assert.That(session.InitializationRequested, Is.False);
    }

    [Test]
    public void AgreeInitializesOnceAndReopeningRetriesLoginWithoutLoggingOut()
    {
        var session = new PlatformPolicySession(Tap(), PlatformPolicyConsent.Agreed);
        var adapter = new FakeAdapter();
        adapter.Apply(session.Start());
        adapter.Apply(session.Start());
        adapter.Apply(session.ReportInitialization(true));
        Assert.That(adapter.Initializations, Is.EqualTo(1));
        Assert.That(adapter.LoginRequests, Is.EqualTo(1));
        session.ReportLogin(true);
        adapter.Apply(session.ChangeConsent(PlatformPolicyConsent.Declined));
        Assert.That(session.LoggedIn, Is.True, "Disabling collection is not a logout.");
        Assert.That(adapter.Collection, Is.False);
        Assert.That(session.CanExecuteLogin, Is.False);
        adapter.Apply(session.ChangeConsent(PlatformPolicyConsent.Agreed));
        Assert.That(adapter.Initializations, Is.EqualTo(1));
        Assert.That(adapter.LoginRequests, Is.EqualTo(1), "An existing account does not need another login.");
    }

    [Test]
    public void FailedInitializationCanRetryAndLateConsentChangePreventsDelayedLogin()
    {
        var session = new PlatformPolicySession(Tap(), PlatformPolicyConsent.Agreed);
        session.Start();
        session.ReportInitialization(false);
        Assert.That(session.Start().InitializeBackend, Is.True);
        session.ChangeConsent(PlatformPolicyConsent.Declined);
        Assert.That(session.ReportInitialization(true).RequestAutoLogin, Is.False);
        Assert.That(session.CanExecuteLogin, Is.False);
        Assert.That(session.CollectionEnabled, Is.False);
    }

    [TestCase(PlatformPolicyBackend.None)]
    [TestCase(PlatformPolicyBackend.Steam)]
    public void NonTapBackendRetainsExistingInitializationPolicy(PlatformPolicyBackend backend)
    {
        var options = Tap(); options.Backend = backend;
        Assert.That(PlatformPolicy.CollectionAllowedAtInitialization(options, PlatformPolicyConsent.Declined), Is.True);
        Assert.That(PlatformPolicy.CanAutoLogin(options, PlatformPolicyConsent.Agreed, true, false), Is.False);
    }
}
