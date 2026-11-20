---
id: kd_e0c3ad5c-ce77-4061-8b14-b677dbd9435d
injectMode: inherit
injectAgents:
- '*'
aiEditMode: inherit
---

# 工程导航

当前实现入口：`AGENTS.md` → `Assets/Docs/Workflow/PROJECT.md` → `Assets/Docs/Workflow/ARCHITECTURE.md` → `Locus/knowledge/skill/INDEX.md`。只按任务读取模块。

- 自定义代码主目录 `Assets/Scripts/`；Data 不只含数据，亦含 BattleManager、RunManager、PlayerState、EnemyModel、MagicModel、BuffModel、ArrowReadSystem。
- 具体道具在 Magics，具体敌人在 Enemies，箭头附魔在 Modifiers，道具强化在 MagicModifiers，Buff 在 Buffs；遗物逻辑在 Relics，由RelicFactory显式注册。各类注册机制不同，见 content/arrows/relics。
- HandSystemUI仍持表现和宿主流程；RulePresentation先提交BattleTurnDriver再播冻结trace；Commands/Choices/RunSession partial接真实共享会话。Simulation含完整Battle/GameSession、选择/经济/教程/地图/上下文/快照与AI replay。Core在Data根asmdef+7规则asmref，UnityAdapters单向引用Core，Presentation引用两者，Tests/Editor独立；Standalone链接同一生产源，无UnityDLL。UIManager绑定面板，旧兜底与名称查找仍存在；边界见实施计划。
- JSON 在 Resources/Data；敌人 SO 在 Resources/Config/Enemies 且由 GameContentDatabase 覆盖 JSON；箭头附魔 SO 在 Resources/EnchantConfig/MaterialModifiers。
- StartConfig.json 是当前开局读取表；ScriptedMagicModels.cs 为空，旧 SampleScene.unity 已不在工程。
- 主场景为 StartScene、SampleScene_PC、SampleScene_PE；PC二层场景在 Assets 根目录；具体启用构建列表看 PROJECT，不用旧计划推断。
- 模块知识：content、combat、arrows、run-save-tutorial、maps、economy-events、ui-localization、progression、relics、platform、presentation。
- 当前缺口集中 `Assets/Docs/Workflow/KNOWN_GAPS.md`；旧快照/方案恢复档不默认读取。新增知识修正文档本身，不追加互相覆盖的 Update 条目。
