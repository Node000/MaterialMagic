# PlotWeave · 类银河城地牢地图编辑器开发文档

> 版本：v2.1（链式条带 / 四区预设 / 松散度 / 非环 / 出入口格升级）  
> 目标：在保留 PlotWeave 零依赖、纯静态 ES Modules + Canvas 2D 与 `core/ui` 严格解耦架构的基础上，打造面向**类银河城地牢探索**的地图编辑器，支持**条带模板制作与导出**、**三区域三角单层地图生成**，并为**完整地图生成**预留扩展位。

---

## 1. 功能总览与三大工作区模式

编辑器顶栏提供三大核心模式切换：

| 模式 ID | 名称 | 状态 | 核心功能 |
| --- | --- | --- | --- |
| `templates` | **条带模板** | 完整实现 | 全局格子类型数值配置、2D 多格异形条带画布编辑、按随机配置一键生成草稿并手工精修保存、模板库 JSON 导入/导出 |
| `floor` | **单层地图** | 完整实现 | 三区域三角形单层生成方案配置、总资源/总难度区间控制（留空自动计算）、节点图距离与条带/敌人等级计算与可视化、单层地图 JSON / 2x PNG 导出 |
| `world` | **完整地图** | **留空占位** | 保留顶栏入口、占位界面与 `world-placeholder.js` 接口，暂不实现具体逻辑 |

---

## 2. 核心概念与数据模型

### 2.1 全局格子类型配置（`cellTypes`）

每种格子类型仅包含两个数值属性：**资源值（`resource`）** 与 **难度值（`difficulty`）**（无冗余“价值”属性）：

```json
{
  "id": "combat",
  "name": "战斗",
  "short": "战",
  "icon": "⚔",
  "color": "#d97b6c",
  "resource": 5,
  "difficulty": 10,
  "supportsOffset": true,
  "defaultOffset": 0
}
```

内置默认格子类型：

| `id` | 名称 | 图标 | 默认资源值 | 默认难度值 | 支持难度偏移 |
| --- | --- | --- | :---: | :---: | :---: |
| `empty` | 通道 / 空 | `·` | 0 | 0 | 否 |
| `combat` | 战斗 | `⚔` | 5 | 10 | **是**（默认 `0`） |
| `elite` | 精英 | `★` | 15 | 25 | **是**（默认 `+2`） |
| `shop` | 商店 | `⚖` | 20 | 0 | 否 |
| `reward` | 奖励 | `◆` | 25 | 0 | 否 |
| `event` | 事件 | `?` | 10 | 5 | 否 |
| `spawn` | 出生点 | `⚑` | 0 | 0 | 否 |
| `rest` | 休整 | `✚` | 15 | 0 | 否 |

- 支持在编辑器内全局修改任意格子类型的资源值、难度值、名称、颜色、图标，或新增自定义格子类型。
- 修改全局配置后，所有未启用手动覆写的条带模板自动联动重算其总资源值与总难度值。

---

### 2.2 链式多格条带模板（`StripTemplate`）

条带为**无矩形（无 2×2 实心块）、无分叉、明确首尾的单链多格房间**（直线或 L/Z/S/U 折线），样例条带集中在 **4–6 格**（4 格较少，5–6 格为主）。

```json
{
  "id": "tpl_city_ambush_l5",
  "name": "霓虹转角巷",
  "role": "normal",
  "noLoop": false,
  "spawnEligible": false,
  "regionIds": [],
  "allowRotate": true,
  "cells": [
    { "rx": 0, "ry": 0, "type": "empty",  "difficultyOffset": 0, "isPortal": true },
    { "rx": 1, "ry": 0, "type": "combat", "difficultyOffset": 0, "isPortal": false,
      "typePool": [{ "type": "combat", "weight": 2 }, { "type": "elite", "weight": 1 }] },
    { "rx": 2, "ry": 0, "type": "combat", "difficultyOffset": 1, "isPortal": false },
    { "rx": 2, "ry": 1, "type": "event",  "difficultyOffset": 0, "isPortal": false },
    { "rx": 2, "ry": 2, "type": "reward", "difficultyOffset": 0, "isPortal": false }
  ],
  "metrics": {
    "resourceOverride": null,
    "difficultyOverride": null
  },
  "generation": {
    "weight": 1,
    "max": 4,
    "unique": false
  },
  "tags": ["折线", "战斗"],
  "folderId": "template_group_city"
}
```

