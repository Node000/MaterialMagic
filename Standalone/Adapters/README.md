# Bounded standalone adapters (D1)

These files are compiled only when `MATERIALMAGIC_STANDALONE` is defined. They contain host/value compatibility APIs, not copies of Player, Battle, enemy, magic, buff, relic, arrow, difficulty, or unlock rules. They do not reference Unity assemblies, UGUI, TMP, DOTween, or the Editor. No project files or Unity assets are read or written by these adapters.

## Dependencies and real-source closure

Runtime/library dependencies of the adapter code:

- .NET base libraries: `System`, collections, LINQ, reflection, globalization, and Console diagnostics.
- Existing `Newtonsoft.Json`: field-only JsonUtility support and detached profile DTO snapshots. No extra package is introduced by these files.
- Real `UnlockProgressData` from `Assets/Scripts/Data/UnlockDataTypes.cs`. No duplicate DTO is declared here.

The inspected production source set is linked, not copied:

- `Assets/Scripts/Simulation/*.cs`.
- Data: PlayerState, PlayerStatus, BattleManager, EnemyModel, EnemyRuntimeDefinition, BuffModel, MagicModel, GameDataTypes, PlayerModel, UnitModel, ArrowReadSystem, RunRandom, RunManager, EventModel, GameLog, GameDataReader, MapGenConfigData, MagicRaritySystem, LocalizationSystem, MaterialModifierDatabase, MaterialModifierDefinition, MaterialModifierDisplayDatabase.
- `Assets/Scripts/Magics/`, `Enemies/`, `Buffs/`, `Modifiers/`, `MagicModifiers/`, and `Relics/`. The UGUI `MaterialModifierRTChain.cs` now lives under `Assets/Scripts/PresentationData/` and is outside this closure.
- Rules/config now under `Assets/Scripts/Data/`: DifficultyUpgradeSystem, ShopLayer, RunMapNodeModel, MagicMatchOrderUtility, ArrowUpgradeConfig, ArrowUpgradeSystem, EnemySummonLayoutConfig, UnlockSystem and RunSaveData.
- Additional actual source dependencies: `Assets/Scripts/Data/GameContentDatabase.cs`, `EnemyDefinition.cs`, and `UnlockDataTypes.cs`. Production `Simulation/RuleRunSnapshot.cs` supplies the same pure export/restore implementation used by Unity's save adapter.

Production LocalizationSystem, DifficultyUpgradeSystem, AscensionSystem, and UnlockSystem remain the rule/read implementations. EventModel's former MaterialCardView name formatting now calls the real LocalizationKeys API; no UI class facade is provided. The separate `Assets/Scripts/UnityAdapters/RunSaveSystem.cs` persistence class is not linked and needs no fake run-save implementation. Its production compatibility methods forward to the Core snapshot codec.

## Coverage

| Adapter | Covered API | Boundary |
| --- | --- | --- |
| UnityValues | Object.name, TextAsset.text, ScriptableObject.CreateInstance (generic/Type), opaque Material | CLR objects only; no native identity, destroyed-object null semantics, lifecycle callbacks, or rendering |
| UnityValues | SerializeField, CreateAssetMenuAttribute, TooltipAttribute | Metadata only |
| UnityValues | Mathf.Max/Min/Clamp (int/float), Clamp01, RoundToInt, Approximately | RoundToInt uses nearest-even midpoint rounding |
| UnityValues | Vector2 x/y/constructor; Vector2Int x/y/constructor, cardinal values, equality; Color RGBA/constructor/white | Only the value surface required by linked rules |
| UnityValues | ColorUtility.TryParseHtmlString, ToHtmlStringRGB | Hex `#RGB`, `#RGBA`, `#RRGGBB`, `#RRGGBBAA`; named CSS colors unsupported |
| UnityValues | Random.Range(int,int) | Always throws; supply real seeded run RNG or explicit NextRandomInt services |
| UnityResources | Load<T>, LoadAll<T> | Explicit registered values/resolvers only; missing assets return null/empty; no disk lookup |
| UnityJsonUtility | FromJson<T>, FromJson(Type), ToJson, FromJsonOverwrite | Public and private SerializeField fields only, not computed properties; no Unity reference resolution/native serializer emulation |
| UnityHostBoundary | Debug.Log/LogWarning/LogError and configurable LogSink | Defaults to Console.Error; null sink is explicit opt-out |
| UnityHostBoundary | PlayerPrefs.GetString/GetInt with read-only injected snapshot | SetString/SetInt/Save throw unsupported persistence |
| UnityHostBoundary | Application.version, persistentDataPath | Version is `standalone`; save-directory lookup throws |
| StandaloneProfileHost | Configure/Reset; registered RuleProfilePersistence.LoadCurrent/SaveCurrent delegates | Detached read-only outer-profile DTO; writes throw; no duplicate storage facade |

