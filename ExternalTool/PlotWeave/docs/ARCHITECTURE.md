# 架构与移植

## 1. 分层

```text
constants ──► rng
    │          │
    ▼          ▼
 geometry ──► ports ──► rules
    │                     │
    └────────┬────────────┘
             ▼
          project ◄──── data/sample-project
             │
      ┌──────┴────────┐
      ▼               ▼
  generator      validate-map
      └──────┬────────┘
             ▼
       serialization

registry ◄──── generator / UI / plugins
```

依赖方向只有一个：**core 不依赖 UI，UI 依赖 core**。

- `src/core/*` 不读取 `window`、`document`、`localStorage`、Canvas 或任何浏览器 API，因此可以在 Node 中直接导入、做批量生成或嵌入别的前端。
- `src/ui/*` 负责状态、编辑、绘制和交互，不实现地块规则。
- `src/data/sample-project.js` 只是示例数据，不参与运行时逻辑。
- `plugins/*` 只通过 `registry` 接触核心，不修改核心源码。

## 2. 一次生成的调用链

```text
AppStore.generate()
  → registry.getGenerator(profile.generator)
  → generator.generate(project, { profile, seed })
      → planQuotas()         按区域配额
      → pickStart()          放起始地块
      → 端口生长循环         门 / 交叉 / 跨区种子
      → assignSubRooms()     按种类池与密度挂子房间
      → computeMapStats()
      → validateMap()
  → store 保存地图、选择地图、发 change 事件
  → Sidebar / Inspector / MapCanvas 重绘
```

生成器不依赖 UI。`registry` 让「标准生成器」和插件生成器使用同一个调用契约。

## 3. 状态与历史

`AppStore` 持有：

```js
{
  project,          // 工程（区域、地块种类、子房间种类、目录、方案）
  maps: [],         // 最多 12 张地图
  currentMapId,
  selection,        // { type, id }
  ui: { tab, filter, tool, zoom, showGrid, ... }
}
```

- 所有工程修改通过 `commitProject(mutator, { label })`；地图修改通过 `commitMap(...)`。
- 提交前会做一次 `normalize + validate + save`，并压入最多 60 条历史。
- 撤销/重做恢复的是整个工作区快照，因此删除地图、导入 Bundle 等操作也能撤销。
- 工作区写入 `localStorage['plotweave:workspace:v1']`。格式不兼容时会回退到示例工程，而不是崩溃。
- 明暗主题由 `src/ui/theme.js` 独立管理，写入 `localStorage['plotweave:theme:v1']`；它是显示偏好，不进入工程、地图或 Bundle。
- 窄屏布局由 `src/ui/mobile.js` 的 `MobileShell` 管理：底部导航、`sidebar` / `inspector` 上拉抽屉、遮罩与滚动锁定。它只操作 DOM 与面板状态，不修改工程或地图数据；桌面三栏布局不受影响。

## 4. 地图坐标与绘制

- 世界坐标以「格」为单位，`x` 向右、`y` 向下。
- 画布视图 `{ scale, offsetX, offsetY }`，屏幕坐标 = `offset + world * scale`。
- `computeFit()` 使用 `mapBounds(plots)`，不是整个工程网格，所以小地图不会被大网格缩成一点；窄屏使用更小的边距，让地图尽量占满画布。
- 触摸交互：单指拖动平移、双指以中点为锚捏合缩放、轻点选择、320ms 内双击聚焦；`touch-action: none` 阻止页面滚动干扰。鼠标滚轮与拖拽路径保持原样。
- 绘制顺序：背景 → 网格 → 区域色底 → 地块格/廊道 → 子房间 → 内容点 → 出生点 → 地块名 → 扩展图层。
- `toPngBlob()` 用 2x 离屏 Canvas 重放 `drawScene(..., { exportMode: true })`，导出图不带 UI 选中态，但保留标题和种子水印。

## 5. 关键设计决定

| 决定 | 原因 |
| --- | --- |
| 地块是 1–5 格直线条带 | 与「层级中的地点」概念一致，又能用简单几何做确定性生长 |
| 门与交叉分开 | 门是相邻格之间的侧向连接；交叉是共享格，二者的校验、绘制和容量语义不同 |
| 未建 link 的相邻格视为墙 | 避免视觉相邻被误认为连通，保证网络语义明确 |
| 区域只做分类与配额 | 区域不占格，地图不会出现「区域边界」这种额外结构 |
| 生成必须从现有接口长出 | 每张图天然连通，不需要生成后再修补 |
| 种子驱动所有随机 | 设计可复现，方便分享与回归测试 |
| 内容只存目录 ID | 玩法层不需要进入地图数据结构，保持工具中立 |
| 注册表而不是 if/else | 生成器、图层、导出器、校验器都能被插件替换/追加 |

## 6. 移植

### 6.1 作为静态站点

复制整个 `PlotWeave/` 到任意静态服务器；入口是 `index.html`，不需要构建。

### 6.2 嵌入其他 Web 应用

只复用核心：

```js
import { createDefaultProject } from './PlotWeave/src/core/project.js';
import { generateMap } from './PlotWeave/src/core/generator.js';

const project = createDefaultProject();
const map = generateMap(project, { profileId: 'standard', seed: 'DEMO-1' });
```

核心模块是原生 ES Modules，没有路径别名、没有打包器宏、没有 Node 内置模块。

### 6.3 换一套 UI

实现自己的 `MapCanvas` 即可。需要的最小数据是：

- `map.plots`：几何 + 种类 + 内容 + 子房间；
- `map.links`：门/交叉；
- `map.spawn`；
- `project.regions` / `project.plotKinds`：颜色、名称、规则。
- `project.plotGroups`：只服务结构树收纳，可用 `parentGroupId` 嵌套；地块用 `plotKind.groupId` 归组，不进入生成算法。

`src/ui/canvas.js` 的 `drawScene()` 可以作为一个可读的参考实现。

### 6.4 工具链

- 浏览器：任意支持 ES Modules 与 Canvas 2D 的现代版本。
- Node：只用于测试、示例导出和静态服务器，要求 18+。
- 没有 npm 依赖，不需要 `npm install`。

## 7. 失败与边界

- 生成器会尝试多个派生种子；返回「地块最多、错误最少」的一次，并在 `map.issues/warnings/errors` 中保留问题。
- 地图校验失败不会吞掉地图：仍然返回结果，让设计者在检视器里看到问题并修正工程。
- 导入 JSON 永不抛异常；格式错误会返回 `{ ok:false, errors }`，UI 用文本查看器展示。
- 当前没有撤销跨页持久化、多人协作、服务端存储或版本合并；这些不属于设计工具的核心范围。