`folderId` 是编辑器左侧文件夹的 UI 归类，可为空；它不参与结构签名、家族兼容判断、生成或 Unity 运行时。模板库 JSON 顶层额外保存 `templateGroups: [{ id, name, parentGroupId, order }]`，支持无限嵌套；删除文件夹只移动模板与子文件夹，不删除模板。

模板可选 `uiOrder` 只控制编辑器左侧显示顺序；它不进入结构签名，也不改变 `stripTemplates` 数组顺序和生成结果。

关键规则：
1. **单链无矩形（明确首尾）**：`cells` 必须四连通、无任意 2×2 实心块、且无分叉（当格数 $\ge 2$ 时恰有 2 个度为 1 的首尾端点格，其余格度为 2）。
2. **数值求和与手动覆写**：
   - 自动求和：固定格用 `cellType(cell.type)`；带 `typePool` 的格用各类型资源/难度的加权期望。`autoDifficulty` 同理；
   - 若 `resourceOverride !== null`（或 `difficultyOverride !== null`），则使用手动覆写值；
   - **战斗格 `difficultyOffset` 不改变条带总难度值**，仅用于地图生成后的最终敌人等级计算；
   - 地图实例的实际资源/难度按最终抽中的类型重新计算，不等于模板卡片上的期望值。
3. **非环属性（`noLoop`）**：标记为 `noLoop: true` 的条带不参与任何环路，仅作为端点支路（度数严格为 1，不向外再生长、不补门、不挂载跨区桥）；每个区域独立配置 `maxNoLoop` 上限（设为 `0` 则该区不生成非环条带）。
4. **出入口格子（`isPortal`）**：
   - 大多数条带不设置出入口格（全格皆可随机开门或单格交叉，受结构松散度调节）；
   - 一旦某条带存在 $\ge 1$ 个 `isPortal: true` 的格子，则它与其它条带的**所有门连接和单格交叉**都严格限制在这些出入口格上。
5. **单格模糊类型池（`typePool`）**：
   - 可选字段，格式 `[{ type, weight }]`；`type` 仍是主类型和固定回退，池长度 >=2 才启用。
   - 生成每个条带实例时，逐格用独立的内容随机流按权重抽一个实际类型；几何、旋转、版本选择不受影响。
   - 若任一项支持 `difficultyOffset`，该格保留偏移；最终敌人等级按实际抽中的类型判断是否支持。
   - `spawn` 不能进入池；权重必须为正。浏览器编辑器、浏览器生成器和 Unity 运行时均支持该字段。
6. **四种区域类型与归属**：内置区域类型为 **城市（`city`）、工厂（`factory`）、郊区（`suburb`）、太空（`space`）**；模板 `regionIds: []` 表示四区通用，也可勾选限定区域。

### 2.3 单层生成方案（`FloorProfile`）

- 3 个区域槽位从 `城市 / 工厂 / 郊区 / 太空` 四种区域类型中选择（不重复），每区独立配置 `stripMin / stripMax` 与 **`maxNoLoop`（最多非环条带数，0 表示无非环）**；
- `topology.looseness`（`0.0 ~ 1.0`）：**结构松散度**——`0` 倾向于在条带中段相连/交叉（紧凑团块），`1` 倾向于在条带首尾相连（舒展长廊）；
- 其余预算区间（留空自动计算）与节点图距离等级曲线同前。

---

### 2.4 单层地图实例（`FloorMap`）

