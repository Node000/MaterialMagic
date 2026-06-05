# NewMagicREADME

`MagicData.json` 只保存法术的展示、配方、标签和脚本类名；实际战斗效果写在对应的 `MagicModel` 子类里。

## 新增法术流程

1. 在 `Assets/Resources/Data/MagicData.json` 的 `items` 中新增一项，填写唯一 `numericId`。
2. 在 `Assets/Scripts/Magics/` 新增独立脚本 `XXMagicModel.cs`，继承 `MagicModel` 并重写 `ResolveCast`。
3. 在 `Assets/Scripts/Magics/MagicFactory.cs` 中增加 `case nameof(XXMagicModel)`，返回新类实例。
4. 在 `Assets/Resources/Data/Localization/zh-CN.json` 增加 `nameKey` / `descriptionKey` 文本。
5. 图标放在 `Assets/Resources/Images/Magics/`，`iconName` 写 Resources 路径名，不写扩展名。
6. 如果新法术要进入奖励池，在 `Assets/Resources/Data/RewardPoolData.json` 的 `magicIds` 中加入它的 `numericId`。
7. 如果使用新 `tagIds`，同步更新 `Assets/Resources/Data/TagData.json` 和 `Assets/Resources/Data/Localization/zh-CN_Tag.json`。

## 基础结构

```json
{
  "numericId": 33,
  "id": "magic_example",
  "script": "ExampleMagicModel",
  "nameKey": "magic.example.name",
  "descriptionKey": "magic.example.desc",
  "iconName": "example_icon",
  "element": 1,
  "tagIds": [
    "burning"
  ],
  "recipe": [
    1,
    2
  ],
  "playPlayerCastAnimation": true
}
```

## MagicData 字段

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `numericId` | int | 是 | 法术数字 ID，必须唯一。存档、奖励池、初始配置都使用它。`<=0` 会被读取器忽略。 |
| `id` | string | 是 | 法术字符串 ID，用于日志和可读性。建议格式 `magic_xxx`。 |
| `script` | string | 是 | 具体 `MagicModel` 子类类名，必须和 C# 类名完全一致，如 `IgniteMagicModel`。`MagicFactory` 通过它创建实例。 |
| `nameKey` | string | 是 | 名称本地化 key。 |
| `descriptionKey` | string | 是 | 描述本地化 key。Tooltip 显示这里对应的文本。 |
| `iconName` | string | 是 | 图标 Resources 路径。实际加载 `Resources/Images/Magics/{iconName}`。 |
| `element` | int | 是 | 法术主元素，用于背景色/筛选。见 `MaterialEnum`。 |
| `tagIds` | string[] | 否 | 关键词 ID。用于 tooltip 关键词和部分强化筛选；没有就写 `[]`。 |
| `recipe` | int[] | 是 | 触发配方。默认按顺序匹配玩家出牌区连续素材。 |
| `matchRule` | int | 否 | 匹配规则。省略等于 `0 ExactRecipe`。 |
| `playPlayerCastAnimation` | bool | 否 | 是否播放玩家施法动画/粒子流程。省略时 C# 默认 true；护盾/抽牌等不需要飞向敌人的法术通常设 false。 |

## MaterialEnum / recipe 数字

| 数字 | 名称 | 含义 |
| --- | --- | --- |
| `0` | `None` | 无。配方里不要用。 |
| `1` | `Fire` | 火。 |
| `2` | `Wind` | 风。 |
| `3` | `Water` | 水。 |
| `4` | `Earth` | 土。 |
| `5` | `Wild` | 万能；当前一般作为素材能力，不建议直接作为普通配方需求。 |

## MagicMatchRule

| 数字 | 名称 | 说明 |
| --- | --- | --- |
| `0` | `ExactRecipe` | 默认规则。按 `recipe` 顺序逐张匹配。 |
| `1` | `AnyTwoDifferentElements` | 需要从当前位置开始的两张不同元素素材；此规则会忽略 `recipe` 的具体内容。 |

## 脚本类要求

最小脚本示例：

```csharp
public class ExampleMagicModel : MagicModel
{
    public ExampleMagicModel(MagicData data, int slotIndex = 0) : base(data, slotIndex) { }
    public override MagicEffectType EffectType => MagicEffectType.Damage;

    protected override void ResolveCast(PlayerState playerState, BattleManager battleManager, MagicCastResult result)
    {
        DamageTarget(playerState, battleManager, 3, result);
    }
}
```

常用可重写属性：

| 属性 | 说明 |
| --- | --- |
| `EffectType` | 法术分类，供强化筛选/表现分类使用，不负责实际结算。常用：`Damage`、`GainShield`、`Heal`、`ApplyBuff`、`DrawNextTurn`。 |
| `CastParticleTargetsAllEnemies` | true 时施法粒子尝试飞向所有敌人。AOE 伤害/全体 Debuff 常用。 |
| `CastParticleTargetsPlayer` | true 时表现目标视为玩家。自我强化、护盾、抽牌类可用。 |

常用辅助函数：

| 函数 | 用途 |
| --- | --- |
| `Target(battleManager)` | 取得本次锁定目标。 |
| `Damage(playerState, target, damage, result)` | 对指定敌人造成主动法术伤害，走玩家 Buff 与 Magic Modifier 攻击链。 |
| `DamageTarget(playerState, battleManager, damage, result)` | 对锁定目标造成伤害。 |
| `DamageAll(playerState, battleManager, damage, result)` | 对所有存活敌人造成伤害。 |
| `AddBuff(target, buffType, stack, result)` | 给单位添加 Buff；对敌方 Debuff 会吃玩家 `DebuffPower` 加成。 |
| `AddBuffAll(battleManager, buffType, stack, result)` | 给所有存活敌人添加 Buff。 |
| `GainShield(playerState, battleManager, amount, result)` | 玩家获得护盾，并触发护盾相关强化/反伤。 |
| `AddTemporaryMaterialToHand(playerState, material)` | 下回合把临时素材加入手牌。 |
| `AddMaterialNextTurn(playerState, material, modifier)` | 下回合获得带 modifier 的素材。 |

## tagIds

`tagIds` 只写 ID，例如 `"burning"`。Tag 的显示名/描述来自：

- `Assets/Resources/Data/TagData.json`
- `Assets/Resources/Data/Localization/zh-CN_Tag.json`

当前常见 tag：`burning`、`arc`、`charge`、`temporary`、`vortex`、`vulnerable`、`slow`、`weak`、`sturdy`、`shield_reflect`、`extra_draw`、`extra_refresh`、`spell_power`、`defense_power`、`burning_deepen`。

## 不再使用的旧字段

不要再往 `MagicData.json` 写这些旧通用效果字段：

- `effectType`
- `power`
- `hitCount`
- `buffType`
- `buffAmount`
- `rarity`

如果需要改法术效果，改对应 `Assets/Scripts/Magics/*MagicModel.cs`，并保持 `descriptionKey` 对应文案一致。

## 常见注意事项

- JSON 不能写注释。
- `script` 写错时，`MagicFactory` 会退回基础 `MagicModel`，导致法术没有实际效果；新增后务必验证创建出来的类型。
- 新增 C# 脚本后必须重新编译。
- `numericId` 不要复用，否则存档/奖励池会混乱。
- 文案写在本地化表，不要直接写中文到 `MagicData.json`。
