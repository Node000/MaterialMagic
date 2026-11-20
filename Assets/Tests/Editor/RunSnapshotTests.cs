using System;
using Newtonsoft.Json;
using NUnit.Framework;

/// <summary>Only in-memory export/restore; never reads, writes, or clears real save slots.</summary>
public sealed class RunSnapshotTests
{
    [Test] public void SnapshotRoundTrip_PreservesPilesIdentityRngAndDecisionState()
    {
        var player = new PlayerStatus(50, 23, PlayerStateServices.Simulation, new RunDifficultySaveData { ascensionLevel = 2 });
        player.SetRunRandomState(7135, 7);
        MaterialModel card = player.AddDeckMaterial(MaterialEnum.Earth);
        player.DrawPile.Clear();
        player.Hand.Add(card);
        var battle = new BattleManager(player);
        battle.BeginPlayerTurn();
        var commands = new BattleCommandSession(player, battle);
        Assert.That(commands.Execute(BattleCommand.Play(card, 0)).Accepted, Is.True);
        commands.RestoreDecisionState(true, true);
        player.AddPermanentRefreshChance(2);
        player.UsePermanentRefreshChance();
        player.SetRefreshLimitReductionThisTurn(1);
        player.SetHandLimitThisTurn(3);
        int step = player.RunRandomStep;
        RunMemorySnapshot snapshot = RunSaveSystem.ExportSnapshot(player, null, battleManager: battle, commands: commands);
        // Memory branching retains FullGraph/EnemyRuntime; portable JSON uses GameSessionCheckpoint.
        RunMemorySnapshot reloaded = snapshot;
        PlayerStatus restored = RunSaveSystem.RestoreSnapshot(reloaded);
        BattleManager restoredBattle = RunSaveSystem.RestoreSnapshotBattle(reloaded, restored);
        BattleCommandSession restoredCommands = RunSaveSystem.RestoreSnapshotCommands(reloaded, restored, restoredBattle);
        Assert.That(restored.Services, Is.SameAs(PlayerStateServices.Simulation));
        Assert.That(restored.PlayZone[0], Is.SameAs(restored.Deck[0]));
        Assert.That(restored.PlayZone[0], Is.Not.SameAs(card));
        Assert.That(restored.RunRandomSeed, Is.EqualTo(7135));
        Assert.That(restored.RunRandomStep, Is.EqualTo(step));
        Assert.That(restored.Rules.Difficulty.ascensionLevel, Is.EqualTo(2));
        Assert.That(restored.RemainingPermanentRefreshChancesThisTurn, Is.EqualTo(1));
        Assert.That(restored.RefreshLimitReductionThisTurn, Is.EqualTo(1));
        Assert.That(restored.HandLimitThisTurn, Is.EqualTo(3));
        Assert.That(restoredCommands.RefreshUsedThisTurn, Is.True);
        Assert.That(restoredCommands.Query(BattleCommand.EndTurn()), Is.EqualTo(BattleCommandRejection.TurnResolving));
    }

    [Test] public void TwoRestoredWorlds_DoNotReplaceGlobalRunBattleOrDifficulty()
    {
        RunManager hostRun = RunManager.Current;
        BattleManager hostBattle = BattleManager.Instance;
        string hostDifficulty = JsonConvert.SerializeObject(DifficultyUpgradeSystem.ExportCurrentState());
        var first = new PlayerStatus(50, 0, PlayerStateServices.Simulation, new RunDifficultySaveData { ascensionLevel = 1 });
        first.SetRunRandomState(111, 4);
        var second = new PlayerStatus(50, 0, PlayerStateServices.Simulation, new RunDifficultySaveData { ascensionLevel = 3 });
        second.SetRunRandomState(222, 8);
        RunMemorySnapshot a = RunSaveSystem.ExportSnapshot(first, null, battleManager: new BattleManager(first));
        RunMemorySnapshot b = RunSaveSystem.ExportSnapshot(second, null, battleManager: new BattleManager(second));
        PlayerStatus restoredA = RunSaveSystem.RestoreSnapshot(a);
        PlayerStatus restoredB = RunSaveSystem.RestoreSnapshot(b);
        RunSaveSystem.RestoreSnapshotBattle(a, restoredA);
        RunSaveSystem.RestoreSnapshotBattle(b, restoredB);
        Assert.That(restoredA.NextRunRandomInt(0, 100), Is.EqualTo(RunRandom.Range(111, 4, 0, 100)));
        Assert.That(restoredB.NextRunRandomInt(0, 100), Is.EqualTo(RunRandom.Range(222, 8, 0, 100)));
        Assert.That(restoredA.Rules.Difficulty.ascensionLevel, Is.EqualTo(1));
        Assert.That(restoredB.Rules.Difficulty.ascensionLevel, Is.EqualTo(3));
        Assert.That(RunManager.Current, Is.SameAs(hostRun));
        Assert.That(BattleManager.Instance, Is.SameAs(hostBattle));
        Assert.That(JsonConvert.SerializeObject(DifficultyUpgradeSystem.ExportCurrentState()), Is.EqualTo(hostDifficulty));
    }