```json
{
  "id": "floor_DEMO-1",
  "kind": "plotweave-floor-map",
  "seed": "DEMO-1",
  "profileId": "floor_default",
  "regions": [ ... ],
  "strips": [
    {
      "id": "s1",
      "templateId": "tpl_spawn_hall",
      "templateName": "起始大厅",
      "role": "spawn",
      "regionId": "region_a",
      "bridgeBetween": null,
      "origin": { "x": 12, "y": 14 },
      "rotation": 0,
      "shapeSignature": "0,0|1,0|0,1|1,1",
      "resource": 15,
      "difficulty": 10,
      "distanceFromSpawn": 0,
      "level": 1,
      "cells": [
        { "index": 0, "rx": 0, "ry": 0, "x": 12, "y": 14, "type": "spawn",  "difficultyOffset": 0, "enemyLevel": null },
        { "index": 1, "rx": 1, "ry": 0, "x": 13, "y": 14, "type": "combat", "difficultyOffset": 1, "enemyLevel": 2 }
      ]
    }
  ],
  "links": [
    { "id": "l1", "type": "door",  "a": { "stripId": "s1", "cellIndex": 1 }, "b": { "stripId": "s2", "cellIndex": 0 }, "dir": "E", "isBridgeLink": false },
    { "id": "l2", "type": "cross", "a": { "stripId": "s2", "cellIndex": 2 }, "b": { "stripId": "s3", "cellIndex": 1 }, "cell": { "x": 15, "y": 14 }, "isBridgeLink": false }
  ],
  "spawn": { "stripId": "s1", "cellIndex": 0, "x": 12, "y": 14 },
  "budget": {
    "target": { "resourceMin": 180, "resourceMax": 260, "difficultyMin": 160, "difficultyMax": 240, "autoResource": true, "autoDifficulty": true },
    "actual": { "resource": 225, "difficulty": 195 }
  },
  "stats": { ... },
  "issues": []
}
```

---

## 3. 核心算法设计

### 3.1 多格条带几何（`src/core/polyomino.js`）
1. **归一化与旋转缓存**：对模板 `cells` 计算 0°/90°/180°/270° 旋转后的相对坐标并归一化至第一象限，按格坐标集去重（若模板 `allowRotate === false` 则仅保留 0°）。
2. **形状签名（Shape Signature）**：对 4 种旋转的排序坐标串取字典序最小串，作为几何同构标识，供后续“同形状模板互换”直接匹配。
3. **任意格门连接与单格交叉规则**：
   - **门（`door`）**：两条条带任意正交相邻格之间可建立门连接，且两者的占格集合无交集（`A ∩ B = ∅`）。相邻但无 `door` 边则视为墙。
   - **交叉（`cross`）**：仅限**同区域内**两条条带；任意格均可作为交叉点，但满足四项不变量：
     1. **单格重叠**：`|cells(A) ∩ cells(B)| === 1`；
     2. **每格最多双层**：单个世界坐标最多被 2 条条带共享；
     3. **无平行重边**：任意两条条带之间最多存在 1 条 `link`（门或交叉），防止出现 2-节点伪环；
     4. **冲突软规避**：生成候选评分时优先选择至少一方是 `empty` 通道格的交叉点，尽量避免两个功能/特殊格（如商店与精英）直接重叠。

### 3.2 三区域三角形单层生成算法（`src/core/floor-generator.js`）

采用 **「局部区内成环生长 → 向量差哈希 $O(1)$ 三角刚体拼装 → 双通道数值收敛 → 节点图等级计算」** 四阶段流水线：

#### 阶段 1：三区域各自局部成环生长
- 针对 A、B、C 三个区域分别在局部坐标系中独立生成（目标条带数在 `[stripMin, stripMax]` 内由 RNG 采样）：
  1. 放置起始条带（区域 A 优先选择 `spawnEligible` 模板）。
  2. 常规生长：从可用模板池中**按预算引导权重**抽取模板，枚举其旋转姿态与与本区已有条带的「门」/「单格交叉」落点，优先紧凑分布（靠近局部质心）且优先避免特殊格重叠。
  3. **成环保障（真实简单环 $\ge 3$ 节点）**：
     - 每次新条带落地后，若与另一个图距离 $\ge 2$ 的已有条带正交相邻，按 `extraLoopChance` 补门形成环路；
     - 若到达最后 2 条配额仍未形成环路（`cycles === 0`），优先在已有相邻且图距离 $\ge 2$ 的条带间补门；若无直接相邻对，则搜索能够**同时连接两条图距离 $\ge 2$ 的已有条带**的新条带落点（支持门+门、门+交叉、交叉+交叉）一举闭合环路。

