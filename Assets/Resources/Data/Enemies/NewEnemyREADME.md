# NewEnemyREADME

本目录用于放置单个敌人的 JSON。运行时通过 `Resources.LoadAll<TextAsset>("Data/Enemies")` 读取本目录所有文本资源，并按 `numericId` 注册到 `GameDataDatabase.EnemyData`。

## 新增敌人流程

1. 在本目录新增一个文件，如 `Enemy_014.json`。
2. 填写唯一 `numericId`，并在 `Assets/Resources/Data/Localization/zh-CN_Enemy.json` 增加 `nameKey` 对应文本。
3. 如果战斗关卡要使用它，在 `Assets/Resources/Data/LevelData.json` 的 `enemies` 中引用 `enemyId`。
4. 如果敌人需要特殊行为（死亡效果、特殊意图等），在 `Assets/Scripts/Enemies/` 新增独立 `XXEnemyModel.cs`，并在 `Assets/Scripts/Enemies/EnemyFactory.cs` 按 `numericId` 创建该类；普通敌人不需要脚本，默认 `EnemyModel` 即可。
5. 敌人贴图放在 `Assets/Resources/Images/Enemies/`，`iconName` / `spriteAnimationPath` 使用 Resources 路径名，不写扩展名。

## 基础结构

```json
{
  "numericId": 14,
  "string_id": "enemy_example",
  "nameKey": "enemy.example.name",
  "maxHealth": 30,
  "baseAttack": 5,
  "initialBuffs": [],
  "phases": [
    {
      "phase": 0,
      "intentPool": [
        { "intents": [ { "actionType": 1, "value": 5 } ] },
        { "intents": [ { "actionType": 2, "value": 8 } ] }
      ]
    }
  ],
  "iconName": "示例敌人",
  "spriteAnimationPath": "",
  "animationFrameRate": 8.0
}
```

## EnemyData 字段

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `numericId` | int | 是 | 敌人数字 ID，必须唯一。关卡、召唤意图、工厂映射都使用它。`<=0` 会被读取器忽略。 |
| `id` | string | 否 | 旧字符串 ID。当前新表通常不用。 |
| `string_id` | string | 建议 | 敌人的字符串 ID。`EnemyData.Id` 会优先返回它；用于日志和可读性。 |
| `nameKey` | string | 是 | 本地化 key，敌人名通过 `LocalizationSystem.GetText(nameKey, Data.Id)` 显示。 |
| `maxHealth` | int | 是 | 最大生命值，同时也是战斗开始当前生命值。 |
| `baseAttack` | int | 否 | 基础攻击字段。目前标准意图主要读取意图里的 `value`，该字段保留给脚本或未来逻辑使用。 |
| `initialBuffs` | array | 否 | 战斗开始自动获得的 Buff 列表。每项见 `BuffStackData`。没有就写 `[]`。 |
| `phases` | array | 建议 | 分阶段意图池。当前普通敌人都使用 `phase: 0`。如果存在，优先用当前 `Phase` 对应的 `intentPool`。 |
| `intentLoop` | array | 否 | 旧/备用意图循环。只有没有可用 `phases` 时才使用。 |
| `actionLoop` | array | 否 | 更旧的行动循环。只有没有可用意图组时，才转成单意图使用。新敌人优先用 `phases.intentPool`。 |
| `iconName` | string | 建议 | 静态头像/默认动画帧路径。实际加载 `Resources/Images/Enemies/{iconName}`。 |
| `spriteAnimationPath` | string | 否 | 动画帧文件夹/路径。为空时使用 `iconName` 的静态图。 |
| `animationFrameRate` | float | 否 | 敌人序列帧播放帧率，默认 8。 |

## BuffStackData

```json
{ "buffType": 10, "stack": 3 }
```

| 字段 | 说明 |
| --- | --- |
| `buffType` | `BuffEnum` 数字。 |
| `stack` | 层数，`<=0` 不会生效。 |

常用 `BuffEnum`：`1 Shield`，`2 Burning`，`3 Weak`，`6 Vulnerable`，`7 Slow`，`9 Arc`，`10 SpellPower`，`11 BurningNextTurn`，`12 ShieldReflect`，`13 ExtraDraw`，`14 ExtraRefresh`，`15 Sturdy`，`16 Stable`，`17 Disorder`，`18 DefensePower`，`19 BurnOnAttack`，`20 RepeatSpell`，`21 DebuffPower`，`22 VortexNextDraw`。

## 意图结构

`phases[].intentPool` 是一个循环列表。敌人每行动一次推进 `ActionIndex`，按顺序取下一个意图组；到末尾后循环。

```json
{
  "id": 1,
  "onlyOnce": false,
  "intents": [
    { "actionType": 1, "value": 5, "times": 1 }
  ]
}
```

| 字段 | 位置 | 说明 |
| --- | --- | --- |
| `phase` | `EnemyPhaseData` | 阶段编号。当前普通敌人通常只写 `0`。脚本可调用 `SetPhase` 切阶段。 |
| `intentPool` | `EnemyPhaseData` | 当前阶段的意图组循环。 |
| `id` | `EnemyIntentGroupData` | 意图组 ID。只在 `onlyOnce=true` 时用于记录已用过。可省略或为 0。 |
| `onlyOnce` | `EnemyIntentGroupData` | true 时该组只会被选择一次，之后跳过。 |
| `intents` | `EnemyIntentGroupData` | 同一回合同时展示并依次执行的意图数组。 |

## EnemyActionType / intent 字段

`intentType` 会由代码根据 `actionType` 自动补齐，新 JSON 通常不用写。

| actionType | 名称 | 需要字段 | 效果 |
| --- | --- | --- | --- |
| `1` | `Attack` | `value`，可选 `times` | 攻击玩家。`times` 默认为 1。 |
| `2` | `GainShield` | `value` | 敌人获得护盾。 |
| `3` | `ApplyBuff` | `buffs` | 给敌人自己添加 Buff。 |
| `6` | `ApplyDebuff` | `buffs` | 给玩家添加 Buff/Debuff。 |
| `7` | `Summon` | `summonEnemyId` 或 `value`，可选 `summonCount` | 召唤敌人。`summonCount` 默认为 1。 |
| `8` | `AttackAll` | `value`，可选 `times` | 攻击玩家，并攻击场上其他敌人。慎用。 |
| `9` | `Special` | `value` | 调用具体敌人脚本的 `ProcessSpecialIntent(value, playerState)`；没有脚本时只打日志。 |
| `10` | `Stunned` | 无 | 跳过行动，用于眩晕/空过意图。 |

`descriptionKey` 字段仍存在，但当前意图 UI 主要根据 `actionType` 显示图标和数值；如果后续做文字 tooltip，再补充本地化。

## 常见注意事项

- JSON 不能写注释。
- `numericId` 不要复用；否则后加载的资源会覆盖前一个 ID。
- 只改 JSON 后，Unity 可能需要刷新资源；如果新增 C# 敌人脚本，必须重新编译。
- 敌人名本地化放在 `Assets/Resources/Data/Localization/zh-CN_Enemy.json`。
- 关卡里推荐使用 `enemies` 数组而不是只用旧 `enemyIds`，这样可以配置每个敌人的位置：`{ "enemyId": 14, "x": 0, "y": 0 }`。
