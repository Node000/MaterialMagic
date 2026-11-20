using System;
using System.Collections.Generic;
using System.Text;

public enum GameCommandKind { EnterNode, MoveMap, Battle, NodeCards, SubmitChoice, ContinueEvent, LeaveShop, ShopPurchase, ShopRefresh, ShopUndo, TutorialContinue, ShopRemove }

[Serializable]
public sealed class GameCommand
{
    public GameCommandKind kind;
    public int levelId;
    public int nodeChoiceIndex = -1;
    public MaterialEnum direction;
    public AICommandData battle;
    public string choiceId;
    public int[] candidateIndexes;
    public bool cancelChoice;
    public int offerIndex;
    public string removalCardId;
}

[Serializable]
public sealed class GameCommandLogEntry
{
    public GameCommand command;
    public bool accepted;
    public string rejection;
    public string before;
    public string after;
    public string error;
}

[Serializable]
public sealed class GameSessionLog
{
    public int version = 1;
    public int seed;
    public string contentVersion;
    public string initialFingerprint;
    public List<GameCommandLogEntry> entries = new List<GameCommandLogEntry>();
}

/// <summary>
/// Scene-free full-run host. Owns orchestration only: node transitions, battle commands/driver,
/// event/rest choices, reward decisions, bonus grid and shop transactions use their production sessions.
/// Starting content and map generation are explicit inputs. This does not claim the designed Unity
/// map generator or external profile/storage/SDK adapters are a standalone .NET content pipeline.
/// </summary>
public sealed class GameSession
{
    private readonly GameSessionContent content;
    private readonly AIEntityIds ids = new AIEntityIds();
    private AIBattleHost nodeCards;
    private EventResolutionSession eventResolution;
    private bool rewardIsBattle;
    private readonly Dictionary<string, string> choiceIds = new Dictionary<string, string>(StringComparer.Ordinal);
    public PlayerStatus Player { get; }
    public int InitialMaxHealth { get; }
    public int InitialGold { get; }
    public RunManager Run { get; }
    public TutorialPolicy Tutorial { get; }
    public AIBattleHost Battle { get; private set; }
    public EventModel Event { get; private set; }
    public RewardSession Reward { get; private set; }
    public BonusGridSession Bonus { get; private set; }
    public ShopSession Shop { get; private set; }
    public IChoiceSession Choice => (IChoiceSession)eventResolution ?? Reward;
    public GameSessionLog Log { get; }
    public bool IsFinished => Run.Session.IsTerminal;

