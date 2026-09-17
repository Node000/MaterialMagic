# Boss 地图格图标 与 箭头附魔图标 配置说明

实现日期：2026-09-17

## 1. Boss 地图格图标

- **确定时机**：每局的 Boss 在**生成章节地图时**确定一次 —— 先从章节 `BossPool` 抽 Boss 关卡，再给该关卡抽一个随机敌人组。结果随局内存档保存（`runPools.chapterBossLevelId` / `runPools.chapterBossGroupIndex`），读档后不变；Boss 格图标、Boss 战实际敌人均由这一份结果决定。
- **图标取值**：Boss 格（含进阶 A-20 增加的多个 Boss 格）图标 = 该 Boss 对应敌人组配置的图标；没配则回退通用图标 `Assets/Resources/Images/UI/Boss.png`。仅对 Boss 战生效，普通格不受影响。
- **配置位置**：`Assets/Resources/Data/LevelData.json`
  - 关卡带 `randomEnemyGroups` 时，写在**组**上：`"mapIconPath": "Images/UI/Boss蓝眼"`
  - 关卡没有随机组时，写在**关卡**上：`"mapIconPath": "Images/UI/Boss蓝眼"`
  - 值为 Resources 相对路径（不带扩展名）；**留空 = 用通用图标**（字段默认留空）。
- **当前已配**：`level_battle_023` 的三个组 → 敌人 21「蓝眼」/ 23「彩虹」/ 24「南极洲」，图标分别为 `Images/UI/Boss蓝眼`、`Boss彩虹`、`Boss南极洲`。
- **新增 Boss 图标**：PNG 放进 `Assets/Resources/Images/UI/`（导入类型必须是 Sprite），再在对应组/关卡填 `mapIconPath`。

## 2. 箭头附魔图标与两层颜色

- **图标预制体**：`Assets/Prefabs/UI/EnchantIcon.prefab`
  - 子物体 `底色`（画在下层）与 `上层`（画在上层），各一个 `Image`；根上的 `EnchantIconUI` 用序列化字段绑定这两层。
- **颜色配置**：`Assets/Resources/Config/Enchant_Color.asset`（`EnchantColorConfig`，一个附魔一条：`modifierId` / `topColor` / `baseColor`）
  - 改颜色：选中该资产，在 Inspector 列表里用取色器改（按 `modifierId` 对应 `Assets/Resources/EnchantConfig/MaterialModifiers/<id>.asset` 的 id）。
  - **未配置的附魔保持预制体自身颜色**，代码不写任何颜色默认值；新增附魔后到该资产补一条即可。
  - 初始值：生成时从每个附魔 SO 的 `lineColor` 播种，两层同色，美术按需区分两层。
- **选择附魔界面**（`MagicModifierSelectionPanelUI` 的「箭头附魔」模式）
  - 每个选项**只显示图标**（64×64、居中），不再显示名称/描述文本。
  - 附魔信息（名称 + 说明）改由统一详情面板 `UnifiedDetailPopup` 展示：悬停（进入选项）或按下时弹出。
  - 面板标题与操作提示文本保留（那是流程说明，不是附魔信息）。
- **道具强化模式**（同一面板的另一个模式）保持原样：图标 + 文本；两个模式互相切换时会互相隐藏对方的内容。
- 面板上的图标预制体引用为序列化绑定（`MagicModifierSelectionPanelUI.enchantIconPrefab`），三个场景（`SampleScene_PC` / `SampleScene_PE` / `PvOnlyScene`）均已绑定。

## 3. 相关代码

| 功能 | 位置 |
| --- | --- |
| Boss 确定与图标路径 | `RunManager.ResolveChapterBoss` / `ResolveChapterBossGroupIndex` / `ResolveChapterBossMapIconPath` |
| 地图生成时调用 | `HandSystemUI.BuildChapterMapGrid`（生成地图前调用 `ResolveChapterBoss`） |
| Boss 战取用同一份结果 | `HandSystemUI.ResolveChapterBossBattleLevel` / `SelectLevelEnemyGroup` |
| Boss 格图标绘制 | `ChapterGridPanelUI.ResolveBossMapIcon` / `ApplyCellVisual` |
| 附魔图标组件 | `Assets/Scripts/EnchantIconUI.cs` |
| 附魔颜色配置 | `Assets/Scripts/Data/EnchantColorConfig.cs` |
| 附魔详情内容 | `UnifiedDetailContentBuilder.Build(MaterialModifierData)`（`UnifiedDetailSourceType.MaterialModifier`） |
