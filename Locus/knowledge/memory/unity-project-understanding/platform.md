---
id: kd_fd230645-217d-4edf-9e9f-0560c9a568d4
injectMode: inherit
injectAgents:
- '*'
aiEditMode: inherit
---

# 平台启动、TapTap与埋点

## 启动配置

`Assets/Scripts/GameInitializer.cs` 仍负责SDK/统计生命周期与Unity适配。`Assets/Scenes/StartScene.unity`配置为TapTap backend、enableAnalytics=true、requirePrivacyConsent=true、autoLoginOnStartup=true、runTapTapInEditor=false、channel=default；Editor通常EffectiveBackend=None，不等于真机关闭SDK。

TapSDK core/login当前4.10.9，Steam为条件编译分支。凭据来自GameBackendLocalCredentials和本地配置；`Assets/Resources/LocalSettings/`、`LocalSafe/`始终忽略，知识不复制密钥/完整设备账号标识。

`Assets/Scripts/Simulation/PlatformPolicy.cs` 不引用SDK/PlayerPrefs/GameInitializer。正式GameInitializer已共享三个纯判定：NeedsConsentPrompt、CollectionAllowedAtInitialization、延迟登录前CanAutoLogin。现有PlatformPolicySession/Decision/IPlatformPolicyAdapter可供脚本/fake测试，但正式启动/设置尚未整体委托这个session，不能声称所有异步生命周期已统一。

PrivacyConsentGate先问选择；按当前requirePrivacyConsent=true配置，拒绝不初始化整个TapSDK，也不自动登录。初始化gate与延迟登录guard不同：关闭requireConsent可允许初始化，但TapTap自动登录仍复核真实Agreed、已初始化且未登录。TapTapAuthService等待就绪并支持取消，账号UnionID优先/OpenID回退；不用policy单测证明SDK实际登录。

运行中关闭开关停止项目自有上报、清待发，不立即登出，也不能证明原生SDK本会话停止。下次启动依同意决定初始化；重新开启但服务未就绪时可能本地暂存后补发，本地写入不等于入库。真实SDK、持久设置、延迟协程和回调仍由适配层执行，Simulation不调用这些外部效果。

PlatformPolicyTests已纳入268/268通过的最终合并EditMode批次，仅验证纯判定/状态机，不代表真机SDK、网络、隐私窗口点击、后台入库或发布验收。

## 事件体系

`Assets/Scripts/Analytics/AnalyticsEvent.cs`、AnalyticsProperty.cs定义名称；RunAnalyticsLedger.Context与HandSystemUI.Emit*等实际调用组装上下文；AnalyticsService负责开关/上下文/队列，AnalyticsSink处理输出。BuildPayload自动补is_dev，不能假设它会替所有调用补全部公共字段。长字符串有截断处理；分析时检查截断标记和replayed。

代码已经有run/node/battle/magic/event/shop等调用；旧“32事件只登记未接入”失效。但每个定义都有调用、调用必达、后台必收是三件事，具体改动须沿调用者验证。

本地登记文件为 `Assets/Docs/event.csv`、eventProp.csv、userProp.csv；维护说明 `Assets/Docs/埋点后台录入表.md`。它们不是后台实时快照。本轮不访问后台、不导入、不发测试事件；过去验收记录在历史恢复档，不能当当前线上状态。

诊断先看当前payload的字段名/类型、同意与SDK就绪、实际调用是否到达，再检查元数据关联。单看错误详情不足以判全部丢失；需结合真实入库结果/概览与一次字段查询。只读查询仍须避免输出敏感标识，写后台须本次明确授权。

## 发布资料

政策/UI行为事实见 `Assets/Docs/隐私授权UI需求与文案.md`；政策起草文件不代表已发布或法律审核通过。不从SDK能力推断本游戏采集范围，不把登录成功等同实名认证/防沉迷完成。发布日期、主体、删除流程与服务端保留期限需运营证据。

SceneTransitionManager平台宏Android/iOS选择PE，Editor可forceMobileSceneInEditor；当前Build Settings的PC禁用限制见PROJECT。构建窗口Android重绘和构建后刷新曾崩溃，先看Editor.log判成功与崩溃先后，别无依据重写游戏代码。
