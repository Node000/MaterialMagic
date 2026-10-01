# 「测试数据收集」弹窗与设置开关：UI 需求与文案（2026-10-01 / 第二版）

> 本版按用户 2026-10-01 的第二次调整重写：**登录不再依赖同意**（TapTap 平台默认账号，所有玩家都自动登录），同意只控制**是否采集测试数据**；弹窗改为「测试数据收集」栏目、两个按钮、不使用未成年专用语气、**游戏内不显示隐私政策**。
> 方案与决策见 `plan/隐私授权与隐私政策软化改造.md`；政策正文见 `Assets/Docs/隐私政策全文（登录埋点版）.md`。
> **代码不写文案、不写颜色**：下列文字与配色请由美术在 Scene 里设置，代码只做 Inspector 绑定与显隐。

## 一、首启询问弹窗（Scene：`Assets/Scenes/StartScene.unity`）

**已在场景里搭好**（2026-10-01 改造，代码不再生成弹窗）：`StartMenuCanvas/MenuContentRoot/TestDataConsentPanel`，`PrivacyConsentPanel` 与 `StartScene/GameInitializer.privacyConsentPanel` 均已绑定，`buildFallbackPanelWhenUnbound = false`。

```
TestDataConsentPanel（全屏容器，保持 active，逻辑组件挂在它上面）
├─ Overlay                      ← dimOverlay：全屏暗色遮罩（raycastTarget=true，挡住后面菜单的点击）
└─ PopupDragonWindowBackground  ← panelRoot：美术窗框（Shadow / Frame / TitleBar / Content）
   └─ Frame/Content
      ├─ Title        「测试数据收集」   key ui.test_data.title
      ├─ Body         正文（三段）        key ui.test_data.body
      ├─ AgreeButton  「同意」            key ui.test_data.agree
      └─ RejectButton 「不同意」          key ui.test_data.reject
```

- 起始状态：容器 active、`Overlay` 与窗框 **inactive**（运行期由 `Show()` 打开；`Hide()` 会连遮罩一起关，不会残留全屏 Image 吞点击）。
- 素材来源（均为场景内已有美术件，风格一致）：窗框复制自 `SaveSlotPanel/PopupDragonWindowBackground`，标题复制自它的 `Content/Title (2)`，正文复制自 `ExitConfirmPanel/Text`，两个按钮复制自 `StartConfigPanel/StartConfigActionButtonGroup/{ConfirmButton, CancelButton}`（游戏统一的“文字 + 波浪下划线”按钮样式；源节点在场景里是隐藏的，复制后已置为 active）。窗框尺寸暂为 820x720。
- **美术可以自由改**：窗框大小/位置、标题与正文字号与配色、按钮样式、间距——**只要保留这五个节点的组件与引用**（`Title`/`Body` 上的 `LocalizedTMPText` 键、两个按钮上的 `Button`）。改完请在 Play Mode 里点一次两个按钮确认。
- 文案走本地化：`Assets/Resources/Data/Localization/{zh-CN,en-US}_UI.json` 里的 `ui.test_data.{title,body,agree,reject}`。

### 1. 控件与绑定（已接好，供核对）

| 控件 | 说明 | 绑定字段 |
| --- | --- | --- |
| 半透明全屏遮罩 | 背景压暗。若与面板是**兄弟**节点，必须拖进来一起开关（否则关面板会留下全屏 Image 吞点击） | `dimOverlay` |
| 面板根节点 | 代码只 SetActive | `panelRoot` |
| 标题 TMP | 「测试数据收集」 | 美术自定（代码不写） |
| 正文 TMP | 见下文案，约 80 字，一屏不滚动 | `bodyText` |
| 主按钮 | 「同意」 | `agreeButton` |
| 次按钮 | 「不同意」 | `rejectButton` |

**不要放隐私政策按钮**：2026-10-01 决定游戏内不提供隐私政策入口；`policyButton` / `policyUrl` 字段保留但代码不绑定。

### 2. 文案终稿（请照抄）

**标题**

```
测试数据收集
```

**正文**（三段，段间空一行）

```
欢迎游玩《有氧地下城》！

我们会收集您在游戏过程中的道具选取、事件选择、通关数据等游戏内数据，来辅助我们进行内容开发与平衡性调整。

您随时可以在设置界面关闭这一功能。
```

**按钮**：主 `同意` ／ 次 `不同意`（两者都会继续游玩）

### 3. 文案约束

- 项目 UI 字体 `FZG_CN SDF` **缺这些符号字形（会渲染成方框）**：`「」`、`〈〉`、全角 `～`、`・`、`◆`、`※`、`★`。可用：`“”`、`‘’`、`《》`、`【】`、`、`、`。`、`，`、`·`、`•`、`●`、`—`、`…`、ASCII `~`、`（）`、`？`。
- 改完文案请在 Play Mode 截图确认，并看控制台有无 `The character with Unicode value \uXXXX was not found in the [FZG_CN SDF] font asset` 警告（`TMP.HasCharacter()` 判断不可靠）。

