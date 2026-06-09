# 附魔 Shader 参数说明

每个附魔现在都有独立 Shader，材质仍放在 `Assets/Resources/Materials/MaterialModifiers/`，运行时由 `MaterialModifierVisualUtility` 按附魔脚本名加载同名材质。

通用 UI/UGUI 参数：
- `_MainTex`：由 Image/Sprite 自动传入，通常不用手动指定。
- `_Color`：整体乘色，默认白色；一般保持不动。
- `_AuraColor`：附魔主色/高光色；运行时会按 `MaterialModifierData.json` 的 `lineColor` 覆盖。
- `_EffectSpeed`：动画速度，数值越大越快。
- `_EffectStrength`：效果强度，通常 0-1；过高会影响箭头可读性。
- `_ArrowDirection`：运行时按素材卡方向覆盖，火=0、水=1、风=2、土=3；美术通常不用手调。
- `_CopyCount`：仅 BigArrow2/3/4 使用，对应 2/3/4。
- `_AltTex1..4`：仅 RandomArrow 使用，四张基础箭头循环贴图。
- `_Stencil*`、`_ColorMask`、`_UseUIAlphaClip`：UGUI Mask/裁剪兼容参数，不建议修改。
## HalfArrowModifier

- Shader：`UI/MaterialModifiers/HalfArrowModifier`
- 文件：`Assets/Shaders/UI/MaterialModifiers/HalfArrowModifier.shader`
- 用途：半箭头：沿斜切线隐藏其中一半，保留黑色切割线；斜切线会周期偏移。当前材质 `_VisibleSide=1`。
- 美术参数：主要调 `_VisibleSide`、`_LineAngle`、`_LineWidth`、`_LineLength`、`_EffectSpeed`、`_EffectStrength`。

## FragileArrowModifier

- Shader：`UI/MaterialModifiers/HalfArrowModifier`
- 文件：`Assets/Shaders/UI/MaterialModifiers/HalfArrowModifier.shader`
- 用途：脆弱箭头：复用半箭头 Shader，沿斜切线切开，两部分都显示，并沿斜切线方向循环错位偏移。当前材质 `_VisibleSide=0`。
- 美术参数：主要调 `_LineAngle`、`_LineWidth`、`_LineLength`、`_EffectSpeed`、`_EffectStrength`。

## RepeatArrowModifier

- Shader：`UI/MaterialModifiers/RepeatArrowModifier`
- 文件：`Assets/Shaders/UI/MaterialModifiers/RepeatArrowModifier.shader`
- 用途：重复箭头：使用居中缩放布局叠加一层残影，避免复制贴图超出 UI 框被裁切。
- 美术参数：主要调 `_AuraColor`、`_EffectSpeed`、`_EffectStrength`。

## BigArrow2Modifier

- Shader：`UI/MaterialModifiers/BigArrow2Modifier`
- 文件：`Assets/Shaders/UI/MaterialModifiers/BigArrow2Modifier.shader`
- 用途：双重箭头：显示 2 个横向排列的箭头拷贝。
- 美术参数：主要调 `_AuraColor`、`_EffectSpeed`、`_EffectStrength`。

## BigArrow3Modifier

- Shader：`UI/MaterialModifiers/BigArrow3Modifier`
- 文件：`Assets/Shaders/UI/MaterialModifiers/BigArrow3Modifier.shader`
- 用途：三重箭头：显示 3 个横向排列的箭头拷贝。
- 美术参数：主要调 `_AuraColor`、`_EffectSpeed`、`_EffectStrength`。

## BigArrow4Modifier

