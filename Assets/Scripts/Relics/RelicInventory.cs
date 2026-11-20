using System;
using System.Collections.Generic;

/// <summary>Player-owned inventory and synchronous rule dispatcher. No views, coroutines or Unity objects.</summary>
public sealed class RelicInventory
{
    private sealed class Work
    {
        public Action Action;
        public HashSet<string> Path;
    }
    private sealed class Scope : IDisposable
    {
        private RelicInventory owner;
        public Scope(RelicInventory owner) { this.owner = owner; owner.depth++; }
        public void Dispose()
        {
            if (owner == null) return;
            var value = owner; owner = null;
            if (--value.depth == 0) value.Drain();
        }
    }
    private sealed class DamageScope : IDisposable
    {
        private readonly RelicInventory owner;
        private readonly RelicDamageKind previous;
        public DamageScope(RelicInventory owner, RelicDamageKind kind)
        { this.owner = owner; previous = owner.DamageKind; owner.DamageKind = kind; }
        public void Dispose() { owner.DamageKind = previous; }
    }

    private readonly List<RelicModel> items = new List<RelicModel>();
    private readonly IReadOnlyList<RelicModel> readOnlyItems;
    private readonly Queue<Work> facts = new Queue<Work>();
    private readonly Queue<Work> effects = new Queue<Work>();
    private readonly Dictionary<BuffModel, long> buffChanges = new Dictionary<BuffModel, long>();
    private HashSet<string> path = new HashSet<string>();
    private string currentHook;
    private string originRelic;
    private int depth;
    private bool draining;
    private bool battleStarted;
    private bool battleEnded;
    private int workCount;
    private long eventSequence;
    public const int MaxWorkPerOperation = 1024;
    public PlayerState Owner { get; }
    public BattleManager Battle { get; private set; }
    public RelicDamageKind DamageKind { get; private set; } = RelicDamageKind.EffectDamage;
    public string OriginRelicId => originRelic;
    public bool IsFaulted { get; private set; }
    public bool IsSettled => !IsFaulted && depth == 0 && !draining && facts.Count == 0 && effects.Count == 0;
    public IReadOnlyList<RelicModel> Items => readOnlyItems;
    public event Action Changed;
    public event Action<RelicModel> StateChanged;
    /// <summary>Committed facts for tests/logging. Observers must not mutate rules.</summary>
    public event Action<RelicEvent> Resolved;