#### 阶段 2：向量差哈希 $O(1)$ 三角刚体拼装与独立桥接（跨区仅用门）
- 抽取 3 条独立的 `role === 'bridge'` 桥接条带：`Bridge_AB`、`Bridge_BC`、`Bridge_CA`（若模板库暂无桥接模板则自动回退至通用模板）。
- **步骤 2.1**：固定区域 A；在 A 的外边界（非占格、且周围不冲突的空格）通过**门**挂载 `Bridge_AB`；在 `Bridge_AB` 远离 A 的外边界通过**门**挂载旋转/平移后的区域 B，要求 A 与 B 之间**零重叠且零直接相邻**（保持清晰区域边界，杜绝任何隐式接触）。
- **步骤 2.2**：预处理区域 C（及其 4 种整体旋转）的所有外边界可开门出口 `(portX, portY, inwardCell)`，建立向量差哈希表 `deltaMap[(dx, dy)] -> List<(port1, port2)>`。
- **步骤 2.3**：在 A 远离 B 的外边界挂载 `Bridge_AC`（仅门），在 B 远离 A 的外边界挂载 `Bridge_BC`（仅门），遍历两桥的候选出口对 `(exit1, exit2)`：
  - 计算 `reqDelta = (exit2.x - exit1.x, exit2.y - exit1.y)`；
  - **$O(1)$ 查询 `deltaMap[reqDelta]`** 得到区域 C 的精确平移偏移；
  - 校验区域 C 与 A、B、三座桥无重叠，且 C 仅在 `exit1`、`exit2` 处与两座桥建立门连接，与 A、B 无直接相邻！
- **步骤 2.4**：将整张拼装完毕的三角地图平移归一化到 `(padding, padding)`，并记录每条桥的 `bridgeBetween: [regionX, regionY]`。

#### 阶段 3：总资源与总难度区间收敛
- 若用户留空 `resourceMin/Max` 或 `difficultyMin/Max`，程序按候选模板库的均值与当前总条带数 $N$ 自动估算目标区间（均值 $\pm 15\%$，并保底最小跨度）。
- 若拼装后总资源或总难度偏离目标区间：
  - **Pass 1（同形状互换）**：在不改变几何的前提下，按 `shapeSignature` 查找同形兼容模板，采用贪心/局部搜索降低越界误差；
  - **Pass 2（非割点叶条带换形）**：选取区域内度为 1 且非出生条带的末端支路，移除后在同一门/交叉锚点处尝试替换为其它形状模板（验无碰撞与不接触其它区域/桥），进一步将 `(totalResource, totalDifficulty)` 拉入区间。
- 外部执行确定性多轮种子派生重试（`seed`, `seed#2`, ..., `seed#K`），择优选择「0 拓扑错误 + 命中预算区间 + 结构紧凑」的最佳地图。

#### 阶段 4：节点图距离与等级计算
- 构造无向节点图 $G = (V, E)$，$V$ 为全部条带（含 3 条独立桥），$E$ 为全部 `door` 与 `cross` 连接。
- 从出生点所在条带 `spawn.stripId` 运行 BFS，得到每条条带的 `distanceFromSpawn`。
- 计算每条条带等级与战斗格最终敌人等级：
  - `strip.level = clamp(baseLevel + Math.floor(distanceFromSpawn * levelPerHop), 1, maxLevel)`
  - 对所有 `supportsOffset`（如 `combat`、`elite`）格子：`cell.enemyLevel = Math.max(1, strip.level + cell.difficultyOffset)`。

---

### 3.3 单层不变量校验器（`src/core/floor-validator.js`）
每次生成或手动修改后执行完整校验：
1. 三区域存在且均非空；
2. 每对区域 `(A,B), (B,C), (C,A)` 之间**有且仅有 1 条独立桥接条带**，且该桥接条带仅通过 2 扇门分别连接对应两区；
3. 任意不同区域之间无直接门、无直接交叉、无格重叠；
4. 每个区域子图连通，且简单环数 $E_r - V_r + 1 \ge 1$（且无平行重边）；
5. 任意两交叉条带共享格严格为 1，每格最多 2 条条带；
6. 出生点合法，整图从出生点 100% 可达；
7. 统计总资源、总难度是否落入限定区间（超界给出 `warning` 及具体差值）。

---

