/**
 * 地图不变量校验与统计。
 *
 * 生成器只保证「它自己生成的地图」正确；导入、手改、插件生成的地图都要
 * 经过这里。校验读取工程定义（地块种类、区域、连接规则），但不修改地图。
 */

import {
  DIRECTIONS,
  PLOT_LENGTH_MIN,
  PLOT_LENGTH_MAX,
  CONTENT_TYPE_IDS,
} from './constants.js';
import {
  inBounds,
  areAdjacent,
  directionFromTo,
  cellsFor,
  mapBounds,
  plotBounds,
  cellKey,
} from './geometry.js';
import { canConnectKinds, canCrossKinds, regionById, plotKindById } from './rules.js';

const issue = (level, code, message, path = '') => ({ level, code, message, path });

function linkEndpoints(link) {
  return [link.a, link.b];
}

function validIndex(plot, index) {
  return Number.isInteger(index) && index >= 0 && index < (plot?.cells?.length || 0);
}

function cellOf(plot, index) {
  return plot?.cells?.[index] || null;
}

function makeAdjacency(map) {
  const adjacency = new Map((map?.plots || []).map((plot) => [plot.id, new Set()]));
  for (const link of map?.links || []) {
    if (!link?.a?.plotId || !link?.b?.plotId) continue;
    if (!adjacency.has(link.a.plotId) || !adjacency.has(link.b.plotId)) continue;
    adjacency.get(link.a.plotId).add(link.b.plotId);
    adjacency.get(link.b.plotId).add(link.a.plotId);
  }
  return adjacency;
}

export function computeMapStats(map) {
  const plots = Array.isArray(map?.plots) ? map.plots : [];
  const links = Array.isArray(map?.links) ? map.links : [];
  const grid = map?.settings?.grid || { width: 0, height: 0 };
  const degree = new Map(plots.map((plot) => [plot.id, 0]));
  const regions = new Map();
  const kinds = new Map();
  const cells = new Set();
  const contentCounts = { props: 0, relics: 0, events: 0, enemies: 0, npcs: 0 };
  let subRooms = 0;

  for (const plot of plots) {
    degree.set(plot.id, degree.get(plot.id) || 0);
    for (const cell of plot.cells || []) cells.add(cellKey(cell.x, cell.y));

    const region = regions.get(plot.regionId) || { regionId: plot.regionId, plots: 0, cells: 0, subRooms: 0 };
    region.plots += 1;
    region.cells += plot.cells?.length || 0;
    region.subRooms += plot.subRooms?.length || 0;
    regions.set(plot.regionId, region);

    const kind = kinds.get(plot.kindId) || { kindId: plot.kindId, regionId: plot.regionId, plots: 0, cells: 0 };
    kind.plots += 1;
    kind.cells += plot.cells?.length || 0;
    kinds.set(plot.kindId, kind);

    for (const type of CONTENT_TYPE_IDS) {
      contentCounts[type] += plot.contents?.[type]?.length || 0;
    }
    for (const room of plot.subRooms || []) {
      subRooms += 1;
      for (const type of CONTENT_TYPE_IDS) {
        contentCounts[type] += room.contents?.[type]?.length || 0;
      }
    }
  }

  let doors = 0;
  let crossings = 0;
  for (const link of links) {
    if (link.type === 'cross') crossings += 1;
    else doors += 1;
    for (const end of linkEndpoints(link)) {
      if (end?.plotId && degree.has(end.plotId)) degree.set(end.plotId, degree.get(end.plotId) + 1);
    }
  }

  const degrees = [...degree.values()];
  const bounds = mapBounds(plots);
  const adjacency = makeAdjacency(map);
  const start = map?.spawn?.plotId;
  let reachable = 0;
  let maxHops = 0;
  if (start && adjacency.has(start)) {
    const seen = new Map([[start, 0]]);
    const queue = [start];
    while (queue.length) {
      const current = queue.shift();
      for (const next of adjacency.get(current) || []) {
        if (!seen.has(next)) {
          seen.set(next, seen.get(current) + 1);
          maxHops = Math.max(maxHops, seen.get(next));
          queue.push(next);
        }
      }
    }
    reachable = seen.size;
  }

  let crossRegionLinks = 0;
  for (const link of links) {
    const a = plots.find((plot) => plot.id === link.a?.plotId);
    const b = plots.find((plot) => plot.id === link.b?.plotId);
    if (a && b && a.regionId !== b.regionId) crossRegionLinks += 1;
  }

  return {
    plots: plots.length,
    links: links.length,
    doors,
    crossings,
    cells: cells.size,
    subRooms,
    regions: [...regions.values()],
    kinds: [...kinds.values()],
    contents: contentCounts,
    degrees: {
      max: degrees.length ? Math.max(...degrees) : 0,
      average: degrees.length ? degrees.reduce((sum, value) => sum + value, 0) / degrees.length : 0,
      isolated: degrees.filter((value) => value === 0).length,
    },
    bounds,
    grid,
    fillRatio: grid.width && grid.height ? cells.size / (grid.width * grid.height) : 0,
    reachable,
    maxHops,
    crossRegionLinks,
  };
}

