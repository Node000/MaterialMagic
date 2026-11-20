using System;
using System.Collections.Generic;

/// <summary>Serializable command identity; no mutable model references or privileged hidden query.</summary>
[Serializable]
public sealed class AICommandData
{
    public BattleCommandKind kind;
    public string cardId;
    public string enemyId;
    public int targetIndex;
    public int sourceIndex;
    public int slotCount;
    public bool preserveEmptySlots;
    public bool allowDisabled;
    public bool ignoreRefreshLimit;

    internal static AICommandData Capture(BattleCommand command, AIEntityIds ids) => new AICommandData
    {
        kind = command.Kind, cardId = ids.Card(command.Card), enemyId = ids.Enemy(command.TargetEnemy),
        targetIndex = command.TargetIndex, sourceIndex = command.SourceIndex, slotCount = command.SlotCount,
        allowDisabled = command.AllowDisabled, ignoreRefreshLimit = command.IgnoreRefreshLimit, preserveEmptySlots = command.PreserveEmptySlots
    };

    internal BattleCommand Bind(AIEntityIds ids)
    {
        switch (kind)
        {
            case BattleCommandKind.Play: return BattleCommand.Play(ids.FindCard(cardId), targetIndex, allowDisabled);
            case BattleCommandKind.Withdraw: return BattleCommand.Withdraw(ids.FindCard(cardId), targetIndex);
            case BattleCommandKind.ReorderHand: return BattleCommand.ReorderHand(ids.FindCard(cardId), targetIndex);
            case BattleCommandKind.ReorderPlay: return BattleCommand.ReorderPlay(ids.FindCard(cardId), targetIndex);
            case BattleCommandKind.Refresh: return BattleCommand.Refresh(ignoreRefreshLimit);
            case BattleCommandKind.ToggleFocus: return BattleCommand.ToggleFocus(ids.FindEnemy(enemyId));
            case BattleCommandKind.EndTurn: return BattleCommand.EndTurn();
            case BattleCommandKind.ReorderMagic: return BattleCommand.ReorderMagic(sourceIndex, targetIndex, slotCount, preserveEmptySlots);
            default: throw new ArgumentOutOfRangeException(nameof(kind));
        }
    }

    internal AICommandData Copy() => new AICommandData
    {
        kind = kind, cardId = cardId, enemyId = enemyId, targetIndex = targetIndex, sourceIndex = sourceIndex, slotCount = slotCount,
        allowDisabled = allowDisabled, ignoreRefreshLimit = ignoreRefreshLimit, preserveEmptySlots = preserveEmptySlots
    };
}

[Serializable]
public sealed class AICommandLogEntry
{
    public AICommandData command;
    public BattleCommandRejection rejection;
    public bool hasTurnOutcome;
    public BattleTurnOutcome turnOutcome;
    public string before;
    public string after;
    public string error;
}

[Serializable]
public sealed class AIBattleCommandLog
{
    public int version = 1;
    public int seed;
    public int initialRandomStep;
    // Content/initialization identity is supplied by the caller's factory, not inferred from a scene.
    public string contentVersion;
    public string initialFingerprint;
    public List<AICommandLogEntry> entries = new List<AICommandLogEntry>();
}

public interface IAICommandPolicy
{
    /// <summary>Choose an index from the legal payloads. Observation excludes hidden diagnostic state.</summary>
    int Choose(AIBattleObservation observation, IReadOnlyList<AICommandData> legalCommands);
}

public sealed class AIBattleStepResult
{
    public BattleCommandResult CommandResult { get; internal set; }
    public BattleTurnResult TurnResult { get; internal set; }
    public AIBattleObservation Observation { get; internal set; }
}

