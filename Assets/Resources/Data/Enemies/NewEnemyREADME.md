# 新增与修改敌人入口

正式步骤见 `Locus/knowledge/skill/content-integration/enemies.md`；当前内容契约见 `Locus/knowledge/memory/unity-project-understanding/content.md`。

- 本目录JSON是基线；GameDataDatabase随后加载 `Assets/Resources/Config/GameContentDatabase.asset` 的EnemyDefinition，按同numericId覆盖。已有SO对应的敌人只改JSON不会改变最终结果。
- 日常修改目标SO；新增Definition还须注册数据库列表。结构化资产用Unity API，保留GUID与美术值。
- 意图优先看intentGroups+intentLoop及phases；intentPool/actionLoop为兼容字段，具体选择顺序看EnemyRuntimeDefinition，不沿用旧表优先级。
- 特殊行为独立放 `Assets/Scripts/Enemies/`，文件名采用 `XXEnemyModel.cs`，EnemyFactory按numericId创建，再统一ApplyRuntimeDefinition；普通行为可用EnemyModel。
- LevelData用enemies/randomEnemyGroups绑定enemyId和位置；同步中英Enemy表、图像/序列帧及意图显示。
- EnemyContentImportValidator的Validate是检查，Import JSON会SetData覆盖已有SO并替换数据库列表。不得为验证全量导入。

[敌人ID对照](EnemyIdMap.md)只列身份；数值从最终SO/运行定义读取。
