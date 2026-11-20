---
id: kd_62f8bd2c-3806-4a01-8580-33741737e202
injectMode: inherit
injectAgents:
- '*'
aiEditMode: inherit
skillEnabled: true
skillSurface: command
---

# 敌人接入附页

1. 读 `Assets/Scripts/Data/GameDataTypes.cs` 的EnemyData/意图类型、`EnemyDefinition.cs`、`GameDataReader.cs`及相近敌人。最终数据=JSON基线+GameContentDatabase同ID SO覆盖；日常改已有敌人优先目标SO。
2. 新敌人保持唯一numericId/string_id；需要SO时用Unity CreateAsset/序列化API创建Definition并加入 `Assets/Resources/Config/GameContentDatabase.asset`。JSON可作基线，不能把未注册SO当已生效。
3. 普通行为沿用EnemyModel；特殊行为单独放 `Assets/Scripts/Enemies/`，文件名采用 `XXEnemyModel.cs`，在EnemyFactory按numericId注册。检查最后仍ApplyRuntimeDefinition，勿绕过进阶修改或把运行数值写回SO。
4. 用intentGroups+intentLoop表达组、循环、随机组、onlyOnce，phases处理阶段。intentPool/actionLoop是兼容路径，按当前类型与相近资产填字段，不抄旧README的过期优先级。Special调用ProcessSpecialIntent；同步意图显示值/预览/Tooltip且预览不消耗随机。
5. LevelData的enemies/randomEnemyGroups绑定enemyId与坐标；需要新关卡再接Chapter池。补中英Enemy表、Images/Enemies图像或序列帧路径，专属意图/Buff图标按实际displayType/BuffIconData绑定。
6. 批量收尾检查最终加载数据和Factory类型、一次意图/死亡分支；召唤/死亡清场变动再选现有测试。无需用真实存档或普通运行奖励池做探针。

`Assets/Editor/EnemyContentImportValidator.cs` 的 **Validate ScriptableObjects** 才是内容检查。**Import JSON to ScriptableObjects** 会SetData覆盖已有SO并替换数据库列表，不得以“验证”为由全量导入覆盖Inspector修改。

删除或换ID需先查关卡、召唤、工厂、SO数据库、存档兼容与序列化使用；保留GUID不等于保留SO数据。离线不能raw YAML修改SO。