### 3.4 随机条带模板草稿生成器（`src/core/strip-templates.js`）
编辑器内提供「随机配置 → 一键生成草稿 → 展示在画布 → 手工精修 → 保存」工作流：
- **配置项**：
  - 格数范围 `minCells ~ maxCells`（默认 `3 ~ 6`）；
  - 形状风格：`mixed`（混合）/ `line`（直线）/ `polyline`（折线 L/Z/U）/ `rect`（矩形/块状）；
  - 角色：`normal` / `bridge`；
  - 各类格子权重（空、战斗、精英、商店、奖励、事件、休整）；
  - 战斗/精英格难度偏移随机范围 `[offsetMin, offsetMax]`；
  - 软约束（例如同一条带默认最多 1 个商店、自动避免全空）。
- 生成结果作为临时草稿直接显示于中央 2D 编辑台，不覆盖已有模板，用户精修后点击保存加入模板库。

---

## 4. 目录与模块清单

```text
PlotWeave/
├─ docs/
│  └─ DUNGEON_EDITOR_DESIGN.md    # 本文档
├─ src/
│  ├─ core/
│  │  ├─ constants.js             # 扩展 DEFAULT_CELL_TYPES / SHAPE_STYLES / STRIP_ROLES
│  │  ├─ polyomino.js             # [新增] 多格条带几何、旋转去重、形状签名、门与单格交叉工具
│  │  ├─ strip-templates.js       # [新增] 全局格子配置、条带模板模型、随机草稿生成、模板库 JSON 序列化
│  │  ├─ floor-validator.js       # [新增] 单层三角地图不变量校验与统计
│  │  ├─ floor-generator.js       # [新增] 三区域三角成环单层生成器 + 预算求解 + 等级计算
│  │  ├─ world-placeholder.js     # [新增] 完整地图留空占位模块
│  │  ├─ project.js               # 扩展 dungeon 字段（cellTypes / stripTemplates / floorProfiles / world）并保持 v1 兼容
│  │  └─ serialization.js         # 支持工程/模板库/单层地图导入导出
│  ├─ data/
│  │  └─ sample-project.js        # 内置丰富的多格条带示例模板（直线/L/T/Z/矩形/独立桥/出生厅）与默认单层方案
│  └─ ui/
│     ├─ template-editor.js       # [新增] 模式一：条带模板编辑器（2D画布 + 随机草稿 + 全局格子配置弹窗）
│     ├─ floor-canvas.js          # [新增] 模式二：多格三角单层 Canvas 2D 渲染器（格子图标/交叉格/等级/节点图/热力）
│     ├─ floor-editor.js          # [新增] 模式二：单层地图面板（三区方案/预算区间/统计徽章/条带检视与微调）
│     ├─ world-view.js            # [新增] 模式三：完整地图留空占位视图
│     ├─ app.js                   # 顶栏三模式切换集成、快捷操作、主题联动
│     ├─ store.js                 # 状态仓库扩展（mode、floorMaps、templateDraft、撤销重做、持久化）
│     └─ styles.css               # 三模式编辑器样式（兼顾暗色/亮色主题）
└─ tests/
   ├─ polyomino.test.mjs          # [新增] 多格几何与交叉校验单元测试
   ├─ strip-templates.test.mjs    # [新增] 模板求和/覆写/随机生成/JSON导入导出测试
   └─ floor-generator.test.mjs    # [新增] 60+ 种子批量单层生成硬约束与预算收敛测试
```

---

## 5. 验证标准

1. 原有 35 个 PlotWeave 测试全部继续通过（向后兼容）。
2. 新增 `polyomino`、`strip-templates`、`floor-generator` 单元测试全部通过：
   - 批量 60 个随机种子下，单层生成 **100% 满足**：三区域三角互连、区间有且仅有 1 条独立桥且仅用门、每区至少 1 个 $\ge 3$ 节点环路、单格交叉不变量、从出生点 100% 可达、距离与等级严格单调、战斗格偏移正确反映到 `enemyLevel` 且不改条带总难度、总资源与总难度稳定收敛入目标区间。
3. 浏览器内三大模式切换顺畅：
   - 条带模板：可随机生成草稿、2D 画布增删涂刷、调难度偏移、修改全局格子数值实时联动、手动覆写条带数值、导出/导入模板库 JSON；
   - 单层地图：可调节方案并生成、画布清晰展示异形条带/独立桥/交叉格/条带等级/敌人等级/节点图叠层/距离热力、导出 JSON 与 2x PNG；
   - 完整地图：清晰留空占位。
