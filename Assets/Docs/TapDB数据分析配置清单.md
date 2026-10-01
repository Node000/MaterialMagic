# TapDB 数据分析配置清单

用途：告诉你在 TapDB 后台「做什么分析、用什么模型、配哪些事件与属性、回答什么问题」。
前置：已导入 `event.csv` / `eventProp.csv` / `userProp.csv`（35 个自定义事件 + 135 个事件属性 + 10 个设备属性）。

> 技朮提示：事件属性里的 `replayed=true` 表示“上次启动未上报成功、本次补发”的数据（其 `#ts` 是补发时刻而不是发生时刻）。做时长/漏斗类报表时建议先排除或单独标注，避免把补发数据当成新的时间点。

---

## 0. 先看清数据边界（最重要）

| 数据 | 现在有没有 | 说明 |
| --- | --- | --- |
| 启动 / 游戏时长 / 账号登录 / 登出 / 崩溃 | ✅ 已有 | TapDB **预置事件**自动上报：`device_login`（启动）、`play_game`（带 `duration`，单位秒，退出或切后台时上报）、`user_login`、`disconnect`、`exception_crash` / `exception_error` |
| 设备属性 | ✅ 已有 | `app_version` / `platform` / `channel` / `region` / `language` / `is_dev` / `save_slot` / `ascension_max` / `tutorial_done` / `build_type` |
| **对局数据**（关卡/战斗/道具/事件/商店/结算） | ❌ **还没上报** | 那 35 个自定义事件尚未挂进游戏流程（代码侧 P0-B 未做）。**你最初提的四个需求现在都还分析不了** |

所以：**现在先做第 1 节的 5 张报表**（验证埋点 + 拿到基础画像），**第 2 节的 8 张报表要等 P0-B 落地后才有数据**。

---

## 1. 现在就能做的 5 张报表

### R1. 新手时长分布（回答"多少人是低时长用户"）
- 模型：**分布分析**（或事件分析的"数值分布"）
- 事件：`play_game`，数值字段 `duration`
- 分组：按 `build_type` / `platform` 拆分
- 分档建议：0–300s / 300–900s / 900–1800s / >1800s
- 产出：低时长用户占比；后续接上对局事件后，这张表就是"低时长流失"的分母

### R2. 启动→登录转化（回答"自动登录有没有问题"）
- 模型：**漏斗分析**
- 步骤：`device_login` → `user_login`（窗口期 1 天）
- 拆分组：`platform`（安卓/iOS）、`build_type`
- 产出：有多少人启动后被记到账号维度（其余是匿名设备维度）

### R3. 留存（回答"次日/7 日还在不在"）
- 模型：**留存分析**
- 初始事件：`device_login`；回访事件：`device_login`
- 口径：按**设备**（TapDB 支持设备/账号两种对象；未登录玩家也能算）
- 产出：次日/3 日/7 日留存曲线——这是判断"整体留不住"的第一张图

### R4. 崩溃与低时长交叉（排查"是不是崩溃把人赶走了"）
- 模型：**事件分析** + 筛选
- 事件：`exception_crash` 的人数/次数
- 交叉：按 `duration` 分档（用事件分析的数值维度）或先建"崩溃设备"用户分群，再看这群人的 `play_game` 时长分布
- 产出：异常退出的原因归因（崩溃 vs 主动退出）

### R5. 版本/渠道对比（回答"不同包/渠道行为差异"）
- 模型：**事件分析**
- 事件：`device_login`（启动人次）、`play_game`（时长）
- 拆分组：`app_version`、`channel`、`build_type`
- 产出：版本迭代前后、渠道用户行为差异

> 建好后**保存为报表 → 添加到看板**，建议先建一个"**基础总览**"看板把这 5 张放一起。

---

## 2. P0-B 落地后要建的 8 张报表（你的真实目标）

