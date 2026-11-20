using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Production run DTO codec and in-memory branching. No disk, slot, wall clock, scene or profile writes.</summary>
public static class RuleRunSnapshot
{
    public const int CurrentVersion = 4;
    public const string MapSelectionState = "MapSelection";
    public const string BeforeNodeState = "BeforeNode";

    /// <summary>Pure DTO export. All world inputs are explicit; no slot, disk, profile or Current singleton is read.</summary>
    public static RunSaveData ExportCurrentRun(PlayerState player, RunManager run,
        IReadOnlyList<RunMapNodeModel> mapNodes = null, int currentMapNodeIndex = 0, ChapterData chapter = null,
        LevelData currentLevel = null, BattleManager battleManager = null, EventModel currentEvent = null,
        string sceneName = null)
    {
        if (player == null) throw new ArgumentNullException(nameof(player));
        if (run != null && !ReferenceEquals(run.PlayerStatus, player)) throw new ArgumentException("Run/player ownership differs.", nameof(run));
        if (battleManager != null && !ReferenceEquals(battleManager.PlayerState, player)) throw new ArgumentException("Battle/player ownership differs.", nameof(battleManager));
        using (player.Rules.Enter())
        {
            return new RunSaveData
            {
                version = CurrentVersion,
                runState = currentLevel != null ? BeforeNodeState : MapSelectionState,
                sceneName = sceneName,
                chapterNumericId = chapter != null ? chapter.numericId : 0,
                currentMapNodeIndex = currentMapNodeIndex,
                mapNodes = ExportMapNodes(mapNodes ?? run?.MapNodes),
                mapGrid = ExportMapGrid(run?.MapGrid),
                runPools = run != null ? run.ExportPoolState() : null,
                difficulty = CopyDto(player.Rules.Difficulty),
                player = ExportPlayer(player),
                magicReadingOrder = ExportMagicReadingOrder(player),
                magicLayoutSlotCount = player.MagicLayoutSlotCount,
                magicRightNeighborStep = player.MagicRightNeighborStep,
                currentNode = currentLevel != null ? ExportCurrentNode(currentLevel, player, battleManager, currentEvent, null) : null
            };
        }
    }

    /// <summary>Complete player/command decision snapshot without filesystem or host side effects.</summary>
    public static RunMemorySnapshot ExportSnapshot(PlayerState player, RunManager run,
        IReadOnlyList<RunMapNodeModel> mapNodes = null, int currentMapNodeIndex = 0, ChapterData chapter = null,
        LevelData currentLevel = null, BattleManager battleManager = null, EventModel currentEvent = null,
        BattleCommandSession commands = null)
    {
        if (player == null) throw new ArgumentNullException(nameof(player));
        if (!player.Relics.IsSettled || (battleManager != null && battleManager.HasPendingDeaths))
            throw new InvalidOperationException("Capture at a settled rule decision boundary, not in a death/relic queue.");
        if (commands != null && !ReferenceEquals(commands.Player, player)) throw new ArgumentException("Commands/player ownership differs.", nameof(commands));
        using (player.Rules.Enter())
        using (RuleMaterialCodec.ProjectLegacyGraphForSnapshot())
        {
            return new RunMemorySnapshot
            {
                Run = CopyDto(ExportCurrentRun(player, run, mapNodes, currentMapNodeIndex, chapter, currentLevel, battleManager, currentEvent)),
                Combat = ExportPlayerCombat(player),
                Content = RuleContentArchive.Capture(player.Rules.Content),
                Profile = player.Rules.Profile.Export(),
                FullGraph = new RulePlayerGraphSnapshot(player),
                EnemyRuntime = ExportEnemyRuntime(battleManager),
                Battle = battleManager != null ? ExportBattle(currentLevel, battleManager, player) : null,
                RefreshUsedThisTurn = commands != null && commands.RefreshUsedThisTurn,
                CommandTurnResolving = commands != null && commands.TurnResolving
            };
        }
    }