    public RelicInventory(PlayerState owner)
    {
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        readOnlyItems = items.AsReadOnly();
    }
    public IDisposable BeginOperation() => new Scope(this);
    public IDisposable BeginDamage(RelicDamageKind kind) => new DamageScope(this, kind);
    public void BindBattle(BattleManager battle)
    {
        if (ReferenceEquals(Battle, battle)) return;
        Battle = battle;
        ResetBattleLifecycle();
    }
    internal void ResetBattleLifecycle() { battleStarted = false; battleEnded = false; }
    internal void MarkBattleRestored() { battleStarted = true; battleEnded = false; }
    public bool Contains(int id) => items.Exists(x => x.Data.numericId == id);
    public RelicAcquireResult TryAcquire(int id)
        => GameDataDatabase.TryGetRelicData(id, out var data) ? TryAcquire(data) : RelicAcquireResult.Invalid;
    public RelicAcquireResult TryAcquire(RelicData data)
    {
        if (data == null || data.numericId <= 0 || IsFaulted) return RelicAcquireResult.Invalid;
        return TryAcquire(RelicFactory.Create(data));
    }
    /// <summary>Explicit model injection for extensions and deterministic rule tests.</summary>
    public RelicAcquireResult TryAcquire(RelicModel model)
    {
        if (model?.Data == null || model.Data.numericId <= 0 || !model.IsEnabled || IsFaulted ||
            model.OwningInventory != null && !ReferenceEquals(model.OwningInventory, this)) return RelicAcquireResult.Invalid;
        var data = model.Data;
        using (BeginOperation())
        {
            if (Contains(data.numericId))
            {
                Owner.AddGold(Math.Max(0, data.duplicateGold), false, RelicGoldReason.Compensation);
                return RelicAcquireResult.DuplicateCompensated;
            }
            model.OwningInventory = this;
            items.Add(model);
            // Acquisition goes only to this instance, never replayed during Restore.
            QueueFact(model, new RelicEvent(RelicEventKind.Acquired));
            Changed?.Invoke();
            return RelicAcquireResult.Acquired;
        }
    }
    public void Restore(IEnumerable<RelicSaveData> saved)
    {
        if (!IsSettled) throw new InvalidOperationException("Cannot restore relics during resolution.");
        foreach (var item in items) item.OwningInventory = null;
        items.Clear();
        if (saved != null)
            foreach (var value in saved)
            {
                if (value == null || Contains(value.numericId)) continue;
                RelicModel model = null;
                if (value.stateVersion == 1 && GameDataDatabase.TryGetRelicData(value.numericId, out var data))
                    model = RelicFactory.Create(data);
                model = model ?? new UnavailableRelicModel(value);
                model.RestoreState(value);
                model.OwningInventory = this;
                items.Add(model);
            }
        Changed?.Invoke();
    }
    public RelicSaveData[] Export()
    {
        if (!IsSettled) throw new InvalidOperationException("Cannot save an unresolved relic operation.");
        var result = new RelicSaveData[items.Count];
        for (int i = 0; i < items.Count; i++) result[i] = items[i].ExportState();
        return result;
    }
    public void Publish(RelicEvent value)
    {
        if (IsFaulted) throw new InvalidOperationException("Relic resolution is faulted; reload the last checkpoint.");
        if (value == null) throw new ArgumentNullException(nameof(value));
        value.EventId = ++eventSequence;
        // Capture eligible instances now, not when a later queued event is processed.
        var snapshot = items.ToArray();
        facts.Enqueue(new Work { Path = new HashSet<string>(path), Action = () =>
        {
            Resolved?.Invoke(value);
            foreach (var item in snapshot) Dispatch(item, value);
        }});
        if (depth == 0) Drain();
    }
    private void QueueFact(RelicModel model, RelicEvent value)
    {
        value.EventId = ++eventSequence;
        facts.Enqueue(new Work { Path = new HashSet<string>(path), Action = () => Dispatch(model, value) });
    }
    private void Dispatch(RelicModel item, RelicEvent value)
    {
        if (!item.IsEnabled || !items.Contains(item)) return;
        string key = item.InstanceId + ":" + value.Kind;
        if (path.Contains(key)) return;
        var previous = path;
        var previousHook = currentHook;
        path = new HashSet<string>(path) { key };
        currentHook = key;
        try
        {
            item.OnEvent(new RelicContext(this, item), value);
            StateChanged?.Invoke(item);
        }
        finally { path = previous; currentHook = previousHook; }
    }
    internal void Enqueue(RelicModel relic, Action effect)
    {
        if (currentHook == null) throw new InvalidOperationException("Relic actions require a committed trigger.");
        effects.Enqueue(new Work { Path = new HashSet<string>(path), Action = () =>
        {
            if (Owner.CurrentHealth <= 0) return;
            string previous = originRelic;
            originRelic = relic.InstanceId;
            try { effect(); }
            finally { originRelic = previous; }
        }});
    }
    private void Drain()
    {
        if (draining || depth != 0) return;
        if (IsFaulted) throw new InvalidOperationException("Relic resolution is faulted.");
        draining = true;
        workCount = 0;
        var previous = path;
        try
        {
            while (facts.Count > 0 || effects.Count > 0)
            {
                if (++workCount > MaxWorkPerOperation)
                    throw new InvalidOperationException("Relic operation exceeded work budget. Trigger path: " + string.Join(" -> ", path));
                var work = facts.Count > 0 ? facts.Dequeue() : effects.Dequeue();
                path = work.Path;
                work.Action();
            }
        }
        catch { IsFaulted = true; facts.Clear(); effects.Clear(); throw; }
        finally { path = previous; draining = false; buffChanges.Clear(); }
    }
    public int ModifyAttack(int amount) => Modify(amount, x => x.ModifyAttack);
    public int ModifyMagicAttack(MagicModel magic, int amount)
    {
        foreach (var item in items) if (item.IsEnabled) amount = item.ModifyMagicAttack(magic, amount);
        return Math.Max(0, amount);
    }
    public int ModifyDamageTaken(int amount) => Modify(amount, x => x.ModifyDamageTaken);
    public int ModifyShieldGain(int amount) => Modify(amount, x => x.ModifyShieldGain);
    public int ModifyGoldGain(int amount) => Modify(amount, x => x.ModifyGoldGain);
    private int Modify(int amount, Func<RelicModel, Func<int, int>> select)
    {
        foreach (var item in items) if (item.IsEnabled) amount = select(item)(amount);
        return Math.Max(0, amount);
    }
    public int ModifyBuffGiven(BuffEnum type, int amount)
    {
        foreach (var item in items) if (item.IsEnabled) amount = item.ModifyBuffGiven(type, amount);
        return Math.Max(0, amount);
    }
    public int ModifyBuffReceived(BuffEnum type, int amount)
    {
        foreach (var item in items) if (item.IsEnabled) amount = item.ModifyBuffReceived(type, amount);
        return Math.Max(0, amount);
    }
    public void StartBattle()
    {
        if (battleStarted) return;
        battleStarted = true; battleEnded = false;
        foreach (var item in items) item.ResetBattleState();
        Publish(new RelicEvent(RelicEventKind.BattleStarted));
    }
    public void PrepareTurn()
    {
        foreach (var item in items) item.ResetTurnState();
        Publish(new RelicEvent(RelicEventKind.TurnPreparing));
    }
    public void EndBattle(bool victory)
    {
        if (battleEnded) return;
        battleEnded = true;
        Publish(new RelicEvent(RelicEventKind.BattleEnded, victory: victory));
    }
    internal bool IsCurrentTarget(CombatantModel target)
        => target != null && !target.IsDead && (target.IsPlayer ? ReferenceEquals(target.Player, Owner) :
            Battle != null && Battle.Enemies.ContainsReference(target.Enemy));
    internal void DealDamage(CombatantModel target, int amount, RelicDamageKind kind)
    {
        if (!IsCurrentTarget(target) || amount <= 0 || Battle == null) return;
        using (BeginOperation())
        using (BeginDamage(kind))
        {
            if (kind == RelicDamageKind.HealthLoss) { target.TakeDirectDamage(amount); return; }
            var source = new CombatantModel(Owner);
            if (kind == RelicDamageKind.Attack) Owner.TriggerOnAttack(target, ref amount);
            var result = target.TakeDamageResult(amount, source);
            if (kind == RelicDamageKind.Attack)
            {
                int actual = result.HealthDamage;
                Owner.TriggerAfterAttack(target, ref actual);
            }
        }
    }
    internal long BuffChangeStamp(BuffModel buff) => buffChanges.TryGetValue(buff, out long value) ? value : 0;
    internal void RecordBuffChange(CombatantModel target, BuffModel buff, int before, int after)
    {
        if (buff == null || before == after) return;
        buffChanges[buff] = BuffChangeStamp(buff) + after - before;
        Publish(new RelicEvent(RelicEventKind.BuffChanged, target: target, amount: after - before,
            before: before, after: after, buffType: buff.buffType, originRelicId: originRelic));
    }
    internal void ObserveBuffHook(CombatantModel target, BuffModel buff, Action callback)
    {
        using (BeginOperation())
        {
        int before = buff.stack;
        long stamp = BuffChangeStamp(buff);
        callback();
        int nestedDelta = (int)(BuffChangeStamp(buff) - stamp);
        int remaining = buff.stack - before - nestedDelta;
        if (remaining != 0) RecordBuffChange(target, buff, buff.stack - remaining, buff.stack);
        }
    }
}

internal static class RelicEnemyListUtility
{
    internal static bool ContainsReference(this IReadOnlyList<EnemyModel> enemies, EnemyModel target)
    {
        for (int i = 0; i < enemies.Count; i++) if (ReferenceEquals(enemies[i], target)) return true;
        return false;
    }
}
