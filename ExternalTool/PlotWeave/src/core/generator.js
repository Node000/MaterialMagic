/**
 * 标准地图生成器。
 *
 * 生成规则刻意保持“可解释”：
 *   1. 先按区域配额决定这一张图要长多少块；
 *   2. 从出生地块开始，每次从已有地块的一个可用接口长出新地块；
 *   3. 新地块可以通过门接在相邻格，也可以垂直穿过已有格形成交叉；
 *   4. 区域之间只在配额和方案上限允许时连接；
 *   5. 最后挂子房间、出生点并做完整校验。
 *
 * 所有随机性来自 RNG(seed)，不使用 Math.random。
 */

import {
  DIRECTIONS,
  clamp,
} from './constants.js';
import {
  cellsFor,
  cellKey,
  inBounds,
} from './geometry.js';
import {
  plotKindById,
  activeRegionsForProfile,
  canConnectKinds,
  canCrossKinds,
  plotKindsForRegion,
  profileById,
  subRoomKindById,
} from './rules.js';
import { RNG, normalizeSeed, randomSeed } from './rng.js';
import { registry } from './registry.js';
import { portsForPlot, sideIsAllowed } from './ports.js';
import { computeMapStats, validateMap } from './validate-map.js';

const GENERATOR_ID = 'standard';
const GENERATOR_VERSION = 1;
const CANDIDATE_LIMIT = 80;

const clone = (value) => JSON.parse(JSON.stringify(value));
const otherAxis = (axis) => (axis === 'h' ? 'v' : 'h');
const stepOf = (axis) => (axis === 'h' ? { x: 1, y: 0 } : { x: 0, y: 1 });

function range(min, max) {
  const values = [];
  for (let value = min; value <= max; value += 1) values.push(value);
  return values;
}

function centeredOrigin(width, height, axis, length) {
  const centerX = Math.floor(width / 2);
  const centerY = Math.floor(height / 2);
  if (axis === 'h') {
    return {
      x: clamp(centerX - Math.floor((length - 1) / 2), 0, Math.max(0, width - length)),
      y: clamp(centerY, 0, Math.max(0, height - 1)),
    };
  }
  return {
    x: clamp(centerX, 0, Math.max(0, width - 1)),
    y: clamp(centerY - Math.floor((length - 1) / 2), 0, Math.max(0, height - length)),
  };
}

function regionCapacity(project, regionId) {
  return plotKindsForRegion(project, regionId).reduce((sum, kind) => {
    const limit = kind.generation.max;
    if (limit <= 0 || kind.connections.max <= 0) return sum;
    return sum + (kind.generation.unique ? Math.min(1, limit) : limit);
  }, 0);
}

function activeRegions(project, profile, startRegionId) {
  const regions = activeRegionsForProfile(project, profile);
  if (!regions.some((region) => region.id === startRegionId)) {
    const start = project.regions.find((region) => region.id === startRegionId);
    if (start) regions.unshift(start);
  }
  return regions;
}