    public static PlayerStatus RestoreSnapshot(RunMemorySnapshot snapshot, PlayerStateServices services = null,
        RunDifficultySaveData difficulty = null, IRuleContentProvider content = null, RuleProfilePolicy profilePolicy = null)
    {
        if (snapshot?.Run?.player == null) throw new ArgumentException("A player snapshot is required.", nameof(snapshot));
        PlayerStatus player = CreatePlayerStatus(snapshot.Run, services ?? PlayerStateServices.Simulation,
            difficulty ?? snapshot.Run.difficulty ?? new RunDifficultySaveData(), true,
            content ?? snapshot.Content?.CreateProvider(), profilePolicy ?? RuleProfilePolicy.Restore(snapshot.Profile));
        using (player.Rules.Enter())
        {
            RestorePlayerCombat(snapshot.Combat, player);
            snapshot.FullGraph?.Restore(player, snapshot.Combat);
        }
        return player;
    }

    /// <summary>Does not replace BattleManager.Instance. The caller owns this independent world.</summary>
    public static BattleManager RestoreSnapshotBattle(RunMemorySnapshot snapshot, PlayerState player)
    {
        BattleNodeSaveData saved = snapshot?.Battle ?? snapshot?.Run?.currentNode?.battle;
        if (saved == null) return null;
        if (player == null) throw new ArgumentNullException(nameof(player));
        using (player.Rules.Enter())
        {
            var battle = new BattleManager(player);
            LevelData level = GetLevel(saved.levelId);
            battle.ConfigureLevelContext(level, saved.chapterNumericId, saved.isBoss);
            RestoreBattle(saved, battle, player);
            snapshot.FullGraph?.Restore(player, snapshot.Combat);
            if (snapshot.EnemyRuntime != null)
            {
                if (snapshot.EnemyRuntime.Count != battle.Enemies.Count) throw new InvalidOperationException("Restored enemy content differs from the runtime snapshot.");
                for (int i = 0; i < battle.Enemies.Count; i++) snapshot.EnemyRuntime[i]?.Restore(battle.Enemies[i]);
            }
            return battle;
        }
    }

    private static IReadOnlyList<RuleEnemyRuntimeSnapshot> ExportEnemyRuntime(BattleManager battle)
    {
        if (battle == null) return null;
        var result = new List<RuleEnemyRuntimeSnapshot>();
        foreach (EnemyModel enemy in battle.Enemies) result.Add(enemy != null ? new RuleEnemyRuntimeSnapshot(enemy) : null);
        return result.AsReadOnly();
    }

    public static BattleCommandSession RestoreSnapshotCommands(RunMemorySnapshot snapshot, PlayerState player,
        BattleManager battle = null, TutorialPolicy tutorial = null)
    {
        if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
        var commands = new BattleCommandSession(player, battle, tutorial);
        commands.RestoreDecisionState(snapshot.RefreshUsedThisTurn, snapshot.CommandTurnResolving);
        return commands;
    }

    public static T CopyDto<T>(T value) where T : class
    {
        return RuleJson.Clone(value);
    }
    private static bool HasCurrentNodeSnapshot(CurrentNodeSaveData node)
    {
        return node != null && (HasBattleSnapshot(node.battle) || node.shop != null || node.eventState != null);
    }

    private static bool HasBattleSnapshot(BattleNodeSaveData battle)
    {
        if (battle == null || battle.enemies == null)
            return false;

        for (int i = 0; i < battle.enemies.Length; i++)
        {
            EnemyBattleSaveData enemy = battle.enemies[i];
            if (enemy != null && enemy.enemyId > 0)
                return true;
        }
        return false;
    }

    public static PlayerState CreatePlayer(RunSaveData save, PlayerStateServices services = null, RunDifficultySaveData difficulty = null,
        IRuleContentProvider content = null, RuleProfilePolicy profilePolicy = null)
    {
        return CreatePlayerStatus(save, services, difficulty, false, content, profilePolicy);
    }

    public static MagicModel CreateMagic(MagicSlotSaveData slot)
    {
        if (slot == null || !GameDataDatabase.TryGetMagicData(slot.magicNumericId, out MagicData data))
            return null;

        MagicModel magic = MagicFactory.Create(data, slot.slotIndex);
        magic.RestoreSellPriceState(slot.sellPriceState);
        if (!string.IsNullOrEmpty(slot.modifierId) && GameDataDatabase.TryGetMagicModifierData(slot.modifierId, out MagicModifierData modifierData))
            magic.AddModifier(MagicModifierFactory.Create(modifierData));
        return magic;
    }

