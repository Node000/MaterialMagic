# 逻辑 / 表现分离与纯脚本、AI 测试审阅

## 1. 结论与边界

**目前是“部分规则模型已独立、完整游戏流程仍由 UI 驱动”，尚不是可以无场景运行整局的游戏内核。**

现有 `PlayerState`、`BattleManager`、`EnemyModel`、`MagicModel`、Buff / 附魔模型、地图生成器都有可复用的基础，不建议推倒重写。最大缺口不在模型类的数量，而在：

1. 完整规则调度仍在 `HandSystemUI` 的协程中，动画、视图和规则交错执行。
2. 部分表现的存在与完成情况直接决定规则是否执行、何时提交。
3. 商店、奖励、教程、奖励地图的权威状态仍属于 UI 对象。
4. 随机、存档、难度、图鉴、埋点和战斗上下文有进程级静态依赖，难以隔离多个测试世界。
5. 缺少统一的操作、合法性、观察、选择请求和重放接口。脚本能调用底层函数，不等于 AI 能操作完整游戏。

### 本次审阅范围

- 全量扫描 `Assets/Scripts/` 自有 C# 的结构和关键依赖，重点深读战斗、牌堆、道具匹配、死亡、经济、事件、地图、教程、保存、成长、平台入口与现有测试。
- 查看 `Assets/Tests/Editor/` 的 11 个测试文件及测试方法；没有运行这些测试。
- 不对 DOTween、TapSDK、Unity 包内部做架构审计；渲染、音频、动效本身是正常的表现层，不把它们依赖 Unity 当作错误。
- 未逐个敌人 / 道具 / Buff 穷举验证，也未逐个场景 / Prefab 核对 Inspector 绑定。结论是源码架构审阅，不是游戏功能、画面或构建通过声明。
- 审阅期间检测到 `Assets/Scripts/Data/`、`Assets/Scripts/HandSystemUI.cs`、`Assets/Scripts/Relics/` 和保存代码的并发改动。本文按最后读取到的实现描述；源码行号会移动，因此证据优先使用文件 + 方法名。没有覆盖或修改这些工作。
- 本轮仅新增本文，没有修改游戏代码、场景、美术参数、用户存档或平台配置。

## 2. “纯脚本 / AI 测试”的两个层级

### 第一目标：Unity 中无场景的规则测试

在 EditMode 中通过脚本创建一局，载入内容数据，不创建 Canvas、Button、RectTransform、Animator、AudioManager，不经过鼠标点击、不等待真实秒数，即可：

- 开局、地图移动、进入节点；
- 出牌、撤回、排序、换牌、选目标、结算完整回合；
- 处理亡语、召唤、胜败、奖励、商店、事件、休息；
- 回答多步选择，导出状态、检查结果、保存 / 恢复测试快照；
- 固定 seed + 初始条件 + 操作序列，复现相同规则结果。

这个目标可以暂时保留 `Mathf`、`Vector2Int`、Unity 的内容加载适配，不要求第一阶段清除所有 `using UnityEngine`。

### 第二目标：Unity 外的 .NET 批量模拟

需要进一步把 Unity 的数据加载、SO 转换、日志、数学类型、文件路径和 PlayerPrefs 等依赖移到适配层，建立可独立编译的规则程序集与测试宿主。

**建议先完成第一目标，再做第二目标。** 仅将方法改为 public、把协程速度调快、创建隐藏 Canvas 或使用 `-batchmode -nographics`，都不等于规则已独立。AI 自动点 UI 是交互测试手段，不应成为玩法正确性测试的唯一入口。

## 3. 已有的可复用基础