    public GameSession(GameSessionOptions options)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        if (options.Seed == 0) throw new ArgumentException("Scripted sessions require an explicit nonzero seed.", nameof(options));
        Tutorial = options.Tutorial;
        InitialMaxHealth = options.MaxHealth;
        InitialGold = options.StartingGold;
        Player = new PlayerStatus(options.MaxHealth, options.StartingGold, options.Services ?? PlayerStateServices.Simulation, options.Difficulty, options.RuleContent, options.Profile);
        using (Player.Rules.Enter()) content = options.CreateContent?.Invoke(Player) ?? options.Content
            ?? throw new ArgumentNullException(nameof(options.Content));
        Player.SetRunRandomState(options.Seed, 0);
        using (Player.Rules.Enter()) options.InitializePlayer?.Invoke(Player);
        Run = new RunManager(Player);
        using (Player.Rules.Enter())
        {
            options.ConfigureRun?.Invoke(Run);
            int length = Run.ActiveChapter?.levelLength > 0 ? Run.ActiveChapter.levelLength : Run.MapNodes.Count;
            Run.Session.ConfigureMapProgress(DifficultyUpgradeSystem.ModifyChapterLength(Math.Max(1, length)), Tutorial?.State.IsTutorialRun == true);
            Run.Session.ActivatePendingBossMap();
            Tutorial?.OnLevelSelectShown(Run.CurrentMapNodeIndex);
        }
        Log = new GameSessionLog { seed = options.Seed, contentVersion = content.Version, initialFingerprint = DiagnosticFingerprint() };
    }

    /// <summary>One explicit decision; exceptions retain the failing command and partially committed fingerprint.</summary>
    public GameCommandLogEntry Execute(GameCommand command)
    {
        if (command == null) throw new ArgumentNullException(nameof(command));
        GameCommand logged = Copy(command);
        if (logged.kind == GameCommandKind.SubmitChoice && logged.choiceId != null)
        {
            PendingChoice current = Choice?.PendingChoice;
            if (current != null) StableChoiceId(current.Id);
            if (choiceIds.TryGetValue(logged.choiceId, out string logical)) logged.choiceId = logical;
        }
        var entry = new GameCommandLogEntry { command = logged, before = DiagnosticFingerprint() };
        Log.entries.Add(entry);
        try
        {
            using (Player.Rules.Enter())
            {
                if (IsFinished) entry.rejection = "RunEnded";
                else entry.accepted = ExecuteCore(command, out entry.rejection);
            }
            entry.after = DiagnosticFingerprint();
            return entry;
        }
        catch (Exception exception)
        {
            entry.error = exception.GetType().FullName + ": " + exception.Message;
            entry.after = DiagnosticFingerprint();
            throw;
        }
    }

    private bool ExecuteCore(GameCommand command, out string rejection)
    {
        rejection = null;
        if (Tutorial?.State.WaitingForContinue == true && command.kind != GameCommandKind.TutorialContinue)
            return Reject("TutorialBlocked", out rejection);
        switch (command.kind)
        {
            case GameCommandKind.EnterNode:
                if (Run.State != RunFlowState.MapSelection) return Reject("WrongPhase", out rejection);
                LevelData selected = command.nodeChoiceIndex >= 0 ? SelectedNodeBranch(command.nodeChoiceIndex) : content.FindLevel(command.levelId);
                if (!IsSelectable(selected)) return Reject("NodeNotAvailable", out rejection);
                return Enter(selected, content.NodePolicy(Run, selected), out rejection);
            case GameCommandKind.MoveMap:
                if (Run.State != RunFlowState.MapSelection) return Reject("WrongPhase", out rejection);
                RunMapCellModel cell = Run.PeekMapDestination(command.direction);
                if (cell == null) return Reject("MoveBlocked", out rejection);
                LevelData cellLevel = cell.isCompleted ? null : RunLevelRules.ResolveMapCellLevel(Run, cell);
                if (cellLevel == null)
                {
                    Run.MoveMapPlayer(command.direction);
                    return true;
                }
                RunNodePolicy cellPolicy = content.GetDestinationPolicy(Run, cellLevel, cell);
                LevelData resolved = RunLevelRules.ResolveSelectedLevel(Run, cellLevel, cellPolicy.IsBoss);
                if (resolved == null) return Reject("MissingLevel", out rejection);
                Run.MoveMapPlayer(command.direction);
                return EnterResolved(resolved, cellPolicy, out rejection);
            case GameCommandKind.Battle:
                if (Run.State != RunFlowState.Battle || Battle == null) return Reject("WrongPhase", out rejection);
                AIBattleStepResult battleStep = Battle.Execute(command.battle);
                if (!battleStep.CommandResult.Accepted) return Reject(battleStep.CommandResult.Rejection.ToString(), out rejection);
                if (battleStep.TurnResult != null && battleStep.TurnResult.Outcome != BattleTurnOutcome.NextPlayerTurn)
                {
                    if (battleStep.TurnResult.Outcome == BattleTurnOutcome.Defeat) Run.Session.EndRun(false);
                    else
                    {
                        RunLevelRules.ResetNodeDeck(Player);
                        Tutorial?.EndBattle();
                        RunTransition transition = Run.Session.BattleWon();
                        if (transition.To == RunFlowState.Reward) OpenBattleReward();
                    }
                }
                return true;
            case GameCommandKind.NodeCards:
                if (nodeCards == null || Event == null && Bonus == null || Choice?.PendingChoice != null) return Reject("WrongPhase", out rejection);
                AIBattleStepResult cardStep = nodeCards.Execute(command.battle);
                if (!cardStep.CommandResult.Accepted) return Reject(cardStep.CommandResult.Rejection.ToString(), out rejection);
                if (command.battle.kind == BattleCommandKind.EndTurn) ResolveNodeTurn();
                return true;
            case GameCommandKind.SubmitChoice:
                PendingChoice pending = Choice?.PendingChoice;
                if (pending == null) return Reject("NoPendingChoice", out rejection);
                if (pending.Id != command.choiceId && StableChoiceId(pending.Id) != command.choiceId) return Reject("StaleChoice", out rejection);
                var selectedIds = new List<string>();
                foreach (int index in command.candidateIndexes ?? Array.Empty<int>())
                {
                    if (index < 0 || index >= pending.Candidates.Count) return Reject("UnknownCandidate", out rejection);
                    selectedIds.Add(pending.Candidates[index].Id);
                }
                ChoiceResult choice = Choice.SubmitChoice(new ChoiceSubmission(pending.Id, command.cancelChoice, selectedIds.ToArray()));
                if (!choice.Accepted) return Reject(choice.Failure.ToString(), out rejection);
                if (eventResolution != null) ResumeEvent();
                else if (Reward.IsComplete) ApplyTransition(rewardIsBattle ? Run.Session.CompleteReward(content.AutomaticShopLevel) : Run.Session.CompleteNode());
                return true;
            case GameCommandKind.ContinueEvent:
                if (Event == null || eventResolution != null || Event.CurrentOptions.Length > 0) return Reject("WrongPhase", out rejection);
                if (Event.TryAdvanceToNextNode()) DrawEventHand();
                else ApplyTransition(Run.Session.CompleteNode());
                return true;
            case GameCommandKind.LeaveShop:
                if (Run.State != RunFlowState.Shop || Shop == null) return Reject("WrongPhase", out rejection);
                Shop.CancelSelection();
                ApplyTransition(Run.Session.CompleteNode());
                return true;
            case GameCommandKind.ShopPurchase:
                if (Shop == null || Run.State != RunFlowState.Shop) return Reject("WrongPhase", out rejection);
                if (command.offerIndex < 0 || command.offerIndex >= Shop.Offers.Count) return Reject("InvalidOffer", out rejection);
                ids.Register(Player, null);
                bool bought = Shop.TryPurchase(Shop.Offers[command.offerIndex], out _, out ShopTransactionFailure failure, ids.FindCard(command.removalCardId));
                return bought || Reject(failure.ToString(), out rejection);
            case GameCommandKind.ShopRemove:
                if (Shop == null || Run.State != RunFlowState.Shop) return Reject("WrongPhase", out rejection);
                ids.Register(Player, null);
                return Shop.TryRemoveMaterial(ids.FindCard(command.removalCardId), out _, out ShopTransactionFailure removeFailure)
                    || Reject(removeFailure.ToString(), out rejection);
            case GameCommandKind.ShopRefresh:
                return Shop != null && Run.State == RunFlowState.Shop && Shop.TryRefresh(out _) || Reject("ShopRefreshRejected", out rejection);
            case GameCommandKind.ShopUndo:
                return Shop != null && Run.State == RunFlowState.Shop && Shop.TryUndo(out _) || Reject("ShopUndoRejected", out rejection);
            case GameCommandKind.TutorialContinue:
                return Tutorial != null && Tutorial.Continue() || Reject("TutorialContinueRejected", out rejection);
            default: return Reject("UnknownCommand", out rejection);
        }
    }

    private bool Enter(LevelData selected, RunNodePolicy policy, out string rejection)
    {
        return EnterResolved(RunLevelRules.ResolveSelectedLevel(Run, selected, policy.IsBoss), policy, out rejection);
    }

    private bool EnterResolved(LevelData level, RunNodePolicy policy, out string rejection)
    {
        RunTransition transition = Run.Session.SelectNode(level, policy);
        if (!transition.Accepted) return Reject(transition.Rejection.ToString(), out rejection);
        Run.BeginLevel(level);
        RunLevelRules.ResetNodeDeck(Player);
        Battle = null; Event = null; Reward = null; Bonus = null; Shop = null; eventResolution = null; nodeCards = null;
        switch (Run.State)
        {
            case RunFlowState.Battle:
                Run.AdvanceRunRandomStep(); // Same pre-spawn seed step as StartBattleLevelRoutine.
                BattleManager manager = content.CreateBattle(this, level);
                Run.SetBattle(manager);
                if (content.IsTutorialBattle(level)) Tutorial?.BeginBattle();
                manager.BeginBattleRules();
                manager.BeginPlayerTurnStartRules(Player.DrawCount, Tutorial != null ? (Func<bool>)(() => Tutorial.TryApplyFixedTurnHand(Player)) : null);
                Battle = new AIBattleHost(new BattleCommandSession(Player, manager, Tutorial), content.Version);
                break;
            case RunFlowState.Event:
                EventData data = content.FindEvent(level);
                if (data == null) throw new InvalidOperationException("Missing event content for level " + level.numericId);
                Event = new EventModel(data, Player.NextRunRandomInt);
                DrawEventHand(); break;
            case RunFlowState.Rest:
                Event = RunLevelRules.CreateRestEvent(Run, level, Player.DrawCount);
                DrawEventHand(); break;
            case RunFlowState.Reward:
                BonusLevelData bonus = content.FindBonus(level);
                if (bonus == null) throw new InvalidOperationException("Missing bonus content for level " + level.numericId);
                Bonus = new BonusGridSession(bonus, Player.NextRunRandomInt);
                Player.DrawBasicMaterialCards(Bonus.DrawCount);
                nodeCards = new AIBattleHost(new BattleCommandSession(Player, tutorial: Tutorial), content.Version);
                break;
            case RunFlowState.Shop: OpenShop(level); break;
        }
        rejection = null;
        return true;
    }

    private void OpenBattleReward()
    {
        var generator = new RewardOfferGenerator(Player, Run.Session.NodeLevel, Run.ActiveChapter, Run.Session.IsBossNode,
            content.Economy, content.RewardServices(Player));
        int choiceGold = generator.BattleGoldChoice();
        int baseGold = generator.BaseGold();
        if (baseGold > 0) Player.AddGold(baseGold, false, playFeedback: false);
        Reward = new RewardSession(Player, generator.GenerateBattleChoices(choiceGold), content.MagicSlotCapacity);
        rewardIsBattle = true;
        Tutorial?.OnRewardPanelShown();
    }

    private void OpenShop(LevelData level)
    {
        RunLevelRules.ResetNodeDeck(Player);
        Shop = new ShopSession(Player, content.Economy, content.MagicSlotCapacity, content.ShopServices(Player));
        Shop.Open(content.ShopLayers(level), nodeSeed: Run.GetShopSeed(level));
        Player.CurrentShop = Shop;
        Tutorial?.OnShopPanelShown();
    }

    private void DrawEventHand()
    {
        int count = Run.State == RunFlowState.Rest ? 2 : Event.Data.drawCount >= 0 ? Event.Data.drawCount : Player.DrawCount;
        Player.DrawCards(count);
        nodeCards = new AIBattleHost(new BattleCommandSession(Player, tutorial: Tutorial), content.Version);
        Tutorial?.OnEventOptionsShown();
    }

    private void ResolveNodeTurn()
    {
        if (Bonus != null)
        {
            var cards = new List<MaterialModel>(Player.PlayZone);
            for (int i = 0; i < cards.Count; i++)
            {
                BonusGridStepResult step = Bonus.Step(cards[i]?.material ?? MaterialEnum.None);
                BonusGridRewardRules.Apply(step, Player, content.ModifyBonusGold, playGoldFeedback: false);
            }
            ApplyTransition(Run.Session.CompleteNode());
            return;
        }
        ArrowReadSequence sequence = ArrowReadSystem.BuildSequence(Player.PlayZone, Player, null);
        EventOptionData option;
        bool matched = Event.TryGetMatchedOption(sequence.Tokens, out option);
        if (matched) Tutorial?.OnEventOptionResolved();
        if (!matched) matched = Event.TryGetExitOption(out option);
        if (!matched && Run.State != RunFlowState.Rest) matched = Event.TryGetDefaultEndOption(out option);
        eventResolution = new EventResolutionSession(Player, Event, option, CreateEventServices(), Run.State == RunFlowState.Rest);
        ResumeEvent();
    }

    private EventRuleServices CreateEventServices()
    {
        EventRuleServices services = content.EventServices?.Invoke(Player);
        if (services == null)
        {
            var generator = new RewardOfferGenerator(Player, Run.Session.NodeLevel, Run.ActiveChapter, Run.Session.IsBossNode,
                content.Economy, content.RewardServices(Player));
            services = new EventRuleServices(Player) { NextRandomInt = Player.NextRunRandomInt, MagicChoices = generator.GetMagicChoices };
        }
        services.MagicSlotCapacity = content.MagicSlotCapacity;
        return services;
    }

    private void ResumeEvent()
    {
        eventResolution.RunUntilChoiceOrComplete();
        if (eventResolution.PlayerDefeated) { Run.Session.EndRun(false); return; }
        if (!eventResolution.IsComplete) return;
        bool advanced = eventResolution.NodeAdvanced;
        eventResolution = null;
        if (!advanced) ApplyTransition(Run.Session.CompleteNode());
        else if (Event.CurrentOptions.Length > 0) DrawEventHand();
        // Text-only next nodes pause for an explicit ContinueEvent, not a UI timing dependency.
    }

    private void ApplyTransition(RunTransition transition)
    {
        if (!transition.Accepted) throw new InvalidOperationException("Node transition rejected: " + transition.Rejection);
        Player.ForceDiscardAll(); Player.ClearCombatState(); Player.CurrentShop = null;
        BattleManager.ClearInstance(Run.CurrentBattle ?? Player.Battle);
        Event = null; eventResolution = null; Reward = null; Bonus = null; nodeCards = null; Shop = null; Battle = null;
        if (transition.To == RunFlowState.Shop) OpenShop(transition.NextLevel);
        else if (transition.To == RunFlowState.MapSelection)
        {
            Run.Session.ActivatePendingBossMap();
            Tutorial?.OnLevelSelectShown(Run.CurrentMapNodeIndex);
        }
    }

    private bool IsSelectable(LevelData level)
    {
        if (level == null) return false;
        if (Run.MapGrid != null && Run.MapGrid.CellCount > 0)
            return ReferenceEquals(Run.MapGrid.GetCurrentCell()?.level, level);
        if (Run.MapNodes == null || Run.CurrentMapNodeIndex >= Run.MapNodes.Count) return false;
        RunMapNodeModel node = Run.MapNodes[Run.CurrentMapNodeIndex];
        return ReferenceEquals(node.leftLevel, level) || ReferenceEquals(node.rightLevel, level);
    }

    private LevelData SelectedNodeBranch(int index)
    {
        if (Run.MapNodes == null || Run.CurrentMapNodeIndex >= Run.MapNodes.Count || index < 0 || index > 1) return null;
        RunMapNodeModel node = Run.MapNodes[Run.CurrentMapNodeIndex];
        return index == 0 ? node.leftLevel : node.rightLevel;
    }

    public IReadOnlyList<AICommandData> GetLegalCardCommands() => (Battle ?? nodeCards)?.GetLegalCommands() ?? Array.Empty<AICommandData>();

    public IReadOnlyList<GameCommand> GetLegalCommands()
    {
        using (Player.Rules.Enter()) return GetLegalCommandsCore();
    }

    private IReadOnlyList<GameCommand> GetLegalCommandsCore()
    {
        var commands = new List<GameCommand>();
        if (IsFinished) return commands.AsReadOnly();
        if (Tutorial?.State.WaitingForContinue == true)
        {
            commands.Add(new GameCommand { kind = GameCommandKind.TutorialContinue });
            return commands.AsReadOnly();
        }
        PendingChoice pending = Choice?.PendingChoice;
        if (pending != null)
        {
            for (int count = pending.MinSelections; count <= pending.MaxSelections; count++)
                AddChoiceCombinations(commands, pending, count, 0, new List<int>());
            if (pending.CanCancel) commands.Add(new GameCommand { kind = GameCommandKind.SubmitChoice, choiceId = StableChoiceId(pending.Id), cancelChoice = true });
            return commands.AsReadOnly();
        }
        if (Run.State == RunFlowState.MapSelection)
        {
            if (Run.MapGrid != null && Run.MapGrid.CellCount > 0)
            {
                foreach (MaterialEnum direction in new[] { MaterialEnum.Fire, MaterialEnum.Wind, MaterialEnum.Water, MaterialEnum.Earth })
                {
                    UnityEngine.Vector2Int delta = RunManager.GetMapDirection(direction);
                    if (Run.Exploration.CanMove(delta.x, delta.y)) commands.Add(new GameCommand { kind = GameCommandKind.MoveMap, direction = direction });
                }
            }
            else for (int i = 0; i < 2; i++) if (SelectedNodeBranch(i) != null)
                commands.Add(new GameCommand { kind = GameCommandKind.EnterNode, nodeChoiceIndex = i });
        }
        else if (Run.State == RunFlowState.Shop)
        {
            commands.Add(new GameCommand { kind = GameCommandKind.LeaveShop });
            ids.Register(Player, null);
            for (int i = 0; i < Shop.Offers.Count; i++)
                if (Shop.CanUseOffer(Shop.Offers[i]) && Shop.CanAfford(Shop.Offers[i]))
                {
                    if (Shop.Offers[i].kind == ShopItemKind.RemoveMaterial)
                    {
                        foreach (MaterialModel card in Player.Deck) if (Shop.IsRemovableMaterial(card))
                            commands.Add(new GameCommand { kind = GameCommandKind.ShopPurchase, offerIndex = i, removalCardId = ids.Card(card) });
                    }
                    else commands.Add(new GameCommand { kind = GameCommandKind.ShopPurchase, offerIndex = i });
                }
            if (!Shop.RemoveArrowUsed && Player.Gold >= Shop.RemoveArrowPrice)
                foreach (MaterialModel card in Player.Deck) if (Shop.IsRemovableMaterial(card))
                    commands.Add(new GameCommand { kind = GameCommandKind.ShopRemove, removalCardId = ids.Card(card) });
            if (Player.Gold >= Shop.RefreshCost) commands.Add(new GameCommand { kind = GameCommandKind.ShopRefresh });
            if (Shop.UndoTransaction != null) commands.Add(new GameCommand { kind = GameCommandKind.ShopUndo });
        }
        else if (Event != null && Event.CurrentOptions.Length == 0)
            commands.Add(new GameCommand { kind = GameCommandKind.ContinueEvent });
        else foreach (AICommandData card in GetLegalCardCommands())
            commands.Add(new GameCommand { kind = Battle != null ? GameCommandKind.Battle : GameCommandKind.NodeCards, battle = card });
        return commands.AsReadOnly();
    }

    public GameSessionObservation Observe()
    {
        using (Player.Rules.Enter()) return new GameSessionObservation(this, (Battle ?? nodeCards)?.Observe());
    }

    internal string StableChoiceId(string actual)
    {
        if (actual == null) return null;
        if (!choiceIds.TryGetValue(actual, out string logical))
        {
            logical = "choice:" + (choiceIds.Count + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            choiceIds.Add(actual, logical);
        }
        return logical;
    }

    private void AddChoiceCombinations(List<GameCommand> commands, PendingChoice pending, int remaining, int start, List<int> selected)
    {
        if (remaining == 0)
        {
            foreach (int index in selected)
            {
                ChoiceCandidate candidate = pending.Candidates[index];
                if (candidate.MagicData != null && (pending.Kind == ChoiceKind.BattleReward || pending.Kind == ChoiceKind.MagicReward) && RewardSession.FirstFreeSlot(Player, content.MagicSlotCapacity) < 0)
                    return;
            }
            commands.Add(new GameCommand { kind = GameCommandKind.SubmitChoice, choiceId = StableChoiceId(pending.Id), candidateIndexes = selected.ToArray() });
            return;
        }
        for (int i = start; i <= pending.Candidates.Count - remaining; i++)
        {
            selected.Add(i);
            AddChoiceCombinations(commands, pending, remaining - 1, i + 1, selected);
            selected.RemoveAt(selected.Count - 1);
        }
    }

    /// <summary>Hidden test/replay fingerprint, not an AI observation or a complete clone/save format.</summary>
    public string DiagnosticFingerprint()
    {
        ids.Register(Player, Run.CurrentBattle);
        var text = new StringBuilder();
        text.Append((int)Run.State).Append('/').Append(Run.CurrentMapNodeIndex).Append('/').Append(Run.CurrentLevel?.numericId ?? 0)
            .Append('/').Append(Player.RunRandomSeed).Append('/').Append(Player.RunRandomStep).Append('/').Append(Player.CurrentHealth).Append('/').Append(Player.Gold)
            .Append('/').Append(Event?.CurrentNode?.id).Append('/').Append(StableChoiceId(Choice?.PendingChoice?.Id)).Append('/').Append(Shop?.RefreshCount ?? 0);
        text.Append('|').Append(new AIBattleDiagnosticSnapshot(new BattleCommandSession(Player, Run.CurrentBattle), ids, null).Fingerprint);
        if (Bonus != null)
        {
            text.Append('|').Append(Bonus.Position);
            for (int i = 0; i < Bonus.CellCount; i++) text.Append(Bonus.GetCell(i).Collected ? '1' : '0');
        }
        if (Shop != null) for (int i = 0; i < Shop.Offers.Count; i++)
        {
            ShopOffer offer = Shop.Offers[i];
            text.Append('|').Append((int)offer.kind).Append(':').Append(offer.price).Append(':').Append(offer.purchased)
                .Append(':').Append(offer.magicData?.numericId ?? 0).Append(':').Append((int)offer.material);
        }
        return text.ToString();
    }

    private static bool Reject(string reason, out string rejection) { rejection = reason; return false; }
    private static GameCommand Copy(GameCommand command) => new GameCommand
    {
        kind = command.kind, levelId = command.levelId, nodeChoiceIndex = command.nodeChoiceIndex,
        direction = command.direction, battle = command.battle?.Copy(),
        choiceId = command.choiceId, candidateIndexes = command.candidateIndexes != null ? (int[])command.candidateIndexes.Clone() : null,
        cancelChoice = command.cancelChoice, offerIndex = command.offerIndex, removalCardId = command.removalCardId
    };
}
