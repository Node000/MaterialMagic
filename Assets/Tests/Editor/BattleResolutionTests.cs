using System;
using System.Collections.Generic;
using NUnit.Framework;

/// <summary>Exercises the shared production schedule; tests never reproduce the scheduling loop.</summary>
public class BattleResolutionTests
{
    private BattleManager battle;

    [TearDown]
    public void TearDown()
    {
        BattleManager.ClearInstance(battle);
        MaterialModifierModel.CurrentContext = null;
        MagicModifierModel.CurrentContext = null;
    }

    [Test]
    public void NoSceneSession_BuildsOnce_InvokesTokensBeforeLock_RegistersEachMatchNotEachReplay()
    {
        PlayerState player = CreatePlayer();
        EnemyModel first = AddEnemy(100);
        EnemyModel selected = AddEnemy(100);
        var probe = new ArrowProbe { FocusOnInvoke = selected, ReplayCount = 1, TokenCount = 2 };
        MaterialModel card = AddCard(player, MaterialEnum.Earth, probe);
        player.AddBuff(BuffEnum.RepeatSpell, 2);
        var firstMagic = new ProbeMagic("first", MaterialEnum.Earth);
        var secondMagic = new ProbeMagic("second", MaterialEnum.Earth);
        var casts = new List<MagicModel>();
        var phases = new List<BattleResolutionStepKind>();

        using (var session = new BattlePlayZoneResolutionSession(player, battle, player.PlayZone,
            new MagicModel[] { firstMagic, secondMagic }, new[] { 1, 0 }))
        {
            Assert.That(session.ReadSequence, Is.Null, "Construction must not build or invoke read hooks.");
            Assert.That(session.MoveNext(out BattleResolutionStep before), Is.True);
            Assert.That(before.Kind, Is.EqualTo(BattleResolutionStepKind.BeforeArrow));
            Assert.That(probe.BeforeReadCount, Is.EqualTo(1));
            Assert.That(player.Shield, Is.Zero, "BeforeArrow must precede the base rule.");
            session.RunToCompletion(step =>
            {
                phases.Add(step.Kind);
                if (step.Kind == BattleResolutionStepKind.BeforeMagicFill)
                    Assert.That(battle.CurrentCastTarget, Is.Null);
                if (step.Kind == BattleResolutionStepKind.BeforeMagicImpact)
                {
                    Assert.That(probe.InvokeSawUnlockedTarget, Is.True);
                    Assert.That(step.Target, Is.SameAs(selected));
                }
                if (step.Kind == BattleResolutionStepKind.AfterMagic)
                    casts.Add(step.Magic);
            });
        }

        Assert.That(probe.BeforeReadCount, Is.EqualTo(1), "Main and replay share one BuildSequence.");
        Assert.That(probe.BaseCount, Is.EqualTo(2), "One base rule per read step, not per token.");
        Assert.That(probe.InvokeCount, Is.EqualTo(8)); // 2 tokens * 2 magic matches * main/replay
        Assert.That(firstMagic.CastCount, Is.EqualTo(12));
        Assert.That(secondMagic.CastCount, Is.EqualTo(12));
        Assert.That(battle.ContinuousCastCount, Is.EqualTo(8));
        Assert.That(player.Shield, Is.EqualTo(6));
        Assert.That(first.CurrentHealth, Is.EqualTo(100));
        Assert.That(casts, Is.EqualTo(new[] { secondMagic, firstMagic, secondMagic, firstMagic,
            secondMagic, firstMagic, secondMagic, firstMagic }));
        Assert.That(phases.IndexOf(BattleResolutionStepKind.AfterArrow), Is.LessThan(phases.IndexOf(BattleResolutionStepKind.BeforeMagicFill)));
    }

    [Test]
    public void NoSceneMagic_UsesBaseEffectBeforeMatchingAcrossUnreadFutureTokens()
    {
        PlayerState player = CreatePlayer();
        EnemyModel enemy = AddEnemy(50);
        AddCard(player, MaterialEnum.Fire);
        AddCard(player, MaterialEnum.Earth);
        var magic = new DamageProbeMagic(new[] { MaterialEnum.Fire, MaterialEnum.Earth }, 5)
        {
            BeforeDamage = () =>
            {
                Assert.That(enemy.CurrentHealth, Is.EqualTo(47), "The first arrow's base damage precedes its token match.");
                Assert.That(player.Shield, Is.Zero, "A matching future token does not execute its base effect early.");
            }
        };
        using (var session = new BattlePlayZoneResolutionSession(player, battle, player.PlayZone, new MagicModel[] { magic }))
            session.RunToCompletion();
        Assert.That(enemy.CurrentHealth, Is.EqualTo(42));
        Assert.That(player.Shield, Is.EqualTo(3));
        Assert.That(magic.CastCount, Is.EqualTo(1));
        Assert.That(battle.ContinuousCastCount, Is.EqualTo(1));
    }

