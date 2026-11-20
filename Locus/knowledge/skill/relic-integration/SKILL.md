---
id: kd_8c7348ef-7ff6-42eb-8a16-ee8b416e5f11
injectMode: inherit
injectAgents:
- '*'
aiEditMode: inherit
name: materialmagic-relic-integration
description: 新增或修改遗物逻辑、计数、Buff联动、伤害策略、候选领取和遗物存档时使用；纯规则层开发与正式测试工作流。
summary: 遗物逻辑接入、状态与来源规则、候选幂等、检查点保存和测试要求。
skillEnabled: true
skillSurface: command
---

# 遗物逻辑开发与验证

适用：新增/修改遗物、遗物计数、Buff 双向联动、伤害策略、候选领取或遗物状态保存。当前实现入口为 `Assets/Scripts/Relics/`，完整说明在 `Assets/Docs/遗物系统逻辑实现说明.md`。这是纯逻辑层工作流，不默认授权修改场景、平衡投放或二层流程。

## 1. 定位与范围

1. 读取目标遗物、RelicModel、RelicInventory 和触发事实的真实模型提交点；涉及 Buff/伤害再按需读 battle-rule-change Skill。
2. 先明确计数的是实体箭头、实际读取 Step、重读、Token、施法、命中或真实损血。拖拽布置/撤回/BuildSequence 不等于使用，不在 UI Refresh 中计数。
3. 确认是纯规则变更还是投放/UI。遗物库存已存在不等于现有精英奖励、GainRelic 和商店商品已接入；未授权不自动投放。

## 2. 新增内容

1. 在 `Assets/Resources/Data/RelicData.json` 增加稳定 numericId/id、script、数值/阈值/重复补偿/价格；不重排既有 ID。示例池 relic_test 不是正式投放池。
2. 每个效果独立 `Assets/Scripts/Relics/XXRelicModel.cs`，继承 RelicModel，不继承 BuffModel，不写 PlayerState.Buffs 字典。
3. 在 RelicFactory 显式注册 script。未知 script 必须拒绝发放，不返回空效果基类；读档未知项保留为 UnavailableRelicModel。
4. OnEvent 按 RelicEventKind 严格筛选 Source/Target、Amount、IsReplay 和 Origin；没有合法目标时明确消耗与取消策略。
5. 具名整数状态通过 Get/Set 保存；Run 计数不在回合/战斗重置，Turn/Battle 状态分别重写 ResetTurnState/ResetBattleState；需要新类型状态时显式扩展版本化 DTO，不用 CLR 类型名反序列化。

## 3. 联动规则

- 用 RelicContext.GiveBuff/Damage/GainShield/Draw/Heal/GainGold 产生派生效果，不直接写生命/护盾/牌堆，不调用 UI/动画/协程。门面在回调内排队，不能把 context 留到下一帧。
- GiveBuff 保留玩家来源，才能吃 OnGiveBuff/OnReceiveBuff/AfterGiveBuff。不要设 null 来规避攻击增伤。
- Damage 默认 EffectDamage：跳过进攻方攻击链，但经过目标易损/护盾/受击；Attack 显式走攻击链；HealthLoss 才跳护盾。统计读事实 Amount/ShieldDamage，不读 AfterAttack 的可变表现返回值。
- ModifyAttack/ModifyMagicAttack/ModifyDamageTaken/ModifyShieldGain/ModifyGoldGain/ModifyBuffGiven/Received 必须纯查询：不写计数、不消耗、不抽随机、不发命令。一次性资格在正式事件提交。
- 遗物按获得顺序、事实快照分发；路径防环阻断同一实例同一钩子的链式重入。不要移除防环来“修复”触发不足，先区分独立命中与同一路径。
- 旧 Buff 同步副作用保留。层数观测是钩子净变化；同一钩子内无通知的先加后减不保证中间事件。清场/恢复不触发施加收益，不把消失一律算消耗。

## 4. 候选与存档

- `RelicOfferService.Create` 写入 PendingRelicOffer；未领取同池请求返回原候选，不重掷/RNG。TryClaim 以权威 pending ID/价格为准，重复或过时副本不可再次领取。
- 服务不负责文件 IO。渠道编排器应在逻辑完成后，把库存/金币/pending领取状态与节点阶段一起提交安全快照；不要靠 UI 动画结束才实际发放。
- 库存 Export 要求 IsSettled 且非 faulted；异常或派生预算超限要回到最近检查点，不能保存半个操作。
- RunSaveSystem v4 保存实例与 pending；v3 无遗物为空。恢复不触发 OnAcquired、不发金币、不推进随机。
- 相关 Buff 若有私有字段，同步扩展 ExportRuntimeState/RestoreRuntimeState，确保 PlayerCombatSaveData.runtimeBuffs 恢复一致；不调用 AddBuff 恢复，不只存 stack。
- 当前是节点检查点恢复，不声称任意动画中途完整恢复；未知内容/stateVersion 保留占位，勿自动补偿或删数据。

## 5. 验证

一次改动集中完成后完整编译，再运行 `RelicSystemTests`，按触点追加 PlayerRoundBuffLifetimeTests、EnemyDeathQueueTests、ArrowReadSystemTests、AttributeDisabledBuffTests、ForceDiscardAllTests、MagicMatchOrderTests，不默认跑不相关地图测试。

至少覆盖：正常触发、未成功操作不计数、来源 Buff 链、护盾挡完与真实损血、目标死亡、重读/重放、顺序与防环、重复预览无副作用、状态往返、候选读档不重掷、重复/过时领取、退款不加成。

使用正式 Unity Test 工具（需 test-framework >=1.4.0；本项目已批准升级 1.4.6）。工具不可用时先报告开关/版本/桥接错误，不手动反射调用测试假装通过，不任意升级依赖；在用户许可后修前置条件。测试与重编译持有最小 Editor 状态锁，其他代码/文档工作在锁外。

## 6. 交付与知识维护

- 更新 `memory/unity-project-understanding/relics.md` 的稳定事实、实现说明和缺口；类名/数值以代码/JSON为准，不重复维护大表。
- 设计稿是未来契约，实际实现说明是当前边界，二者不得混淆。
- 不修改现有 Scene/Prefab。表现层若另获授权，只在用户指定独立 Relic-Test 场景搭建，容器在 Scene、内容项 Prefab、Inspector 绑定、颜色来源美术配置。
