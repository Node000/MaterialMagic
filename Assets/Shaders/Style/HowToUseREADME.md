# How To Use Style Shaders

## UI 面板/按钮

1. 选中 Scene 或 Prefab 中的 `Image`。
2. 将材质设置为 `Assets/Materials/Style/UI/M_UI_GlassPanel_Default.mat`。
3. Hover/Selected 状态可换成 `M_UI_GlassPanel_Hover.mat`，或在脚本/动画中调高 `_InnerGlowIntensity`。
4. 悬停扫光用 `M_UI_HoverSweep_Default.mat`，建议只给当前 Hover 的按钮使用，并用 `_SweepOffset` 或 `_UseTime` 控制播放。

## 浮动文字

当前战斗飘字使用 TMP 预制体，而不是运行时直接 new Text 对象：

- 预制体：`Assets/Prefabs/UI/FloatingText.prefab`
- 字体：`Assets/Resources/Fonts/FZG_CN SDF.asset`
- TMP 材质：`Assets/Materials/Style/Text/M_TMP_FloatingText_Default.mat`
- 类型：伤害飘字、护盾飘字、回血飘字；伤害被护盾完全挡住时显示 `BLOCK`。

`UI/Text_FloatAccent.shader` 仍保留给 legacy `UnityEngine.UI.Text`，但它不是 TMP SDF Shader，不能直接复用到当前飘字。TMP 飘字复用的是同一套颜色/描边/阴影设计，并通过 TMP Distance Field 材质实现。

文字位移、缩放、淡出仍应由脚本/动画完成，Shader/材质只负责增强描边、阴影和可读性。

## 敌人/角色 Image 或 Sprite

当前 `EnemyView` 的身体是 UI `Image`，这些 Sprite Shader 也支持 UGUI Image：

- 受击短闪：`Assets/Materials/Style/Sprite/M_Sprite_HitFlash_Default.mat`，动画 `_FlashAmount`。
- 选中/可攻击：`Assets/Materials/Style/Sprite/M_Sprite_Outline_Selected.mat`。
- Boss/特殊目标：`Assets/Materials/Style/Sprite/M_Sprite_Outline_Boss.mat`。
- 死亡/召唤：`Assets/Materials/Style/Sprite/M_Sprite_Dissolve_Default.mat`，动画 `_DissolveThreshold`。
- 精神污染/梦境状态：`Assets/Materials/Style/Sprite/M_Sprite_PsychedelicDistortion_Subtle.mat`。
- 全息敌人：`Assets/Materials/Style/Sprite/M_Sprite_HologramCRT_Default.mat`。

如需多个敌人同时不同参数，运行时应使用 `MaterialPropertyBlock` 或为 UI Image 实例化材质，避免直接改共享材质。

## 背景

1. 在 Canvas 最底层添加全屏 `Image`。
2. Sprite 可用项目里的 `Assets/Resources/Images/UI/基本方块.png` 或任意白色 9-slice/白图。
3. 材质使用 `Assets/Materials/Style/Background/M_BG_LayeredVaporwave_Default.mat`。
4. 边缘剪影/胶带/结构线等装饰使用单独 `Image`，材质用 `Assets/Materials/Style/Background/M_BG_EdgeDecorTint_Default.mat`，透明度控制在 `8%~15%`。
5. 全屏事件 glitch 叠加层放在背景上方、核心 UI 下方或最上层短时显示，材质用 `Assets/Materials/Style/Screen/M_Screen_EventGlitchOverlay_Default.mat`。

## 可读性底线

- 中央战斗区不要放强对比线条或高频动态元素。
- 常驻扫描线、噪声、RGB 分离都要低强度。
- 按钮与文字可读性优先于特效。
