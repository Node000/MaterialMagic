/**
 * 将地图生成池预览接口返回的物理地板 JSON ({ floor, metrics, layout? })
 * 适配为 FloorCanvas 与检视面板所需的完整 FloorMap 视图模型。
 *
 * - 当 Unity 预览桥返回 layout.strips（含 slot/templateId）时精确还原 A/B/C 三区与跨区桥；
 * - 当仅有规范 floor.cells + floor.edges 时，仍自动重建条带、门、交叉、BFS 跳数、等级与三区归属。
 */

import { DEFAULT_CELL_TYPES, DIRECTIONS, DUNGEON_REGION_PRESETS, clamp } from '../core/constants.js';
import { floorSharedWalls } from '../core/floor-contacts.js';
import { boundsOfCells, coordKey } from '../core/polyomino.js';
import { cellTypeMap, computeTemplateMetrics } from '../core/strip-templates.js';

const SLOT_LABELS = ['A', 'B', 'C'];
const SLOT_FALLBACK_COLORS = ['#5d9cec', '#e89c52', '#69b984', '#b187e6', '#4cc6c0', '#e0768b'];

function presetForRegion(regionTypeId) {
  return DUNGEON_REGION_PRESETS.find((r) => r.id === regionTypeId) || null;
}

export function regionPaletteForEntry(entry) {
  const used = new Set();
  const slots = entry?.regions || [];
  return SLOT_LABELS.map((slotLabel, idx) => {
    const cfg = slots[idx] || { regionTypeId: 'city', stripMin: 5, stripMax: 6, maxNoLoop: 1 };
    const preset = presetForRegion(cfg.regionTypeId);
    let color = preset?.color || SLOT_FALLBACK_COLORS[idx % SLOT_FALLBACK_COLORS.length];
    if (used.has(color)) {
      color = SLOT_FALLBACK_COLORS.find((c) => !used.has(c)) || color;
    }
    used.add(color);
    const baseName = preset?.name || cfg.regionTypeId || `区域 ${slotLabel}`;
    const shortBase = preset?.shortName || cfg.regionTypeId || slotLabel;
    return {
      id: `slot_${idx}`,
      slotIndex: idx,
      slotLabel,
      regionTypeId: cfg.regionTypeId,
      name: `${slotLabel} · ${baseName}`,
      shortName: `${slotLabel}·${shortBase}`,
      color,
      role: idx === 0 ? 'start' : 'wing',
      stripMin: cfg.stripMin,
      stripMax: cfg.stripMax,
      maxNoLoop: cfg.maxNoLoop,
    };
  });
}

function numericStripOrder(id) {
  const m = /^s(\d+)$/.exec(id);
  return m ? Number(m[1]) : Number.MAX_SAFE_INTEGER;
}

function inferTriangleAssignment(stripIds, adj, spawnStripId, entryRegions) {
  // Unity SecondFloorPatternGenerator 按区域 A -> B -> C 顺序编号 s0..s(K-1)，最后 3 条为跨区桥。
  const sorted = [...stripIds].sort((a, b) => numericStripOrder(a) - numericStripOrder(b) || a.localeCompare(b));
  const result = new Map();
  if (sorted.length < 12) return null;

  const candidateBridges = sorted.slice(-3);
  const allDegreeTwo = candidateBridges.every((id) => (adj.get(id)?.size || 0) === 2);
  if (!allDegreeTwo) return null;

  const bridgeSet = new Set(candidateBridges);
  const visited = new Set();
  const components = [];
  for (const id of sorted) {
    if (bridgeSet.has(id) || visited.has(id)) continue;
    const comp = [];
    const queue = [id];
    visited.add(id);
    while (queue.length) {
      const cur = queue.shift();
      comp.push(cur);
      for (const nxt of adj.get(cur) || []) {
        if (bridgeSet.has(nxt) || visited.has(nxt)) continue;
        visited.add(nxt);
        queue.push(nxt);
      }
    }
    components.push(comp);
  }
  if (components.length !== 3) return null;

  // 验证每个区域规模与三槽位配置大致吻合
  const spawnCompIdx = components.findIndex((c) => c.includes(spawnStripId));
  if (spawnCompIdx < 0) return null;
  const remaining = components.filter((_, idx) => idx !== spawnCompIdx);
  remaining.sort((a, b) => numericStripOrder(a[0]) - numericStripOrder(b[0]));
  const ordered = [components[spawnCompIdx], ...remaining];

  // 允许在配置微调时宽容匹配；只要拆分出 3 个非空连通块即采用
  const plausible = ordered.every((comp, idx) => {
    const cfg = entryRegions?.[idx];
    if (!cfg) return comp.length >= 2;
    return comp.length >= Math.max(2, (cfg.stripMin || 3) - 1) && comp.length <= (cfg.stripMax || 20) + 1;
  });
  if (!plausible) return null;

  ordered.forEach((comp, slot) => {
    for (const id of comp) result.set(id, slot);
  });
  for (const id of candidateBridges) result.set(id, -1);
  return result;
}