    public static MaterialModel CreateMaterialCard(MaterialCardSaveData data)
    {
        return CreateMaterial(data);
    }

    public static PlayerStatus CreatePlayerStatus(RunSaveData save, PlayerStateServices services = null,
        RunDifficultySaveData difficulty = null, bool restoreSavedPlayLimit = false,
        IRuleContentProvider content = null, RuleProfilePolicy profilePolicy = null)
    {
        PlayerSaveData playerData = save != null ? save.player : null;
        if (playerData == null)
        {
            if (services != null || difficulty != null) throw new ArgumentException("A player snapshot is required.", nameof(save));
            return PlayerStatus.CreateDefaultStatus();
        }

        PlayerStatus player = new PlayerStatus(playerData.maxHealth, playerData.gold, services, difficulty ?? save.difficulty, content, profilePolicy);
        using (player.Rules.Enter())
        {
        player.RestoreCurrentHealth(playerData.currentHealth);
        player.DrawCount = playerData.drawCount;
        // 打出上限来自开局配置 maxPlayCount（默认为 8）：旧存档里该字段只是与配置无关联的镜像值，
        // 不用它覆盖配置；将来若出现“局内修改打出上限”的玩法，再在这里补回存档优先。
        if (restoreSavedPlayLimit) player.MaxPlayCount = playerData.maxPlayCount;
        else
        {
            GameDataDatabase.TryGetPlayerStartConfigData(
                !string.IsNullOrEmpty(save.startConfigId) ? save.startConfigId : PlayerState.SelectedStartConfigId,
                out PlayerStartConfigData startConfig);
            player.MaxPlayCount = PlayerState.ResolveMaxPlayCount(startConfig);
        }
        player.RestoreArrowUpgradeState(playerData.arrowUpgradeUnlockedNodeIds, playerData.arrowUpgradePendingNextTurnShield, playerData.permanentRefreshChances);
        player.RestorePaidShopArrowRemovalCount(playerData.paidShopArrowRemovalCount);
        ApplyPreparedBuffs(player, playerData.preparedBuffs);
        player.SetRunRandomState(playerData.runRandomSeed, playerData.runRandomStep);
        player.DrawOrderVersion = playerData.drawOrderVersion;
        player.Relics.Restore(playerData.relics);
        player.PendingRelicOffer = CopyDto(playerData.pendingRelicOffer);
        player.Deck.Clear();
        player.DrawPile.Clear();
        player.DiscardPile.Clear();
        player.ConsumedPile.Clear();
        player.Hand.Clear();
        player.PlayZone.Clear();
        player.MagicBook.Clear();

        for (int i = 0; playerData.deck != null && i < playerData.deck.Length; i++)
        {
            MaterialModel card = CreateMaterial(playerData.deck[i]);
            if (card != null)
                player.Deck.Add(card);
        }

        player.DrawPile.AddRange(player.Deck);

        for (int i = 0; playerData.magicBook != null && i < playerData.magicBook.Length; i++)
        {
            MagicSlotSaveData slot = playerData.magicBook[i];
            MagicModel magic = CreateMagic(slot);
            if (magic != null)
                player.SetMagicAtSlot(magic, slot.slotIndex);
        }

        player.SetMagicSlotLayout(save.magicLayoutSlotCount, save.magicRightNeighborStep);
        if (save.magicReadingOrder != null)
        {
            var ranks = new List<SlotReadingRank>();
            foreach (SlotReadingRankSaveData rank in save.magicReadingOrder)
                if (rank != null) ranks.Add(new SlotReadingRank(rank.slotIndex, rank.rank));
            player.SetMagicReadingOrder(ranks);
        }
        return player;
        }
    }

    private static SlotReadingRankSaveData[] ExportMagicReadingOrder(PlayerState player)
    {
        IReadOnlyList<SlotReadingRank> ranks = player.MagicReadingOrder.Export();
        var result = new SlotReadingRankSaveData[ranks.Count];
        for (int i = 0; i < result.Length; i++) result[i] = new SlotReadingRankSaveData { slotIndex = ranks[i].SlotIndex, rank = ranks[i].Rank };
        return result;
    }

