# 附魔 Shader 使用说明

本文档给美术使用，覆盖当前所有附魔视觉 Shader。

## 1. 文件位置

- Shader：`Assets/Shaders/UI/MaterialModifiers/`
- 材质预设：`Assets/Resources/Materials/MaterialModifiers/`
- 运行时加载：`MaterialModifierVisualUtility` 会按附魔脚本名加载同名材质，例如 `ChargeModifier` 会加载 `Resources/Materials/MaterialModifiers/ChargeModifier.mat`。

美术主要调整 `.mat` 材质，不需要改 Shader 代码。

## 2. 最推荐的调法

1. 在 Project 中打开 `Assets/Resources/Materials/MaterialModifiers/`。
2. 选择要调的附魔材质，例如 `ChargeModifier.mat`。
3. 在 Inspector 里调整参数。
4. 进入战斗界面查看对应附魔卡牌效果。

注意：运行时会复制材质实例，所以运行时调出来的数值不会自动保存。需要在 Edit Mode 直接改 `.mat` 资源。

## 3. 通用参数

### 不建议美术修改

- `_MainTex`：卡牌/箭头贴图，由 Image 自动传入。
- `_Color`：整体乘色，一般保持白色。
- `_ArrowDirection`：运行时根据素材方向覆盖，火=上、水=下、风=左、土=右。
- `_CopyCount`：多重箭头数量，BigArrow2/3/4 对应 2/3/4，一般不要改。
- `_AltTex1~4`：随机箭头使用的四张基础箭头贴图，运行时会自动设置。
- `_Stencil*`、`_ColorMask`、`_UseUIAlphaClip`：UGUI Mask/裁剪参数，不要改。

### 常用可调

- `_EffectSpeed`：动画速度。越大越快；建议 `0.6~2.5`，闪电/碎裂类可到 `4`。
- `_EffectStrength`：特效强度。越大越明显；建议 `0.2~0.75`，超过 `0.85` 容易影响箭头识别。
- `_AuraColor`：主发光色。注意：运行时通常会被 `MaterialModifierData.json` 里的 `lineColor` 覆盖，所以如果要稳定改颜色，应同时改数据表颜色。

## 4. 渐变参数

多数附魔材质都有渐变参数：

- `_GradientColor1~4`：渐变颜色。
- `_GradientPosition2`、`_GradientPosition3`：中间颜色的位置。
- `_GradientAngle`：渐变方向，单位是弧度；`0` 横向，`1.57` 约等于竖向，`0.785` 约等于 45 度。
- `_GradientScale`：渐变缩放，越大色带越密。
- `_GradientOffset`：整体偏移。
- `_GradientScrollSpeed`：渐变滚动速度。
- `_GradientIntensity`：渐变混合强度。`0` 表示不用渐变，`1` 表示很强。

建议：先只改 `_GradientIntensity` 到 `0.15~0.35`，确认不影响箭头可读性后再调整颜色和滚动。

## 5. 通用扫光类参数

`LinkedArrowModifier`、`OmniArrowModifier`、`PackArrowModifier`、`PeriodArrowModifier` 使用边缘光 + 斜向扫光：

- `_PulseSpeed`：呼吸速度。
- `_PulseStrength`：呼吸强度，建议 `0.05~0.25`。
- `_SweepSpeed`：扫光速度。
- `_SweepWidth`：扫光宽度，建议 `0.06~0.18`。
- `_SweepIntensity`：扫光亮度，建议 `0.25~0.8`。
- `_EdgeIntensity`：边缘光强度，建议 `0.25~0.75`。

如果画面太花，优先降低 `_SweepIntensity` 和 `_EdgeIntensity`。

## 6. 各附魔效果说明

### HalfArrowModifier

- Shader：`UI/MaterialModifiers/HalfArrowModifier`
- 材质：`Assets/Resources/Materials/MaterialModifiers/HalfArrowModifier.mat`
- 效果：半箭头，按当前方向切去半边，并用发光色强调切线。
- 主要调：`_EffectStrength` 控制切线/缺损存在感，`_EffectSpeed` 控制切线闪动速度。
- 建议：强度不要太高，避免看起来像箭头断裂错误。

### FragileArrowModifier

- Shader：`UI/MaterialModifiers/FragileArrowModifier`
- 材质：`Assets/Resources/Materials/MaterialModifiers/FragileArrowModifier.mat`
- 效果：易碎箭头，沿方向裂开，两片轻微错位并高亮裂缝。
- 主要调：`_EffectStrength` 控制裂缝和错位，`_EffectSpeed` 控制抖动。
- 建议：这是反馈强的特效，可以比普通附魔更亮，但不要让原箭头断成不可读。

### RepeatArrowModifier

