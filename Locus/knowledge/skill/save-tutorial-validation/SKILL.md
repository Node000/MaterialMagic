---
id: kd_72271062-17ab-4184-9cf5-9da8e748215d
injectMode: inherit
injectAgents:
- '*'
aiEditMode: auto
name: materialmagic-save-tutorial-validation
description: 修改保存、继续、槽位、结算或独立教程时使用；核对导出恢复、正常入口、教程隔离和输入门控。
summary: 修改保存、继续、槽位、结算或独立教程时使用；核对导出恢复、正常入口、教程隔离和输入门控。
skillEnabled: true
skillSurface: command
---

# 存档、槽位与教程隔离

适用：保存/恢复/退出/胜败/放弃、场景继续、教程入口与门控。先读run-save-tutorial模块，确认正常菜单入口与directSampleDebugRun差异。

## 定位与实现

1. 读RunSaveSystem的实际调用者、DTO及导出/恢复两端，区分Map检查点、Node快照与纯摘要；别把能序列化字段当作每步都会自动保存。
2. 明确目标槽位与persistentDataPath，先隔离/备份，未经授权不清用户档、历史、解锁和PlayerPrefs。优先内存DTO或隔离路径。directSampleDebugRun仅挡检查点保存：Awake.EmitAscensionSelect直接读unlock、默认services写codex、正式StartShopLevel读run、结果页直接清档均需另查。无IO注入须在Play的SubsystemRegistration重置之后、目标Awake之前；只在EditMode预设会被覆盖。
3. 持久字段同时覆盖export/create/restore/默认或版本迁移，ID与随机状态稳定。递归linkedCards/附魔与道具numericId需通过现行工厂还原；不复用已发行ID。
4. 场景恢复看sceneName调用是否传入，Build Settings与实际场景路径是否可加载；普通保存与EnterSecondFloor边界有已知风险，不以旧“二层已接入”推断可用。
5. 教程用BeginNewTutorialRun/ConsumeStartingTutorialRun/EndTutorialRun独立临时状态，保存/历史/正常结算早退；教程A0、金币10、固定回合手牌和章节常量由现有代码控制，不读普通档当教程初始状态。
6. 教程UI复用Scene的Steps/InputBlocker/Cutout，检查读段与行为页的快捷键门控；PE优先_pe文本，缺失回退共用key。

## 一次集中验证

按此次变化挑选必要项，合并成一次检查，不默认全套：

- 同一隔离槽位保存→还原目标状态，另槽不变；普通菜单“继续”才证明正常入口，直接Play的Debug局不能证明持久化。
- 新字段或卡牌状态 round-trip：数值、ID、附魔、容器、随机seed/step一致；旧DTO默认/迁移路径不丢档。防SL还须比恢复前后step、手牌和派生缓存；先随机构造再Restore覆盖仍可能耗流，UI显示getter也要检查。seed/step持久化不能单独证明恢复确定。
- 教程进入→退出或完成，普通运行/历史/解锁不被清写；同意状态、平台账号不作为测试对象清除。
- 胜败/放弃只产生预期历史和清档；只在这些路径被改动时验证，不为文案重复结算整局。
- 教程真实点击/拖拽与快捷键检查门控、段落与Cutout切换，结束后输入释放。Play修改是临时的，永久接线须Edit Mode完成。

总检查遵守当前60秒预算；先完成一批改动再开始。需要Editor时编译完成后再运行，依赖共享状态的最小区间加Unity锁。未连接或时间不足明确未覆盖部分，禁止为“证明隔离”改正式存档。恢复备份、清探针与临时注册后交付。
