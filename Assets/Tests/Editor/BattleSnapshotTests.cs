using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>Production A3 driver/trace contract. No scene, coroutine, frame, audio or real profile is used.</summary>
public class BattleSnapshotTests
{
    private readonly List<BattleManager> ownedBattles = new List<BattleManager>();
    private MaterialModifierContext previousMaterial;
    private MagicModifierContext previousMagic;
    private RuleExecutionContext previousRules;
    private BattleManager previousSingleton;

    [SetUp]
    public void SetUp()
    {
        previousMaterial = MaterialModifierModel.CurrentContext;
        previousMagic = MagicModifierModel.CurrentContext;
        previousRules = RuleExecutionScope.Current;
        previousSingleton = BattleManager.Instance;
    }

    [TearDown]
    public void TearDown()
    {
        foreach (BattleManager manager in ownedBattles) BattleManager.ClearInstance(manager);
        ownedBattles.Clear();
        MaterialModifierModel.CurrentContext = previousMaterial;
        MagicModifierModel.CurrentContext = previousMagic;
        Assert.That(RuleExecutionScope.Current, Is.SameAs(previousRules));
        Assert.That(BattleManager.Instance, Is.SameAs(previousSingleton), "new BattleManager must never replace a host singleton.");
    }

    private static PlayerStatus Player(int seed = 41011, int health = 100, PlayerStateServices services = null)
    {
        var player = new PlayerStatus(health, 0, services ?? PlayerStateServices.Simulation,
            new RunDifficultySaveData { upgradeIds = Array.Empty<string>() },
            new RuleContentSnapshot().Freeze(), RuleProfilePolicy.AllUnlocked);
        player.SetRunRandomState(seed, 0);
        return player;
    }

    private BattleManager Battle(PlayerState player)
    {
        var manager = new BattleManager(player);
        ownedBattles.Add(manager);
        manager.BeginPlayerTurn();
        return manager;
    }

    private static EnemyModel Enemy(BattleManager manager, int health, int attack = 0, int hits = 1)
    {
        EnemyModel enemy;
        using (manager.PlayerState.Rules.Enter())
            enemy = new EnemyModel(new EnemyData
            {
                numericId = 94101 + manager.Enemies.Count,
                string_id = "snapshot_enemy_" + manager.Enemies.Count,
                maxHealth = health
            });
        manager.AddEnemy(enemy);
        SetIntents(enemy, attack > 0
            ? new EnemyIntentData { actionType = EnemyActionType.Attack, value = attack, times = hits }
            : new EnemyIntentData { actionType = EnemyActionType.Stunned });
        return enemy;
    }

    private static void SetIntents(EnemyModel enemy, params EnemyIntentData[] intents)
    {
        enemy.ClearCurrentIntents();
        ((List<EnemyIntentData>)enemy.CurrentIntents).AddRange(intents);
    }

    private static MaterialModel Played(PlayerState player, MaterialEnum material = MaterialEnum.Earth)
    {
        var card = new MaterialModel("snapshot_card_" + player.PlayZone.Count, material) { isPlayed = true };
        player.PlayZone.Add(card);
        return card;
    }

    private static void PlayCues(IEnumerable<Action> cues)
    {
        foreach (Action cue in cues) cue();
    }

