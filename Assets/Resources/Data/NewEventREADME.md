# NewEventREADME

事件数据集中在 `Assets/Resources/Data/EventData.json`。事件由若干节点组成：玩家进入事件后抽取事件手牌，回合结束时用出牌区素材匹配当前节点的选项，匹配后执行效果并跳转到下一个节点。

## 新增事件流程

1. 在 `EventData.json` 的 `items` 中新增一项，填写唯一 `numericId`。
2. 在 `Assets/Resources/Data/Localization/zh-CN_Event.json` 增加标题、节点文本、选项标题等 key。
3. 如果要让关卡使用该事件，在 `Assets/Resources/Data/LevelData.json` 的事件关卡中设置 `eventPoolId` 为事件的 `numericId`。如果 `eventPoolId <= 0` 或找不到，会退回读取第一个事件。
4. 优先使用 `effects` 数组描述奖励/惩罚；旧 `resultId` 只保留给少量兼容事件。
5. 如选项使用 `tagIds` 展示素材 modifier 提示，需要保证对应 `modifier.{id}.name/desc` 本地化存在。

## 基础结构

```json
{
  "numericId": 108,
  "id": "event_example",
  "titleKey": "event.example.title",
  "startNodeId": "start",
  "defaultEndNodeId": "leave",
  "drawCount": 4,
  "nodes": [
    {
      "id": "start",
      "textKeys": [
        "event.example.start.1"
      ],
      "options": [
        {
          "id": "take_reward",
          "titleKey": "event.example.option.take_reward",
          "recipe": "12",
          "ignoreOrder": true,
          "effects": [
            { "rewardType": 3, "amount": 2 }
          ],
          "nextNodeId": "reward"
        },
        {
          "id": "leave",
          "titleKey": "event.common.option.leave",
          "isExitOption": true,
          "nextNodeId": "leave"
        }
      ]
    },
    {
      "id": "reward",
      "textKeys": [
        "event.example.reward.1"
      ],
      "options": []
    },
    {
      "id": "leave",
      "textKeys": [
        "event.example.leave.1"
      ],
      "options": []
    }
  ]
}
```

## EventData 字段

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `numericId` | int | 是 | 事件数字 ID，必须唯一。事件关卡通过 `LevelData.eventPoolId` 引用。`<=0` 会被读取器忽略。 |
| `id` | string | 是 | 事件字符串 ID，用于日志和可读性。建议格式 `event_xxx`。 |
| `titleKey` | string | 是 | 事件标题本地化 key。 |
| `startNodeId` | string | 建议 | 起始节点 ID。为空或找不到时使用 `nodes[0]`。 |
| `defaultEndNodeId` | string | 否 | 当前节点没有匹配普通选项时，可自动跳到的默认结束节点。若为空但存在 `default_end` 节点，会使用 `default_end`。 |
| `drawCount` | int | 否 | 进入事件时抽几张事件手牌。`>=0` 使用该值；`-1` 或省略时使用玩家当前 `DrawCount`。 |
| `nodes` | array | 是 | 事件节点列表。每个节点 ID 必须在本事件内唯一。 |

## EventNodeData 字段

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `id` | string | 是 | 节点 ID。`startNodeId` / `nextNodeId` 通过它跳转。 |
| `textKeys` | string[] | 否 | 节点正文 key 列表。事件面板会逐段显示。 |
| `nextNodeId` | string | 否 | 无选项节点可用的自动后继节点。`TryAdvanceToNextNode` 使用它。 |
| `options` | array | 否 | 当前节点可匹配/可退出的选项。没有就写 `[]`。 |

## EventOptionData 字段

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `id` | string | 是 | 选项 ID。重复选项的 `escalatePerUse` 计数按这个 ID 记录。 |
| `titleKey` | string | 是 | 选项标题本地化 key。 |
| `recipe` | string | 条件 | 固定配方字符串，如 `"123"` 表示火风水。普通选项需要它才能被素材匹配。 |
| `randomRecipeLength` | int | 否 | 大于 0 时进入事件时随机生成该长度配方，并覆盖 `recipe`。随机素材范围为 1-4。 |
| `ignoreOrder` | bool | 否 | true 时只要求素材种类/数量匹配，不要求顺序。默认 false。 |
| `effects` | array | 建议 | 新事件优先使用的效果数组。可同时写多个效果。 |
| `resultId` | int | 否 | 旧兼容结果。没有 `effects` 时才执行。新事件尽量不用。 |
| `isExitOption` | bool | 否 | true 时作为退出/保底选项，不参与普通素材匹配；如果没有匹配普通选项，代码会尝试选择它。 |
| `nextNodeId` | string | 否 | 效果执行后跳转的节点。为空或找不到时事件结束。 |
| `choiceCount` | int | 否 | 旧 `resultId` 选牌数量，或作为 `effects.choiceCount` 的备用值。 |
| `tagIds` | string[] | 否 | 选项 tooltip 关键词。当前按 `modifier.{id}.name/desc` 查本地化，常用于素材 modifier 选项。 |

