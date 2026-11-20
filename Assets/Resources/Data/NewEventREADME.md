# 新增事件入口

正式步骤见 `Locus/knowledge/skill/content-integration/events.md`，可复制完整节点与双语形状见 `Locus/knowledge/skill/content-integration/templates/event.template.md`。字段以GameDataTypes为准，实际效果case在HandSystemUI.ResolveEventEffectRoutine。

- EventData.json的items由nodes/options/effects组成；LevelData.eventPoolId绑定事件numericId。
- recipe是“1234”字符串（火风水土），不同于道具配方数组；randomRecipeLength在事件创建时生成，ignoreOrder控制无序，isExitOption不参与普通匹配。
- 新事件优先effects；GainMagic=4打开通用奖励且不读magicIds，与GainMagicModifier/GainMaterialModifier同为回收后延迟。指定赠送使用GainMagicById=25+magicIds，当前回收前执行，满栏不发、空位不足只发部分，不提供替换；GainRelic等保留值无执行case。
- 文案统一在 `Assets/Docs/事件本地化文本审阅.md` 调整，并同步中英Event表的key/text；选项iconName为Images/Events下图片名。
- 完成一批后集中检查节点后继、效果分支、文本与关卡绑定，不向正式池留下测试事件。
