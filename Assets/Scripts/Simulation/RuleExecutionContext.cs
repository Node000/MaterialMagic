using System;
using System.Collections.Generic;

/// <summary>Per-player rule inputs. Legacy static facades read this only during synchronous rule calls.</summary>
public sealed class RuleExecutionContext
{
    public PlayerState Player { get; }
    public BattleManager Battle => Player.Battle;
    public RunDifficultySaveData Difficulty { get; }
    public IReadOnlyList<DifficultyUpgrade> Upgrades { get; }
    public IRuleContentProvider Content { get; }
    public RuleProfilePolicy Profile { get; }

    internal RuleExecutionContext(PlayerState player, RunDifficultySaveData difficulty, IRuleContentProvider content = null, RuleProfilePolicy profilePolicy = null)
    {
        Player = player;
        Content = content != null ? RuleContentArchive.Capture(content).CreateProvider() : RuleContentProviderHost.CaptureForNewWorld();
        Profile = profilePolicy != null ? RuleProfilePolicy.Restore(profilePolicy.Export())
            : ReferenceEquals(player.Services, PlayerStateServices.Simulation)
                ? RuleProfilePolicy.AllUnlocked : RuleProfilePolicyHost.CaptureForNewWorld();
        Difficulty = new RunDifficultySaveData
        {
            ascensionLevel = difficulty != null ? difficulty.ascensionLevel : 0,
            upgradeIds = difficulty != null && difficulty.upgradeIds != null
                ? (string[])difficulty.upgradeIds.Clone() : Array.Empty<string>()
        };
        var upgrades = new List<DifficultyUpgrade>();
        foreach (string id in Difficulty.upgradeIds)
        {
            DifficultyUpgrade upgrade = Content != null && Content.Named<DifficultyUpgradeData>().TryGetValue(id, out DifficultyUpgradeData data)
                ? new DataDrivenDifficultyUpgrade(data) : Content == null ? DifficultyUpgradeFactory.Create(id) : null;
            if (upgrade != null) upgrades.Add(upgrade);
        }
        Upgrades = upgrades.AsReadOnly();
    }

    public RuleExecutionScope Enter() => new RuleExecutionScope(this);
}

/// <summary>Never retain this scope over a Unity yield/await. Nested calls restore their caller exactly.</summary>
public sealed class RuleExecutionScope : IDisposable
{
    [ThreadStatic] private static RuleExecutionContext current;
    [ThreadStatic] private static List<Action> deferredFeedback;
    private readonly RuleExecutionContext previous;
    private readonly MaterialModifierContext material;
    private readonly MagicModifierContext magic;
    private bool disposed;
    public static RuleExecutionContext Current => current;

    internal RuleExecutionScope(RuleExecutionContext context)
    {
        previous = current;
        material = MaterialModifierModel.CurrentContext;
        magic = MagicModifierModel.CurrentContext;
        current = context;
        if (!ReferenceEquals(previous, context))
            MaterialModifierModel.CurrentContext = new MaterialModifierContext
            { PlayerState = context.Player, BattleManager = context.Battle };
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        MaterialModifierModel.CurrentContext = material;
        MagicModifierModel.CurrentContext = magic;
        current = previous;
    }

    /// <summary>Queue a presentation callback while rules are captured; otherwise the host runs it now.</summary>
    public static bool DeferFeedback(Action cue)
    {
        if (deferredFeedback == null) return false;
        deferredFeedback.Add(cue);
        return true;
    }

    internal static IReadOnlyList<Action> DrainFeedback()
    {
        if (deferredFeedback == null) return Array.Empty<Action>();
        Action[] copy = deferredFeedback.ToArray();
        deferredFeedback.Clear();
        return Array.AsReadOnly(copy);
    }

    public static RuleFeedbackCapture CaptureFeedback() => new RuleFeedbackCapture();

    public sealed class RuleFeedbackCapture : IDisposable
    {
        private readonly List<Action> previous;
        private readonly List<Action> cues = new List<Action>();
        internal RuleFeedbackCapture() { previous = deferredFeedback; deferredFeedback = cues; }
        public IReadOnlyList<Action> Drain()
        {
            Action[] result = cues.ToArray();
            cues.Clear();
            return Array.AsReadOnly(result);
        }
        public void Dispose() { deferredFeedback = previous; }
    }
}
