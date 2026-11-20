---
id: kd_acee62c0-1c77-4ede-b9f9-b2c1434d72ac
injectMode: inherit
injectAgents:
- '*'
aiEditMode: auto
name: materialmagic-knowledge-maintenance
description: 工程事实变化或项目Skill新增、合并、更新、弃用、删除时使用；更新对应知识与索引，归档旧资料并批量收尾。
summary: 工程事实变化或项目Skill新增、合并、更新、弃用、删除时使用；更新对应知识与索引，归档旧资料并批量收尾。
skillEnabled: true
skillSurface: command
---

# 项目知识与 Skill 维护

适用：实现事实变化、文档过期、方法重复、Skill增删合并。用户已授权本项目知识/项目Skill自主维护；不扩大到用户级Skill、后台、发布或新增玩法设计。

## 最小读取

从AGENTS→PROJECT/ARCHITECTURE→匹配模块/Skill定位，读取受影响源码与配置；先判断旧文档是当前事实、用户产品意图、执行方法还是历史。不要每次全库哈希/盘点，避免把标题“最新”、旧通过记录或注释当现场证据。

## 更新去向

| 内容 | 放置位置 |
| --- | --- |
| 当前环境、入口/模块导航 | Assets/Docs/Workflow/PROJECT.md、ARCHITECTURE.md |
| 可复用工程事实、约束与查找入口 | memory/unity-project-understanding/对应模块 |
| 用户确认的产品方向/需求 | design/；本任务仅事实纠正不顺带发明新决策 |
| 尚未接入/当前限制 | Assets/Docs/Workflow/KNOWN_GAPS.md；计划仅在确有执行任务时建立 |
| 可重复的方法与检查 | skill/对应SKILL.md，内容差异用附页 |
| 人类文案、配置说明、交付报告 | Assets/Docs/ |
| 外部资料 | reference/并保留来源，尊重只读权限 |

改文档本身以替换旧结论，不能堆“旧结论+追加更正”。源码数值表、易变行号、一次调查轨迹尽量不长期抄录；保留入口/契约/重要差异。读取元数据与maintenanceRules，保现有id和用户偏好含义。

## Skill 生命周期

1. 有真实重复任务与稳定入口才新增，不造空泛流程。方法先找已有Skill，主题重叠则合并；模块事实不要复制进每个Skill。
2. 项目Skill在Locus/knowledge/skill，不安装用户级目录。维护INDEX触发条件和附页链接，按需加载；Skill不授予额外操作权限。
3. 删除/合并旧文档前确认没有独有有效决定丢失；有必要则先保可恢复历史。更新所有活动入口/链接，历史档明确不作为当前指令，不默认解压。
4. 普通源码任务只同步影响的知识，无长期变化不记日志；不要把临时未连接、一次权限或个人标识写成永久事实。

## 模板维护

容易反复误配的稳定字段/API适合放同Skill下 `templates/` 的Markdown代码块，入口页链接并说明何时加载；不要每个任务默认加载全部模板。模板应给完整最小形状、合并位置、统一替换规则和适用限制；示例ID不视为预留，不能直接注册到正式内容。

字段/签名/路由变化时同时更新模板与对应知识，验证JSON可解析、字段类型与当前DTO匹配、C#调用签名/工厂契约和引用闭合。不要把示例静态校验写成游戏编译/运行通过。稳定契约之外的平衡值、空闲ID、Prefab参数仍现场读，不在模板固化。

模板不是独立执行Skill：知识write传Markdown正文，由工具生成frontmatter；需要调整注册属性时读取生成结果，再用edit保留id并设置 `skillEnabled: false`，避免把第二份frontmatter写进正文或误作命令发布。现有正式SKILL.md继续启用，模板只被对应入口按需引用。

## 一次收尾

全部改完后一次检查目标文件存在、相对链接/索引、明显旧字段/错误入口和改动范围；当前总校验≤60秒，不运行Unity/构建来验证文档。不向真实游戏新增测试内容；可选探针只在内存/Library/Locus/tmp，使用后清除。检查失败先集中修正，不反复全库复验。

Git仅放行项目knowledge，继续忽略Locus配置/会话/工作树与本地秘密；不自动提交/推送。最终报告新入口、合并/弃用结果、验证范围与限制；用户要求一次最终汇报时不发进度播报。