| 模块 | 当前基础 | 仍需注意 |
| --- | --- | --- |
| 玩家 / 牌堆 | `Assets/Scripts/Data/PlayerState.cs` 已有抽牌、牌堆转换、出牌、保留、消耗、Buff 等接口 | 存在音频、图鉴写盘、全局战斗与难度依赖 |
| 战斗原语 | `Assets/Scripts/Data/BattleManager.cs` 已抽出回合起止、敌人行动、单意图 / 单段命中、死亡队列 | 完整回合的正确调用顺序仍由 UI 负责 |
| 道具 / Buff / 敌人 | `Assets/Scripts/Data/MagicModel.cs`、`Assets/Scripts/Magics/`、`Assets/Scripts/Buffs/`、`Assets/Scripts/Enemies/` | 不应因总流程耦合而重写所有效果；优先补调度与上下文 |
| 箭头读取 | `Assets/Scripts/Data/ArrowReadSystem.cs` 已有序列、Token、读取步骤和后置动作数据 | 建序列会涉及规则钩子与随机，不应误当成无副作用的通用预览 |
| 配方匹配 | `Assets/Scripts/MagicMatchOrderUtility.cs` 可用模型与 ranks 匹配 | 生产调用从视图获得候选与 ranks |
| 地图 | `Assets/Scripts/RunMapNodeModel.cs`、`Assets/Scripts/Data/RunManager.cs` 已有格子状态、可达性、移动、Boss 状态 | 消费节点、进入节点、结算离开仍在 UI |
| 地图生成 | `Assets/Scripts/Data/DungeonMapGenerator.cs`、`Assets/Scripts/SecondFloor/SecondFloorPCMapGenerator.cs`、`Assets/Scripts/SecondFloor/StripDungeonMapGenerator.cs` | 已有按 seed 测试的基础，不必连同地图 UI 一起重写 |
| 箭头强化 | `Assets/Scripts/ArrowUpgrades/ArrowUpgradeSystem.cs` 有条件检查与 `TryUnlock` | 配置仍静态加载；生命周期挂钩依赖上层调度 |
| 遗物（并发工作） | `Assets/Scripts/Relics/RelicInventory.cs` 有实例状态、操作作用域、事实 / 效果队列、结算状态与事件 | 是正向基础；不能据此宣称整个游戏已具备独立事件流水或完整 AI 接口 |
| 埋点 | `Assets/Scripts/Analytics/AnalyticsSink.cs` 已有 `IAnalyticsSink` | 可复用 sink 边界，但全局 ledger 与模型 / UI 挂点仍影响测试隔离 |

## 4. P0：优先解除的规则 / 表现反向依赖

这里的 P0 表示“阻碍无场景规则测试的优先级”，不是对线上事故等级的判定。

### F01. 无读取指示器，出牌规则直接不结算

**证据：** `Assets/Scripts/HandSystemUI.cs` → `PlayResolveAnimation`。

方法开头判断 `PlayArea == null || ResolveIndicator == null` 后直接 `yield break`，发生在 `ArrowReadSystem.BuildSequence` 之前。后续箭头基础效果、匹配施法、Token 钩子、读取后置动作都在该方法中。

**影响：** 无 Canvas / 指示器的宿主不能通过生产流程结算出牌；删除或漏绑纯表现对象也会跳过玩法。

**建议：** 提取唯一的规则结算器，负责读取步骤、匹配、施法、死亡屏障和后置动作。指示器、高亮、粒子仅消费规则输出，缺失时只少演出，不少规则。

### F02. 实际施法顺序由场景布局与视图集合决定

**证据：** `Assets/Scripts/HandSystemUI.cs` → `CollectCastableMagicsInLayoutOrder`；`Assets/Scripts/MagicBookVisualOrder.cs` → `ComputeRanks`。

生产调用遍历 `magicViews` 获取 `MagicModel`，读取每个 `RectTransform.anchoredPosition`，再根据 `MagicBookCurveLayout` / `GridLayoutGroup` / `MagicBookRowLayout` 计算 ranks。

**影响：** 仅有玩家的 `MagicBook` 数据并不足以按生产规则确定全部施法顺序；布局是否完成、PC / PE 布局形态、正在拖动的视觉位置都成为测试输入。现有纯位置测试能验证排序算法，不能证明无视图时完整结算与正式运行一致。

**建议：** 保留现行“从上到下、行内从左到右”的玩法语义，不回退成配方长度排序。把阅读顺序提升为明确、可序列化的规则输入 / 槽位顺序：由逻辑持有排列，UI 负责呈现；若布局规格确实需要影响顺序，则将规格与确定性的顺序映射作为数据，而不是规则结算临时读取 Transform。跨平台是否必须完全同序需作为产品约束确认，不能在重构中擅自改变。

### F03. 完整回合与死亡结算被动画时间驱动

**证据：** `Assets/Scripts/HandSystemUI.cs` → `ResolveEndTurnRoutine`、`ResolveEnemyIntentsRoutine`、`PlayResolveAnimation`、`RunEnemyDeathPipeline`、`PlayEnemyDeathItem`。

