using System;
using System.Collections.Generic;

public static class ChoiceRuleRandom
{
    public static int Next(PlayerState owner, int min, int max)
    {
        PlayerState player = owner ?? RuleExecutionScope.Current?.Player;
        return player != null ? player.NextRelicRandomInt(min, max) : UnityEngine.Random.Range(min, max);
    }
}

internal static class ChoiceSessionIdentity
{
    private static long next;
    public static string Next(string prefix) => prefix + ":session:" + System.Threading.Interlocked.Increment(ref next);
}

public enum ChoiceKind
{
    BattleReward, MagicReward, MagicModifier, MagicTarget, MaterialModifier, MaterialTarget, RemoveMaterial, LegacyMaterial
}

/// <summary>Identity is stable for the lifetime of a session; payloads are identities, not copied inventory entities.</summary>
public sealed class ChoiceCandidate
{
    public string Id { get; }
    public int Amount { get; }
    public MagicData MagicData { get; }
    public RewardArrowOption Arrow { get; }
    public MagicModifierData MagicModifier { get; }
    public MaterialModifierData MaterialModifier { get; }
    public MagicModel Magic { get; }
    public MaterialModel Material { get; }

    public ChoiceCandidate(string id, int amount = 0, MagicData magicData = null, RewardArrowOption arrow = null,
        MagicModifierData magicModifier = null, MaterialModifierData materialModifier = null,
        MagicModel magic = null, MaterialModel material = null)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Amount = amount; MagicData = magicData; Arrow = arrow; MagicModifier = magicModifier;
        MaterialModifier = materialModifier; Magic = magic; Material = material;
    }
}

public sealed class PendingChoice
{
    private readonly IReadOnlyList<ChoiceCandidate> candidates;
    public string Id { get; }
    public ChoiceKind Kind { get; }
    public int MinSelections { get; }
    public int MaxSelections { get; }
    public bool CanCancel { get; }
    public IReadOnlyList<ChoiceCandidate> Candidates => candidates;

    public PendingChoice(string id, ChoiceKind kind, int min, int max, bool canCancel, IList<ChoiceCandidate> candidates)
    {
        if (min < 0 || max < min || candidates == null || max > candidates.Count)
            throw new ArgumentException("Invalid choice selection bounds.");
        Id = id ?? throw new ArgumentNullException(nameof(id)); Kind = kind;
        MinSelections = min; MaxSelections = max; CanCancel = canCancel;
        this.candidates = new List<ChoiceCandidate>(candidates).AsReadOnly();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (ChoiceCandidate candidate in candidates)
            if (candidate == null || !ids.Add(candidate.Id)) throw new ArgumentException("Candidate IDs must be unique.");
    }

    public ChoiceCandidate Find(string id)
    {
        for (int i = 0; i < candidates.Count; i++) if (candidates[i].Id == id) return candidates[i];
        return null;
    }

    public ChoiceFailure Validate(ChoiceSubmission answer, out List<ChoiceCandidate> selected)
    {
        selected = new List<ChoiceCandidate>();
        if (answer == null || answer.ChoiceId != Id) return ChoiceFailure.StaleChoice;
        if (answer.Cancel) return CanCancel && answer.CandidateIds.Count == 0 ? ChoiceFailure.None : ChoiceFailure.CannotCancel;
        if (answer.CandidateIds.Count < MinSelections || answer.CandidateIds.Count > MaxSelections) return ChoiceFailure.InvalidCount;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < answer.CandidateIds.Count; i++)
        {
            ChoiceCandidate candidate = Find(answer.CandidateIds[i]);
            if (candidate == null) return ChoiceFailure.UnknownCandidate;
            if (!seen.Add(candidate.Id)) return ChoiceFailure.DuplicateCandidate;
            selected.Add(candidate);
        }
        return ChoiceFailure.None;
    }
}

public sealed class ChoiceSubmission
{
    public string ChoiceId { get; }
    public IReadOnlyList<string> CandidateIds { get; }
    public bool Cancel { get; }
    public ChoiceSubmission(string choiceId, params string[] candidateIds)
        : this(choiceId, false, candidateIds) { }
    public ChoiceSubmission(string choiceId, bool cancel, params string[] candidateIds)
    {
        ChoiceId = choiceId; Cancel = cancel;
        CandidateIds = Array.AsReadOnly(candidateIds != null ? (string[])candidateIds.Clone() : Array.Empty<string>());
    }
    public static ChoiceSubmission CancelChoice(string id) => new ChoiceSubmission(id, true);
}

public enum ChoiceFailure
{
    None, NoPendingChoice, StaleChoice, InvalidCount, CannotCancel, UnknownCandidate, DuplicateCandidate,
    TargetUnavailable, NoMagicSlot, InvalidReward, Busy
}

/// <summary>Immutable scalar presentation receipt plus the committed entity identities. Never apply this receipt again.</summary>
public sealed class ChoiceResult
{
    public bool Accepted => Failure == ChoiceFailure.None;
    public ChoiceFailure Failure { get; }
    public bool Cancelled { get; internal set; }
    public int GoldBefore { get; internal set; }
    public int GoldAfter { get; internal set; }
    public int HealthBefore { get; internal set; }
    public int HealthAfter { get; internal set; }
    public int MagicSlotIndex { get; internal set; } = -1;
    public int ResultingMagicCount { get; internal set; }
    public MagicModel AcquiredMagic { get; internal set; }
    public MaterialModel AddedMaterial { get; internal set; }
    public ChoiceResult(ChoiceFailure failure = ChoiceFailure.None) { Failure = failure; }
}

public interface IChoiceSession
{
    PendingChoice PendingChoice { get; }
    bool IsComplete { get; }
    ChoiceResult SubmitChoice(ChoiceSubmission submission);
}
