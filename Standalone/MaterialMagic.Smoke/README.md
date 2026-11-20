# Standalone rules smoke

This executable references `MaterialMagic.Core.csproj`, which links the production rule source files. It does not copy Player/Battle/effects, open Unity, call SDKs, save profiles, or modify Scenes/Prefabs.

## Invocation

From the project root, after the main agent's unified build authorization/check:

```sh
dotnet run --project Standalone/MaterialMagic.Smoke/MaterialMagic.Smoke.csproj -- "D:/Unity/Unity Project/MaterialMagic"
```

On the verified development machine only .NET 6/10 runtimes are installed, not the target .NET 8 runtime. After building, launch the DLL with the existing .NET 10 runtime (no dependency installation was performed):

```sh
dotnet build Standalone/MaterialMagic.Smoke/MaterialMagic.Smoke.csproj
dotnet --roll-forward Major Standalone/MaterialMagic.Smoke/bin/Debug/net8.0/MaterialMagic.Smoke.dll "D:/Unity/Unity Project/MaterialMagic"
```

The main agent's actual standalone smoke run passed with `--roll-forward Major`. A plain `dotnet run` built successfully but could not launch on that machine without .NET 8 or the roll-forward option; this is a runtime-selection requirement, not a gameplay failure.

Optional second argument: a JSON exported by `RuleContentArchive.Capture(player.Rules.Content).ToJson()`. This reproduces Unity's already-applied JSON + ScriptableObject precedence, material modifier definitions, and rule configuration. Without this archive, smoke explicitly registers and converts the actual JSON files under `Assets/Resources/Data/`; it **does not** parse Unity YAML or pretend raw enemy JSON includes the saved EnemyDefinition overrides.

```sh
dotnet run --project Standalone/MaterialMagic.Smoke/MaterialMagic.Smoke.csproj -- "D:/Unity/Unity Project/MaterialMagic" "Library/Locus/tmp/content-archive.json"
```

## What the source checks

- Real Resources JSON conversion through the production loaders into a detached `IRuleContentProvider`.
- Shared first-floor `RunMapGenerationRules.Generate`, including the cached chapter Boss.
- Real start config via `PlayerState.ApplyStartConfig`; real enemy, arrow, magic/buff turn through legal `BattleCommandSession` / `BattleTurnDriver`.
- Two interleaved worlds, fixed-seed command replay, and a JSON `GameSessionCheckpoint` restore.
- Real event matching/continuation and real shop generation/purchase-when-affordable/undo/node completion.
- No unrestricted `UnityEngine.Random`: the bounded standalone adapter throws if a seeded dependency was missed.

The bounded event/shop route uses real level records as explicit setup; the generated-map world separately exercises the actual first-floor generator. This is not an AI skill/balance test and does not promise to win a random production run. `Assets/Tests/Editor/GameSessionTests.cs` provides a controlled all-node route (Battle → reward → automatic Shop → Event → Rest → Bonus → Shop → Boss), plus replay and pending-reward checkpoint cases.

## Reusable whole-run API

```csharp
var options = GameSessionFactory.Production(
    seed: 903,
    content: exportedArchive.CreateProvider(),
    profile: RuleProfilePolicy.AllUnlocked,
    startConfigId: "balanced",
    chapterId: 1,
    contentVersion: "content-export-v1");
var game = new GameSession(options);
IReadOnlyList<GameCommand> legal = game.GetLegalCommands();
GameSessionObservation visible = game.Observe(); // no hidden draw pile / RNG / hidden map content
GameCommandLogEntry receipt = game.Execute(legal[0]);

var checkpoint = GameSessionCheckpoint.Capture(game);
var restored = checkpoint.Restore(seed => GameSessionFactory.Production(
    seed, exportedArchive.CreateProvider(), RuleProfilePolicy.AllUnlocked,
    "balanced", 1, "content-export-v1"));
```

`Restore` replaces seed, catalog, profile, difficulty, initial constructor health/gold and services with the checkpoint's captured values and Simulation services, then replays the logged commands to the same decision point. `GameSessionOptions.CreateContent` resolves production economy/automatic-shop records from the actual restored player's catalog. Initialization/map factory delegates must consume scoped catalog/options rather than close over a mutable live world. Pending choices are rebuilt, not partially serialized; local choice identities are mapped to deterministic `choice:N` log identities. Caller-provided content version identifies compatible generators/configuration.

## Limits

- Runtime gameplay feel, PC/PE visuals/clicks, native SDK/login, real save IO, and packaging require separate checks.
- The battle driver preserves the original production terminal-check timing. A player killed by next-turn-start rules can reach a `NextPlayerTurn` result with zero health; the full-run batch reports a no-legal-command sample rather than silently adding a different defeat schedule.
- Diagnostic fingerprints are assertions, not complete mutable-state clones. The checkpoint's actual restoration mechanism is initial catalog/profile/config + same initialization + production command replay.
- `SecondFloorExplorationSession` shares absolute RunGrid Move/Reveal. `FirstPersonGridExplorationSession` is also wired into the strip first-person prototype's Move/Turn/current forward-ray reveal. This does not add a normal second-floor chapter entry or build-scene integration.

Final verification: Core/Smoke build passed with 0 warnings/errors; smoke passed with both real JSON and a Unity-exported catalog including saved SO precedence. Unity assembly compilation/domain reload passed, and the final merged EditMode batch passed 268/268 tests. No Play Mode, visual, real-click, native-device or normal save-slot checks are claimed.