    [Test]
    public void ArrowDeathSummon_IsResolvedBeforeVictoryDecisionAndRecipeCast()
    {
        PlayerState player = CreatePlayer();
        var summoned = new EnemyModel(EnemyData(30));
        var dying = new DeathProbe(EnemyData(1)) { OnDeath = () => battle.SpawnEnemy(summoned) };
        battle.AddEnemy(dying);
        AddCard(player, MaterialEnum.Fire);
        var magic = new DamageProbeMagic(new[] { MaterialEnum.Fire }, 5);
        using (var session = new BattlePlayZoneResolutionSession(player, battle, player.PlayZone, new MagicModel[] { magic }))
            session.RunToCompletion();
        Assert.That(dying.DeathCount, Is.EqualTo(1));
        Assert.That(summoned.CurrentHealth, Is.EqualTo(25), "A death summon prevents early victory and is available to the recipe cast.");
        Assert.That(magic.CastCount, Is.EqualTo(1));
        Assert.That(battle.HasPendingDeaths, Is.False);
    }

    [Test]
    public void DeathChain_IsDrainedBeforeNextMagic_AndSnapshotsStayAtTheirCapturedValues()
    {
        PlayerState player = CreatePlayer();
        EnemyModel survivor = AddEnemy(50);
        var second = new DeathProbe(EnemyData(1));
        var first = new DeathProbe(EnemyData(1)) { OnDeath = () => second.Kill() };
        battle.AddEnemy(first);
        battle.AddEnemy(second);
        AddCard(player, MaterialEnum.Earth);
        var kill = new ProbeMagic("kill", MaterialEnum.Earth) { OnCast = () => first.Kill() };
        var observe = new ProbeMagic("observe", MaterialEnum.Earth)
        {
            OnCast = () =>
            {
                Assert.That(first.DeathHandled, Is.True);
                Assert.That(second.DeathHandled, Is.True);
                Assert.That(battle.HasPendingDeaths, Is.False);
            }
        };
        var deaths = new List<EnemyModel>();
        BattleStateSnapshot snapshot = null;
        using (var session = new BattlePlayZoneResolutionSession(player, battle, player.PlayZone, new MagicModel[] { kill, observe }))
        {
            session.RunToCompletion(step =>
            {
                if (step.Kind == BattleResolutionStepKind.DeathPending)
                    Assert.That(step.DeathEntry.Enemy.DeathHandled, Is.False);
                if (step.Kind == BattleResolutionStepKind.DeathResolved)
                {
                    deaths.Add(step.DeathEntry.Enemy);
                    Assert.That(step.DeathEntry.Enemy.DeathHandled, Is.True);
                }
                if (step.Kind == BattleResolutionStepKind.AfterArrow)
                    snapshot = step.After;
            });
        }
        Assert.That(deaths, Is.EqualTo(new EnemyModel[] { first, second }));
        Assert.That(first.DeathCount, Is.EqualTo(1));
        Assert.That(second.DeathCount, Is.EqualTo(1));
        Assert.That(observe.CastCount, Is.EqualTo(1));
        Assert.That(snapshot.Enemies[1].Health, Is.EqualTo(1));
        Assert.That(snapshot.Enemies[1].DeathHandled, Is.False);
        Assert.That(survivor.IsDead, Is.False);
    }

    [Test]
    public void EarlyVictory_AppliesOnlyAlreadyReadMainSequenceAfterActions_ExactlyOnce()
    {
        PlayerState player = CreatePlayer();
        AddEnemy(1);
        MaterialModel lethal = AddCard(player, MaterialEnum.Fire, new FragileArrowModifier());
        lethal.AddModifier(new RepeatArrowModifier());
        MaterialModel unread = AddCard(player, MaterialEnum.Earth, new EternalArrowModifier());
        BattleResolutionStep afterRead = null;
        int arrows = 0;
        using (var session = new BattlePlayZoneResolutionSession(player, battle, player.PlayZone, new MagicModel[0]))
        {
            session.RunToCompletion(step =>
            {
                if (step.Kind == BattleResolutionStepKind.AfterArrow)
                    arrows++;
                if (step.Kind == BattleResolutionStepKind.AfterRead)
                    afterRead = step;
            });
            Assert.That(session.MoveNext(out _), Is.False);
        }
        Assert.That(arrows, Is.EqualTo(1), "Victory skips unread base steps and all replay sequences.");
        Assert.That(afterRead.AfterReadActions.Count, Is.EqualTo(1));
        Assert.That(afterRead.AfterReadActions[0].Card, Is.SameAs(lethal));
        Assert.That(afterRead.AfterReadActions[0].Applied, Is.True);
        Assert.That(player.ConsumedPile, Does.Contain(lethal));
        Assert.That(player.DiscardPile.Count, Is.EqualTo(2), "Fragile splits once even with a replay.");
        Assert.That(player.PlayZone, Does.Contain(unread));
        Assert.That(player.TemporaryMaterialsNextTurn.Count, Is.Zero);
    }