    [Test] public void SnapshotCopiesPendingOfferAndRestoredValuesNeverAliasInput()
    {
        var player = new PlayerStatus(50, 0, PlayerStateServices.Simulation, new RunDifficultySaveData());
        player.SetRunRandomState(42, 0);
        player.PendingRelicOffer = new RelicOfferSaveData { offerId = "frozen", prices = new[] { 7 } };
        RunMemorySnapshot snapshot = RunSaveSystem.ExportSnapshot(player, null);
        player.PendingRelicOffer.prices[0] = 99;
        Assert.That(snapshot.Run.player.pendingRelicOffer.prices[0], Is.EqualTo(7));
        PlayerStatus restored = RunSaveSystem.RestoreSnapshot(snapshot);
        restored.PendingRelicOffer.prices[0] = 12;
        Assert.That(snapshot.Run.player.pendingRelicOffer.prices[0], Is.EqualTo(7));
    }

    [Test] public void ExplicitRestoreServicesAndDifficultyPreservePaidRemovalAndSavedPlayLimit()
    {
        var services = new PlayerStateServices();
        var snapshot = new RunMemorySnapshot(
            new RunSaveData { difficulty = new RunDifficultySaveData { ascensionLevel = 1 }, player = new PlayerSaveData
            {
                maxHealth = 50, currentHealth = 40, gold = 12, drawCount = 6, maxPlayCount = 11,
                paidShopArrowRemovalCount = 4, runRandomSeed = 55, runRandomStep = 3
            } }, new PlayerCombatSaveData());
        var explicitDifficulty = new RunDifficultySaveData { ascensionLevel = 4, upgradeIds = Array.Empty<string>() };
        PlayerStatus player = RunSaveSystem.RestoreSnapshot(snapshot, services, explicitDifficulty);
        Assert.That(player.Services, Is.SameAs(services));
        Assert.That(player.PaidShopArrowRemovalCount, Is.EqualTo(4));
        Assert.That(player.MaxPlayCount, Is.EqualTo(11));
        Assert.That(player.Rules.Difficulty.ascensionLevel, Is.EqualTo(4));
        explicitDifficulty.ascensionLevel = 0;
        Assert.That(player.Rules.Difficulty.ascensionLevel, Is.EqualTo(4));
    }

