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
- 附魔的**商店价格不在这张表里**：由每个附魔自己 SO 上的 `MaterialModifierDefinition.price`（相对普通箭头 2 的差值）决定，见 `Assets/Docs/箭头附魔价格配置.md`。
- **选择附魔界面**（`MagicModifierSelectionPanelUI` 的「箭头附魔」模式）
  - 选项 = **图标在上 + 名字在下**（名字取附魔本地化 `nameKey`），与「道具强化」模式、以及战斗结算奖励的选项同一形式。
  - 布局参考战斗结算奖励：**选项 168×92、间距 230（= 场景里美术摆的格子）**（`MagicModifierSelectionPanelUI` 里的 `OptionWidth/OptionHeight/OptionSpacing`）；附魔图标 64@(0,+14)，名字框 146×28@(0,−31)；道具强化模式图标 51@(0,+14)。
  - 名字画在选项自带的 `Text` 上（字号沿用场景里的 17；战斗结算奖励用的是 22，要改就改 `Assets/Scenes/SampleScene_*` 里 `ModifierOption*/Text` 的 Font Size）。
  - 附魔的说明仍由统一详情面板 `UnifiedDetailPopup` 展示：悬停（进入选项）或按下时弹出。
  - 面板标题与操作提示文本保留（那是流程说明，不是附魔信息）。
- **道具强化模式**（同一面板的另一个模式）保持原样：图标 + 文本；两个模式互相切换时会互相隐藏对方的内容。
- 面板上的图标预制体引用为序列化绑定（`MagicModifierSelectionPanelUI.enchantIconPrefab`），三个场景（`SampleScene_PC` / `SampleScene_PE` / `PvOnlyScene`）均已绑定。

## 2.1 统一详情面板里的附魔显示

- **箭头详情**：附魔图标画在右上角额外框的左上角（见 2.2）；详情面板**不再有**左下角的附魔图标行。
  - 旧实现（已停用）：`EnchantEntry.prefab` + `EnchantEntryUI.cs` + 场景里的 `EnchantEntryRoot` 容器（两个场景的容器已删）；`UnifiedDetailContent.EnchantIds` 也已移除。
- **附魔自身的详情**（悬停附魔选项/附魔奖励等）：在面板左上角（原图片图标槽）显示附魔图标——`UnifiedDetailContent.EnchantId` → `UnifiedDetailPopupUI/Content/EnchantIconRoot` 容器（110×110，对齐 `Content/Icon` 中心，美术可改），非附魔内容自动隐藏。
- **箭头序列框内的图案位置**（`ArrowSequenceFrame`）：框本身位置不动（`anchoredPosition` = (0,−120)，已还原），只把**框内的序列整体下移**——图标槽位改成正方形 78×78（`UnifiedDetailArrowSequenceUI.iconSize`，原来 78×96 会在图案上下各留 9 空白），行布局 `HorizontalLayoutGroup` 对齐改为 `LowerLeft`、内边距改为 (l:10, r:10, **t:30, b:4**)，图案底边距框底从 17 变为 **4**（两个场景均已改；想再贴边/抬高就改这个 `padding.bottom`）。
- **视觉效果**：图标自动套用该附魔的视觉材质（`Assets/Resources/Materials/MaterialModifiers/<Script>.mat`，Shader 家族 `UI/MaterialModifiers/*`，与箭头卡上同一份）。做法是「一个组件管两层」：`EnchantIconUI` 在图标根物体上按附魔建一份材质实例、两层图片共用（`_MainTex` 是 `[PerRendererData]`，由各自 Sprite 传入），每层颜色仍取 `Enchant_Color.asset`（Shader 乘 vertexColor），所以颜色配置照旧生效，效果参数/动画与卡上一致。
  - **预制体根上有一个 `Canvas`（overrideSorting=false）**：这套效果 Shader 会按噪声抠掉图标主体透明度再叠火焰粒子，合成结果对**渲染上下文**敏感——同一个图标在**根 Canvas** 里会偏橙、在**嵌套 Canvas**（详情面板就是带 `overrideSorting` 的嵌套 Canvas）里会偏红，看起来就像“颜色不一致”。给图标预制体加一个 `overrideSorting=false` 的嵌套 Canvas 后，所有位置的图标都走同一种上下文，实测选项图标与详情框图标颜色一致（改前 109,70,51 vs 94,43,33；改后 102,48,38 vs 94,43,33）。**不要删这个 Canvas**，也不要改成 `overrideSorting=true`（会改变图标在 UI 里的叠放层次）。
  - 开关：`Assets/Prefabs/UI/EnchantIcon.prefab` 上 `EnchantIconUI.applyVisualEffect`（默认开）；没配视觉材质的附魔（如「禁用」）保持原样。
  - 随机箭头类附魔（`_RandomLocked` / `_RandomIndex` / `_RandomPhaseOffset`）默认按材质自身参数（循环未定格）；Half/Fragile 用材质默认切线角。需要让图标跟当前箭头方向一致时再传参（目前未传）。

