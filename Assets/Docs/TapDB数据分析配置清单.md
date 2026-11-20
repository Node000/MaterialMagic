# TapDB 分析查询指南

代码已有run/node/battle/magic/event/shop等自定义事件调用；旧“事件只登记未接入”的前置已失效。本地登记表与后台接收状态分别管理；本轮没有访问后台或发送事件。

| 分析目标 | 主要事件/字段 | 解释边界 |
| --- | --- | --- |
| 单局耗时/难度 | run_end的run_seconds、result、ascension、start_config_id | 不用SDK会话时长替代单局时长 |
| 失败位置/敌人 | battle_end的result、level_id/step、defeat_source_enemy_id | 先核对真实payload命名与结果大小写 |
| 道具来源/时机 | magic_acquire的magic_id、source、step、rarity、price | 图鉴/起始获得与购买可能不同来源 |
| 事件选择 | event_options_shown、event_option_resolved、event_no_match | 用同局/节点上下文关联，别跨局拼漏斗 |
| 商店经济 | shop_purchase、shop_leave、price、total_spent、is_undo | 撤回/重进可能改变统计口径 |
| 对局关联 | run_id及chapter_id/level_id/step | BuildPayload不自动替所有调用补完整上下文 |

先从AnalyticsEvent/AnalyticsProperty、RunAnalyticsLedger.Context及实际Emit调用确认字段与类型，再看本地event.csv/eventProp.csv。报表具体字段须以当前入库payload验证；不同事件的result值不能假定完全统一。

replayed表示补发，时间可能是补发时刻；长字符串检查truncated标记。SDK预置启动/游玩/登录事件在SDK启用条件下才可能出现，不从登记文件推断每次启动都有。未同意则不初始化TapSDK；开关关闭与当前原生会话限制见平台模块。

后台只读查询与写入按任务授权；更新元数据、实时监测、上传CSV或发探针事件不属于本轮文档维护。历史后台验收结果在恢复档，不能当当前线上证明。维护方式见 `Assets/Docs/埋点后台录入表.md`。