    [Test] public void MemorySnapshotPreservesPackedGraphRandomLockAndModifierFlags()
    {
        var player = new PlayerStatus(50, 0, PlayerStateServices.Simulation, new RunDifficultySaveData());
        player.SetRunRandomState(55, 8);
        var card = new MaterialModel("container", MaterialEnum.Wild) { isPlayed = true, removeCardAfterBattle = true };
        var packed = new MaterialModel("packed", MaterialEnum.Fire);
        card.packedCards.Add(packed);
        card.linkedCards.Add(packed);
        var modifier = new RandomArrowModifier();
        card.AddModifier(modifier);
        modifier.MarkRemoveAfterTurn();
        typeof(RandomArrowModifier).GetField("rolledMaterial", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .SetValue(modifier, MaterialEnum.Earth);
        player.Deck.Add(card); player.PlayZone.Add(card);
        RunMemorySnapshot snapshot = RunSaveSystem.ExportSnapshot(player, null);
        PlayerStatus restored = RunSaveSystem.RestoreSnapshot(snapshot);
        var copy = restored.PlayZone[0];
        Assert.That(copy, Is.SameAs(restored.Deck[0]));
        Assert.That(copy.packedCards[0], Is.SameAs(copy.linkedCards[0]));
        Assert.That(copy.removeCardAfterBattle, Is.True);
        Assert.That(copy.modifiers[0].RemoveModifierAfterTurn, Is.True);
        Assert.That(copy.modifiers[0].model, Is.SameAs(copy));
        Assert.That(copy.modifiers[0].GetLockedArrowDisplayMaterial(), Is.EqualTo(MaterialEnum.Earth));
        Assert.That(restored.RunRandomStep, Is.EqualTo(8));
        copy.packedCards[0].material = MaterialEnum.Water;
        Assert.That(RunSaveSystem.RestoreSnapshot(snapshot).PlayZone[0].packedCards[0].material, Is.EqualTo(MaterialEnum.Fire));
    }

    [Test] public void ExplicitCatalogAndProfileStayWorldLocalAndArchiveRoundTrips()
    {
        var data = new MagicData { numericId = 991, id = "world_magic", script = "FireballArtMagicModel" };
        var catalog = new RuleContentSnapshot().AddNumeric(new System.Collections.Generic.Dictionary<int, MagicData> { { 991, data } }).Freeze();
        data.id = "changed_after_capture";
        var player = new PlayerStatus(50, 0, PlayerStateServices.Simulation, new RunDifficultySaveData(), catalog, RuleProfilePolicy.Empty);
        using (player.Rules.Enter())
        {
            Assert.That(GameDataDatabase.TryGetMagicData(991, out MagicData captured), Is.True);
            Assert.That(captured.id, Is.EqualTo("world_magic"));
            Assert.That(UnlockSystem.IsMagicUnlocked(captured), Is.False);
        }
        var portable = RuleContentArchive.FromJson(RuleContentArchive.Capture(catalog).ToJson()).CreateProvider();
        Assert.That(portable.Numeric<MagicData>()[991].id, Is.EqualTo("world_magic"));
    }

    [Test] public void ContentArchiveRoundTripsRealUnityVectorsAndReadonlyArrowValues()
    {
        var enemy = new EnemyData
        {
            numericId = 880, id = "vector_enemy", maxHealth = 37, baseAttack = 9,
            infoBoxSize = new UnityEngine.Vector2(320.5f, 184.25f),
            infoBoxOffset = new UnityEngine.Vector2(-12.75f, 6.5f)
        };
        var zeroEnemy = new EnemyData { numericId = 881, id = "zero_vector_enemy" };
        var config = new RuleConfiguration(arrows: new System.Collections.Generic.Dictionary<string, RuleArrowUpgradeValue>
        {
            { "up_up", new RuleArrowUpgradeValue(7, 11) }
        });
        var catalog = new RuleContentSnapshot(configuration: config)
            .AddNumeric(new System.Collections.Generic.Dictionary<int, EnemyData> { { 880, enemy }, { 881, zeroEnemy } }).Freeze();
        enemy.infoBoxSize = new UnityEngine.Vector2(1f, 2f);
        enemy.maxHealth = 1;

        string json = RuleContentArchive.Capture(catalog).ToJson();
        Assert.That(json, Does.Not.Contain("normalized"));
        Assert.That(json, Does.Not.Contain("magnitude"));
        var document = Newtonsoft.Json.Linq.JObject.Parse(json);
        var size = (Newtonsoft.Json.Linq.JObject)document["enemies"]["880"]["infoBoxSize"];
        Assert.That(size.Count, Is.EqualTo(2), "Vector2 has only x/y data; computed Unity properties are not portable state.");
        Assert.That((float)size["x"], Is.EqualTo(320.5f));
        Assert.That((float)size["y"], Is.EqualTo(184.25f));

        IRuleContentProvider restored = RuleContentArchive.FromJson(json).CreateProvider();
        EnemyData copy = restored.Numeric<EnemyData>()[880];
        Assert.That(copy, Is.Not.SameAs(enemy));
        Assert.That(copy.maxHealth, Is.EqualTo(37));
        Assert.That(copy.baseAttack, Is.EqualTo(9));
        Assert.That(copy.infoBoxSize.x, Is.EqualTo(320.5f));
        Assert.That(copy.infoBoxSize.y, Is.EqualTo(184.25f));
        Assert.That(copy.infoBoxOffset.x, Is.EqualTo(-12.75f));
        Assert.That(copy.infoBoxOffset.y, Is.EqualTo(6.5f));
        Assert.That(restored.Numeric<EnemyData>()[881].infoBoxSize.x, Is.Zero);
        Assert.That(restored.Numeric<EnemyData>()[881].infoBoxSize.y, Is.Zero);
        Assert.That(restored.Numeric<EnemyData>()[881].infoBoxOffset.x, Is.Zero);
        Assert.That(restored.Numeric<EnemyData>()[881].infoBoxOffset.y, Is.Zero);
        var configuration = (IRuleConfigurationProvider)restored;
        Assert.That(configuration.TryGetArrowUpgrade("up_up", out RuleArrowUpgradeValue arrow), Is.True);
        Assert.That(arrow.Primary, Is.EqualTo(7));
        Assert.That(arrow.Secondary, Is.EqualTo(11));
    }

    [Test] public void MemorySnapshotPreservesSelfCycleWhilePersistentMaterialExportRejectsIt()
    {
        var player = new PlayerStatus(50, 0, PlayerStateServices.Simulation, new RunDifficultySaveData(),
            new RuleContentSnapshot().Freeze(), RuleProfilePolicy.Empty);
        var card = new MaterialModel("self_cycle", MaterialEnum.Fire);
        card.linkedCards.Add(card);
        player.Deck.Add(card);
        player.Hand.Add(card);
        Assert.Throws<InvalidOperationException>(() => RuleMaterialCodec.Export(card));

        RunMemorySnapshot snapshot = RuleRunSnapshot.ExportSnapshot(player, null);
        PlayerStatus restored = RuleRunSnapshot.RestoreSnapshot(snapshot);
        Assert.That(restored.Deck[0], Is.Not.SameAs(card));
        Assert.That(restored.Hand[0], Is.SameAs(restored.Deck[0]));
        Assert.That(restored.Deck[0].linkedCards[0], Is.SameAs(restored.Deck[0]));
        Assert.Throws<InvalidOperationException>(() => RuleMaterialCodec.Export(card),
            "Snapshot's temporary legacy projection scope must not weaken later persistent saves.");
    }

    [Test] public void RestoreSnapshotBattleResolvesLevelFromOwnerNotForeignAmbientCatalog()
    {
        var ownerLevel = new LevelData { numericId = 771, id = "owner_level", rewardPoolId = 17 };
        var ownerCatalog = new RuleContentSnapshot()
            .AddNumeric(new System.Collections.Generic.Dictionary<int, LevelData> { { 771, ownerLevel } }).Freeze();
        var player = new PlayerStatus(50, 0, PlayerStateServices.Simulation, new RunDifficultySaveData(), ownerCatalog, RuleProfilePolicy.Empty);
        var battle = new BattleManager(player);
        battle.ConfigureLevelContext(ownerLevel, 3, true);
        RunMemorySnapshot snapshot = RuleRunSnapshot.ExportSnapshot(player, null, currentLevel: ownerLevel, battleManager: battle);
        PlayerStatus restored = RuleRunSnapshot.RestoreSnapshot(snapshot);
        var foreignCatalog = new RuleContentSnapshot()
            .AddNumeric(new System.Collections.Generic.Dictionary<int, LevelData>
            {
                { 771, new LevelData { numericId = 771, id = "foreign_level", rewardPoolId = 99 } }
            }).Freeze();
        var foreign = new PlayerStatus(50, 0, PlayerStateServices.Simulation, new RunDifficultySaveData(), foreignCatalog, RuleProfilePolicy.Empty);

        using (foreign.Rules.Enter())
        {
            RuleExecutionContext ambient = RuleExecutionScope.Current;
            BattleManager restoredBattle = RuleRunSnapshot.RestoreSnapshotBattle(snapshot, restored);
            Assert.That(restoredBattle.CurrentLevel.id, Is.EqualTo("owner_level"));
            Assert.That(restoredBattle.CurrentLevel.rewardPoolId, Is.EqualTo(17));
            Assert.That(restoredBattle.CurrentChapterNumericId, Is.EqualTo(3));
            Assert.That(restoredBattle.CurrentLevelIsBoss, Is.True);
            Assert.That(RuleExecutionScope.Current, Is.SameAs(ambient));
        }
    }

    [Test] public void SnapshotRejectsMismatchedWorldOwnership()
    {
        var first = new PlayerStatus(50, 0, PlayerStateServices.Simulation, new RunDifficultySaveData());
        var second = new PlayerStatus(50, 0, PlayerStateServices.Simulation, new RunDifficultySaveData());
        Assert.Throws<ArgumentException>(() => RunSaveSystem.ExportSnapshot(first, new RunManager(second)));
        Assert.Throws<ArgumentException>(() => RunSaveSystem.ExportSnapshot(first, null, battleManager: new BattleManager(second)));
    }
}
