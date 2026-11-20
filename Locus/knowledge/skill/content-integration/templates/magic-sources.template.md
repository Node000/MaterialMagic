---
id: kd_35829852-bdd2-4c87-97a2-46fe900f15ca
injectMode: inherit
injectAgents:
- '*'
aiEditMode: auto
skillEnabled: false
---

# 道具获得途径与解锁模板

只在需要接奖励、商店、开局、事件或解锁时读取。上级为 [道具接入](../magics.md)，道具定义/脚本见 [最小模板](magic.template.md)。示例道具900001、池900101及字符串example均未预留；统一替换为现场确认的ID。下列JSON是**局部条目或字段片段**，不能覆盖整表/已有数组。

## 奖励

修改 `Assets/Resources/Data/RewardPoolData.json` 的目标items条目，向magicIds追加道具ID；如新建池，形状例如：

```json
{
  "numericId": 900101,
  "id": "reward_pool_example",
  "magicIds": [900001],
  "allowedRarities": [0]
}
```

在 `Assets/Resources/Data/LevelData.json` 的目标关卡将rewardPoolId指向该池，或复用已被目标关卡引用的现有池。allowedRarities为空不限制品质；非空则新道具rarity必须匹配。当前HandSystemUI.BuildRewardMagicPool还过滤UnlockSystem；池为空且没有品质限制时可能回退全部已解锁道具，不能用“偶然出现”证明目标池配置正确。数量不足的单条示例只说明结构，正式池按需要提供足够候选；最终概率看 `Assets/Scripts/Data/MagicRaritySystem.cs` 与对应配置，不在模板写死。

## 商店

修改 `Assets/Resources/Data/ShopProductPoolData.json` 的目标条目magicIds；经济配置 `Assets/Resources/Data/EconomyConfig.json` 的shopProductPoolId选择该商品池。

```json
{ "magicIds": [900001] }
```

这只是要合并的字段，保留strongMaterialOffers/normalMaterialOffers/weakMaterialOffers等原字段。当前ShopPanelUI.BuildMagicPool优先商品池magicIds，空时尝试经济配置shopMagicRewardPoolId对应奖励池，结果仍为空时回退全部已解锁道具；最终过滤解锁。不要只改回退池就认为显式商品池已接入。道具报价按现行稀有度/进阶链，不在MagicData新加price字段。

## 开局

修改 `Assets/Resources/Data/StartConfig.json` 中需要的配置，在initialMagics中追加空槽位的记录，不替换已有配置其他字段：

```json
{ "slotIndex": 0, "magicId": 900001 }
```

slotIndex为示例，必须选择当前布局和进阶容量允许的槽位；注意已有初始道具避免覆盖。实际读取入口GameDataDatabase.PlayerStartConfigData指向StartConfig文件，不是PlayerStartConfig.json。

## 事件

指定赠送道具使用 `Assets/Resources/Data/EventData.json` 的某选项effects：

```json
{ "rewardType": 25, "magicIds": [900001] }
```

rewardType25是GainMagicById，读取magicIds直接发放；rewardType4是GainMagic，只打开奖励选择，**不读取magicIds**。两者时序、满栏和过滤不同，参见 [事件模板](event.template.md)。新事件还须由LevelData.eventPoolId引用事件numericId，再将该关卡接目标章节池；既有可达事件无需新增关卡。指定发放不保证有空位，也不自动套用随机池解锁过滤。

## 可选解锁

没有对应规则时通常默认开放，需要解锁限制才编辑 `Assets/Resources/Data/UnlockData.json`。以下例子表示普通非教程胜利后解锁；不是每个新道具都要添加：

```json
{
  "id": "unlock_magic_example_bolt",
  "targetType": "Magic",
  "targetId": "magic_example_bolt",
  "conditions": [
    { "type": "NonTutorialVictoryCount", "value": 1 }
  ],
  "messageKey": "unlock.magic.example_bolt"
}
```

targetId优先用道具字符串id，与UnlockSystem.IsMagicUnlocked契约一致；无字符串规则时才回退numericId字符串。多条conditions为全部满足。messageKey补中英主表key/text：

```json
{ "key": "unlock.magic.example_bolt", "text": "已解锁示例道具。" }
```

```json
{ "key": "unlock.magic.example_bolt", "text": "Example Bolt unlocked." }
```

规则的检查/授予/队列通过正常结算链，不把Debug授予当正式条件通过；如果要求事件/开局也服从锁定，要读对应直接授予路径，不能只依赖奖励/商店过滤。

## 图鉴与保存

`Assets/Scripts/StartMagicCodexPanelUI.cs` 的RebuildMagicItemList直接枚举GameDataDatabase.MagicData.Values、按numericId排序；一般新道具无需单独登记图鉴列表。已构建面板缓存内容，刷新数据库后还需重建面板或新会话。

发现/NEW标记来自 `Assets/Scripts/MagicCodexProgressSystem.cs`，与UnlockSystem不是同一状态。PlayerState.SetMagicAtSlot会MarkMagicDiscovered；验证中向正常玩家入槽可能写图鉴进度，宜隔离/备份，不能称其纯内存检查。

RunSaveSystem的MagicSlotSaveData用slotIndex/magicNumericId/modifierId重建，常规无状态道具沿现有Factory路径即可。新增跨节点持久状态或改ID时才扩大存档验证，不为每次新道具默认跑全局胜败/三槽测试。
