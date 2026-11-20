---
id: kd_5ea82e2f-cc83-40dc-bcbd-9e95a7d874fa
injectMode: inherit
injectAgents:
- '*'
aiEditMode: auto
skillEnabled: false
---

# 事件节点与道具奖励模板

新增事件或修改事件发放道具时读取，上级流程见 [事件接入](../events.md)。本模板只有Markdown示例，不创建正式事件/关卡；900201事件、900301关卡、900001道具与所有example key均未预留，复制前替换并核对引用。

## 两种“获得道具”先选对

| 需求 | rewardType/枚举 | 读取字段与行为 | 当前执行时点 |
| --- | --- | --- | --- |
| 打开通用道具奖励选择 | 4 GainMagic | 调用ShowEventMagicRewardRoutine/RewardPanelUI.ShowMagicOnly；不使用magicIds，候选走当前奖励池/解锁链 | IsDeferredEventEffect列入：先非延迟效果，再ForceDiscardAll回收，随后打开奖励 |
| 直接发放指定道具 | 25 GainMagicById | 读取magicIds中有效numericId，ShowEventMagicByIdRoutine/GrantEventMagicPackRoutine直接入槽；不经过随机奖励候选的解锁过滤 | 不在延迟名单，回收前执行；协程完成后才继续后继流程 |

指定发放时满栏直接不发、空位不足只发放得下的部分；当前没有槽位替换。GameDataTypes中旧注释仍写满栏替换，应以HandSystemUI实际路径为准。GainMagic的满栏选择也受现有奖励面板/空位约束，不能从“打开面板”推断必然获得道具。若需要新的满栏补偿或锁定检查，应作为明确玩法变更实现。

## 1. 固定配方、固定道具事件

合并一条到 `Assets/Resources/Data/EventData.json` 的items；以下外层只示意容器，不替换原表。事件配方为字符串，`"12"`表示上(火)、左(风)，与MagicData.recipe整数数组不同。

```json
{
  "items": [
    {
      "numericId": 900201,
      "id": "event_example_gift",
      "titleKey": "event.example_gift.title",
      "startNodeId": "start",
      "defaultEndNodeId": "leave",
      "drawCount": 4,
      "nodes": [
        {
          "id": "start",
          "textKeys": ["event.example_gift.start.1"],
          "options": [
            {
              "id": "take",
              "titleKey": "event.example_gift.option.take",
              "iconName": "",
              "recipe": "12",
              "ignoreOrder": false,
              "effects": [
                { "rewardType": 25, "magicIds": [900001] }
              ],
              "nextNodeId": "received"
            },
            {
              "id": "leave",
              "titleKey": "event.example_gift.option.leave",
              "isExitOption": true,
              "effects": [],
              "nextNodeId": "leave"
            }
          ]
        },
        {
          "id": "received",
          "textKeys": ["event.example_gift.received.1"],
          "options": []
        },
        {
          "id": "leave",
          "textKeys": ["event.example_gift.leave.1"],
          "options": []
        }
      ]
    }
  ]
}
```

这是“尝试发放”样例，满栏可能未收到，因此结果文案不要无条件宣称已入槽。若要通用奖励，**用下列对象替换整个effects条目**，不保留magicIds：

```json
{ "rewardType": 4 }
```

其他奖励也按EventRewardType与ResolveEventEffectRoutine实际case配置；保留GainRelic枚举不代表已实现。randomRecipeLength、ignoreOrder、重复代价等不在本最小样例中，按需要读EventModel与相近事件。

## 2. 中英文本

以下分别合并到 `Assets/Resources/Data/Localization/zh-CN_Event.json` 和 `Assets/Resources/Data/Localization/en-US_Event.json`：

```json
{
  "items": [
    { "key": "event.example_gift.title", "text": "示例赠礼" },
    { "key": "event.example_gift.start.1", "text": "终端提供一件道具，需要留出道具栏空位。" },
    { "key": "event.example_gift.option.take", "text": "领取" },
    { "key": "event.example_gift.option.leave", "text": "离开" },
    { "key": "event.example_gift.received.1", "text": "终端完成了交付尝试。" },
    { "key": "event.example_gift.leave.1", "text": "你离开了终端。" }
  ]
}
```

```json
{
  "items": [
    { "key": "event.example_gift.title", "text": "Example Gift" },
    { "key": "event.example_gift.start.1", "text": "The terminal offers an item. Leave room in your item bar." },
    { "key": "event.example_gift.option.take", "text": "Collect" },
    { "key": "event.example_gift.option.leave", "text": "Leave" },
    { "key": "event.example_gift.received.1", "text": "The terminal has finished its delivery attempt." },
    { "key": "event.example_gift.leave.1", "text": "You leave the terminal." }
  ]
}
```

按用户文案规范同步 `Assets/Docs/事件本地化文本审阅.md` 的目标事件，再回写两表；不全表重新生成覆盖用户未同步编辑。选项图标需要时放Images/Events并仅填图片名（不带路径/扩展名）；空值就是不显示图标。

## 3. 接入关卡

仅新增事件还不会自动在地图出现。需要新事件关卡时，在 `Assets/Resources/Data/LevelData.json` 的items新增例如：

```json
{
  "numericId": 900301,
  "id": "level_event_example_gift",
  "titleKey": "event.example_gift.title",
  "levelType": 1,
  "eventPoolId": 900201
}
```

LevelType.Event=1；eventPoolId指向事件numericId，**不是**关卡ID或事件字符串id。再把900301这个关卡ID接入 `Assets/Resources/Data/ChapterData.json` 的目标章节实际使用的EventPool/对应池；不要将900201事件ID直接放关卡池。复用既有可达关卡时只替换授权字段，保留其他数值。

## 4. 最小验证

同批检查JSON字段/节点后继/中英key/道具与关卡ID引用。效果路由分别核对4不读取magicIds、25读取magicIds；指定发放任务覆盖有空位与本次关心的满栏/不足容量边界。行为改变才运行匹配与退出目标路径；离开节点确认ForceDiscardAll，延迟奖励等待完成再推进。普通文案只检查目标文本，不默认跑所有节点和保存链；不保留正式测试事件。
