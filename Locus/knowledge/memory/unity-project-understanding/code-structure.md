---
id: kd_80101230-4fa5-476b-99d3-8ddd587540e8
type: memory
path: unity-project-understanding/code-structure.md
title: code-structure
inheritInjectMode: true
summaryEnabled: true
commandEnabled: false
readOnly: false
aiMaintained: true
explicitMaintenanceRules: true
createdAt: 1779519783691
updatedAt: 1779874533830
---

# code-structure

## Summary
Unity 项目结构与系统缓存：自定义脚本主要在 `Assets/Scripts/`，DOTween 可用；数据系统用 `Resources/Data/*.json` + `JsonUtility`，本地化按 `zh-CN*.json` 分表；战斗、Buff/Modifier、法术、事件、奖励、地图和 UI 主要入口与注意事项记录在正文。弹窗/提示框最高渲染层级由 `PopupLayerUtility` 统一设置独立 Canvas sortingOrder=9000。

<!-- locus:maintain-rules:start -->
- Record only Unity project structure knowledge and lookup info that reduce repeated exploration
- Maintain only project-derived engineering understanding, including directory responsibilities, system entry points, asset relationships, runtime entry points, and config mappings
- Write user-supplied design goals, gameplay intent, product direction, and solution decisions into Design
- Prioritize directory responsibilities, core system entry points, key scenes, prefabs, ScriptableObjects, assemblies, and config mappings
- Record verified asset relationships, runtime entry points, key dependencies, and common lookup paths
- Remove temporary investigation traces, one-off task residue, unverified guesses, and expired cache
<!-- locus:maintain-rules:end -->