## 二、设置面板的「测试数据收集」开关（Scene：`StartScene` 的 `StartSettingsPanelUI`）

作用：给玩家随时关闭/重新开启的入口——弹窗结尾那句「您随时可以在设置界面关闭这一功能」指的就是它。

### 1. 需要美术做的

> **现状（2026-10-01 已加过渡版本，可直接用）**：`StartScene/StartMenuCanvas/MenuContentRoot/SettingsPanel` 下已新增
> `TestDataLabel`（LocalizedTMPText key `ui.start_settings.test_data`，回退「测试数据收集」）与
> `TestDataToggle`（复用 MusicSlider 的轨道/填充素材做的勾选框：整行 360x44 可点，勾选框 56x56，已挂 `AnalyticsConsentToggleUI` 并绑定 `sharingToggle`）。
> 为容纳这一行：面板高度 520→680，`CloseButton` 由 y=-175 下移到 y=-290。
> **美术出正式样式时只需替换/重排这两个节点，逻辑与绑定不用改**（保留组件与引用即可）。

1. 在设置面板里加一行：文字「测试数据收集」+ 一个 `Toggle`；
2. 把 `Toggle` 拖到组件 `AnalyticsConsentToggleUI` 的 `sharingToggle`；
3. 可选：为「开 / 关」两种状态各做一个视觉对象，拖到 `onStateRoot` / `offStateRoot`（代码只切显隐、不写颜色）。

### 2. 语义（代码已实现：`Assets/Scripts/Platform/AnalyticsConsentToggleUI.cs` + `GameInitializer.ApplyConsentChange`）

| 操作 | 行为 |
| --- | --- |
| 打开 | 记录同意；我们自己的上报**立即恢复记录**。若本次启动时 SDK 是按“不采集”初始化的（首启未决、或曾被拒绝），接下来的事件会先**落盘**到 `persistentDataPath/Save/analytics_pending.jsonl`，**下次启动自动补发**（补发的事件带 `replayed=true`） |
| 关闭 | 记录拒绝；我们自己的上报**立即**停发，并**删除未上报的本地待发数据**；TapDB 自带的启动/时长统计到**下次启动**才彻底停 |
| 打开设置面板时 | 开关状态自动按真实状态回填（场景里美术存成"开"也不会误写同意） |
| 与登录的关系 | **无**：无论开关怎么动，TapTap 登录都不受影响，不会要求重新登录或登出 |

## 三、流程总览（2026-10-01 第三版：登录在弹窗之前）

```
启动 → Init(enableTapTapEvent = 上次是否同意) → 自动登录（静默 / 授权页 / 取消）
├─ 首次启动：登录流程结束后弹「测试数据收集」
│   ├─ 同意   → 记录同意；我们自己的上报立即开始记录。本次 SDK 是按“不采集”初始化的，
│   │            所以事件先落盘（analytics_pending.jsonl），下次启动自动补发（replayed=true）
│   └─ 不同意 → 记录拒绝；不采集、也不留任何本地待发数据（照常玩）
├─ 之后启动：按上次的选择直接初始化（同意＝采集 + 补发上次落盘；拒绝＝不采集），不再弹窗
└─ 设置界面「测试数据收集」：关 → 自己的上报立即停 + 丢弃未上报的本地数据
                              开 → 自己的上报立即恢复（本会话原生未开则落盘，下次启动补发）
```

> 为什么不能“运行期改原生采集”：安卓/iOS 的 `enableTapTapEvent` 只在 Init 时交给原生 SDK（`TapCoreMobile.Init` 把配置序列化进 bridge），C# 侧既读不到也写不了，重复 Init 也不会刷新它；所以“打开”方向的数据用**落盘补发**兜底——晚一期到后台，但一条不丢。

## 四、验收步骤（真机）

1. **首次启动**：清应用数据 → 弹「测试数据收集」；点「不同意」→ 能正常游玩、TapTap 登录照常、后台收不到数据、**重启不再弹**；点「同意」→ 后台能收到事件与设备属性。
2. **登录独立性**：无论选同意还是不同意，启动后都应出现 TapTap 登录（首次授权可取消，取消后游客继续玩）。
3. **设置开关**：关闭 → 本次不再上报我们自己的事件；重启 → 完全不再采集；再打开 → 记录同意，下次启动起采集。
4. **点击可用性**：用 `EventSystem.RaycastAll` 确认屏幕中心最上层命中的是按钮本身（防遮罩/文字吞点击）；关闭面板后中心点不应再命中全屏遮罩。
5. 编辑器内走不到这条链路（`runTapTapInEditor=false` 时 `EffectiveBackend=None`，设计如此）：编辑器只验 UI 与显隐。
