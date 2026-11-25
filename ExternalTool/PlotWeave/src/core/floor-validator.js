/**
 * 单层三角地牢地图校验与统计。
 *
 * 硬约束校验清单：
 *   1. 三区域均存在且各自内部连通；
 *   2. 每个区域内部至少形成 1 个真实简单环（节点数 >= 3，无平行重边）；
 *   3. 三对区域 (A-B, B-C, C-A) 之间有且仅有 1 条独立的跨区桥接条带，且跨区仅用门；
 *   4. 不同区域之间无直接连接、无格子重叠；
 *   5. 任意两条交叉条带仅共享 1 格，单格最多被 2 条条带占据；
 *   6. 从出生点出发整图 100% 可达；
 *   7. 总资源量与总难度量落入限定区间（超界记为 warning 供策划知晓）。
 */

import { hasConnectedOpenEdges, isOpenChain, needsStrictCross, straightAxis, validateOpenDirs } from './strip-edges.js';
import { DIRECTIONS } from './constants.js';
import { boundsOfCells, computeChainOrderMap, coordKey, overlapCells } from './polyomino.js';

const issue = (level, code, message) => ({ level, code, message });

function pairKey(a, b) {
  return [a, b].sort().join('::');
}

function buildAdjacency(strips, links) {
  const adj = new Map((strips || []).map((s) => [s.id, new Set()]));
  for (const link of links || []) {
    const a = link?.a?.stripId;
    const b = link?.b?.stripId;
    if (!a || !b || !adj.has(a) || !adj.has(b)) continue;
    adj.get(a).add(b);
    adj.get(b).add(a);
  }
  return adj;
}

function countComponents(nodeIds, adj) {
  const allowed = new Set(nodeIds);
  const visited = new Set();
  let components = 0;
  for (const id of nodeIds) {
    if (visited.has(id)) continue;
    components += 1;
    const queue = [id];
    visited.add(id);
    while (queue.length) {
      const cur = queue.shift();
      for (const next of adj.get(cur) || []) {
        if (allowed.has(next) && !visited.has(next)) {
          visited.add(next);
          queue.push(next);
        }
      }
    }
  }
  return components;
}

