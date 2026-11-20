# 当前缺口与未接入边界

依据当前代码、磁盘配置及本轮运行观察。以下记录当前限制，不自动实施玩法修复，也不把早期设计当现状。

## 构建/平台

- 当前磁盘 Build Settings 启用 StartScene 和 PE，PC 禁用；从菜单选择 PC 的源码路径存在，但这个列表不能支持完整 PC 构建流程。未改平台设置。
- PC 二层场景实际在 `Assets/SampleScene_PC_SecondFloor.unity`，未列入构建。EnterSecondFloor 仅有定义，没有常规流程调用者。
- 规则分离已完成 Unity 程序集编译 / 268项EditMode与独立宿主smoke；PC/PE隔离战斗、奖励、商店及真实射线交互已补验，详见运行验收报告；正式菜单/槽位、全面画面、真机、二层正式入口与后台未验。二层原型已委托纯Move/Turn/Reveal，不等于新增常规入口。

## 内容与节点

- 敌人 SO 覆盖 JSON；单改已有 SO 对应的 JSON 不会改变最终数据。全量导入反而会覆盖 SO 内容。
- 未注册道具 script 回退 MagicModel，ResolveCast 为空；未知敌人 ID 回退通用 EnemyModel；未知箭头附魔 script 返回 null。三种失败信号不同。
- ThornsBuffModel 类存在，但 BuffModel.Create 未注册 Thorns，AddBuff(Thorns) 当前创建无效果基类；本次逻辑 / 表现分离验证发现并记录，尚未修复。
- MagicData 无 element 字段，rarity 是现行字段。数据文件中的旧字段不能仅因仍存在就当有效。
- 遗物纯逻辑已实现（实例/计数、Buff和伤害联动、候选领取、v4存档），见 `Assets/Docs/遗物系统逻辑实现说明.md`。未接正常精英奖励、GainRelic事件case、既有商店商品/UI和二层门控；测试池不自动投放。
- 故障值与二层 Tag 商店没有对应运行系统；箭头基础强化有实现和 Debug 接口，但不等于二层正式入口已完成。

## 保存与额外 Boss

- SaveRunProgress 按 Map/Node 检查点去重，通常恢复到节点初始化附近，不是每一步中途状态；商店完整事务强制保存，自动奖励金币仍受整笔奖励事务guard保护，不能仅看Force调用就声称已落盘。奖励棋盘无坐标 / 已领取格子恢复协议，不能只存收益而重建新棋盘。
- seed+step按需生成并保存，并非预生成串；事件/休息恢复会重复生成/发牌耗流，鹿男玩法缓存未持久化且显示getter抽随机，敌人19/22/24初始随机意图在Battle绑定前走Unity.Random。仅审查，未修复；商店已存货架则直接恢复，未证简单load能免费重抽。详见 `Assets/Docs/运行随机数与防SL审查.md`。
- 共享完整回合 / 冻结 trace、合法命令、Run / 地图 / 节点会话、owner 上下文与 .NET 宿主已接入；不是视觉验收。回合初致死与亡语同归于尽仍沿既有终局时机，NextPlayerTurn 不额外保证存活。正式旧 JSON 不含内存 FullGraph 全部 packed/runtime；PendingChoice / 自动奖励收益期间禁止半事务持久保存，退出返回最近完整 checkpoint。脚本可用 GameSessionCheckpoint 重放恢复选择。
- 普通 SaveRunProgress/SaveRunProgressForce 没传 sceneName；EnterSecondFloor 显式保存二层场景名，后续普通保存存在覆盖为空的风险。未在文档重建中修改代码。
- 奖励→自动商店→节点完成现由 RunSession 统一提交，Boss 标记与推进不再依赖 UI 分支之后的旧代码；额外 Boss 真正流程与视觉仍需集成验收。
- RunManager 缓存单一章节 Boss 及敌人组，额外 Boss 格可能复用；不能从“配置三个 Boss 格”推断三套独立 Boss。

## UI/语言与平台服务

- 多处仍保留按名称查找、运行时兜底面板和代码颜色逻辑；这是当前兼容实现，不是允许新增时继续复制的规范。用户的场景优先、Inspector 绑定、颜色来源约束继续生效。
- reload再次出现domain backup加载错误，Play日志仍有linkedCards递归depth10警告；StartScene缺失脚本0、目标SO可加载，不等于提示已根治。另出现RewardOption2缺脚本警告，事后PE活跃场景全树missing=0，来源尚未定位；没有盲删组件或改GUID。
- LocalizedTMPText 有效 key 会覆盖场景 TMP 文本；移除组件可能让英文显示场景中文。文案源需要逐目标确认。
- 测试数据开关关闭后立即停项目自有上报，不等于本会话原生 SDK 已停或立即登出；发布说明不能夸大。
- 本地 event/eventProp/userProp CSV 与后台是否一致不能只靠旧记录证明；本次不访问后台、不发布政策、不补凭据。

新任务触及以上范围时，集中读取对应模块和实际函数，再决定最小修复。不要为“知识一致”静默改游戏行为。