/// <summary>
/// Script/AI host over the SAME BattleCommandSession and BattleTurnDriver used by production rules.
/// No card/enemy collection writes. A factory must create initialized Simulation-service models with
/// fixed seed/content. Host registration itself does not draw, build reads, change scope or use RNG.
/// </summary>
public sealed class AIBattleHost
{
    private readonly AIEntityIds ids = new AIEntityIds();
    private readonly Func<string> diagnosticExtension;
    public BattleCommandSession Commands { get; }
    public AIBattleCommandLog Log { get; }
    public BattleTurnOutcome? LastOutcome { get; private set; }
    public bool IsFinished => LastOutcome == BattleTurnOutcome.Victory || LastOutcome == BattleTurnOutcome.Defeat
        || Commands.Player.CurrentHealth <= 0 || Commands.Battle?.CurrentPhase == BattlePhase.Finished;

    public AIBattleHost(BattleCommandSession commands, string contentVersion = null, Func<string> diagnosticExtension = null)
    {
        Commands = commands ?? throw new ArgumentNullException(nameof(commands));
        this.diagnosticExtension = diagnosticExtension;
        ids.Register(commands.Player, commands.Battle);
        AIBattleDiagnosticSnapshot initial = CaptureDiagnostics();
        Log = new AIBattleCommandLog
        {
            seed = initial.Seed, initialRandomStep = initial.RandomStep,
            contentVersion = contentVersion ?? string.Empty, initialFingerprint = initial.Fingerprint
        };
    }

    public AIBattleObservation Observe()
    {
        ids.Register(Commands.Player, Commands.Battle);
        return new AIBattleObservation(Commands, ids);
    }

    public AIBattleDiagnosticSnapshot CaptureDiagnostics()
    {
        ids.Register(Commands.Player, Commands.Battle);
        return new AIBattleDiagnosticSnapshot(Commands, ids, diagnosticExtension?.Invoke());
    }

    public IReadOnlyList<AICommandData> GetLegalCommands()
    {
        ids.Register(Commands.Player, Commands.Battle);
        IReadOnlyList<BattleCommand> commands = Commands.GetLegalCommands();
        var result = new List<AICommandData>();
        for (int i = 0; i < commands.Count; i++) result.Add(AICommandData.Capture(commands[i], ids));
        return result.AsReadOnly();
    }

    public AIBattleStepResult Execute(AICommandData command)
    {
        if (command == null) throw new ArgumentNullException(nameof(command));
        ids.Register(Commands.Player, Commands.Battle);
        var entry = new AICommandLogEntry { command = command.Copy(), before = CaptureDiagnostics().Fingerprint };
        Log.entries.Add(entry); // Keep the failing command even if a content hook throws mid-commit.
        try
        {
            // Resolve IDs only through this session's registry; missing/stale identities are rejected by commands.
            BattleCommand bound = command.Bind(ids);
            BattleCommandResult result = command.allowDisabled || command.ignoreRefreshLimit
                ? new BattleCommandResult(Commands, bound, BattleCommandRejection.InvalidCommand)
                : Commands.Execute(bound);
            entry.rejection = result.Rejection;
            var step = new AIBattleStepResult { CommandResult = result };
            if (result.Accepted && command.kind == BattleCommandKind.EndTurn && Commands.Battle != null)
            {
                step.TurnResult = BattleTurnDriver.ResolveTurn(Commands.Battle, tutorial: Commands.Tutorial);
                LastOutcome = step.TurnResult.Outcome;
                entry.hasTurnOutcome = true;
                entry.turnOutcome = step.TurnResult.Outcome;
                if (step.TurnResult.Outcome == BattleTurnOutcome.NextPlayerTurn)
                    Commands.BeginPlayerTurn();
            }
            entry.after = CaptureDiagnostics().Fingerprint;
            step.Observation = Observe();
            return step;
        }
        catch (Exception exception)
        {
            entry.error = exception.GetType().FullName + ": " + exception.Message;
            entry.after = CaptureDiagnostics().Fingerprint;
            throw;
        }
    }

    public AIBattleStepResult Step(IAICommandPolicy policy)
    {
        if (policy == null) throw new ArgumentNullException(nameof(policy));
        IReadOnlyList<AICommandData> legal = GetLegalCommands();
        if (legal.Count == 0) throw new InvalidOperationException("No legal command at the current decision point.");
        int index = policy.Choose(Observe(), legal);
        if (index < 0 || index >= legal.Count) throw new InvalidOperationException("AI selected an invalid command index.");
        return Execute(legal[index]);
    }
}

