---
id: kd_dfd1a44e-1ccc-437c-a9f4-b6896d0dce52
injectMode: inherit
injectAgents:
- '*'
aiEditMode: inherit
---

# UI、绑定与本地化

## 结构与职责

主菜单StartMenuUI、游戏内HandSystemUI/UIManager编排，具体PanelUI与View负责刷新。Scene容器、Prefab内容项是现行协作偏好；旧代码的动态兜底和按名查找属于兼容实现，不作为新面板范式。

PC与PE分别保有布局/槽位预制体：`Assets/Scenes/SampleScene_PC.unity`、`Assets/Scenes/SampleScene_PE.unity`，`Assets/Prefabs/UI/MagicSlot_PC.prefab`、MagicSlot_PE.prefab。共享代码变化两端生效，Scene结构/绑定变化须逐端处理；不整场复制覆盖PE美术调参。PvOnly和PC二层亦可能挂共享组件，按实际用途确认范围。

UnifiedDetailContentBuilder生产统一详情，UnifiedDetailPopupUI展示；AddedDetailedUI/UnifiedDetailArrowSequenceUI显示附加框与序列。EnchantIconUI与Enchant_Color资产负责附魔图标。用户美术配色、文字、位置与引用要保留；不能为了点击行为运行时重写“X”等按钮文案。

## 本地化

`Assets/Scripts/Data/LocalizationSystem.cs`加载Resources/Data/Localization中的语言主表与_UI/_Tutorial/_Buff/_Material/_Modifier/_MagicModifier/_Enemy/_Event/_Tag补表，当前中英20份。JSON顶层items，每项**key/text**，不是value。

`Assets/Scripts/LocalizedTMPText.cs`有有效key时表值覆盖场景TMP，缺失/空才fallback。改场景中文不等于运行文案已改；删组件前先确认英文是否因此回退中文。LocalizedImageSprite负责语言图片，详情/关键词另有解析。

TutorialManagerUI在PE优先查key_pe；缺失回退共用key。改方位文案时先检查这个入口，不再新建第二套教程管理器。

事件中文审阅源为 `Assets/Docs/事件本地化文本审阅.md`；改动要回写中英表及相应事件结构。其他文案先确认目标key、场景组件和默认文本的来源，不自动“同步”覆盖美术独立内容。

## 常见边界

- RectTransform拉伸锚点时sizeDelta不是最终宽高；操作前看局部字段与世界标注，勿把画布像素/世界坐标混写。
- dim遮罩若是面板兄弟，开关必须覆盖两者；面板消失但遮罩仍可吞点击。
- SpringLineHighlightUI.hideOnAwake会停整棵子树，不可挂在需常显的内容父级却保留true。
- 悬停、移动确认与快捷键分支不同；真实点击要RaycastAll+ExecuteEvents，onClick.Invoke不能证明未被遮挡。
- FZG_CN SDF缺部分符号（如「」〈〉～◆※★）；以实际渲染/Console缺字告警判断，HasCharacter曾误判。优先用已有字形，别顺手重建字体资产。
- Serialized字段/方法改名先查代码及Unity序列化使用，加FormerlySerializedAs/迁移，避免例行保存场景把旧美术参数洗掉。

方法见 `Locus/knowledge/skill/scene-ui-integration/SKILL.md`。文档任务无需为此启动Play；无法观察的画面/真机明确未验证。
