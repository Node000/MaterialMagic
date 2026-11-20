---
id: kd_a54f916c-e569-4cdd-b14e-d6ff216a9509
injectMode: inherit
injectAgents:
- '*'
aiEditMode: auto
name: materialmagic-battle-rule-change
description: 修改伤害、Buff、回合、施法顺序、箭头读取、牌堆和死亡队列时使用；保持规则链、随机与生命周期，选择最小检查。
summary: 修改伤害、Buff、回合、施法顺序、箭头读取、牌堆和死亡队列时使用；保持规则链、随机与生命周期，选择最小检查。
skillEnabled: true
skillSurface: command
---

# 战斗规则修改

适用：伤害/护盾/Buff、回合、施法/箭头读取、敌人行动与死亡、牌堆生命周期。先读combat模块；触及Token再读arrows，不默认拆HandSystemUI。

## 实施步骤

1. 沿真实触发定位：输入/回合入口→模型规则→Buff/强化链→结果→表现。读受影响函数与相近实现，确认目标是主动攻击、直接伤害、Buff还是纯表现；先说明当前与目标差异。
2. 改最小规则入口，保持source/目标、逐击次数、护盾与生命结果、死亡入队、随机seed/step和预览无副作用。出牌区调度先看BattlePlayZoneResolutionSession，死亡屏障看BattleDeathResolution，UI不能再次Cast / ResolveEnemyDeath / ApplyArrowReadAfterAction；完整BattleTurnDriver在UI播放前提交，表现必须读冻结trace而非最终live模型。复用现有伤害/施法辅助，不直接改血量绕过钩子。
3. Buff变更核对Create/GetKind、枚举、双语与图标。箭头规则区分原牌/Step/Token、消费/返回和容器，不能用显示数量代替模型状态。
4. 生命周期看回合开始清盾/效果、回合末效果后临时附魔移除、战斗结束ForceDiscardAll与地图清场；保留牌不应绕过节点清场。新增持久字段同步保存/恢复，否则报告不持久。
5. 敌人规则严格串行，表现尾部可重叠；保持trace播放双索引闸门与死亡屏障，预览不得重抽意图或触发实际效果。owner scope仅同步调用，不跨yield/await；测试可new BattleManager(player)交错，内容/profile/difficulty显式注入。不因调动画速度改规则执行顺序。
6. 美术Scene/Prefab参数与配色不随规则变更重写；C#保存后应用hot reload，新增类型/字段、测试发现及序列化前完整编译。

## 验证选择

一批完成后只挑能暴露该差异的最小检查，优先已有测试：

| 变化 | 优先检查 |
| --- | --- |
| Token/重复/容器 | BattleResolutionTests、ArrowReadSystemTests、RandomArrowModifierTests、ProliferatingArrowModifierTests |
| 同起点道具顺序 | MagicMatchOrderTests + 实际布局顺序 |
| 死亡/清场 | EnemyDeathQueueTests |
| 回合Buff/清牌 | PlayerRoundBuffLifetimeTests、AttributeDisabledBuffTests、ForceDiscardAllTests |
| 内容SO覆盖 | EnemyContentDatabaseTests |

测试中的保留 / 临时牌用RetainedArrowModifier / TemporaryModifier构造，不能只设会被RebuildModifierFlags覆盖的bool；Buff先确认Create实际返回类型，类存在不代表已注册。无场景模型测试注入PlayerStateServices.Simulation与显式difficulty/content/profile，不依赖BattleManager.Instance。内存分支用RuleRunSnapshot，保FullGraph；可移植JSON用GameSessionCheckpoint/RuleJson，不能裸JsonConvert序列化Unity Vector2。

不为低影响可逆变化写镜像测试。运行检查关注一次正常路径和此次边界，如致死/护盾挡完/保留跨回合；UI反馈只在表现受影响时看。遵守当前用户授权的累计校验预算（项目默认60秒，任务显式扩展优先），编译/Editor不可用就交付静态证据及未覆盖项，不重复重跑求“全绿”。

结束后同步对应规则知识/文案，移除临时日志与探针；不顺带改未授权平衡或重构整体架构。