export function computeFloorStats(map) {
  const strips = Array.isArray(map?.strips) ? map.strips : [];
  const links = Array.isArray(map?.links) ? map.links : [];
  const regions = Array.isArray(map?.regions) ? map.regions : [];
  const stripMap = new Map(strips.map((s) => [s.id, s]));

  const allCells = [];
  const uniqueCellKeys = new Set();
  const cellOwners = new Map();
  const cellTypeCounts = {};
  let totalResource = 0;
  let totalDifficulty = 0;

  for (const strip of strips) {
    totalResource += Number(strip.resource) || 0;
    totalDifficulty += Number(strip.difficulty) || 0;
    for (const cell of strip.cells || []) {
      allCells.push(cell);
      const key = coordKey(cell.x, cell.y);
      uniqueCellKeys.add(key);
      const owners = cellOwners.get(key) || [];
      owners.push(strip.id);
      cellOwners.set(key, owners);
      cellTypeCounts[cell.type] = (cellTypeCounts[cell.type] || 0) + 1;
    }
  }
  totalResource = Math.round(totalResource * 100) / 100;
  totalDifficulty = Math.round(totalDifficulty * 100) / 100;

  let doors = 0;
  let crossings = 0;
  let bridgeDoors = 0;
  for (const link of links) {
    if (link.type === 'cross') crossings += 1;
    else {
      doors += 1;
      if (link.isBridgeLink) bridgeDoors += 1;
    }
  }

  const adj = buildAdjacency(strips, links);
  const startId = map?.spawn?.stripId;
  const distances = new Map();
  let maxDistance = 0;
  if (startId && adj.has(startId)) {
    distances.set(startId, 0);
    const queue = [startId];
    while (queue.length) {
      const cur = queue.shift();
      const d = distances.get(cur);
      for (const next of adj.get(cur) || []) {
        if (!distances.has(next)) {
          distances.set(next, d + 1);
          if (d + 1 > maxDistance) maxDistance = d + 1;
          queue.push(next);
        }
      }
    }
  }

  const regionStats = regions.map((region) => {
    const rStrips = strips.filter((s) => s.role !== 'bridge' && s.regionId === region.id);
    const rIds = rStrips.map((s) => s.id);
    const rIdSet = new Set(rIds);
    const edgePairs = new Set();
    for (const link of links) {
      const a = link?.a?.stripId;
      const b = link?.b?.stripId;
      if (rIdSet.has(a) && rIdSet.has(b) && a !== b) {
        edgePairs.add(pairKey(a, b));
      }
    }
    const components = countComponents(rIds, adj);
    const cycles = rIds.length > 0 ? Math.max(0, edgePairs.size - rIds.length + components) : 0;
    const rResource = Math.round(rStrips.reduce((sum, s) => sum + (Number(s.resource) || 0), 0) * 100) / 100;
    const rDifficulty = Math.round(rStrips.reduce((sum, s) => sum + (Number(s.difficulty) || 0), 0) * 100) / 100;
    const noLoopCount = rStrips.filter((s) => s.noLoop).length;
    return {
      regionId: region.id,
      name: region.name,
      color: region.color,
      strips: rStrips.length,
      cells: rStrips.reduce((sum, s) => sum + (s.cells?.length || 0), 0),
      edges: edgePairs.size,
      components,
      connected: rIds.length > 0 && components === 1,
      cycles,
      noLoopCount,
      maxNoLoop: Number.isFinite(region.maxNoLoop) ? region.maxNoLoop : null,
      resource: rResource,
      difficulty: rDifficulty,
    };
  });

  // 统计区域内连接的端点倾向均值（用于量化结构松散度效果）
  const chainOrderByStrip = new Map();
  for (const strip of strips) {
    chainOrderByStrip.set(strip.id, computeChainOrderMap(strip.cells || []));
  }
  let endFactorSum = 0;
  let endFactorSamples = 0;
  for (const link of links) {
    if (link.isBridgeLink) continue;
    const mapA = chainOrderByStrip.get(link.a?.stripId);
    const mapB = chainOrderByStrip.get(link.b?.stripId);
    const fa = mapA?.get(link.a?.cellIndex)?.endFactor;
    const fb = mapB?.get(link.b?.cellIndex)?.endFactor;
    if (Number.isFinite(fa) && Number.isFinite(fb)) {
      endFactorSum += (fa + fb) / 2;
      endFactorSamples += 1;
    }
  }
  const avgConnectionEndFactor = endFactorSamples > 0 ? Math.round((endFactorSum / endFactorSamples) * 1000) / 1000 : 0.5;
  const totalNoLoopCount = strips.filter((s) => s.noLoop).length;

  const bridgeStrips = strips.filter((s) => s.role === 'bridge');
  const bridgePairs = new Map();
  for (const bridge of bridgeStrips) {
    if (Array.isArray(bridge.bridgeBetween) && bridge.bridgeBetween.length === 2) {
      const key = pairKey(bridge.bridgeBetween[0], bridge.bridgeBetween[1]);
      bridgePairs.set(key, (bridgePairs.get(key) || 0) + 1);
    }
  }

  const levels = strips.map((s) => Number(s.level) || 1);
  const enemyLevels = [];
  for (const strip of strips) {
    for (const cell of strip.cells || []) {
      if (Number.isFinite(cell.enemyLevel)) enemyLevels.push(cell.enemyLevel);
    }
  }

  const targetBudget = map?.budget?.target || null;
  const resourceInRange = targetBudget
    ? totalResource >= targetBudget.resourceMin && totalResource <= targetBudget.resourceMax
    : true;
  const difficultyInRange = targetBudget
    ? totalDifficulty >= targetBudget.difficultyMin && totalDifficulty <= targetBudget.difficultyMax
    : true;

  const expectedPairs = [];
  for (let i = 0; i < regions.length; i += 1) {
    for (let j = i + 1; j < regions.length; j += 1) {
      expectedPairs.push(pairKey(regions[i].id, regions[j].id));
    }
  }
  const allPairsBridgedOnce =
    regions.length === 3 &&
    bridgeStrips.length === 3 &&
    expectedPairs.every((key) => bridgePairs.get(key) === 1);

  const allRegionsHaveCycle =
    regionStats.length === 3 &&
    regionStats.every((r) => r.connected && r.cycles >= 1);

  return {
    strips: strips.length,
    regionStripCount: strips.length - bridgeStrips.length,
    bridges: bridgeStrips.length,
    links: links.length,
    doors,
    crossings,
    bridgeDoors,
    uniqueCells: uniqueCellKeys.size,
    bounds: boundsOfCells(allCells),
    totalResource,
    totalDifficulty,
    resourceInRange,
    difficultyInRange,
    reachable: distances.size,
    allReachable: strips.length > 0 && distances.size === strips.length,
    maxDistance,
    minLevel: levels.length ? Math.min(...levels) : 1,
    maxLevel: levels.length ? Math.max(...levels) : 1,
    maxEnemyLevel: enemyLevels.length ? Math.max(...enemyLevels) : 1,
    cellTypeCounts,
    regions: regionStats,
    triangleValid: allPairsBridgedOnce,
    allRegionsHaveCycle,
    maxCellStack: Math.max(0, ...[...cellOwners.values()].map((list) => list.length)),
    avgConnectionEndFactor,
    totalNoLoopCount,
  };
}