- 弃牌动画回调完成后才推进敌方回合。
- 敌人意图波纹、攻击演出和帧等待之后才调用 `BeginEnemyAction` / `ResolveEnemyIntentAt` / `ResolveEnemyIntentHitAt`。
- 玩家施法等待粒子命中或视觉超时后才执行 `CastMagic`。
- 死亡入队已有规则 API，但生产中的 `ResolveEnemyDeath` 从 UI 死亡演出管线调用；亡语可能伤害、加盾或召唤，并不只是删除视图。
- `nextEnemyRuleResolveIndex`、`nextEnemyPresentationIndex`、`deathPipelineRunning` 等规则 / 演出闸门均由 UI 管理。

**影响：** 测试需要重建这一套协程调度才能等价于生产路径；随意直接调用底层原语容易漏掉亡语、临时牌或 Buff 生命周期。停协程 / 销毁面板可能停在尚未结算完整的中间状态。

**建议：** 规则调度器按确定顺序推进到“下一次玩家决策”或终局，并在明确屏障排空死亡 / 连锁规则。输出有序的不可变结果事件，演出器按事件安排交错和节奏。若正式玩法要求边算边播，也应让规则步进接口与播放时间解耦，提供无时间等待的脚本驱动器；不要再维护一套测试专用规则顺序。

**必须保留：** 敌方规则严格串行、多段命中逐次结算、清场死亡不触发普通亡语、重复施法与箭头后置动作的现行顺序。

### F04. 购买提交仍依赖动画；金币入账边界已在并发工作中改善

**证据：**

- `Assets/Scripts/HandSystemUI.cs` → `GainGoldAnimated`：最终核对时已改为一次调用 `playerState.AddGold(amount, false)`，计算实际 gained，再将动画回调绑定到局部 displayed 计数。真实金币已不再逐枚入账，这是正确的分离方向。
- `Assets/Scripts/GoldDisplayUI.cs` → `PlayGain` / `OnDisable`：金币 Tween 完成调用显示回调，禁用时 Kill；按上述最新调用方式，中断显示不再撤销已提交金币。保存与后续流程仍在动画结束后。
- `Assets/Scripts/ShopPanelUI.cs` → `CompleteMagicPurchase` / `CompleteMaterialPurchase`：先扣钱、标记已购，再调用动画接口；撤销信息在完成回调登记。
- `Assets/Scripts/HandSystemUI.cs` → `SetShopMagicAtSlotAnimatedRoutine` / `AddShopMaterialAnimatedRoutine`：先等待获取动画，再真正装备道具 / 加入牌组。

**影响：** 同一交易存在“已扣钱、未入库、未登记撤销”的窗口。中断动画、退出、保存时机与恢复行为需要专门处理；不能假定动画一定完成。这是代码路径上的风险，本轮没有运行中断场景复现丢失。

**已更新的审阅结论：** 审阅中途曾读到逐枚 AddGold，会与 `Assets/Scripts/Relics/GoldBonusRelicModel.cs` 的每次正收益加成产生重复触发风险；最终静态核对发现并发工作已将入账改为一次提交，因此不再列为当前缺陷。尚未运行验证，也不把这次源码修正归因于本次审阅操作。

**建议：** 保留最新“一次提交金币、显示追赶”的方式，并把提交从 UI 方法移入规则操作；演出等待不应阻止脚本调度或必要的保存。购买 / 删除 / 撤销作为完整命令：校验、扣款、入库、已购状态、撤销凭据在规则操作内完成，再输出展示事件。若支持跨帧交易，交易状态也必须属于规则模型并可恢复，而不是只存在于回调闭包。

## 5. P1：完整 AI 可操作性与测试隔离缺口

### F05. 出牌 / 换牌合法性只完成了一部分下沉

**证据：** `Assets/Scripts/HandSystemUI.cs` → `CanUseBattleCardInput`、`TryMoveHandCardToPlayZone`、`RefreshPlayZoneCards`、`EndTurn`、`MagicSlotCapacity`。

- PlayerState 已有禁用箭头和出牌次数检查，应该复用。
- 但当前阶段、UI busy、教程约束、基础换牌是否已用、额外换牌机会消耗、教程强制换牌和通知仍由 UI 汇合判断。
- `refreshUsedThisTurn` 属于 HandSystemUI；模型的换牌原语不能代表一次完整、合法的玩家换牌操作。
- 基础道具槽数 `baseMagicSlotCount` 是 HandSystemUI 的序列化字段，容量再应用静态难度；购买、奖励和卖出后的归一化使用该容量。

**建议：** 增加完整语义命令而不是让 AI 直接改集合。命令处理器复用 PlayerState 原语，统一阶段、数量、容量、教程检查，返回稳定的拒绝原因。美术配置不要直接覆盖；先识别真正的玩法配置，再迁移并保持现有值。

