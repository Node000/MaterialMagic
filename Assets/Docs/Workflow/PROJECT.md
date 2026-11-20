# 项目现状与开发入口

依据当前代码、配置与本轮连接的 Unity Editor。规则分离已完整编译，合并 EditMode 268 / 268 通过，独立 .NET smoke 通过；补充 PC / PE 隔离运行与真实射线交互记录见 [运行验收](../逻辑表现分离运行验收.md)。这些结果不代表正式槽位恢复、真机或所有画面通过。

## 产品与技术

工程名 MaterialMagic，产品文案为“有氧地下城”。素材/方向箭头排列生成读取 Token，匹配道具配方并施法，局内通过地图进入战斗、事件、奖励、商店与休息。当前具体规则见 [当前玩法](../../../Locus/knowledge/design/current-game.md)。

| 项目 | 现状 |
| --- | --- |
| Unity | 2022.3.62f2c1，见 `ProjectSettings/ProjectVersion.txt` |
| 渲染/UI | URP 14.0.12、UGUI 1.0.0、TMP 3.0.7；主要为 Scene/Prefab UI |
| 输入/动效 | Legacy Input Manager；DOTween 位于 `Assets/DOTween/` |
| 数据 | Resources JSON + SO；内容 JsonUtility，运行存档 Newtonsoft.Json |
| 平台 | PC/PE 独立主体场景，TapSDK core/login 4.10.9；Steam 仅条件编译分支 |
| 测试设施 | Test Framework 1.4.6；规则 / Commands / Choices / Run / 隔离 / checkpoint 等合并 268 项 EditMode 已通过 |
| 源码 | Core / UnityAdapters / Presentation / Tests / Editor 使用独立 asmdef，7 个规则目录以 asmref 归 Core；Standalone 链接生产规则源 |

## 目录与版本管理

| 路径 | 放什么 |
| --- | --- |
| `Assets/Scenes/`、`Assets/Prefabs/` | 场景与可复用对象；正式构建入口以 `ProjectSettings/EditorBuildSettings.asset` 为准。 |
| `Assets/Scripts/` | `Data/`、`Simulation/` 等目录组成 Core；`UnityAdapters/` 对接 Unity，Presentation 负责 UI 与表现。模块入口见 [架构地图](ARCHITECTURE.md)。 |
| `Assets/Resources/`、`Assets/Settings/` | 运行时加载的数据与资源，以及渲染设置；`Assets/Resources/LocalSettings/` 内是本地凭据，不入库。 |
| `Assets/Editor/`、`Assets/Tests/Editor/` | Unity 编辑器工具和 EditMode 测试。仓库根目录的 `Editor/` 在 `Assets/` 外，不是 Unity 编辑器程序集入口。 |
| `Assets/Docs/`、`Locus/knowledge/` | 项目文档与共享项目知识；从 [文档索引](../INDEX.md) 按需查阅。 |
| `Packages/`、`ProjectSettings/` | Unity 依赖清单、锁文件及共享工程设置；本机注入的 `Packages/com.farlocus.locus/` 不入库。 |
| `Standalone/` | 独立 .NET 规则验证项目，链接生产规则源；跟踪源码与手写 `.csproj`，忽略各级 `bin/`、`obj/`。 |

提交 Unity 资源应带上对应 `.meta`。`Assets/Resources/LocalSettings.meta` 是已跟踪的目录元文件，不包含目录内的凭据。根目录由 Unity 生成的 IDE 工程、Locus 生成的 `Directory.Build.props`（含本机绝对路径）、Locus 会话和构建缓存均留在本地。

## 运行与场景

- 主入口 `Assets/Scenes/StartScene.unity`：StartMenuUI、GameInitializer、SceneTransitionManager。
- PC：`Assets/Scenes/SampleScene_PC.unity`；PE：`Assets/Scenes/SampleScene_PE.unity`。共享逻辑改动可能同时影响两端及 PvOnly。
- 当前磁盘 Build Settings：StartScene、PE 启用；PC 列出但禁用。这是当前配置，不是禁止维护 PC，也不证明 PC 包可启动。
- Android/iOS 编译宏选择 PE；编辑器可用 `forceMobileSceneInEditor` 强制 PE。直接 Play 主体场景通常进入 `directSampleDebugRun`，不等同于从菜单开始的正式存档流程。
- `Assets/SampleScene_PC_SecondFloor.unity` 位于 Assets 根目录；有代码加载入口但没有普通流程调用者，也未列入构建列表。
- 独立演示/实验：`Assets/Scenes/PvOnlyScene.unity`、`Assets/Scenes/ShaderExperiments/ShaderArtLab.unity`、两个 SecondFloorStripDungeon 测试场景。不要将原型规则或实验 Renderer 当正式玩法。

