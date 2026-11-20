---
id: kd_036e7d89-3555-446d-a4cf-361f8d288287
injectMode: inherit
injectAgents:
- '*'
aiEditMode: auto
name: materialmagic-scene-ui-integration
description: Scene或Prefab面板、按钮、详情、布局、多语言及PC/PE绑定变更时使用；保美术参数并验证真实射线和显隐边界。
summary: Scene或Prefab面板、按钮、详情、布局、多语言及PC/PE绑定变更时使用；保美术参数并验证真实射线和显隐边界。
skillEnabled: true
skillSurface: command
---

# Scene UI、交互与语言接入

适用：面板、按钮、布局、详情、语言或PC/PE接线。先读 `Locus/knowledge/memory/unity-project-understanding/ui-localization.md`，再定位准确资产和对象。

## 定位与修改

1. 从最新Active Scene或用户明确路径确定Scene实例/Prefab范围，读Property Tree、Prefab源/Override和组件字段。重复名字使用返回的序号路径；不把实例请求自动扩大成Prefab修改。
2. 查PanelUI/View与UIManager的序列化引用、按钮事件、显隐根和遮罩；改名/移除字段/方法/组件前查代码与unity_code_usages（含UnityEvent/AnimationEvent），保留FormerlySerializedAs等迁移。
3. Scene直接搭面板/容器/地图/浮层，动态内容项复用Prefab和独立View。优先Inspector绑定，不新增按名递归查找或整套运行时兜底。PC/PE只补目标结构/绑定，不整份复制覆盖美术布局。
4. 美术颜色、字号、尺寸、速度、按钮样式和“X”文案不擅改；必要视觉变化先按既有授权判断，否则提出具体方案。代码只改行为与必要显示状态；结构化资产用Unity API，保持GUID/引用。
5. 改RectTransform前区分local字段、world标注、锚点/pivot与拉伸sizeDelta。确认LayoutGroup/脚本是否会在运行中覆盖所改值。
6. 文案先确定LocalizedTMPText/key/fallback/运行覆盖，JSON写key/text并维护中英；教程PE查key+_pe。改Scene文本或删组件前考虑另一语言；文案审阅文件放Assets/Docs。

## 一次集中验收

- 代码已应用；序列化/Inspector验证前完整编译。短暂依赖Active Scene/Selection/Play状态时取得最小Unity锁，最后状态相关调用后释放，不把普通资产编辑包进锁。
- 走目标入口，检查activeInHierarchy、CanvasGroup/Graphic射线、全屏dim和SpringLineHighlightUI.hideOnAwake。按钮用EventSystem.RaycastAll找实际最高命中，再ExecuteEvents走事件，Invoke只可辅助逻辑检查。
- 看一次正常打开→操作→关闭和再次打开，关闭后遮罩不得吞点击；有快捷键需确认输入门控同样生效。
- 语言看中英长文本/缺字与目标平台分辨率；PE走点击/确认分支，不以鼠标悬停证明触摸可达。截图只在视觉变更必要时做。
- 一批后合并检查，不反复跑全场景。Editor未连或预算不足，报告未覆盖的运行/视觉/真机项；不能用静态检查代替点击通过。

交付列明实例或资产范围、实际验证和需用户判断的画面/手感。保留用户原场景设置和未保存工作，不留下临时面板/截图探针组件。