### F06. 事件、休息、删牌、附魔选择仍靠 UI 协程 / 回调推进

**证据：**

- `Assets/Scripts/Data/EventModel.cs` 负责配方匹配、节点与计数，但大部分 `EventEffectData` 实际在 `Assets/Scripts/HandSystemUI.cs` → `ResolveEventEffectRoutine` 分支执行。
- `RemoveEventMaterialsRoutine`、`ShowEventMagicRewardRoutine`、`ShowEventMagicModifierRoutine` 等打开面板并等待 `completed`。
- 缺少面板时部分路径直接退出，没有形成一个“等待脚本回答的选择”。
- `Assets/Scripts/EventPanelUI.cs` 持有打字、等待最后点击、选项是否显示等状态；HandSystemUI.Update 根据这些状态与鼠标点击结束事件 / 休息节点。

**影响：** AI 不能只依赖 EventModel 完成正式事件语义；需要伪造面板、点按钮或直接篡改状态。跳过面板往往是跳过收益 / 选择，不是无表现执行。

**建议：** 事件规则返回结构化 PendingChoice：候选 ID、最小 / 最大数量、目标约束、取消规则与续接位置；脚本和 UI 用同一个 SubmitChoice 命令回答。叙事“继续”也是明确操作，打字速度不决定规则状态。

### F07. 商店和战后奖励的权威状态在面板里

**证据：**

- `Assets/Scripts/ShopPanelUI.cs` 持有 offers、价格计算、商品池、刷新次数、已购、删除已用、待选择、交易与撤销；生成和购买规则也在该组件中。
- `Assets/Scripts/RewardPanelUI.cs` → `Show` 每次生成 currentChoices，并持有 settlementClaimed、claimInProgress、领取与完成回调。
- `Assets/Scripts/HandSystemUI.cs` 又持有 pendingRewardMagic、pendingMagicModifier、pendingMaterialModifier 和奖励撤销信息。
- `ShopPanelUI.GetMagicSellPrice` 被 HandSystemUI 卖出逻辑调用：纯经济公式放在 UI 类中。

**影响：** 无场景无法拥有完整商店 / 奖励会话；候选生成与“显示面板”绑定，重建 / 再显示需要小心避免重新随机；测试难以直接检查合法候选、刷新报价、重复领取与取消行为。

**建议：** 提取 ShopState / RewardState 与对应命令处理，价格与候选生成作为规则服务。面板 Bind 已有会话而不是 Show 时创建权威会话。ShopOffer、BattleRewardChoices、RewardArrowOption 等规则数据也应移到非 UI 文件，减少依赖反向传播。

### F08. 奖励地图几乎是一个写在 UI 里的小游戏

**证据：** `Assets/Scripts/RewardGridPanelUI.cs` → `RewardCell`、`BuildGrid`、`ResolvePathRoutine`、`ApplyCellRewardRoutine`。

格子奖励、Collected、坐标、移动边界和路径执行均由 UI 持有。`BuildGrid` 在 gridRoot 为空时直接返回，逻辑格子与视图一起创建；奖励在标记移动 Tween 之后领取。

**建议：** 提取 BonusGridState 与路径规则，生成格子 / 奖励不需要 gridRoot。移动和领取输出事件；界面仅布局与播放路径。测试覆盖边缘停步、重复经过不重复领取、治疗上限、金币加成与确定性。

### F09. RunManager 并未拥有完整运行流程

**证据：**

- `Assets/Scripts/Data/RunManager.cs` 有 RunFlowState、地图移动、池抽取与 BeginLevel 等基础。
- `Assets/Scripts/HandSystemUI.cs` → `Awake`、`StartLevel`、`ChapterMapMoveRoutine`、`FinishBattleRoutine`、`ShowRewardPanelRoutine`、`FinishReward`、`StartBattleRewardShop` 负责开局、读档、消费地图节点、战斗结束、奖励 / 商店转移、清理、Boss 完成与胜败。
- 当前层级、关卡、地图节点索引、runEnded 与多个 pending flag 分散于 RunManager / HandSystemUI；开局模式还依赖当前 Scene 名、菜单静态标志和 SceneTransitionManager。

**影响：** 直接调用 `RunManager.MoveMapPlayer` 只是移动，不等于完成正式“移动并进入节点”；AI 要绕过正常玩法就容易漏消费、保存、Boss 标记或后续商店。

