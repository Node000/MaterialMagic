# PE 场景迁移方案与执行记录（PC 场景复制为 PE 之后）

场景现状：`Assets/Scenes/SampleScene_PE.unity` = PC 场景的完整拷贝；旧 PE 场景为 `Assets/Scenes/SampleScene_PE_old.unity`。
结构迁移已于 2026-09-17 在 Unity 中执行完成（下方第 1 节），运行时验证尚未做。

> **状态（2026-09-17）：本方案已全部回滚。** 老的 PE 场景已改回 `Assets/Scenes/SampleScene_PE.unity`（保留原 GUID `8c9cfa26…`），PC 副本场景与原槽位预制体副本已删除，`Assets/Prefabs/UI/MagicSlot_PE.prefab` 恢复为老 PE 预制体（GUID `af459803…`）；Build Settings 重新指向老 PE 场景。
> 下方第 1–5 节仅作历史记录，不再适用；当前 PE 端的实际改动见文末「附：PE 端三项改动（2026-09-17）」。

## 0. 现状核实（已确认）

| 项 | 结果 | 证据 |
| --- | --- | --- |
| PE 场景内容 | 与 PC 场景**字节级一致** | `cmp Assets/Scenes/SampleScene_PC.unity Assets/Scenes/SampleScene_PE.unity` 无差异 |
| 旧 PE 场景 | `SampleScene_PE_old.unity` 保留了原 PE 的 GUID `8c9cfa26abfee488c85f1582747f6a02` | 与 `git show HEAD:Assets/Scenes/SampleScene_PE.unity.meta` 一致 |
| 新 PE 场景 GUID | `769f98edbd4cd31478bce50e2e851021`，meta 为 `DefaultImporter` | `Assets/Scenes/SampleScene_PE.unity.meta` |
| 画布 / 相机 | 旧 PE 与 PC 本来就是同一套：CanvasScaler `1920×1080`、`MatchWidthOrHeight 0.5`；Canvas `PlaneDistance 100`；相机 orthographic size `5` | 两端场景 YAML 对比 |
| 平台分叉面 | 只有槽位预制体相关：3 个 Inspector 引用 + 11 个场景实例 | 见第 1 节 |

## 1. 执行记录（2026-09-17，已在 Unity 中完成）

### 1.1 槽位预制体（按你的决定：PE 保留同名预制体）

| 结果 | GUID |
| --- | --- |
| `Assets/Prefabs/UI/MagicSlot_PE.prefab` → 改名为 `MagicSlot_PE_old.prefab`（保留原 GUID） | `af459803ffa12da43960513a56d9a7b0` |
| `MagicSlot_PC.prefab` 复制为新的 `MagicSlot_PE.prefab`（新 GUID，根对象名改为 `MagicSlot_PE`，含 `SellButton` / `SpringFrame`） | `e1c53e13cd235fe48ab980341dd6496c` |
| `MagicSlot_PC.prefab` 未改动 | `8a9a73d9f734da940a365d246a74394b` |

### 1.2 PE 场景实例换源（11 个，override 全部保留）

- 8 个道具栏槽位 `PlayerArea/MagicBookArea/MagicSlot_PE`、`(1)`…`(7)`：源预制体由 `MagicSlot_PC` 换成 `MagicSlot_PE`，锚点/位置/尺寸/俯角/旋转逐项与 PC 场景一致（`-834.4…834.4`、旋转 `339.91°…20.09°`）。
- 3 个结算槽位 `RewardPanel/RewardMagic0/1/2`：同上，`m_IsActive=0`、位置 `(-160/0/160, -18)` 保留。
- 实现方式：`PrefabUtility.InstantiatePrefab` + 重映射 override target（`TryGetGUIDAndLocalFileIdentifier` 取 local ID，两个预制体为字节级副本所以 ID 一一对应）+ `PrefabUtility.SetPropertyModifications`，逐项校验通过后才删除旧实例。
- 与 PC 场景逐项对比：11 vs 11 个实例、名称一一对应、锚点/尺寸/旋转/激活态**全部一致**；仅 PC 侧两个实例多一条冗余的 `m_LocalScale.x = 1` override（值相同，无影响）。

