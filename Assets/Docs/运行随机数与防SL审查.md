# 运行随机数与防 SL 审查

本次仅审查随机数与存档，没有修改随机算法、消费时机或持久字段。结论来自当前生产源码，并以 live Editor 核对敌人 16、19、22、24 的有效 Definition 配置；没有用真实用户档做退出/继续或故障实验。

## 结论

当前不是开局预生成一整条随机数数组，而是保存 **seed + step**，每次请求按这两个值计算一个确定值，再推进 step。这个方法本身可以满足确定性恢复，不需要把整条串存下来；前提是恢复时不额外消费、所有玩法入口都使用它，并保存会影响结果的状态。

目前这三个前提尚未全部满足，因此 **不能认定完整防 SL**。商店已有具体货架和事务保存保护；事件/休息及鹿男恢复存在源码可证的重抽/推进缺口，敌人首次随机意图还有实际的全局 Random 旁路。

| 用户关心的问题 | 当前答案 |
| --- | --- |
| 开局是否生成 seed | 是，正常 PlayerStatus 初始化生成非零 seed。 |
| 是否预生成并保存整条随机数串 | 否，按 seed + step 即时计算，没有随机数数组。 |
| seed / 进度是否进存档 | 是，PlayerSaveData.runRandomSeed / runRandomStep 同份 JSON 保存。 |
| 是否所有玩法都走这个 seed | 否，敌人首次随机意图在绑定 Battle 前走 UnityEngine.Random。 |
| 直接继续已保存商店是否重抽货架 | 正常成功保存后恢复具体商品、价格、已购标记、刷新次数；未证简单反复 load 能免费重抽。 |
| 牌序是否在开局固定 | 否，每次从当前牌堆随机取索引；相同完整状态和调用顺序可重复，只有 seed 相同并不够。 |
| 是否已完全防 SL | 否，下面列出可证恢复缺口与未覆盖边界。 |

## 随机流与持久化

- `Assets/Scripts/Data/RunRandom.cs:3`：CreateSeed 混合 Environment.TickCount 与 Guid.NewGuid().GetHashCode()。Range / NextUInt 对 seed、step 做整数混合再取模，无缓存串。
- `Assets/Scripts/Data/PlayerStatus.cs:22`：NextRunRandomInt 计算后 step++。SetRunRandomState 遇零 seed 会重生，负 step 归零。
- `Assets/Scripts/Data/RunManager.cs:409`：代理到 PlayerStatus；AdvanceRunRandomStep 会额外消费并丢弃一次，正式新战斗在 `Assets/Scripts/HandSystemUI.cs:1645` 使用。
- `Assets/Scripts/Data/RunSaveData.cs:293`：玩家 DTO 保存 runRandomSeed / runRandomStep。`Assets/Scripts/Simulation/RuleRunSnapshot.cs:626` 导出，`:197` 恢复。
- `Assets/Scripts/UnityAdapters/RunSaveSystem.cs:373`：导出并写生产 JSON；LoadRun 反序列化。正常继续入口在 `Assets/Scripts/HandSystemUI.cs:3187` 恢复玩家，之后恢复章节、地图、池和当前节点。

商店、奖励、抽牌、事件等共享一个 step。不同操作数量会改变未来尚未生成的结果，所以“同一个开局 seed”不表示每个未来商店已经独立固定。候选池、遍历顺序、请求范围及调用次数也属于确定性条件。

## 主要缺口

### 事件 / 休息继续会重抽手牌并推进随机流

这是源码可证的恢复链，未用真实槽位复现：

1. 初次进入节点，在配方生成和发牌之后保存了 seed / step。
2. 恢复玩家时，事件/休息的瞬时手牌未保存，只让 DrawPile = Deck，见 `Assets/Scripts/Simulation/RuleRunSnapshot.cs:200`、`Assets/Scripts/Data/RunSaveData.cs:219`。
3. `Assets/Scripts/HandSystemUI.cs:1841` 的休息恢复先 CreateRestEventModel，再 RestoreSaveData 覆盖配方，再 DrawRestOptionsHand。`Assets/Scripts/Simulation/EventRestRules.cs:14` 至少消费两步生成配方，之后再抽两张牌。
4. 同文件 `:1934` 的事件恢复先 new EventModel，再 RestoreSaveData，然后 `:1948` 重新发事件手牌。`Assets/Scripts/Data/EventModel.cs:42` 构造时已经随机化配方，恢复没有回滚这段消耗。
5. 新 UI 的 lastRunCheckpointKey 初值为空，恢复末尾 `HandSystemUI.cs:1849 / :1952` 又能保存新增 step。