function planQuotas(project, profile, regions, startRegionId, target, rng) {
  const quotas = new Map();
  const caps = new Map();
  for (const region of regions) {
    const cap = Math.min(region.generation.max, regionCapacity(project, region.id));
    caps.set(region.id, region.id === startRegionId ? Math.max(1, cap) : Math.max(0, cap));
    quotas.set(region.id, 0);
  }

  const base = new Map();
  let total = 0;
  for (const region of regions) {
    const value = Math.min(Math.max(0, region.generation.min), caps.get(region.id));
    base.set(region.id, value);
    total += value;
  }

  if (total > target) {
    // 目标小于所有区域的最小值：优先让出生区域拿到 1，其余按权重分配。
    for (const region of regions) quotas.set(region.id, 0);
    quotas.set(startRegionId, 1);
    let remaining = target - 1;
    while (remaining > 0) {
      const pool = regions.filter((region) => quotas.get(region.id) < caps.get(region.id));
      if (pool.length === 0) break;
      const region = rng.weighted(pool, (candidate) => Math.max(0.01, candidate.generation.weight));
      if (!region) break;
      quotas.set(region.id, quotas.get(region.id) + 1);
      remaining -= 1;
    }
    return quotas;
  }

  for (const region of regions) quotas.set(region.id, base.get(region.id) || 0);

  if ((quotas.get(startRegionId) || 0) < 1) {
    let need = 1 - (quotas.get(startRegionId) || 0);
    quotas.set(startRegionId, (quotas.get(startRegionId) || 0) + need);
    // 从配额最大的非出生区域里扣除，保持总数不变。
    while (need > 0) {
      const donor = regions
        .filter((region) => region.id !== startRegionId && quotas.get(region.id) > 0)
        .sort((a, b) => quotas.get(b.id) - quotas.get(a.id))[0];
      if (!donor) break;
      quotas.set(donor.id, quotas.get(donor.id) - 1);
      need -= 1;
    }
  }

  let remaining = target - [...quotas.values()].reduce((sum, value) => sum + value, 0);
  while (remaining > 0) {
    const pool = regions.filter((region) => quotas.get(region.id) < caps.get(region.id));
    if (pool.length === 0) break;
    const region = rng.weighted(pool, (candidate) => {
      const left = caps.get(candidate.id) - quotas.get(candidate.id);
      return Math.max(0.01, left) * Math.max(0.01, candidate.generation.weight);
    });
    if (!region) break;
    quotas.set(region.id, quotas.get(region.id) + 1);
    remaining -= 1;
  }

  // 极端情况下（start 强制 1 导致溢出）从非 start 区域回退。
  let overflow = [...quotas.values()].reduce((sum, value) => sum + value, 0) - target;
  while (overflow > 0) {
    const donor = regions
      .filter((region) => region.id !== startRegionId && quotas.get(region.id) > 0)
      .sort((a, b) => quotas.get(b.id) - quotas.get(a.id))[0];
    if (!donor) break;
    quotas.set(donor.id, quotas.get(donor.id) - 1);
    overflow -= 1;
  }

  return quotas;
}

function freeCells(cells, width, height, occupied, allowedKey = null) {
  for (const cell of cells) {
    if (!inBounds(cell.x, cell.y, width, height)) return false;
    const key = cellKey(cell.x, cell.y);
    if (allowedKey && key === allowedKey) continue;
    if (occupied.has(key)) return false;
  }
  return true;
}

function pickStart(project, profile, regions, target, width, height, rng, issues) {
  let kind = plotKindById(project, profile.startKindId);
  let region = kind ? project.regions.find((candidate) => candidate.id === kind.regionId) : null;
  if (!kind || !region || !regions.some((candidate) => candidate.id === region.id)) {
    const fallbackRegion = regions.find((candidate) =>
      project.plotKinds.some((plotKind) => plotKind.regionId === candidate.id),
    );
    kind =
      project.plotKinds.find((candidate) => candidate.regionId === fallbackRegion?.id && candidate.spawnEligible) ||
      project.plotKinds.find((candidate) => candidate.regionId === fallbackRegion?.id) ||
      project.plotKinds[0];
    region = kind ? project.regions.find((candidate) => candidate.id === kind.regionId) : null;
    issues.push({
      level: 'warning',
      code: 'generator.startKind.fallback',
      message: `起始地块无效，已回退到「${kind?.name || '无'}」。`,
    });
  }
  if (!kind || !region) return null;
  if (!kind.spawnEligible) {
    issues.push({
      level: 'warning',
      code: 'generator.startKind.spawn',
      message: `起始地块「${kind.name}」没有标记为出生点候选。`,
    });
  }

  const maxLength = Math.max(kind.length.min, Math.min(kind.length.max, Math.max(width, height)));
  const lengths = range(kind.length.min, maxLength);
  rng.shuffle(lengths);
  for (const length of lengths) {
    const axes = rng.shuffle(['h', 'v']);
    for (const axis of axes) {
      if ((axis === 'h' && length > width) || (axis === 'v' && length > height)) continue;
      const origin = centeredOrigin(width, height, axis, length);
      const cells = cellsFor(origin, axis, length);
      if (cells.every((cell) => inBounds(cell.x, cell.y, width, height))) {
        return { kind, region, axis, length, origin, cells };
      }
    }
  }
  return null;
}

function makePlot({ id, kind, region, axis, origin, length }) {
  return {
    id,
    kindId: kind.id,
    regionId: region.id,
    axis,
    origin: { x: origin.x, y: origin.y },
    length,
    cells: cellsFor(origin, axis, length),
    contents: clone(kind.contents || {}),
    subRooms: [],
  };
}

