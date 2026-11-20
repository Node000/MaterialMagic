using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;

public class RelicSystemTests
{
    private PlayerStatus player;
    private BattleManager battle;
    private EnemyModel enemy;

    private sealed class ProbeRelic : RelicModel
    {
        public Action<RelicContext, RelicEvent> Handler;
        public readonly List<RelicEvent> Events = new List<RelicEvent>();
        public ProbeRelic(int id) : base(new RelicData { numericId = id }) { }
        public override void OnEvent(RelicContext context, RelicEvent value)
        { Events.Add(value); Handler?.Invoke(context, value); }
    }

    private sealed class TestMagic : MagicModel
    {
        public TestMagic() : base(new MagicData { numericId = 99992, id = "relic_magic_test", recipe = new[] { MaterialEnum.Fire } }, 0) { }
        protected override void ResolveCast(PlayerState state, BattleManager manager, MagicCastResult result)
            => DamageTarget(state, manager, 3, result);
    }

    [SetUp]
    public void SetUp()
    {
        player = new PlayerStatus(50, 20);
        player.SetRunRandomState(12345, 0);
        battle = BattleManager.Create(player);
        enemy = new EnemyModel(new EnemyData { numericId = 99991, string_id = "relic_test", maxHealth = 50 });
        battle.AddEnemy(enemy);
    }
    [TearDown]
    public void TearDown()
    {
        BattleManager.ClearInstance(battle);
        MaterialModifierModel.CurrentContext = null;
        MagicModifierModel.CurrentContext = null;
    }
    private RelicModel Acquire(int id)
    {
        Assert.That(player.TryAcquireRelic(id), Is.EqualTo(RelicAcquireResult.Acquired));
        return player.Relics.Items.Last();
    }
    private ProbeRelic Probe(int id = 99901)
    {
        var value = new ProbeRelic(id);
        player.Relics.TryAcquire(value);
        value.Events.Clear();
        return value;
    }
    private ArrowReadStep Step(MaterialEnum direction = MaterialEnum.None)
    {
        var step = new ArrowReadStep(new MaterialModel(Guid.NewGuid().ToString(), direction), 0);
        if (direction != MaterialEnum.None) step.AddBaseEffectDirection(direction);
        return step;
    }