public sealed class AIReplayResult
{
    public bool Matched { get; internal set; }
    public int DivergedAt { get; internal set; } = -1;
    public string Reason { get; internal set; }
}

public static class AIBattleReplay
{
    /// <summary>Factory must recreate identical content and the same initial draw/intent setup from seed.</summary>
    public static AIReplayResult Replay(AIBattleCommandLog log, Func<int, AIBattleHost> create)
    {
        if (log == null) throw new ArgumentNullException(nameof(log));
        if (create == null) throw new ArgumentNullException(nameof(create));
        if (log.version != 1) return Diverged(-1, "Unsupported log version.");
        AIBattleHost host = create(log.seed);
        if (host.Log.contentVersion != log.contentVersion) return Diverged(-1, "Content version differs.");
        if (host.Log.initialFingerprint != log.initialFingerprint) return Diverged(-1, "Initial state differs.");
        for (int i = 0; i < log.entries.Count; i++)
        {
            AICommandLogEntry expected = log.entries[i];
            if (host.CaptureDiagnostics().Fingerprint != expected.before) return Diverged(i, "Before-state differs.");
            try { host.Execute(expected.command); }
            catch (Exception exception)
            {
                string error = exception.GetType().FullName + ": " + exception.Message;
                if (error != expected.error) return Diverged(i, "Exception differs: " + error);
            }
            AICommandLogEntry actual = host.Log.entries[host.Log.entries.Count - 1];
            if (actual.error != expected.error) return Diverged(i, "Exception differs.");
            if (actual.rejection != expected.rejection) return Diverged(i, "Command acceptance differs.");
            if (actual.hasTurnOutcome != expected.hasTurnOutcome || (actual.hasTurnOutcome && actual.turnOutcome != expected.turnOutcome))
                return Diverged(i, "Turn outcome differs.");
            if (actual.after != expected.after) return Diverged(i, "After-state differs.");
        }
        return new AIReplayResult { Matched = true };
    }
    private static AIReplayResult Diverged(int index, string reason) => new AIReplayResult { DivergedAt = index, Reason = reason };
}

public sealed class AIBatchResult
{
    public int Seed { get; internal set; }
    public bool Finished { get; internal set; }
    public string Failure { get; internal set; }
    public AIBattleCommandLog Log { get; internal set; }
}

public static class AIBattleBatch
{
    /// <summary>Bounded synchronous seeds; caller chooses a deterministic policy and content factory.</summary>
    public static IReadOnlyList<AIBatchResult> Run(IEnumerable<int> seeds, Func<int, AIBattleHost> create,
        Func<int, IAICommandPolicy> createPolicy, int maxCommandsPerSeed = 1000)
    {
        if (seeds == null) throw new ArgumentNullException(nameof(seeds));
        if (create == null) throw new ArgumentNullException(nameof(create));
        if (createPolicy == null) throw new ArgumentNullException(nameof(createPolicy));
        if (maxCommandsPerSeed <= 0) throw new ArgumentOutOfRangeException(nameof(maxCommandsPerSeed));
        var results = new List<AIBatchResult>();
        foreach (int seed in seeds)
        {
            AIBattleHost host = null;
            var result = new AIBatchResult { Seed = seed };
            try
            {
                host = create(seed);
                IAICommandPolicy policy = createPolicy(seed);
                for (int i = 0; i < maxCommandsPerSeed && !host.IsFinished; i++) host.Step(policy);
                result.Finished = host.IsFinished;
                if (!result.Finished) result.Failure = "Command budget exhausted.";
            }
            catch (Exception exception) { result.Failure = exception.GetType().Name + ": " + exception.Message; }
            result.Log = host?.Log;
            results.Add(result);
        }
        return results.AsReadOnly();
    }
}
