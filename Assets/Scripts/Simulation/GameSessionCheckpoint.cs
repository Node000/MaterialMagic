using System;

/// <summary>
/// A replay checkpoint, not a partially serialized PendingChoice. Carries the initial detached catalog,
/// profile/config inputs and every committed command; restore recreates the same sessions and pauses at
/// the same decision/choice. No RunSaveSystem IO, PlayerPrefs, real profile, audio or codex is invoked.
/// </summary>
public sealed class GameSessionCheckpoint
{
    public int version = 1;
    public int seed;
    public string contentVersion;
    public string contentArchiveJson;
    public RuleProfileSnapshot profile;
    public RunDifficultySaveData difficulty;
    public int initialMaxHealth;
    public int initialGold;
    public GameSessionLog commands;
    public string finalFingerprint;

    public static GameSessionCheckpoint Capture(GameSession session)
    {
        if (session == null) throw new ArgumentNullException(nameof(session));
        return new GameSessionCheckpoint
        {
            seed = session.Log.seed, contentVersion = session.Log.contentVersion,
            contentArchiveJson = RuleContentArchive.Capture(session.Player.Rules.Content).ToJson(),
            profile = session.Player.Rules.Profile.Export(),
            difficulty = Copy(session.Player.Rules.Difficulty), commands = Copy(session.Log),
            initialMaxHealth = session.InitialMaxHealth, initialGold = session.InitialGold,
            finalFingerprint = session.DiagnosticFingerprint()
        };
    }

    /// <summary>
    /// The options factory supplies the same pure initialization/map setup; catalog/profile/difficulty,
    /// seed and services are forcibly replaced with the checkpoint's explicit Simulation inputs.
    /// Factory delegates must consume these options/scoped catalog, not close over a mutable host world.
    /// </summary>
    public GameSession Restore(Func<int, GameSessionOptions> createOptions)
    {
        if (version != 1) throw new NotSupportedException("Unsupported game checkpoint version.");
        if (createOptions == null) throw new ArgumentNullException(nameof(createOptions));
        GameSessionOptions options = createOptions(seed) ?? throw new InvalidOperationException("Missing restore options.");
        options.Seed = seed;
        options.RuleContent = RuleContentArchive.FromJson(contentArchiveJson).CreateProvider();
        options.Profile = RuleProfilePolicy.Restore(Copy(profile));
        options.Difficulty = Copy(difficulty);
        options.Services = PlayerStateServices.Simulation;
        // Older version-1 checkpoints omitted these inputs and retain factory behavior.
        if (initialMaxHealth > 0)
        {
            options.MaxHealth = initialMaxHealth;
            options.StartingGold = initialGold;
        }
        GameSession restored = new GameSession(options);
        if (restored.Log.contentVersion != contentVersion || restored.Log.initialFingerprint != commands.initialFingerprint)
            throw new InvalidOperationException("Checkpoint initialization differs.");
        for (int i = 0; i < commands.entries.Count; i++)
        {
            GameCommandLogEntry expected = commands.entries[i];
            if (restored.DiagnosticFingerprint() != expected.before)
                throw new InvalidOperationException("Checkpoint diverged before command " + i);
            try { restored.Execute(Copy(expected.command)); }
            catch (Exception exception)
            {
                if (exception.GetType().FullName + ": " + exception.Message != expected.error) throw;
            }
            GameCommandLogEntry actual = restored.Log.entries[restored.Log.entries.Count - 1];
            if (actual.accepted != expected.accepted || actual.rejection != expected.rejection || actual.error != expected.error || actual.after != expected.after)
                throw new InvalidOperationException("Checkpoint diverged at command " + i);
        }
        if (restored.DiagnosticFingerprint() != finalFingerprint)
            throw new InvalidOperationException("Checkpoint final state differs.");
        return restored;
    }

    public string ToJson() => RuleJson.Serialize(this, true);
    public static GameSessionCheckpoint FromJson(string json) => RuleJson.Deserialize<GameSessionCheckpoint>(json)
        ?? throw new ArgumentException("A checkpoint is required.", nameof(json));
    private static T Copy<T>(T value) => RuleJson.Clone(value);
}
