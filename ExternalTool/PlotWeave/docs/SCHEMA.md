# 数据格式

`schemaVersion` 当前为 `1`。所有导出的 JSON 都是 UTF-8、可读、无循环引用。

## 1. 工程 Project

```json
{
  "app": "plotweave",
  "schemaVersion": 1,
  "meta": {
    "name": "静默层",
    "author": "PlotWeave",
    "description": "示例工程",
    "createdAt": "2026-01-01T00:00:00.000Z",
    "updatedAt": "2026-01-01T00:00:00.000Z"
  },
  "settings": { "grid": { "width": 44, "height": 32 } },
  "regions": [],
  "plotKinds": [],
  "plotGroups": [],
  "subRoomKinds": [],
  "catalogs": {
    "props": [],
    "relics": [],
    "events": [],
    "enemies": [],
    "npcs": []
  },
  "profiles": [],
  "extensions": {}
}
```

`grid` 限制在 8–160 格。`extensions` 是给外部工具预留的自由对象，核心不解释其内容。

### 1.1 区域 Region

```json
{
  "id": "atrium",
  "name": "前厅",
  "shortName": "前厅",
  "color": "#d9a441",
  "description": "……",
  "tags": ["公共"],
  "hidden": false,
  "generation": { "min": 4, "max": 8, "weight": 1.3, "order": 0 }
}
```

- `generation.min/max`：这张图希望从该区域生成的地块数范围。
- `generation.weight`：区域被选中的相对权重。
- `hidden`：只影响展示，不影响生成。
- `color` 必须是 `#rrggbb`。

### 1.1.1 折叠组 PlotGroup

```json
{
  "id": "group_2",
  "name": "入口区",
  "regionId": "atrium",
  "parentGroupId": "group_1",
  "description": "",
  "order": 0
}
```

- 折叠组只用于左侧结构树的收纳与阅读，不参与生成算法。
- 折叠组可以嵌套；`parentGroupId` 为空表示区域根级，非空时必须指向同一区域内的另一个折叠组，且不能形成循环。
- `regionId` 必须指向存在的区域；`plotKind.groupId` 可以留空，非空时必须与地块属于同一区域。
- 删除折叠组不会删除地块或子组：直接归属的地块会移到上级组（根级则移到未归组），子折叠组会上移一级。

### 1.2 地块种类 PlotKind

```json
{
  "id": "atrium_foyer",
  "name": "门厅",
  "shortName": "门厅",
  "regionId": "atrium",
  "groupId": "",
  "description": "……",
  "tags": ["起点"],
  "length": { "min": 2, "max": 3 },
  "connections": {
    "max": 4,
    "anchors": "both",
    "allowCrossing": true,
    "allowedKinds": [],
    "allowedTags": []
  },
  "subRooms": {
    "enabled": true,
    "min": 1,
    "max": 2,
    "pool": ["watch", "store"]
  },
  "contents": {
    "props": ["flashlight"],
    "relics": [],
    "events": ["lights_out"],
    "enemies": [],
    "npcs": ["wanderer"]
  },
  "generation": { "weight": 1, "max": 6, "unique": false },
  "visual": { "color": "#d9a441", "icon": "▭" },
  "spawnEligible": true
}
```

字段约束：

| 字段 | 约束 |
| --- | --- |
| `length.min/max` | 1–5，且 `max >= min` |
| `connections.max` | 0–8，统计门与交叉的总度数 |
| `connections.anchors` | `ends` / `middle` / `both` / `any` |
| `groupId` | 可空；非空时必须是同区域已有的折叠组 |
| `allowedKinds` | 为空表示不按种类限制；非空时双向检查 |
| `allowedTags` | 为空表示不按标签限制；非空时双向交集 |
| `subRooms.max` | 每块地最多挂几个子房间，不是每格 |
| `generation.max` | 单张地图中该种类最多出现几次；0 表示不生成 |
| `generation.unique` | 为真时整张图最多一块 |
| `spawnEligible` | 只有这些种类可以被设为出生点 |

`contents` 里的字符串是 `catalogs` 中对应类型的条目 `id`，不是完整对象。生成器不会读取它们。

### 1.3 子房间种类 SubRoomKind

```json
{
  "id": "watch",
  "name": "监控室",
  "description": "……",
  "color": "#6aa9c9",
  "tags": ["设施"],
  "contents": { "props": [], "relics": [], "events": [], "enemies": [], "npcs": [] },
  "generation": { "weight": 1 }
}
```

地块种类通过 `subRooms.pool` 引用这些 ID。子房间实例只存在于**地图**里，不在地块种类里。

### 1.4 内容目录 CatalogItem

五类目录共用字段：

```json
{
  "id": "flashlight",
  "name": "手电筒",
  "description": "……",
  "tags": ["照明"],
  "rarity": "common",
  "icon": "◆"
}
```

`rarity`：`common` / `uncommon` / `rare`。目录条目只被引用，不携带玩法数据。

### 1.5 生成方案 Profile

```json
{
  "id": "standard",
  "name": "标准漫游",
  "description": "……",
  "plotTarget": 24,
  "startRegionId": "atrium",
  "startKindId": "atrium_foyer",
  "regionIds": [],
  "allowCrossRegion": true,
  "crossRegionRatio": 0.18,
  "maxCrossRegionPerPair": 2,
  "allowCrossing": true,
  "subRoomDensity": 1,
  "generator": "standard"
}
```

