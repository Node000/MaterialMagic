# PE 版本同步审阅（PC 相对 PE）

审阅对象：`Assets/Scenes/SampleScene_PE.unity`（移动端 PE）
对比对象：`Assets/Scenes/SampleScene_PC.unity`（PC，当前开发主线）
生成时间：2026-09-17　方式：静态比对（git + 场景 YAML + 代码引用），**未做 PE 运行验证**

> 状态更新（2026-09-17）：「PE 改为 PC 场景拷贝」的迁移已**全部回滚** —— PE 恢复为老 PE 场景（`SampleScene_PE_old` 改名回 `SampleScene_PE`，GUID 不变），PC 副本场景与副件预制体已删除。
> 因此本文第一、二节的差异仍然存在（只是选择逐个迁移，而不是整场景拷贝），第三节 3.2 也依然成立。
> 已实际迁移到 PE 的三项（道具触发顺序行优先、商店面板与内容生成、打出限制 UI）见 `Assets/Docs/PE场景迁移方案.md` 末节。
> 状态更新（2026-09-17，教程迁移完成）：**F5、F6、U3 已同步到 PE** —— `TutorialRoot/InputBlocker` 已新建并接入（不再靠运行时兜底）、补上 `Battle_ArrowBase`/`Battle_PlayLimit` 两步（PE 现 13 步）、13 页窗口由 `PopupDragonWindowBlank` 实例换成 `TutorialStepWindow` 实例（带 `Title`，Body 30 号自动缩放），并补齐每段 `Cutout`/`Cutout2`/`Cutout3`。窗口与高亮框**未照搬 PC 坐标**，按 PE 实测（道具栏在右、玩家信息在左下、敌人居中上方）重排；逐页逐段数值核对 + Play Mode 出图核对已完成。实现记录见 `Locus/knowledge/plan/教程重做方案.md` §25；仍待决：本地化文案描述的是 PC 方位（如 info 的“左上角”、refresh 的“左下角”）、\`PvOnlyScene\` 未同步。

## 前置信息

- PE 基线：`fbfe873e`（2026-09-15「教程流程精简、手牌悬停音效与本地化/场景更新」），是 `SampleScene_PE.unity` 最后一次改动。之后 PC 侧有 6 个提交 / 43 个文件，PE 场景未再改动。
- 如需固化基线：`git tag pe-baseline-2026-09-15 fbfe873e`（尚未执行，待确认）。
- 基线之后的更新基本是两端共享内容，PE 自动生效，无需同步动作：本地化 JSON、数值与关卡 JSON、共享脚本逻辑（明细与流程层面的改动见第三节）。
- PE 侧独立资产只有两个：`Assets/Prefabs/UI/MagicSlot_PE.prefab`（最后改动 2026-08-03）、`Assets/Scenes/SampleScene_PE.unity`。
- 两端场景内本地引用**零断链**；TMP `LiberationSans SDF - Fallback.asset` 字形被反复清空属噪音（动态图集运行时会补字形）。

## 一、功能变更（PC 有、PE 无）

| # | 变更 | PC 现状 | PE 现状 | 影响 | 决定 |
| --- | --- | --- | --- | --- | --- |
| F1 | 商店购买流程重构（分层槽位 / 刷新 / 移除箭头） | `ItemRoot` 下有 `ItemLayer`（3 槽）+ `ArrowLayer`（6 槽），槽位挂 `ShopSlotView`；`shopItemSlotPrefab` / `shopArrowSlotPrefab` / `refreshButton` / `removeArrowButton` 均已绑定 | 只有 8 个旧 `ShopItem.prefab` 实例；上述 4 个引用均为 `None` | 代码按名字找行并收集 `ShopSlotView` → **PE 商店不显示任何商品**；刷新、移除箭头也不可用；`removeArrowButton` 在 `ShopPanelUI.BeginRemoveArrowPurchase` 未判空 | ☐ 同步　☐ 暂缓 |
| F2 | 道具栏卖出道具 | `MagicSlot_PC.prefab` 有 `SellButton` + `SellText`，`MagicItemView.sellButton` 已绑定 | `MagicSlot_PE.prefab` 无 `SellButton`（2026-08-03 后未更新） | PE 无法卖出道具；商店文案“道具栏已满时需先卖出道具”在 PE 无法闭环 | ☐ 同步　☐ 暂缓 |
| F3 | 结算奖励三选一 | `RewardPanel/ChoiceArea` = `ChoiceGold` / `ChoiceItem` / `ChoiceArrow`；`rewardItemCardPrefab` 已绑定 | 只有旧 `OptionArea/RewardOption0..2`；`rewardItemCardPrefab` / `magicChoicePanel` / `magicChoiceBackButton` / `arrowChoiceLabel` 均为 `None` | PE 没有三选一结构，走代码运行时兜底建面板（样式由代码生成，与美术面板不一致） | ☐ 同步　☐ 暂缓 |
| F4 | 堆栈悬停详情 | `PileHoverPanel`（`PileHoverPanelUI`）+ `UIManager.pileHoverPanelUI` 绑定 | 无该节点，字段为 `None` | PE 无抽牌/弃牌/消耗堆悬停详情（不报错） | ☐ 同步　☐ 暂缓 |
| F5 | 教程全屏输入拦截 | `TutorialRoot/InputBlocker`（`TutorialInputBlockerUI`，`visualConfig` 已配） | 无 | `TutorialManagerUI` 运行时兜底创建遮罩（可用，样式来自代码配置而非美术面板） | ☐ 同步　☐ 暂缓 |
| F6 | 教程新增两个步骤 | 步骤 `Battle_ArrowBase`、`Battle_PlayLimit`（各含 `Cutout2` / `Cutout3`）；共 13 步 | 共 11 步，无这两步 | 这两条引导在 PE 不出现 | ☐ 同步　☐ 暂缓 |
| F7 | 详情弹窗箭头序列图 | `ArrowSequenceFrame` + `UnifiedDetailArrowSequenceUI` | 无，`arrowSequenceUI` 为 `None` | PE 不显示箭头序列图 | ☐ 同步　☐ 暂缓 |
| F8 | 出牌次数与底框联动 | `PlayLimitDisplayUI.scaleTarget` / `visibilityTarget` 指向 `spring back` | 两字段为 `None` | PE 开合动画只作用在文字自身（现状可用，无底框联动） | ☐ 同步　☐ 暂缓 |
| F9 | 按图形命中 | `SpritePhysicsShapeRaycastFilter`（`EndTurnButton/Image (1)`、`RefreshButton/Image`） | 无 | PE 用矩形命中，按钮透明区域误点范围更大 | ☐ 同步　☐ 暂缓 |
| F10 | 调试面板功能 | `GoldButton` / `KillAllButton` / `KillTargetButton` / `RandomEnchantButton` / `StartRestButton` | 无这 5 个按钮（`DamageButton` / `DrawCardButton` 靠名字兜底可用） | 仅开发用；PE 上这几个调试功能不可用（不报错） | ☐ 同步　☐ 暂缓 |
| F11 | PE 未收录进 Build Settings | — | 仅 `StartScene` + `SampleScene_PC`（2026-08-04 曾收录 PE，2026-09-05 被移除） | 移动端加载 `SampleScene_PE` 会失败（`SceneTransitionManager` 记录错误并退出） | ☐ 同步　☐ 暂缓 |

## 二、UI 变更（PC 有、PE 无或不同）

| # | 变更 | PC 现状 | PE 现状 | 影响 | 决定 |
| --- | --- | --- | --- | --- | --- |
| U1 | 商店面板结构 | `ItemRoot`：`ItemLayer` / `ArrowLayer` + `LayerSep0` / `LayerSep1` 分隔线 + `RefreshButton` / `RemoveArrowButton` + `SpringFrame` ×2 | 8 个旧 `ShopItem` 实例，无分隔线/按钮/`SpringFrame` | 与 F1 配套；分层位置与槽位尺寸需按 PE 屏比重排 | ☐ 同步　☐ 暂缓 |
| U2 | 结算面板布局 | `RewardPanel/ChoiceArea` 布局 + `RewardItemCard.prefab` 卡位 | 旧 `OptionArea/RewardOption0..2` | 与 F3 配套 | ☐ 同步　☐ 暂缓 |
| U3 | 教程步骤窗口外观 | 13 个步骤用 `Assets/Prefabs/UI/TutorialStepWindow.prefab` 实例 | 步骤窗口仍是 `PopupDragonWindowBlank` 实例 | 教程窗口外观与 PC 不一致 | ☐ 同步　☐ 暂缓 |
| U4 | 两个窗口重构 | `SelectionShowPanel` / `AscensionDetailPanel` 改用 `PopupDragonWindowBlank` 预制体实例，面板根新增 `CloseButton` / `TitleText` | 仍是手工 `PopupDragonWindowBackground` + `Frame/TitleBar/CloseButton` | 功能可用，仅视觉不统一；注意美术已停用 `PopupDragonWindowBlank` 的 `Frame/TitleBar` 按钮 | ☐ 同步　☐ 暂缓 |
| U5 | 详情弹窗位置 | `PlayerArea/Rightside/UnifiedDetailPopup` | `PlayerArea/leftside/UnifiedDetailPopup` | 位置不同，PE 位置由美术定 | ☐ 同步　☐ 暂缓 |
| U6 | 层级重排 | `HandArea` / `Panel` / `PlayArea` 收进 `PlayerArea/middle`；`SpellParticleCaster` 移到 `PlayerCastAnimator` 下 | 仍在 `PlayerArea` 直下 | 代码按名字递归查找，两端都能用；属结构/层次偏好 | ☐ 同步　☐ 暂缓 |
| U7 | 堆栈图标位置 | `Rightside/BattleActionBar/DrawPileIcon`（`Discard` / `Exhaust` 同） | `CombatPileSummaryButton/DrawPileIcon` 等 | `ResolvePileIcon` 兼容两端；位置与尺寸需美术确认 | ☐ 同步　☐ 暂缓 |
| U8 | 蜡笔描边表现 | `CrayonUIEdge` 挂在玩家立绘、回合结束按钮、刷新按钮、三个堆栈图标 | 无 | 是否在 PE 使用描边由美术定（有性能开销） | ☐ 同步　☐ 暂缓 |

## 三、流程变更

### 3.1 已随共享数据/脚本进入两端（PE 自动生效，无同步动作）

| 流程 | 变更 | 证据 |
| --- | --- | --- |
| 地图节点类型 | 删除「删箭头」(`level_arrow_remove`, numericId 501, levelType 6) 与「加箭头」(`level_arrow_add`, 502, levelType 7) 两种专用格；改为两个事件格 `level_event_arrow_recycle_station`(108)、`level_event_arrow_claw_machine`(109) | `Assets/Resources/Data/LevelData.json`、`EventData.json`（事件文本与 zh-CN/en-US 本地化齐备） |
| 章节权重与关卡池 | `eventMapLevelWeight` 4 → 8、`removeMapLevelWeight` 2 → 0、`addMapLevelWeight` 2 → 0；章节关卡池 113/114/115 → 108/109 | `Assets/Resources/Data/ChapterData.json` |
| 地图生成规则 | 事件格权重 5 → 7，删除 levelType 6/7 的生成规则 | `Assets/Resources/Data/MapGenConfig.json` |
| 商店经济 | 商品价格 2 → 3（个别 4 → 3）；附魔价格新增步进 `materialEnchantPriceStep: 1` | `ShopProductPoolData.json`、`EconomyConfig.json` |
| 进阶 A6 | 由「奖励道具三选一数量 ±1」改为「每回合出牌次数 ±1」（效果 type 31） | `AscensionData.json` + `DifficultyUpgradeData.json` 新增 `play_limit_minus_1_a6` / `play_limit_plus_1_a6` |
| 休息流程 | 休息选项变为 默认休息 / 学习道具 / 附魔箭头（新增 `rest.option.study`，resultId 301） | `Assets/Scripts/HandSystemUI.cs` |
| 战斗结算顺序 | 新增 `Assets/Scripts/MagicMatchOrderUtility.cs`（配套 `Assets/Tests/Editor/MagicMatchOrderTests.cs`），修正道具触发顺序 | `Assets/Docs/道具触发顺序修复.md` |

### 3.2 PC 相对 PE 仍缺的流程环节（场景没跟上 → 流程走不通或走代码兜底）

| 流程 | 对应项 | 影响 |
| --- | --- | --- |
| 商店购买（含刷新、移除箭头） | F1 / U1 | PE 商店无商品，购买流程无法开始 |
| 道具栏卖出（满栏腾位） | F2 | PE 无卖出入口，满栏后买不进新道具 |
| 战斗结算奖励三选一 | F3 / U2 | PE 走 `RewardPanelUI` 的代码兜底面板，非美术面板 |
| 教程引导流程 | F5 / F6 / U3 | PE 缺输入遮罩（运行时兜底创建）与 2 个步骤（PE 11 步 / PC 13 步） |
| 进入 PE 的入口 | F11 | PE 不在 Build Settings，移动端流程无法启动 |

## 四、平台差异（建议保留，不纳入同步）

- `HandSystemUI.magicSlotPrefab`：PC 绑 `MagicSlot_PC.prefab`、PE 绑 `MagicSlot_PE.prefab`（有意分平台）。若 F2 决定同步卖出功能，需先定“给 PE 预制体加 `SellButton`”还是“PE 改用 PC 预制体”。
- PC/PE 画布尺寸、转场聚焦圈大小等按平台独立配置（`StartScene` 的 `SceneTransitionManager`），不要统一。
- 移动交互分支：`HandSystemUI.ShouldUseMobileInteraction()`（`Application.isMobilePlatform`，编辑器下由 `simulateMobileInteractionInEditor` 控制，两端当前都是 0）。移动端下奖励道具/商店道具购买会先弹 `RewardMagicConfirmPanel` 确认；PC 直接执行。

## 五、后续两项需求的预留

1. **购买确认面板**：机制已存在——`HandSystemUI.ShowRewardMagicConfirmPanel` / `ShowShopMagicConfirmPanel` 走 `RewardMagicConfirmPanel`（两端场景都有该节点），目前仅移动交互路径启用；**箭头购买没有确认**。建议先不动这块，等统一方案定了再一起做，避免做两遍。
2. **字体放大一倍**：工程内没有全局字号缩放（`UnifiedDetailTextConfig` 只负责详情框文案与颜色），字号来自各 TMP 组件在 Scene/Prefab 上的值，因此是逐场景/逐预制体的调整，且 PE 与 PC 屏比不同、无法共用数值；建议与第一、二节的 PE 同步工作合并进行。

## 六、核对方式（结论出处）

- PE 最后改动提交：`git log --oneline -- Assets/Scenes/SampleScene_PE.unity`
- 基线之后文件清单：`git diff --stat fbfe873e..HEAD`
- 场景结构差异：解析两端场景的 GameObject/Transform/PrefabInstance 层级并按路径求差（PC 独有 49 处 / PE 独有 40 处顶层差异，共享路径组件差异 6 处）
- 绑定缺失：扫描两端场景中项目脚本的 `{fileID: 0}` 引用字段并按脚本名求差（PE 少绑 18 个字段）
- 断链检查：两端场景内所有本地 `fileID` 引用都能解析，无断引用；缺失 GUID 均为 UGUI/TMP 内置包脚本，属误报
- 运行时验证**尚未做**。PE 同步完成后建议：打开 `Assets/Scenes/SampleScene_PE.unity` 检查无缺失组件 → 开 `simulateMobileInteractionInEditor` 跑商店/道具栏/结算/教程 → Android 或 iOS 真机各跑一轮

## 七、2026-09-18 追加：三个选择面板 PE 与 PC 完全对齐

- 对比口径：按路径快照三个面板子树的 `sizeDelta / anchoredPosition / localScale / anchor / pivot / 组件 / 预制体来源`，逐节点求差。
- 对齐结果：**差异 0 个节点**（`DebugBattleUI/RewardPanel`、`DebugBattleUI/RewardMagicChoicePanel`、`DebugBattleUI/MagicModifierSelectionPanel`，PC 141 节点 / PE 141 节点）。
- 为对齐所做的改动（都在 PE 场景）：
  1. 结算奖励槽 `RewardPanel/RewardMagic0..2` 由 `MagicSlot_PE.prefab` 换成 `MagicSlot_PC.prefab`（名称、父级、位置、尺寸不变；`RewardPanelUI.CacheReferences` 按名字/组件收集，无需重绑）。
  2. 补上 `RewardPanel/PopupDragonWindowBackground/Frame/TitleBar/TitleText`（PC 是场景新增节点：文本 `C:/BattleReward`、字号 22、白色、拉伸锚点、rect (-122,0)、pos (16,0)）。
- 道具强化 / 箭头附魔选择面板（`MagicModifierSelectionPanel`）两端本来就逐节点一致，无需改动；选项布局由代码统一（168×92、间距 230、图标在上、名字在下）。
- 未纳入本次对齐：`MagicSlot_PE.prefab` 仍用于手牌区道具栏（分平台差异，未动）。

## 八、2026-09-18 追加：Debug 战斗面板 PE 完全采用 PC 版

- 对比口径：按路径快照 `DebugBattleUI/DebugPanel` 整棵子树（尺寸/位置/缩放/锚点/组件/文本），逐节点求差。
- 对齐结果：**差异 0 个节点**（PC 78 / PE 78）。
- 做法（PE 场景）：把 PC 的 `DebugPanel` 整棵复制过来替换旧面板（临时预制体 → PE 实例化 → 完全解包 → 删除临时预制体），**名称与路径保持不变**（`DebugBattleUI/DebugPanel`，父级、兄弟序号一致），所以任何按名字/类型找面板的逻辑都不受影响。
- 补齐的功能（PE 原来缺 5 个按钮 + 弹性滑框）：
  1. `KillTargetButton`（秒杀目标敌人）、`KillAllButton`（击杀所有敌人）、`GoldButton`（获得10金币）
  2. `EnchantRewardButton`（获得1次附魔奖励 → 选附魔 → 箭头选择面板）
  3. `MagicModifierRewardButton`（获得1次道具强化 → 选择道具强化面板）
  4. 面板内容改为 `ScrollView / Viewport(RectMask2D) / Content(VerticalLayoutGroup + ContentSizeFitter)` 弹性滑框（内容 984 / 视口 640，可滚动）
- 复制后重接的外部引用：`DebugBattlePanelUI.handSystem → DebugBattleUI`（存预制体时对场景对象的引用会被清空，已在 PE 里补回）；其余引用都在面板内部，逐字段对比 PC 与 PE 完全一致。
- 运行时验证（PE Play）：面板 21 个控件齐全，滑框可滚；点「获得1次附魔奖励」弹出「选择箭头附魔」、点「获得1次道具强化」弹出「选择道具强化」，均正常。
