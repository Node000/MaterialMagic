---
id: kd_064462c1-f489-403c-97f5-d58d58c67042
injectMode: inherit
injectAgents:
- '*'
aiEditMode: auto
name: materialmagic-content-integration
description: 新增或修改道具、敌人、事件、箭头附魔与道具强化时使用；按内容类型加载附页，完成当前数据、工厂、池、多语言和保存ID接入。
summary: 新增或修改道具、敌人、事件、箭头附魔与道具强化时使用；按内容类型加载附页，完成当前数据、工厂、池、多语言和保存ID接入。
skillEnabled: true
skillSurface: command
---

# MaterialMagic 内容接入

适用：新增或修改敌人、道具、事件、箭头附魔、道具强化及相关获得途径。先读本页，再只读对应附页；不要同时扫全部内容表。

## 定位

1. 从请求确认内容类型、效果、目标ID与出现途径；查看 `Assets/Docs/Workflow/ARCHITECTURE.md` 的工厂差异和 `Locus/knowledge/memory/unity-project-understanding/content.md`。
2. 读对应附页及匹配的模板，再核对当前DTO、一个最接近的实例、工厂与实际规则调用；有SO覆盖时读取最终资产，不能只看JSON。模板给稳定签名/字段形状，不替代现场ID、数值和资产绑定。
3. 沿ID查关卡/奖励/商店/开局/解锁/存档，判断此次确需修改哪些路径。新增ID唯一且不复用旧ID；改名/删除先查代码、序列化与字符串/存档引用。

## 接入

- [敌人](enemies.md)：JSON基线、SO覆盖层、numericId工厂与意图。
- [道具](magics.md)：MagicData、script switch、ResolveCast、稀有度和池。
- [事件](events.md)：nodes/options/effects、配方与关卡绑定。
- [箭头附魔](enchantments.md)：Definition、反射工厂、生命周期、材质/颜色、价格与过滤。
- 道具强化：读 `Assets/Scripts/MagicModifiers/MagicModifierFactory.cs` 的id分派及相近类，修改MagicModifierData和中英MagicModifier表；未知id回退GenericMagicModifierModel。别照搬箭头反射接入。

## 按需模板

| 任务 | 模板 | 内容 |
| --- | --- | --- |
| 新道具定义/行为 | [magic.template.md](templates/magic.template.md) | MagicData、完整最小MagicModel、工厂case、双语与图标格式 |
| 道具出现途径 | [magic-sources.template.md](templates/magic-sources.template.md) | 奖励/商店/开局、事件指定赠送、解锁与图鉴契约 |
| 新事件/道具发放 | [event.template.md](templates/event.template.md) | 完整节点/文本/关卡形状，GainMagic与GainMagicById的时序与满栏区别 |

模板是非执行Markdown支持资料，示例ID和名字未预留，代码块不可自动导入为游戏内容。只加载匹配文件，合并目标条目并保留其他配置。结构化资产用Unity API，保GUID和既有美术值；离线不raw YAML写SO。文本JSON使用items容器，不写注释。中英文key/text与图标/Tag按需要一起接，文案说明放Assets/Docs。

## 一批完成后的最小验证

1. 集中查JSON解析、ID重复、工厂目标存在、目标池/关卡与多语言key。不要每写一个文件就重复全量校验或哈希。
2. 有C#改动通过hot reload/完整编译应用；新增类型、序列化和测试发现需完整编译。观察创建后的实际类型，避免回退类掩盖漏注册。
3. 常规新道具最低限度确认工厂类型、目标配方/效果与需求中的获得来源；按影响增加规则测试。只有改到UI/布局时走目标平台真实射线，新增持久状态/改ID或保存链时才扩展存档检查。不把所有可选项当必跑，不把配置存在当行为通过。
4. 若受当前60秒预算或Editor连接限制不能完成，报告实际完成和未覆盖部分。探针放隔离内存/Library/Locus/tmp，用完清理；不得把测试内容注册到正式池或存档。

交付：接入位置、出现途径、实际验证与剩余依赖；事实变动只同步对应模块，不生成全工程盘点。