    public static void RestoreMapNodes(RunSaveData save, List<RunMapNodeModel> target)
    {
        target.Clear();
        for (int i = 0; save != null && save.mapNodes != null && i < save.mapNodes.Length; i++)
        {
            RunMapNodeSaveData nodeData = save.mapNodes[i];
            RunMapNodeModel node = new RunMapNodeModel
            {
                fixedSingleChoice = nodeData.fixedSingleChoice,
                leftHidden = nodeData.leftHidden,
                rightHidden = nodeData.rightHidden,
                leftLevel = GetLevel(nodeData.leftLevelId),
                rightLevel = GetLevel(nodeData.rightLevelId),
                selectedLevel = GetLevel(nodeData.selectedLevelId)
            };
            target.Add(node);
        }
    }

    public static RunMapGridModel RestoreMapGrid(RunSaveData save)
    {
        RunMapGridSaveData data = save != null ? save.mapGrid : null;
        if (data == null || data.width <= 0 || data.height <= 0 || data.cells == null || data.cells.Length == 0)
            return null;

        bool legacyGridAvailability = save == null || save.version < 2;
        bool legacyGridReveal = save == null || save.version < 3;
        RunMapGridModel grid = new RunMapGridModel
        {
            width = data.width,
            height = data.height,
            playerX = data.playerX,
            playerY = data.playerY,
            bossMapActive = data.bossMapActive
        };

        for (int i = 0; i < data.cells.Length; i++)
        {
            RunMapCellSaveData cellData = data.cells[i];
            if (cellData == null)
                continue;

            grid.cells.Add(new RunMapCellModel
            {
                x = cellData.x,
                y = cellData.y,
                level = GetLevel(cellData.levelId),
                isBoss = cellData.isBoss,
                isEnd = cellData.isEnd,
                isCompleted = cellData.isCompleted,
                isAvailable = legacyGridAvailability || cellData.isAvailable || cellData.isBoss || cellData.levelId > 0 || (cellData.x == data.playerX && cellData.y == data.playerY),
                isRevealed = legacyGridReveal ? cellData.isBoss || (cellData.x == data.playerX && cellData.y == data.playerY) : cellData.isRevealed,
                isHidden = cellData.isHidden
            });
        }
        grid.ClampPosition(ref grid.playerX, ref grid.playerY);
        return grid;
    }

    public static bool ShouldAutoStartSavedNode(RunSaveData save)
    {
        return save != null && save.runState == BeforeNodeState && save.currentNode != null && save.currentNode.levelId > 0;
    }

    public static LevelData GetSavedCurrentLevel(RunSaveData save)
    {
        return save != null && save.currentNode != null ? GetLevel(save.currentNode.levelId) : null;
    }

    public static BattleNodeSaveData GetSavedBattle(RunSaveData save)
    {
        BattleNodeSaveData battle = save != null && save.currentNode != null ? save.currentNode.battle : null;
        return HasBattleSnapshot(battle) ? battle : null;
    }

    public static ShopNodeSaveData GetSavedShop(RunSaveData save, LevelData level)
    {
        return save != null && save.currentNode != null && level != null && save.currentNode.levelId == level.numericId ? save.currentNode.shop : null;
    }

    public static EventNodeSaveData GetSavedEvent(RunSaveData save, LevelData level)
    {
        return save != null && save.currentNode != null && level != null && save.currentNode.levelId == level.numericId ? save.currentNode.eventState : null;
    }