    [Test]
    public void FullDriverCommitsVictoryAndCleanupBeforeAnyTracePlayback()
    {
        PlayerStatus player = Player();
        BattleManager manager = Battle(player);
        EnemyModel enemy = Enemy(manager, 4);
        player.AddBuff(BuffEnum.MagicAttackAll, 1);
        MaterialModel card = Played(player);
        var magic = new MultiHitMagic(5, 1);
        var observed = new List<BattleResolutionStep>();

        BattleTurnResult result = BattleTurnDriver.ResolveTurn(manager, new[] { magic }, nextTurnDrawCount: 0,
            onStep: observed.Add);

        // No consumer has played a checkpoint or feedback cue; authoritative state is nevertheless final.
        Assert.That(result.Outcome, Is.EqualTo(BattleTurnOutcome.Victory));
        Assert.That(manager.CurrentPhase, Is.EqualTo(BattlePhase.Finished));
        Assert.That(enemy.IsDead, Is.True);
        Assert.That(enemy.DeathHandled, Is.True);
        Assert.That(player.PlayZone, Is.Empty);
        Assert.That(player.Hand, Is.Empty);
        Assert.That(player.DiscardPile, Does.Contain(card));
        Assert.That(player.Buffs, Is.Empty);
        Assert.That(magic.Casts, Is.EqualTo(1));
        Assert.That(result.Steps, Is.EqualTo(observed));
        Assert.That(result.Steps.Last().Kind, Is.EqualTo(BattleResolutionStepKind.BattleFinished));
        Assert.That(result.InitialState.Phase, Is.EqualTo(BattlePhase.PlayerTurn));
        Assert.That(result.InitialState.FindEnemy(enemy).Health, Is.EqualTo(4));
        Assert.That(result.InitialState.PlayZone.Single(), Is.SameAs(card));
        Assert.That(result.FinalState.FindEnemy(enemy).Health, Is.Zero);
        Assert.That(result.FinalState.Phase, Is.EqualTo(BattlePhase.Finished));

        int finalHealth = player.CurrentHealth;
        int actionIndex = enemy.ActionIndex;
        foreach (BattleResolutionStep step in result.Steps)
        {
            // Reading the trace repeatedly is not a second rule submission.
            Assert.That(step.After, Is.Not.Null);
            PlayCues(step.FeedbackCues);
            if (step.CastResult != null)
                foreach (MagicDamageHitResult hit in step.CastResult.enemyDamageHits) PlayCues(hit.FeedbackCues);
        }
        Assert.That(player.CurrentHealth, Is.EqualTo(finalHealth));
        Assert.That(enemy.ActionIndex, Is.EqualTo(actionIndex));
        Assert.That(magic.Casts, Is.EqualTo(1));
        Assert.That(player.DiscardPile.Count(item => ReferenceEquals(item, card)), Is.EqualTo(1));
    }

    [Test]
    public void EnemyTraceCapturesEveryHitAndEachEnemyGroupBeforeFinalTurnStart()
    {
        PlayerStatus player = Player();
        BattleManager manager = Battle(player);
        EnemyModel first = Enemy(manager, 100, attack: 4, hits: 3);
        EnemyModel second = Enemy(manager, 100, attack: 2, hits: 2);

        BattleTurnResult result = BattleTurnDriver.ResolveTurn(manager, Array.Empty<MagicModel>(), nextTurnDrawCount: 0);
        BattleResolutionStep[] hits = result.Steps.Where(step => step.Kind == BattleResolutionStepKind.EnemyIntentResolved).ToArray();

        Assert.That(result.Outcome, Is.EqualTo(BattleTurnOutcome.NextPlayerTurn));
        Assert.That(hits.Select(step => step.ActingEnemy), Is.EqualTo(new[] { first, first, first, second, second }));
        Assert.That(hits.Select(step => step.EnemyActionIndex), Is.EqualTo(new[] { 0, 0, 0, 1, 1 }));
        Assert.That(hits.Select(step => step.HitIndex), Is.EqualTo(new[] { 0, 1, 2, 0, 1 }));
        Assert.That(hits.Select(step => step.HitCount), Is.EqualTo(new[] { 3, 3, 3, 2, 2 }));
        Assert.That(hits.Select(step => step.Before.PlayerHealth), Is.EqualTo(new[] { 100, 96, 92, 88, 86 }));
        Assert.That(hits.Select(step => step.After.PlayerHealth), Is.EqualTo(new[] { 96, 92, 88, 86, 84 }));
        Assert.That(result.EnemyIntents.Count, Is.EqualTo(5));
        Assert.That(player.CurrentHealth, Is.EqualTo(84));
        Assert.That(first.ActionIndex, Is.EqualTo(1));
        Assert.That(second.ActionIndex, Is.EqualTo(1));
        Assert.That(result.Steps.Last().Kind, Is.EqualTo(BattleResolutionStepKind.PlayerTurnStarted));
        Assert.That(result.FinalState.PlayerHealth, Is.EqualTo(84));
        Assert.That(result.FinalState.Phase, Is.EqualTo(BattlePhase.PlayerTurn));

        player.TakeDirectDamage(20);
        first.TakeDirectDamage(10);
        Assert.That(hits[0].After.PlayerHealth, Is.EqualTo(96));
        Assert.That(hits[0].After.FindEnemy(first).Health, Is.EqualTo(100));
        Assert.That(result.FinalState.PlayerHealth, Is.EqualTo(84));
    }

