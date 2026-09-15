---
id: kd_builtin_memory_project_mistake_note
injectMode: full
aiEditMode: auto
maintenanceRules: |-
  - Record only verified problems, rework causes, and avoidance steps
  - Prioritize recurring pitfalls, constraints, regression points, and confirmed fixes
  - Keep each entry short and focused on one lesson or constraint
  - Keep the list within 20 items and merge duplicates regularly
  - Remove outdated issues, non-reproducible issues, and unsupported guesses
---

- 此前误提交的 `Packages/com.farlocus.locus` 插件是残缺编译快照（`CaptureViewportRequest`/`MonoMod` 缺失、`unsafe` 未开、依赖缺失）。本机仍保留可编译的完整插件版本；现在该插件已从 Git 及 `Packages/packages-lock.json` 移除；`.gitignore` 仍排除本地插件目录，当前环境可继续保留本地文件而不会进入仓库。
- UI 按钮「点了没反应」不能只用 `Button.onClick.Invoke()` 验收：射线被上层 Graphic 挡住时 Invoke 仍会成功。验收交互必须用 `EventSystem.RaycastAll` + `ExecuteEvents` 走真实射线路径。实例：`StartScene` 论坛面板标题栏关闭按钮被美术新增的 `ForumPanel/PopupDragonWindow2/Frame/Content/Title (1)`（TMP 文字、`raycastTarget=true`）吞掉点击，把该文字 `raycastTarget` 置为 false 后恢复。同类隐患：`SettingsPanel/Title`、`HistoryPanel/Title` 也是 `raycastTarget=true`，纯文字标签应统一设为 false。
- 横向拉伸锚点（`anchorMin.x != anchorMax.x`）的 RectTransform 上写 `sizeDelta.x`，实际宽度 = 父级宽度 + sizeDelta，而不是 sizeDelta。实例：`PileHoverPanel/RowContainer/PileRow/Content` 是拉伸锚点，`BattleMaterialRowUI.Refresh` 设 `sizeDelta.x = 800` 后实际宽 892+800=1692，而箭头行以 Content 左边缘为 x=0 排布，导致 Hover 面板里箭头整行偏左溢出（面板内不居中）；`BattleMaterialRowUI` 里检测到拉伸锚点就收敛为居中固定锚点后恢复。该类容器在设置尺寸前先确认锚点语义。
- 保存场景会把脚本里已改名/删除的序列化字段“按新名 + 代码默认值”重写，旧名下的调参值直接消失（运行时其实早已读不到旧名值）。实例：`SampleScene_PC` 中 `HandSystemUI` 仍存着 `resolveMotionSpeedMultiplier 2 / castProjectileSpeedMultiplier 1.5 / comboResolveSpeedStep 0.1`，而脚本已改成 `resolveSpeedMultiplier / comboSpeedStartLayer / comboSpeedStep`；一次例行存盘把这些行改写为 1.1 / 5 / 0.05，导致美术/手感调过的数值在 YAML 里无法找回。改名字段时要同步加 `[FormerlySerializedAs]` 或先把旧值从场景里抄出来，才能安全保存场景。

