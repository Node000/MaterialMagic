using System;
using System.Collections.Generic;

/// <summary>Presentation checkpoints. Advancing past a Before/Pending checkpoint commits its rule.</summary>
public enum BattleResolutionStepKind
{
    BeforeArrow,
    AfterArrow,
    BeforeMagicFill,
    BeforeMagicImpact,
    AfterMagic,
    DeathPending,
    DeathResolved,
    BeforeAfterRead,
    AfterRead,
    PlayerTurnEnded,
    EnemyTurnStarted,
    EnemyActionStarted,
    BeforeEnemyIntent,
    EnemyIntentResolved,
    EnemyActionEnded,
    EnemyTurnEnded,
    PlayerTurnStarted,
    BattleFinished,
    PlayerResolveStarted,
    Completed,
    HandDiscardedOnConfirm
}

/// <summary>A value snapshot; model references identify units but numeric values do not change later.</summary>
public sealed class BattleEnemySnapshot
{
    public EnemyModel Enemy { get; }
    public int Health { get; }
    public int Shield { get; }
    public bool IsDead { get; }
    public bool DeathHandled { get; }
    public int MaxHealth { get; }
    private readonly IReadOnlyDictionary<BuffEnum, BuffModel> buffs;
    public IReadOnlyDictionary<BuffEnum, BuffModel> Buffs => BattleSnapshotValues.CopyBuffs(buffs);
    public IReadOnlyList<BattleIntentSnapshot> Intents { get; }

    internal BattleEnemySnapshot(EnemyModel enemy, PlayerState player)
    {
        Enemy = enemy;
        Health = enemy.CurrentHealth;
        Shield = enemy.Shield;
        IsDead = enemy.IsDead;
        DeathHandled = enemy.DeathHandled;
        MaxHealth = enemy.MaxHealth;
        buffs = BattleSnapshotValues.CopyBuffs(enemy.Buffs);
        var intents = new List<BattleIntentSnapshot>();
        foreach (EnemyIntentData intent in enemy.CurrentIntents) intents.Add(new BattleIntentSnapshot(enemy, intent, player));
        Intents = intents.AsReadOnly();
    }
}

public sealed class BattleStateSnapshot
{
    public int PlayerHealth { get; }
    public int PlayerShield { get; }
    public int PlayerMaxHealth { get; }
    public int PlayerGold { get; }
    public int ContinuousCastCount { get; }
    public EnemyModel FocusTarget { get; }
    public BattlePhase Phase { get; }
    public int DrawPileCount { get; }
    public int DiscardPileCount { get; }
    public int ConsumedPileCount { get; }
    public IReadOnlyList<MaterialModel> Hand { get; }
    public IReadOnlyList<MaterialModel> PlayZone { get; }
    private readonly IReadOnlyDictionary<BuffEnum, BuffModel> playerBuffs;
    public IReadOnlyDictionary<BuffEnum, BuffModel> PlayerBuffs => BattleSnapshotValues.CopyBuffs(playerBuffs);
    public int PlayedCardCount { get; }
    public int PlayLimit { get; }
    public int RemainingRefreshChances { get; }
    private readonly Dictionary<MaterialModel, MaterialModel> cards = new Dictionary<MaterialModel, MaterialModel>();
    public MaterialModel GetCardDisplay(MaterialModel identity)
    {
        return identity != null && cards.TryGetValue(identity, out MaterialModel copy) ? RuleMaterialCodec.CloneForDisplay(copy) : null;
    }
    public IReadOnlyList<BattleEnemySnapshot> Enemies { get; }
    public bool AllEnemiesDead { get; }

    private BattleStateSnapshot(PlayerState player, BattleManager battle)
    {
        PlayerHealth = player != null ? player.CurrentHealth : 0;
        PlayerShield = player != null ? player.Shield : 0;
        PlayerMaxHealth = player != null ? player.MaxHealth : 0;
        PlayerGold = player != null ? player.Gold : 0;
        ContinuousCastCount = battle != null ? battle.ContinuousCastCount : 0;
        Phase = battle != null ? battle.CurrentPhase : BattlePhase.None;
        FocusTarget = battle?.FocusTarget;
        DrawPileCount = player != null ? player.DrawPile.Count : 0;
        DiscardPileCount = player != null ? player.DiscardPile.Count : 0;
        ConsumedPileCount = player != null ? player.ConsumedPile.Count : 0;
        Hand = new List<MaterialModel>(player != null ? player.Hand : new List<MaterialModel>()).AsReadOnly();
        PlayZone = new List<MaterialModel>(player != null ? player.PlayZone : new List<MaterialModel>()).AsReadOnly();
        if (player != null)
        {
            foreach (var pile in new[] { player.Deck, player.Hand, player.PlayZone, player.DrawPile, player.DiscardPile, player.ConsumedPile, player.TemporaryMaterialsNextTurn })
                foreach (MaterialModel card in pile)
                    if (card != null && !cards.ContainsKey(card)) cards.Add(card, RuleMaterialCodec.CloneForDisplay(card));
        }
        playerBuffs = BattleSnapshotValues.CopyBuffs(player?.Buffs);
        PlayedCardCount = player?.PlayedCardCountThisTurn ?? 0;
        PlayLimit = player?.PlayLimitPerTurn ?? 0;
        RemainingRefreshChances = player != null ? player.RemainingPermanentRefreshChancesThisTurn + player.ExtraRefreshChancesThisTurn : 0;
        AllEnemiesDead = battle != null && battle.AllEnemiesDead();
        var enemies = new List<BattleEnemySnapshot>();
        for (int i = 0; battle != null && i < battle.Enemies.Count; i++)
        {
            EnemyModel enemy = battle.Enemies[i];
            if (enemy != null)
                enemies.Add(new BattleEnemySnapshot(enemy, player));
        }
        Enemies = enemies.AsReadOnly();
    }

