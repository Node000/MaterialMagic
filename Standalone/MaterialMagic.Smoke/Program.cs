using System.Security.Cryptography;
using UnityEngine;

// Explicit file-reading host. Core itself never searches Resources or touches a real profile/save.
if (args.Length < 1 || args.Length > 2) throw new ArgumentException("Usage: MaterialMagic.Smoke <absolute-project-root> [exported-content-archive.json]");
string root = Path.GetFullPath(args[0]);
string dataRoot = Path.Combine(root, "Assets", "Resources", "Data");
if (!Directory.Exists(dataRoot)) throw new DirectoryNotFoundException(dataRoot);
Resources.Reset();
UnityEngine.Debug.LogSink = null;
using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
foreach (string file in Directory.EnumerateFiles(dataRoot, "*.json", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.Ordinal))
{
    string relative = Path.GetRelativePath(Path.Combine(root, "Assets", "Resources"), file).Replace('\\', '/');
    string json = File.ReadAllText(file);
    hash.AppendData(System.Text.Encoding.UTF8.GetBytes(relative + "\n" + json));
    Resources.Register(relative[..^5], new TextAsset(json));
}
string version = Convert.ToHexString(hash.GetHashAndReset());
// Convert real shipped JSON through production loaders; no fake models/effect implementations.
// SO enemy/modifier overrides are NOT parsed as YAML here. Pass a Unity-exported archive for exact saved SO precedence.
IRuleContentProvider provider = RuleContentProviderHost.CaptureForNewWorld();
RuleContentArchive archive = args.Length == 2 ? RuleContentArchive.FromJson(File.ReadAllText(Path.GetFullPath(args[1])))
    : RuleContentArchive.FromJson(RuleContentArchive.Capture(provider).ToJson());
provider = archive.CreateProvider();
Check(provider.Numeric<LevelData>().Count > 0 && provider.Numeric<EnemyData>().Count > 0, "real content catalog");
Check(provider.Named<PlayerStartConfigData>().ContainsKey("balanced"), "real start config");

GameSessionOptions Options(int seed, bool generatedMap)
{
    IRuleContentProvider catalog = archive.CreateProvider();
    PlayerStartConfigData start = catalog.Named<PlayerStartConfigData>()["balanced"];
    ChapterData chapter = catalog.Numeric<ChapterData>().Values.First(value => value.numericId != TutorialPolicy.TutorialChapterNumericId);
    LevelData battle = catalog.Numeric<LevelData>()[1];
    LevelData eventLevel = catalog.Numeric<LevelData>().Values.First(value => value.levelType == LevelType.Event && catalog.Numeric<EventData>().ContainsKey(value.eventPoolId));
    LevelData shop = catalog.Numeric<LevelData>().Values.First(value => value.levelType == LevelType.Shop);
    EconomyConfigData economy = catalog.Numeric<EconomyConfigData>().Values.First();
    return new GameSessionOptions
    {
        Seed = seed, RuleContent = catalog, Profile = RuleProfilePolicy.AllUnlocked, Services = PlayerStateServices.Simulation,
        MaxHealth = start.maxHealth, StartingGold = start.gold,
        CreateContent = player => new GameSessionContent
        {
            Version = version, Economy = player.Rules.Content.Numeric<EconomyConfigData>()[economy.numericId],
            AutomaticShopLevel = player.Rules.Content.Numeric<LevelData>()[shop.numericId]
        },
        InitializePlayer = player => PlayerState.ApplyStartConfig(player, player.Rules.Content.Named<PlayerStartConfigData>()["balanced"]),
        ConfigureRun = run =>
        {
            if (generatedMap)
            {
                RunMapGenerationResult result = RunMapGenerationRules.Generate(run, run.PlayerStatus.Rules.Content.Numeric<ChapterData>()[chapter.numericId]);
                Check(result.Succeeded && result.Grid.CellCount > 0, "production first-floor generation");
            }
            else
            {
                // Explicit route of REAL level records, for bounded deterministic battle/event/shop smoke.
                // The separate generatedMap run above covers actual production map generation.
                run.AttachMapNodes(new List<RunMapNodeModel>
                {
                    new() { leftLevel = run.PlayerStatus.Rules.Content.Numeric<LevelData>()[battle.numericId], rightLevel = run.PlayerStatus.Rules.Content.Numeric<LevelData>()[battle.numericId], fixedSingleChoice = true },
                    new() { leftLevel = run.PlayerStatus.Rules.Content.Numeric<LevelData>()[eventLevel.numericId], rightLevel = run.PlayerStatus.Rules.Content.Numeric<LevelData>()[eventLevel.numericId], fixedSingleChoice = true },
                    new() { leftLevel = run.PlayerStatus.Rules.Content.Numeric<LevelData>()[shop.numericId], rightLevel = run.PlayerStatus.Rules.Content.Numeric<LevelData>()[shop.numericId], fixedSingleChoice = true }
                });
            }
        }
    };
}