function crossRegionBlocked(profile, target, sourceRegionId, targetRegionId, crossLinks, pairCounts) {
  if (sourceRegionId === targetRegionId) return false;
  if (!profile.allowCrossRegion) return true;
  if ((profile.maxCrossRegionPerPair || 0) <= 0) return true;
  const cap = Math.floor(Math.max(0, target) * Math.max(0, profile.crossRegionRatio || 0));
  if (crossLinks >= cap) return true;
  const pairKey = [sourceRegionId, targetRegionId].sort().join('|');
  const used = pairCounts.get(pairKey) || 0;
  return used >= (profile.maxCrossRegionPerPair || 0);
}

function regionMatches(sourceRegionId, targetRegionId, mode) {
  if (mode === 'same') return sourceRegionId === targetRegionId;
  if (mode === 'cross') return sourceRegionId !== targetRegionId;
  return true;
}

function collectDoorCandidates(context, targetKind, targetRegion, mode = 'any') {
  const {
    profile,
    target,
    states,
    width,
    height,
    occupied,
    crossLinks,
    pairCounts,
    rng,
  } = context;
  const candidates = [];
  const ordered = rng.shuffle([...states.values()]);

  for (const state of ordered) {
    if (candidates.length >= CANDIDATE_LIMIT) break;
    if (!state.kind || state.degree >= state.kind.connections.max) continue;
    if (!canConnectKinds(state.kind, targetKind)) continue;
    if (!regionMatches(state.plot.regionId, targetRegion.id, mode)) continue;
    if (crossRegionBlocked(profile, target, state.plot.regionId, targetRegion.id, crossLinks, pairCounts)) continue;

    const ports = rng.shuffle(
      portsForPlot({
        plot: state.plot,
        kind: state.kind,
        width,
        height,
        usedSides: state.usedSides,
        degree: state.degree,
        isEmpty: (x, y) => !occupied.has(cellKey(x, y)),
      }),
    );

    for (const port of ports) {
      if (candidates.length >= CANDIDATE_LIMIT) break;
      const lengths = rng.shuffle(range(targetKind.length.min, targetKind.length.max));
      for (const length of lengths) {
        const alongAxis = DIRECTIONS[port.dir].axis;
        const variants = [];
        const facing = DIRECTIONS[port.dir].opposite;

        // 新地块沿门的方向延伸：门在新地块的端部。
        if (sideIsAllowed(targetKind, alongAxis, length, 0, facing)) {
          variants.push({ axis: alongAxis, index: 0, origin: { x: port.nx, y: port.ny } });
        }

        // 新地块垂直于门的方向：门落在新地块中段（或端部之外的侧面）。
        const perpAxis = otherAxis(alongAxis);
        const step = stepOf(perpAxis);
        for (let index = 0; index < length; index += 1) {
          if (!sideIsAllowed(targetKind, perpAxis, length, index, facing)) continue;
          variants.push({
            axis: perpAxis,
            index,
            origin: { x: port.nx - step.x * index, y: port.ny - step.y * index },
          });
        }

        for (const variant of variants) {
          if (candidates.length >= CANDIDATE_LIMIT) break;
          const cells = cellsFor(variant.origin, variant.axis, length);
          if (!freeCells(cells, width, height, occupied)) continue;
          if (cells[variant.index]?.x !== port.nx || cells[variant.index]?.y !== port.ny) continue;
          candidates.push({
            type: 'door',
            source: state,
            sourceCellIndex: port.cellIndex,
            dir: port.dir,
            targetCellIndex: variant.index,
            axis: variant.axis,
            origin: variant.origin,
            length,
            cells,
          });
        }
      }
    }
  }
  return candidates;
}