    public static void RestoreBattle(BattleNodeSaveData data, BattleManager battleManager, PlayerState player)
    {
        if (data == null || battleManager == null)
            return;

        if (player == null || !ReferenceEquals(battleManager.PlayerState, player))
            throw new ArgumentException("Battle/player ownership differs.", nameof(player));
        PlayerStatus status = player as PlayerStatus;
        int seed = status != null ? status.RunRandomSeed : 0;
        int step = status != null ? status.RunRandomStep : 0;
        using (player.Rules.Enter())
        {
        try
        {
        battleManager.ClearEnemies();
        for (int i = 0; data.enemies != null && i < data.enemies.Length; i++)
        {
            EnemyBattleSaveData enemyData = data.enemies[i];
            if (enemyData == null || !GameDataDatabase.TryGetEnemyData(enemyData.enemyId, out EnemyData baseData))
                continue;

            EnemyModel enemy = EnemyFactory.Create(baseData, battleManager.CreateDifficultyContext());
            if (enemy == null)
                continue;
            battleManager.SpawnEnemy(enemy);
            enemy.RestoreBattleState(enemyData);
        }

        RestorePlayerCombat(data.playerCombat, player);
        battleManager.RestoreBattleState((BattlePhase)data.phase, data.continuousCastCount);
        if (data.focusEnemyIndex >= 0 && data.focusEnemyIndex < battleManager.Enemies.Count)
            battleManager.SetFocusTarget(battleManager.Enemies[data.focusEnemyIndex]);
        player.Relics.MarkBattleRestored();
        }
        finally
        {
            // Enemy constructors/intent reconstruction may roll; restoring is not a gameplay decision.
            if (status != null) status.SetRunRandomState(seed, step);
        }
        }
    }

    private static void RestorePlayerCombat(PlayerCombatSaveData data, PlayerState player)
    {
        if (data == null || player == null)
            return;

        player.RestoreBattleBuffs(data.runtimeBuffs);
        player.RestoreBattleDrawOrder(data.battleDrawSeed, data.battleDrawStep);
        Dictionary<string, MaterialModel> deckLookup = BuildDeckLookup(player);
        player.RestoreCombatSnapshot(
            data.shield,
            RestoreCards(data.hand, deckLookup),
            RestoreCards(data.drawPile, deckLookup),
            RestoreCards(data.discardPile, deckLookup),
            RestoreCards(data.playZone, deckLookup),
            RestoreCards(data.consumedPile, deckLookup),
            RestoreCards(data.temporaryMaterialsNextTurn, deckLookup),
            data.extraRefreshChancesThisTurn,
            data.playedCardCountThisTurn);
        player.RestoreRefreshTurnState(data.permanentRefreshChancesUsedThisTurn, data.refreshLimitReductionThisTurn, data.handLimitThisTurn);
        if (data.chargedPlayZoneIndices != null)
        {
            var charged = new List<MaterialModel>();
            foreach (int index in data.chargedPlayZoneIndices)
                if (index >= 0 && index < player.PlayZone.Count) charged.Add(player.PlayZone[index]);
            player.RestorePlayLimitChargedCards(charged);
        }
    }

    private static Dictionary<string, MaterialModel> BuildDeckLookup(PlayerState player)
    {
        Dictionary<string, MaterialModel> lookup = new Dictionary<string, MaterialModel>();
        for (int i = 0; player != null && i < player.Deck.Count; i++)
        {
            MaterialModel card = player.Deck[i];
            if (card != null && !string.IsNullOrEmpty(card.instanceId) && !lookup.ContainsKey(card.instanceId))
                lookup.Add(card.instanceId, card);
        }
        return lookup;
    }

    private static List<MaterialModel> RestoreCards(MaterialCardSaveData[] data, Dictionary<string, MaterialModel> deckLookup)
    {
        List<MaterialModel> cards = new List<MaterialModel>();
        for (int i = 0; data != null && i < data.Length; i++)
        {
            MaterialCardSaveData cardData = data[i];
            if (cardData == null)
                continue;

            MaterialModel card = null;
            if (!string.IsNullOrEmpty(cardData.instanceId) && deckLookup != null)
                deckLookup.TryGetValue(cardData.instanceId, out card);
            if (card == null)
                card = CreateMaterial(cardData);
            if (card != null)
                cards.Add(card);
        }
        return cards;
    }
    public static CurrentNodeSaveData ExportCurrentNode(LevelData currentLevel, PlayerState player, BattleManager battleManager, EventModel currentEvent, PlayerSaveData initialPlayer)
    {
        if (currentLevel == null)
            return null;

        if (player == null)
            return null;

        CurrentNodeSaveData data = new CurrentNodeSaveData
        {
            levelId = currentLevel.numericId,
            initialPlayer = initialPlayer ?? ExportPlayer(player)
        };

        if ((currentLevel.levelType == LevelType.Battle || currentLevel.levelType == LevelType.Elite) && battleManager != null)
        {
            BattleNodeSaveData battle = ExportBattle(currentLevel, battleManager, player);
            if (HasBattleSnapshot(battle))
                data.battle = battle;
        }
        if (currentLevel.levelType == LevelType.Shop && player.CurrentShop != null)
            data.shop = player.CurrentShop.ExportState();
        if (currentEvent != null && IsEventSnapshotLevel(currentLevel.levelType))
            data.eventState = currentEvent.ExportSaveData(currentLevel.numericId);

        return data;
    }