    [Test]
    public void MainAndReplay_AfterReadActionIsDeduplicatedBySourceCard()
    {
        PlayerState player = CreatePlayer();
        AddEnemy(100);
        MaterialModel card = AddCard(player, MaterialEnum.Earth, new FragileArrowModifier());
        card.AddModifier(new RepeatArrowModifier());
        int actions = 0;
        using (var session = new BattlePlayZoneResolutionSession(player, battle, player.PlayZone, new MagicModel[0]))
            session.RunToCompletion(step => { if (step.Kind == BattleResolutionStepKind.AfterRead) actions = step.AfterReadActions.Count; });
        Assert.That(player.Shield, Is.EqualTo(6));
        Assert.That(actions, Is.EqualTo(1));
        Assert.That(player.DiscardPile.Count, Is.EqualTo(2));
    }

    [Test]
    public void FullTurn_MultiHitReactsPerHit_KeepsRetained_RemovesTemporary_AndStartsNextTurn()
    {
        PlayerState player = CreatePlayer();
        EnemyModel enemy = AddEnemy(50);
        SetIntents(enemy, new EnemyIntentData { actionType = EnemyActionType.Attack, value = 4, times = 3 });
        player.AddBuff(BuffEnum.ShieldReflect, 1);
        player.GainShield(9);
        MaterialModel retained = new MaterialModel("retained", MaterialEnum.Water);
        retained.AddModifier(new RetainedArrowModifier());
        MaterialModel temporary = new MaterialModel("temporary", MaterialEnum.Wind);
        temporary.AddModifier(new TemporaryModifier());
        MaterialModel draw = new MaterialModel("draw", MaterialEnum.Fire);
        player.Hand.Add(retained);
        player.Hand.Add(temporary);
        player.DrawPile.Add(draw);
        AddCard(player, MaterialEnum.Earth);

        BattleTurnResult result = BattleTurnDriver.ResolveTurn(battle, new MagicModel[0], nextTurnDrawCount: 1);

        Assert.That(result.Outcome, Is.EqualTo(BattleTurnOutcome.NextPlayerTurn));
        Assert.That(player.CurrentHealth, Is.EqualTo(50)); // 12 damage, 9 starting + 3 arrow shield
        Assert.That(enemy.CurrentHealth, Is.EqualTo(38), "Shield reflection must react separately to all three hits.");
        Assert.That(result.EnemyIntents.Count, Is.EqualTo(3));
        Assert.That(result.EnemyIntents[0].HitIndex, Is.Zero);
        Assert.That(result.EnemyIntents[2].HitIndex, Is.EqualTo(2));
        Assert.That(enemy.ActionIndex, Is.EqualTo(1), "EndEnemyAction advances one group only once.");
        Assert.That(player.Hand, Does.Contain(retained));
        Assert.That(player.Hand, Does.Contain(draw));
        Assert.That(player.Hand.Contains(temporary), Is.False);
        Assert.That(result.RemovedTemporaryCards, Does.Contain(temporary));
        Assert.That(player.GetBuffStack(BuffEnum.ShieldReflect), Is.Zero, "Next-player-turn start must expire round buffs.");
        Assert.That(player.PlayZone.Count, Is.Zero);
        Assert.That(player.Shield, Is.Zero);
        Assert.That(battle.CurrentPhase, Is.EqualTo(BattlePhase.PlayerTurn));
    }