function collectCrossCandidates(context, targetKind, targetRegion, mode = 'any') {
  const {
    profile,
    target,
    states,
    width,
    height,
    occupied,
    cellOwners,
    crossLinks,
    pairCounts,
    rng,
  } = context;
  const candidates = [];
  const ordered = rng.shuffle([...states.values()]);

  for (const state of ordered) {
    if (candidates.length >= CANDIDATE_LIMIT) break;
    if (!state.kind || state.degree >= state.kind.connections.max) continue;
    if (!state.kind.connections.allowCrossing) continue;
    if (!targetKind.connections.allowCrossing) continue;
    if (!canConnectKinds(state.kind, targetKind)) continue;
    if (!canCrossKinds(state.kind, targetKind, profile)) continue;
    if (!regionMatches(state.plot.regionId, targetRegion.id, mode)) continue;
    if (crossRegionBlocked(profile, target, state.plot.regionId, targetRegion.id, crossLinks, pairCounts)) continue;

    const axis = otherAxis(state.plot.axis);
    const step = stepOf(axis);
    const indexedCells = rng.shuffle(state.plot.cells.map((cell, index) => ({ cell, index })));

    for (const { cell, index: sourceCellIndex } of indexedCells) {
      if (candidates.length >= CANDIDATE_LIMIT) break;
      if ((cellOwners.get(cellKey(cell.x, cell.y)) || []).length >= 2) continue;
      const lengths = rng.shuffle(range(targetKind.length.min, targetKind.length.max));
      for (const length of lengths) {
        for (let targetCellIndex = 0; targetCellIndex < length; targetCellIndex += 1) {
          if (candidates.length >= CANDIDATE_LIMIT) break;
          const origin = {
            x: cell.x - step.x * targetCellIndex,
            y: cell.y - step.y * targetCellIndex,
          };
          const cells = cellsFor(origin, axis, length);
          if (!freeCells(cells, width, height, occupied, cellKey(cell.x, cell.y))) continue;
          if (cells[targetCellIndex]?.x !== cell.x || cells[targetCellIndex]?.y !== cell.y) continue;
          candidates.push({
            type: 'cross',
            source: state,
            sourceCellIndex,
            targetCellIndex,
            axis,
            origin,
            length,
            cells,
            sharedCell: cell,
          });
        }
      }
    }
  }
  return candidates;
}

function chooseKind(project, regionId, kindCounts, rng) {
  const pool = plotKindsForRegion(project, regionId).filter((kind) => {
    if (kind.generation.max <= 0 || kind.connections.max <= 0) return false;
    const count = kindCounts.get(kind.id) || 0;
    if (count >= kind.generation.max) return false;
    if (kind.generation.unique && count > 0) return false;
    return true;
  });
  return rng.weighted(pool, (kind) => Math.max(0.01, kind.generation.weight));
}

function chooseRegion(regions, quotas, regionCounts, rng) {
  const pool = regions.filter(
    (region) => (regionCounts.get(region.id) || 0) < (quotas.get(region.id) || 0),
  );
  return rng.weighted(pool, (region) => {
    const left = (quotas.get(region.id) || 0) - (regionCounts.get(region.id) || 0);
    return Math.max(0.01, left) * Math.max(0.01, region.generation.weight);
  });
}

function placeCandidate(candidate, { width, height, project, plots, states, occupied, cellOwners, links, counts }) {
  const { source } = candidate;
  const targetKind = candidate.targetKind;
  const targetRegion = candidate.targetRegion;
  const id = `p${plots.length + 1}`;
  const plot = makePlot({
    id,
    kind: targetKind,
    region: targetRegion,
    axis: candidate.axis,
    origin: candidate.origin,
    length: candidate.length,
  });

  if (!plot.cells.every((cell) => inBounds(cell.x, cell.y, width, height))) return null;
  const sharedKey =
    candidate.type === 'cross' ? cellKey(candidate.sharedCell.x, candidate.sharedCell.y) : null;
  for (const cell of plot.cells) {
    const key = cellKey(cell.x, cell.y);
    if (key === sharedKey) continue;
    if (occupied.has(key)) return null;
  }
  if (sharedKey) {
    const owners = cellOwners.get(sharedKey) || [];
    if (owners.length >= 2) return null;
  }

  const usedSides = new Set();
  const degree = 1;
  if (candidate.type === 'door') {
    usedSides.add(DIRECTIONS[candidate.dir].opposite);
    source.usedSides.add(candidate.dir);
    source.degree += 1;
  } else {
    source.degree += 1;
  }

  plots.push(plot);
  states.set(plot.id, { plot, kind: targetKind, usedSides, degree });
  for (const cell of plot.cells) {
    const key = cellKey(cell.x, cell.y);
    occupied.add(key);
    const owners = cellOwners.get(key) || [];
    if (!owners.includes(plot.id)) owners.push(plot.id);
    cellOwners.set(key, owners);
  }

  const link =
    candidate.type === 'door'
      ? {
          id: `l${links.length + 1}`,
          type: 'door',
          a: { plotId: source.plot.id, cellIndex: candidate.sourceCellIndex },
          b: { plotId: plot.id, cellIndex: candidate.targetCellIndex },
          dir: candidate.dir,
        }
      : {
          id: `l${links.length + 1}`,
          type: 'cross',
          a: { plotId: source.plot.id, cellIndex: candidate.sourceCellIndex },
          b: { plotId: plot.id, cellIndex: candidate.targetCellIndex },
          cell: { x: candidate.sharedCell.x, y: candidate.sharedCell.y },
        };
  links.push(link);

  counts.kind.set(targetKind.id, (counts.kind.get(targetKind.id) || 0) + 1);
  counts.region.set(targetRegion.id, (counts.region.get(targetRegion.id) || 0) + 1);

  return plot;
}

