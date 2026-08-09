# Style Shader README

本目录交付《风格化shader和背景需求.md》中的 Shader 与基础预设。默认值均按“常驻克制、关键反馈短促”设置，不追求满屏强刺激。

## 文件结构

- `UI/UI_GlassPanel.shader`：S01，深色玻璃面板、双描边、轻噪点、弱扫描线。
- `UI/UI_HoverSweep.shader`：S02，按钮/标签悬停扫光，可用 `_SweepOffset` 脚本驱动单次扫过。
- `UI/Text_FloatAccent.shader`：S03 的旧版 UGUI Text 实现，保留给 legacy UI Text 使用；当前飘字改用 TMP SDF 预制体。
- `Sprite/Sprite_HitFlash.shader`：S04，敌人/角色受击短闪。
- `Sprite/Sprite_Outline.shader`：S05，选中、精英、Boss 等状态描边。
- `Sprite/Sprite_Dissolve.shader`：S06，噪声 + 像素块 + 扫描线混合溶解。
- `Sprite/Sprite_PsychedelicDistortion.shader`：S07，特殊状态低幅 UV 扭曲、色偏与残像。
- `Sprite/Sprite_HologramCRT.shader`：S08，全息/CRT 投影感。
- `Screen/Screen_EventGlitchOverlay.shader`：S10，短时全屏事件 glitch 叠加层。
- `Screen/TVRectGlitch.shader`：旧的独立 URP 全屏随机矩形横线扭曲、色散与扫描线测试效果。
- `UI/UI_TVRectGlitch.shader`：可直接挂在 UGUI Image 上的随机矩形横线扭曲、色散与扫描线效果。
- `UI/UI_TVRectGlitchSubtract.shader`：UGUI Image 减色叠加版本；用 RevSub 混合在随机故障矩形内扣减下方颜色，适合背景图。
- `Background/BG_LayeredVaporwave.shader`：BG01/BG02/BG04/BG06/BG07 合并底层背景 Shader。
- `Background/BG_EdgeDecorTint.shader`：BG03 边缘剪影/装饰贴图染色与弱质感 Shader。
- `Test/PixelParticleDissolve.shader`：像素粒子汇聚/消散测试效果，参考 `Assets/Shaders/Ref/Dissolve.gdshader` 的固定网格噪声消散方向；直接采样所挂 `Image` 的 Sprite，初态保留原贴图，消散时才转粒子色并透明。
- `Test/SpatialRift.shader`：程序化空间裂缝，支持尺寸与裂缝密度。
- `Test/FluidInkBackground.shader`：多色液体颜料扩散背景，支持颜色数量与流速。
- `Test/HalftoneFloatingBackground.shader`：暗色半调网点与低频浮动面板背景。
- `Test/SquareParticleBurst.shader`：方形粒子描边、染色与透明材质，交由 Particle System 提供敌人死亡爆散运动。

## 独立测试场景

测试场景及专用材质在 `Assets/Shaders/TestScene/`：

- `PixelParticleDissolveTest.unity`：提供“汇聚”“消散”按钮；按钮平滑驱动 `_DissolveProgress`。
- `SpatialRiftTest.unity`：提供“大小”“密度”滑条，分别驱动 `_RiftSize` 与 `_RiftDensity`。
- `FluidInkBackgroundTest.unity`：提供“颜色数量”“速度”滑条，分别驱动 `_ColorCount` 与 `_FlowSpeed`。
- `HalftoneBackgroundTest.unity`：半调浮动背景预览。当前以 StartScene 的暗色低频浮动语汇为基础；提供实际图片后可改为采样图片的半调版本。
- `SquareParticleBurstTest.unity`：方形粒子爆散预览；点击“播放爆散”重放敌人死亡用的 Particle System。
- `TVRectGlitchTest.unity`：UGUI Image 电视信号测试；`TV Signal Image` 使用专用材质，RectTransform 可直接调整位置、大小和旋转，随机矩形会在静默、故障、恢复之间循环。

测试交互控制脚本为 `Assets/Scripts/VFX/ShaderEffectTestController.cs`。每个场景只引用其在 `Assets/Shaders/TestScene/Materials/` 中的专用材质，调参不会影响项目已有材质。

## 材质预设

材质预设位于 `Assets/Materials/Style/`：

- UI：`M_UI_GlassPanel_Default`、`M_UI_GlassPanel_Hover`、`M_UI_HoverSweep_Default`
- Text：`M_Text_FloatAccent_Damage`、`M_Text_FloatAccent_Crit`、`M_Text_FloatAccent_Heal`、`M_Text_FloatAccent_Debuff`（legacy UI Text），`M_TMP_FloatingText_Default`（TMP 飘字）
- Sprite：`M_Sprite_HitFlash_Default`、`M_Sprite_Outline_Selected`、`M_Sprite_Outline_Boss`、`M_Sprite_Dissolve_Default`、`M_Sprite_PsychedelicDistortion_Subtle`、`M_Sprite_HologramCRT_Default`
- Background：`M_BG_LayeredVaporwave_Default`、`M_BG_EdgeDecorTint_Default`
- Screen：`M_Screen_EventGlitchOverlay_Default`

## 调参原则

- 常驻 UI：`_NoiseIntensity` 建议 `0.02~0.08`，`_ScanlineIntensity` 建议 `0~0.05`。
- 受击：只动画 `_FlashAmount`，时长建议 `0.05s~0.12s`。
- 描边：普通选中 `1~2px`，Boss `3~4px`，不要长时间高强度脉冲。
- 溶解：动画 `_DissolveThreshold`，死亡建议 `0.35s~1.0s`。
- 扭曲：`_DistortAmount` 常驻不要超过 `0.01`。
- 全屏 glitch：只短时把 `_DurationAmount` 拉到 `1` 后回 `0`，单次 `0.10s~0.40s`。
- TMP 飘字：使用 `Assets/Prefabs/UI/FloatingText.prefab`，字体为 `Assets/Resources/Fonts/FZG_CN SDF.asset`，材质为 `Assets/Materials/Style/Text/M_TMP_FloatingText_Default.mat`。
- 背景：默认背景已包含渐变、中心光晕、低强度网格、颗粒、扫描线和暗角；中心区域保持干净。
