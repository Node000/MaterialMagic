---
id: kd_db39bc70-975a-413f-972b-f7774cdec50a
injectMode: inherit
injectAgents:
- '*'
aiEditMode: inherit
---

# 奖励、商店、事件与休息

## 奖励

`Assets/Scripts/Simulation/RewardOfferGenerator.cs` 共享池、稀有度、持有降权、箭头和金币生成；`RewardSession.cs`持选择提交，HandSystemUI.Choices/RewardPanelUI只绑定同会话与播receipt。GameSession同样复用这些规则。战胜自动基础金币基数2经进阶修正；“更多金币”另从EconomyConfig/战斗池/精英Boss类型取，不能相加称固定奖励。

RewardPanel提供金币/单道具/单箭头择一或跳过；PendingChoice以nonce、候选ID、数量与资格校验，陈旧/重复请求不提交。解锁/进阶改池与数量。FinishReward委托RunSession.CompleteReward：自动商店保留原节点/Boss归属，离店才完成一次节点；不能仅凭旧入口的早return判断仍会漏结算。

随机道具奖励、商店生成/刷新及传说保底在同品质内按当前MagicBook降权，配置为 `Assets/Resources/Data/EconomyConfig.json` 的ownedMagicWeightMultiplier（默认0.25，1关闭）；不改变品质抽取概率，不按持有份数叠乘，全持有时等概率兜底。卖出恢复权重，存档已有报价不重抽；教程固定奖励与指定ID发放不参与。

## 商店

`Assets/Scripts/Simulation/ShopSession.cs`持报价/选择/买入/撤销/刷新/删箭头与导出恢复，ShopOfferGenerator持生成/定价及可注入输入；ShopPanelUI和GameSession使用同核心，UI只播已提交receipt。卖出道具仍在HandSystemUI路径；`Assets/Scripts/Data/ShopLayer.cs`定义层预算。面板/容器放Scene，内容项用Prefab，不运行时重建整套UI。

当前slotLimit6；道具成本2，箭头1.5，常规为3道具+4箭头，箭头末格强附魔保底。弱附魔分类、概率、池不足回退与排除列表在ShopOfferGenerator；池配置不是唯一过滤条件，UnlockSystem也参与。

- 道具买价以economy.shopSpellPrice为基数，加MagicRaritySystem偏移（Common -2、Rare -1、Epic 0、Legendary +1），再走进阶；当前基数5。
- 基础卖价按当前购买价的一半向下取整；出售弹窗和成交统一走 `ShopOfferGenerator.GetMagicSellPrice(MagicModel, PlayerState)`，经 `MagicModel.ModifySellPrice` 修正后最低为 0。`RunSession` 只在新节点提交时通知 `OnRoomEntered`，读档恢复或战后自动商店不通知。柱状图逐房累积当前卖价，实例值随存档保存，0 是有效值。
- 普通箭头用商品条目price（0也是有效报价），负值回退默认价；附魔箭头用普通基价+Definition.price，夹取后走进阶，忽略附魔商品条目price。
- 修改price不等于上架；IsExcludedShopModifierId仍排除一组语法/特殊箭头，详见arrows模块。
- 刷新价为EconomyConfig基础价+本商店refreshCount。删箭按钮价为进阶修正后的shopRemoveMaterialPrice+PlayerState.PaidShopArrowRemovalCount；仅商店成功且实际收费>0的删箭交易累计一次，免费/事件/内部移除不计数。累计随玩家存档跨店/跨层保留，新局归零；刷新只恢复删箭机会不重置累计。撤销恢复累计、金币、箭头及机会；undo保存实际成交价与计数变更，旧档缺计数按0、旧撤销不减新计数。
- 商店保存从player.CurrentShop导出offers、purchased、undo、removeArrowUsed、refreshCount、layerOfferCounts，恢复不重抽。Hide保留会话，离开节点清空。
- 购买先完整提交、回调只解表现锁；道具飞入用receipt.ResultingMagicCount，不能按已入库Count再加1。删箭头取消不扣款 / 不消耗次数；选定后原子校验并提交。同步观察者抛异常的完整事务恢复尚未实现。

## 奖励棋盘

`Assets/Scripts/Simulation/BonusGridSession.cs`持确定性棋盘、位置/边界/领取状态；BonusGridRewardRules.Apply每step最多提交一次。RewardGridPanelUI逐步提交再播移动/到达/间隔，不预提交全路径；GameSession同用Step/Apply。正式存档仍无棋盘中途DTO，保持节点检查点，已提交收益不因播放取消自动回滚；不要把整局checkpoint重放误称棋盘DTO或任意动画续播。

## 事件与休息

`Assets/Scripts/Data/EventModel.cs`管节点/配方/匹配/重复次数；`Assets/Scripts/Simulation/EventResolutionSession.cs`与EventRuleServices共享效果、回收、后继和分阶段选择，EventRestRules构造休息模型。HandSystemUI.Choices的ResolveNodeChoicesRoutine/PlayEventRuleResolution与GameSession都委托该会话，面板只呈现。EventData在Resources/Data，LevelData.eventPoolId引用numericId。

事件recipe是“1234”字符串（火风水土），不是道具recipe数组；randomRecipeLength在事件创建时确定，ignoreOrder控制无序。匹配用首读取序列，普通选项失败才找isExitOption/defaultEndNodeId。无有效后继通常结束。

GainMagic=4走通用随机道具选择，不读取magicIds，与GainMagicModifier/GainMaterialModifier延后至回收后。GainMagicById=25按magicIds直接给有效内容，回收前执行、不走随机池解锁过滤；满栏不发/空位不足给部分，无替换。其他非延迟效果先执行。多阶段PendingChoice仍属于同一EventResolutionSession，取消/提交后继续共享调度，不提前清上下文。新增效果须查实际switch，保留枚举不代表已实现。

正式保存的全事务guard为HandSystemUI.HasUnpersistedChoiceSession：覆盖战斗基础金币准备、奖励session和整个event session，不只等待选择一帧；普通Save与Force都受限。它是拒绝半事务落盘，不是把PendingChoice写入旧存档。GameSessionCheckpoint可归档初始catalog/profile/config+命令并重放至PendingChoice，需同版本factory；RunMemorySnapshot的FullGraph/EnemyRuntime仅进程内，JSON不保全图，三者不要混用。

休息复用事件面板，默认休息/学习道具/箭头附魔由EventRestRules同源构造；旧resultId兼容，新事件优先effects。文案以 `Assets/Docs/事件本地化文本审阅.md` 为准，维护中英Event及Tooltip一致。

验证：上述EventChoice/RewardChoice/Shop/BonusGrid/GameSession已纳入268/268通过的最终合并EditMode批次；独立宿主真实SO-resolved内容archive smoke通过。未覆盖真机UI手感或任意演出取消恢复，具体结果见实施计划。