**建议：** 让运行会话统一拥有状态转移，输入明确的 NewRunOptions / RestoreSnapshot，而不是由 Awake 与当前场景推断。场景切换是宿主行为，规则产生“进入节点 / 结束运行”等结果。不把 Debug 伤害、Debug 完成运行等作弊接口当成 AI 正常操作接口。

### F10. 教程是规则控制器，不只是教程 UI

**证据：** `Assets/Scripts/TutorialManagerUI.cs` → `TryApplyFixedTurnHand`、`TryGetForcedRefreshMaterialsForPlayZone`、`CanMoveCardToPlay`、`CanEndTurn`、`ShouldKillTutorialEnemyAfterPlayerTurn`、`AdvanceStepByClick`。

固定手牌、强制换牌、操作权限、教程回合计数和结束条件与段落、镂空、高亮、点击状态在同一个 MonoBehaviour 中。

**建议：** 提取 TutorialState / Policy，规则层处理步骤、允许操作与脚本化牌序；UI 展示段落与镂空，提交 ContinueTutorial。测试必须包含教程路径，而不是默认去掉教程组件后宣称整局可测试。

### F11. 全局战斗 / 运行 / 附魔上下文破坏多世界隔离

**证据：**

- `Assets/Scripts/Data/BattleManager.cs` 的 Instance；`Assets/Scripts/Data/RunManager.cs` 的 Current。
- `Assets/Scripts/Data/EnemyModel.cs` → `QueueOrResolveDeath`：有 Instance 时死亡入全局队列，没有时同步 TriggerOnDie；同一模型的死亡语义取决于外部静态状态。
- EnemyModel 的随机、全体伤害，以及部分 Buff / 敌人 / 附魔也读 Instance。
- `Assets/Scripts/Modifiers/MaterialModifierModel.cs`、`Assets/Scripts/MagicModifiers/MagicModifierModel.cs` 的静态 CurrentContext；`Assets/Scripts/Data/MagicModel.cs` 设置这些上下文。
- `Assets/Scripts/DifficultyUpgradeSystem.cs` 的 activeUpgrades / currentState 是全局运行难度。
- `Assets/Scripts/Enemies/EnemyFactory.cs` 使用静态构造初始化抑制标志。

**影响：** 不能安全假设同时创建两场战斗、AI 分支预测、交错测试或并行模拟互不影响。临时 `new BattleManager` 也不意味着所有内部效果都用该实例。

**建议：** 规则依赖显式的会话上下文或所属 Battle 引用；难度、随机、内容与成长快照属于该会话。阶段性可以保留旧单例外观用于 UI 兼容，但内核不能回读它。先增加“两局交错执行不串状态”测试，再逐步拆除静态依赖。

### F12. 确定性随机尚未成为完整会话契约

**证据：**

- `Assets/Scripts/Data/RunRandom.cs` 有 seed + step 的确定性 Range；`Assets/Scripts/Data/PlayerStatus.cs` 已保存随机状态。
- `Assets/Scripts/Data/ArrowReadSystem.cs`、`Assets/Scripts/Data/BattleManager.cs`、`Assets/Scripts/Data/EnemyModel.cs`、`Assets/Scripts/Data/EventModel.cs`、`Assets/Scripts/Data/MagicRaritySystem.cs` 等在没有指定状态 / 回调时回落 Unity Random。
- EventModel 构造时随机化配方并直接写 `Data.nodes[].options[].recipe`；同一份共享内容数据若用于多个会话，会有互相覆盖的风险。
- 商店 / 奖励 / 奖励格子的随机调用部分由打开和构建 UI 触发。

**建议：** 会话显式注入规则 RNG，测试不静默回落到 Unity 全局随机；表现随机继续留在表现层。克隆事件运行数据或单独持有配方结果，内容表只读。重放输入除了 seed，还要包括内容版本、难度、解锁快照、开局配置与初始状态。预览 / 获取合法动作不得推进规则 RNG。

### F13. 保存与成长副作用未与规则操作隔离

**证据：**

