---
id: kd_builtin_memory_user_preference
type: memory
path: user-preference.md
title: user-preference
injectMode: rule
summaryEnabled: false
commandEnabled: false
readOnly: false
aiMaintained: true
explicitMaintenanceRules: true
createdAt: 1779518876486
updatedAt: 1779791237718
---

# user-preference

<!-- locus:maintain-rules:start -->
- Record only long-term user preferences that stay stable across tasks
- Prioritize language, reporting style, code style, taboos, and explicit requirements
- Keep each entry short and limited to stable preferences or hard constraints
- Keep the list within 20 items and merge similar preferences
- Remove one-off arrangements, temporary phrasing, and unconfirmed inferences
<!-- locus:maintain-rules:end -->

<!-- locus:body:start -->
- 用户偏好：面板、容器、布局应优先直接搭建在 Scene 内；运行时只动态生成容器中的内容项；内容项应做成 Prefab，并由单独 UI 控件脚本刷新显示。
- 用户偏好：选关、地图路线、信息悬浮框等 UI 不应动态生成；除非用户明确要求使用预制体（如卡牌、敌人、法术卡牌），否则 UI 应在 Scene 中直接搭建好，代码只负责修改和更换已有内容。
- 用户偏好：每个具体敌人脚本都应单独保存为一个 `XXEnemyModel.cs` 文件，放在 `Assets/Scripts/Enemies/` 下，不要集中写在 `EnemyModel.cs` 或其他集合文件中。
<!-- locus:body:end -->