    [Test]
    public void FullTurn_EnemyBurningAndDeathEffectsFinishBeforeFirstIntent()
    {
        PlayerState player = CreatePlayer();
        EnemyModel survivor = AddEnemy(30);
        var dying = new DeathProbe(EnemyData(2));
        var summoned = new EnemyModel(EnemyData(20));
        battle.AddEnemy(dying); // The earlier enemy must not act before the later enemy burns.
        dying.OnDeath = () => battle.SpawnEnemy(summoned);
        SetIntents(survivor, new EnemyIntentData { actionType = EnemyActionType.Attack, value = 4 });
        SetIntents(dying, new EnemyIntentData { actionType = EnemyActionType.Attack, value = 20 });
        survivor.GainShield(10);
        survivor.AddBuff(BuffEnum.Vulnerable, 2);
        survivor.AddBuff(BuffEnum.Burning, 5);
        dying.AddBuff(BuffEnum.Burning, 3);
        summoned.AddBuff(BuffEnum.Burning, 4);
        player.AddBuff(BuffEnum.Burning, 3);
        player.AddBuff(BuffEnum.BurningDamageShieldNextTurn, 1);

        BattleTurnResult result = BattleTurnDriver.ResolveTurn(battle, nextTurnDrawCount: 0);

        var steps = new List<BattleResolutionStep>(result.Steps);
        int startIndex = steps.FindIndex(step => step.Kind == BattleResolutionStepKind.EnemyTurnStarted);
        int deathIndex = steps.FindIndex(step => step.Kind == BattleResolutionStepKind.DeathResolved);
        int actionIndex = steps.FindIndex(step => step.Kind == BattleResolutionStepKind.EnemyActionStarted);
        Assert.That(startIndex, Is.GreaterThanOrEqualTo(0));
        Assert.That(deathIndex, Is.GreaterThan(startIndex));
        Assert.That(actionIndex, Is.GreaterThan(deathIndex));
        BattleResolutionStep opening = steps[startIndex];
        Assert.That(opening.Before.FindEnemy(survivor).Health, Is.EqualTo(30));
        Assert.That(opening.After.FindEnemy(survivor).Health, Is.EqualTo(25));
        Assert.That(opening.After.FindEnemy(survivor).Shield, Is.EqualTo(10));
        Assert.That(opening.After.FindEnemy(survivor).Buffs[BuffEnum.Burning].stack, Is.EqualTo(2));
        Assert.That(opening.After.FindEnemy(survivor).Buffs[BuffEnum.Vulnerable].stack, Is.EqualTo(2));
        Assert.That(opening.After.FindEnemy(dying).Health, Is.Zero);
        Assert.That(opening.After.PlayerHealth, Is.EqualTo(50));
        Assert.That(steps[actionIndex].Before.FindEnemy(dying).DeathHandled, Is.True);
        Assert.That(result.EnemyIntents.Count, Is.EqualTo(1));
        Assert.That(steps[actionIndex].ActingEnemy, Is.SameAs(survivor));
        Assert.That(dying.ActionIndex, Is.Zero);
        Assert.That(summoned.ActionIndex, Is.Zero);
        Assert.That(summoned.CurrentHealth, Is.EqualTo(20));
        Assert.That(dying.DeathCount, Is.EqualTo(1));
        Assert.That(player.CurrentHealth, Is.EqualTo(43)); // One enemy hit, then player's own Burning.
        Assert.That(player.Shield, Is.EqualTo(7)); // Can: actual enemy HP lost (5 + 2).
    }

    [Test]
    public void FullTurn_OpeningBurningVictoryDoesNotExecuteAnyIntent()
    {
        PlayerState player = CreatePlayer();
        EnemyModel enemy = AddEnemy(2);
        enemy.AddBuff(BuffEnum.Burning, 3);
        SetIntents(enemy, new EnemyIntentData { actionType = EnemyActionType.Attack, value = 7 });

        BattleTurnResult result = BattleTurnDriver.ResolveTurn(battle, nextTurnDrawCount: 0);

        Assert.That(result.Outcome, Is.EqualTo(BattleTurnOutcome.Victory));
        Assert.That(result.EnemyIntents, Is.Empty);
        Assert.That(result.NextPlayerTurnStart, Is.Null);
        Assert.That(enemy.ActionIndex, Is.Zero);
        Assert.That(enemy.DeathHandled, Is.True);
        Assert.That(player.CurrentHealth, Is.EqualTo(50));
    }

    [Test]
    public void FullTurn_DeathBarrierWaitsForWholeEnemyGroup_ThenSkipsEnemyKilledByDeathChain()
    {
        PlayerState player = CreatePlayer();
        var next = new DeathProbe(EnemyData(30));
        var attacker = new DeathProbe(EnemyData(1)) { OnDeath = () => next.Kill() };
        battle.AddEnemy(attacker);
        battle.AddEnemy(next);
        SetIntents(attacker, new EnemyIntentData { actionType = EnemyActionType.Attack, value = 1, times = 3 });
        SetIntents(next, new EnemyIntentData { actionType = EnemyActionType.Attack, value = 20 });
        player.AddBuff(BuffEnum.ShieldReflect, 1);
        player.GainShield(3);

        BattleTurnResult result = BattleTurnDriver.ResolveTurn(battle, new MagicModel[0], nextTurnDrawCount: 0);

        Assert.That(result.Outcome, Is.EqualTo(BattleTurnOutcome.Victory));
        Assert.That(result.EnemyIntents.Count, Is.EqualTo(3), "A started enemy group completes; the next dead enemy never acts.");
        Assert.That(player.CurrentHealth, Is.EqualTo(50));
        Assert.That(attacker.DeathCount, Is.EqualTo(1));
        Assert.That(next.DeathCount, Is.EqualTo(1));
        Assert.That(next.ActionIndex, Is.Zero);
        Assert.That(battle.HasPendingDeaths, Is.False);
    }

    [Test]
    public void FullTurn_VictoryCleansMinionsWithoutTheirDeathEffects_AndForceDiscardsRetained()
    {
        PlayerState player = CreatePlayer();
        AddEnemy(1);
        var minion = new DeathProbe(EnemyData(20)) { OnDeath = () => player.TakeDamage(100) };
        minion.SetMinion(true);
        battle.AddEnemy(minion);
        var retained = new MaterialModel("retained", MaterialEnum.Water);
        retained.AddModifier(new RetainedArrowModifier());
        player.Hand.Add(retained);
        AddCard(player, MaterialEnum.Fire);

        BattleTurnResult result = BattleTurnDriver.ResolveTurn(battle, new MagicModel[0]);

        Assert.That(result.Outcome, Is.EqualTo(BattleTurnOutcome.Victory));
        Assert.That(minion.DeathHandled, Is.True);
        Assert.That(minion.DeathCount, Is.Zero);
        Assert.That(player.CurrentHealth, Is.EqualTo(50));
        Assert.That(player.Hand.Count, Is.Zero);
        Assert.That(player.PlayZone.Count, Is.Zero);
        Assert.That(player.DiscardPile, Does.Contain(retained));
        Assert.That(battle.CurrentPhase, Is.EqualTo(BattlePhase.Finished));
        Assert.That(MaterialModifierModel.CurrentContext, Is.Null);
    }

