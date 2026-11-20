---
id: kd_c817b79d-4847-449c-9cd0-e38ada2b7d43
injectMode: inherit
injectAgents:
- '*'
aiEditMode: inherit
---

# 运行、保存与教程隔离

## 正常入口

`Assets/Scenes/StartScene.unity` 的 StartMenuUI 选择槽位、开局配置与进阶，再经 SceneTransitionManager 进入主体场景。`Assets/Scripts/HandSystemUI.cs` 的 Awake 检查 GameSceneEntryRequested/场景转场标记；直接 Play 主体场景通常建立 directSampleDebugRun，不写正式检查点。Debug 场景通过不证明正常入口可存档。

`Assets/Scripts/Data/RunManager.cs` 管章节池、地图与局内随机，`Assets/Scripts/Simulation/RunSession.cs` 持正式节点状态转移；`Assets/Scripts/Data/RunMapNodeModel.cs` 管格子。PlayerStatus携带seed/step按需生成并保存，不预生成串；正常收益随机走此链，但敌人首次意图仍有全局Random旁路。`GameSession` 是共享会话的整局脚本host；Production factory使用真实开局与一层生成。

## 保存契约

`Assets/Scripts/UnityAdapters/RunSaveSystem.cs` 持版本4正式三槽IO、PlayerPrefs选槽与history（最多100条）；`Application.persistentDataPath/Save/` 下run_slot_N.json、history_slot_N.json，N=1..3。纯DTO导出/重建已在 `Assets/Scripts/Simulation/RuleRunSnapshot.cs`，RunSaveSystem委托该规则，不把Resources当用户档。

正式DTO含牌库/道具/附魔、地图/池/Boss组、进阶、seed/step、节点战斗/商店/事件；MaterialCardSaveData递归linkedCards，MagicSlot按numericId/modifierId重建，magicReadingOrder另存槽位阅读rank。v4含遗物/pending及有状态Buff runtime；旧档缺字段兼容，不触发获得/施加链。保ID不等于保所有运行字段，旧JSON不是完整内存图；循环linkedCards正式导出明确拒绝，不静默裁断。遗物未settled或faulted不可保存。

HandSystemUI.SaveRunProgress按Map/Node检查点去重；Force仅绕去重，不绕directSampleDebugRun与事务guard。`HasUnpersistedChoiceSession` 从战斗基础金币准备起至奖励事务收尾、以及整个EventChoiceSession/BattleRewardSession存在期间拦普通/Force保存，不仅检查当前是否PendingChoice，防基础金币、即时效果、取消/多阶段选择被半途落盘。商店从player.CurrentShop导出offers/undo/refreshCount/layerOfferCounts，不查面板。奖励棋盘正式DTO仍无坐标/已领取中途快照，保持原节点边界，不能逐格强存收益。

`RunMemorySnapshot` 是进程内分支：显式catalog/profile、Combat/命令状态，加[JsonIgnore] FullGraph与EnemyRuntime保共享/循环卡图、具体modifier/buff/敌派生runtime。只在settled决策点捕获，死亡/遗物队列中拒绝；JSON往返会失去这两个内存分支，不能称完整跨进程快照。

`GameSessionCheckpoint` 是另一种跨进程方案：归档content/profile/difficulty/初始生命金币/seed与完整命令日志，使用同一factory初始化后重放到相同决策/PendingChoice并核验指纹；不是序列化半个PendingChoice。Choice nonce归一为choice:N，恢复强制Simulation服务。Factory须从恢复player的scoped catalog创建适配器（CreateContent），不能闭包旧世界；contentVersion/factory兼容性由调用方负责。指纹是断言，不是完整状态克隆；此协议不替代正式槽位IO。

普通保存未传sceneName，EnterSecondFloor显式传，后续空值覆盖风险仍需按当前代码核验。事件/休息恢复未存手牌且先构造随机配方再覆盖，会额外耗流、重发手牌并再次保存step；鹿男缓存未持久化、恢复后显示getter重抽。商店savedState直接恢复具体货架，成功刷新事务在动画前Force保存，未证简单load能免费刷货架。仅只读审查未修，详见 `Assets/Docs/运行随机数与防SL审查.md`。

直接Debug只挡两条检查点保存，不挡Awake.EmitAscensionSelect直接读unlock、默认services图鉴写入、正式StartShopLevel读run或结果页清档。运行隔离必须在SubsystemRegistration后、目标Awake前注入无IOservices/profile并检查旁路；不能宣称directSampleDebugRun本身保证零用户档读写。未授权不清真实档。

## 教程

RunSaveSystem.BeginNewTutorialRun/ConsumeStartingTutorialRun/EndTutorialRun 控制临时上下文；SaveCurrentRun 与 RecordRunEndAndClearCurrentRun 在教程中跳过，不清普通运行档。DifficultyUpgradeSystem.InitializeNewRun 对教程用A0，教程开局固定配置和金币10。

`Assets/Scripts/Simulation/TutorialPolicy.cs`/TutorialState是真共享门控与固定序列，TutorialManagerUI负责窗口/段落与输入适配；BattleCommandSession、BattleTurnDriver及GameSession使用同一policy。chapter100、battle1001、event level1002、rest201、boss1005、event numeric1001；1×5教学地图无独立商店格。第1回合两火、第2回合三土，刷新固定火火水；动作页不能用Continue跳过。GameSession发地图提示，WaitingForContinue时仅接受TutorialContinue，不能靠直接命令绕过。

13页由 CacheSteps 映射场景命名：Map_Panel、Map_Movement、Map_StepLimit、Battle_Info、Battle_EnemyInfo、Battle_ArrowBase、Battle_MagicBook、Battle_PlayLimit、Battle_Play、Battle_Refresh、Reward_Claim、Shop_BuyHint、Event_Options。流程由代码触发，不仅由Hierarchy顺序决定。

正文空行拆段，StepSplitsParagraphs 指定哪些页使用；Cutout/Cutout2/Cutout3 按段切换。TutorialInputBlockerUI 和快捷键门控共同拦输入，不能只挡鼠标。PE ResolveStepKey 优先 key+_pe，缺失回退共用key；旧“PE永远只有PC方位文案”已失效。

文字表在中英Tutorial JSON，Scene已有窗口/遮罩/高亮，勿运行时重建。文案入口 `Assets/Docs/教程文本与顺序审阅.md`。GameSessionTests全节点/PendingReward checkpoint及地图Continue→两回合教程Play/Refresh→PendingReward checkpoint→商店链，纳入268/268通过批次。另补PC/PE隔离战斗、奖励/商店真实射线交互及奖励期保存guard；正式档槽/教程画面与真机仍未验，证据见 `Assets/Docs/逻辑表现分离运行验收.md`。
