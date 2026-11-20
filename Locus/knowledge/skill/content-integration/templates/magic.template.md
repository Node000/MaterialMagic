---
id: kd_f23003ab-219a-448a-b539-b7b221b17e21
injectMode: inherit
injectAgents:
- '*'
aiEditMode: auto
skillEnabled: false
---

# 新道具最小模板

只在新增道具时加载；上级流程见 [道具接入](../magics.md)。这是文档内的复制模板，不是已注册游戏内容，也不是独立Skill。代码签名与当前 `Assets/Scripts/Data/MagicModel.cs` 对齐，实施时仍核对目标分支。

## 替换约定

`ExampleBoltMagicModel`、`magic_example_bolt`、`magic.example_bolt.*`、`example_bolt`、900001都是**示例值，未预留**。复制到正式文件前统一替换，确认numericId/id不冲突且未复用已发布ID；2点伤害、稀有度和配方也按需求确定。不要直接创建同名测试内容或覆盖整张表。

## 1. MagicData 条目

下方外层items仅展示文件形状；实际将一条记录合并到 `Assets/Resources/Data/MagicData.json` 的现有items中。

```json
{
  "items": [
    {
      "numericId": 900001,
      "id": "magic_example_bolt",
      "rarity": 0,
      "script": "ExampleBoltMagicModel",
      "nameKey": "magic.example_bolt.name",
      "descriptionKey": "magic.example_bolt.desc",
      "iconName": "example_bolt",
      "tagIds": [],
      "recipe": [1, 2],
      "matchRule": 0,
      "playPlayerCastAnimation": true
    }
  ]
}
```

当前枚举：rarity为Common0/Rare1/Epic2/Legendary3；recipe方向为Fire1上、Wind2左、Water3下、Earth4右。None0不作普通配方，Wild5需按实际匹配需求确认。matchRule为ExactRecipe0、AnyTwoDifferentElements1；后者按当前位置两项是否能匹配不同基础元素判断，不按recipe具体内容判定。效果数值不写入旧power/element字段；普通配方保持非空。

## 2. 独立脚本

在 `Assets/Scripts/Magics/` 新增与类名相同的 `.cs` 文件。单目标伤害模板如下：

```csharp
public class ExampleBoltMagicModel : MagicModel
{
    public ExampleBoltMagicModel(MagicData data, int slotIndex = 0)
        : base(data, slotIndex) { }

    public override MagicEffectType EffectType => MagicEffectType.Damage;

    protected override void ResolveCast(
        PlayerState playerState,
        BattleManager battleManager,
        MagicCastResult result)
    {
        DamageTarget(playerState, battleManager, 2, result);
    }
}
```

不要复制/重写MagicModel.Cast的整条管线；辅助函数保留强化、攻击/受击、来源、结果和死亡链。没有新持久状态时，通常不需要增加存档字段。

| 效果 | 当前调用形状 | 表现声明 |
| --- | --- | --- |
| 单目标伤害 | `DamageTarget(playerState, battleManager, 2, result)` | EffectType.Damage |
| 多段单目标 | `DamageTargetTimes(playerState, battleManager, 2, 3, result)` | 每击经规则链并推进结果step |
| 全体伤害 | `DamageAll(playerState, battleManager, 2, result)` | EffectType.Damage；按需求覆写CastParticleTargetsAllEnemies为true |
| 敌方Buff | `AddBuff(Target(battleManager), BuffEnum.Burning, 3, result)` | EffectType.ApplyBuff；复合效果沿现有相近类 |
| 玩家护盾 | `GainShield(playerState, battleManager, 2, result)` | EffectType.GainShield；可显式覆写CastParticleTargetsPlayer为true，参照StoneWallMagicModel |

辅助调用只展示API形状，不表示能任意混用分类。HandSystemUI.GetMagicEffectTargetType把GainShield/Heal/DrawNextTurn或CastParticleTargetsPlayer视为玩家方向，其余默认敌方；CastParticleTargetsAllEnemies控制全体表现，**不会代替DamageAll规则**。playPlayerCastAnimation=false会跳过该表现入口，不会替你实现效果。复合目标沿相近类确认，颜色/尺寸从已有美术配置读取。

## 3. 工厂注册

在 `Assets/Scripts/Magics/MagicFactory.cs` 的 `switch (data.script)` 内、default前加入：

```csharp
case nameof(ExampleBoltMagicModel):
    return new ExampleBoltMagicModel(data, slotIndex);
```

JSON.script必须等于类名。工厂当前签名为 `public static MagicModel Create(MagicData data, int slotIndex = 0)`，默认分支返回空效果MagicModel。检验必须从工厂创建并看实际类型，不能只new子类证明注册正确。

## 4. 双语与图像

分别合并到 `Assets/Resources/Data/Localization/zh-CN.json` 和 `Assets/Resources/Data/Localization/en-US.json` 的items：

```json
{
  "items": [
    { "key": "magic.example_bolt.name", "text": "示例道具" },
    { "key": "magic.example_bolt.desc", "text": "对目标造成2点伤害。" }
  ]
}
```

```json
{
  "items": [
    { "key": "magic.example_bolt.name", "text": "Example Bolt" },
    { "key": "magic.example_bolt.desc", "text": "Deal 2 damage to the target." }
  ]
}
```

图像导入为Sprite，放 `Assets/Resources/Images/Magics/`；上例需命名为example_bolt对应的素材，iconName只写相对这个目录的名称，不含Images/Magics前缀和扩展名。加载器会拼接 `Images/Magics/`。不要为套模板修改已有素材导入参数。

默认空tagIds无需新Tag；若新增关键词，补 `Assets/Resources/Data/TagData.json` 条目的id/nameKey/descriptionKey，并补两种 `_Tag.json` 的key/text。显示名使用本地化key，不依赖旧JSON额外displayName字段。

## 5. 收口

按需要加载 [获得途径模板](magic-sources.template.md)，至少明确一个正式来源或明确本次只建定义。新增类完整编译；改数据导入后用新运行/重建视图观察。最低限度检查工厂类型、目标配方/效果和本次要求的来源；只有规则、UI、持久状态被改动时再增加对应验证。模板文档不进入Resources、工厂、池或用户存档。