    [Test]
    public void FrozenBuffAndIntentValuesSurviveLiveMutationAndFinalCombatCleanup()
    {
        PlayerStatus player = Player();
        BattleManager manager = Battle(player);
        EnemyModel enemy = Enemy(manager, 4);
        var intent = new EnemyIntentData
        {
            actionType = EnemyActionType.ApplyBuff, value = 3, times = 1,
            buffs = new[] { new BuffStackData { buffType = BuffEnum.Weak, stack = 7 } },
            descriptionKey = "snapshot.original"
        };
        SetIntents(enemy, intent);
        player.AddBuff(BuffEnum.MagicAttackAll, 2);
        enemy.AddBuff(BuffEnum.ShieldReflect, 3);
        BuffModel livePlayerBuff = player.Buffs[BuffEnum.MagicAttackAll];
        BuffModel liveEnemyBuff = enemy.Buffs[BuffEnum.ShieldReflect];
        Played(player);

        BattleTurnResult result = BattleTurnDriver.ResolveTurn(manager, new[] { new MultiHitMagic(5, 1) },
            nextTurnDrawCount: 0, onStep: null);
        Assert.That(result.Outcome, Is.EqualTo(BattleTurnOutcome.Victory));
        Assert.That(player.Buffs, Is.Empty, "Production victory cleanup has already removed live combat buffs.");
        Assert.That(result.InitialState.PlayerBuffs[BuffEnum.MagicAttackAll].stack, Is.EqualTo(2));
        Assert.That(result.InitialState.FindEnemy(enemy).Buffs[BuffEnum.ShieldReflect].stack, Is.EqualTo(3));
        BattleIntentSnapshot frozenIntent = result.InitialState.FindEnemy(enemy).Intents.Single();
        string frozenDescription = frozenIntent.TooltipDescription;
        string frozenBuffDescription = result.InitialState.PlayerBuffs[BuffEnum.MagicAttackAll].GetDesc();

        livePlayerBuff.stack = 99;
        liveEnemyBuff.stack = 99;
        intent.value = 99;
        intent.buffs[0].stack = 99;
        intent.descriptionKey = "snapshot.changed";
        player.ClearCombatState();
        enemy.ClearCurrentIntents();
        result.InitialState.PlayerBuffs[BuffEnum.MagicAttackAll].stack = -1;
        result.InitialState.FindEnemy(enemy).Buffs[BuffEnum.ShieldReflect].stack = -1;

        Assert.That(player.Buffs, Is.Empty);
        Assert.That(result.InitialState.PlayerBuffs[BuffEnum.MagicAttackAll].stack, Is.EqualTo(2), "Each read is detached, including from a consumer's mutation.");
        Assert.That(result.InitialState.FindEnemy(enemy).Buffs[BuffEnum.ShieldReflect].stack, Is.EqualTo(3));
        Assert.That(result.InitialState.PlayerBuffs[BuffEnum.MagicAttackAll].GetDesc(), Is.EqualTo(frozenBuffDescription));
        Assert.That(frozenIntent.Intent.value, Is.EqualTo(3));
        Assert.That(frozenIntent.Intent.buffs.Single().stack, Is.EqualTo(7));
        Assert.That(frozenIntent.Intent.descriptionKey, Is.EqualTo("snapshot.original"));
        Assert.That(frozenIntent.TooltipDescription, Is.EqualTo(frozenDescription));
        Assert.That(frozenIntent.TooltipBuffs.Single().stack, Is.EqualTo(7));
    }