function assignSubRooms(project, map, profile, rng, issues) {
  let counter = 0;
  for (const plot of map.plots) {
    const kind = plotKindById(project, plot.kindId);
    if (!kind || !kind.subRooms?.enabled || kind.subRooms.pool.length === 0) continue;
    const pool = kind.subRooms.pool
      .map((id) => subRoomKindById(project, id))
      .filter(Boolean);
    if (pool.length === 0) {
      issues.push({
        level: 'warning',
        code: 'generator.subRooms.empty',
        message: `地块「${kind.name}」的子房间池没有有效种类，已跳过。`,
      });
      continue;
    }

    const density = Math.max(0, profile.subRoomDensity ?? 1);
    if (density <= 0) continue;
    const desired = rng.int(kind.subRooms.min, kind.subRooms.max);
    let count = Math.round(desired * density);
    if (desired > 0 && count === 0) count = 1;
    const spawnIndex = map.spawn?.plotId === plot.id ? map.spawn.cellIndex : -1;
    const available = rng.shuffle(
      plot.cells
        .map((cell, index) => ({ cell, index }))
        .filter((entry) => entry.index !== spawnIndex),
    );
    count = Math.min(count, available.length);
    for (let index = 0; index < count; index += 1) {
      const roomKind = rng.weighted(pool, (candidate) => Math.max(0.01, candidate.generation.weight));
      if (!roomKind) break;
      const entry = available[index];
      counter += 1;
      plot.subRooms.push({
        id: `s${counter}`,
        kindId: roomKind.id,
        cellIndex: entry.index,
        x: entry.cell.x,
        y: entry.cell.y,
        contents: clone(roomKind.contents || {}),
      });
    }
  }
}

function weightedOrder(items, weightFn, rng) {
  const pool = [...items];
  const result = [];
  while (pool.length > 0) {
    const picked = rng.weighted(pool, weightFn);
    if (!picked) break;
    result.push(picked);
    pool.splice(pool.indexOf(picked), 1);
  }
  return result;
}

function eligibleKinds(project, regionId, kindCounts) {
  return plotKindsForRegion(project, regionId).filter((kind) => {
    if (kind.generation.max <= 0 || kind.connections.max <= 0) return false;
    const count = kindCounts.get(kind.id) || 0;
    if (count >= kind.generation.max) return false;
    if (kind.generation.unique && count > 0) return false;
    return true;
  });
}