export function validateMap(project, map, options = {}) {
  const issues = [];
  const error = (code, message, path) => issues.push(issue('error', code, message, path));
  const warn = (code, message, path) => issues.push(issue('warning', code, message, path));

  if (!map || typeof map !== 'object' || !Array.isArray(map.plots)) {
    error('map.invalid', '地图数据无效。', '');
    return { ok: false, errors: issues, warnings: [], issues, stats: computeMapStats(map) };
  }

  const grid = map.settings?.grid || project.settings?.grid;
  const plots = new Map();
  const plotIds = new Set();
  const plotKindMap = new Map();

  map.plots.forEach((plot, index) => {
    const path = `plots[${index}]`;
    if (!plot.id) error('plot.id', '地块实例缺少 id。', `${path}.id`);
    if (plot.id && plotIds.has(plot.id)) error('plot.id.duplicate', `地块实例 id「${plot.id}」重复。`, `${path}.id`);
    plotIds.add(plot.id);
    plots.set(plot.id, plot);

    const kind = plotKindById(project, plot.kindId);
    plotKindMap.set(plot.id, kind);
    if (!kind) {
      error('plot.kind', `地块实例「${plot.id}」引用了不存在的地块种类「${plot.kindId}」。`, `${path}.kindId`);
    } else if (kind.regionId !== plot.regionId) {
      error('plot.region', `地块实例「${plot.id}」的区域与种类所属区域不一致。`, `${path}.regionId`);
    }
    if (!map.plots.length || !['h', 'v'].includes(plot.axis)) {
      error('plot.axis', `地块实例「${plot.id}」的朝向无效。`, `${path}.axis`);
    }
    if (
      !Number.isInteger(plot.length) ||
      plot.length < PLOT_LENGTH_MIN ||
      plot.length > PLOT_LENGTH_MAX
    ) {
      error('plot.length', `地块实例「${plot.id}」的长度需要在 ${PLOT_LENGTH_MIN}–${PLOT_LENGTH_MAX}。`, `${path}.length`);
    }
    if (!Number.isInteger(plot.origin?.x) || !Number.isInteger(plot.origin?.y)) {
      error('plot.origin', `地块实例「${plot.id}」的原点不是整数格。`, `${path}.origin`);
    }

    const expected = cellsFor(plot.origin || { x: 0, y: 0 }, plot.axis, plot.length);
    if (!Array.isArray(plot.cells) || plot.cells.length !== expected.length) {
      error('plot.cells', `地块实例「${plot.id}」的格子数量与长度不一致。`, `${path}.cells`);
    } else {
      for (let cellIndex = 0; cellIndex < expected.length; cellIndex += 1) {
        if (plot.cells[cellIndex]?.x !== expected[cellIndex].x || plot.cells[cellIndex]?.y !== expected[cellIndex].y) {
          error('plot.cells.geometry', `地块实例「${plot.id}」的格子与原点/朝向不一致。`, `${path}.cells[${cellIndex}]`);
          break;
        }
      }
    }
    for (const cell of plot.cells || []) {
      if (!inBounds(cell.x, cell.y, grid.width, grid.height)) {
        error('plot.cells.bounds', `地块实例「${plot.id}」超出网格范围。`, `${path}.cells`);
        break;
      }
    }

    const roomIds = new Set();
    (plot.subRooms || []).forEach((room, roomIndex) => {
      const roomPath = `${path}.subRooms[${roomIndex}]`;
      if (!room.id) error('subroom.id', '子房间实例缺少 id。', `${roomPath}.id`);
      if (room.id && roomIds.has(room.id)) error('subroom.id.duplicate', `子房间实例 id「${room.id}」重复。`, `${roomPath}.id`);
      roomIds.add(room.id);
      const roomKind = project.subRoomKinds.find((candidate) => candidate.id === room.kindId);
      if (!roomKind) error('subroom.kind', `子房间实例「${room.id}」引用了不存在的种类「${room.kindId}」。`, `${roomPath}.kindId`);
      if (!validIndex(plot, room.cellIndex)) {
        error('subroom.cell', `子房间实例「${room.id}」的格子序号无效。`, `${roomPath}.cellIndex`);
      } else {
        const cell = cellOf(plot, room.cellIndex);
        if (room.x !== cell.x || room.y !== cell.y) {
          error('subroom.position', `子房间实例「${room.id}」的坐标与所在格不一致。`, `${roomPath}`);
        }
      }
      for (const type of CONTENT_TYPE_IDS) {
        for (const itemId of room.contents?.[type] || []) {
          if (!project.catalogs?.[type]?.some((item) => item.id === itemId)) {
            warn('subroom.contents.ref', `子房间实例引用了不存在的${type}「${itemId}」。`, `${roomPath}.contents.${type}`);
          }
        }
      }
    });

    for (const type of CONTENT_TYPE_IDS) {
      for (const itemId of plot.contents?.[type] || []) {
        if (!project.catalogs?.[type]?.some((item) => item.id === itemId)) {
          warn('plot.contents.ref', `地块实例引用了不存在的${type}「${itemId}」。`, `${path}.contents.${type}`);
        }
      }
    }
  });

  // 格子重叠：只允许两块地交叉于唯一一格，并且必须有 cross 连接。
  const cellOwners = new Map();
  for (const plot of map.plots) {
    for (const cell of plot.cells || []) {
      const key = cellKey(cell.x, cell.y);
      if (!cellOwners.has(key)) cellOwners.set(key, []);
      cellOwners.get(key).push(plot.id);
    }
  }

  const crossings = new Map();
  const linkIds = new Set();
  const doorSides = new Map();
  const pairCrossCount = new Map();
  const degree = new Map([...plots.keys()].map((id) => [id, 0]));

  const links = Array.isArray(map.links) ? map.links : [];
  links.forEach((link, index) => {
    const path = `links[${index}]`;
    if (!link.id) error('link.id', '连接缺少 id。', `${path}.id`);
    if (link.id && linkIds.has(link.id)) error('link.id.duplicate', `连接 id「${link.id}」重复。`, `${path}.id`);
    linkIds.add(link.id);
    if (!['door', 'cross'].includes(link.type)) {
      error('link.type', `连接「${link.id}」类型无效。`, `${path}.type`);
      return;
    }

    const a = plots.get(link.a?.plotId);
    const b = plots.get(link.b?.plotId);
    if (!a || !b) {
      error('link.plot', `连接「${link.id}」指向不存在的地块实例。`, `${path}`);
      return;
    }
    if (a.id === b.id) {
      error('link.self', `连接「${link.id}」不能连接地块自身。`, `${path}`);
      return;
    }
    if (!validIndex(a, link.a.cellIndex) || !validIndex(b, link.b.cellIndex)) {
      error('link.cell', `连接「${link.id}」的格子序号无效。`, `${path}`);
      return;
    }

    const aCell = cellOf(a, link.a.cellIndex);
    const bCell = cellOf(b, link.b.cellIndex);
    const aKind = plotKindMap.get(a.id) || plotKindById(project, a.kindId);
    const bKind = plotKindMap.get(b.id) || plotKindById(project, b.kindId);

    if (link.type === 'door') {
      if (!areAdjacent(aCell, bCell)) {
        error('link.door.adjacency', `门「${link.id}」连接的两个格不相邻。`, `${path}`);
      }
      const direction = directionFromTo(aCell, bCell);
      if (!direction) {
        error('link.door.direction', `门「${link.id}」缺少可用的方向。`, `${path}`);
      } else {
        if (link.dir && link.dir !== direction) {
          error('link.door.dir', `门「${link.id}」记录的方向与实际位置不一致。`, `${path}.dir`);
        }
        const sideKey = `${a.id}:${link.a.cellIndex}:${direction}`;
        const reverseKey = `${b.id}:${link.b.cellIndex}:${DIRECTIONS[direction].opposite}`;
        if (doorSides.has(sideKey) || doorSides.has(reverseKey)) {
          error('link.door.side', `门「${link.id}」与另一扇门占用了同一格同一侧。`, `${path}`);
        }
        doorSides.set(sideKey, link.id);
        doorSides.set(reverseKey, link.id);
      }
      if (aKind && bKind && !canConnectKinds(aKind, bKind)) {
        error('link.door.rules', `门「${link.id}」连接的「${aKind.name}」与「${bKind.name}」不允许互通。`, `${path}`);
      }
    } else {
      if (aCell.x !== bCell.x || aCell.y !== bCell.y) {
        error('link.cross.cell', `交叉「${link.id}」的两个地块没有共享同一格。`, `${path}`);
      }
      if (a.axis === b.axis) {
        error('link.cross.axis', `交叉「${link.id}」的两块地需要垂直。`, `${path}`);
      }
      if (aKind && bKind && !canCrossKinds(aKind, bKind, options.profile)) {
        error('link.cross.rules', `交叉「${link.id}」的「${aKind.name}」与「${bKind.name}」不允许交叉。`, `${path}`);
      }
      const pairKey = [a.id, b.id].sort().join('|');
      const count = pairCrossCount.get(pairKey) || 0;
      if (count > 0) {
        error('link.cross.pair', `地块「${a.id}」与「${b.id}」在同一格重复交叉。`, `${path}`);
      }
      pairCrossCount.set(pairKey, count + 1);
      crossings.set(cellKey(aCell.x, aCell.y), link.id);
    }

    degree.set(a.id, (degree.get(a.id) || 0) + 1);
    degree.set(b.id, (degree.get(b.id) || 0) + 1);
  });

  // 每个格子的重叠解释
  for (const [key, owners] of cellOwners) {
    if (owners.length <= 1) continue;
    if (owners.length > 2) {
      error('map.overlap.multiple', `格子 (${key}) 被 ${owners.length} 块地同时占用。`, 'plots');
      continue;
    }
    const [aId, bId] = owners;
    if (!crossings.has(key)) {
      error('map.overlap.unlinked', `地块「${aId}」与「${bId}」在格子 (${key}) 重叠，但没有交叉连接。`, 'plots');
    }
    const shared = [...cellOwners.entries()].filter(([, ids]) => ids.includes(aId) && ids.includes(bId));
    if (shared.length > 1) {
      error('map.overlap.multiple-cells', `地块「${aId}」与「${bId}」共享了多格，无法用单点交叉解释。`, 'plots');
    }
  }

  for (const plot of map.plots) {
    const kind = plotKindMap.get(plot.id);
    const value = degree.get(plot.id) || 0;
    if (kind && value > kind.connections.max) {
      error('plot.degree', `地块「${plot.id}」有 ${value} 个连接，超过种类「${kind.name}」上限 ${kind.connections.max}。`, `plots`);
    }
  }

  if (map.plots.length > 0) {
    const adjacency = makeAdjacency(map);
    const start = map.plots[0].id;
    const seen = new Set([start]);
    const queue = [start];
    while (queue.length) {
      const current = queue.shift();
      for (const next of adjacency.get(current) || []) {
        if (!seen.has(next)) {
          seen.add(next);
          queue.push(next);
        }
      }
    }
    if (seen.size !== map.plots.length) {
      error('map.connectivity', `地图不连通：从「${start}」只能到达 ${seen.size}/${map.plots.length} 块地。`, 'links');
    }
  }

  const spawn = map.spawn;
  if (!spawn) {
    warn('spawn.missing', '地图没有出生点。', 'spawn');
  } else {
    const plot = plots.get(spawn.plotId);
    if (!plot) {
      error('spawn.plot', `出生点指向不存在的地块「${spawn.plotId}」。`, 'spawn.plotId');
    } else {
      if (!validIndex(plot, spawn.cellIndex)) {
        error('spawn.cell', '出生点的格子序号无效。', 'spawn.cellIndex');
      } else {
        const cell = cellOf(plot, spawn.cellIndex);
        if (spawn.x !== cell.x || spawn.y !== cell.y) {
          error('spawn.position', '出生点坐标与所在格不一致。', 'spawn');
        }
        if (!inBounds(spawn.x, spawn.y, grid.width, grid.height)) {
          error('spawn.bounds', '出生点超出网格范围。', 'spawn');
        }
      }
      const kind = plotKindById(project, plot.kindId);
      if (kind && !kind.spawnEligible) {
        warn('spawn.eligible', `出生点所在地块「${kind.name}」没有标记为出生点候选。`, 'spawn');
      }
    }
  }

  if (options.profile) {
    const profile = options.profile;
    let crossRegion = 0;
    const pairCounts = new Map();
    for (const link of links) {
      const a = plots.get(link.a?.plotId);
      const b = plots.get(link.b?.plotId);
      if (!a || !b || a.regionId === b.regionId) continue;
      crossRegion += 1;
      const pairKey = [a.regionId, b.regionId].sort().join('|');
      pairCounts.set(pairKey, (pairCounts.get(pairKey) || 0) + 1);
    }
    if (!profile.allowCrossRegion && crossRegion > 0) {
      error('map.cross.disabled', `生成方案「${profile.name}」禁止跨区，但地图有 ${crossRegion} 条跨区连接。`, 'links');
    }
    if (profile.allowCrossRegion) {
      const cap = Math.floor(Math.max(0, profile.plotTarget) * Math.max(0, profile.crossRegionRatio));
      if (crossRegion > cap) {
        warn('map.cross.ratio', `地图有 ${crossRegion} 条跨区连接，超过方案容量 ${cap}。`, 'links');
      }
      for (const [pairKey, count] of pairCounts) {
        if (count > profile.maxCrossRegionPerPair) {
          warn('map.cross.pair', `区域对「${pairKey}」有 ${count} 条跨区连接，超过方案上限 ${profile.maxCrossRegionPerPair}。`, 'links');
        }
      }
    }
  }

  const stats = computeMapStats(map);
  const errors = issues.filter((entry) => entry.level === 'error');
  const warnings = issues.filter((entry) => entry.level === 'warning');
  return { ok: errors.length === 0, errors, warnings, issues, stats };
}