    private static bool IsEventSnapshotLevel(LevelType levelType)
    {
        return levelType == LevelType.Event || levelType == LevelType.Rest || levelType == LevelType.RemoveMaterial || levelType == LevelType.AddMaterial;
    }

    private static BattleNodeSaveData ExportBattle(LevelData currentLevel, BattleManager battleManager, PlayerState player)
    {
        BattleNodeSaveData data = new BattleNodeSaveData
        {
            levelId = currentLevel != null ? currentLevel.numericId : 0,
            phase = (int)battleManager.CurrentPhase,
            chapterNumericId = battleManager.CurrentChapterNumericId,
            isBoss = battleManager.CurrentLevelIsBoss,
            focusEnemyIndex = -1,
            continuousCastCount = battleManager.ContinuousCastCount,
            playerCombat = ExportPlayerCombat(player)
        };

        IReadOnlyList<EnemyModel> enemies = battleManager.Enemies;
        data.enemies = new EnemyBattleSaveData[enemies != null ? enemies.Count : 0];
        for (int i = 0; enemies != null && i < enemies.Count; i++)
        {
            data.enemies[i] = ExportEnemy(enemies[i]);
            if (ReferenceEquals(enemies[i], battleManager.FocusTarget)) data.focusEnemyIndex = i;
        }
        return data;
    }

    private static EnemyBattleSaveData ExportEnemy(EnemyModel enemy)
    {
        if (enemy == null)
            return null;

        return new EnemyBattleSaveData
        {
            enemyId = enemy.NumericId,
            currentHealth = enemy.CurrentHealth,
            shield = enemy.Shield,
            actionIndex = enemy.ActionIndex,
            phase = enemy.Phase,
            deathHandled = enemy.DeathHandled,
            canActThisEnemyTurn = enemy.CanActThisEnemyTurn,
            isMinion = enemy.IsMinion,
            hasSpawnPosition = enemy.HasSpawnPosition,
            spawnPositionX = enemy.SpawnPositionX,
            spawnPositionY = enemy.SpawnPositionY,
            buffs = ExportBuffs(enemy.Buffs),
            consumedOnlyOnceIntentIds = enemy.ExportConsumedOnlyOnceIntentIds(),
            lastResolvedIntentGroupId = enemy.LastResolvedIntentGroupId,
            selectedIntentPhase = enemy.SelectedIntentPhase,
            selectedIntentActionIndex = enemy.SelectedIntentActionIndex,
            selectedIntentGroupId = enemy.SelectedIntentGroupId
        };
    }

    private static PlayerCombatSaveData ExportPlayerCombat(PlayerState player)
    {
        if (player == null)
            return null;

        return new PlayerCombatSaveData
        {
            runtimeBuffs = BuffRuntimeSaveData.Export(player.Buffs),
            battleDrawSeed = player.BattleDrawSeed,
            battleDrawStep = player.BattleDrawStep,
            shield = player.Shield,
            extraRefreshChancesThisTurn = player.ExtraRefreshChancesThisTurn,
            playedCardCountThisTurn = player.PlayedCardCountThisTurn,
            chargedPlayZoneIndices = ExportChargedPlayZoneIndices(player),
            permanentRefreshChancesUsedThisTurn = player.PermanentRefreshChancesUsedThisTurn,
            refreshLimitReductionThisTurn = player.RefreshLimitReductionThisTurn,
            handLimitThisTurn = player.HandLimitThisTurn,
            hand = ExportDeck(player.Hand),
            drawPile = ExportDeck(player.DrawPile),
            discardPile = ExportDeck(player.DiscardPile),
            playZone = ExportDeck(player.PlayZone),
            consumedPile = ExportDeck(player.ConsumedPile),
            temporaryMaterialsNextTurn = ExportDeck(player.TemporaryMaterialsNextTurn)
        };
    }

