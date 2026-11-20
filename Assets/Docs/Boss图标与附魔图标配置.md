# Boss 与附魔图标配置

Boss在RunManager.ResolveChapterBoss确定关卡与敌人组，缓存chapterBossLevelId/chapterBossGroupIndex随局保存；地图和战斗应复用同一结果。LevelData.randomEnemyGroups[].mapIconPath优先，其次关卡mapIconPath，空值回退Resources/Images/UI/Boss。路径为Resources相对路径，不含扩展名。多Boss格是否独立抽组见当前缓存逻辑和KNOWN_GAPS，不能仅凭格数推断。

附魔图标为 `Assets/Prefabs/UI/EnchantIcon.prefab`，EnchantIconUI序列化绑定底色/上层两张Image；颜色来源为 `Assets/Resources/Config/Enchant_Color.asset`，材质来源为对应Definition.visualMaterial。displayName只供Inspector识别，运行使用modifierId。

补新附魔时只补缺项和名称，不重播种已有美术颜色。相关工具在 `Assets/Editor/EnchantColorConfigEditor.cs`；菜单“刷新/补齐”是写入，不等同只读校验。选项、奖励和详情应复用该图标；无配置保留Prefab本身表现，不代码生成默认色。

统一详情由UnifiedDetailContentBuilder填内容，UnifiedDetailPopupUI显示，附加框 `Assets/Prefabs/UI/AddedDetailed.prefab` / AddedDetailedUI容纳附魔或道具强化图标。尺寸、位置、字号在实际Prefab/Scene读取，不沿用历史实测坐标。

EnchantIcon预制体的嵌套Canvas与材质实例释放涉及上下文和层级；改动需看现有配置及目标卡/选项/详情一致性，不随手删Canvas或开启overrideSorting。随机/半箭头的图标参数与实际卡牌方向不一定相同，需按需求检查动态参数。

配置价格另见 `Assets/Docs/箭头附魔价格配置.md`；视觉方法见scene-ui-integration Skill。本轮未重新验证旧图标数量、逐场景绑定或颜色截图。