| # | 报表 | 模型 | 事件 | 关键字段 / 配置 |
| --- | --- | --- | --- | --- |
| R6 | **失败玩家时长分布** | 分布分析 | `run_end`（筛选 `result=Defeat`） | 数值 `run_seconds` 分档；拆分 `ascension` / `start_config_id` |
| R7 | **失败位置热力** | 事件分析 | `battle_end`（`result=defeat`） | 分组 `step`、`end_level_id`、`defeat_source_enemy_id`、`is_boss`/`is_elite` |
| R8 | **失败时的道具情况** | 事件分析 | `run_end`（`result=Defeat`） | 数值 `magic_acquire_count`、`magic_count`、`gold`；配合 R9 看拿到了什么 |
| R9 | **道具获取来源与步数** | 事件分析 | `magic_acquire` | 分组 `source`（奖励/商店/事件/休息/奖励关/起始）、`rarity`；数值 `step`、`price` |
| R10 | **节点级漏斗（劝退关卡）** | 漏斗分析 | `node_enter` → `node_exit` → 下一 `node_enter` | 用 `step` / `level_id` 作为拆分维度；失败率 = `battle_end(defeat)` ÷ `node_enter` |
| R11 | **事件选项选择情况** | 漏斗 + 事件分析 | `event_options_shown` → `event_option_resolved`（另看 `event_no_match`） | 分组 `option_ids` / `option_recipes`；无匹配率 = `event_no_match` ÷ `event_options_shown` |
| R12 | **商店经济** | 事件分析 | `shop_purchase`、`shop_leave` | 分组 `kind`；数值 `price` / `total_spent` / `gold_left`；`is_undo` 看撤回率 |
| R13 | **通关时的道具构成** | 事件分析 | `run_end`（`result=Victory`） | `magic_book_ids`、`magic_acquire_list`、`deck_count`；分组 `start_config_id` |

建议看板：
- 「**流失与失败归因**」= R1 + R6 + R7 + R8 + R10
- 「**内容与平衡**」= R9 + R11 + R12 + R13
- 「**基础总览**」= R2 + R3 + R4 + R5

---

## 3. 做分析前先花 5 分钟校验数据质量

| # | 检查 | 怎么做 | 期望 |
| --- | --- | --- | --- |
| 3.1 | 设备属性是否上报 | 用户精查 → 挑一个设备看它的属性 | 能看到 `build_type`/`platform`/`channel`/`save_slot`/`ascension_max`/`tutorial_done` |
| 3.2 | `play_game.duration` 是否合理 | 事件分析 → `play_game` 的数值字段 | 秒数接近你实际游玩时长（不是 0、不是天数级） |
| 3.3 | 时间是否正确 | 任意事件的时间列 | 与本地时间一致（注意后台时区设置） |
| 3.4 | 是否有未登记属性被丢弃 | 埋点管理 → 未识别/待登记属性 | 为空；若有，把名字发我，我补进 `eventProp.csv` |
| 3.5 | 自定义事件是否显示"未上报" | 配置 → 事件管理 | 35 个事件应为"未上报"（还没挂），这是正常的 |

---

## 4. 常见坑

1. **未登录 ≠ 没数据**：未登录玩家走**设备维度**，靠 `device_login` 的 `install_uuid`/`device_id` 聚合；不要以为"没登录就没有数据"。
2. **登录后历史数据会归到账号**：`SetUserID(unionId)` 之后，TapDB 会把该设备与账号关联，账号维度的历史会被补齐，属正常现象。
3. **`play_game` 的时长是"本次游玩"**（每 2 秒累加、退出/切后台时上报），不是"单局时长"；**单局时长要用我们自己的 `run_end.run_seconds`**。
4. **漏斗要设窗口期**：节点级漏斗建议 10–30 分钟，避免跨天把多次启动拼成一次漏斗。
5. **先别用属性做"卡点"结论**：在自定义事件接上之前，任何"玩家卡在第几关"的结论都不可靠。

---

## 5. 下一步

要拿到你在最开始提的四条需求（通关道具、事件选项、失败敌人、道具获取的步数与情景），**前置就是 P0-B：把 35 个自定义事件挂进游戏流程**。这一步不依赖后台，我可以直接开工；完成后我会在本地用 `analytics_events.jsonl` 先验证事件序列正确，再让你真机跑一局核对后台。
