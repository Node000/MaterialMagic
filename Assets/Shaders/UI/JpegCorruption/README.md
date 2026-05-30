# UI JPEG Corruption Shader

精简版 UGUI 材质 Shader，面向 `RawImage` 使用；`Image` 仅建议使用 `Type = Simple`，且最好不要使用会进入图集的 Sprite。

## 资源

- Shader：`Assets/Shaders/UI/JpegCorruption/UI_JpegCorruption.shader`
- 默认材质：`Assets/Materials/M_UI_JpegCorruption_Default.mat`
- 示例场景：`Assets/Scenes/Demo_UI_JpegCorruption.unity`

## 使用

1. 给 `RawImage` 指定一张纹理。
2. 将 `M_UI_JpegCorruption_Default.mat` 挂到该 `RawImage` 的 Material。
3. 通过材质参数调整损坏效果。

`Intensity = 0` 时输出原图；默认 `PreserveAlpha = 1`，透明 PNG 会保留原始 alpha。`UseTime = 0` 且 `Seed` 固定时，结果保持稳定。

## 主要参数

- `Intensity`：总强度。
- `BlockSize`：以 `_MainTex_TexelSize` 换算的像素宏块大小。
- `BlockProbability`：每个宏块触发错位/损坏的概率。
- `MaxBlockOffset`：宏块采样偏移像素数。
- `RGBSplit`：RGB 通道分离像素数。
- `ChromaOffset`：简化色度偏移像素数。
- `LumaSteps` / `ChromaSteps`：亮度/色度量化级数。
- `LineJitter`：横向故障条带的水平偏移像素数。
- `Speed` / `UpdateRate` / `Seed` / `UseTime`：随机模式动画与稳定控制。
- `PreserveAlpha`：保留原始 alpha。
- `Saturation` / `Contrast`：损坏后色彩强化。

## 当前限制

- 不是真实 JPEG 编解码，只模拟 JPEG 损坏观感。
- 不保证 `Mask`、`RectMask2D`、`ScrollView`、TMP、`Image` 的 Sliced/Tiled/Filled 兼容性。
- 采样偏移使用拼贴式 wrap；`Image` 如果使用图集 Sprite，强偏移可能采到图集相邻区域。
