---
id: kd_613daf22-d9c2-43c9-8988-b4c017e3159a
injectMode: inherit
injectAgents:
- '*'
aiEditMode: inherit
---

# 进阶、解锁、基础强化与图鉴

`Assets/Scripts/Data/DifficultyUpgradeSystem.cs`同时定义进阶数据/运行状态与修改器；AscensionSystem管理选择和可选边界。`Assets/Resources/Data/AscensionData.json`列等级到upgradeIds，DifficultyUpgradeData.json列实际效果。正负阶按各自方向累计；教程InitializeNewRun使用A0，读档InitializeFromSave恢复difficulty而非菜单当前选择。

StartAscensionSelectionUI、AscensionDetailPanelUI、AscensionTopBarIndicatorUI分别对应菜单抽屉、详情与局内显示。新增进阶应检查实际Modify入口是否被玩法调用，不仅增加一条数据。地图、敌人运行定义、金币、商店、出牌上限等有不同修改点；配置数值不能直接当最终值。

`Assets/Scripts/Data/UnlockSystem.cs`按targetType/targetId规则过滤开局、道具、箭头附魔、道具强化；没有规则通常默认开放。Magic可先按字符串id规则，再按numericId回退。UnityAdapters/UnlockProgressSaveSystem经RuleProfilePersistence端口持久化进度；每世界Rules.Profile是detached固定输入，Simulation不读写真实profile；正常运行结算调用推进与弹窗；教程隔离，不用调试授予代替正式条件验证。

主菜单设置中的「开发者模式」只开放给 Editor 和显式启用 `MATERIALMAGIC_QA_UNLOCK` 的 Development Build。QA 包在目标平台的 Player Settings → Scripting Define Symbols 添加该宏；不要用仅传给脚本编译器的临时 define 代替。构建场景处理器移除非 QA 包的整个按钮子树，解锁执行逻辑也不编入。QA 玩家包的玩法存档用 `Save_QA` 和独立选槽键，不与正式 `Save` 共用；Editor 默认仍使用当前存档，可用绝对路径环境变量 `MATERIALMAGIC_EDITOR_SAVE_PATH` 隔离验证。

确认后 `DeveloperUnlockService` 只给当前槽补规则、开放进阶上限、点亮现有道具图鉴；不伪造通关、不清原弹窗队列、不修改正在进行的局。操作持久化，可重复调用补后续内容；图鉴/解锁各写自己的文件，失败可以重试。

`Assets/Scripts/Data/ArrowUpgradeSystem.cs`、ArrowUpgradeConfig、ArrowUpgradePanelUI与Visual配置已存在；RunSaveSystem保存节点ID、下回合护盾等，PlayerState基础效果接入。二层入口未正式串联，不能把“强化系统存在”写成“二层过渡流程完成”。数值/颜色从SO和现有视觉配置读，不复制到代码默认值。

`Assets/Scripts/StartMagicCodexPanelUI.cs` 的RebuildMagicItemList枚举GameDataDatabase.MagicData.Values、按numericId排序，常规新增道具无需另一份图鉴登记。面板已构建后缓存列表，改数据库需重建面板/新会话，普通刷新不保证新项出现。

`Assets/Scripts/MagicCodexProgressSystem.cs` 管发现/NEW标记，PlayerState.SetMagicAtSlot经Services通知发现；生产bootstrap绑定MarkMagicDiscovered，Simulation无真实写入。图鉴发现不等于UnlockSystem开放奖励池；验证入槽可能影响真实图鉴进度，要隔离/恢复。新增内容按需求考虑获得、解锁、图鉴和历史存档，不默认把四者全部强制联动。

遗物纯逻辑已实现，入口见 `memory/unity-project-understanding/relics.md` 与 `skill/relic-integration/SKILL.md`；包含实例/计数、Buff/战斗联动、候选领取和v4存档。二层门控、正常渠道投放与UI未接，ShopSlotEnum.Relic/GainRelic仍不能当完整商品/事件入口。故障值/Tag过渡系统只留历史设计恢复档。