- Shader：`UI/MaterialModifiers/BigArrow4Modifier`
- 文件：`Assets/Shaders/UI/MaterialModifiers/BigArrow4Modifier.shader`
- 用途：四重箭头：按 `_CopyCount` 在 1-20 个之间复制箭头，通过 `_GroupScale` 控制整体缩放适配 UI 框，`_BaseSpacing`/`_AnimAmplitude`/`_AnimSpeed` 控制横向间距循环动画，`_OverlapOrder` 控制左右层级覆盖顺序。
- 美术参数：主要调 `_CopyCount`、`_GroupScale`、`_OverlapOrder`、`_BaseSpacing`、`_AnimAmplitude`、`_AnimSpeed`。

## ProliferatingArrowModifier

- Shader：`UI/MaterialModifiers/ProliferatingArrowModifier`
- 文件：`Assets/Shaders/UI/MaterialModifiers/ProliferatingArrowModifier.shader`
- 用途：增殖箭头：箭头局部膨胀并带一枚新生残影。
- 美术参数：主要调 `_AuraColor`、`_EffectSpeed`、`_EffectStrength`。

## ReturnArrowModifier

- Shader：`UI/MaterialModifiers/ReturnArrowModifier`
- 文件：`Assets/Shaders/UI/MaterialModifiers/ReturnArrowModifier.shader`
- 用途：返回箭头：压暗原图并叠加回返箭头符号。
- 美术参数：主要调 `_AuraColor`、`_EffectSpeed`、`_EffectStrength`。

## RandomArrowModifier

- Shader：`UI/MaterialModifiers/RandomArrowModifier`
- 文件：`Assets/Shaders/UI/MaterialModifiers/RandomArrowModifier.shader`
- 用途：随机箭头：在四种基础箭头贴图间循环渐变；切换时使用 alpha 加权混合，避免透明区插值出色块。
- 美术参数：主要调 `_AuraColor`、`_EffectSpeed`、`_EffectStrength`。

## RetainedArrowModifier

- Shader：`UI/MaterialModifiers/RetainedArrowModifier`
- 文件：`Assets/Shaders/UI/MaterialModifiers/RetainedArrowModifier.shader`
- 用途：保留箭头：横向故障偏移、扫描线和轻微色散。
- 美术参数：主要调 `_AuraColor`、`_EffectSpeed`、`_EffectStrength`。

## EternalArrowModifier

- Shader：`UI/MaterialModifiers/EternalArrowModifier`
- 文件：`Assets/Shaders/UI/MaterialModifiers/EternalArrowModifier.shader`
- 用途：永恒箭头：周期性反相闪烁。
- 美术参数：主要调 `_AuraColor`、`_EffectSpeed`、`_EffectStrength`。

## DoomModifier

- Shader：`UI/MaterialModifiers/DoomModifier`
- 文件：`Assets/Shaders/UI/MaterialModifiers/DoomModifier.shader`
- 用途：毁灭箭头：灰阶压暗、噪声和暗条纹。
- 美术参数：主要调 `_AuraColor`、`_EffectSpeed`、`_EffectStrength`。

## TemporaryModifier

- Shader：`UI/MaterialModifiers/TemporaryModifier`
- 文件：`Assets/Shaders/UI/MaterialModifiers/TemporaryModifier.shader`
- 用途：临时箭头：整体呼吸式透明度和颜色漂移。
- 美术参数：主要调 `_AuraColor`、`_EffectSpeed`、`_EffectStrength`。

## LazyModifier

- Shader：`UI/MaterialModifiers/LazyModifier`
- 文件：`Assets/Shaders/UI/MaterialModifiers/LazyModifier.shader`
- 用途：懒惰箭头：纵向波浪形拖拽变形。
- 美术参数：主要调 `_AuraColor`、`_EffectSpeed`、`_EffectStrength`。

## VortexModifier

- Shader：`UI/MaterialModifiers/VortexModifier`
- 文件：`Assets/Shaders/UI/MaterialModifiers/VortexModifier.shader`
- 用途：漩涡箭头：中心旋涡扭曲和环形光。
- 美术参数：主要调 `_AuraColor`、`_EffectSpeed`、`_EffectStrength`。

## SturdyModifier