在节点仍有选项、正常保存成功的条件下，重复继续可推进同一 seed 的随机流并重抽手牌，不保证每次牌面都不同。保存配方通常会被恢复为原值；问题主要在额外耗流与重新发牌，不能笼统说所有事件配方都会重抽。

### 鹿男的玩法随机缓存未持久化，显示接口会重新抽

- `Assets/Scripts/Enemies/DeerManEnemyModel.cs:3` 的 pendingDisabledMaterial / lastDisabledMaterial / action / phase 缓存不在 EnemyBattleSaveData 或 RuleRunSnapshot.ExportEnemy 中。
- GetSpecialIntentDisplayValue / GetIntentTooltipBuffs 会调用 `:89` GetPendingDisabledMaterial；缓存为空时通过 `:104` NextRelicRandomInt 选元素。`:77` 的正式行动用同一个元素禁用箭头，因此属于玩法随机。
- UI 创建链：`Assets/Scripts/HandSystemUI.cs:6667` → `:9774` → `Assets/Scripts/EnemyIntentView.cs:110` → 鹿男显示 getter。
- 战斗恢复 `HandSystemUI.cs:1626` 先 RestoreBattle；codec 在 `Assets/Scripts/Simulation/RuleRunSnapshot.cs:363` 回滚恢复期间 RNG。随后 `HandSystemUI.cs:1629` 创建新视图又抽一次，`:1641` 保存新增 step。

有效 `Assets/Resources/Config/Enemies/Enemy_016.asset` 已 live 确认首个意图是 Special、value=1。因此在该意图被保存且场景正常创建意图视图时，反复继续存在重新抽禁用元素并推进流的路径。未做槽位运行复现。

### 敌人初始随机意图绕过开局 seed

- `Assets/Scripts/Data/BattleManager.cs:418 / :431` 先 EnemyFactory.Create，`:450` 才赋 enemy.Battle。
- `Assets/Scripts/Enemies/EnemyFactory.cs:24` 返回前 ApplyRuntimeDefinition，触发 EnemyModel.UpdateCurrentIntents / SelectCurrentIntents。
- 此时 `Assets/Scripts/Data/EnemyModel.cs:565` 的 Battle 为空，`:571` 实际走 UnityEngine.Random.Range。
- 有效配置已核：敌人 19 / 22 / 24 的 phase 0 首 loop randomGroupIds 分别是 [1,3]、[1,2,3]、[1,2]，与生产覆盖后的 GameDataDatabase 一致。对应资产在 `Assets/Resources/Config/Enemies/Enemy_019.asset`、`Enemy_022.asset`、`Enemy_024.asset`。

这已经否定“所有玩法随机都来自开局 seed”。纯视觉也使用全局 Unity Random，所以这个玩法结果还可能受表现调用时序影响。不过生产战斗 DTO 保存 selectedIntentGroupId，不能仅凭初始化旁路推断已保存战斗每次继续必重抽首意图。

## 商店已有保护

`Assets/Scripts/ShopPanelUI.cs:195` 为 ShopSession 注入主随机流；ShopOfferGenerator 的品质、商品、弱附魔概率和箭头候选均用该流。

- `Assets/Scripts/Simulation/ShopSession.cs:60`：savedState 存在时直接 RestoreState，不 Generate。
- 同文件 `:312 / :339`：保存、还原货架、分层数量、价格、已购标记、刷新次数、删牌状态与撤销事务。DTO 见 `Assets/Scripts/Data/RunSaveData.cs:245`。
- TryRefreshCore 同步生成、扣款、加次数；`Assets/Scripts/ShopPanelUI.cs:118` 在动画前 NotifyShopTransactionCommitted，委托 `Assets/Scripts/HandSystemUI.RulePresentation.cs:36` 调 ForceSave。购买、删除和撤销也强制保存。

所以常规成功落盘后，继续商店应恢复原货架与费用；本轮没有找到直接 load 已存货架可免费重抽的链。恢复漏洞及不同操作数量仍会改变未来未生成商店。

## 其他随机入口覆盖