### 1.3 引用与工程配置

| 项 | 结果 |
| --- | --- |
| `DebugBattleUI/HandSystemUI.magicSlotPrefab` / `magicViewPrefab` | 已回绑到新的 `MagicSlot_PE.prefab` |
| `DebugBattleUI/ShopPanel/ShopPanelUI.magicViewPrefab` | 已回绑到新的 `MagicSlot_PE.prefab` |
| 场景内指向 `MagicSlot_PC.prefab` 的引用 | **0 处**（整场景遍历序列化属性扫描确认） |
| Build Settings | 已加回 `Assets/Scenes/SampleScene_PE.unity`（顺序 `StartScene` → `SampleScene_PC` → `SampleScene_PE`，均启用）；`_old` 场景不在列表 |
| PE 场景健康度 | 缺失脚本 `0`、断引用 `0`；Console `0` error / `0` warn |
| `StartScene/SceneTransitionManager.peGameSceneName` | 仍为 `"SampleScene_PE"`，无需改动 |

## 2. 槽位字号（已按 1.3 倍写入，一处待定）

`MagicSlot_PE.prefab`（仅 PE，PC 预制体未动）已按 **×1.3 取整**调整字号，`m_fontSize` / `m_fontSizeBase` 同步写入，框尺寸未改：

| 文本对象 | 原 → 新字号 | 框尺寸（已解析） | overflow | 实测结论 |
| --- | --- | --- | --- | --- |
| `NameText` | 30 → **39** | 126.9×30 | Truncate | **装不下**：即使 2 字名（“火球” 78.7×39）高度已超 30；“有害电波” 会换成 2 行而被截掉第二行 |
| `Tooltip/NameText` | 22 → **29** | 216×30 | Truncate | 可用；仅混排（如“先驱α”）行高 33.5 略超 30（临界） |
| `Tooltip/DescriptionText` | 17 → **22** | 210×54 | Truncate | 可用（换行后仍在框内） |
| `Tooltip/EffectText` | 17 → **22** | 210×30 | Truncate | 可用 |
| `SellButton/SellText` | 20 → **26** | 120×36 | Overflow | 可用（“卖出 0$” 实测 91 宽；三位数价格仍有余量） |
| `TagTooltip/Text` | 16 → **21** | 148.5×100 | Overflow | 可用 |
| `ModifierTooltip/Text` | 15 → **20** | 206×56 | Overflow | 可用 |

**待定：`NameText` 在 1.3 倍下会被截切**（框高 30 小于行高 39，且 overflow 模式是 Truncate）。三选一，确认后我改：

- `A` 只把 `NameText` 的框改大（126.9×30 → 约 170×42）——属美术布局调整；
- `B` `NameText` 开自动缩放（min≈26 / max 39），短名保持 39，长名自动缩到框内；
- `C` `NameText` 单独保持 30，其余文本维持 1.3 倍。

## 3. 尚未处理 / 需决定

| # | 项 | 说明 |
| --- | --- | --- |
| 1 | `NameText` 字号/框 | 见第 2 节 A/B/C |
| 2 | `PvOnlyScene.unity`、`Assets/Prefabs/UI/Rightside.prefab` | 仍引用 `MagicSlot_PE_old.prefab`（PvOnly 未收录进 Build Settings；`Rightside.prefab` 无任何引用，是孤儿资源）。若要一起换成新槽位，需要单独确认 |
| 3 | `SampleScene_PE_old.unity` 与 `MagicSlot_PE_old.prefab` | 验证通过后删除；`MagicSlot_PE_old.prefab` 目前只被 `_old` 场景 / PvOnly / Rightside 引用 |
| 4 | 移动端适配 | 屏幕方向（现 `defaultScreenOrientation: AutoRotation` 且四方向全开 → 建议锁横屏）、安全区（工程内无 SafeArea 脚本）、触摸命中尺寸、`SpritePhysicsShapeRaycastFilter`（2 处）、`CrayonUIEdge`（6 处）真机性能 |
| 5 | 悬停类交互在移动端的空白 | `TopBarIconTooltipUI` 只实现 PointerEnter/Exit；`ShopPanelUI.BindHoverDetail` 只注册 PointerEnter；`HandSystemUI.ShowPileHover` 在移动端主动禁用。如需补齐要新增长按/点击入口 |
| 6 | 运行时验证 | 仍未做。见第 4 节清单 |