    [Test]
    public void FullTurn_PlayerDefeatDoesNotDrawNextTurnOrRunVictoryCleanup()
    {
        PlayerState player = CreatePlayer(3);
        EnemyModel enemy = AddEnemy(50);
        SetIntents(enemy, new EnemyIntentData { actionType = EnemyActionType.Attack, value = 5, times = 2 });
        var retained = new MaterialModel("retained", MaterialEnum.Water);
        retained.AddModifier(new RetainedArrowModifier());
        player.Hand.Add(retained);
        var draw = new MaterialModel("undrawn", MaterialEnum.Fire);
        player.DrawPile.Add(draw);

        BattleTurnResult result = BattleTurnDriver.ResolveTurn(battle, new MagicModel[0]);

        Assert.That(result.Outcome, Is.EqualTo(BattleTurnOutcome.Defeat));
        Assert.That(result.EnemyIntents.Count, Is.EqualTo(2));
        Assert.That(result.NextPlayerTurnStart, Is.Null);
        Assert.That(player.DrawPile, Does.Contain(draw));
        Assert.That(player.Hand, Does.Contain(retained));
        Assert.That(battle.CurrentPhase, Is.EqualTo(BattlePhase.Finished));
        Assert.That(MaterialModifierModel.CurrentContext, Is.Null);
    }

    [TestCase(1, 2)]
    [TestCase(2, 4)]
    public void FullTurn_RainBannerGrantsThreeNowAndTwoPerCastNextTurn(int matches, int expectedNextTurnArrows)
    {
        PlayerState player = CreatePlayer();
        AddEnemy(100);
        var recipe = new[] { MaterialEnum.Fire, MaterialEnum.Water, MaterialEnum.Earth };
        var rain = new RainBannerMagicModel(new MagicData { id = "magic_rain_banner", recipe = recipe });
        player.SetMagicAtSlot(rain, 0);
        for (int i = 0; i < matches; i++)
            foreach (MaterialEnum material in recipe)
                AddCard(player, material);

        var normalDraw = new MaterialModel("normal_draw", MaterialEnum.Earth);
        player.DrawPile.Add(normalDraw);
        player.AddBuff(BuffEnum.RetainedNextDraw, expectedNextTurnArrows + 1);
        var immediateArrows = new List<MaterialModel>();
        int casts = 0;
        BattleTurnResult result = BattleTurnDriver.ResolveTurn(battle, nextTurnDrawCount: 1, onStep: step =>
        {
            if (step.Kind == BattleResolutionStepKind.AfterMagic && step.CastPerformed)
            {
                casts++;
                Assert.That(step.Magic, Is.SameAs(rain));
                Assert.That(step.CreatedHandCards.Count, Is.EqualTo(3));
                foreach (MaterialModel card in step.CreatedHandCards)
                {
                    Assert.That(card.material, Is.EqualTo(MaterialEnum.Water));
                    Assert.That(card.isTemporary, Is.True);
                    Assert.That(card.isRetained, Is.False);
                    Assert.That(player.Deck.Contains(card), Is.False);
                }
                immediateArrows.AddRange(step.CreatedHandCards);
                Assert.That(step.After.Hand, Is.EqualTo(immediateArrows));
                Assert.That(step.After.PlayerBuffs[BuffEnum.RainBanner].stack, Is.EqualTo(casts));
            }
            if (step.Kind == BattleResolutionStepKind.PlayerTurnEnded)
            {
                Assert.That(step.Before.Hand, Is.EqualTo(immediateArrows));
                Assert.That(step.After.Hand, Is.Empty);
                Assert.That(step.RemovedTemporaryCards, Is.EquivalentTo(immediateArrows));
            }
            if (step.Kind == BattleResolutionStepKind.EnemyTurnEnded)
            {
                Assert.That(step.After.Hand, Is.Empty);
                Assert.That(step.After.PlayerBuffs[BuffEnum.RainBanner].stack, Is.EqualTo(matches));
            }
        });

        Assert.That(result.Outcome, Is.EqualTo(BattleTurnOutcome.NextPlayerTurn));
        Assert.That(casts, Is.EqualTo(matches));
        Assert.That(player.Buffs.ContainsKey(BuffEnum.RainBanner), Is.False);
        Assert.That(player.Hand.Count, Is.EqualTo(expectedNextTurnArrows + 1));
        Assert.That(player.Hand[0], Is.SameAs(normalDraw));
        Assert.That(normalDraw.isRetained, Is.True);
        for (int i = 1; i < player.Hand.Count; i++)
        {
            MaterialModel card = player.Hand[i];
            Assert.That(card.material, Is.EqualTo(MaterialEnum.Water));
            Assert.That(card.isTemporary, Is.True);
            Assert.That(card.isRetained, Is.False);
            Assert.That(player.Deck.Contains(card), Is.False);
            Assert.That(immediateArrows, Has.No.Member(card));
        }
        BattleTurnDriver.ResolveTurn(battle, nextTurnDrawCount: 0);
        Assert.That(player.Hand, Is.EqualTo(new[] { normalDraw }), "Rain arrows expire without being granted again.");
    }

