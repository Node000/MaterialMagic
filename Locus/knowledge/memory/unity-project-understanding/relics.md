---
id: kd_c0c54603-83e2-4e9d-ba75-85633b32e1c9
injectMode: inherit
injectAgents:
- '*'
aiEditMode: inherit
---

# 遗物逻辑系统

实现说明：`Assets/Docs/遗物系统逻辑实现说明.md`；执行步骤：`skill/relic-integration/SKILL.md`。早期完整设计在 `Assets/Docs/遗物系统代码设计.md`，不是全部已实现的接口清单。

## 状态与入口

已实现纯逻辑层，在 `Assets/Scripts/Relics/`：PlayerState.Relics 独立持有 RelicInventory，PlayerStatus 继承获得，不混入 Buff 容器。通过 TryAcquireRelic/RelicInventory.TryAcquire 发放；数据来自 RelicData.json/RelicPoolData.json，工厂按 script 显式注册，未知脚本拒绝新发放。`relic_test` 保留 9 个结构示例；新叶/火种/源泉/结核（ID 10–13）在独立 `relic_temporary_arrows_test` 池。两池均未接正常奖励/商店/事件投放。

用户本轮明确只做逻辑和测试；无遗物 UI/新场景，不改其他场景。二层开放仍是未来产品方向，不在这次实现门控。后续表现若获授权限定独立 Relic-Test 场景。

## 联动契约

- RelicModel.OnEvent 接已提交事实，Modify* 纯查询。RelicContext 门面排队产生实际模型效果；遗物自身无 UI/UnityEngine/Tween 依赖，但旧 PlayerState/EnemyModel 内原有音效未全量纯化。
- GiveBuff 保留玩家来源，复用 OnGiveBuff/OnReceiveBuff/AfterGiveBuff。BuffApplied/净层数变化可反馈给遗物，旧同步反馈保留；批量清场/恢复不广播施加收益。净变化记录不能保证钩子内每次裸写中间值。
- EffectDamage 默认不走进攻 OnAttack/AfterAttack，走目标易损/护盾/受击；Attack 显式进攻链；HealthLoss 才跳护盾。实际损血事实不受 AfterAttack 改表现返回值影响，死亡进既有队列。
- 快照按获得顺序分发，遗物派生效果 FIFO；同实例同钩子路径防环。工作预算/回调异常使库存 faulted，禁止导出半结果。
- ArrowStepRuleResolver 从 UI 移出基础效果规则，实际读取后才发 ArrowRead。BuildSequence/放入/撤回不计使用；重读与实体消耗分开；换牌补充不算普通抽牌。
- 开局护盾在首回合清盾后 TurnPreparing 发放。方向遗物在每场第 1、3、5……个玩家回合的 TurnStarted（抽牌后）赠送临时箭头；只触发入手，不算抽牌，满手跳过不顺延。首次施法在 MagicModel 的实际重放循环内部计数，不借 UI 连击数。

## 获得、保存与边界

RelicOfferService.Create 固定候选并写 PendingRelicOffer，未领取同池不重掷；TryClaim 验权威 pending ID/价格，重复/过时副本不能再次领取。服务没有 UI/IO，实际渠道接入时需库存、金币、领取阶段一同安全保存。GoldBonus 只修正常 Reward，不修 Refund/Compensation/Spend；金币动画已改一次规则提交，飞行金币只推进显示。

RunSaveSystem v4 保存遗物状态和 pending，v3 缺字段为空；未知 ID/script/状态版本保留为禁用占位。恢复不重播 Acquired、不补偿、不推进 RNG。玩家战斗快照 runtimeBuffs 保存 Buff 私有计数/待下回合标记，通过工厂直接恢复不经 AddBuff。仍按现有节点检查点频率，不承诺任意动画中途/所有旧临时状态完整恢复。正式渠道 UI 阶段、GainRelic case、既有商店商品、二层流程尚未接入。

测试 `Assets/Tests/Editor/RelicSystemTests.cs` 与相关 EditMode 回归已运行通过。测试框架经用户批准从1.1.33升为1.4.6，NUnit依赖2.0.3；升级热切换曾出现类型加载错误，Editor重启后官方测试恢复。