JsonUtility requires object-root JSON. It honors current DTO field initializers for omitted fields; overwrite preserves omitted fields and replaces supplied arrays/lists. Asset references must be registered/connected explicitly, not embedded as Unity instance IDs. JSON metadata cannot request CLR type construction through `$type`.

## Explicit host setup APIs

```csharp
// Use actual host-provided JSON, not an implicit search of the user's checkout.
Resources.Register("Data/MagicData", new TextAsset(magicJson));
Resources.Register("Data/Localization/zh-CN", new TextAsset(chineseJson));

// Real production ScriptableObject config/definition classes are reused.
var modifier = ScriptableObject.CreateInstance<MaterialModifierDefinition>();
modifier.SetData(modifierData, null); // no visual material required by rules
Resources.Register("EnchantConfig/MaterialModifiers/selected_modifier", modifier);

var config = ScriptableObject.CreateInstance<ArrowUpgradeConfig>();
JsonUtility.FromJsonOverwrite(configJson, config);
Resources.Register("Config/ArrowUpgradeConfig", config);

// Startup-only outer host data; neither call opens a preference/progress file.
PlayerPrefs.SetReadOnlySnapshot(stringValues, intValues);
StandaloneProfileHost.Configure(progressSnapshot);
```

Optional resolver hooks are `Resources.Configure(Func<Type,string,UnityEngine.Object> load = null, Func<Type,string,UnityEngine.Object[]> loadAll = null)`. Registry entries take priority for individual loads. Collection loads combine registered and resolver-provided values, deduplicating by CLR reference. Registered LoadAll results are sorted by resource key using ordinal order; resolver order is host-owned. Duplicate registrations fail explicitly. Resource keys are relative and cannot contain rooted/traversal segments.

`Resources.Reset()` clears registry/resolvers only. It does **not** reset production static caches. Configure this process-wide host layer before first content access/world creation. Production cached config references (ArrowUpgradeSystem, BattleManager, MaterialModifierDisplayDatabase, LocalizationSystem) are not world-local or hot-swappable merely because the registry can reset. Per-world catalogs/profiles are supplied through the production execution-context APIs; this adapter does not duplicate those rules or promise per-world configuration swapping.

With the real RuleProfilePolicyHost linked, standalone's absent registered snapshot provider defaults to its deterministic Empty policy. Use an explicit world profile for desired unlock eligibility. StandaloneProfileHost registers only the outer-profile `RuleProfilePersistence` read/rejected-write boundary, not a replacement unlock policy or real persistence implementation.

Language changes through production `LocalizationSystem.SetLanguage` invoke PlayerPrefs persistence and therefore throw; use host startup preferences or the existing non-persistent LoadLanguage entry point. This is an unsupported operation boundary, not a successful saved language change.

## Verification boundary

Files and exact linked-rule references were inspected and a read-only source/API dependency audit was performed. No compiler, tests, package restore, Unity Editor, Play Mode, or build was invoked for this work. Static coverage is not a runtime or compile-pass claim.