function generateOnce(project, profile, seed, attempt, options) {
  const width = project.settings.grid.width;
  const height = project.settings.grid.height;
  const rng = new RNG(seed);
  const issues = [];
  const target = Math.max(1, Math.round(profile.plotTarget));
  const regions = activeRegions(project, profile, profile.startRegionId);
  if (regions.length === 0) {
    throw new Error('没有可用区域，无法生成地图。');
  }

  const start = pickStart(project, profile, regions, target, width, height, rng, issues);
  if (!start) {
    throw new Error('起始地块放不进当前网格，请增大地图网格或缩短起始地块。');
  }

  const quotas = planQuotas(project, profile, regions, start.region.id, target, rng);
  const regionCounts = new Map();
  const kindCounts = new Map();
  const plots = [];
  const states = new Map();
  const links = [];
  const occupied = new Set();
  const cellOwners = new Map();
  let crossLinks = 0;
  const pairCounts = new Map();

  const startPlot = makePlot({
    id: 'p1',
    kind: start.kind,
    region: start.region,
    axis: start.axis,
    origin: start.origin,
    length: start.length,
  });
  plots.push(startPlot);
  states.set(startPlot.id, {
    plot: startPlot,
    kind: start.kind,
    usedSides: new Set(),
    degree: 0,
  });
  for (const cell of startPlot.cells) {
    const key = cellKey(cell.x, cell.y);
    occupied.add(key);
    cellOwners.set(key, [startPlot.id]);
  }
  kindCounts.set(start.kind.id, 1);
  regionCounts.set(start.region.id, 1);

  const context = {
    profile,
    target,
    project,
    states,
    width,
    height,
    occupied,
    cellOwners,
    crossLinks,
    pairCounts,
    rng,
  };

  let attempts = 0;
  let failureStreak = 0;
  const maxAttempts = Math.min(5000, Math.max(120, target * 35));
  const enteredRegions = new Set([start.region.id]);

  const tryPlace = (candidate, region, targetKind) => {
    candidate.targetKind = targetKind;
    candidate.targetRegion = region;
    const before = plots.length;
    const placedPlot = placeCandidate(candidate, {
      width,
      height,
      project,
      plots,
      states,
      occupied,
      cellOwners,
      links,
      counts: { kind: kindCounts, region: regionCounts },
    });
    if (!placedPlot || plots.length === before) return false;

    if (candidate.source.plot.regionId !== region.id) {
      crossLinks += 1;
      const pairKey = [candidate.source.plot.regionId, region.id].sort().join('|');
      pairCounts.set(pairKey, (pairCounts.get(pairKey) || 0) + 1);
    }
    return true;
  };

  while (plots.length < target && attempts < maxAttempts) {
    attempts += 1;
    // 每次放置时把最新的跨区计数写回 context。
    context.crossLinks = crossLinks;
    context.pairCounts = pairCounts;

    let placed = false;

    // 1) 先让尚未进入的区域通过一条跨区连接落地。否则跨区上限容易被
    //    已进入区域之间的环路花光，后面的区域永远进不来。
    if (profile.allowCrossRegion) {
      const pending = regions.filter(
        (region) =>
          !enteredRegions.has(region.id) &&
          (regionCounts.get(region.id) || 0) < (quotas.get(region.id) || 0),
      );
      const ordered = weightedOrder(
        pending,
        (region) => {
          const left = (quotas.get(region.id) || 0) - (regionCounts.get(region.id) || 0);
          return Math.max(0.01, left) * Math.max(0.01, region.generation.weight);
        },
        rng,
      );
      for (const region of ordered) {
        const kindPool = eligibleKinds(project, region.id, kindCounts);
        for (const targetKind of rng.shuffle(kindPool)) {
          const candidates = [
            ...collectDoorCandidates(context, targetKind, region, 'cross'),
            ...collectCrossCandidates(context, targetKind, region, 'cross'),
          ];
          if (candidates.length === 0) continue;
          const candidate = rng.pick(candidates);
          if (!candidate) continue;
          if (tryPlace(candidate, region, targetKind)) {
            enteredRegions.add(region.id);
            placed = true;
            break;
          }
        }
        if (placed) break;
      }
    }

    // 2) 常规生长：同区域优先；同区域没有位置时再用剩余跨区额度补充。
    if (!placed) {
      const pending = regions.filter(
        (region) => (regionCounts.get(region.id) || 0) < (quotas.get(region.id) || 0),
      );
      const ordered = weightedOrder(
        pending,
        (region) => {
          const left = (quotas.get(region.id) || 0) - (regionCounts.get(region.id) || 0);
          return Math.max(0.01, left) * Math.max(0.01, region.generation.weight);
        },
        rng,
      );

      for (const region of ordered) {
        const kindPool = eligibleKinds(project, region.id, kindCounts);
        for (const targetKind of rng.shuffle(kindPool)) {
          const preferCross =
            profile.allowCrossing &&
            targetKind.connections.allowCrossing &&
            rng.chance(0.35);
          let candidates = preferCross
            ? collectCrossCandidates(context, targetKind, region, 'same')
            : collectDoorCandidates(context, targetKind, region, 'same');
          if (candidates.length === 0) {
            candidates = preferCross
              ? collectDoorCandidates(context, targetKind, region, 'same')
              : collectCrossCandidates(context, targetKind, region, 'same');
          }
          if (candidates.length === 0 && profile.allowCrossRegion) {
            candidates = [
              ...collectDoorCandidates(context, targetKind, region, 'cross'),
              ...collectCrossCandidates(context, targetKind, region, 'cross'),
            ];
          }
          if (candidates.length === 0) continue;
          const candidate = rng.pick(candidates);
          if (!candidate) continue;
          if (tryPlace(candidate, region, targetKind)) {
            placed = true;
            break;
          }
        }
        if (placed) break;
      }
    }

    if (!placed) {
      failureStreak += 1;
      if (failureStreak > regions.length * 4) break;
    } else {
      failureStreak = 0;
    }
  }

  const startCell = startPlot.cells[0];
  const map = {
    id: '',
    seed: '',
    profileId: profile.id,
    generator: GENERATOR_ID,
    generatorVersion: GENERATOR_VERSION,
    createdAt: options.createdAt || new Date().toISOString(),
    settings: { grid: { width, height } },
    plots,
    links,
    spawn: {
      plotId: startPlot.id,
      cellIndex: 0,
      x: startCell.x,
      y: startCell.y,
    },
    stats: null,
    issues: [],
    warnings: [],
    errors: [],
    generation: {
      attempt,
      target,
      actual: plots.length,
      attempts,
      quotas: Object.fromEntries(quotas),
      counts: Object.fromEntries(regionCounts),
      crossLinks,
    },
  };

  assignSubRooms(project, map, profile, rng, issues);
  map.stats = computeMapStats(map);

  const validation = validateMap(project, map, { profile });
  const merged = [...issues, ...validation.issues];
  const seen = new Set();
  map.issues = merged.filter((entry) => {
    const key = `${entry.level}:${entry.code}:${entry.message}`;
    if (seen.has(key)) return false;
    seen.add(key);
    return true;
  });
  map.warnings = map.issues.filter((entry) => entry.level === 'warning');
  map.errors = map.issues.filter((entry) => entry.level === 'error');
  map.stats = validation.stats;

  if (plots.length < target) {
    map.warnings.push({
      level: 'warning',
      code: 'generator.target',
      message: `目标 ${target} 块，实际生成 ${plots.length} 块：可用空间、区域配额或连接规则提前耗尽。`,
    });
  }
  for (const region of regions) {
    const quota = quotas.get(region.id) || 0;
    const actual = regionCounts.get(region.id) || 0;
    if (actual < quota) {
      map.warnings.push({
        level: 'warning',
        code: 'generator.regionQuota',
        message: `区域「${region.name}」计划 ${quota} 块，实际 ${actual} 块。`,
      });
    }
  }

  return map;
}

