---
id: kd_builtin_memory_project_mistake_note
injectMode: full
aiEditMode: auto
maintenanceRules: |-
  - Record only verified problems, rework causes, and avoidance steps
  - Prioritize recurring pitfalls, constraints, regression points, and confirmed fixes
  - Keep each entry short and focused on one lesson or constraint
  - Keep the list within 20 items and merge duplicates regularly
  - Remove outdated issues, non-reproducible issues, and unsupported guesses
---

- 本地Locus插件 `Packages/com.farlocus.locus/` 曾误提交残缺编译快照；继续按.gitignore留本地，不把插件源码或运行缓存带回仓库。
- 按钮onClick.Invoke成功不证明能点：必须用EventSystem.RaycastAll确认最高命中，再ExecuteEvents。纯标签raycastTarget、全屏遮罩和CanvasGroup都可能挡住真实点击。
- dim遮罩若与面板是兄弟，Hide须同时关闭；只关窗口会留下全屏Image吞点击。验收包含关闭后底层交互。
- SpringLineHighlightUI.hideOnAwake=true会停用整棵子树；常显面板父级不能沿用该悬停高亮配置。检查activeInHierarchy，不能只看Edit Mode外观。
- 横向拉伸锚点的sizeDelta.x不是最终宽度；写尺寸前确认anchor/pivot与父级宽度，避免重复加宽导致布局溢出。
- 序列化字段改名后例行保存Scene会用新字段默认值覆盖旧调参；先查序列化使用，加FormerlySerializedAs或迁移，再保存，保留美术/手感数据。
- 运行时颜色曾覆盖血条、血量字等美术设定；代码不硬编码或重写颜色，Scene/Prefab Graphic/TMP与专用美术配置是来源。规则验证不能顺带重播种视觉值。
- ItemWaterfallEffect碎片停稳转Static曾在支撑被回收后留下悬空碎片；保持Dynamic，优化先用对象池/统一Tick，不重新引入解冻补丁链。
- Unity 2022.3的Android Build Settings重绘曾在DoTargetListUnsafe抛GUILayout异常并原生崩溃；切平台前关Build Settings。另有构建成功后TapSDK删link.xml/刷新附近崩溃，根因未定，先读Editor.log判断构建是否已成功，不直接归因游戏代码或Locus。
- FZG_CN SDF缺「」〈〉、全角～、〜、・、◆、※、★等；HasCharacter曾误判。改文案以实际渲染/Console缺字为准，可用“”《》【】·•●—…和ASCII~，不随手重建字体资产。
- 本地化JSON是items内key/text，不是value。LocalizedTMPText有效key会覆盖场景文字，只有缺失/空值才fallback；删组件前检查英文，避免中文场景文本覆盖英文体验。
- 敌人数据最终由同ID Definition覆盖JSON；Import JSON to ScriptableObjects会SetData覆盖SO并替换数据库列表，不能当只读Validate。保GUID不保证保Inspector内容。
- 内容工厂不能混用：道具按script switch（漏case为空效果基类）、敌人按numericId再应用运行定义、箭头附魔反射短类名（失败null）、道具强化按id switch。新增后确认实际创建类型。