## 配方字符串

事件配方是字符串，不是数组。每个字符是一张素材：

| 字符 | 素材 |
| --- | --- |
| `1` | 火 |
| `2` | 风 |
| `3` | 水 |
| `4` | 土 |

示例：

- `"12"`：按顺序打出火、风。
- `"24"` + `ignoreOrder: true`：打出风、土，顺序不限。
- `randomRecipeLength: 3`：进入事件时随机生成 3 张基础素材配方。

## EventEffectData 字段

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `rewardType` | int | 效果类型，见下表。 |
| `amount` | int | 数值。不同效果默认值不同；`0` 表示使用默认值。 |
| `count` | int | 数量。用于获得素材等；`<=0` 表示使用默认数量。 |
| `choiceCount` | int | 选择数量。用于删除素材/法术强化选择；优先级高于 option 的 `choiceCount`。 |
| `escalatePerUse` | int | 每次重复选择同一 option 后，额外增加的失血数值。当前只在 `LoseHealth` 中使用。 |
| `material` | int | 指定素材类型。当前用于 `GainMaterial`；为 0 时随机基础素材。 |

## EventRewardType

| rewardType | 名称 | 字段 | 效果 |
| --- | --- | --- | --- |
| `1` | `Heal` | `amount`，默认 10 | 回复生命。 |
| `2` | `LoseHealth` | `amount`，默认 1；可选 `escalatePerUse` | 失去生命，直接扣血。 |
| `3` | `GainGold` | `amount`，默认 1 | 获得金币并播放金币动画。 |
| `4` | `GainMagic` | 无 | 打开一次法术奖励选择。该效果会延后到回合回收后执行。 |
| `8` | `GainMagicModifier` | `choiceCount`，默认 2 | 打开法术强化选择。该效果会延后到回合回收后执行。 |
| `9` | `IncreaseMaxHealth` | `amount`，默认 5 | 提高生命上限。 |
| `10` | `GainMaterial` | `material`，`count` 默认 1 | 获得指定素材；`material=0` 时随机基础素材。 |
| `11` | `GainRandomMaterial` | `count`，默认 1 | 获得若干张随机基础素材，每张独立随机。 |
| `12` | `GainSameRandomMaterials` | `count`，默认 1 | 随机一种基础素材，获得多张同种素材。 |
| `13` | `IncreaseDrawCount` | `amount`，默认 1 | 玩家每回合抽牌数增加。 |
| `14` | `RemoveMaterial` | `choiceCount`，默认 1 | 打开素材列表，选择并删除牌组中的素材。 |

枚举里还有旧值 `0 None`、`5 UpgradeMaterial`、`6 RemovePollution`、`7 GainRelic`，当前事件执行代码没有处理这些效果，新事件不要使用。

## 旧 resultId

仅在选项没有 `effects` 时执行：

| resultId | 效果 |
| --- | --- |
| `1` | 回复 10 生命。 |
| `2` | 抽牌数 +1。 |
| `100` | 选择并删除 1 张素材；`choiceCount` 可改数量。 |
| `101` | 获得火素材。 |
| `102` | 获得风素材。 |
| `103` | 获得水素材。 |
| `104` | 获得土素材。 |
| `201` | 给所选手牌添加 `KindlingModifier`。 |
| `202` | 给所选手牌添加 `FlowModifier`。 |
| `203` | 给所选手牌添加 `LiquefyModifier`。 |

新事件优先用 `effects`。只有需要沿用旧选牌改造素材流程时，才考虑 `resultId`。

## 节点结束规则

- 匹配到普通选项：先执行非延后 `effects`，回收本回合牌，再执行法术奖励/法术强化等延后效果，然后按 `nextNodeId` 跳转。
- 没匹配普通选项：尝试选择 `isExitOption=true` 的选项。
- 仍没有选项：尝试 `defaultEndNodeId` / `default_end`。
- 选项没有有效 `nextNodeId` 或目标节点不存在：事件结束，进入奖励/下一流程。

## 常见注意事项

- JSON 不能写注释。
- 文本都写本地化 key，不要把正文中文直接写进事件 JSON。
- 有 `randomRecipeLength` 的选项会在事件创建时随机一次；同一事件模型内不会每回合重随机。
- `GainMagic` 和 `GainMagicModifier` 是延后效果；如果同一个选项还有扣血/金币/素材等效果，会先执行那些，再回收卡牌，最后打开奖励选择。
- `isExitOption=true` 的选项不会被 `TryGetMatchedOption` 当作普通素材选项匹配，因此退出选项可以不写 `recipe`。
