---
id: kd_0622b080-06fd-438a-bfa9-0c6f59ff6c18
injectMode: inherit
injectAgents:
- '*'
aiEditMode: inherit
skillEnabled: true
skillSurface: command
---

# 事件接入附页

需要新增事件或配置发放道具时，读取 [事件最小模板](templates/event.template.md)，包含完整nodes/options、中英文本、关卡绑定及两类道具奖励的准确区分。模板示例未注册，合并目标条目且替换ID，不覆盖整张表。

1. 读 `Assets/Scripts/Data/GameDataTypes.cs` 的EventData/EventNodeData/EventOptionData/EventEffectData及EventModel，选一个相近事件。`Assets/Resources/Data/EventData.json` 顶层items；`Assets/Resources/Data/LevelData.json` 的eventPoolId用事件numericId，不是字符串id。
2. 节点id在事件内唯一，检查startNodeId、defaultEndNodeId和全部nextNodeId。正文/标题用key；中英Event表为key/text。文案审阅源 `Assets/Docs/事件本地化文本审阅.md`，只同步目标事件，保留用户未同步编辑。
3. recipe为数字字符串（1火上、2风左、3水下、4土右）；randomRecipeLength在创建时生成，ignoreOrder控制无序。事件只用首读取序列；isExitOption不参加普通匹配。
4. 优先effects；rewardType须在HandSystemUI.ResolveEventEffectRoutine有实际case。GainRelic/RemovePollution等保留值不要使用。具体的道具奖励见下表，不能把同名字段套到所有类型。
5. 选项iconName是 `Assets/Resources/Images/Events/` 下图片名，不含目录/扩展名；缺图可静默隐藏。Tag提示按modifier key链查证，不把所有tagIds都当道具Tag。
6. 新事件关卡再接 `Assets/Resources/Data/ChapterData.json` 目标章节实际使用的EventPool/对应关卡池；池里放关卡ID，不是事件ID。已可达事件只修改授权目标字段。
7. 完成一批后集中检查节点、类型、双语key、ID引用。效果/流程改变才验证匹配或退出目标路径、选择完成后再跳节点；只改文案不跑整局。探针不留正式池。离开节点ForceDiscardAll，地图只显示移动箭头。

## 道具奖励契约

| 配置 | 实际行为 | 时序与限制 |
| --- | --- | --- |
| `rewardType: 4`（GainMagic） | ShowEventMagicRewardRoutine打开奖励选择；**不读magicIds**，沿当前关卡奖励池/解锁过滤 | 列入IsDeferredEventEffect，回收后执行 |
| `rewardType: 25`（GainMagicById）+`magicIds` | ShowEventMagicByIdRoutine读取有效道具numericId并直接入槽；不经过随机池解锁过滤 | **不在延迟名单**，回收前执行；满栏直接不发，容量不足只发得下部分，无替换 |

GainMagicModifier/GainMaterialModifier与GainMagic同为回收后延迟；不能把GainMagicById也归进去。枚举旧注释“满栏替换”与当前HandSystemUI不符，以实际GrantEventMagicPackRoutine为准。设计若要求满栏补偿/替换或直接赠送也检查锁定，需在本次任务明确并实现，不靠修改描述假装已有。

指定/随机两种可复制effects对象、全事件与文本形状在模板中；数值类型仍以当前GameDataTypes和执行switch为准，避免维护一份全枚举表。