- `Assets/Scripts/RunSaveSystem.cs` → `ExportCurrentNode` 调用 `ShopPanelUI.TryExportCurrentState`。
- `Assets/Scripts/ShopPanelUI.cs` → `TryExportCurrentState` 使用 `FindObjectOfType` 并要求面板 activeInHierarchy；player 参数没有用于核对面板所属玩家。
- 保存路径固定于 Application.persistentDataPath，槽位通过 PlayerPrefs，运行状态还读取 RunManager.Current / DifficultyUpgradeSystem。
- `Assets/Scripts/Data/PlayerState.cs` → `SetMagicAtSlot` 直接 `MagicCodexProgressSystem.MarkMagicDiscovered`；后者在 `Assets/Scripts/MagicCodexProgressSystem.cs` 写真实槽位文件。
- `Assets/Scripts/UnlockSystem.cs` → `IsUnlocked` 读当前档案；`ProcessRunEnded` 读写成长和待显示消息。内容候选与进阶因此可能受到开发者当前存档影响。

**影响：** 无 UI 保存不了完整商店状态；测试仅装备道具也可能改变真实图鉴。换一台机器 / 槽位可能得到不同候选。现有 RunSave DTO 是持久化基础，但不是完整的任意中间状态模拟快照。

**建议：** 分离纯 Export / Restore、序列化与持久化。正式存档与测试内存存储共用数据转换，不共用真实用户路径。把 ProfileState / 解锁快照作为输入，发现、解锁、保存请求作为提交后的效果，由适配器处理。AI 搜索用单独 SimulationSnapshot，不强迫正式断点存档覆盖每个演出中间态。

**顺带确认的保存缺口：** 商店刷新价格使用 UI 的 refreshCount，但当前 ShopNodeSaveData / ExportState 未保存该计数，Show 会重置为 0；此外普通 SaveRunProgress 仍按节点检查点去重。这些都应进入保存回归测试，不能把“已有保存方法”等同于逐命令可恢复。

## 6. P2：次一级依赖与边界整理

### F14. 模型仍直接触发音效和埋点

`Assets/Scripts/Data/PlayerState.cs` 与 `Assets/Scripts/Data/EnemyModel.cs` 在伤害 / 金币函数里直接访问 AudioManager；伤害还修改全局 RunAnalyticsLedger。由于有空判断，缺 AudioManager 不一定阻止规则，因此优先级低于 F01–F04；但这是明确的逻辑到表现依赖，隔离测试可能播放声音、污染统计。

建议由规则输出 DamageResolved / GoldChanged 等事实，音频、UI、统计分别订阅。当前遗物事件只是一个可参考的局部事件边界，不能不经梳理就当全游戏展示事件使用；表现事件应保存当时的值，而不是只有以后会变化的模型引用。

### F15. 内容加载、显示辅助与模型混放

- `Assets/Scripts/Data/EventModel.cs` 的配方显示调用 `MaterialCardView.GetMaterialName`。
- 多个模型直接提供本地化 Name / Description；`Assets/Scripts/Data/GameDataReader.cs`、`Assets/Scripts/Data/MaterialModifierDatabase.cs` 等直接加载 Resources。
- BattleManager 加载 `Assets/Scripts/EnemySummonLayoutConfig.cs` 对应的表现布局配置，生成召唤坐标；选最左敌人又使用 SpawnPositionX。
- 当前 Assets 下未发现 asmdef，规则与 UI 主要处于同一运行程序集，没有编译期依赖护栏。**没有 asmdef 不代表现有 Editor 测试失效。**

建议先区分“游戏位置 / 目标顺序”与“像素位置 / 缩放”。如果最左是玩法概念，规则应持有稳定逻辑排序 / 坐标，由显示映射为像素；不要直接移除现有位置字段改变选目标结果。SO 可以继续作为作者配置，进入规则内核时转只读数据。最后再拆 Core、UnityAdapters、Presentation 与 Tests 程序集，并检查反射工厂的发现范围。

### F16. 启动、平台、设置与渲染的测试应分层

`Assets/Scripts/GameInitializer.cs`、`Assets/Scripts/Platform/TapTapAuthService.cs`、`Assets/Scripts/SceneTransitionManager.cs` 是 Unity / SDK 适配边界，不要求把实际登录、加载画面、渲染特效变成纯计算。

- 平台策略 / 授权状态转移可通过 fake 登录与 fake 授权存储测试；原生 SDK 成功与取消仍需设备集成验证。
- 音量、语言、存档选择的策略与 PlayerPrefs 写入可以隔离；真正显示、声音和设备行为不能被纯规则断言替代。
- `Assets/Scripts/UI/`、`Assets/Scripts/VFX/` 的渲染和动画保留为表现；本次未逐 Shader / 画面验证。
- 运行时构建布局、按名称找对象、硬编码颜色等是已有表现维护问题；它们不应成为本轮核心规则抽离的前置全量重做，更不能顺手改美术参数。

