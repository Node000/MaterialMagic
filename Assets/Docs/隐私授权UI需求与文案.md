# 测试数据收集：当前行为与UI维护

行为入口：GameInitializer、Platform/PrivacyConsentGate、PrivacyConsentPanel、AnalyticsConsentToggleUI、TapTapAuthService。StartScene的GameInitializer配置TapTap backend、统计开启、要求同意、启动自动登录、Editor不跑TapSDK。

首启先询问，未同意则不初始化整个TapSDK，也不登录，仍可正常游玩；同意后初始化成功才自动登录。登录授权可取消。已有同意记录与PolicyVersion共同决定是否重问；文档日期不是自动提高版本的理由。

运行中关闭只保证项目自有统计立即停止并清待发，不立即结束当前账号态；已启动的原生统计本会话可能继续，下次启动按关闭状态不初始化。重新开启可能初始化/恢复服务；不要写成“关闭后所有SDK立即停”“拒绝仍自动登录”或“无需同意一直采集”。

UI按Scene已有面板/开关与Inspector绑定维护，PrivacyConsentPanel的panelRoot和dimOverlay需一起显隐。具体对象/覆盖值从当前Property Tree确认；不能因旧文档说已绑定就不检查。常显内容父级不能沿用SpringLineHighlightUI.hideOnAwake=true。

文案来源逐目标查看LocalizedTMPText/key/fallback，JSON为key/text。有效key会覆盖Scene TMP；删组件先确认英文。按钮样式/文案与颜色由美术配置，代码只负责行为，不运行时回写。真实点击检查RaycastAll/ExecuteEvents与关闭后遮罩，不只Invoke。

政策草稿保留于 `Assets/Docs/隐私政策（最新埋点审阅版）.md`，不是已发布/审核通过声明。新发布任务需按当次包与运营流程确认；本轮不发布政策、不访问后台、不改同意版本或美术资产。
