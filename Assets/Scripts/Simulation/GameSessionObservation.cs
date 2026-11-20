using System;
using System.Collections.Generic;

public sealed class GameChoiceObservation
{
    public string Id { get; }
    public ChoiceKind Kind { get; }
    public int MinSelections { get; }
    public int MaxSelections { get; }
    public bool CanCancel { get; }
    public IReadOnlyList<string> CandidateDescriptions { get; }
    internal GameChoiceObservation(PendingChoice choice, string stableId)
    {
        Id = stableId; Kind = choice.Kind; MinSelections = choice.MinSelections; MaxSelections = choice.MaxSelections; CanCancel = choice.CanCancel;
        var values = new List<string>();
        foreach (ChoiceCandidate candidate in choice.Candidates)
            values.Add(candidate.MagicData != null ? "magic:" + candidate.MagicData.numericId
                : candidate.Material != null ? "material:" + (int)candidate.Material.material
                : candidate.MagicModifier != null ? "magic_modifier:" + candidate.MagicModifier.id
                : candidate.MaterialModifier != null ? "material_modifier:" + candidate.MaterialModifier.id
                : candidate.Arrow != null ? "arrow:" + (int)candidate.Arrow.material
                : candidate.Magic != null ? "magic_slot:" + candidate.Magic.SlotIndex
                : "amount:" + candidate.Amount);
        CandidateDescriptions = values.AsReadOnly();
    }
}

public sealed class GameMapCellObservation
{
    public int X { get; }
    public int Y { get; }
    public bool Available { get; }
    public bool Revealed { get; }
    public bool Hidden { get; }
    public int? VisibleLevelId { get; }
    public bool VisibleBoss { get; }
    internal GameMapCellObservation(RunMapCellModel cell)
    {
        X = cell.x; Y = cell.y; Available = cell.isAvailable; Revealed = cell.isRevealed; Hidden = cell.isRevealed && cell.isHidden;
        VisibleLevelId = Revealed && !Hidden ? (int?)cell.level?.numericId : null;
        VisibleBoss = Revealed && !Hidden && cell.isBoss;
    }
}

/// <summary>Visible scalar/copy-only observation for whole-run policies. No RNG, future recipes or hidden node content.</summary>
public sealed class GameSessionObservation
{
    public RunFlowState Phase { get; }
    public int NodeIndex { get; }
    public int Health { get; }
    public int Gold { get; }
    public int MapX { get; }
    public int MapY { get; }
    public int? LeftLevelId { get; }
    public int? RightLevelId { get; }
    public string EventNodeId { get; }
    public GameChoiceObservation Choice { get; }
    public AIBattleObservation Cards { get; }
    public IReadOnlyList<GameMapCellObservation> MapCells { get; }

    internal GameSessionObservation(GameSession session, AIBattleObservation cards)
    {
        Phase = session.Run.State; NodeIndex = session.Run.CurrentMapNodeIndex; Health = session.Player.CurrentHealth; Gold = session.Player.Gold;
        MapX = session.Run.CurrentMapX; MapY = session.Run.CurrentMapY;
        if (NodeIndex >= 0 && NodeIndex < session.Run.MapNodes.Count)
        {
            RunMapNodeModel node = session.Run.MapNodes[NodeIndex];
            LeftLevelId = node.leftHidden ? null : (int?)node.leftLevel?.numericId;
            RightLevelId = node.rightHidden ? null : (int?)node.rightLevel?.numericId;
        }
        EventNodeId = session.Event?.CurrentNode?.id;
        if (session.Choice?.PendingChoice != null) Choice = new GameChoiceObservation(session.Choice.PendingChoice, session.StableChoiceId(session.Choice.PendingChoice.Id));
        Cards = cards;
        var cells = new List<GameMapCellObservation>();
        foreach (RunMapCellModel cell in session.Run.MapGrid.cells) if (cell != null) cells.Add(new GameMapCellObservation(cell));
        MapCells = cells.AsReadOnly();
    }
}

public interface IAIGamePolicy
{
    int Choose(GameSessionObservation observation, IReadOnlyList<GameCommand> legalCommands);
}

public static class GameSessionReplay
{
    public static AIReplayResult Replay(GameSessionLog log, Func<int, GameSession> create)
    {
        if (log == null) throw new ArgumentNullException(nameof(log));
        if (create == null) throw new ArgumentNullException(nameof(create));
        if (log.version != 1) return Failure(-1, "Unsupported log version.");
        GameSession session = create(log.seed);
        if (session.Log.contentVersion != log.contentVersion || session.Log.initialFingerprint != log.initialFingerprint)
            return Failure(-1, "Initial content/state differs.");
        for (int i = 0; i < log.entries.Count; i++)
        {
            GameCommandLogEntry expected = log.entries[i];
            if (session.DiagnosticFingerprint() != expected.before) return Failure(i, "Before-state differs.");
            try { session.Execute(expected.command); }
            catch (Exception exception)
            {
                if (exception.GetType().FullName + ": " + exception.Message != expected.error)
                    return Failure(i, "Exception differs.");
            }
            GameCommandLogEntry actual = session.Log.entries[session.Log.entries.Count - 1];
            if (actual.accepted != expected.accepted || actual.rejection != expected.rejection || actual.error != expected.error)
                return Failure(i, "Command result differs.");
            if (actual.after != expected.after) return Failure(i, "After-state differs.");
        }
        return new AIReplayResult { Matched = true };
    }
    private static AIReplayResult Failure(int index, string reason) => new AIReplayResult { DivergedAt = index, Reason = reason };
}

public sealed class GameBatchResult
{
    public int Seed { get; internal set; }
    public RunFlowState FinalState { get; internal set; }
    public string Failure { get; internal set; }
    public GameSessionLog Log { get; internal set; }
}

public static class GameSessionBatch
{
    public static IReadOnlyList<GameBatchResult> Run(IEnumerable<int> seeds, Func<int, GameSession> create,
        Func<int, IAIGamePolicy> createPolicy, int maxCommandsPerSeed = 1000)
    {
        if (seeds == null) throw new ArgumentNullException(nameof(seeds));
        if (create == null) throw new ArgumentNullException(nameof(create));
        if (createPolicy == null) throw new ArgumentNullException(nameof(createPolicy));
        if (maxCommandsPerSeed <= 0) throw new ArgumentOutOfRangeException(nameof(maxCommandsPerSeed));
        var results = new List<GameBatchResult>();
        foreach (int seed in seeds)
        {
            GameSession session = null;
            var result = new GameBatchResult { Seed = seed };
            try
            {
                session = create(seed);
                IAIGamePolicy policy = createPolicy(seed);
                for (int i = 0; i < maxCommandsPerSeed && !session.IsFinished; i++)
                {
                    IReadOnlyList<GameCommand> legal = session.GetLegalCommands();
                    if (legal.Count == 0) throw new InvalidOperationException("No legal whole-run command at decision point.");
                    int selected = policy.Choose(session.Observe(), legal);
                    if (selected < 0 || selected >= legal.Count) throw new InvalidOperationException("AI selected an invalid command index.");
                    GameCommandLogEntry entry = session.Execute(legal[selected]);
                    if (!entry.accepted) throw new InvalidOperationException("Legal command rejected: " + entry.rejection);
                }
                result.FinalState = session.Run.State;
                if (!session.IsFinished) result.Failure = "Command budget exhausted.";
            }
            catch (Exception exception) { result.Failure = exception.GetType().Name + ": " + exception.Message; }
            result.Log = session?.Log;
            results.Add(result);
        }
        return results.AsReadOnly();
    }
}