var generated = new GameSession(GameSessionFactory.Production(903, archive.CreateProvider(),
    RuleProfilePolicy.AllUnlocked, contentVersion: version));
Check(generated.Run.MapGrid.CellCount > 0 && generated.Run.ChapterBossLevel != null, "map/boss production identity");
GameSession CreateRoute(int seed) => new(Options(seed, false));
var first = CreateRoute(904);
var second = CreateRoute(905);
Exec(first, first.GetLegalCommands().First(command => command.kind == GameCommandKind.EnterNode));
Exec(second, second.GetLegalCommands().First(command => command.kind == GameCommandKind.EnterNode));
string secondBefore = second.DiagnosticFingerprint();
// Play a real hand card then resolve the actual enemy/arrow/magic/buff turn rules.
GameCommand play = first.GetLegalCommands().FirstOrDefault(command => command.battle?.kind == BattleCommandKind.Play);
if (play != null) Exec(first, play);
Exec(first, first.GetLegalCommands().First(command => command.battle?.kind == BattleCommandKind.EndTurn));
Check(second.DiagnosticFingerprint() == secondBefore, "interleaved world isolation");
Check(GameSessionReplay.Replay(first.Log, CreateRoute).Matched, "real battle seed replay");
GameSession restored = GameSessionCheckpoint.FromJson(GameSessionCheckpoint.Capture(first).ToJson()).Restore(seed => Options(seed, false));
Check(restored.DiagnosticFingerprint() == first.DiagnosticFingerprint(), "full command checkpoint restore");

// Real event model/rules and shop generator/transaction on their own explicit real-content route.
var economyRoute = new GameSession(Options(906, false));
using (economyRoute.Player.Rules.Enter())
{
    LevelData eventLevel = provider.Numeric<LevelData>().Values.First(value => value.levelType == LevelType.Event && provider.Numeric<EventData>().ContainsKey(value.eventPoolId));
    LevelData shop = provider.Numeric<LevelData>().Values.First(value => value.levelType == LevelType.Shop);
    // Route setup is input, not a mutation by an AI policy; all actions below go through sessions.
    economyRoute.Run.AttachMapNodes(new List<RunMapNodeModel>
    {
        new() { leftLevel = economyRoute.Player.Rules.Content.Numeric<LevelData>()[eventLevel.numericId], rightLevel = economyRoute.Player.Rules.Content.Numeric<LevelData>()[eventLevel.numericId] },
        new() { leftLevel = economyRoute.Player.Rules.Content.Numeric<LevelData>()[shop.numericId], rightLevel = economyRoute.Player.Rules.Content.Numeric<LevelData>()[shop.numericId] }
    });
}
Exec(economyRoute, economyRoute.GetLegalCommands().First(command => command.kind == GameCommandKind.EnterNode));
for (int i = 0; i < 30 && economyRoute.Event != null; i++)
{
    IReadOnlyList<GameCommand> legal = economyRoute.GetLegalCommands();
    GameCommand next = legal.FirstOrDefault(command => command.kind == GameCommandKind.SubmitChoice && command.cancelChoice)
        ?? legal.FirstOrDefault(command => command.kind == GameCommandKind.ContinueEvent)
        ?? legal.FirstOrDefault(command => command.battle?.kind == BattleCommandKind.EndTurn)
        ?? legal.First();
    Exec(economyRoute, next);
}
Check(economyRoute.Run.State == RunFlowState.MapSelection, "real event continuation");
Exec(economyRoute, economyRoute.GetLegalCommands().First(command => command.kind == GameCommandKind.EnterNode));
Check(economyRoute.Shop != null && economyRoute.Shop.Offers.Count > 0, "real shop generation");
GameCommand purchase = economyRoute.GetLegalCommands().FirstOrDefault(command => command.kind == GameCommandKind.ShopPurchase);
if (purchase != null)
{
    Exec(economyRoute, purchase);
    Exec(economyRoute, new GameCommand { kind = GameCommandKind.ShopUndo });
}
Exec(economyRoute, new GameCommand { kind = GameCommandKind.LeaveShop });
Check(economyRoute.IsFinished, "run node completion");
Console.WriteLine("PASS real JSON conversion, first-floor map, real battle turn, legal commands, two-world isolation, replay/checkpoint, event, shop, run completion");

static void Exec(GameSession session, GameCommand command)
{
    GameCommandLogEntry result = session.Execute(command);
    Check(result.accepted, "command " + command.kind + ": " + result.rejection);
}
static void Check(bool condition, string description)
{
    if (!condition) throw new InvalidOperationException("Smoke failed: " + description);
}