- `regionIds` 为空表示全部区域；非空时只使用列出的区域。
- `crossRegionRatio` / `maxCrossRegionPerPair` 共同限制跨区连接数量。
- `generator` 是注册表 ID；缺省为 `standard`。
- `plotTarget` 只是目标，实际数量受区域容量、连接上限、空间与重试影响。

## 2. 地图 Map

```json
{
  "id": "map_DEMO-1",
  "seed": "DEMO-1",
  "profileId": "standard",
  "generator": "standard",
  "generatorVersion": 1,
  "createdAt": "2026-01-01T00:00:00.000Z",
  "attempt": 0,
  "settings": { "grid": { "width": 44, "height": 32 } },
  "plots": [],
  "links": [],
  "spawn": null,
  "stats": {},
  "issues": [],
  "warnings": [],
  "errors": [],
  "generation": {}
}
```

`issues` 是 `{ level, code, message }` 数组；`warnings` / `errors` 是消息字符串，便于直接展示。

### 2.1 地图地块 Plot

```json
{
  "id": "p1",
  "kindId": "atrium_foyer",
  "regionId": "atrium",
  "axis": "h",
  "origin": { "x": 22, "y": 16 },
  "length": 2,
  "cells": [
    { "x": 22, "y": 16, "index": 0 },
    { "x": 23, "y": 16, "index": 1 }
  ],
  "contents": { "props": [], "relics": [], "events": [], "enemies": [], "npcs": [] },
  "subRooms": [
    {
      "id": "s1",
      "kindId": "watch",
      "cellIndex": 1,
      "x": 23,
      "y": 16,
      "contents": { "props": [], "relics": [], "events": [], "enemies": [], "npcs": [] }
    }
  ]
}
```

- `axis` 决定条带方向；`cells` 必须与 `origin + axis + length` 完全一致。
- `cellIndex` 从 0 开始，沿条带正向递增。
- 地图地块的 `contents` 可以是种类默认内容的副本，也可以在检视器里单独改。
- 子房间必须落在该地块的格子上，`cellIndex` 与 `x/y` 一致。

### 2.2 连接 Link

门：

```json
{
  "id": "l1",
  "type": "door",
  "a": { "plotId": "p1", "cellIndex": 0 },
  "b": { "plotId": "p2", "cellIndex": 0 },
  "dir": "N"
}
```

- `a` 与 `b` 必须来自不同地块；
- 两格必须相邻；
- `dir` 是从 `a` 指向 `b` 的方向；
- 同一格的同一侧最多一扇门。

交叉：

```json
{
  "id": "l4",
  "type": "cross",
  "a": { "plotId": "p1", "cellIndex": 0 },
  "b": { "plotId": "p5", "cellIndex": 0 },
  "cell": { "x": 22, "y": 16 }
}
```

- 两个地块共享同一格；
- 两块地的 `axis` 必须垂直；
- 交叉不占侧面，但计入两个地块的度数。

### 2.3 出生点

```json
{ "plotId": "p1", "cellIndex": 0, "x": 22, "y": 16 }
```

只能落在 `spawnEligible` 的地块种类上，且格坐标必须属于该地块。

### 2.4 统计 stats

`computeMapStats(map)` 输出：

| 字段 | 含义 |
| --- | --- |
| `plots` / `links` / `doors` / `crossings` | 数量 |
| `cells` | 去重后的被占格数 |
| `subRooms` | 子房间实例数 |
| `regions` / `kinds` | 按区域/种类的计数 |
| `contents` | 五类内容引用计数 |
| `degrees` | `{ max, average, isolated }` |
| `bounds` | 地图包围盒 |
| `grid` | 工程网格 |
| `fillRatio` | 被占格 / 网格面积 |
| `reachable` | 从出生点可达的地块数 |
| `maxHops` | 最远地块的 BFS 跳数 |
| `crossRegionLinks` | 跨区域连接数 |

## 3. Bundle

Bundle 是「工程 + 可选地图 + 导出信息」：

```json
{
  "app": "plotweave",
  "kind": "plotweave-bundle",
  "schemaVersion": 1,
  "exportedAt": "2026-01-01T00:00:00.000Z",
  "projectFingerprint": "pw_xxxxxxxx",
  "project": {},
  "map": {}
}
```

导入时：

1. `parseBundle` 接受 Bundle 或裸工程；
2. 迁移/规范化工程；
3. 若带地图，按工程重新执行 `validateMap`；
4. 返回 `{ ok, project, map, errors, warnings, migrated, fingerprint }`。

地图校验失败不会丢弃工程，设计者可以继续修复。

## 4. ID 约定

- 工程内区域、地块种类、子房间种类、目录条目、方案使用稳定字符串 ID。
- 地图内地块使用 `p1, p2, ...`，子房间使用 `s1, s2, ...`，连接使用 `l1, l2, ...`。
- 工厂函数生成 `region_1`、`kind_1` 这类 ID；调用方负责 push 到工程数组。
- 修改 ID 会让已有地图的引用失效；改 ID 后应重新生成或手动修复地图。

## 5. 兼容性

- 读取时 `schemaVersion` 小于当前值会走 `migrateProject`，保留未知字段并补齐默认值。
- 写入时保留 `extensions` 与未知顶层字段，方便外部工具附加数据。
- 解析器不会抛异常；坏 JSON、错误 Bundle、缺字段都通过返回值报告。