## 数据权威来源

| 内容 | 当前编辑/读取入口 |
| --- | --- |
| 开局 | `Assets/Resources/Data/StartConfig.json`；旧 PlayerStartConfig.json 不在当前 PlayerStartConfigData 查表入口 |
| 道具 | MagicData.json + `Assets/Scripts/Magics/MagicFactory.cs` 按 script 创建；rarity 有效，element 不在当前 MagicData 类型 |
| 敌人 | 先读 Data/Enemies JSON，再由 `Config/GameContentDatabase` 中同 ID 的 EnemyDefinition 覆盖；当前 27 份引用 |
| 箭头附魔 | `Assets/Resources/EnchantConfig/MaterialModifiers/` 的 MaterialModifierDefinition；不是 MaterialModifierData.json |
| 道具强化 | MagicModifierData.json，MagicModifierFactory 按 id 创建；与箭头附魔不是同一系统 |
| 经济/池 | EconomyConfig、RewardPoolData、ShopProductPoolData、ChapterData/LevelData；另经解锁/进阶过滤 |
| 多语言 | Localization 目录中英主表 + UI/Tutorial/Buff/Material/Modifier/MagicModifier/Enemy/Event/Tag 补表；字段 key/text |
| 持久化 | `RunSaveSystem` 版本 4；纯 codec 为 RuleRunSnapshot，真实 IO 在 UnityAdapters；run_slot_N/history_slot_N，槽位 1–3；教程临时隔离 |

敌人 JSON 导入器会覆盖现有 Definition 内容并替换数据库列表；不作为无副作用校验。结构化资产只用 Unity API；不要改原始 YAML。数值细表从配置读，不在多份文档抄写。

## 平台服务当前配置

StartScene 的 GameInitializer：backend=TapTap、enableAnalytics=true、requirePrivacyConsent=true、autoLoginOnStartup=true、runTapTapInEditor=false。编辑器默认跳过 TapSDK；真机按同意状态启动。

首启先询问数据收集，同意后初始化 SDK，再自动登录；拒绝不初始化整个 TapSDK，也不登录，正常游玩。设置关闭停止自有上报、丢弃待发数据，不立即结束当前登录态；下次启动不再初始化，原生预置事件不能假定立即关闭。完整入口见 [平台模块](../../../Locus/knowledge/memory/unity-project-understanding/platform.md)。

## 验证与任务边界

先集中实现，再一次批量收尾，遵守当前任务的校验预算和授权；默认不跑全库哈希或全套测试。逻辑分离实施批已获 3 分钟扩展，规则测试、编译和 standalone 结果见实施计划；后续 Play 交互只覆盖运行验收报告列出的隔离路径。最低成本静态检查不代表运行通过。

日常代码任务按改动选择规则测试或目标交互，不默认全套。UI 真正点击需 RaycastAll/ExecuteEvents；字体缺字与语言布局靠目标画面判断。构建前核对平台/场景/本地凭据和已有工作，不猜构建命令。

- 架构导航：[ARCHITECTURE.md](ARCHITECTURE.md)
- 项目 Skill：[INDEX.md](../../../Locus/knowledge/skill/INDEX.md)
- 当前缺口：[KNOWN_GAPS.md](KNOWN_GAPS.md)
- 文档目录：[INDEX.md](../INDEX.md)

当前未覆盖项集中在 KNOWN_GAPS。随机数为 seed + step 按需生成并保存，已有恢复耗流与敌人初始意图旁路，详见 [只读审查](../运行随机数与防SL审查.md)；尚未实施随机数修复。日常维护保留用户与并发改动，不自动改美术值、后台或真实存档，不保留测试内容。
