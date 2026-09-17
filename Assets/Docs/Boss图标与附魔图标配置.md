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
- **颜色配置**：`Assets/Resources/Config/Enchant_Color.asset`（`EnchantColorConfig`，一个附魔一条：`modifierId` / `displayName` / `topColor` / `baseColor`）
  - **美术怎么改**：Project 里选中该资产 → Inspector 里每条的表头就是「附魔中文名（id）」，下面直接改「上层颜色」「底色层颜色」两个取色器即可；改完自动保存，不需要动代码与预制体。
  - **中文名注释**：`displayName` 只给美术对照用（不参与运行逻辑），由 Inspector 底部按钮「刷新附魔中文名」或菜单 `Tools/Content/Enchant/刷新附魔颜色表（补齐缺失 + 刷新中文名 + 校验）` 从 `zh-CN_Modifier.json` 写入。
  - **新增附魔时**：点同一个菜单（或 Inspector 上的「补齐缺失附魔」）会把还没进表的附魔补上，两层颜色先用该附魔 SO 的 `lineColor` 播种；「校验」会报「缺哪个附魔 / 表里多哪个 id / 重复 id」。
  - **未配置的附魔保持预制体自身颜色**，代码不写任何颜色默认值。
  - 当前状态：25 条覆盖全部 25 个附魔，无重复（校验通过）。
- **选择附魔界面**（`MagicModifierSelectionPanelUI` 的「箭头附魔」模式）
  - 每个选项**只显示图标**（64×64、居中），不再显示名称/描述文本。
  - 附魔信息（名称 + 说明）改由统一详情面板 `UnifiedDetailPopup` 展示：悬停（进入选项）或按下时弹出。
  - 面板标题与操作提示文本保留（那是流程说明，不是附魔信息）。
- **道具强化模式**（同一面板的另一个模式）保持原样：图标 + 文本；两个模式互相切换时会互相隐藏对方的内容。
- 面板上的图标预制体引用为序列化绑定（`MagicModifierSelectionPanelUI.enchantIconPrefab`），三个场景（`SampleScene_PC` / `SampleScene_PE` / `PvOnlyScene`）均已绑定。

## 2.1 统一详情面板里的附魔显示（箭头详情）

- **内容项预制体**：`Assets/Prefabs/UI/EnchantEntry.prefab`（根 `EnchantEntry`）+ 组件 `Assets/Scripts/EnchantEntryUI.cs`
  - 内部：嵌套 `EnchantIcon.prefab`（64×64，画在下方的图标）+ `Name`（TMP 文字，画在图标下方）。
  - 只按附魔 id 刷新：`EnchantIconUI` 刷两层颜色；名字取该附魔 SO 的 `nameKey` 本地化文本（取不到数据时退回 id）。
- **尺寸参考「道具强化选择面板」**：图标 64×64、名字 17pt（字体 `FZG_CN SDF`、居中、白色，与面板选项名同一样式）；内容项 120×96。
- **容器**：`UnifiedDetailPopup/EnchantEntryRoot`（Scene 内，`HorizontalLayoutGroup`：spacing 8 / 居中 / 不控制子项尺寸），位置与配方框 `ArrowSequenceFrame` 同槽位（两者互斥：配方框只用于道具、附魔行只用于箭头）。
- **屏幕尺寸对齐**：详情面板运行时缩放固定为 1（`theme.HiddenScale` → 动画终点 `Vector3.one`），所以容器 `localScale = 1.1` 即与选择面板（1.1）等大；要放大/缩小只改容器 `localScale` 或预制体尺寸。
- 没有附魔时不显示整行（容器隐藏）。

## 3. 相关代码

| 功能 | 位置 |
| --- | --- |
| Boss 确定与图标路径 | `RunManager.ResolveChapterBoss` / `ResolveChapterBossGroupIndex` / `ResolveChapterBossMapIconPath` |
| 地图生成时调用 | `HandSystemUI.BuildChapterMapGrid`（生成地图前调用 `ResolveChapterBoss`） |
| Boss 战取用同一份结果 | `HandSystemUI.ResolveChapterBossBattleLevel` / `SelectLevelEnemyGroup` |
| Boss 格图标绘制 | `ChapterGridPanelUI.ResolveBossMapIcon` / `ApplyCellVisual` |
| 附魔图标组件 | `Assets/Scripts/EnchantIconUI.cs` |
| 详情面板附魔内容项（图标 + 名字） | `Assets/Scripts/EnchantEntryUI.cs` |
| 附魔颜色配置 | `Assets/Scripts/Data/EnchantColorConfig.cs` |
| 附魔颜色表美术向 Inspector（中文名表头 + 补齐/刷新/校验按钮） | `Assets/Editor/EnchantColorConfigEditor.cs` |
| 附魔详情内容 | `UnifiedDetailContentBuilder.Build(MaterialModifierData)`（`UnifiedDetailSourceType.MaterialModifier`） |