    private static int[] ExportChargedPlayZoneIndices(PlayerState player)
    {
        var indices = new List<int>();
        foreach (MaterialModel card in player.PlayLimitChargedCards)
        {
            int index = player.PlayZone.IndexOf(card);
            if (index >= 0) indices.Add(index);
        }
        indices.Sort();
        return indices.ToArray();
    }

    private static BuffStackData[] ExportBuffs(IReadOnlyDictionary<BuffEnum, BuffModel> buffs)
    {
        if (buffs == null || buffs.Count == 0)
            return Array.Empty<BuffStackData>();

        List<BuffStackData> results = new List<BuffStackData>();
        foreach (BuffModel buff in buffs.Values)
        {
            if (buff != null && buff.buffType != BuffEnum.None && buff.stack > 0)
                results.Add(new BuffStackData { buffType = buff.buffType, stack = buff.stack });
        }
        return results.ToArray();
    }

    public static RunMapNodeSaveData[] ExportMapNodes(IReadOnlyList<RunMapNodeModel> mapNodes)
    {
        if (mapNodes == null) return Array.Empty<RunMapNodeSaveData>();
        RunMapNodeSaveData[] results = new RunMapNodeSaveData[mapNodes.Count];
        for (int i = 0; i < mapNodes.Count; i++)
        {
            RunMapNodeModel node = mapNodes[i];
            results[i] = new RunMapNodeSaveData
            {
                leftLevelId = node.leftLevel != null ? node.leftLevel.numericId : 0,
                rightLevelId = node.rightLevel != null ? node.rightLevel.numericId : 0,
                selectedLevelId = node.selectedLevel != null ? node.selectedLevel.numericId : 0,
                fixedSingleChoice = node.fixedSingleChoice,
                leftHidden = node.leftHidden,
                rightHidden = node.rightHidden
            };
        }
        return results;
    }

    public static RunMapGridSaveData ExportMapGrid(RunMapGridModel grid)
    {
        if (grid == null || grid.width <= 0 || grid.height <= 0 || grid.cells == null || grid.cells.Count == 0)
            return null;

        RunMapCellSaveData[] cells = new RunMapCellSaveData[grid.cells.Count];
        for (int i = 0; i < grid.cells.Count; i++)
        {
            RunMapCellModel cell = grid.cells[i];
            cells[i] = new RunMapCellSaveData
            {
                x = cell != null ? cell.x : 0,
                y = cell != null ? cell.y : 0,
                levelId = cell != null && cell.level != null ? cell.level.numericId : 0,
                isBoss = cell != null && cell.isBoss,
                isEnd = cell != null && cell.isEnd,
                isCompleted = cell != null && cell.isCompleted,
                isAvailable = cell != null && cell.isAvailable,
                isRevealed = cell != null && cell.isRevealed,
                isHidden = cell != null && cell.isHidden
            };
        }

        return new RunMapGridSaveData
        {
            width = grid.width,
            height = grid.height,
            playerX = grid.playerX,
            playerY = grid.playerY,
            bossMapActive = grid.bossMapActive,
            cells = cells
        };
    }

