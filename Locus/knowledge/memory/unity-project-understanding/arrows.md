---
id: kd_56df34c3-9eae-4466-acbd-659238f216e2
injectMode: inherit
injectAgents:
- '*'
aiEditMode: inherit
---

# 箭头、Token 与附魔

## 读取与匹配

`Assets/Scripts/Data/ArrowReadSystem.cs` 把 MaterialModel 列表解析为 ArrowReadSequence。Step 代表一次读取与基础效果，Token 代表配方位置，SourceCard/SourceCardIndex/SourceStepIndex 保留来源；三者不能互相当数量使用。多重箭头可提供多个 Token 而只触发一次基础效果。

BuildSequence 带 PlayerState/BattleManager 上下文时可走局内随机源；预览不要无意推进随机。MaxReadSteps=256、MaxContainerDepth=8 是当前防护值，不代表产品要求只能一层容器。ResolveSequences 含主序列与重读序列；事件只取首序列 Token。

`Assets/Scripts/Data/MagicMatchOrderUtility.cs` 按起点，再按显式ranks / 玩家MagicReadingOrder / 槽位顺序施法；`Assets/Scripts/MagicBookVisualOrder.cs`仅在表现边界把PC/PE实际布局转ranks。策略随槽位持久保存，重排道具不搬走槽位rank；正式结算与预览共用CaptureMagicReadingOrder。不要恢复按配方长度排序。

## 附魔数据和创建

- `Assets/Scripts/Data/MaterialModifierDatabase.cs` 从 `Resources/EnchantConfig/MaterialModifiers` 读取 MaterialModifierDefinition；JSON 旧表不是现行入口。
- Definition 保存 id、script、nameKey、descriptionKey、lineColor、inArrowModifierRewardPool、price、visualMaterial。id 用于存档/池；script 是短类名。
- `Assets/Scripts/Modifiers/MaterialModifierFactory.cs` 在 MaterialModifierModel 所在程序集发现非抽象子类，以 Activator 无参构造；无需 switch。不存在的 script 返回 null。heavy_arrow 兼容映射 repeat_arrow。
- 具体行为在独立 Modifier 类，MaterialModelExtensions 提供组合查询；不要只改 UI 或仅写 Definition。
- 道具强化使用 MagicModifierFactory 的 id switch，不能照搬本页反射流程。

## 生命周期与组合

MaterialModifierModel 有牌堆/回合/读取钩子；改行为前查看本类与调用链。临时牌、回合临时附魔、战斗临时附魔和永久牌库状态分开处理。回合末先完成相关效果，再移除 turn-only；战斗结束及地图边界强制回收，保留不跨越节点清场。

容器/黏合、半箭头、随机箭头、句点/返回/打包、重复读取各有专门逻辑。应复用来源追踪和 AfterReadAction，不用“删除 Token”替代对原牌的消费或下回合返回。增殖等派生牌要检查进入哪一牌堆、是否临时、是否继承附魔及递归上限。

## 显示、价格与获得途径

- 单效果由 `Assets/Scripts/MaterialModifierVisualUtility.cs` 驱动；多效果 RT 链在 `Assets/Scripts/PresentationData/MaterialModifierRTChain.cs`，需释放实例材质/RT、恢复原图像状态。
- 材质在 `Assets/Resources/Materials/MaterialModifiers/`；Definition.visualMaterial 是绑定入口。EnchantIconUI 使用同类材质，两层颜色来自 `Assets/Resources/Config/Enchant_Color.asset`；不要代码填默认配色。
- `price` 是相对普通箭头的差值；商店最终经进阶计算，商品池和排除列表仍可阻止上架。inArrowModifierRewardPool 只控制对应奖励池，不自动进入商店/事件/开局。
- 商店 IsExcludedShopModifierId 排除 omni_arrow、return_arrow、period_arrow、pack_arrow、linked_arrow、random_arrow、eternal_arrow、repeat_arrow；只加 ShopProductPoolData 不足以上架。
- 中英 Modifier 表提供名称/说明；Tag、Icon、材料视觉是独立接入项。

现有 ArrowReadSystem、MagicMatchOrder、RandomArrowModifier、ProliferatingArrowModifier 测试可按影响选用。接入步骤见 `Locus/knowledge/skill/content-integration/enchantments.md`。