    [Test] public void DataAndFactoriesAreComplete()
    {
        Assert.That(GameDataDatabase.RelicData.Count, Is.EqualTo(13));
        foreach (var data in GameDataDatabase.RelicData.Values) Assert.That(RelicFactory.Create(data), Is.Not.Null);
        Assert.That(RelicFactory.Create(new RelicData { numericId = 99, script = "missing" }), Is.Null);
    }
    [Test] public void UniqueAcquisitionCompensatesWithoutGoldBonus()
    {
        Acquire(5);
        int before = player.Gold;
        Assert.That(player.TryAcquireRelic(5), Is.EqualTo(RelicAcquireResult.DuplicateCompensated));
        Assert.That(player.Gold - before, Is.EqualTo(3));
        Assert.That(player.Relics.Items.Count, Is.EqualTo(1));
    }
    [Test] public void OpeningShieldSurvivesFirstTurnClearButDoesNotRepeat()
    {
        Acquire(1);
        battle.BeginBattleRules();
        battle.BeginPlayerTurnStartRules(0, null);
        Assert.That(player.Shield, Is.EqualTo(5));
        battle.BeginPlayerTurnStartRules(0, null);
        Assert.That(player.Shield, Is.Zero);
        battle.ClearEnemies(); battle.AddEnemy(enemy);
        battle.BeginBattleRules(); battle.BeginPlayerTurnStartRules(0, null);
        Assert.That(player.Shield, Is.EqualTo(5));
    }
    [TestCase(10, MaterialEnum.Earth)]
    [TestCase(11, MaterialEnum.Fire)]
    [TestCase(12, MaterialEnum.Water)]
    [TestCase(13, MaterialEnum.Wind)]
    public void TemporaryArrowRelicGrantsOnOddTurnsAndRestartsEachBattle(int id, MaterialEnum direction)
    {
        var relic = Acquire(id);
        var probe = Probe();
        var regular = player.AddDeckMaterial(MaterialEnum.Water);
        Assert.That(player.Hand, Is.Empty);
        battle.BeginBattleRules();

        for (int turn = 1; turn <= 5; turn++)
        {
            probe.Events.Clear();
            battle.BeginPlayerTurnStartRules(1, null);
            bool shouldGrant = turn % 2 != 0;
            Assert.That(player.Hand, Has.Count.EqualTo(shouldGrant ? 2 : 1), $"Turn {turn}");
            Assert.That(player.Hand[0], Is.SameAs(regular));
            Assert.That(probe.Events.Where(x => x.Kind == RelicEventKind.CardDrawn).Select(x => x.Card), Is.EqualTo(new[] { regular }));
            if (shouldGrant)
            {
                MaterialModel granted = player.Hand[1];
                Assert.That(granted.material, Is.EqualTo(direction));
                Assert.That(granted.HasModifier<TemporaryModifier>() && granted.IgnoresPlayLimit(), Is.True);
                Assert.That(player.Deck.Contains(granted), Is.False);
                Assert.That(probe.Events.Single(x => x.Kind == RelicEventKind.CardEnteredHand && x.Card == granted).OriginRelicId,
                    Is.EqualTo(relic.InstanceId));
            }
            battle.EndPlayerTurnRules(null);
        }

        battle.FinishBattleRules(null);
        Assert.That(player.ConsumedPile, Is.Empty);
        Assert.That(player.Deck, Is.EqualTo(new[] { regular }));
        battle.ClearEnemies();
        battle.AddEnemy(enemy);
        battle.BeginBattleRules();
        battle.BeginPlayerTurnStartRules(0, null);
        Assert.That(player.Hand.Single().material, Is.EqualTo(direction));
    }
    [Test] public void TemporaryArrowRelicsUseOnlyRemainingHandSpaceWithoutDeferring()
    {
        for (int id = 10; id <= 13; id++) Acquire(id);
        var regular = player.AddDeckMaterial(MaterialEnum.Earth);
        battle.BeginBattleRules();
        player.AddBuff(BuffEnum.HandLimitNextTurn, 2);
        battle.BeginPlayerTurnStartRules(1, null);
        Assert.That(player.Hand, Has.Count.EqualTo(2));
        Assert.That(player.Hand[0], Is.SameAs(regular));
        Assert.That(player.Hand[1].material, Is.EqualTo(MaterialEnum.Earth));
        Assert.That(player.Hand[1].isTemporary, Is.True);
        battle.EndPlayerTurnRules(null);

        battle.BeginPlayerTurnStartRules(1, null);
        Assert.That(player.Hand, Is.EqualTo(new[] { regular }));
        battle.EndPlayerTurnRules(null);

        battle.BeginPlayerTurnStartRules(1, null);
        Assert.That(player.Hand.Skip(1).Select(x => x.material),
            Is.EqualTo(new[] { MaterialEnum.Earth, MaterialEnum.Fire, MaterialEnum.Water, MaterialEnum.Wind }));
        Assert.That(player.Hand.Skip(1).All(x => x.isTemporary), Is.True);
    }
    [Test] public void TemporaryArrowRelicKeepsItsCadenceWhenRestored()
    {
        var relic = Acquire(10);
        battle.BeginBattleRules();
        battle.BeginPlayerTurnStartRules(0, null);
        battle.EndPlayerTurnRules(null);

        var saved = JsonConvert.DeserializeObject<RelicSaveData[]>(JsonConvert.SerializeObject(player.Relics.Export()));
        player.Relics.Restore(saved);
        Assert.That(player.Relics.Items.Single().InstanceId, Is.EqualTo(relic.InstanceId));
        Assert.That(player.Hand, Is.Empty);
        battle.BeginPlayerTurnStartRules(0, null);
        Assert.That(player.Hand, Is.Empty);
        battle.EndPlayerTurnRules(null);
        battle.BeginPlayerTurnStartRules(0, null);
        Assert.That(player.Hand.Single().material, Is.EqualTo(MaterialEnum.Earth));
    }
    [Test] public void TemporaryArrowRelicAcquiredMidBattleWaitsForNextBattle()
    {
        battle.BeginBattleRules();
        battle.BeginPlayerTurnStartRules(0, null);
        Acquire(13);
        battle.BeginPlayerTurnStartRules(0, null);
        battle.BeginPlayerTurnStartRules(0, null);
        Assert.That(player.Hand, Is.Empty);

        battle.FinishBattleRules(null);
        battle.ClearEnemies();
        battle.AddEnemy(enemy);
        battle.BeginBattleRules();
        battle.BeginPlayerTurnStartRules(0, null);
        Assert.That(player.Hand.Single().material, Is.EqualTo(MaterialEnum.Wind));
    }
    [Test] public void TemporaryArrowTestPoolContainsAllFourRelics()
    {
        var offer = RelicOfferService.Create(player, "relic_temporary_arrows_test", 4);
        Assert.That(offer.relicIds, Is.EquivalentTo(new[] { 10, 11, 12, 13 }));
    }
    [Test] public void RelicBuffUsesSourceAmplifierAndFoamShield()
    {
        Acquire(6);
        battle.BeginBattleRules();
        player.AddBuff(BuffEnum.ExtraEnemyDebuff, 1);
        player.AddBuff(BuffEnum.FoamShield, 1);
        player.Relics.PrepareTurn();
        Assert.That(enemy.GetBuffStack(BuffEnum.Burning), Is.EqualTo(4));
        Assert.That(player.Shield, Is.EqualTo(4));
    }
    [Test] public void BuffApplicationFeedsRelicThenExistingBuff()
    {
        Acquire(7);
        player.AddBuff(BuffEnum.SpellPowerOnExtraDraw, 1);
        var source = new CombatantModel(player);
        enemy.AddBuff(BuffEnum.Weak, 1, source);
        enemy.AddBuff(BuffEnum.Vulnerable, 1, source);
        Assert.That(player.GetBuffStack(BuffEnum.ExtraDraw), Is.EqualTo(1));
        Assert.That(player.GetBuffStack(BuffEnum.SpellPower), Is.EqualTo(1));
    }
    [Test] public void ArrowPulseCountsActualReadsNotBuildOrReplay()
    {
        var relic = Acquire(2);
        var step = Step();
        ArrowReadSystem.BuildSequence(new[] { step.SourceCard }, player, battle);
        Assert.That(relic.DisplayCounter, Is.Zero);
        ArrowStepRuleResolver.Resolve(player, battle, step);
        ArrowStepRuleResolver.Resolve(player, battle, step, true);
        Assert.That(relic.DisplayCounter, Is.EqualTo(1));
        ArrowStepRuleResolver.Resolve(player, battle, step);
        ArrowStepRuleResolver.Resolve(player, battle, step);
        Assert.That(enemy.CurrentHealth, Is.EqualTo(46));
        Assert.That(relic.DisplayCounter, Is.Zero);
    }
    [Test] public void EffectDamageUsesVulnerableAndShieldButNotAttackBuffs()
    {
        Acquire(2);
        player.AddBuff(BuffEnum.SpellPower, 20);
        player.AddBuff(BuffEnum.Weak, 10);
        enemy.AddBuff(BuffEnum.Vulnerable, 2);
        enemy.GainShield(3);
        for (int i = 0; i < 3; i++) ArrowStepRuleResolver.Resolve(player, battle, Step());
        Assert.That(enemy.CurrentHealth, Is.EqualTo(47));
        Assert.That(enemy.Shield, Is.Zero);
        Assert.That(player.GetBuffStack(BuffEnum.Weak), Is.EqualTo(10));
        Assert.That(enemy.GetBuffStack(BuffEnum.Vulnerable), Is.EqualTo(1));
    }
    [Test] public void RetaliationRequiresActualHealthDamageAndLivingOwner()
    {
        Acquire(4);
        player.GainShield(5);
        player.TakeDamageResult(3, new CombatantModel(enemy));
        Assert.That(enemy.CurrentHealth, Is.EqualTo(50));
        player.TakeDamageResult(3, new CombatantModel(enemy));
        Assert.That(enemy.CurrentHealth, Is.EqualTo(47));
        player.TakeDamageResult(100, new CombatantModel(enemy));
        Assert.That(enemy.CurrentHealth, Is.EqualTo(47));
    }
    [Test] public void HealthLossDoesNotTriggerOrdinaryRetaliation()
    {
        Acquire(4); player.GainShield(10); player.TakeDirectDamage(3);
        Assert.That(player.Shield, Is.EqualTo(10));
        Assert.That(enemy.CurrentHealth, Is.EqualTo(50));
    }
    [Test] public void FirstDrawDoesNotRecurseOrCountRefresh()
    {
        Acquire(3);
        for (int i = 0; i < 8; i++) player.DrawPile.Add(new MaterialModel("draw" + i, MaterialEnum.Fire));
        player.Relics.PrepareTurn();
        Assert.That(player.DrawCardsForRefresh(1), Is.EqualTo(1));
        Assert.That(player.Hand.Count, Is.EqualTo(1));
        player.DrawCards(1);
        Assert.That(player.Hand.Count, Is.EqualTo(3));
        player.DrawCards(1);
        Assert.That(player.Hand.Count, Is.EqualTo(4));
        Assert.That(player.Relics.IsSettled, Is.True);
    }
    [Test] public void CounterAndInstanceIdentityRoundTripWithoutAcquisitionReplay()
    {
        var relic = Acquire(2);
        ArrowStepRuleResolver.Resolve(player, battle, Step());
        string json = JsonConvert.SerializeObject(player.Relics.Export());
        var copy = new PlayerStatus();
        int rng = copy.RunRandomStep;
        copy.Relics.Restore(JsonConvert.DeserializeObject<RelicSaveData[]>(json));
        Assert.That(copy.Relics.Items[0].DisplayCounter, Is.EqualTo(1));
        Assert.That(copy.Relics.Items[0].InstanceId, Is.EqualTo(relic.InstanceId));
        Assert.That(copy.RunRandomStep, Is.EqualTo(rng));
        Assert.That(copy.Gold, Is.Zero);
    }
    [Test] public void UnknownStateVersionIsPreservedAsInactive()
    {
        var saved = new RelicSaveData { numericId = 2, instanceId = "future", stateVersion = 99,
            values = new Dictionary<string, int> { { "future", 42 } } };
        player.Relics.Restore(new[] { saved });
        Assert.That(player.Relics.Items[0].IsEnabled, Is.False);
        var copy = player.Relics.Export()[0];
        Assert.That(copy.stateVersion, Is.EqualTo(99));
        Assert.That(copy.values["future"], Is.EqualTo(42));
    }
    [Test] public void WeightedOfferIsUniqueSavedAndClaimIsIdempotent()
    {
        var offer = RelicOfferService.Create(player, "relic_test", 3, true);
        Assert.That(offer.relicIds.Distinct().Count(), Is.EqualTo(3));
        int step = player.RunRandomStep;
        var copy = JsonConvert.DeserializeObject<RelicOfferSaveData>(JsonConvert.SerializeObject(offer));
        Assert.That(player.RunRandomStep, Is.EqualTo(step));
        Assert.That(RelicOfferService.TryClaim(player, copy, 0), Is.EqualTo(RelicAcquireResult.Acquired));
        Assert.That(RelicOfferService.TryClaim(player, copy, 0), Is.EqualTo(RelicAcquireResult.Invalid));
        Assert.That(player.Gold, Is.EqualTo(12));
    }
    [Test] public void GoldBonusIsOncePerGainNotRefundOrSpend()
    {
        Acquire(5);
        player.AddGold(3, false);
        Assert.That(player.Gold, Is.EqualTo(24));
        player.AddGold(3, false, RelicGoldReason.Refund);
        Assert.That(player.Gold, Is.EqualTo(27));
        player.AddGold(-2, false);
        Assert.That(player.Gold, Is.EqualTo(25));
    }
    [Test] public void DispatchPreservesAcquisitionOrderAndBlocksCycle()
    {
        var order = new List<int>();
        var a = Probe(99101); var b = Probe(99102);
        a.Handler = (c, e) => { if (e.Kind == RelicEventKind.NodeCompleted) { order.Add(1); c.GainGold(1); }
            if (e.Kind == RelicEventKind.GoldChanged) c.GainShield(1); };
        b.Handler = (c, e) => { if (e.Kind == RelicEventKind.NodeCompleted) order.Add(2);
            if (e.Kind == RelicEventKind.ShieldGained) c.GainGold(1); };
        player.Relics.Publish(new RelicEvent(RelicEventKind.NodeCompleted));
        Assert.That(order, Is.EqualTo(new[] { 1, 2 }));
        Assert.That(player.Relics.IsSettled, Is.True);
        Assert.That(player.Gold, Is.EqualTo(22));
        Assert.That(player.Shield, Is.EqualTo(1));
    }
    [Test] public void BuffDecayIsObservableAndDoesNotDoubleReportConsume()
    {
        var probe = Probe();
        player.AddBuff(BuffEnum.Weak, 8);
        probe.Events.Clear();
        player.TriggerOnTurnEnd(null);
        var changes = probe.Events.Where(x => x.Kind == RelicEventKind.BuffChanged && x.BuffType == BuffEnum.Weak).ToArray();
        Assert.That(changes.Length, Is.EqualTo(1));
        Assert.That(changes[0].Amount, Is.EqualTo(-4));
        probe.Events.Clear();
        player.ConsumeBuff(BuffEnum.Weak, 2);
        Assert.That(probe.Events.Count(x => x.Kind == RelicEventKind.BuffChanged), Is.EqualTo(1));
    }
    [Test] public void SavingInsideOperationIsRejected()
    {
        using (player.Relics.BeginOperation())
            Assert.Throws<InvalidOperationException>(() => player.Relics.Export());
        Assert.That(player.Relics.IsSettled, Is.True);
    }
    [Test] public void LegacyPlayerSaveRestoresEmptyRelics()
    {
        var restored = RunSaveSystem.CreatePlayerStatus(new RunSaveData { version = 3,
            player = new PlayerSaveData { maxHealth = 50, currentHealth = 40 } });
        Assert.That(restored.Relics.Items, Is.Empty);
    }
    [Test] public void DamagePreviewDoesNotPublishFactsOrSpendState()
    {
        var probe = Probe();
        player.AddBuff(BuffEnum.Vulnerable, 4);
        probe.Events.Clear(); int random = player.RunRandomStep;
        for (int i = 0; i < 4; i++)
        {
            int amount = 3;
            player.TriggerOnTakeDamage(new CombatantModel(enemy), ref amount);
            Assert.That(amount, Is.EqualTo(7));
        }
        Assert.That(player.GetBuffStack(BuffEnum.Vulnerable), Is.EqualTo(4));
        Assert.That(player.RunRandomStep, Is.EqualTo(random));
        Assert.That(probe.Events, Is.Empty);
    }
    [Test] public void DeathFromRelicUsesExistingDeathQueue()
    {
        Acquire(2);
        enemy.TakeDirectDamage(48);
        for (int i = 0; i < 3; i++) ArrowStepRuleResolver.Resolve(player, battle, Step());
        Assert.That(enemy.IsDead, Is.True);
        Assert.That(enemy.DeathHandled, Is.False);
        Assert.That(battle.HasPendingDeaths, Is.True);
    }
    [Test] public void FirstMagicBonusIsConsumedByCastNotArrowOrPreview()
    {
        Acquire(9); battle.BeginBattleRules();
        ArrowStepRuleResolver.Resolve(player, battle, Step(MaterialEnum.Fire));
        Assert.That(enemy.CurrentHealth, Is.EqualTo(47));
        var magic = new TestMagic();
        Assert.That(player.Relics.ModifyMagicAttack(magic, 3), Is.EqualTo(3));
        magic.Cast(player, battle);
        Assert.That(enemy.CurrentHealth, Is.EqualTo(42));
        magic.Cast(player, battle);
        Assert.That(enemy.CurrentHealth, Is.EqualTo(39));
    }
    [Test] public void CommittedDamageFactsPrecedeNestedReflectionAndCarryActualValues()
    {
        var probe = Probe();
        player.AddBuff(BuffEnum.ShieldReflect, 1);
        player.GainShield(2);
        probe.Events.Clear();
        player.TakeDamageResult(4, new CombatantModel(enemy));
        var damage = probe.Events.Where(x => x.Kind == RelicEventKind.DamageResolved).ToArray();
        Assert.That(damage.Length, Is.EqualTo(2));
        Assert.That(damage[0].Target.Player, Is.SameAs(player));
        Assert.That(damage[0].Amount, Is.EqualTo(2));
        Assert.That(damage[0].ShieldDamage, Is.EqualTo(2));
        Assert.That(damage[0].RawDamage, Is.EqualTo(4));
        Assert.That(damage[1].Target.Enemy, Is.SameAs(enemy));
        Assert.That(damage[0].EventId, Is.LessThan(damage[1].EventId));
    }
    [Test] public void RuntimeBuffPrivateCounterRestoresWithoutHooks()
    {
        player.AddBuff(BuffEnum.ExtraDrawOnEnemyDamage, 1);
        int amount = 1;
        player.TriggerAfterAttack(new CombatantModel(enemy), ref amount);
        var saved = BuffRuntimeSaveData.Export(player.Buffs);
        var copy = new PlayerState();
        copy.RestoreBattleBuffs(JsonConvert.DeserializeObject<BuffRuntimeSaveData[]>(JsonConvert.SerializeObject(saved)));
        Assert.That(copy.GetBuffStack(BuffEnum.ExtraDraw), Is.Zero);
        copy.TriggerAfterAttack(new CombatantModel(enemy), ref amount);
        Assert.That(copy.GetBuffStack(BuffEnum.ExtraDraw), Is.EqualTo(1));
    }
    [Test] public void RestoredNextDrawBuffKeepsArmedState()
    {
        var buff = new ChargeNextDrawBuffModel(2);
        buff.OnTurnStart(new CombatantModel(player), null);
        var copy = new ChargeNextDrawBuffModel(2);
        copy.RestoreRuntimeState(buff.ExportRuntimeState());
        var card = new MaterialModel("charge_save", MaterialEnum.Fire);
        copy.AfterDraw(new CombatantModel(player), card);
        Assert.That(card.HasModifier<ChargeModifier>(), Is.True);
        Assert.That(copy.stack, Is.EqualTo(1));
    }
    [Test] public void FailureMarksInventoryUnsavableInsteadOfSavingPartialResolution()
    {
        var probe = Probe();
        probe.Handler = (c, e) => { if (e.Kind == RelicEventKind.NodeCompleted) throw new InvalidOperationException("test failure"); };
        Assert.Throws<InvalidOperationException>(() => player.Relics.Publish(new RelicEvent(RelicEventKind.NodeCompleted)));
        Assert.That(player.Relics.IsFaulted, Is.True);
        Assert.Throws<InvalidOperationException>(() => player.Relics.Export());
    }
    [Test] public void NewRelicDoesNotReceiveAlreadyQueuedFact()
    {
        var later = new ProbeRelic(99111);
        using (player.Relics.BeginOperation())
        {
            player.Relics.Publish(new RelicEvent(RelicEventKind.NodeCompleted));
            player.Relics.TryAcquire(later);
        }
        Assert.That(later.Events.Any(x => x.Kind == RelicEventKind.NodeCompleted), Is.False);
        Assert.That(later.Events.Any(x => x.Kind == RelicEventKind.Acquired), Is.True);
    }
    [Test] public void OfferInsufficientFundsAndInvalidInputsDoNotCommit()
    {
        var offer = RelicOfferService.Create(player, "relic_test", 3, true);
        player.AddGold(-player.Gold, false);
        Assert.That(RelicOfferService.TryClaim(player, offer, 0), Is.EqualTo(RelicAcquireResult.Invalid));
        Assert.That(offer.committed, Is.False);
        Assert.That(RelicOfferService.TryClaim(player, new RelicOfferSaveData { relicIds = null }, 0), Is.EqualTo(RelicAcquireResult.Invalid));
        Assert.That(RelicOfferService.Create(player, null, 3), Is.Null);
    }
    [Test] public void StaleOfferCloneCannotClaimAgainOrChangePrice()
    {
        var offer = RelicOfferService.Create(player, "relic_test", 3, true);
        var stale = JsonConvert.DeserializeObject<RelicOfferSaveData>(JsonConvert.SerializeObject(offer));
        stale.prices[0] = 0;
        Assert.That(RelicOfferService.TryClaim(player, stale, 0), Is.EqualTo(RelicAcquireResult.Acquired));
        Assert.That(player.Gold, Is.EqualTo(12));
        Assert.That(RelicOfferService.TryClaim(player, stale, 0), Is.EqualTo(RelicAcquireResult.Invalid));
        Assert.That(player.Gold, Is.EqualTo(12));
    }
    [Test] public void PendingOfferDoesNotRerollOrAdvanceRng()
    {
        var offer = RelicOfferService.Create(player, "relic_test", 3);
        int step = player.RunRandomStep;
        Assert.That(RelicOfferService.Create(player, "relic_test", 3), Is.SameAs(offer));
        Assert.That(player.RunRandomStep, Is.EqualTo(step));
    }
    [Test] public void RunSaveRestoresRelicsAndPendingOfferWithoutAcquiredEffect()
    {
        Acquire(2);
        ArrowStepRuleResolver.Resolve(player, battle, Step());
        var offer = RelicOfferService.Create(player, "relic_test", 3);
        var dto = new RunSaveData { version = 4, player = new PlayerSaveData {
            maxHealth = 50, currentHealth = 40, gold = 20, relics = player.Relics.Export(), pendingRelicOffer = offer,
            runRandomSeed = player.RunRandomSeed, runRandomStep = player.RunRandomStep } };
        var restored = RunSaveSystem.CreatePlayerStatus(JsonConvert.DeserializeObject<RunSaveData>(JsonConvert.SerializeObject(dto)));
        Assert.That(restored.Relics.Items[0].DisplayCounter, Is.EqualTo(1));
        Assert.That(restored.PendingRelicOffer.offerId, Is.EqualTo(offer.offerId));
        Assert.That(restored.RunRandomStep, Is.EqualTo(player.RunRandomStep));
        Assert.That(restored.CurrentHealth, Is.EqualTo(40));
    }
    [Test] public void BattleSnapshotRestoresBuffsWithoutPublishingApplications()
    {
        player.AddBuff(BuffEnum.SpellPower, 3);
        var saved = new BattleNodeSaveData { enemies = Array.Empty<EnemyBattleSaveData>(),
            playerCombat = new PlayerCombatSaveData { runtimeBuffs = BuffRuntimeSaveData.Export(player.Buffs) } };
        player.ClearBuffs();
        var probe = Probe();
        RunSaveSystem.RestoreBattle(saved, battle, player);
        Assert.That(player.GetBuffStack(BuffEnum.SpellPower), Is.EqualTo(3));
        Assert.That(probe.Events, Is.Empty);
    }
    [Test] public void NodeCompletionIsIdempotentAndClearsStateBeforeDispatch()
    {
        var run = new RunManager(player);
        var probe = Probe();
        run.BeginLevel(new LevelData { numericId = 1234 });
        probe.Handler = (c, e) => { if (e.Kind == RelicEventKind.NodeCompleted) Assert.That(run.CurrentLevel, Is.Null); };
        run.ClearCurrentLevel(); run.ClearCurrentLevel();
        Assert.That(probe.Events.Count(x => x.Kind == RelicEventKind.NodeCompleted), Is.EqualTo(1));
    }
    [Test] public void RelicInstanceCannotBeSharedBetweenPlayers()
    {
        var relic = Acquire(2);
        var other = new PlayerState();
        Assert.That(other.Relics.TryAcquire(relic), Is.EqualTo(RelicAcquireResult.Invalid));
        Assert.That(other.Relics.Items, Is.Empty);
    }
    [Test] public void ExhaustedPoolReturnsNoOfferWithoutRngStep()
    {
        foreach (int id in GameDataDatabase.RelicData.Keys) Acquire(id);
        int step = player.RunRandomStep;
        Assert.That(RelicOfferService.Create(player, "relic_test", 3), Is.Null);
        Assert.That(player.RunRandomStep, Is.EqualTo(step));
    }
    [Test] public void BattleEndHealingIsOnceAndRequiresEliteVictory()
    {
        Acquire(8);
        battle.ConfigureLevelContext(new LevelData { levelType = LevelType.Elite }, 1, false);
        player.TakeDirectDamage(10);
        player.Relics.EndBattle(true); player.Relics.EndBattle(true);
        Assert.That(player.CurrentHealth, Is.EqualTo(43));
    }
}