export function generateMap(project, options = {}) {
  const profile = options.profile || profileById(project, options.profileId) || project.profiles[0];
  if (!profile) throw new Error('工程没有可用的生成方案。');
  const seed = normalizeSeed(options.seed || randomSeed());
  const attempts = options.attempts || (profile.plotTarget > 60 ? 3 : 6);
  let best = null;

  for (let attempt = 0; attempt < attempts; attempt += 1) {
    const attemptSeed = attempt === 0 ? seed : `${seed}#${attempt + 1}`;
    const map = generateOnce(project, profile, attemptSeed, attempt, {
      createdAt: options.createdAt,
    });
    map.seed = seed;
    map.id = `map_${seed.replace(/[^A-Za-z0-9_-]+/g, '_').slice(0, 64)}`;
    map.attempt = attempt;

    if (
      !best ||
      map.plots.length > best.plots.length ||
      (map.plots.length === best.plots.length && map.errors.length < best.errors.length)
    ) {
      best = map;
    }
    if (map.plots.length >= profile.plotTarget && map.errors.length === 0) break;
  }

  if (!best) throw new Error('地图生成失败。');
  best.stats = computeMapStats(best);
  return best;
}

export function previewQuotas(project, profile, seed = 'preview') {
  const rng = new RNG(normalizeSeed(seed));
  const regions = activeRegions(project, profile, profile.startRegionId);
  const quotas = planQuotas(project, profile, regions, profile.startRegionId, profile.plotTarget, rng);
  return regions.map((region) => ({
    regionId: region.id,
    name: region.name,
    quota: quotas.get(region.id) || 0,
    capacity: regionCapacity(project, region.id),
    min: region.generation.min,
    max: region.generation.max,
  }));
}

registry.registerGenerator(
  GENERATOR_ID,
  {
    label: '标准漫游',
    version: GENERATOR_VERSION,
    description: '按区域配额、接口与连接上限生长的默认生成器。',
  },
  (project, options = {}) => generateMap(project, options),
);

export { GENERATOR_ID, GENERATOR_VERSION };