- Shader：`UI/MaterialModifiers/RepeatArrowModifier`
- 材质：`Assets/Resources/Materials/MaterialModifiers/RepeatArrowModifier.mat`
- 效果：重复箭头，在原箭头后方叠一层半透明残影。
- 主要调：`_EffectStrength` 控制残影明显程度，`_EffectSpeed` 控制残影流动。
- 建议：残影太强会像多重箭头；如果和 BigArrow 混淆，降低强度。

### BigArrow2Modifier / BigArrow3Modifier / BigArrow4Modifier

- Shader：`UI/MaterialModifiers/BigArrow2Modifier`、`BigArrow3Modifier`、`BigArrow4Modifier`
- 材质：对应 `BigArrow2Modifier.mat`、`BigArrow3Modifier.mat`、`BigArrow4Modifier.mat`
- 效果：显示 2/3/4 个横向排列的箭头拷贝。
- 主要调：`_EffectStrength` 控制高光，`_EffectSpeed` 控制动态。
- 不建议改：`_CopyCount`，否则视觉数量会和玩法含义不一致。

### ProliferatingArrowModifier

- Shader：`UI/MaterialModifiers/ProliferatingArrowModifier`
- 材质：`Assets/Resources/Materials/MaterialModifiers/ProliferatingArrowModifier.mat`
- 效果：增殖箭头，箭头局部膨胀并带新生残影。
- 主要调：`_EffectStrength` 控制膨胀/残影，`_EffectSpeed` 控制生长节奏。
- 建议：适合偏绿色、生长感；强度过高会显得箭头变形。

### ReturnArrowModifier

- Shader：`UI/MaterialModifiers/ReturnArrowModifier`
- 材质：`Assets/Resources/Materials/MaterialModifiers/ReturnArrowModifier.mat`
- 效果：返回箭头，压暗原图并叠加回返箭头符号。
- 主要调：`_EffectStrength` 控制回返符号强度，`_EffectSpeed` 控制动态。
- 建议：主色通常保持白色或浅色，确保“返回”符号清楚。

### RandomArrowModifier

- Shader：`UI/MaterialModifiers/RandomArrowModifier`
- 材质：`Assets/Resources/Materials/MaterialModifiers/RandomArrowModifier.mat`
- 效果：随机箭头，在四种基础箭头贴图间循环渐变。
- 主要调：`_EffectSpeed` 控制切换速度。
- 不建议改：`_AltTex1~4`，运行时会自动填入四张基础箭头。

### RetainedArrowModifier

- Shader：`UI/MaterialModifiers/RetainedArrowModifier`
- 材质：`Assets/Resources/Materials/MaterialModifiers/RetainedArrowModifier.mat`
- 效果：保留箭头，横向故障偏移、扫描线和轻微色散。
- 主要调：`_EffectStrength` 控制故障感，`_EffectSpeed` 控制扰动速度。
- 建议：这是常驻可见效果，强度建议低一些。

### EternalArrowModifier

- Shader：`UI/MaterialModifiers/EternalArrowModifier`
- 材质：`Assets/Resources/Materials/MaterialModifiers/EternalArrowModifier.mat`
- 效果：永恒箭头，周期性反相闪烁，并出现循环光线。
- 主要调：`_EffectStrength` 控制闪烁亮度，`_EffectSpeed` 控制循环速度。
- 建议：避免太频繁强闪，容易抢走主要 UI 注意力。

### DoomModifier

- Shader：`UI/MaterialModifiers/DoomModifier`
- 材质：`Assets/Resources/Materials/MaterialModifiers/DoomModifier.mat`
- 效果：毁灭箭头，灰阶压暗、噪声和暗条纹。
- 主要调：`_EffectStrength` 控制压暗和噪声，`_EffectSpeed` 控制暗纹移动。
- 建议：可以偏灰黑，但不要把箭头主体压到看不清。

### TemporaryModifier

- Shader：`UI/MaterialModifiers/TemporaryModifier`
- 材质：`Assets/Resources/Materials/MaterialModifiers/TemporaryModifier.mat`
- 效果：临时箭头，整体呼吸式透明度和颜色漂移。
- 主要调：`_EffectStrength` 控制忽隐忽现程度，`_EffectSpeed` 控制呼吸速度。
- 建议：项目偏好动态生成后静态显示 UI 保持完全不透明；这里是 Shader 内部表现，仍要避免看起来像卡牌消失。

### LazyModifier

- Shader：`UI/MaterialModifiers/LazyModifier`
- 材质：`Assets/Resources/Materials/MaterialModifiers/LazyModifier.mat`
- 效果：懒惰箭头，纵向波浪形拖拽变形。
- 主要调：`_EffectStrength` 控制拖拽变形，`_EffectSpeed` 控制慢速摆动。
- 建议：速度可以慢，强度不要过高，避免箭头方向误读。

### VortexModifier