    public static PlayerSaveData ExportPlayer(PlayerState player)
    {
        PlayerSaveData data = new PlayerSaveData
        {
            maxHealth = player.MaxHealth,
            currentHealth = player.CurrentHealth,
            gold = player.Gold,
            drawCount = player.DrawCount,
            maxPlayCount = player.MaxPlayCount,
            preparedBuffs = ExportPreparedBuffs(player.Buffs),
            runRandomSeed = player is PlayerStatus status ? status.RunRandomSeed : 0,
            runRandomStep = player is PlayerStatus statusForStep ? statusForStep.RunRandomStep : 0,
            drawOrderVersion = player.DrawOrderVersion,
            deck = ExportDeck(player.Deck),
            magicBook = ExportMagicBook(player.MagicBook),
            arrowUpgradeUnlockedNodeIds = CopyArrowUpgradeNodeIds(player.ArrowUpgrades.UnlockedNodeIds),
            arrowUpgradePendingNextTurnShield = player.ArrowUpgrades.PendingNextTurnShield,
            permanentRefreshChances = player.PermanentRefreshChances,
            paidShopArrowRemovalCount = player.PaidShopArrowRemovalCount,
            relics = player.Relics.Export(),
            pendingRelicOffer = CopyDto(player.PendingRelicOffer)
        };
        return data;
    }

    private static string[] CopyArrowUpgradeNodeIds(IReadOnlyList<string> nodeIds)
    {
        if (nodeIds == null || nodeIds.Count == 0)
            return Array.Empty<string>();

        string[] copy = new string[nodeIds.Count];
        for (int i = 0; i < nodeIds.Count; i++)
            copy[i] = nodeIds[i];
        return copy;
    }

    private static MaterialCardSaveData[] ExportDeck(IReadOnlyList<MaterialModel> deck)
    {
        MaterialCardSaveData[] results = new MaterialCardSaveData[deck.Count];
        for (int i = 0; i < deck.Count; i++)
            results[i] = ExportMaterialCard(deck[i]);
        return results;
    }

    private static MaterialCardSaveData ExportMaterialCard(MaterialModel card)
    {
        return RuleMaterialCodec.Export(card);
    }

    private static MagicSlotSaveData[] ExportMagicBook(IReadOnlyList<MagicModel> magicBook)
    {
        MagicSlotSaveData[] results = new MagicSlotSaveData[magicBook.Count];
        for (int i = 0; i < magicBook.Count; i++)
        {
            MagicModel magic = magicBook[i];
            results[i] = new MagicSlotSaveData
            {
                slotIndex = magic.SlotIndex,
                magicNumericId = magic.NumericId,
                sellPriceState = magic.ExportSellPriceState(),
                modifierId = magic.PrimaryModifier != null ? magic.PrimaryModifier.Id : string.Empty
            };
        }
        return results;
    }

    private static BuffStackData[] ExportPreparedBuffs(IReadOnlyDictionary<BuffEnum, BuffModel> buffs)
    {
        if (buffs == null || buffs.Count == 0)
            return Array.Empty<BuffStackData>();

        List<BuffStackData> results = new List<BuffStackData>();
        foreach (BuffModel buff in buffs.Values)
        {
            if (buff != null && buff.buffType != BuffEnum.None && buff.stack > 0 && !buff.IsVisible)
                results.Add(new BuffStackData { buffType = buff.buffType, stack = buff.stack });
        }
        return results.ToArray();
    }

    private static void ApplyPreparedBuffs(PlayerState player, BuffStackData[] buffs)
    {
        if (player == null || buffs == null)
            return;

        for (int i = 0; i < buffs.Length; i++)
        {
            BuffStackData buff = buffs[i];
            if (buff != null)
                player.AddBuff(buff.buffType, buff.stack);
        }
    }

    private static string[] ExportMaterialModifiers(IReadOnlyList<MaterialModifierModel> modifiers)
    {
        List<string> ids = new List<string>();
        for (int i = 0; modifiers != null && i < modifiers.Count; i++)
        {
            string id = GetMaterialModifierId(modifiers[i]);
            if (!string.IsNullOrEmpty(id))
                ids.Add(id);
        }
        return ids.ToArray();
    }

    private static MaterialModel CreateMaterial(MaterialCardSaveData data)
    {
        return RuleMaterialCodec.Create(data);
    }

    public static LevelData GetLevel(int id)
    {
        return id > 0 && GameDataDatabase.TryGetLevelData(id, out LevelData level) ? level : null;
    }

    private static string GetMaterialModifierId(MaterialModifierModel modifier)
    {
        return MaterialModifierFactory.GetId(modifier);
    }

    private static MaterialModifierModel CreateMaterialModifier(string id)
    {
        return MaterialModifierFactory.Create(id);
    }
}