- Shader：`UI/MaterialModifiers/SturdyModifier`
- 文件：`Assets/Shaders/UI/MaterialModifiers/SturdyModifier.shader`
- 用途：稳固：金属扫光和边缘硬光。
- 美术参数：主要调 `_AuraColor`、`_EffectSpeed`、`_EffectStrength`。

## KindlingModifier

- Shader：`UI/MaterialModifiers/KindlingModifier`
- 文件：`Assets/Shaders/UI/MaterialModifiers/KindlingModifier.shader`
- 用途：引燃：像素火焰噪声从底部闪烁。
- 美术参数：主要调 `_AuraColor`、`_EffectSpeed`、`_EffectStrength`。

## ChargeModifier

- Shader：`UI/MaterialModifiers/ChargeModifier`
- 文件：`Assets/Shaders/UI/MaterialModifiers/ChargeModifier.shader`
- 用途：充能：随机闪电折线。
- 美术参数：主要调 `_AuraColor`、`_EffectSpeed`、`_EffectStrength`。

## FlowModifier

- Shader：`UI/MaterialModifiers/FlowModifier`
- 文件：`Assets/Shaders/UI/MaterialModifiers/FlowModifier.shader`
- 用途：流动：水平流线和波纹高光。
- 美术参数：主要调 `_AuraColor`、`_EffectSpeed`、`_EffectStrength`。

## LiquefyModifier

- Shader：`UI/MaterialModifiers/LiquefyModifier`
- 文件：`Assets/Shaders/UI/MaterialModifiers/LiquefyModifier.shader`
- 用途：液化：水滴状高光和水平扭动。
- 美术参数：主要调 `_AuraColor`、`_EffectSpeed`、`_EffectStrength`。

## LinkedArrowModifier

- Shader：`UI/MaterialModifiers/LinkedArrowModifier`
- 文件：`Assets/Shaders/UI/MaterialModifiers/LinkedArrowModifier.shader`
- 用途：联结箭头：通用边缘光与斜向扫光。
- 美术参数：该类附魔当前使用边缘光+斜向扫光，主要调 `_AuraColor`、`_PulseSpeed`、`_PulseStrength`、`_SweepSpeed`、`_SweepWidth`、`_SweepIntensity`、`_EdgeIntensity`。

## OmniArrowModifier

- Shader：`UI/MaterialModifiers/OmniArrowModifier`
- 文件：`Assets/Shaders/UI/MaterialModifiers/OmniArrowModifier.shader`
- 用途：全向箭头：通用边缘光与斜向扫光。
- 美术参数：该类附魔当前使用边缘光+斜向扫光，主要调 `_AuraColor`、`_PulseSpeed`、`_PulseStrength`、`_SweepSpeed`、`_SweepWidth`、`_SweepIntensity`、`_EdgeIntensity`。

## PackArrowModifier

- Shader：`UI/MaterialModifiers/PackArrowModifier`
- 文件：`Assets/Shaders/UI/MaterialModifiers/PackArrowModifier.shader`
- 用途：打包箭头：通用边缘光与斜向扫光。
- 美术参数：该类附魔当前使用边缘光+斜向扫光，主要调 `_AuraColor`、`_PulseSpeed`、`_PulseStrength`、`_SweepSpeed`、`_SweepWidth`、`_SweepIntensity`、`_EdgeIntensity`。

## PeriodArrowModifier

- Shader：`UI/MaterialModifiers/PeriodArrowModifier`
- 文件：`Assets/Shaders/UI/MaterialModifiers/PeriodArrowModifier.shader`
- 用途：周期箭头：通用边缘光与斜向扫光。
- 美术参数：该类附魔当前使用边缘光+斜向扫光，主要调 `_AuraColor`、`_PulseSpeed`、`_PulseStrength`、`_SweepSpeed`、`_SweepWidth`、`_SweepIntensity`、`_EdgeIntensity`。