export function validateFloorMap(map) {
  const issues = [];
  const stats = computeFloorStats(map);
  const strips = Array.isArray(map?.strips) ? map.strips : [];
  const links = Array.isArray(map?.links) ? map.links : [];
  const regions = Array.isArray(map?.regions) ? map.regions : [];
  const stripMap = new Map(strips.map((s) => [s.id, s]));

  for (const strip of strips) {
    if (!validateOpenDirs(strip.cells) || !hasConnectedOpenEdges(strip.cells)) {
      issues.push(issue('error', 'floor.strip.edges', `条带「${strip.id}」的内部开放边无效或不连通。`));
    } else if (strip.role === 'bridge' && !isOpenChain(strip.cells)) {
      issues.push(issue('error', 'floor.bridge.chain', `桥「${strip.id}」的开放边不是单链。`));
    }
  }
  if (regions.length === 0 && !map?.generator) return { ok: issues.length === 0, issues, stats, errors: issues.map((i) => i.message), warnings: [] };

  if (regions.length !== 3) {
    issues.push(issue('error', 'floor.regions.count', `单层地图需要恰好 3 个区域，当前为 ${regions.length} 个。`));
  }

  // 1. 区域内部连通性与环路检查
  for (const r of stats.regions) {
    if (r.strips === 0) {
      issues.push(issue('error', 'floor.region.empty', `区域「${r.name}」没有任何条带。`));
    } else if (!r.connected) {
      issues.push(issue('error', 'floor.region.disconnected', `区域「${r.name}」内部不连通（${r.components} 个分量）。`));
    }
    if (r.cycles < 1) {
      issues.push(issue('error', 'floor.region.noCycle', `区域「${r.name}」内部未形成环路（当前环数 ${r.cycles}）。`));
    }
  }

  // 2. 独立桥接条带检查（三对区域每对有且仅有 1 条独立桥，且仅用门连接两区）
  const bridges = strips.filter((s) => s.role === 'bridge');
  if (bridges.length !== 3) {
    issues.push(issue('error', 'floor.bridges.count', `三区域三角形需要 3 条独立桥接条带，当前为 ${bridges.length} 条。`));
  }

  const pairBridges = new Map();
  for (const bridge of bridges) {
    const connectedLinks = links.filter((l) => l.a?.stripId === bridge.id || l.b?.stripId === bridge.id);
    const nonDoors = connectedLinks.filter((l) => l.type !== 'door');
    if (nonDoors.length > 0) {
      issues.push(issue('error', 'floor.bridge.nonDoor', `桥接条带「${bridge.id}」存在非门连接（跨区仅允许门）。`));
    }
    const targetRegions = new Set();
    for (const link of connectedLinks) {
      const otherId = link.a?.stripId === bridge.id ? link.b?.stripId : link.a?.stripId;
      const other = stripMap.get(otherId);
      if (!other || other.role === 'bridge') {
        issues.push(issue('error', 'floor.bridge.invalidEndpoint', `桥接条带「${bridge.id}」连接到了非法端点。`));
      } else {
        targetRegions.add(other.regionId);
      }
    }
    if (connectedLinks.length !== 2 || targetRegions.size !== 2) {
      issues.push(
        issue(
          'error',
          'floor.bridge.endpoints',
          `桥接条带「${bridge.id}」应通过 2 扇门连接 2 个不同区域，当前连接 ${connectedLinks.length} 条边 / ${targetRegions.size} 个区域。`,
        ),
      );
    } else {
      const key = pairKey(...targetRegions);
      pairBridges.set(key, (pairBridges.get(key) || 0) + 1);

      // 校验桥接条带的两扇跨区门是否分别连在不同的两端
      const cells = bridge.cells || [];
      if (cells.length >= 2) {
        const bridgeCellIndices = connectedLinks.map((link) =>
          link.a?.stripId === bridge.id ? link.a.cellIndex : link.b.cellIndex,
        );
        const [idx1, idx2] = bridgeCellIndices;
        if (idx1 === idx2) {
          issues.push(
            issue(
              'error',
              'floor.bridge.sameCell',
              `桥接条带「${bridge.templateName}(${bridge.id})」的两扇跨区门连在了同一格上。`,
            ),
          );
        } else {
          const hasPortals = cells.some((c) => c.isPortal);
          if (!hasPortals) {
            const orderMap = computeChainOrderMap(cells);
            const i1 = orderMap.get(idx1);
            const i2 = orderMap.get(idx2);
            const oppositeEnds =
              Boolean(i1 && i2) && ((i1.isHead && i2.isTail) || (i1.isTail && i2.isHead));
            if (!oppositeEnds) {
              issues.push(
                issue(
                  'error',
                  'floor.bridge.oppositeEnds',
                  `桥接条带「${bridge.templateName}(${bridge.id})」未分别在首尾两端连接两个区域。`,
                ),
              );
            }
          }
        }
      }
    }
  }

  if (regions.length === 3) {
    for (let i = 0; i < 3; i += 1) {
      for (let j = i + 1; j < 3; j += 1) {
        const key = pairKey(regions[i].id, regions[j].id);
        const count = pairBridges.get(key) || 0;
        if (count !== 1) {
          issues.push(
            issue(
              'error',
              'floor.triangle.pair',
              `区域「${regions[i].name}」与「${regions[j].name}」之间应有且仅有 1 条桥接条带，当前为 ${count} 条。`,
            ),
          );
        }
      }
    }
  }

  // 3. 连接几何校验 + 禁止平行重边 + 禁止跨区直连
  const linkPairs = new Set();
  for (const link of links) {
    const a = stripMap.get(link.a?.stripId);
    const b = stripMap.get(link.b?.stripId);
    if (!a || !b || a.id === b.id) {
      issues.push(issue('error', 'floor.link.invalid', `连接「${link.id}」引用了无效条带。`));
      continue;
    }
    const pk = pairKey(a.id, b.id);
    if (linkPairs.has(pk)) {
      issues.push(issue('error', 'floor.link.parallel', `条带「${a.id}」与「${b.id}」存在重复连接。`));
    }
    linkPairs.add(pk);

    const isRegionPair = a.role !== 'bridge' && b.role !== 'bridge';
    if (isRegionPair && a.regionId !== b.regionId) {
      issues.push(
        issue(
          'error',
          'floor.link.directCrossRegion',
          `条带「${a.id}」与「${b.id}」分属不同区域，不允许直接连接。`,
        ),
      );
    }

    const cellA = a.cells?.[link.a.cellIndex];
    const cellB = b.cells?.[link.b.cellIndex];
    if (!cellA || !cellB) {
      issues.push(issue('error', 'floor.link.cell', `连接「${link.id}」的格索引越界。`));
      continue;
    }

    if (link.type === 'door') {
      const dist = Math.abs(cellA.x - cellB.x) + Math.abs(cellA.y - cellB.y);
      if (dist !== 1) {
        issues.push(issue('error', 'floor.door.notAdjacent', `门连接「${link.id}」的两端格不相邻。`));
      }
      const dir = DIRECTIONS[link.dir];
      if (!dir || cellA.x + dir.dx !== cellB.x || cellA.y + dir.dy !== cellB.y) {
        issues.push(issue('error', 'floor.door.dir', `门连接「${link.id}」的方向与格坐标不匹配。`));
      }
    } else if (link.type === 'cross') {
      if (a.role === 'bridge' || b.role === 'bridge' || a.regionId !== b.regionId) {
        issues.push(issue('error', 'floor.cross.crossRegion', `交叉连接「${link.id}」不允许跨区或作用在桥接条带上。`));
      }
      if (cellA.x !== cellB.x || cellA.y !== cellB.y) {
        issues.push(issue('error', 'floor.cross.coord', `交叉连接「${link.id}」未共享同一格坐标。`));
      }
      if (needsStrictCross(a.cells) || needsStrictCross(b.cells)) {
        const axisA = straightAxis(a.cells, link.a.cellIndex), axisB = straightAxis(b.cells, link.b.cellIndex);
        if (!axisA || !axisB || axisA === axisB) issues.push(issue('error', 'floor.cross.axis', `交叉「${link.id}」没有垂直的直通开放边。`));
      }
      if (cellA.type !== 'empty' && cellB.type !== 'empty') {
        issues.push(
          issue(
            'error',
            'floor.cross.bothNonEmpty',
            `交叉连接「${link.id}」重叠了两个非空格（要求至少一方为通道/空）。`,
          ),
        );
      }
    }
  }

  // 4. 格重叠校验：同区交叉仅允许单格重叠且有 cross link；跨区/桥接零重叠；每格最多 2 条带
  const cellOwners = new Map();
  for (const strip of strips) {
    for (const cell of strip.cells || []) {
      const k = coordKey(cell.x, cell.y);
      const list = cellOwners.get(k) || [];
      list.push(strip.id);
      cellOwners.set(k, list);
    }
  }
  for (const [k, list] of cellOwners) {
    if (list.length > 2) {
      issues.push(issue('error', 'floor.cell.stack', `格 (${k}) 被 ${list.length} 条条带重叠占据，超过上限 2。`));
    }
  }

  const crossLinkMap = new Map();
  for (const link of links) {
    if (link.type === 'cross') {
      crossLinkMap.set(pairKey(link.a.stripId, link.b.stripId), link);
    }
  }

  for (let i = 0; i < strips.length; i += 1) {
    for (let j = i + 1; j < strips.length; j += 1) {
      const a = strips[i];
      const b = strips[j];
      const overlaps = overlapCells(a.cells || [], b.cells || []);
      if (overlaps.length === 0) continue;
      const pk = pairKey(a.id, b.id);
      const hasCross = crossLinkMap.has(pk);
      if (!hasCross) {
        issues.push(issue('error', 'floor.overlap.unlinked', `条带「${a.id}」与「${b.id}」在无交叉连接时发生格重叠。`));
      } else if (overlaps.length > 1) {
        issues.push(issue('error', 'floor.cross.multiCell', `交叉条带「${a.id}」与「${b.id}」共享了 ${overlaps.length} 格（仅允许单格交叉）。`));
      }
    }
  }

  // 5. 出生点与可达性
  if (!map?.spawn || !stripMap.has(map.spawn.stripId)) {
    issues.push(issue('error', 'floor.spawn.missing', '单层地图缺少有效的出生点。'));
  } else if (!stats.allReachable) {
    issues.push(issue('error', 'floor.unreachable', `存在不可达条带：可达 ${stats.reachable}/${stats.strips}。`));
  }

  // 5.5 非环属性校验（noLoop 条带度数必须严格 === 1，且不超过区域 maxNoLoop 上限）
  const degree = new Map(strips.map((s) => [s.id, 0]));
  for (const link of links) {
    if (degree.has(link.a?.stripId)) degree.set(link.a.stripId, degree.get(link.a.stripId) + 1);
    if (degree.has(link.b?.stripId)) degree.set(link.b.stripId, degree.get(link.b.stripId) + 1);
  }
  for (const strip of strips) {
    if (strip.noLoop && degree.get(strip.id) !== 1) {
      issues.push(
        issue(
          'error',
          'floor.noLoop.degree',
          `非环条带「${strip.templateName}(${strip.id})」仅可作为端点支路（度数须为 1），当前度数为 ${degree.get(strip.id)}。`,
        ),
      );
    }
  }
  for (const r of stats.regions) {
    if (r.maxNoLoop !== null && r.noLoopCount > r.maxNoLoop) {
      issues.push(
        issue(
          'error',
          'floor.noLoop.limit',
          `区域「${r.name}」的非环条带数 ${r.noLoopCount} 超过了上限 ${r.maxNoLoop}。`,
        ),
      );
    }
  }

  // 5.6 出入口格子约束校验（若条带存在 isPortal 格，则所有门与交叉连接必须落在该格上）
  for (const link of links) {
    for (const end of [link.a, link.b]) {
      const strip = stripMap.get(end?.stripId);
      if (!strip) continue;
      const hasPortals = (strip.cells || []).some((c) => c.isPortal);
      if (!hasPortals) continue;
      const cell = strip.cells?.[end.cellIndex];
      if (!cell || !cell.isPortal) {
        issues.push(
          issue(
            'error',
            'floor.portal.violation',
            `条带「${strip.templateName}(${strip.id})」设置了出入口格，但连接「${link.id}」落在了非出入口格上。`,
          ),
        );
      }
    }
  }

  // 6. 数值预算区间提示（warning）
  const target = map?.budget?.target;
  if (target) {
    if (!stats.resourceInRange) {
      issues.push(
        issue(
          'warning',
          'floor.budget.resource',
          `总资源量 ${stats.totalResource} 落在目标区间 [${target.resourceMin}, ${target.resourceMax}] 之外。`,
        ),
      );
    }
    if (!stats.difficultyInRange) {
      issues.push(
        issue(
          'warning',
          'floor.budget.difficulty',
          `总难度量 ${stats.totalDifficulty} 落在目标区间 [${target.difficultyMin}, ${target.difficultyMax}] 之外。`,
        ),
      );
    }
  }

  const errors = issues.filter((item) => item.level === 'error');
  const warnings = issues.filter((item) => item.level === 'warning');
  return {
    ok: errors.length === 0,
    stats,
    issues,
    errors,
    warnings,
  };
}