    [Test]
    public void NewBattleManagersInterleaveWithoutSingletonOrOtherWorldState()
    {
        PlayerStatus firstPlayer = Player(seed: 41011);
        PlayerStatus secondPlayer = Player(seed: 42011);
        BattleManager firstBattle = Battle(firstPlayer);
        BattleManager secondBattle = Battle(secondPlayer); // Deliberately the most recently constructed manager.
        EnemyModel firstEnemy = Enemy(firstBattle, 30);
        EnemyModel secondEnemy = Enemy(secondBattle, 50);
        Played(firstPlayer);
        Played(secondPlayer);

        BattleTurnResult first = BattleTurnDriver.ResolveTurn(firstBattle, new[] { new MultiHitMagic(5, 2) }, nextTurnDrawCount: 0);
        Assert.That(firstEnemy.CurrentHealth, Is.EqualTo(20));
        Assert.That(secondEnemy.CurrentHealth, Is.EqualTo(50));
        Assert.That(firstBattle.ContinuousCastCount, Is.Zero);
        Assert.That(secondBattle.CurrentPhase, Is.EqualTo(BattlePhase.PlayerTurn));
        Assert.That(secondPlayer.PlayZone.Count, Is.EqualTo(1));

        BattleTurnResult second = BattleTurnDriver.ResolveTurn(secondBattle, new[] { new MultiHitMagic(7, 2) }, nextTurnDrawCount: 0);
        Assert.That(secondEnemy.CurrentHealth, Is.EqualTo(36));
        Assert.That(firstEnemy.CurrentHealth, Is.EqualTo(20));
        Assert.That(firstPlayer.Battle, Is.SameAs(firstBattle));
        Assert.That(secondPlayer.Battle, Is.SameAs(secondBattle));
        Assert.That(first.InitialState.Enemies.Single().Enemy, Is.SameAs(firstEnemy));
        Assert.That(second.InitialState.Enemies.Single().Enemy, Is.SameAs(secondEnemy));
        Assert.That(first.FinalState.FindEnemy(secondEnemy), Is.Null);
        Assert.That(second.FinalState.FindEnemy(firstEnemy), Is.Null);

        Played(firstPlayer);
        BattleTurnResult firstAgain = BattleTurnDriver.ResolveTurn(firstBattle, new[] { new MultiHitMagic(3, 1) }, nextTurnDrawCount: 0);
        Assert.That(firstEnemy.CurrentHealth, Is.EqualTo(17));
        Assert.That(secondEnemy.CurrentHealth, Is.EqualTo(36));
        Assert.That(first.FinalState.FindEnemy(firstEnemy).Health, Is.EqualTo(20));
        Assert.That(firstAgain.InitialState.FindEnemy(firstEnemy).Health, Is.EqualTo(20));
        Assert.That(BattleManager.Instance, Is.SameAs(previousSingleton));
    }

    [Test]
    public void MultiHitMagicDefersFeedbackToIndividualFrozenHitCues()
    {
        var playedDamage = new List<string>();
        var services = new PlayerStateServices((health, shield) => playedDamage.Add(health + "/" + shield));
        PlayerStatus player = Player(services: services);
        BattleManager manager = Battle(player);
        EnemyModel enemy = Enemy(manager, 20);
        Played(player);
        var magic = new MultiHitMagic(2, 3);

        BattleTurnResult result = BattleTurnDriver.ResolveTurn(manager, new[] { magic }, nextTurnDrawCount: 0);
        BattleResolutionStep cast = result.Steps.Single(step => step.Kind == BattleResolutionStepKind.AfterMagic && step.CastPerformed);
        MagicDamageHitResult[] hits = cast.CastResult.enemyDamageHits.ToArray();

        Assert.That(playedDamage, Is.Empty, "Resolve commits rules but must not play host feedback.");
        Assert.That(enemy.CurrentHealth, Is.EqualTo(14));
        Assert.That(hits.Select(hit => hit.stepIndex), Is.EqualTo(new[] { 0, 1, 2 }));
        Assert.That(hits.Select(hit => hit.HealthAfter), Is.EqualTo(new[] { 18, 16, 14 }));
        Assert.That(hits.Select(hit => hit.StateAfter.FindEnemy(enemy).Health), Is.EqualTo(new[] { 18, 16, 14 }));
        Assert.That(hits.All(hit => hit.FeedbackCues.Count == 1), Is.True);
        foreach (BattleResolutionStep step in result.Steps) PlayCues(step.FeedbackCues);
        Assert.That(playedDamage, Is.Empty, "Damage cues belong to their hit, not the enclosing AfterMagic step.");

        for (int i = 0; i < hits.Length; i++)
        {
            PlayCues(hits[i].FeedbackCues);
            Assert.That(playedDamage.Count, Is.EqualTo(i + 1));
            Assert.That(playedDamage.Last(), Is.EqualTo("2/0"));
            Assert.That(enemy.CurrentHealth, Is.EqualTo(14), "Cue playback is not another damage submission.");
        }
        enemy.TakeDirectDamage(1);
        Assert.That(hits.Select(hit => hit.HealthAfter), Is.EqualTo(new[] { 18, 16, 14 }));
    }

