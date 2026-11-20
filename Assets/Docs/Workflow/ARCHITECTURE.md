# 架构导航

依据当前实现。Unity 已拆 MaterialMagic.Core → UnityAdapters → Presentation，以及独立 Editor / Tests；Core 不引用 UGUI / TMP / DOTween，仍保留 Unity 数学与作者配置兼容。Standalone 链接同一规则源，无 Unity DLL。先按模块定位，不默认扫描全工程。

## 模块地图

| 模块 | 稳定入口 | 知识 |
| --- | --- | --- |
| 启动/场景 | StartMenuUI、GameInitializer、SceneTransitionManager | [运行与保存](../../../Locus/knowledge/memory/unity-project-understanding/run-save-tutorial.md) |
| 局内状态/地图 | `Assets/Scripts/Data/RunManager.cs`、`Assets/Scripts/Data/RunMapNodeModel.cs`、ChapterGridPanelUI | [地图](../../../Locus/knowledge/memory/unity-project-understanding/maps.md) |
| 战斗/伤害 | `Assets/Scripts/Data/BattleManager.cs`、UnitModel、PlayerState、EnemyModel、BuffModel | [战斗](../../../Locus/knowledge/memory/unity-project-understanding/combat.md) |
| 规则会话 / 同步宿主 | `Assets/Scripts/Simulation/`：BattleCommandSession / BattleTurnDriver、RunSession / GameSession、Choice / Shop / Bonus sessions、RuleRunSnapshot / RuleJson | [实施边界](../逻辑表现分离实施计划.md) |
| 表现/流程编排 | `Assets/Scripts/HandSystemUI.cs`、`Assets/Scripts/HandSystemUI.RulePresentation.cs`、StaggeredPresentationRunner、UIManager | [战斗](../../../Locus/knowledge/memory/unity-project-understanding/combat.md)、[UI](../../../Locus/knowledge/memory/unity-project-understanding/ui-localization.md) |
| 数据加载 | `Assets/Scripts/Data/GameDataReader.cs`、GameContentDatabase、EnemyDefinition | [内容](../../../Locus/knowledge/memory/unity-project-understanding/content.md) |
| 道具 | `Assets/Scripts/Magics/MagicFactory.cs`、`Assets/Scripts/Data/MagicModel.cs` | [内容](../../../Locus/knowledge/memory/unity-project-understanding/content.md) |
| 箭头读取/附魔 | `Assets/Scripts/Data/ArrowReadSystem.cs`、`Assets/Scripts/Modifiers/`、MagicMatchOrderUtility | [箭头](../../../Locus/knowledge/memory/unity-project-understanding/arrows.md) |
| 遗物逻辑 | `Assets/Scripts/Relics/`、PlayerState.Relics、RelicOfferService、ArrowStepRuleResolver | [遗物](../../../Locus/knowledge/memory/unity-project-understanding/relics.md) |
| 道具强化 | `Assets/Scripts/MagicModifiers/`、MagicModifierData.json | [内容](../../../Locus/knowledge/memory/unity-project-understanding/content.md) |
| 事件/奖励/商店 | EventModel、EventPanelUI、RewardPanelUI、ShopPanelUI、ShopLayer | [经济与节点](../../../Locus/knowledge/memory/unity-project-understanding/economy-events.md) |
| 保存/教程 | UnityAdapters/RunSaveSystem、RuleRunSnapshot、TutorialPolicy、TutorialManagerUI | [运行与保存](../../../Locus/knowledge/memory/unity-project-understanding/run-save-tutorial.md) |
| 进阶/解锁/图鉴 | DifficultyUpgradeSystem、UnlockSystem、UnlockProgressSaveSystem、MagicCodexProgressSystem | [成长](../../../Locus/knowledge/memory/unity-project-understanding/progression.md) |
| 平台/埋点 | GameInitializer、Platform/、Analytics/ | [平台](../../../Locus/knowledge/memory/unity-project-understanding/platform.md) |
| 音频/渲染 | AudioManager、VFX/、UI/CrayonUI/、Shaders/ | [视听](../../../Locus/knowledge/memory/unity-project-understanding/presentation.md) |

未加完整路径的脚本在 `Assets/Scripts/` 或其 Data 子目录。各知识页提供精确入口。`Assets/Editor/` 是编辑工具；根 `Editor/ChangeFontWindow.cs` 在 Assets 之外，不当作有效编译入口。`Assets/Scripts/Data/ScriptedMagicModels.cs` 为空，不是新增道具位置。

## HandSystemUI 定位

该类仍集中承担战斗、节点、地图、奖励、保存调用和表现，不是薄手牌控件。按符号查找，避免持久记录容易失效的行号。

| 任务 | 方法 |
| --- | --- |
| 节点开始/读档 | StartLevel、StartSavedLevel、StartBattleLevelRoutine、StartEventLevel、StartShopLevel |
| 地图/二层 | BuildChapterMapGrid、ChapterMapMoveRoutine、BuildSecondFloorMap、EnterSecondFloor |
| 输入/卡牌 | Update、CanUseBattleCardInput、OnCardDragBegin、ApplyCardDragDrop、TryMoveHandCardToPlayZone |
| 回合/事件 | EndTurn、ResolveEndTurnRoutine、ResolveEventEndTurnRoutine、ResolveEventEffectRoutine |
| 匹配/施法 | ResolveCommittedBattleTurn → BattleTurnDriver 一次提交；PlayBattleRuleResolution 播放冻结 trace；MagicReadingOrder 持久槽位 ranks |
| 显示 | RebuildCards、LayoutArea、CreateMagicViews、CreateEnemyView、RefreshEnemyUI |
| 结算 | FinishBattleRoutine、ShowRewardPanelRoutine、FinishReward、StartBattleRewardShop |
| 持久化 | SaveRunProgress、SaveRunProgressForce、SaveCurrentRunAndReturnToStart |
| 演出 | ResolveEnemyIntentsRoutine、RunEnemyDeathPipeline、PlayCommittedDeathCheckpoint；亡语由 BattleDeathResolution 提交 |
| 统计 | EmitNodeEnter、EmitNodeExit、EmitBattleStart、EmitBattleEnd、EmitMagicAcquire |

## 扩展点差异

- 道具：直接继承 MagicModel，重写 ResolveCast；MagicFactory 按 `data.script` 显式 case，漏注册回退空效果基类。
- 敌人：独立 EnemyModel 子类；EnemyFactory 按 numericId 显式 case，再统一应用 EnemyRuntimeDefinition，不能绕过运行难度覆盖。
- 箭头附魔：MaterialModifierModel 无参子类，Definition 的 script 为短类名；工厂在基类所在程序集反射发现，不需要新增 switch。
- 道具强化：MagicModifierFactory 按字符串 id 显式分派，未知 id 为 GenericMagicModifierModel；不是箭头反射工厂。
- Buff：BuffEnum、BuffModel.Create/GetKind、LocalizationKeys、显示图标与具体 Buff 类共同接入。
- 遗物：RelicFactory 按 script 显式注册，未知返回 null；RelicInventory 独立持有状态并按获得顺序分发，纯逻辑候选通过 RelicOfferService。已有逻辑不等于正常渠道/UI接入。
- 节点效果：EventEffectData + Simulation/EventResolutionSession 的实际 case；UI 只消费选择与收据。GainRelic 仍未接入，不能仅凭枚举推断可发遗物。

## 维护

环境/场景改动更新 PROJECT，模块入口/接入契约改动更新本页及对应知识，重复方法更新 Skill。内部实现或数值调整不用重写全表。旧资料恢复档不参与日常导航。当前工作流入口在 `AGENTS.md`。
