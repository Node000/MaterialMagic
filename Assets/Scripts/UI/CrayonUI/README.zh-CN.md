# Unity UI 程序化蜡笔背景

使用 uGUI 的 Canvas + RawImage。纹理由 Shader 实时计算，不需要外部图片。
示例实现纸张颗粒、定向断续笔触、两层颜料、轻微逐帧抖动和缓慢的压力变化。
这是动态涂色质感；如果需要沿路径逐笔绘制、永久保留笔迹，需要另外增加笔画数据和累计绘制。

## 接入

1. 将 ProceduralCrayon.shader 与 CrayonUIAnimator.cs 复制到项目的 Assets/CrayonUI 文件夹。
2. 创建一个 Material，把 Shader 设置为 UI/ProceduralCrayon。
3. 在 Canvas 下创建 UI > Raw Image，命名 CrayonBackground。
4. Canvas 先使用 Screen Space - Overlay。RawImage 的 RectTransform 设置为横纵 Stretch，四边偏移全部为 0。
5. 把 CrayonBackground 放在同一个 Canvas 的第一个子物体位置，让后面的 UI 绘制在它上方。
6. RawImage：Material 使用上一步材质；Texture 留空；Color 为不透明白色；UV Rect 为 (0, 0, 1, 1)；关闭 Raycast Target。
7. 给 RawImage 挂上 CrayonUIAnimator，进入 Play 模式查看动画与正确的宽高比。

Screen Space - Overlay 会画在游戏世界上方，适合菜单背景。要把背景放在游戏角色后面，应改用合适的 Camera/World Space Canvas 和相机、深度排序设置。

## 材质参数

| Inspector 参数 | 默认值 | 含义 |
| --- | --- | --- |
| Paper Color | 暖米白 | 纸底颜色；Alpha 控制整体不透明度 |
| Crayon A / B | 浅绿 / 深绿 | 两层颜料颜色；各自 Alpha 控制沉积量 |
| Stroke Scale | 5 | 增大后笔触更密、更细 |
| Stroke Angle | -18 | 第一层纹理采样坐标的旋转角度 |
| Second Layer Angle Offset | 47 | 两层纹理之间的夹角 |
| Pigment Coverage | 1.25 | 增大后填色更满 |
| Dryness / Paper Gaps | 0.8 | 增大后纸纹露底更明显 |
| Paper Grain Density | 500 | 沿 UI 高度的颗粒尺度；越大越细 |
| Paper Grain Contrast | 0.09 | 纸张自身明暗起伏 |
| Jitter | 0.0007 | 相对 UI 高度的单方向最大抖动幅度 |
| Hand Drawn FPS | 8 | 每秒更新笔触位移的次数；不限制整个游戏帧率 |
| Pressure Cycles Per Second | 0.12 | 颜料覆盖强度缓慢变化的频率 |
| Pattern Seed | 7 | 改变固定纹理布局 |

材质资产请在进入 Play 前调节。脚本在运行时创建独立材质；运行中可从 RawImage 的 Material 引用打开该运行实例调节，退出 Play 后不会保留这些修改。

## 原理

- 纸张坐标：`(uv - 0.5) * float2(width / height, 1)`，避免非正方形 UI 把颗粒拉长。
- 纸张颗粒：两级高频 value noise，保持坐标固定。
- 笔触：旋转坐标后，让噪声沿笔触缓慢变化、横跨笔触快速变化，例如 `Noise(p * float2(1.1, 38))`。
- 断续感：混入低频的覆盖变化，再用 Smoothstep 调整颜料沉积。
- 露底：纸张颗粒控制颜料附着；颜料稀少处露出纸色。
- 叠色：用另一角度的较深颜料再次混合。
- 动画：`floor(time * fps)` 控制微小随机位移，正弦函数控制轻微压力变化；纸张始终不动。

## UI 与性能说明

- 默认使用不受 Time.timeScale 影响的 Time.unscaledTime；关闭脚本的 Use Unscaled Time 可随游戏时间暂停。
- Shader 保留 uGUI 的 Stencil 属性、顶点颜色/透明度、矩形裁剪和 Alpha Clip。
- RectMask2D 使用硬边裁剪；此示例没有实现 RectMask2D 的 Softness 软边。
- 面向常规 Built-in / URP 的 uGUI Canvas 使用；它不是 SpriteRenderer 或 UI Toolkit Shader。具体版本与图形 API 仍需在项目中验证。
- 高频细节有导数过滤，缩小时会淡化，减少颗粒闪烁。
- 脚本只更新宽高比和时间，不逐帧创建 Texture2D 或上传像素。
- 即使 Hand Drawn FPS 是 8，Shader 仍随画面每帧执行。降低这一参数不会把 GPU 渲染开销降到每秒 8 次。
- 高分辨率移动设备上请测量 GPU 时间。如果成为瓶颈，可烘焙固定噪声，或按较低频率把颜料绘制到较小 RenderTexture，再让 RawImage 显示它；静态细纸纹可以另行保留。

## 官方参考

- RawImage：https://docs.unity3d.com/cn/2018.4/Manual/script-RawImage.html
- Unity 6 URP Canvas Shader Graph：https://docs.unity3d.com/cn/6000.0/Manual/urp/prebuilt-shader-graphs-urp-canvas.html
- Time.unscaledTime：https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Time-unscaledTime.html

使用 Unity 6 + URP 且偏好节点编辑时，也可以在 Canvas Shader Graph 中实现同样的纹理计算。

## 验证状态

已检查代码中的材质属性对应关系，并审阅生命周期和 UI 遮罩处理。尝试用本机 Unity 2022.3.48f1c1 在独立测试项目中编译及渲染，但编辑器批处理未能进入导入、编译阶段，因此没有获得 Unity 编译通过、运行通过或视觉验证结果。请在你的 Unity 版本和目标设备中导入确认。