## 4. 验证清单（未做部分）

**Play Mode（Standalone，编辑器）**
- 从 `StartScene` 起跑完整流程：开始 → 地图（含「箭头回收站 / 抓娃娃机」事件格）→ 战斗（出牌、结算、出牌上限）→ 结算三选一 → 商店（购买、刷新、移除箭头、卖出）→ 事件 → 休息（学习道具）→ 教程 13 步
- 槽位相关重点：道具栏 8 槽排布与悬停、卖出按钮、结算槽位显隐、商店购买后的槽位刷新

**移动交互路径**
- 把 `HandSystemUI.simulateMobileInteractionInEditor` 临时设为 `1` 再跑一遍（此时奖励道具 / 商店道具购买会先走 `RewardMagicConfirmPanel`，牌堆 hover 预览被禁用），验证后设回 `0`

**真机（Android + iOS 各一轮）**
- 能进入 PE 场景（Build Settings 已收录）、分辨率与安全区、触摸命中、帧率、描边与粒子开销

## 5. 后续维护约定（避免再次分叉）

- **不要再整份复制场景到 PE**：会覆盖 PE 的移动端适配。PC 改结构时，PE 只补结构/绑定，不动 PE 的布局数值。
- PE 差异清单（当前）：槽位预制体（`MagicSlot_PE.prefab` = PC 内容副本 + 后续 PE 专有调整）、移动端方向/安全区/字号/命中。
- 第二层地图配置通过固定 Resources 路径加载（`HandSystemUI` 的 `Config/SecondFloorPCMapConfig`），两端共用；若 PE 需要不同参数要加平台分支（代码改动，另立项）。

```unity_property
Assets/Scenes/SampleScene_PE.unity/DebugBattleUI#HandSystemUI:magicSlotPrefab
Assets/Scenes/SampleScene_PE.unity/DebugBattleUI#HandSystemUI:magicViewPrefab
Assets/Scenes/SampleScene_PE.unity/DebugBattleUI/ShopPanel#ShopPanelUI:magicViewPrefab
Assets/Scenes/SampleScene_PE.unity/DebugBattleUI#HandSystemUI:simulateMobileInteractionInEditor
```

## 附：PE 端三项改动（2026-09-17，已执行）

### 0. 回滚（先做）

| 项 | 结果 |
| --- | --- |
| 老 PE 场景 | `SampleScene_PE_old.unity` → `SampleScene_PE.unity`（GUID `8c9cfa26abfee488c85f1582747f6a02` 保留），内容与 `HEAD` 一致 |
| PC 副本场景 | 已删除（原 GUID `769f98ed…`） |
| 槽位预制体 | `MagicSlot_PE_old.prefab` → `MagicSlot_PE.prefab`（GUID `af459803…`）；PC 副件 `e1c53e13…` 已删除 |
| Build Settings | `StartScene` → `SampleScene_PC` → `SampleScene_PE`，三条 GUID 与实际一致 |

### 1. 道具触发顺序：改为行从上到下、行内从左到右

代码改动（PC/PE 共享）：`MagicBookVisualOrder.cs`（新）、`MagicMatchOrderUtility.cs`、`HandSystemUI.cs`、`MagicMatchOrderTests.cs`。
详见 `Assets/Docs/道具触发顺序修复.md` 末节（含实现、边界情况与验证数据）。

