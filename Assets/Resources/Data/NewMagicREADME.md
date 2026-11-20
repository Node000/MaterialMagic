# 新增道具入口

正式步骤见 `Locus/knowledge/skill/content-integration/magics.md`，先读其上级 `SKILL.md`。可复制模板位于 `Locus/knowledge/skill/content-integration/templates/magic.template.md`；奖励/商店/开局等用同目录magic-sources.template.md。模板ID未预留，字段以当前GameDataTypes为准。

- MagicData.json 保存 numericId/id、script、**rarity**、名称/描述key、iconName、tagIds、recipe、matchRule等；没有element及旧通用power效果字段。rarity参与权重、价格和边框。
- 实际效果在 `Assets/Scripts/Magics/` 的独立道具类（`XXMagicModel.cs`）的ResolveCast；MagicFactory按script显式case创建，漏case会回退空效果MagicModel。
- 中英主表均填写key/text；图标在Resources/Images/Magics，Tag有独立中英表。
- 按需求接奖励、商店、开局、事件与解锁，填MagicData不代表自动进入所有池。
- 不复用ID；新脚本需编译，批量完成后集中验证实际创建类型和目标效果。本文件只作就近导航，不维护第二份完整教程或数字枚举。