| 范围 | 当前入口 / 边界 |
| --- | --- |
| 抽牌、出牌区洗牌 | `Assets/Scripts/Data/PlayerState.cs:264 / :460 / :520 / :1201`，每次随机取当前索引；不是开局固定整套牌序。 |
| 随机箭头 | `Assets/Scripts/Modifiers/RandomArrowModifier.cs:10` → ArrowReadSystem；有效 PlayerStatus 使用主流，rolledMaterial 缓存结果。持久 codec 不含 FullGraph 的全部运行字段，具体可刷时序未证。 |
| 随机目标 / debuff / 箭头升级 | BattleManager、PlayerState、ArrowUpgradeSystem 正常 owner 路径使用主流。 |
| 敌人后续意图 | 绑定 Battle 后使用主流；首次意图旁路见上。 |
| 战斗奖励 / 事件候选与目标 | RewardOfferGenerator / EventRuleServices / EventResolutionSession 由正式 HandSystemUI 注入主流。 |
| 奖励棋盘 | BonusGridSession 按行列抽格；正式 DTO 未存坐标 / 已领取格中途状态。恢复回原检查点不等于任意中途棋盘恢复。 |
| 遗物候选 | RelicOfferService 使用主流，pending offer 可保存；正常精英 / 事件 / 商店发放入口尚未接入，不能把测试候选当已投放。 |
| 进阶 / 一层地图 / Boss组与关卡池 | DifficultyUpgradeSystem、RunMapGenerationRules、RunManager 正常路径使用 PlayerStatus 流。 |
| 二层 PC 地图 | HandSystemUI 从主流抽子 seed，SecondFloorPCMapGenerator 用 System.Random 生成布局；关卡 resolver 继续用主流。现有二层入口仍未接常规流程。 |
| 第一人称二层原型 | StripDungeonMapGenerator 用 Inspector 显式 seed 与重试子 seed，属 Prototype / TestUI。 |
| 纯视觉、音频、身份与时间 | hover音调、死亡碎片、收屏/掉落/纹理随机，以及 GUID / 时间戳不直接决定玩法；不应机械统一进玩法流。 |

## 存档检查点与限制

`Assets/Scripts/HandSystemUI.cs:7194` 的普通保存按 Map / Node key 去重，key 不含回合、手牌或 step；暂停、退出也走普通保存，所以通常回到节点初始化附近。ForceSave 只绕过去重，不绕事务守卫。

`HasUnpersistedChoiceSession` 覆盖整个奖励准备、EventChoiceSession、BattleRewardSession。ShowRewardPanelRoutine 先开始事务再调用普通/Force 保存，所以不能把其中的保存调用当作基础金币已经落盘。当前策略是回到最近完整检查点，尚无正式 PendingChoice 持久化协议。

普通战斗恢复会还原战斗牌堆和选定意图，并回滚构造阶段消耗，跳过新战斗额外步进；不能概括为所有战斗都会重抽。

`GameSessionCheckpoint` 是独立脚本宿主的 seed + 内容/profile/难度 + 命令重放协议，没有替代生产磁盘继续。内存 FullGraph / EnemyRuntime 也不能当成旧 JSON 已保存全部状态。

次要风险只做记录，未运行证明：保存被遗物未 settled 等条件拒绝后 UI 仍可能更新 checkpoint key；写 tmp 后删除旧文件再 Move 有中断窗口；生产保存未见与内存快照同等的 pending-deaths 门槛。当前无本地 JSON 防外部回滚/篡改机制；预生成整条随机串本身也不能解决复制旧档回滚。

## 后续建议（尚未实施）

1. 先让恢复不做新随机决策：恢复已有事件/休息配方和手牌，或在重建时抑制消费并恢复完整状态。
2. 鹿男随机结果应在规则阶段决定，保存缓存，显示 getter 保持只读。
3. 在首次意图选择前绑定 owner / RNG，移除有效玩法上下文中的全局 Random 旁路。
4. 明确防 SL 产品目标：若希望未来商店不受额外战斗/抽牌次数影响，再考虑按玩法独立流或按节点派生 seed。单纯把现有流预生成数组并不修复上述问题。
5. 用隔离存档验证“同节点反复退出/继续后，seed、step、手牌、货架、鹿男元素完全一致”，另验刷新提交和异常中断；不使用玩家正常档作为测试对象。