要点：视觉顺序按道具栏**当前真实布局**计算（弧形单行 = 从左到右；网格 = 行优先），道具栏增减导致的行列变化自动跟随；
拖拽落点同步改为行优先，单行布局保持原手感。

### 2. 商店面板与内容生成迁移（PC → PE）

把 PC 场景的 `DebugBattleUI/ShopPanel` 整棵子树拷入 PE（保留 override 与内部引用）：
`RevealMask/Content/ItemRoot` = `ItemLayer`（3 个 `ShopItemSlot`）+ `ArrowLayer`（6 个 `ShopArrowSlot`）+ `LayerSep0/1`，
另有 `RefreshButton`、`RemoveArrowButton`、`LeaveButton`、`GoldText`、`SpringFrame`×2、`PopupDragonWindowBackground`；旧的 8 个 `ShopItem` 实例已删除。
绑定与引用：

| 项 | 结果 |
| --- | --- |
| `UIManager.shopPanelUI` | 指向新的 `ShopPanel/ShopPanelUI`（旧面板已删除） |
| `ShopPanelUI` 字段 | `itemRoot` / `revealMask` / `contentRoot` / `leaveButton` / `goldText` / `refreshButton` / `removeArrowButton` 均已绑定；三个槽位/分隔线预制体引用已保留 |
| `ShopPanelUI.magicViewPrefab` | `MagicSlot_PC.prefab` → **`MagicSlot_PE.prefab`**（保持 PE 平台绑定） |

### 3. 打出限制 UI 迁移（PC → PE）

把 PC 的 `DebugBattleUI/PlayerArea/spring back`（含子物体 `PlayLimitDisplay`）拷入 PE 的 `PlayerArea`：

| 项 | 结果 |
| --- | --- |
| `PlayLimitDisplayUI.scaleTarget` / `visibilityTarget` | 指向 `spring back` |
| `spring back` 的 `SpringLineHighlightUI` | `hideOnAwake=false`、`bindHoverTarget=false`（与 PC 一致）；`hoverTarget` 已从 PC 场景改指 PE 的 `PlayerArea`（唯一一处跨场景引用） |
| 位置 | `PlayerArea/spring back/PlayLimitDisplay`（PE 原本没有任何打出限制显示，属新增） |

### 4. 验证

- 单元测试：`MagicMatchOrderTests` **15/15 通过**；PE 真实网格下 `ranks=[1,3,5,7,0,2,4,6]`、触发顺序 `[4,0,5,1,6,2,7,3]`。
- PE 场景：缺失脚本 `0`、断引用 `0`、跨场景引用 `0`；Console `0` error（仅剩与本次改动无关的 `MaterialModel` 序列化深度旧警告）。
- 编辑器 Play Mode 冒烟（PE 场景 + 调试面板开商店）：`UIManager.ShopPanel` 解析为新面板；商店打开后 `slotViews=9`、`offers=7`（3 道具 + 4 箭头可见，余下隐藏）；
  打出限制：`ShowForBattle(true)` → 可见并显示 `0/8`，`Refresh(3,7)` → `3/7`。

### 5. 仍待处理

| # | 项 | 说明 |
| --- | --- | --- |
| 1 | 移动端布局 | 迁移过来的商店面板/打出限制按 PC 位置摆放，需美术按 PE 屏幕确认（尺寸与锚点均未改） |
| 2 | 移动端适配 | 屏幕方向（现为 AutoRotation，建议锁横屏）、安全区（工程内无 SafeArea 脚本）、触摸命中尺寸 |
| 3 | 悬停类交互 | `TopBarIconTooltipUI` / `ShopPanelUI.BindHoverDetail` 只有 PointerEnter，移动端无入口；`ShowPileHover` 在移动端主动禁用 |
| 4 | 平台差异清单 | PE 槽位预制体（`MagicSlot_PE`）仍缺 PC 侧的 `SellButton` / `SpringFrame`，卖道具在 PE 不可用 |

