---
id: kd_8686586b-1c22-417a-ab58-f0b505da9ae1
injectMode: inherit
injectAgents:
- '*'
aiEditMode: inherit
skillEnabled: true
skillSurface: command
---

# 箭头附魔接入附页

1. 读 `Assets/Scripts/Modifiers/MaterialModifierModel.cs` 与相近Modifier，确定读取Token、基础效果、消费/回收、牌堆和生命周期的具体钩子；必要时读ArrowReadSystem。附魔不等于道具强化。
2. 独立非抽象XXModifier、无参构造，与MaterialModifierModel同程序集。MaterialModifierFactory按短类名反射发现，无需switch；Definition.script拼写错误会返回null。不要因当前无asmdef而擅自新建程序集。
3. 用Unity API创建/编辑 `Assets/Resources/EnchantConfig/MaterialModifiers/<id>.asset`，填id/script/nameKey/descriptionKey/inArrowModifierRewardPool/price/visualMaterial等。保持已有GUID/美术参数，旧JSON不是读取源。
4. 行为中的turn-only/battle-only/permanent与临时牌分别处理，重复附魔策略按同类现有实现；新消费或生成效果保留SourceCard映射、容器边界、防递归与随机源。不能只在UI复制牌来实现模型效果。
5. 中英Modifier表补key/text。材质按现有MaterialModifiers家族并绑定Definition；视觉需兼容UGUI遮罩、图标两层、方向/随机参数与多附魔RT链的释放。颜色来源为SO/Prefab/Enchant_Color，勿硬编码。
6. 获得途径分别接奖励池标记、商品池、事件、开局或解锁。price是价格差值，改价不自动上架；先查ShopPanelUI.IsExcludedShopModifierId，移除排除需玩法任务明确要求。维护Enchant_Color时只补目标缺项，勿重播种已有色。
7. 集中检查Definition到script/材质/文本/池，再在受影响规则中验证一次。读取变化按需选ArrowReadSystem、RandomArrowModifier、ProliferatingArrowModifier；回合生命周期看清理后状态，视觉变化另看目标卡和图标。60秒预算内未能运行部分明确列出。

删除附魔先查牌库/存档modifierIds、事件modifierId、开局、池、解锁、视觉与名称映射；旧heavy_arrow兼容映射repeat_arrow须保留或有迁移，不能按零代码引用直接删数据。