## 2.2 附加框内的图标与标题（附魔 / 道具强化）

- **位置**：图标在**附加框内、标题左侧**（不再是压在框角上）。容器 `Assets/Prefabs/UI/AddedDetailed.prefab` 里的 `EnchantIconRoot`：**容器尺寸 = 图标尺寸（50×50），容器位置 = 图标中心**（锚点左上、pivot 居中，现为 (34,−38)）；美术直接改。
- **两类图标互斥**（同一个容器）：
  - 附魔：两层图标预制体 `EnchantIcon.prefab`（`EnchantIconUI`，颜色 + 视觉效果），由 `UnifiedDetailAddedDetail.EnchantId` 驱动。
  - 道具强化等：容器内的单层 `SpriteIcon` 槽（`Image`，preserveAspect），由 `UnifiedDetailAddedDetail.Icon` 驱动（`BuildMagicAddedDetails` 填 `MagicModifierIconDatabase.Get(magic.PrimaryModifier)`）。
- **标题字号**：有图标时放大到 `iconTitleFontSize`（默认 **44**，无图标时是预制体原值 30），位置右移到「图标右边缘 + `iconTitleGap`（默认 **16**，约一个空格）」；标题变高后正文整块下移同样量，并在必要时再让一次以保证正文顶边在图标下方 4 单位（`EnsureBodyBelowIcon`）。没有图标时全部还原成美术摆的值（都在运行时算）。
- 实测（PC 与 PE 一致）：标题 44、标题左=75、图标右=59 → 间隔 **16**；图标底 −63、正文顶 −67 → 正文在图标下方 **4**。
- 调参：图标大小/位置改容器 `EnchantIconRoot`；标题字号 `iconTitleFontSize`；间隔 `iconTitleGap`（都在 `AddedDetailed.prefab` 的 `AddedDetailedUI` 上）。

## 3. 相关代码

| 功能 | 位置 |
| --- | --- |
| Boss 确定与图标路径 | `RunManager.ResolveChapterBoss` / `ResolveChapterBossGroupIndex` / `ResolveChapterBossMapIconPath` |
| 地图生成时调用 | `HandSystemUI.BuildChapterMapGrid`（生成地图前调用 `ResolveChapterBoss`） |
| Boss 战取用同一份结果 | `HandSystemUI.ResolveChapterBossBattleLevel` / `SelectLevelEnemyGroup` |
| Boss 格图标绘制 | `ChapterGridPanelUI.ResolveBossMapIcon` / `ApplyCellVisual` |
| 附魔图标组件 | `Assets/Scripts/EnchantIconUI.cs` |
| 详情面板附魔内容项（已停用，保留备用） | `Assets/Scripts/EnchantEntryUI.cs`、`Assets/Prefabs/UI/EnchantEntry.prefab` |
| 额外框附魔图标（图标 + 标题让位） | `Assets/Scripts/AddedDetailedUI.cs`、`Assets/Prefabs/UI/AddedDetailed.prefab` |
| 附魔颜色配置 | `Assets/Scripts/Data/EnchantColorConfig.cs` |
| 附魔颜色表美术向 Inspector（中文名表头 + 补齐/刷新/校验按钮） | `Assets/Editor/EnchantColorConfigEditor.cs` |
| 附魔详情内容 | `UnifiedDetailContentBuilder.Build(MaterialModifierData)`（`UnifiedDetailSourceType.MaterialModifier`） |