## 附 2：教程迁移（PC → PE，2026-09-17 已执行 + 出图核对）

对应 `PE版本同步审阅.md` 的 F5 / F6 / U3。

| 项 | 结果 |
| --- | --- |
| 页数与顺序 | `TutorialRoot/Steps` 下 13 页，新增 `Battle_ArrowBase`/`Battle_PlayLimit`；层次顺序与 PC 一致；流程顺序由共享代码 `TutorialManagerUI.CacheSteps` 决定，两端本就同序 |
| 窗口外观 | 13 页的窗口子对象由 `PopupDragonWindowBlank` 实例换成 `TutorialStepWindow` 实例（含 `Title` + `Body` 30 号自动缩放）；旧实例已删 |
| 高亮框 | 按 PC 规则补 `Cutout`/`Cutout2`/`Cutout3`（含 PC 里故意停用的占位框），共 24 个；按段换洞与 PC 一致（`ArrowBase`/`MagicBook`/`PlayLimit`/`Refresh`/`EventOptions` 各 3 段） |
| 输入拦截 | 新建 `TutorialRoot/InputBlocker`（`TutorialInputBlockerUI`，全屏拉伸、`raycastTarget=true`、默认停用、排在 `Overlay`/`Steps` 之前）；`TutorialManagerUI.inputBlocker` 已接线 |

### PE 实测区域（画布 1920×1080，原点画布中心，y 向上）

手牌区 `-351..355/-598..-463`、出牌区 `-323..327/-346..-265`、出牌上限 `-86..90/-258..-212`、出手 `514..653/-497..-297`、换牌 `452..588/-516..-308`、道具栏 `544..793/-321..172`、顶栏进度 `454..631/119..217`、玩家信息（生命/Buff/金币，**在左下**）`-973..-372/-544..-403`、敌人区含意图与名字血条 `-160..130/-140..340`、地图弹窗 `-346..346/-173..485`、事件选项列 `25..457/-6..306`、商店商品区 `-435..452/113..301`、结算奖励槽行 `-264..264/122..221`。

### 窗口停靠位

- 战斗页（7 页）统一左上 `(-620, 330)`：PE 正中上方是敌人区，实机意图图标到 `y 251..331`，PC 的 `P_top (0,+250)` 会压住意图。
- 弹窗类（地图 3 页 + 结算 + 商店）在下方 `(0, -290)`；`Event_Options` 在 `(0, -150)`（夹在事件弹窗下沿 -6 与出牌区上沿 -265 之间）。

### 验证

Play Mode 下逐页逐段打印「窗口矩形 vs 高亮洞」：13 页共 22 段均落在预期区域且与窗口不相交；出图拼版 `Library/Locus/tmp/pe_tut_battle_atlas.png`、`pe_tut_panels_atlas.png`、`pe_tut_map_atlas.png`（单页 `Library/Locus/tmp/pe_shot_*.png`）。直接进战斗场景为 debug 局（`directSampleDebugRun=true`），不写普通存档。

### 仍待处理

| # | 项 | 说明 |
| --- | --- | --- |
| 1 | 教程文案方位 | 本地化表 PC/PE 共用，但文案描述的是 PC 排布：`tutorial.battle.info.body`“左上角”、`tutorial.battle.refresh.body` 第 3 段“左下角”、`tutorial.battle.play_limit.body` 第 1 段“出牌区与道具栏之间”。要为 PE 单独出文案需新 key 或分平台表，未擅自改共享表 |
| 2 | `PvOnlyScene` | 仍是旧的 11 页结构（本次只迁移 PE） |
| 3 | 边界分辨率 | 高亮洞仍是固定矩形（1920×1080 口径），非 1080p Game view 下倾斜 HUD 的边缘对象仍会偏 |