### F17. 二层原型的生成与游走分离程度不同

`Assets/Scripts/SecondFloor/SecondFloorPCMapGenerator.cs` 和 `Assets/Scripts/SecondFloor/StripDungeonMapGenerator.cs` 已有无主场景生成与固定 seed 测试。`Assets/Scripts/SecondFloor/SecondFloorStripDungeonFirstPersonPrototype.cs` 的玩家坐标、朝向、前进、转向、揭示与相机 / UI 刷新仍在同一 MonoBehaviour 中。

若后续要让 AI 游走，提取探索状态与 Move / Turn 命令即可，不必重做生成算法。当前原型抵达 Boss 格只设置提示，不能将它当作已接通正式整局流程。

## 7. 现有测试覆盖与缺失

### 已有 11 个 Editor 测试文件

位于 `Assets/Tests/Editor/`：

- ArrowReadSystemTests、RandomArrowModifierTests、ProliferatingArrowModifierTests：读取与特定附魔。
- AttributeDisabledBuffTests、PlayerRoundBuffLifetimeTests：禁用箭头与部分 Buff 生命周期。
- ForceDiscardAllTests：保留 / 强制清牌边界。
- EnemyDeathQueueTests：死亡入队、顺序、去重、清场 / 兜底。
- MagicMatchOrderTests：匹配与视觉顺序计算。
- EnemyContentDatabaseTests：敌人运行数据独立性与稳定 ID。
- SecondFloorPCMapGeneratorTests、StripDungeonMapGeneratorTests：地图约束和确定性。

这些证明工程已有可测试原语与回归资产，不是“完全没有测试基础”。例如死亡测试手动取队列并 ResolveEnemyDeath，是规则可抽离的直接证据；但它不覆盖生产 UI 调度何时排空队列。

### 本次未发现对应的完整流程测试入口

- 完整生产回合：基础箭头 → 配方 → 重复施法 → 亡语 / 召唤 → 回合末 → 敌方多段 → 下一回合。
- 全程无场景的地图 → 战斗 → 奖励 → 商店 → 下节点 → Boss → 结束运行。
- 商店完整交易 / 撤销、奖励幂等、事件多步 PendingChoice、教程无点击执行。
- 保存恢复与继续执行等价；两局交错隔离；带表现与不带表现结果一致。
- 正常 AI 命令协议、合法动作枚举、观察快照、命令重放和批量模拟宿主。
- 遗物系统在审阅期间仍有新增，不把开始时的测试目录清单当成未来覆盖声明。

本轮不运行 Play、测试、编译或构建：审阅不需执行可能写真实图鉴 / 存档的规则，也避免在并发实现中争用 Editor 与编译状态。

## 8. 建议的最小目标架构（提案，尚未实现）

不引入完整 ECS、全量事件溯源或为每个字段建立接口。保留现有模型，以少量明确边界补齐调度：

```text
UI / AI / 测试脚本
        │ 相同语义命令
        ▼
运行会话：统一状态、合法性、节点与选择流程
        ├─ 战斗结算调度器：复用 BattleManager / PlayerState / 效果模型
        ├─ 事件 / 商店 / 奖励规则与会话状态
        └─ RNG、内容快照、难度与成长快照
        │
        ├─ 决策状态 / 终局结果 / 不可变规则事件
        └─ 保存、成长、埋点等提交后效果
                 ▼
Unity 适配器 / 演出器 / 内存测试适配器
```

### 建议的脚本 / AI 接口能力

以下名称仅示意，不是当前存在的 API：

```csharp
session = CreateRun(options, content, profileSnapshot, services);
observation = session.Observe();
actions = session.GetLegalActions();
result = session.Execute(command);
// 自动规则推进到下一次决策 / 终局；不等待动画秒数。
// result 包含 Accepted、RejectReason、Events、PendingChoice、Terminal。
snapshot = session.ExportSimulationSnapshot();
```

最少支持这些语义：

- StartRun / RestoreRun、MoveOnMap、EnterNode（是否合并由现行玩法决定）。
- PlayCard / ReturnCard / ReorderPlayCards / RefreshCards / SetFocus / ResolveTurn。
- ReorderMagicBook / SellMagic、ClaimReward / SkipReward。
- BuyOffer / RefreshShop / RemoveCard / UndoPurchase / LeaveShop。
- SubmitChoice / CancelChoice / ContinueNarrative / ContinueTutorial。

不要只给 AI 一个能任意改 HP / Gold / 集合的调试 API；那可以构造测试夹具，却不能证明实际操作合法。