    [Test]
    public void FullTurn_RainBannerRespectsHandLimitAndClearsAllStacks()
    {
        PlayerState player = CreatePlayer();
        AddEnemy(100);
        player.SetHandLimitThisTurn(1);
        player.AddBuff(BuffEnum.HandLimitNextTurn, 2);
        player.AddBuff(BuffEnum.RainBanner, 1);
        var rain = new RainBannerMagicModel(new MagicData { recipe = new[] { MaterialEnum.Earth } });
        AddCard(player, MaterialEnum.Earth);
        BattleTurnResult result = BattleTurnDriver.ResolveTurn(battle, new[] { rain }, nextTurnDrawCount: 0);

        var cast = new List<BattleResolutionStep>(result.Steps).Find(step => step.Kind == BattleResolutionStepKind.AfterMagic);
        Assert.That(cast.CreatedHandCards.Count, Is.EqualTo(1));
        Assert.That(player.Hand.Count, Is.EqualTo(2));
        Assert.That(player.Buffs.ContainsKey(BuffEnum.RainBanner), Is.False);
    }

    [Test]
    public void FullTurn_IncompleteRainBannerRecipeDoesNotCast()
    {
        PlayerState player = CreatePlayer();
        AddEnemy(100);
        player.SetMagicAtSlot(new RainBannerMagicModel(new MagicData
        {
            id = "magic_rain_banner",
            recipe = new[] { MaterialEnum.Fire, MaterialEnum.Water, MaterialEnum.Earth }
        }), 0);
        AddCard(player, MaterialEnum.Fire);
        AddCard(player, MaterialEnum.Water);

        BattleTurnResult result = BattleTurnDriver.ResolveTurn(battle, nextTurnDrawCount: 0);
        Assert.That(player.Hand, Is.Empty);
        Assert.That(player.Buffs.ContainsKey(BuffEnum.RainBanner), Is.False);
        foreach (BattleResolutionStep step in result.Steps)
            Assert.That(step.Kind, Is.Not.EqualTo(BattleResolutionStepKind.AfterMagic));
    }

    [Test]
    public void FullTurn_WingsKeepsHandAtCleanupOfTheTurnItCasts()
    {
        PlayerState player = CreatePlayer();
        AddEnemy(100);
        var retained = new MaterialModel("retained", MaterialEnum.Earth);
        retained.AddModifier(new RetainedArrowModifier());
        var unsecured = new MaterialModel("unsecured", MaterialEnum.Earth);
        player.Hand.Add(retained);
        player.Hand.Add(unsecured);
        AddCard(player, MaterialEnum.Earth);
        var floating = new FloatingMagicModel(new MagicData { recipe = new[] { MaterialEnum.Earth } });

        BattleTurnDriver.ResolveTurn(battle, new[] { floating }, nextTurnDrawCount: 0);
        Assert.That(player.Hand, Is.EqualTo(new[] { retained, unsecured }));
        Assert.That(player.PlayZone, Is.Empty);
        Assert.That(player.GetBuffStack(BuffEnum.KeepHand), Is.Zero);

        BattleTurnResult emptyPlay = BattleTurnDriver.ResolveTurn(battle, nextTurnDrawCount: 0);
        var cleanup = new List<BattleResolutionStep>(emptyPlay.Steps).Find(step => step.Kind == BattleResolutionStepKind.PlayerTurnEnded);
        Assert.That(cleanup.Before.Hand, Does.Contain(unsecured));
        Assert.That(cleanup.After.Hand, Is.EqualTo(new[] { retained }));
        Assert.That(player.DiscardPile, Does.Contain(unsecured));
    }

