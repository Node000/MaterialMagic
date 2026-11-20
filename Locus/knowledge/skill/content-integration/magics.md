---
id: kd_fa5255bc-ece0-400a-bd59-cb689dadc1ed
injectMode: inherit
injectAgents:
- '*'
aiEditMode: inherit
skillEnabled: true
skillSurface: command
---

# 道具接入附页

常规新道具先读本页，再按需复制 [道具最小模板](templates/magic.template.md) 和 [获得途径模板](templates/magic-sources.template.md)。模板提供当前签名与配置形状，示例ID/类名/数值均未预留；合并现有表，不覆盖整表，不注册测试内容。通常无需为一个新道具读完全部战斗/存档模块。

## 必需接入

1. 确认效果、配方、稀有度、目标与需求中的获得途径；核对 `Assets/Scripts/Data/GameDataTypes.cs` 的MagicData及一个相近道具。MagicData有rarity，无element/power等旧通用效果字段；新ID正数且唯一，不复用已发布ID。
2. 向 `Assets/Resources/Data/MagicData.json` 的items新增numericId/id、script、rarity、nameKey、descriptionKey、iconName、recipe/tagIds、matchRule等。普通recipe为非空MaterialEnum数组；ExactRecipe=0，AnyTwoDifferentElements=1需按特殊规则核对，不能把事件字符串配方抄过来。
3. 在 `Assets/Scripts/Magics/` 建独立 `XXMagicModel.cs`，直接继承MagicModel。当前构造函数形状为 `(MagicData data, int slotIndex = 0) : base(data, slotIndex)`；效果入口为 `protected override void ResolveCast(PlayerState playerState, BattleManager battleManager, MagicCastResult result)`。最小完整类见模板，不向空ScriptedMagicModels.cs集中追加。
4. 在 `Assets/Scripts/Magics/MagicFactory.cs` 的data.script switch加入 `case nameof(XXMagicModel): return new XXMagicModel(data, slotIndex);`。漏case返回空效果MagicModel；必须经工厂检查实际类型。
5. 复用DamageTarget/DamageAll/AddBuff/GainShield等辅助链，维护来源、强化、逐击、锁定目标与MagicCastResult。EffectType和CastParticleTargetsPlayer/AllEnemies是分类/表现声明，不替代规则；数值在ResolveCast，别直接改血量或再次随机目标。复杂规则再读combat/arrows或对应Skill。
6. 名称/描述写 `Assets/Resources/Data/Localization/zh-CN.json` 与 `Assets/Resources/Data/Localization/en-US.json` 的items，字段key/text。新关键词再加 `Assets/Resources/Data/TagData.json` 和两种 `_Tag.json`；空Tag无需新增。
7. 图像在 `Assets/Resources/Images/Magics/` 导入为Sprite，iconName是相对该目录的名称，不含Images/Magics前缀和扩展名；加载器会拼接该前缀。复用现有槽位、粒子及美术配置，不新增代码颜色或每道具一套面板。

## 按需求选择出现途径

| 需求 | 精确文件与字段 | 关键边界 |
| --- | --- | --- |
| 奖励 | `Assets/Resources/Data/RewardPoolData.json` 的magicIds/allowedRarities；`Assets/Resources/Data/LevelData.json` 的rewardPoolId选择池 | 解锁/品质过滤；无品质限制的空池可回退全部，出现一次不能证明目标池正确 |
| 商店 | `Assets/Resources/Data/ShopProductPoolData.json` 的magicIds；`Assets/Resources/Data/EconomyConfig.json` 的shopProductPoolId | 商品池显式列表优先，空时才尝试shopMagicRewardPoolId奖励池，再空回退全库已解锁；价格走稀有度/进阶 |
| 开局 | `Assets/Resources/Data/StartConfig.json` 的initialMagics，元素为slotIndex/magicId | 保留现有道具与合法槽位；不是PlayerStartConfig.json |
| 事件指定赠送 | `Assets/Resources/Data/EventData.json` 的选项effects：rewardType25（GainMagicById）+magicIds | 直接发指定ID，当前回收前执行，满栏不发/容量不足只发一部分，不自动按奖励池解锁过滤 |
| 事件随机奖励 | 同文件effects：rewardType4（GainMagic），不配置magicIds | 打开通用奖励选择，回收后执行，候选由当前奖励池/解锁决定；不是指定赠送 |
| 可选解锁 | `Assets/Resources/Data/UnlockData.json` 的targetType="Magic"、targetId及conditions | 字符串id规则优先；无规则通常开放，直接授予来源另查是否使用过滤 |

只改需求涉及的途径。配置形状和池选择关系见获得途径模板；新事件接入另读 [事件模板](templates/event.template.md)，不凭字面把GainMagic当成读取magicIds的效果。

## 图鉴、保存与刷新

- StartMagicCodexPanelUI.RebuildMagicItemList枚举GameDataDatabase.MagicData.Values、按numericId排序，常规新道具不另登记图鉴列表。发现/NEW由MagicCodexProgressSystem管理，独立于UnlockSystem；PlayerState.SetMagicAtSlot会写发现进度，测试不能污染正式槽位。
- RunSaveSystem的MagicSlotSaveData字段为slotIndex/magicNumericId/modifierId，常规无新持久状态的道具沿现有工厂还原；新增持久状态或改ID才扩展保存验证。
- 外部JSON/图像编辑后先在Unity导入受影响资源。`GameDataDatabase.ClearCache()`同时清数据字典并调用GameDataReader.ClearCache；不代表已创建的MagicModel、图鉴列表或图标缓存自动更新，通常用新运行/重建视图验证。
- 语言有独立TextByKey缓存：清数据缓存后可 `LocalizationSystem.LoadLanguage(LocalizationSystem.CurrentLanguage)`重读文本，但该方法不广播刷新；需重建/刷新目标UI。SetLanguage传当前语言会直接返回，不能当重载。改UnlockData后UnlockSystem也有独立缓存，无公开清缓存入口，宜完整域重载后新运行。
- 新增C#类型完整编译后再运行；不要仅用hot reload声称新增类型/序列化/测试发现就绪。保持用户场景与正式进度。

## 最小验收

一批完成后做一次：JSON/ID/键与目标引用静态检查，完整编译，经工厂确认具体类型，再用目标配方验证效果和需求中的获得来源。新普通道具不默认跑全套胜败、三槽、教程或PC/PE全流程。

仅当本次改变相应部分时增加：匹配顺序用 `Assets/Tests/Editor/MagicMatchOrderTests.cs`；伤害/回合规则按battle-rule-change选测试；UI/布局看目标语言/平台真实输入；新增持久状态做隔离round-trip。遵守当前校验预算，未覆盖部分明确说明，不把模板静态检查当游戏运行通过。
