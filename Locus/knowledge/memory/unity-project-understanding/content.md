---
id: kd_2d1fe848-8277-4e21-bb6a-22d9d7d2c03f
injectMode: inherit
injectAgents:
- '*'
aiEditMode: inherit
---

# 内容数据与扩展入口

依据当前类型和读取器；新增步骤见 `Locus/knowledge/skill/content-integration/SKILL.md` 及附页。

## 数据加载

`Assets/Scripts/Data/GameDataReader.cs` 用 Resources TextAsset、JsonUtility 和 items 容器加载；GameDataDatabase 缓存数字/字符串字典。修改文本后先导入受影响非脚本资源；GameDataDatabase.ClearCache清数据字典并调用GameDataReader.ClearCache。已有模型/视图/图鉴列表和MagicItemView图标缓存不随之重建，宜新运行或重建目标视图。语言TextByKey独立，LoadLanguage(CurrentLanguage)可重读但不通知UI；SetLanguage传相同语言会早退。UnlockSystem也有独立缓存，改规则宜域重载。C#修改按编译路径应用，不以旧缓存判断配置无效。

开局 `GameDataDatabase.PlayerStartConfigData` 指向 StartConfig 而不是 PlayerStartConfig 文件；语言表例外使用 items 内 key/text。numericId<=0 的数字内容会被忽略；ID是存档、池与关卡关联，不重用已发行ID。

## 道具

- `Assets/Resources/Data/MagicData.json` 当前54项。MagicData 有 numericId、id、rarity、nameKey、descriptionKey、script、iconName、tagIds、recipe、matchRule、playPlayerCastAnimation；**无 element、power、buffAmount 等旧通用效果字段**。
- `Assets/Scripts/Magics/MagicFactory.cs` 按 data.script 显式 case；具体类直接继承 `Assets/Scripts/Data/MagicModel.cs`，重写 ResolveCast。默认分支是空效果 MagicModel，不一定报错。
- `MagicModel.GetCombatTrigger` 给出本次有效规则、配方和效果来源；空普通配方与 `NoTrigger` 均不匹配，`AnyTwoDifferentElements` 仍是独立规则。蓝图用 `GetIndex`、`GetRightNeighborIndex`、`GetItemByIndex` 查紧邻右槽；PC 主场景步长 1，PE 取布局行数，空槽不跳过。预览和结算共用触发描述；复制有效要求和基础效果，不复制强化，施放身份仍为蓝图。
- 名称/描述主表 `Assets/Resources/Data/Localization/zh-CN.json` 与 `Assets/Resources/Data/Localization/en-US.json`，使用items内key/text；关键词TagData + 两种Tag补表。图标位于 `Assets/Resources/Images/Magics/` 并导入为Sprite，iconName不带Images/Magics前缀或扩展名，加载器拼接该前缀。
- 按需求接 `Assets/Resources/Data/RewardPoolData.json` 的magicIds/allowedRarities、`Assets/Resources/Data/ShopProductPoolData.json` 的magicIds、`Assets/Resources/Data/StartConfig.json` 的initialMagics（slotIndex/magicId）。事件指定发放使用rewardType25/GainMagicById的magicIds，随机奖励是rewardType4/GainMagic，不读magicIds。奖励/商店有解锁与回退，直接发放不要假设套同样过滤；具体模板见 `Locus/knowledge/skill/content-integration/templates/magic-sources.template.md`。
- `Assets/Scripts/MagicModifiers/MagicModifierFactory.cs` 按强化字符串 id 显式映射，未知为 GenericMagicModifierModel。MagicModel.AddModifier 清旧列表，当前同一道具不同时叠多种独立强化。

## 敌人

- `GameDataDatabase.LoadEnemyData` 先读 Data/Enemies JSON，再加载 `Assets/Resources/Config/GameContentDatabase.asset`，SO同numericId覆盖；当前数据库27个引用。
- `Assets/Scripts/Data/EnemyDefinition.cs` 用JsonUtility往返深拷贝，运行难度变更不写回Definition。
- 日常编辑已有 SO；新增 Definition 必须加入数据库列表，否则不会被覆盖层读取。无 SO 的新 JSON 仍可走基线回退。
- `Assets/Scripts/Enemies/EnemyFactory.cs` 按numericId显式创建子类，再ApplyRuntimeDefinition；未知ID可用通用EnemyModel。不要仅new子类绕过运行难度定义。
- 意图字段优先理解 intentGroups + intentLoop，phases 定义分阶段；intentPool/actionLoop 为兼容入口，不能把新功能只写到旧字段说明。
- intentLoop 的 groupId/randomGroupIds/onlyOnce 驱动选择；多段times/hitInterval逐击；Special调用具体类ProcessSpecialIntent，预览与Tooltip有可重写入口。
- 关卡的 enemies 或 randomEnemyGroups 引用 enemyId与坐标；Chapter池关联LevelData而非直接把敌人ID当关卡ID。
- `Assets/Editor/EnemyContentImportValidator.cs` 菜单 Validate ScriptableObjects为内容检查；Import JSON to ScriptableObjects会对现有Definition SetData、重建数据库列表并保存，不能日常无差别执行。

## 事件

`Assets/Resources/Data/EventData.json` 的事件由nodes/options/effects组成；LevelData.eventPoolId绑定事件numericId。配方是字符串，随机配方在事件初始化生成，事件Token只用首读取序列。效果实际分支在Simulation/EventResolutionSession，HandSystemUI.Choices只呈现；GainMagic=4打开奖励选择且不读magicIds，与GainMagicModifier/GainMaterialModifier同为回收后延迟。GainMagicById=25读取magicIds直接发指定道具，当前不在延迟名单、回收前执行；满栏不发/容量不足只发一部分，不经过随机奖励池解锁过滤，无满栏替换。不要沿用枚举旧注释。GainRelic等保留枚举没有实现case。

## 相关入口

`Assets/Scripts/Data/GameDataTypes.cs` 是内容DTO/枚举定义，RunSaveData.cs持保存DTO。每世界RuleContentSnapshot经RuleJson深拷贝（Unity Vector2只写x/y），RuleContentArchive可导出已应用SO覆盖的纯值内容；显式provider也detach，不因全局数据库缓存共享可变record。涉及字段先读源码，不能按旧README猜结构。内容表、工厂、获得池、多语言和保存ID一起看；只修改本任务需要的部分。结构化SO必须通过Unity API写入，离线时只准备合法源文件或说明，不能raw YAML改资产。
