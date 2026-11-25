---
id: kd_a8d027f6-c128-49c9-a53b-0e77d3a78cd2
injectMode: inherit
injectAgents:
- '*'
aiEditMode: inherit
---

# 战斗规则与表现

## 入口与对象

`Assets/Scripts/Data/BattleManager.cs` 管战斗阶段、敌人、目标、回合规则和死亡队列；PlayerState持玩家牌堆、Buff、金币等，EnemyModel持敌人状态、意图及死亡。PlayerModel包装PlayerStatus/PlayerState，UnitModel提供统一受击接口，CombatantModel在Buff上下文桥接双方。不要因名称相似再建新模型层。

`Assets/Scripts/HandSystemUI.cs`仍持表现与宿主流程，partial `Assets/Scripts/HandSystemUI.RulePresentation.cs`先调用同步BattleTurnDriver提交完整回合，再消费冻结trace；StaggeredPresentationRunner只处理演出。BattlePlayZoneResolutionSession持读取/施法调度，BattleDeathResolution持死亡屏障。HP/Buff/意图/牌显示与逐击cue从snapshot读取，模型引用只标识身份，不能回读最终值驱动中间帧。

## 施法与伤害

MagicModel.Cast每次施法：BeginCastTarget → 遗物MagicStarted → 强化BeforeCast → 玩家Invoke → ResolveCast → 强化AfterCast → 敌方电弧 → 遗物MagicResolved → EndCastTarget。重复施法逐次执行；粒子和规则要复用已锁定目标，不能各随机一次。

主动道具伤害通过MagicModel.Damage/Target/All/Times辅助函数：强化BeforeAttack、玩家OnAttack（含遗物纯攻击修正）、遗物道具攻击修正、敌方TakeDamageResult、攻击后链。TakeDamageResult自身负责受击Buff、护盾/生命、后置受击与死亡；AfterAttack不反向更改已扣血，只影响结果或显式后续效果。直接伤害与无来源伤害不要冒充主动攻击。

- 电弧：Magic.ResolveCast之后对存活敌人结算；`ArcBuffModel.ResolveAfterMagic`走TakeDamageIgnoringVulnerableResult，无攻击者、跳过易损增伤与易损受击衰减，保留护盾和其他受击链，回合结束清零。
- 虚弱：OnAttack减值，实际AfterAttack减半，回合结束也减半；预览不得消耗。
- 易损：OnTakeDamage加层数，FinalDamage>0时AfterTakeDamage减半，无普通回合衰减。
- 自然燃烧无视护盾和易损，造成层数伤害后减半。玩家仍在玩家回合开始结算；敌人在`BeginEnemyTurn`通过独立钩子统一结算。driver先记录开场伤害并排空死亡/亡语，再执行存活敌人意图；单个敌人的其他回合开始钩子仍在其行动前。
- 迟缓：OnGainShield减值，实际GainShield中的ApplyGainShieldModifiers管理消耗，预览不消耗；回合末减半。
- Buff施加有来源OnGiveBuff、目标OnReceiveBuff、实际加入、来源AfterGiveBuff。新规则不要丢source，否则放大/反馈链失效。

## 回合、死亡与随机

BattleManager.BeginPlayerTurnStartRules/EndPlayerTurnRules、BeginEnemyAction/ResolveEnemyIntentHitAt/EndEnemyAction是规则入口。意图多段逐击调用，显示预览用EnemyModel.GetIntentAttackValue/GetIntentAttackTotalValue，不推进意图或随机状态。

死亡按EnemyDeathEntry排队、去重；BattleDeathResolution按DeathPending→唯一ResolveEnemyDeath→DeathResolved推进，UI只播放爆碎 / 反馈。HandleDeathEffect、召唤/清场和胜利判定需核对。KillAsBattleCleanup是纯清场路径，不重新触发战斗死亡收益。

敌人行动规则在driver中严格串行且整行动组后排死亡；UI双索引控制已提交trace的播放顺序，尾部演出可重叠。EndTurn提交后禁止中途换焦点。旋涡新牌/召唤在首次相应snapshot创建view，退场由AfterRead/TurnEnd演出，不用RebuildCards提前回收。

正常收益随机经RunManager/PlayerStatus/RunRandom seed+step保存；死亡碎片等纯表现用Unity.Random。已证缺口：EnemyFactory在enemy.Battle绑定前初始化首意图，敌人19/22/24有效SO首loop随机，实际走全局Random；鹿男显示getter会抽玩法元素且缓存未持久。当前未修，意图预览保持只读仍是应达契约，详见防SL审查。

## 牌堆与退出

PlayerState当前有Deck、DrawPile、DiscardPile、ConsumedPile、Hand、PlayZone；旧“没有弃牌堆”说法失效。通常弃牌入DiscardPile，抽牌不够再洗回；消耗与临时牌有独立去向。

战斗确认时不弃手牌；读箭头、施放、读后处理完成后，`EndPlayerTurnRules`清理两区，UI由同一个`PlayerTurnEnded`的前后差量播放退场。保留箭头不弃，羽翼获得的KeepHand在当轮清理时消费且只保护手牌；假钻石是施放时主动消耗，不等回合末。雨幡每次施放直接发3张临时下箭头，并叠加同名Buff。下一回合正常抽牌后每层再发2张，随后清除层数。两批都受手牌上限约束，发放不触发抽牌钩子。战斗/节点清场仍可强制ForceDiscardAll/ClearCombatState。

## 遗物逻辑接入

PlayerState.Relics 持有独立库存，效果经同一模型入口提交；RelicInventory分发事实/派生队列，不依赖UI。ArrowStepRuleResolver负责已从HandSystemUI移出的箭头基础规则并发布实际读取。伤害策略/来源/防环/保存边界见 `memory/unity-project-understanding/relics.md`，执行方法见 `skill/relic-integration/SKILL.md`。PlayerStateServices现承接玩家及已绑定敌人的伤害 / 金币声音、伤害统计、道具发现，默认UnityHost，测试可注入Simulation。兼容静态facade仍存在，但同步public入口按所属Rules.Enter绑定Battle/RNG/内容/难度/profile，退出恢复caller；不要跨yield/await持scope。Simulation隔离声音/统计/图鉴与真实IO；目录Core/Adapters/Presentation和Standalone边界见实施计划。

## 改动入口

Buff新增：GameDataTypes.BuffEnum、BuffModel.Create/GetKind、独立Buff类、LocalizationKeys映射、BuffIconData和中英Buff表。局内效果显示由BuffSlotView、EnemyIntentView、HealthBarUI等刷新。

批量改动后按影响挑选BattleResolution、EnemyDeathQueue、PlayerRoundBuffLifetime、AttributeDisabledBuff等测试，不默认全套。完整程序集编译及268项规则测试已过；PC/PE隔离真实出牌/出手、亡语、冰镐逐击和旋涡尾卡view已补验，非全面视觉/真机。DebugStartBattleLevel不切RunManager.State，CanUseBattleCardInput须认可debugBattleActive（已修）。证据见 `Assets/Docs/逻辑表现分离运行验收.md`，脚本入口见`Assets/Docs/脚本规则入口与首批验收.md`。Thorns工厂漏case仍未修。方法见 `Locus/knowledge/skill/battle-rule-change/SKILL.md`。