- Shader：`UI/MaterialModifiers/VortexModifier`
- 材质：`Assets/Resources/Materials/MaterialModifiers/VortexModifier.mat`
- 效果：漩涡箭头，中心旋涡扭曲和环形光。
- 主要调：`_EffectStrength` 控制旋涡扭曲，`_EffectSpeed` 控制旋转速度。
- 建议：强度过高会扭曲箭头方向，优先调颜色和光环，不要过度扭曲主体。

### SturdyModifier

- Shader：`UI/MaterialModifiers/SturdyModifier`
- 材质：`Assets/Resources/Materials/MaterialModifiers/SturdyModifier.mat`
- 效果：稳固，金属扫光和边缘硬光。
- 主要调：`_EffectStrength` 控制金属高光，`_EffectSpeed` 控制扫光速度。
- 建议：适合低速、低频、偏金属色。

### KindlingModifier

- Shader：`UI/MaterialModifiers/KindlingModifier`
- 材质：`Assets/Resources/Materials/MaterialModifiers/KindlingModifier.mat`
- 效果：引燃，像素火焰噪声从底部闪烁。
- 主要调：`_EffectStrength` 控制火焰高度和亮度，`_EffectSpeed` 控制火焰闪动。
- 建议：适合橙红色；过亮会和火素材本体混在一起。

### ChargeModifier

- Shader：`UI/MaterialModifiers/ChargeModifier`
- 材质：`Assets/Resources/Materials/MaterialModifiers/ChargeModifier.mat`
- 效果：充能，随机闪电折线。
- 主要调：`_EffectStrength` 控制闪电亮度，`_EffectSpeed` 控制闪烁频率。
- 建议：可以速度较快，但亮度不要常驻过曝。

### FlowModifier

- Shader：`UI/MaterialModifiers/FlowModifier`
- 材质：`Assets/Resources/Materials/MaterialModifiers/FlowModifier.mat`
- 效果：流动，水平流线和波纹高光。
- 主要调：`_EffectStrength` 控制流线亮度，`_EffectSpeed` 控制流速。
- 建议：适合蓝青色，保持柔和流动感。

### LiquefyModifier

- Shader：`UI/MaterialModifiers/LiquefyModifier`
- 材质：`Assets/Resources/Materials/MaterialModifiers/LiquefyModifier.mat`
- 效果：液化，水滴状高光和水平扭动。
- 主要调：`_EffectStrength` 控制水滴/扭动，`_EffectSpeed` 控制流动速度。
- 建议：比 Flow 更粘稠，可以稍慢一些。

### LinkedArrowModifier

- Shader：`UI/MaterialModifiers/LinkedArrowModifier`
- 材质：`Assets/Resources/Materials/MaterialModifiers/LinkedArrowModifier.mat`
- 效果：联结箭头，边缘光与斜向扫光。
- 主要调：`_PulseStrength`、`_SweepIntensity`、`_EdgeIntensity`。
- 建议：用连线感/能量链感时，优先调扫光宽度和亮度。

### OmniArrowModifier

- Shader：`UI/MaterialModifiers/OmniArrowModifier`
- 材质：`Assets/Resources/Materials/MaterialModifiers/OmniArrowModifier.mat`
- 效果：全向箭头，边缘光与斜向扫光。
- 主要调：`_PulseStrength`、`_SweepIntensity`、`_EdgeIntensity`。
- 建议：颜色可偏紫，强调万能/稀有感。

### PackArrowModifier

- Shader：`UI/MaterialModifiers/PackArrowModifier`
- 材质：`Assets/Resources/Materials/MaterialModifiers/PackArrowModifier.mat`
- 效果：打包箭头，边缘光与斜向扫光。
- 主要调：`_PulseStrength`、`_SweepIntensity`、`_EdgeIntensity`。
- 建议：如果需要更“封装/包裹”感，增强边缘光而不是提高整体亮度。

### PeriodArrowModifier

- Shader：`UI/MaterialModifiers/PeriodArrowModifier`
- 材质：`Assets/Resources/Materials/MaterialModifiers/PeriodArrowModifier.mat`
- 效果：周期箭头，边缘光与斜向扫光。
- 主要调：`_PulseSpeed`、`_PulseStrength`、`_SweepSpeed`。
- 建议：通过节奏感表达“周期”，不要只靠高亮。

## 7. 调参原则

- 箭头方向和轮廓可读性优先，特效只能增强，不能盖住主体。
- 常驻效果不要高频闪烁；强闪只适合短反馈。
- 先调 `_EffectStrength`，再调 `_EffectSpeed`，最后再调渐变。
- 如果多个附魔看起来太像，优先区别“运动方式”，其次区别颜色。
- 颜色如果要和游戏数据一致，改材质颜色后还要检查 `Assets/Resources/Data/MaterialModifierData.json` 的 `lineColor`。