export function adaptPoolPreviewToFloorMap({
  floor,
  metrics = {},
  layout = null,
  entry,
  seed,
  cellTypes = DEFAULT_CELL_TYPES,
  stripTemplates = [],
}) {
  const regions = regionPaletteForEntry(entry);
  const tMap = cellTypeMap(cellTypes);
  const tplById = new Map((stripTemplates || []).map((t) => [t.id, t]));
  const layoutStripById = new Map((layout?.strips || []).map((s) => [s.id, s]));

  // 1. 按 stripIds 重建条带与格归属
  const stripMap = new Map();
  const cellOwners = new Map();

  for (const rawCell of floor.cells) {
    const key = coordKey(rawCell.x, rawCell.y);
    const ids = Array.isArray(rawCell.stripIds) && rawCell.stripIds.length ? [...new Set(rawCell.stripIds)] : ['s0'];
    const owners = [];
    for (let i = 0; i < ids.length; i += 1) {
      const sid = ids[i];
      if (!stripMap.has(sid)) {
        const meta = layoutStripById.get(sid) || null;
        const tpl = meta?.templateId ? tplById.get(meta.templateId) : null;
        stripMap.set(sid, {
          id: sid,
          templateId: meta?.templateId || sid,
          templateName: tpl?.name || meta?.templateId || sid,
          role: meta ? (meta.slot === -1 ? 'bridge' : 'normal') : 'normal',
          slot: meta ? meta.slot : null,
          regionId: meta && meta.slot >= 0 && meta.slot < regions.length ? regions[meta.slot].id : null,
          noLoop: Boolean(tpl?.noLoop),
          cells: [],
          distanceFromSpawn: 0,
          level: entry?.progression?.baseLevel ?? 1,
          resource: 0,
          difficulty: 0,
          bridgeBetween: [],
          _tpl: tpl,
        });
      }
      const strip = stripMap.get(sid);
      const isPrimaryOwner = i === 0;
      const cell = {
        index: strip.cells.length,
        x: rawCell.x,
        y: rawCell.y,
        type: isPrimaryOwner ? rawCell.type || 'empty' : 'empty',
        enemyLevel: isPrimaryOwner && rawCell.enemyLevel != null ? rawCell.enemyLevel : null,
        difficultyOffset: 0,
        isPortal: false,
      };
      strip.cells.push(cell);
      owners.push({ strip, cell });
    }
    cellOwners.set(key, owners);
  }

  const strips = [...stripMap.values()].sort(
    (a, b) => numericStripOrder(a.id) - numericStripOrder(b.id) || a.id.localeCompare(b.id),
  );

  // 2. 确定出生条带
  const spawnKey = coordKey(floor.spawn.x, floor.spawn.y);
  const spawnOwners = cellOwners.get(spawnKey) || [];
  const spawnOwner =
    spawnOwners.find((o) => o.cell.type === 'spawn') ||
    spawnOwners[0] ||
    (strips[0] ? { strip: strips[0], cell: strips[0].cells[0] } : null);
  const spawnStripId = spawnOwner?.strip?.id || null;

  // 3. 构建门与交叉连接，同时建立条带邻接图
  const adj = new Map(strips.map((s) => [s.id, new Set()]));
  const links = [];
  let linkSerial = 0;

  for (const edge of floor.edges || []) {
    if (edge.type !== 'door') continue;
    const d = DIRECTIONS[edge.dir];
    if (!d) continue;
    const ownersA = cellOwners.get(coordKey(edge.x, edge.y)) || [];
    const ownersB = cellOwners.get(coordKey(edge.x + d.dx, edge.y + d.dy)) || [];
    if (!ownersA.length || !ownersB.length) continue;

    let pairA = ownersA[0];
    let pairB = ownersB.find((o) => o.strip.id !== pairA.strip.id) || ownersB[0];
    if (pairA.strip.id === pairB.strip.id && ownersA.length > 1) {
      pairA = ownersA[1];
      pairB = ownersB.find((o) => o.strip.id !== pairA.strip.id) || ownersB[0];
    }

    adj.get(pairA.strip.id)?.add(pairB.strip.id);
    adj.get(pairB.strip.id)?.add(pairA.strip.id);
    links.push({
      id: `door_${++linkSerial}`,
      type: 'door',
      dir: edge.dir,
      a: { stripId: pairA.strip.id, cellIndex: pairA.cell.index },
      b: { stripId: pairB.strip.id, cellIndex: pairB.cell.index },
      isBridgeLink: false,
    });
  }

  for (const [, owners] of cellOwners) {
    if (owners.length < 2) continue;
    const [a, b] = owners;
    adj.get(a.strip.id)?.add(b.strip.id);
    adj.get(b.strip.id)?.add(a.strip.id);
    links.push({
      id: `cross_${++linkSerial}`,
      type: 'cross',
      a: { stripId: a.strip.id, cellIndex: a.cell.index },
      b: { stripId: b.strip.id, cellIndex: b.cell.index },
      cell: { x: a.cell.x, y: a.cell.y },
      isBridgeLink: false,
    });
  }

  // 4. 若无 layout 元数据，尝试按三角图结构推断三区与跨区桥
  if (!layout?.strips?.length) {
    const inferred = inferTriangleAssignment(
      strips.map((s) => s.id),
      adj,
      spawnStripId,
      entry?.regions,
    );
    for (const strip of strips) {
      const slot = inferred ? inferred.get(strip.id) : 0;
      strip.slot = slot;
      if (slot === -1) {
        strip.role = 'bridge';
        strip.regionId = null;
      } else {
        strip.role = 'normal';
        strip.regionId = regions[slot ?? 0]?.id || regions[0].id;
      }
    }
  }

  // 标记出生条带 & 回填桥接两端区域 & isBridgeLink
  for (const strip of strips) {
    if (strip.id === spawnStripId && strip.role !== 'bridge') strip.role = 'spawn';
    if (strip.role === 'bridge') {
      const neighborRegions = [...(adj.get(strip.id) || [])]
        .map((nid) => stripMap.get(nid)?.regionId)
        .filter(Boolean);
      strip.bridgeBetween = [...new Set(neighborRegions)];
    }
    if (!strip.noLoop && strip.role === 'normal' && (adj.get(strip.id)?.size || 0) === 1) {
      strip.noLoop = true;
    }
  }

  for (const link of links) {
    const sa = stripMap.get(link.a.stripId);
    const sb = stripMap.get(link.b.stripId);
    link.isBridgeLink = Boolean(sa?.role === 'bridge' || sb?.role === 'bridge');
  }

  // 5. BFS 计算条带跳数距离与条带等级
  const dist = new Map();
  if (spawnStripId) {
    dist.set(spawnStripId, 0);
    const queue = [spawnStripId];
    for (let i = 0; i < queue.length; i += 1) {
      const cur = queue[i];
      const d0 = dist.get(cur);
      for (const nxt of adj.get(cur) || []) {
        if (!dist.has(nxt)) {
          dist.set(nxt, d0 + 1);
          queue.push(nxt);
        }
      }
    }
  }

  const prog = entry?.progression || { baseLevel: 1, levelPerHop: 0.75, maxLevel: 20 };
  const baseLevel = Number.isFinite(prog.baseLevel) ? prog.baseLevel : 1;
  const levelPerHop = Number.isFinite(prog.levelPerHop) ? prog.levelPerHop : 0.75;
  const maxLevel = Number.isFinite(prog.maxLevel) ? prog.maxLevel : 20;

  let maxDistance = 0;
  let minLv = Infinity;
  let maxLv = 0;
  let maxEnemyLevel = 0;

  for (const strip of strips) {
    const d = dist.has(strip.id) ? dist.get(strip.id) : 0;
    strip.distanceFromSpawn = d;
    strip.level = clamp(Math.round(baseLevel + d * levelPerHop), 1, maxLevel);
    maxDistance = Math.max(maxDistance, d);
    minLv = Math.min(minLv, strip.level);
    maxLv = Math.max(maxLv, strip.level);

    if (strip._tpl) {
      const m = computeTemplateMetrics(strip._tpl, cellTypes);
      strip.resource = m.resource;
      strip.difficulty = m.difficulty;
    } else {
      let res = 0;
      let dif = 0;
      for (const cell of strip.cells) {
        const def = tMap.get(cell.type);
        if (def) {
          res += Number(def.resource) || 0;
          dif += Number(def.difficulty) || 0;
        }
      }
      strip.resource = Math.round(res * 100) / 100;
      strip.difficulty = Math.round(dif * 100) / 100;
    }
    delete strip._tpl;

    for (const cell of strip.cells) {
      if (Number.isFinite(cell.enemyLevel)) {
        maxEnemyLevel = Math.max(maxEnemyLevel, cell.enemyLevel);
      }
    }
  }

  // 6. 汇总分区域统计、环数与预算
  const regionStats = regions.map((r) => {
    const rStrips = strips.filter((s) => s.role !== 'bridge' && s.regionId === r.id);
    const idSet = new Set(rStrips.map((s) => s.id));
    let internalEdges = 0;
    const seenEdge = new Set();
    for (const s of rStrips) {
      for (const nxt of adj.get(s.id) || []) {
        if (!idSet.has(nxt)) continue;
        const ek = s.id < nxt ? `${s.id}|${nxt}` : `${nxt}|${s.id}`;
        if (!seenEdge.has(ek)) {
          seenEdge.add(ek);
          internalEdges += 1;
        }
      }
    }
    let comps = 0;
    const vis = new Set();
    for (const s of rStrips) {
      if (vis.has(s.id)) continue;
      comps += 1;
      const q = [s.id];
      vis.add(s.id);
      while (q.length) {
        const u = q.shift();
        for (const v of adj.get(u) || []) {
          if (idSet.has(v) && !vis.has(v)) {
            vis.add(v);
            q.push(v);
          }
        }
      }
    }
    const cycles = Math.max(0, internalEdges - rStrips.length + comps);
    const noLoopCount = rStrips.filter((s) => s.noLoop).length;
    const resource = Math.round(rStrips.reduce((sum, s) => sum + (s.resource || 0), 0) * 100) / 100;
    const difficulty = Math.round(rStrips.reduce((sum, s) => sum + (s.difficulty || 0), 0) * 100) / 100;
    return {
      ...r,
      strips: rStrips.length,
      cycles,
      noLoopCount,
      resource,
      difficulty,
    };
  });

  const bridgeStrips = strips.filter((s) => s.role === 'bridge');
  const computedRes = Math.round(strips.reduce((sum, s) => sum + (s.resource || 0), 0) * 100) / 100;
  const computedDif = Math.round(strips.reduce((sum, s) => sum + (s.difficulty || 0), 0) * 100) / 100;
  const totalResource = Number.isFinite(metrics.resource) ? metrics.resource : floor.budget?.actual?.resource ?? computedRes;
  const totalDifficulty = Number.isFinite(metrics.difficulty) ? metrics.difficulty : floor.budget?.actual?.difficulty ?? computedDif;

  const target = floor.budget?.target || entry?.budget || {};
  const within = (val, min, max) =>
    (!Number.isFinite(min) || val >= min) && (!Number.isFinite(max) || val <= max);
  const hasResBounds = Number.isFinite(target.resourceMin) || Number.isFinite(target.resourceMax);
  const hasDifBounds = Number.isFinite(target.difficultyMin) || Number.isFinite(target.difficultyMax);
  const resourceInRange =
    typeof metrics.resourceInRange === 'boolean'
      ? metrics.resourceInRange
      : hasResBounds
        ? within(totalResource, target.resourceMin, target.resourceMax)
        : null;
  const difficultyInRange =
    typeof metrics.difficultyInRange === 'boolean'
      ? metrics.difficultyInRange
      : hasDifBounds
        ? within(totalDifficulty, target.difficultyMin, target.difficultyMax)
        : null;

  const bounds = boundsOfCells(floor.cells);
  const sharedWalls = floorSharedWalls(floor);
  const doors = links.filter((l) => l.type === 'door').length;
  const crossings = links.filter((l) => l.type === 'cross').length;
  const reachable = dist.size;
  const allRegionsHaveCycle = regionStats.every((r) => r.strips > 0 && r.cycles >= 1);
  const triangleValid =
    bridgeStrips.length === 3 &&
    regionStats.every((r) => r.strips > 0) &&
    bridgeStrips.every((b) => (b.bridgeBetween?.length || 0) === 2);

  return {
    id: floor.id,
    _renderKey: `${floor.id}:${seed}:${floor.cells.length}:${floor.edges.length}`,
    seed: floor.seed ?? seed,
    profileName: entry?.name || '地图生成池预览',
    regions,
    strips,
    links,
    physicalEdges: floor.edges,
    spawn: spawnOwner
      ? {
          stripId: spawnOwner.strip.id,
          cellIndex: spawnOwner.cell.index,
          x: floor.spawn.x,
          y: floor.spawn.y,
        }
      : { x: floor.spawn.x, y: floor.spawn.y },
    budget: {
      target,
      actual: { resource: totalResource, difficulty: totalDifficulty },
    },
    progression: prog,
    stats: {
      strips: strips.length,
      regionStripCount: strips.length - bridgeStrips.length,
      bridges: bridgeStrips.length,
      doors,
      crossings,
      tileCount: metrics.tileCount ?? floor.cells.length,
      edgeCount: floor.edges.length,
      bounds,
      totalResource,
      totalDifficulty,
      resourceInRange,
      difficultyInRange,
      maxDistance,
      minLevel: Number.isFinite(minLv) ? minLv : baseLevel,
      maxLevel: maxLv || baseLevel,
      maxEnemyLevel,
      reachable,
      allReachable: reachable === strips.length,
      regions: regionStats,
      allRegionsHaveCycle,
      triangleValid,
      sharedWalls,
    },
  };
}
