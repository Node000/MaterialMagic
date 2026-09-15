---
id: kd_builtin_memory_user_preference
injectMode: rule
aiEditMode: auto
maintenanceRules: |-
  - Record only long-term user preferences that stay stable across tasks
  - Prioritize language, reporting style, code style, taboos, and explicit requirements
  - Keep each entry short and limited to stable preferences or hard constraints
  - Keep the list within 20 items and merge similar preferences
  - Remove one-off arrangements, temporary phrasing, and unconfirmed inferences
---

- 用户偏好：面板、容器、布局应优先直接搭建在 Scene 内；运行时只动态生成容器中的内容项；内容项应做成 Prefab，并由单独 UI 控件脚本刷新显示。
- 用户偏好：选关、地图路线、信息悬浮框等 UI 不应动态生成；除非用户明确要求使用预制体（如卡牌、敌人、法术卡牌），否则 UI 应在 Scene 中直接搭建好，代码只负责修改和更换已有内容。
- 用户偏好：每个具体敌人脚本都应单独保存为一个 `XXEnemyModel.cs` 文件，放在 `Assets/Scripts/Enemies/` 下，不要集中写在 `EnemyModel.cs` 或其他集合文件中。
- 用户偏好：美术改过的场景/预制体数值一律不要动；确需修改时必须先把方案提交用户审阅。
- 用户偏好：美术统一给出的按钮样式与文案（例如改用“X”图标）不要在运行时写回文字，代码只负责点击行为；功能语义靠逻辑而非文字承担。
- 用户偏好：与美术资源的对接尽量用 Inspector 序列化绑定，不要按路径/名称查找，避免后续重名时选错对象。
- 用户偏好：审阅/说明类文档放到 `Assets/Docs/` 下（如 `Assets/Docs/事件本地化文本审阅.md`），不要散放在 `Resources/Data`；本地化文本调整以该文档为准。