export function describeMapBounds(map) {
  const bounds = mapBounds(map?.plots || []);
  if (!bounds) return '空地图';
  const width = bounds.maxX - bounds.minX + 1;
  const height = bounds.maxY - bounds.minY + 1;
  return `${width}×${height}（x ${bounds.minX}–${bounds.maxX}，y ${bounds.minY}–${bounds.maxY}）`;
}

export function plotDegree(map, plotId) {
  let value = 0;
  for (const link of map?.links || []) {
    if (link.a?.plotId === plotId) value += 1;
    if (link.b?.plotId === plotId) value += 1;
  }
  return value;
}

export function findPlotAtCell(map, x, y) {
  return (map?.plots || []).find((plot) => (plot.cells || []).some((cell) => cell.x === x && cell.y === y)) || null;
}

export function findSubRoomAtCell(plot, x, y) {
  return (plot?.subRooms || []).find((room) => room.x === x && room.y === y) || null;
}

export function plotArea(plot) {
  const bounds = plotBounds(plot);
  return bounds ? (bounds.maxX - bounds.minX + 1) * (bounds.maxY - bounds.minY + 1) : 0;
}

export function regionSummary(map, regionId) {
  return (map?.plots || []).filter((plot) => plot.regionId === regionId);
}

export { regionById };