### 观察与重放要求

- 卡牌、敌人、道具、商品、选择请求使用稳定 ID，不用 Transform 路径、按钮名或显示文字作为规则身份。
- 区分玩家可见观察与测试完整快照；AI 正常游玩不应自动拿到隐藏地图和未来随机信息。
- 多步选择显式存在于状态，包含可选目标、数量、取消规则和请求 ID；重复 / 过期响应被拒绝。
- 合法动作查询和预览必须不改 HP、Buff、牌堆、RNG 或永久存档。
- 命令执行有明确稳定点；未结算的连锁 / 死亡不冒充已结束回合。
- 固定规则输入、seed / step、内容版本、profile / 难度与命令日志可以重放。
- 可以暂时用统一 seed + step，不必第一阶段强制拆多条 RNG 流；表现随机必须独立。

## 9. 建议实施顺序与每阶段验收

### 阶段 A：建立无视图的完整战斗路径

1. 从 HandSystemUI 抽出完整读牌 / 施法 / 亡语 / 敌方行动调度，复用现有原语。
2. 明确道具逻辑阅读顺序与规则目标位置，不再在结算时从视图采集候选。
3. 统一规则稳定点与结果事件，让演出器消费；支持跳过 / 倍速而不改变结果。
4. 以已读场景配置的当前玩法值迁移，不修改美术与手感值，不改现有规则顺序。

**验收：** 不加载场景，构建固定牌序、道具、敌人，脚本执行完整回合；包含亡语连锁、多段、重复箭头、临时 / 保留与玩家死亡，断言完整最终状态及事件顺序。加载同样初始规则状态的正式入口也必须调用同一调度器。

### 阶段 B：经济与交互选择独立

1. 金币和购买改为规则提交、显示追赶。
2. 提取商店、战后奖励、奖励格子状态。
3. 抽出事件效果与通用选择续接机制，保留现行先后顺序。

**验收：** 带 / 不带 GoldDisplay、开 / 关动画所得金币和遗物触发一致；购买立即有完整结果；拒绝重复领取、余额不足、无槽位、非法目标与重复选择。刷新 / 撤销 / 退出 / 恢复行为可脚本断言。

### 阶段 C：整局流程、教程与测试隔离

1. Run 会话拥有开局、节点消费、转移、Boss 完成与终局；场景只作为呈现宿主。
2. 教程规则与教程显示拆开。
3. 持久化、profile、难度、内容、统计使用会话级输入 / 适配；优先消除导致串局的静态访问。
4. 增加不写用户路径的内存保存 / 成长适配和完整模拟快照。

**验收：** 固定 seed 的 AI / 脚本完成一整局，覆盖至少战斗、事件、休息、奖励、商店和 Boss；双会话交错执行不串局；导出恢复继续执行与未中断结果一致；没有真实存档 / 图鉴 / 埋点副作用。

### 阶段 D：独立程序集与 Unity 外批量模拟

1. 在依赖已经明确后建立 Core / Adapters / Presentation / Tests 编译边界。
2. 转换 Unity 专有数据与加载器，检查反射工厂和序列化兼容。
3. 加命令日志、种子批次、失败样本输出与超时 / 循环预算。

**验收：** 独立宿主无 Unity Editor / 图形设备运行同一规则程序集；失败样本能用 seed + 配置快照 + 命令日志在编辑器复现。

## 10. 不能被纯规则测试替代的验收

- UI 是否真的能点：EventSystem.RaycastAll、实际命中目标、遮罩与 CanvasGroup。
- PC / PE 布局、拖拽落点到语义命令的映射、文本溢出与本地化字体。
- 粒子是否打到正确目标、动画快慢 / 交错、音效和画面效果。
- 场景 / Prefab Inspector 引用、保存迁移、平台构建、原生登录与授权。

这些应保留少量针对性的 PlayMode / 集成 / 视觉测试。理想结果不是“AI 不再测 UI”，而是“玩法正确性不靠点 UI 证明，UI 测试只验证输入适配与呈现是否正确”。

## 11. 最推荐的下一步

**先做阶段 A，并同时修正 F04 仍存在的交易提交边界；金币沿用最新的一次入账实现继续下沉。**

交付标准不是类拆得更多，而是能够给出一段无需场景的脚本，走与正式游戏相同的规则入口，执行完整回合并验证结果；随后逐步扩展到整局。没有这条可执行链路，单纯新增 Controller / Service 名称不会解决当前问题。