    public BattleEnemySnapshot FindEnemy(EnemyModel enemy)
    {
        foreach (var snapshot in Enemies) if (ReferenceEquals(snapshot.Enemy, enemy)) return snapshot;
        return null;
    }

    public static BattleStateSnapshot Capture(PlayerState player, BattleManager battle)
    {
        return new BattleStateSnapshot(player, battle);
    }
}

public sealed class BattleAfterReadActionResult
{
    public ArrowReadStep ArrowStep { get; }
    public MaterialModel Card => ArrowStep.SourceCard;
    public ArrowReadAfterReadAction Action => ArrowStep.AfterReadAction;
    public bool Applied { get; }

    internal BattleAfterReadActionResult(ArrowReadStep step, bool applied)
    {
        ArrowStep = step;
        Applied = applied;
    }
}

/// <summary>
/// A rule checkpoint, not a request for the consumer to execute rules. UI can wait between checkpoints;
/// a script can immediately call MoveNext again. References are identities, not deep replay snapshots.
/// </summary>
public sealed class BattleResolutionStep
{
    public BattleResolutionStepKind Kind { get; internal set; }
    public int SequenceIndex { get; internal set; } = -1;
    public int StepIndex { get; internal set; } = -1;
    public ArrowReadSequence Sequence { get; internal set; }
    public ArrowReadStep ArrowStep { get; internal set; }
    public int TokenStart { get; internal set; } = -1;
    public int MatchLength { get; internal set; }
    public int MagicIndex { get; internal set; } = -1;
    public MagicModel Magic { get; internal set; }
    public MagicModel MagicEffectSource { get; internal set; }
    public MaterialEnum ArrowDisplayMaterial { get; internal set; }
    public IReadOnlyList<MaterialEnum> TokenDisplayMaterials { get; internal set; } = Array.Empty<MaterialEnum>();
    public IReadOnlyList<MaterialEnum> TriggerMaterials { get; internal set; } = Array.Empty<MaterialEnum>();
    public EnemyModel Target { get; internal set; }
    public MagicCastResult CastResult { get; internal set; }
    public bool CastPerformed { get; internal set; }
    public IReadOnlyList<MaterialModel> CreatedHandCards { get; internal set; } = Array.Empty<MaterialModel>();
    public int ContinuousCastCount { get; internal set; }
    public EnemyDeathEntry DeathEntry { get; internal set; }
    public EnemyModel ActingEnemy { get; internal set; }
    public EnemyIntentData Intent { get; internal set; }
    public int IntentIndex { get; internal set; } = -1;
    public int HitIndex { get; internal set; } = -1;
    public int HitCount { get; internal set; }
    public int EnemyActionIndex { get; internal set; } = -1;
    public IReadOnlyList<MaterialModel> RemovedTemporaryCards { get; internal set; } = Array.AsReadOnly(new MaterialModel[0]);
    public IReadOnlyList<Action> FeedbackCues { get; internal set; } = Array.AsReadOnly(new Action[0]);
    public BattleStateSnapshot Before { get; internal set; }
    public BattleStateSnapshot After { get; internal set; }
    public IReadOnlyList<BattleAfterReadActionResult> AfterReadActions { get; internal set; }
        = Array.AsReadOnly(new BattleAfterReadActionResult[0]);
}

/// <summary>
/// Shared death barrier. Pending is emitted before ResolveEnemyDeath; the next advance resolves it.
/// Newly queued deaths (including death effects) are drained before leaving the barrier.
/// </summary>
public static class BattleDeathResolution
{
    public static IEnumerable<BattleResolutionStep> Steps(PlayerState player, BattleManager battle)
    {
        if (battle == null)
            throw new ArgumentNullException(nameof(battle));

        while (true)
        {
            battle.EnqueueUnhandledDeadEnemies();
            if (!battle.TryDequeueEnemyDeath(out EnemyDeathEntry entry))
                yield break;

            BattleStateSnapshot before = BattleStateSnapshot.Capture(player, battle);
            yield return new BattleResolutionStep
            {
                Kind = BattleResolutionStepKind.DeathPending,
                DeathEntry = entry,
                Before = before
            };
            battle.ResolveEnemyDeath(entry);
            yield return new BattleResolutionStep
            {
                Kind = BattleResolutionStepKind.DeathResolved,
                DeathEntry = entry,
                Before = before,
                After = BattleStateSnapshot.Capture(player, battle)
            };
        }
    }

    public static void Drain(PlayerState player, BattleManager battle, Action<BattleResolutionStep> onStep = null)
    {
        using (player.Rules.Enter())
            foreach (BattleResolutionStep step in Steps(player, battle))
                onStep?.Invoke(step);
    }
}
