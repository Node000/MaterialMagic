---
id: kd_20091c4d-c3e2-480c-b034-17b7d025fd4b
injectMode: inherit
injectAgents:
- '*'
aiEditMode: inherit
skillEnabled: true
skillSurface: command
---

# 项目 Skill 索引

这些是本项目的执行方法，按任务加载；不是额外需求、权限来源或默认全套检查。事实入口在 `Assets/Docs/Workflow/PROJECT.md` 与 `ARCHITECTURE.md`。不自动安装用户级 Skill。

| 任务 | 先读 | 再按需读取 |
| --- | --- | --- |
| 新增/修改敌人、道具、事件、附魔 | [内容接入](content-integration/SKILL.md) | [敌人](content-integration/enemies.md)、[道具](content-integration/magics.md)、[事件](content-integration/events.md)、[箭头附魔](content-integration/enchantments.md) |
| Scene/Prefab UI、按钮、详情、多语言、PC/PE接线 | [场景UI接入](scene-ui-integration/SKILL.md) | ui-localization模块知识与具体资产 |
| 伤害/Buff/回合/箭头读取/施法顺序 | [战斗规则修改](battle-rule-change/SKILL.md) | combat/arrows模块知识和对应现有测试 |
| 遗物、计数、Buff联动、候选领取与状态保存 | [遗物逻辑开发](relic-integration/SKILL.md) | relics模块知识、实现说明与RelicSystemTests |
| 保存、继续、槽位、教程临时运行 | [存档与教程](save-tutorial-validation/SKILL.md) | run-save-tutorial模块知识 |
| 新事实、过时知识、Skill创建/合并/删除 | [知识与Skill维护](knowledge-maintenance/SKILL.md) | 目标文档及维护规则；无需全库扫描 |
| 条带地牢运行控制迁移、局部几何更新、Scribble成本调查 | [格子地牢改造与几何验证](grid-dungeon-refactor/SKILL.md) | 草稿0.1：仅完成初始现场观察；拓扑适配、局部更新及优化方法均为候选，未实施验证 |

一个任务可组合两项方法，但去重定位和验证。内容脚本改动需编译；普通文档修订不需要Unity启动。没有匹配Skill时按现有工程约定直接做，不造空泛Skill。

## 内容模板

新增道具先读对应附页，再按需加载 [道具定义/脚本模板](content-integration/templates/magic.template.md)、[获得途径模板](content-integration/templates/magic-sources.template.md)；新事件或发放道具用 [事件模板](content-integration/templates/event.template.md)。这些是非执行支持文档，不是新增命令或正式游戏内容；示例ID/数值需现场替换。

## 生命周期

用户已授权项目Skill的新增、更新、合并、弃用、删除。重复方法有复用价值才新增；同主题优先改已有Skill；事实留模块知识，数值留配置。删除/合并时修索引和调用链接，保留必要恢复历史，不能仅改名让旧指令继续被检索。

项目工作流包含内容、战斗、遗物、场景UI、存档和知识维护等任务Skill与内容附页；旧资料在 `Assets/Docs/Archive/knowledge-before-rebuild-2026-11-05.zip`，日常不加载。