    [Test]
    public void FullTurn_HandCountEffectsReadLiveHandAndBothZonesExitAfterRead()
    {
        PlayerState player = CreatePlayer();
        EnemyModel enemy = AddEnemy(100);
        var normal = new MaterialModel("hand_normal", MaterialEnum.Water);
        var temporary = new MaterialModel("hand_temp", MaterialEnum.Wind);
        temporary.AddModifier(new TemporaryModifier());
        player.Hand.Add(normal);
        player.Hand.Add(temporary);
        MaterialModel played = AddCard(player, MaterialEnum.Earth);
        var wave = new HarmfulWaveMagicModel(new MagicData { recipe = new[] { MaterialEnum.Earth } });
        var camouflage = new CamouflageMagicModel(new MagicData { recipe = new[] { MaterialEnum.Earth } });

        BattleTurnResult result = BattleTurnDriver.ResolveTurn(battle, new MagicModel[] { wave, camouflage }, nextTurnDrawCount: 0);

        var steps = new List<BattleResolutionStep>(result.Steps);
        var waveCast = steps.Find(step => step.Kind == BattleResolutionStepKind.AfterMagic && step.Magic == wave);
        var camouflageCast = steps.Find(step => step.Kind == BattleResolutionStepKind.AfterMagic && step.Magic == camouflage);
        Assert.That(waveCast.After.FindEnemy(enemy).Health, Is.EqualTo(94));
        Assert.That(camouflageCast.After.PlayerShield - camouflageCast.Before.PlayerShield, Is.EqualTo(6));
        int afterReadIndex = steps.FindIndex(step => step.Kind == BattleResolutionStepKind.AfterRead);
        int cleanupIndex = steps.FindIndex(step => step.Kind == BattleResolutionStepKind.PlayerTurnEnded);
        Assert.That(cleanupIndex, Is.GreaterThan(afterReadIndex));
        var cleanup = steps[cleanupIndex];
        Assert.That(cleanup.Before.Hand, Is.EqualTo(new[] { normal, temporary }));
        Assert.That(cleanup.Before.PlayZone, Is.EqualTo(new[] { played }));
        Assert.That(cleanup.After.Hand, Is.Empty);
        Assert.That(cleanup.After.PlayZone, Is.Empty);
        Assert.That(cleanup.RemovedTemporaryCards, Is.EqualTo(new[] { temporary }));
        Assert.That(player.DiscardPile, Is.EquivalentTo(new[] { normal, played }));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void FullTurn_StickerSeesConsumptionAtCleanupOrDuringRefine(bool refine)
    {
        PlayerState player = CreatePlayer();
        AddEnemy(100);
        var temporary = new MaterialModel("hand_temp", MaterialEnum.Water);
        temporary.AddModifier(new TemporaryModifier());
        var retained = new MaterialModel("retained", MaterialEnum.Wind);
        retained.AddModifier(new RetainedArrowModifier());
        player.Hand.Add(temporary);
        player.Hand.Add(retained);
        var sticker = new StickerMagicModel(new MagicData { recipe = new[] { MaterialEnum.Earth } });
        var diamond = new RefineMagicModel(new MagicData { recipe = new[] { MaterialEnum.Wind } });
        AddCard(player, MaterialEnum.Earth);
        if (refine) AddCard(player, MaterialEnum.Wind);

        BattleTurnResult result = BattleTurnDriver.ResolveTurn(battle, new MagicModel[] { sticker, diamond }, nextTurnDrawCount: 0);

        var steps = new List<BattleResolutionStep>(result.Steps);
        var cleanup = steps.Find(step => step.Kind == BattleResolutionStepKind.PlayerTurnEnded);
        if (refine)
        {
            var cast = steps.Find(step => step.Kind == BattleResolutionStepKind.AfterMagic && step.Magic == diamond);
            Assert.That(cast.Before.Hand, Is.EqualTo(new[] { temporary, retained }));
            Assert.That(cast.After.Hand, Is.Empty);
            Assert.That(cast.After.ConsumedPile, Is.EquivalentTo(new[] { temporary, retained }));
            Assert.That(cleanup.Before.Hand, Is.Empty);
            Assert.That(cleanup.RemovedTemporaryCards, Is.Empty);
        }
        else
        {
            Assert.That(cleanup.Before.Hand, Does.Contain(temporary));
            Assert.That(cleanup.After.Hand, Is.EqualTo(new[] { retained }));
            Assert.That(cleanup.RemovedTemporaryCards, Is.EqualTo(new[] { temporary }));
        }
        // The played Wind arrow also gives one normal draw in the Refine case.
        Assert.That(player.Hand.Count, Is.EqualTo(refine ? 3 : 2));
        var scheduled = player.Hand.FindAll(arrow => arrow.isTemporary);
        Assert.That(scheduled.Count, Is.EqualTo(refine ? 2 : 1));
        foreach (MaterialModel arrow in scheduled)
            Assert.That(arrow.material, Is.EqualTo(MaterialEnum.Wind));
        Assert.That(player.TemporaryMaterialsNextTurn, Is.Empty);
    }

    [TestCase(true, true)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    public void FullTurn_EggDeathExplosionMakesZeroHealthLoseBeforeAnotherArrowOrEnemyActs(bool kingSurvives, bool lethal)
    {
        PlayerState player = CreatePlayer(lethal ? 3 : 30);
        var egg = new EnemyModel(EnemyData(1));
        battle.AddEnemy(egg);
        egg.AddBuff(BuffEnum.EggDeathExplosion, 3);
        var king = new FlyKingEnemyModel(EnemyData(kingSurvives ? 100 : 3));
        battle.AddEnemy(king);
        battle.SetFocusTarget(egg);
        AddCard(player, MaterialEnum.Fire);
        var later = new ArrowProbe();
        AddCard(player, MaterialEnum.Earth, later);

        BattleTurnResult result = BattleTurnDriver.ResolveTurn(battle, nextTurnDrawCount: 0);
        Assert.That(egg.DeathHandled, Is.True);
        Assert.That(king.IsDead, Is.EqualTo(!kingSurvives));
        Assert.That(result.Outcome, Is.EqualTo(lethal ? BattleTurnOutcome.Defeat : BattleTurnOutcome.NextPlayerTurn));
        Assert.That(later.BaseCount, Is.EqualTo(lethal ? 0 : 1));
        if (lethal)
        {
            Assert.That(player.CurrentHealth, Is.Zero);
            Assert.That(result.PlayerTurnEnd, Is.Null);
            Assert.That(result.EnemyIntents, Is.Empty);
            Assert.That(result.NextPlayerTurnStart, Is.Null);
            Assert.That(battle.CurrentPhase, Is.EqualTo(BattlePhase.Finished));
        }
    }

    private PlayerState CreatePlayer(int maxHealth = 50)
    {
        var player = new PlayerStatus(maxHealth, 0, PlayerStateServices.Simulation, new RunDifficultySaveData());
        player.SetRunRandomState(17, 0);
        battle = BattleManager.Create(player);
        battle.BeginPlayerTurn();
        return player;
    }

    private EnemyModel AddEnemy(int health)
    {
        var enemy = new EnemyModel(EnemyData(health));
        battle.AddEnemy(enemy);
        return enemy;
    }

    private static EnemyData EnemyData(int health)
    {
        return new EnemyData { numericId = 99001, string_id = "battle_resolution_probe", maxHealth = health };
    }

    private static MaterialModel AddCard(PlayerState player, MaterialEnum direction, MaterialModifierModel modifier = null)
    {
        var card = new MaterialModel("card_" + player.PlayZone.Count, direction) { isPlayed = true };
        if (modifier != null)
            card.AddModifier(modifier);
        player.PlayZone.Add(card);
        return card;
    }

    private static void SetIntents(EnemyModel enemy, params EnemyIntentData[] intents)
    {
        enemy.ClearCurrentIntents();
        ((List<EnemyIntentData>)enemy.CurrentIntents).AddRange(intents);
    }

    private sealed class ArrowProbe : MaterialModifierModel
    {
        public int BeforeReadCount;
        public int BaseCount;
        public int InvokeCount;
        public int ReplayCount;
        public int TokenCount = 1;
        public EnemyModel FocusOnInvoke;
        public bool InvokeSawUnlockedTarget = true;

        public override void OnBeforeArrowRead(ArrowReadContext context) { BeforeReadCount++; }
        public override void OnArrowBaseEffectResolve(ArrowReadContext context) { BaseCount++; }
        public override int GetArrowMatchTokenCount(int tokenCount) { return TokenCount; }
        public override int GetArrowSequenceReplayCount() { return ReplayCount; }
        public override void OnTokenInvoke(ArrowReadToken token, ArrowReadStep step)
        {
            InvokeCount++;
            InvokeSawUnlockedTarget &= CurrentContext.BattleManager.CurrentCastTarget == null;
            if (FocusOnInvoke != null)
                CurrentContext.BattleManager.SetFocusTarget(FocusOnInvoke);
        }
    }

    private sealed class ProbeMagic : MagicModel
    {
        public int CastCount;
        public Action OnCast;
        public override MagicEffectType EffectType => MagicEffectType.GainShield;

        public ProbeMagic(string id, MaterialEnum direction)
            : base(new MagicData { id = id, recipe = new[] { direction } }) { }

        protected override void ResolveCast(PlayerState player, BattleManager manager, MagicCastResult result)
        {
            CastCount++;
            OnCast?.Invoke();
        }
    }

    private sealed class DamageProbeMagic : MagicModel
    {
        private readonly int damage;
        public int CastCount;
        public Action BeforeDamage;
        public override MagicEffectType EffectType => MagicEffectType.Damage;

        public DamageProbeMagic(MaterialEnum[] recipe, int damage)
            : base(new MagicData { id = "damage_probe", recipe = recipe }) { this.damage = damage; }

        protected override void ResolveCast(PlayerState player, BattleManager manager, MagicCastResult result)
        {
            CastCount++;
            BeforeDamage?.Invoke();
            DamageTarget(player, manager, damage, result);
        }
    }

    private sealed class DeathProbe : EnemyModel
    {
        public int DeathCount;
        public Action OnDeath;
        public DeathProbe(EnemyData data) : base(data) { }
        protected override void HandleDeathEffect(CombatantModel opponent)
        {
            DeathCount++;
            OnDeath?.Invoke();
        }
    }
}
