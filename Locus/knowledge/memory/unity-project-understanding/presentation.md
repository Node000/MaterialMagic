---
id: kd_ace1c41e-2054-4b73-865a-a550e5973c8c
injectMode: inherit
injectAgents:
- '*'
aiEditMode: inherit
---

# 演出、渲染与音频

## 战斗表现

HandSystemUI调用PlayerCastAnimatorUI、CastParticleEffectCaster/Player、EnemyViewUI、BuffPopupEffectController、FloatingTextUI等；规则目标与粒子目标应共享一次锁定结果。敌人行动/死亡队列时序见combat模块；动画尾部重叠不改变规则串行。

战斗的 `AfterMagic` 成功记录携带本次触发需求；`MagicTriggerFeedbackUI` 在 PC/PE 槽位上独立闪框并按需求逐层发出同图标的 UGUI 轮廓。两端共用 Shader 与波纹 Prefab，参数在各自槽位上；轮廓不新增规则或表现闸门，也不改变槽位层级/输入。

SpringLineHighlightUI、CrayonUI、材质特效和DOTween共同影响UI；颜色、尺寸和速度以Scene/Prefab已有配置为准，不用代码覆盖美术值。ItemWaterfallEffect/ItemFallingPiece保持Dynamic；先前“停稳转Static”会留下悬空碎片，性能改动优先池和统一Tick。

## Renderer与实验

URP 14.0.12；正式渲染配置从GraphicsSettings/QualitySettings所指RenderPipeline资产追踪，别仅按文件名判断当前Renderer。`Assets/Scenes/ShaderExperiments/ShaderArtLab.unity`隔离屏幕边线/蜡笔等实验；正式蜡笔表现使用 `Assets/Scripts/UI/CrayonUI/`。屏幕边线、模型折边、3D平面涂鸦和路径涂鸦是不同入口，不因名称相似互相替换。

`Assets/Scripts/VFX/`含TVRectGlitch、CrtScanLine、PvOnlyVhsFall、FisheyeFall、ScreenEdgeScribble、ModelEdgeScribble等；`Assets/Scripts/VFX/Scribble/`有独立作者/渲染工具。改Shader需看材质、RendererFeature和目标相机/场景，不能把实验Feature挂入所有正式Renderer。

## 音频

`Assets/Scripts/AudioManager.cs`为持久单例，StartScene的gameSfxClips序列化绑定音频；GameSfxId显式值保持兼容，追加不得重排。现有0..8含HandHover=8；CardPlay/ButtonClick等是NormalInteract别名。

PlayOneShot通路和变调源池不同：volumeDb转线性增益，再乘SfxVolume；变调源AudioSource.volume有上限。HandCardView.OnPointerEnter调用PlayHandHoverSfx，有时间限流与pitch抖动。连击挥击由HandSystemUI.PlayPlayerCastSwingSfx按连续施法变调，不从Combo文本推断有独立音色。

`Assets/Docs/SFX音量归一化.md`保留测量方法和导入注意，历史增益表不当实时值；调参从场景绑定读。替换素材需按导入后声道/Normalize口径重新测量，单片峰值合格不代表音乐+叠音主输出不削顶。本次文档重建不重测音频、不改导入器或混音参数。