    [Test]
    public void EnemyDamageFeedbackStaysOnEachHitCheckpointUntilPlayback()
    {
        var playedDamage = new List<int>();
        PlayerStatus player = Player(services: new PlayerStateServices((health, shield) => playedDamage.Add(health)));
        BattleManager manager = Battle(player);
        Enemy(manager, 100, attack: 3, hits: 3);

        BattleTurnResult result = BattleTurnDriver.ResolveTurn(manager, Array.Empty<MagicModel>(), nextTurnDrawCount: 0);
        BattleResolutionStep[] hits = result.Steps.Where(step => step.Kind == BattleResolutionStepKind.EnemyIntentResolved).ToArray();

        Assert.That(player.CurrentHealth, Is.EqualTo(91));
        Assert.That(playedDamage, Is.Empty);
        Assert.That(hits.Length, Is.EqualTo(3));
        Assert.That(hits.All(step => step.FeedbackCues.Count == 1), Is.True);
        for (int i = 0; i < hits.Length; i++)
        {
            PlayCues(hits[i].FeedbackCues);
            Assert.That(playedDamage, Is.EqualTo(Enumerable.Repeat(3, i + 1)));
            Assert.That(player.CurrentHealth, Is.EqualTo(91));
        }
    }

    [Test]
    public void TutorialSecondTurnCommitsStateButDefersChangedUntilPlayerTurnStartedCue()
    {
        PlayerStatus player = Player();
        BattleManager manager = Battle(player);
        Enemy(manager, 100);
        var tutorial = new TutorialPolicy();
        tutorial.Initialize(true);
        tutorial.BeginBattle();
        Assert.That(tutorial.TryApplyFixedTurnHand(player), Is.True);
        tutorial.Show(TutorialStep.BattlePlay, false);
        int changes = 0;
        tutorial.Changed += () => changes++;

        BattleTurnResult result = BattleTurnDriver.ResolveTurn(manager, Array.Empty<MagicModel>(), nextTurnDrawCount: 0, tutorial: tutorial);

        Assert.That(result.Outcome, Is.EqualTo(BattleTurnOutcome.NextPlayerTurn));
        Assert.That(tutorial.State.BattleTurnIndex, Is.EqualTo(2));
        Assert.That(tutorial.State.CurrentStep, Is.EqualTo(TutorialStep.BattleRefresh));
        Assert.That(player.Hand.Select(card => card.material), Is.EqualTo(new[] { MaterialEnum.Earth, MaterialEnum.Earth, MaterialEnum.Earth }));
        Assert.That(result.InitialState.Hand.Select(card => card.material), Is.EqualTo(new[] { MaterialEnum.Fire, MaterialEnum.Fire }));
        Assert.That(changes, Is.Zero, "Changed is a presentation notification, not an early rule-phase popup.");
        BattleResolutionStep nextTurn = result.Steps.Single(step => step.Kind == BattleResolutionStepKind.PlayerTurnStarted);
        foreach (BattleResolutionStep step in result.Steps.TakeWhile(step => !ReferenceEquals(step, nextTurn)))
            PlayCues(step.FeedbackCues);
        Assert.That(changes, Is.Zero);
        Assert.That(nextTurn.FeedbackCues.Count, Is.EqualTo(1));
        PlayCues(nextTurn.FeedbackCues);
        Assert.That(changes, Is.EqualTo(1));
        Assert.That(tutorial.State.BattleTurnIndex, Is.EqualTo(2));
        Assert.That(player.Hand.Count, Is.EqualTo(3));
    }