<!-- locus:body:start -->
- 项目自定义运行时代码主要放在 `Assets/Scripts/`。
- 项目已导入 DOTween，运行时代码可引用 `DG.Tweening`；DOTween 文件位于 `Assets/DOTween/`。
- `JuicyMotion` 是通用 UI/物体交互动效脚本，路径为 `Assets/Scripts/JuicyMotion.cs`，实现 `IPointerEnterHandler`、`IPointerExitHandler` 和 `IPointerClickHandler`；Inspector 提供悬停/点击触发、缩放幅度、抖动幅度、悬停倾斜、弹性、Motion 时间参数。当前悬停会同时轻微倾斜和缩放。
- `Assets/Scripts/PopupLayerUtility.cs` 为弹出提示框/信息框统一应用最高渲染层级：给目标 `RectTransform` 添加/配置独立 `Canvas`，`overrideSorting=true`，`sortingOrder=9000`。普通提示动画仍用各自 `CanvasGroup` 控制透明度。
- `Assets/Scripts/Data/BattleManager.cs` 是战斗目标选择代码层接口：维护 `PlayerState`、敌人列表、`FocusTarget` 和 `CurrentCastTarget`；`BeginCastTarget()` 会复用已存在的 `CurrentCastTarget`，没有时才选择目标；`GetTargetEnemy()` 优先返回 `CurrentCastTarget`，再按集火/随机选择。施法粒子与实际结算目标必须先调用 `BeginCastTarget()` 锁定同一个目标，避免随机目标被重复抽取导致表现和伤害不一致。
- 基础数据系统位于 `Assets/Scripts/Data/`：`GameDataReader` 使用 `Resources.Load<TextAsset>` + `JsonUtility` 读取 `Assets/Resources/Data/*.json`；`GameDataDatabase` 缓存 Magic/Enemy/Event/Level/RewardPool/Chapter/Tag 表。Magic/Enemy/Event/Level/RewardPool/Chapter 通过 `numericId` 建立数字 ID字典，Tag/PlayerStartConfig 使用字符串 id 读取。
- 本地化系统位于 `Assets/Scripts/Data/LocalizationSystem.cs`，主表为 `Assets/Resources/Data/Localization/zh-CN.json`；`LocalizationSystem.LoadLanguage` 还会叠加读取同语言的 `zh-CN_Buff.json`、`zh-CN_Material.json`、`zh-CN_Modifier.json`、`zh-CN_Enemy.json`、`zh-CN_Event.json`、`zh-CN_Tag.json`。Buff、素材、Modifier、Tag、敌人名/意图文本 UI 应通过 `LocalizationKeys` 或 `LocalizationSystem.GetText` 接入，不要硬编码中文。
- 当前基础模型/枚举位于 `Assets/Scripts/Data/`：`MagicData`/`MagicModel`、`EnemyData`/`EnemyModel`、`LevelData`、`RewardPoolData`、`ChapterData`、`EventData`/`EventModel`、`BuffEnum`/`BuffModel`、`MaterialEnum`/`MaterialModel`、`TagData`、`PlayerState`。`BuffModel` 已独立在 `Assets/Scripts/Data/BuffModel.cs`，通过虚函数处理回合开始/结束、抽牌/弃牌后、施法、获取行动、攻击、死亡、过期等时序；`PlayerState` 与 `EnemyModel` 都持有 `Dictionary<BuffEnum, BuffModel>` 并触发这些时序。
- 具体敌人 Model 脚本按用户偏好拆分到 `Assets/Scripts/Enemies/`，命名为 `XXEnemyModel.cs`；`EnemyFactory.Create(EnemyData)` 根据 `EnemyData.numericId` 创建具体敌人类，战斗入口 `HandSystemUI.StartBattleLevel` 不直接 `new EnemyModel(data)`。
- `HandSystemUI.PlayResolveAnimation` 在播放法术粒子前调用 `battleManager.BeginCastTarget()` 锁定目标，然后 `CastMagic` 再次调用 `BeginCastTarget()` 复用该目标；`MagicModel.Cast` 结束每次施法后通过 `EndCastTarget()` 清除锁定。
- 已实现 Buff/Debuff 分类枚举 `BuffKindEnum`（Buff/DeBuff/Neutral）以及易损、迟缓、虚弱、电弧、燃烧、威力强化、下回合燃烧、护盾反射、额外抽牌、额外换牌、坚固等 Buff 模型。坚固由 `HandSystemUI.BeginPlayerTurn` 在起始抽牌和下回合临时素材入手后，对当前所有手牌一次性添加 `SturdyModifier`，后续换牌/额外抽牌不再参与同一个 Buff 结算。
- `MaterialModel` 支持 `modifiers`、`enhancementIds`、`isTemporary`；Modifier 类在 `Assets/Scripts/Modifiers/`，当前包括 `TemporaryModifier`、`ChargeModifier`、`VortexModifier`、`SturdyModifier`。临时素材可被换牌/用于出牌，但在换牌返回、回合结束回牌时不回抽牌堆而是消失。临时素材消失动画由 `Assets/Shaders/UI/TemporaryCardDissolve.shader` 支持。
- `PlayerState` 支持 `TemporaryMaterialsNextTurn`、`AddTemporaryMaterialNextTurn`、`AddTemporaryMaterialToHand`、`ConsumeTemporaryMaterialsNextTurn`；`HandSystemUI.BeginPlayerTurn` 抽牌后会把下回合临时素材加入手牌。
- 新法术脚本集中在 `Assets/Scripts/Data/ScriptedMagicModels.cs`，`MagicFactory.Create(MagicData, slotIndex)` 根据 `MagicData.numericId` 创建继承 `MagicModel` 的具体法术类；新增/奖励/默认法术创建应走 `MagicFactory`，不要直接 `new MagicModel(data)`。
- `MagicModel.Cast(PlayerState, BattleManager)` 会触发施法时序；玩家法术伤害会走 `OnAttack` / `AfterAttack`。
- 法术数据位于 `Assets/Resources/Data/MagicData.json`，本地化文本位于 `Assets/Resources/Data/Localization/zh-CN.json`；当前表为 32 个基础元素法术（点燃到大地之手），文本 key 使用 `magic.<id>.name/desc` 风格。`MagicData.element` 存法术属性，用于 `MagicItemView` 背景色；`MagicData.tagIds` 存关键词，`MagicItemView` 从 `Assets/Resources/Data/TagData.json` 和 `zh-CN_Tag.json` 读取名称/描述，只有名称和描述都存在的 Tag 才显示在法术提示右侧独立弹窗中。
- 事件 UI 在 `Assets/Scripts/EventPanelUI.cs` 与 `Assets/Scripts/EventOptionView.cs`；事件选项预制体为 `Assets/Prefabs/UI/EventOption.prefab`，`EventPanelUI.optionPrefab` 负责实例化选项。事件节点若没有选项，最后一段文本点完后 `HandSystemUI` 会判定通关。
- 战斗奖励 UI 在 `Assets/Scripts/RewardPanelUI.cs`；点击“获得法术”后会运行时创建 `RewardMagicChoicePanel` 作为 `RewardPanel` 的同级覆盖面板，并把 Scene 中已有的 3 个 `MagicItemView` 奖励项临时 reparent 到该覆盖面板中显示；关闭奖励选择时必须把这 3 个奖励项 reparent 回 `RewardPanel` 并恢复锚点/位置/缩放，否则下一场奖励会因留在隐藏覆盖面板内而布局异常。三选一面板有“返回”按钮；选择法术只高亮并设置 pending reward，面板保持打开以便改选，直到点击法术槽完成替换后才关闭并标记法术奖励已领取。
- 地图 UI 在 `Assets/Scripts/MapPanelUI.cs`；`HandSystemUI.FinishReward` 会先将 `MapPanelUI` 的 marker 设为旧节点，再递增 `currentMapNodeIndex`，让结算后地图动画从刚完成节点移动到新节点。`StartLevel` 会写入当前 `RunMapNodeModel.selectedLevel` 并刷新节点视觉，未选路线由 `MapPanelUI.GetChoiceColor` 暗化。
- `Assets/Scenes/SampleScene.unity/DebugBattleUI/SettingsPanel` 由 `SettingsPanelUI` 管理，包含 `MusicSlider`、`SfxSlider`、`ReturnStartButton`、`CloseButton`；音量滑条接入 `AudioManager`，返回按钮通过 `SceneTransitionManager` 或 `SceneManager` 回到 `StartScene`。
- `Assets/Resources/Data/RewardPoolData.json` 当前有一个 `numericId=1` 的全法术奖励池；`LevelData.rewardPoolId` 指向奖励池，胜利奖励从奖励池的数字法术 ID 中抽取。
- `PlayerState` 管理 `Deck`（完整素材列表/玩家牌组）、`DrawPile`（本场战斗可抽牌堆）、`Hand`、`PlayZone`，已没有弃牌堆；进入战斗/事件时 `HandSystemUI.ResetBattleDeckState` 会用 `Deck` 重建 `DrawPile`，战斗胜利时 `RestoreBattleDeckState` 清空手牌/出牌区并再次恢复 `DrawPile`，保证素材列表回到正常状态。
<!-- locus:body:end -->