    [Test]
    public void DirectBattleEntriesBindOwnerAndRestoreForeignAmbientIncludingFailure()
    {
        PlayerStatus owner = Player(seed: 43011);
        PlayerStatus foreign = Player(seed: 44011);
        BattleManager manager = Battle(owner);
        Battle(foreign);
        var observedHooks = new List<string>();
        Action<string> verifyOwner = hook =>
        {
            observedHooks.Add(hook);
            Assert.That(RuleExecutionScope.Current, Is.SameAs(owner.Rules), hook);
            Assert.That(MaterialModifierModel.CurrentContext.PlayerState, Is.SameAs(owner), hook);
            Assert.That(MaterialModifierModel.CurrentContext.BattleManager, Is.SameAs(manager), hook);
        };
        var magic = new MultiHitMagic(1, 1);
        var modifier = new ScopeModifier(verifyOwner);
        magic.AddModifier(modifier);
        owner.MagicBook.Add(magic);
        ScopeEnemy enemy;
        using (owner.Rules.Enter())
            enemy = new ScopeEnemy(new EnemyData { numericId = 94301, string_id = "snapshot_scope", maxHealth = 100 }, verifyOwner);
        manager.AddEnemy(enemy);
        SetIntents(enemy, new EnemyIntentData { actionType = EnemyActionType.Special });
        manager.EnemyAdded += added => verifyOwner("spawn");

        using (foreign.Rules.Enter())
        {
            MaterialModifierContext foreignMaterial = MaterialModifierModel.CurrentContext;
            var foreignMagic = new MagicModifierContext { PlayerState = foreign, BattleManager = foreign.Battle };
            MagicModifierModel.CurrentContext = foreignMagic;
            Action assertForeign = () =>
            {
                Assert.That(RuleExecutionScope.Current, Is.SameAs(foreign.Rules));
                Assert.That(MaterialModifierModel.CurrentContext, Is.SameAs(foreignMaterial));
                Assert.That(MagicModifierModel.CurrentContext, Is.SameAs(foreignMagic));
            };

            manager.BeginBattleRules();
            assertForeign();
            manager.BeginPlayerTurnStartRules(0, () => { verifyOwner("turn-draw-policy"); return true; });
            assertForeign();
            manager.SpawnEnemy(new EnemyData { numericId = 94302, string_id = "snapshot_spawn", maxHealth = 100 });
            assertForeign();
            manager.BeginEnemyAction(enemy);
            assertForeign();
            manager.ResolveEnemyIntentAt(enemy, 0);
            assertForeign();
            manager.ResolveEnemyIntentHitAt(enemy, 0, 0);
            assertForeign();
            manager.EndEnemyAction(enemy);
            assertForeign();
            manager.BeginPlayerResolveRules();
            assertForeign();
            manager.EndPlayerResolveRules();
            assertForeign();
            manager.EndPlayerTurnRules(new List<MaterialModel>());
            assertForeign();
            manager.FinishBattleRules(new List<MaterialModel>());
            assertForeign();

            modifier.ThrowOnStart = true;
            Assert.Throws<InvalidOperationException>(() => manager.BeginBattleRules());
            assertForeign();
        }
        Assert.That(observedHooks, Does.Contain("battle-start"));
        Assert.That(observedHooks, Does.Contain("turn-start"));
        Assert.That(observedHooks, Does.Contain("turn-end"));
        Assert.That(observedHooks, Does.Contain("battle-end"));
        Assert.That(observedHooks, Does.Contain("spawn"));
        Assert.That(observedHooks.Count(hook => hook == "intent"), Is.EqualTo(2));
    }

    private sealed class ScopeModifier : MagicModifierModel
    {
        private readonly Action<string> observe;
        public bool ThrowOnStart;
        public ScopeModifier(Action<string> observe) : base(new MagicModifierData { id = "snapshot_scope" })
        { this.observe = observe; }
        public override void OnBattleStart()
        {
            observe("battle-start");
            if (ThrowOnStart) throw new InvalidOperationException("snapshot deliberate hook failure");
        }
        public override void OnBattleEnd() => observe("battle-end");
        public override void OnTurnStart() => observe("turn-start");
        public override void OnTurnEnd() => observe("turn-end");
    }

    private sealed class ScopeEnemy : EnemyModel
    {
        private readonly Action<string> observe;
        public ScopeEnemy(EnemyData data, Action<string> observe) : base(data) { this.observe = observe; }
        protected override void ProcessSpecialIntent(int value, PlayerState playerState) => observe("intent");
    }

    private sealed class MultiHitMagic : MagicModel
    {
        private readonly int damage;
        private readonly int hits;
        public int Casts { get; private set; }
        public override MagicEffectType EffectType => MagicEffectType.Damage;
        public MultiHitMagic(int damage, int hits)
            : base(new MagicData { numericId = 94201, id = "snapshot_multi_hit", recipe = new[] { MaterialEnum.Earth } })
        { this.damage = damage; this.hits = hits; }
        protected override void ResolveCast(PlayerState player, BattleManager manager, MagicCastResult result)
        {
            Casts++;
            DamageTargetTimes(player, manager, damage, hits, result);
        }
    }
}
