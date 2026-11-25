/**
 * 单层三角类银河城地牢生成器（v2.1）。
 *
 * 四阶段流水线：
 *   1. 三区域（选自 城市/工厂/郊区/太空）各自在局部坐标系独立成环生长：
 *      - 支持无矩形单链条带（直线/折线，明确首尾）、4 向旋转；
 *      - 支持「结构松散度 looseness (0~1)」控制连接倾向中段还是头尾；
 *      - 支持「非环 (noLoop)」端点支路属性及每区「最多非环数 maxNoLoop」限制；
 *      - 支持格子级「出入口 (isPortal)」约束（有出入口格时门与交叉皆仅落于出入口格）；
 *      - 内建预算引导采样与 >=3 节点环路保障；
 *   2. 向量差哈希 O(1) 刚体三角拼装 + 3 条独立跨区桥接条带（跨区仅用门，
 *      桥不挂载非环条带，不同区域严格隔离无直接相邻与重叠）；
 *   3. 双通道预算收敛（同形状模板互换 + 末端叶条带换形），严守 noLoop / isPortal / maxNoLoop；
 *   4. 条带节点图 BFS 距离计算 -> 条带等级 -> 战斗/精英格最终敌人等级（含 difficultyOffset）。
 *
 * 全程使用 RNG(seed) 确定性驱动，不依赖 DOM。
 */

import { exactOpenEdgeKey, needsStrictCross, rotateOpenDirs, straightAxis, stripNeighbors, validateOpenDirs, hasConnectedOpenEdges, isOpenChain } from './strip-edges.js';
import {
  DEFAULT_CELL_TYPES,
  DIRECTIONS,
  DIR_IDS,
  DUNGEON_REGION_PRESETS,
  FLOOR_GENERATOR_ID,
  FLOOR_GENERATOR_VERSION,
  clamp,
  isHexColor,
} from './constants.js';
import {
  adjacentCellPairs,
  boundsOfCells,
  centroidOfCells,
  computeChainOrderMap,
  coordKey,
  exactShapeKey,
  exteriorDoorPorts,
  placeCellsAt,
  rotatePointCW,
  uniqueRotations,
} from './polyomino.js';
import {
  cellTypeMap,
  computeTemplateMetrics,
  createDefaultStripTemplates,
  normalizeCellTypes,
  normalizeStripTemplate,
  templateMatchesRegion,
} from './strip-templates.js';
import { validateFloorMap } from './floor-validator.js';
import { RNG, normalizeSeed, randomSeed } from './rng.js';

const clone = (v) => JSON.parse(JSON.stringify(v));
const isObject = (v) => v !== null && typeof v === 'object' && !Array.isArray(v);

const CW_DIR = { N: 'E', E: 'S', S: 'W', W: 'N' };
function rotateDirCW(dirId, times) {
  let d = dirId;
  const r = ((times % 4) + 4) % 4;
  for (let i = 0; i < r; i += 1) d = CW_DIR[d] || d;
  return d;
}

function asOptionalBound(v) {
  if (v === null || v === undefined || v === '') return null;
  const n = Number(v);
  return Number.isFinite(n) ? Math.max(0, Math.round(n * 100) / 100) : null;
}

function stripHasPortals(cells) {
  return Array.isArray(cells) && cells.some((c) => Boolean(c.isPortal));
}

function cellAllowsConnection(cells, cellIndex) {
  if (!stripHasPortals(cells)) return true;
  return Boolean(cells?.[cellIndex]?.isPortal);
}

/* ------------------------------------------------------------------ *
 * 单层方案规范化与默认工厂（四种区域类型：城市/工厂/郊区/太空）
 * ------------------------------------------------------------------ */

const PRESET_BY_ID = new Map(DUNGEON_REGION_PRESETS.map((p) => [p.id, p]));
const LEGACY_REGION_MAP = {
  region_a: 'city',
  region_b: 'factory',
  region_c: 'suburb',
};

export function createDefaultFloorProfile() {
  return normalizeFloorProfile({
    id: 'floor_standard',
    name: '标准三角地牢层',
    description: '从城市/工厂/郊区/太空中选三个区域呈三角形排布，每区至少一环，区域间由唯一独立桥接条带以门相连。',
    regions: [
      { id: 'city', name: '城市', shortName: '城市', color: '#d9a441', role: 'start', stripMin: 5, stripMax: 6, maxNoLoop: 1 },
      { id: 'factory', name: '工厂', shortName: '工厂', color: '#d97b6c', role: 'wing', stripMin: 5, stripMax: 6, maxNoLoop: 1 },
      { id: 'suburb', name: '郊区', shortName: '郊区', color: '#7fb685', role: 'wing', stripMin: 5, stripMax: 6, maxNoLoop: 1 },
    ],
    topology: {
      minLoopsPerRegion: 1,
      extraLoopChance: 0.35,
      allowCrossing: true,
      crossChance: 0.3,
      looseness: 0.55,
    },
    budget: {
      resourceMin: null,
      resourceMax: null,
      difficultyMin: null,
      difficultyMax: null,
    },
    progression: {
      baseLevel: 1,
      levelPerHop: 0.75,
      maxLevel: 20,
    },
  });
}

export function normalizeFloorProfile(raw) {
  const source = isObject(raw) ? raw : {};
  const defaultIds = ['city', 'factory', 'suburb'];
  const rawRegions = Array.isArray(source.regions) ? source.regions : [];
  const usedIds = new Set();

  const regions = [0, 1, 2].map((index) => {
    const r = isObject(rawRegions[index]) ? rawRegions[index] : {};
    let candidateId = String(r.id || defaultIds[index]).trim();
    if (LEGACY_REGION_MAP[candidateId]) candidateId = LEGACY_REGION_MAP[candidateId];
    if (!PRESET_BY_ID.has(candidateId) || usedIds.has(candidateId)) {
      candidateId = DUNGEON_REGION_PRESETS.find((p) => !usedIds.has(p.id))?.id || defaultIds[index];
    }
    usedIds.add(candidateId);
    const preset = PRESET_BY_ID.get(candidateId) || DUNGEON_REGION_PRESETS[index];

    const stripMin = clamp(Math.round(Number(r.stripMin ?? 5) || 3), 3, 18);
    const stripMax = clamp(Math.round(Number(r.stripMax ?? Math.max(stripMin, 6)) || stripMin), stripMin, 20);
    const maxNoLoop = clamp(Math.round(Number(r.maxNoLoop ?? 1)), 0, 10);
    // 如果是旧版 region_a 迁移而来，同步改用新预设名和色
    const migratedLegacy = Boolean(LEGACY_REGION_MAP[String(r.id || '')]);
    const name = migratedLegacy ? preset.name : (String(r.name || preset.name).trim() || preset.name);
    const shortName = migratedLegacy ? preset.shortName : (String(r.shortName || name).trim() || preset.shortName);
    const color = !migratedLegacy && isHexColor(r.color) ? r.color.toLowerCase() : preset.color;

    return {
      id: candidateId,
      name,
      shortName,
      color,
      role: index === 0 ? 'start' : 'wing',
      stripMin,
      stripMax,
      maxNoLoop: Number.isFinite(maxNoLoop) ? maxNoLoop : 1,
    };
  });

  const topo = isObject(source.topology) ? source.topology : {};
  const budget = isObject(source.budget) ? source.budget : {};
  const prog = isObject(source.progression) ? source.progression : {};

  let resourceMin = asOptionalBound(budget.resourceMin);
  let resourceMax = asOptionalBound(budget.resourceMax);
  if (resourceMin !== null && resourceMax !== null && resourceMin > resourceMax) {
    [resourceMin, resourceMax] = [resourceMax, resourceMin];
  }
  let difficultyMin = asOptionalBound(budget.difficultyMin);
  let difficultyMax = asOptionalBound(budget.difficultyMax);
  if (difficultyMin !== null && difficultyMax !== null && difficultyMin > difficultyMax) {
    [difficultyMin, difficultyMax] = [difficultyMax, difficultyMin];
  }

  return {
    id: String(source.id || 'floor_standard').trim() || 'floor_standard',
    name: String(source.name || '单层三角方案').trim() || '单层三角方案',
    description: String(source.description || ''),
    regions,
    topology: {
      minLoopsPerRegion: clamp(Math.round(Number(topo.minLoopsPerRegion ?? 1) || 1), 1, 4),
      extraLoopChance: clamp(Number(topo.extraLoopChance ?? 0.35), 0, 1),
      allowCrossing: topo.allowCrossing !== false,
      crossChance: clamp(Number(topo.crossChance ?? 0.3), 0, 1),
      looseness: clamp(Number(topo.looseness ?? 0.55), 0, 1),
    },
    budget: {
      resourceMin,
      resourceMax,
      difficultyMin,
      difficultyMax,
    },
    progression: {
      baseLevel: clamp(Math.round(Number(prog.baseLevel ?? 1) || 1), 1, 99),
      levelPerHop: clamp(Number(prog.levelPerHop ?? 0.75), 0, 10),
      maxLevel: clamp(Math.round(Number(prog.maxLevel ?? 20) || 20), 1, 999),
    },
  };
}

/* ------------------------------------------------------------------ *
 * 自动预算区间估算
 * ------------------------------------------------------------------ */

function weightedStats(items, valueFn) {
  if (!items.length) return { mean: 20, min: 0, max: 40 };
  let wSum = 0;
  let vSum = 0;
  let min = Infinity;
  let max = -Infinity;
  for (const item of items) {
    const w = Math.max(0.05, Number(item.template.generation?.weight) || 1);
    const v = valueFn(item);
    wSum += w;
    vSum += w * v;
    if (v < min) min = v;
    if (v > max) max = v;
  }
  return {
    mean: wSum > 0 ? vSum / wSum : 0,
    min: Number.isFinite(min) ? min : 0,
    max: Number.isFinite(max) ? max : 0,
  };
}

export function estimateFloorBudget(rawProfile, rawTemplates, rawCellTypes = DEFAULT_CELL_TYPES) {
  const profile = normalizeFloorProfile(rawProfile);
  const cellTypes = normalizeCellTypes(rawCellTypes);
  const templates = (Array.isArray(rawTemplates) && rawTemplates.length
    ? rawTemplates
    : createDefaultStripTemplates(cellTypes)
  ).map((t, i) => {
    const normalized = normalizeStripTemplate(t, cellTypes, i);
    const metrics = computeTemplateMetrics(normalized, cellTypes);
    return { template: normalized, metrics };
  });

  const normalPool = templates.filter((x) => x.template.role !== 'bridge' && x.template.generation.max > 0);
  const bridgePool = templates.filter((x) => x.template.role === 'bridge' && x.template.generation.max > 0);
  const effectiveNormal = normalPool.length ? normalPool : templates;
  const effectiveBridge = bridgePool.length ? bridgePool : effectiveNormal;

  const expectedRegionStrips = profile.regions.reduce(
    (sum, r) => sum + (r.stripMin + r.stripMax) / 2,
    0,
  );
  const nStatsR = weightedStats(effectiveNormal, (x) => x.metrics.resource);
  const nStatsD = weightedStats(effectiveNormal, (x) => x.metrics.difficulty);
  const bStatsR = weightedStats(effectiveBridge, (x) => x.metrics.resource);
  const bStatsD = weightedStats(effectiveBridge, (x) => x.metrics.difficulty);

  const expectedR = expectedRegionStrips * nStatsR.mean + 3 * bStatsR.mean;
  const expectedD = expectedRegionStrips * nStatsD.mean + 3 * bStatsD.mean;
  const padR = Math.max(25, expectedR * 0.16, nStatsR.mean * 0.8);
  const padD = Math.max(25, expectedD * 0.16, nStatsD.mean * 0.8);

  const autoResourceMin = Math.max(0, Math.floor(expectedR - padR));
  const autoResourceMax = Math.max(autoResourceMin + 10, Math.ceil(expectedR + padR));
  const autoDifficultyMin = Math.max(0, Math.floor(expectedD - padD));
  const autoDifficultyMax = Math.max(autoDifficultyMin + 10, Math.ceil(expectedD + padD));

  const manualRMin = profile.budget.resourceMin;
  const manualRMax = profile.budget.resourceMax;
  const manualDMin = profile.budget.difficultyMin;
  const manualDMax = profile.budget.difficultyMax;

  let resourceMin = manualRMin !== null ? manualRMin : autoResourceMin;
  let resourceMax = manualRMax !== null ? manualRMax : autoResourceMax;
  if (manualRMin !== null && manualRMax === null) {
    resourceMax = Math.max(resourceMin + Math.ceil(padR * 2), autoResourceMax);
  } else if (manualRMin === null && manualRMax !== null) {
    resourceMin = Math.min(Math.max(0, resourceMax - Math.ceil(padR * 2)), autoResourceMin);
  }

  let difficultyMin = manualDMin !== null ? manualDMin : autoDifficultyMin;
  let difficultyMax = manualDMax !== null ? manualDMax : autoDifficultyMax;
  if (manualDMin !== null && manualDMax === null) {
    difficultyMax = Math.max(difficultyMin + Math.ceil(padD * 2), autoDifficultyMax);
  } else if (manualDMin === null && manualDMax !== null) {
    difficultyMin = Math.min(Math.max(0, difficultyMax - Math.ceil(padD * 2)), autoDifficultyMin);
  }

  return {
    resourceMin,
    resourceMax,
    difficultyMin,
    difficultyMax,
    autoResourceMin,
    autoResourceMax,
    autoDifficultyMin,
    autoDifficultyMax,
    autoResource: manualRMin === null && manualRMax === null,
    autoDifficulty: manualDMin === null && manualDMax === null,
    expectedResource: Math.round(expectedR),
    expectedDifficulty: Math.round(expectedD),
  };
}

function rangeDist(value, min, max) {
  if (value < min) return min - value;
  if (value > max) return value - max;
  return 0;
}

/* ------------------------------------------------------------------ *
 * 模板预处理缓存（含链序端点系数 endFactors 与出入口标记）
 * ------------------------------------------------------------------ */

function prepareTemplates(rawTemplates, cellTypes) {
  const list = (Array.isArray(rawTemplates) && rawTemplates.length
    ? rawTemplates
    : createDefaultStripTemplates(cellTypes)
  ).map((raw, i) => normalizeStripTemplate(raw, cellTypes, i));

  return list.filter(({ cells, role }) => validateOpenDirs(cells) && hasConnectedOpenEdges(cells)
    && (role !== 'bridge' || isOpenChain(cells))).map((template) => {
    const metrics = computeTemplateMetrics(template, cellTypes);
    const rotations = uniqueRotations(template.cells, template.allowRotate, {
      preserveContentOrientation: false,
    }).map((variant) => {
      const orderMap = computeChainOrderMap(variant.cells);
      const neighbors = stripNeighbors(variant.cells);
      const strictCross = needsStrictCross(variant.cells);
      const axes = variant.cells.map((_, i) => straightAxis(variant.cells, i, neighbors));
      const endFactors = variant.cells.map((_, idx) => orderMap.get(idx)?.endFactor ?? 0.5);
      const hasPortals = variant.cells.some((c) => Boolean(c.isPortal));
      let headIdx = 0;
      let tailIdx = Math.max(0, variant.cells.length - 1);
      for (const [idx, info] of orderMap) {
        if (info.isHead) headIdx = idx;
        if (info.isTail) tailIdx = idx;
      }
      return {
        ...variant,
        endFactors,
        strictCross,
        axes,
        hasPortals,
        headIdx,
        tailIdx,
      };
    });
    return {
      template,
      metrics,
      rotations,
      signature: metrics.shapeSignature,
    };
  });
}

function eligiblePoolForRegion(
  prepared,
  regionId,
  role,
  usedCounts,
  { relaxMax = false, spawnOnly = false, allowNoLoop = true } = {},
) {
  return prepared.filter((entry) => {
    if (entry.template.role !== role) return false;
    if (role === 'normal' && !templateMatchesRegion(entry.template, regionId)) return false;
    if (spawnOnly && !entry.template.spawnEligible) return false;
    if (!allowNoLoop && entry.template.noLoop) return false;
    if (!relaxMax) {
      const cap = entry.template.generation.unique
        ? Math.min(1, entry.template.generation.max)
        : entry.template.generation.max;
      if (cap <= 0) return false;
      if ((usedCounts.get(entry.template.id) || 0) >= cap) return false;
    }
    return true;
  });
}

function buildStripInstance({ id, entry, variant, origin, regionId, role, bridgeBetween = null }) {
  const cells = placeCellsAt(variant.cells, origin.x, origin.y).map((cell, idx) => ({
    index: cell.index,
    rx: cell.rx,
    ry: cell.ry,
    x: cell.x,
    y: cell.y,
    type: cell.type,
    difficultyOffset: cell.difficultyOffset || 0,
    isPortal: Boolean(cell.isPortal),
    openDirs: cell.openDirs,
    endFactor: variant.endFactors?.[idx] ?? 0.5,
    enemyLevel: null,
  }));
  return {
    id,
    templateId: entry.template.id,
    templateName: entry.template.name,
    role,
    noLoop: Boolean(entry.template.noLoop) && role === 'normal',
    regionId,
    bridgeBetween,
    origin: { x: origin.x, y: origin.y },
    rotation: variant.rotation,
    shapeSignature: entry.signature,
    resource: entry.metrics.resource,
    difficulty: entry.metrics.difficulty,
    distanceFromSpawn: 0,
    level: 1,
    cells,
  };
}

/* ------------------------------------------------------------------ *
 * 阶段 1：区域内局部成环生长（松散度 + 非环 + 出入口格 + 单格交叉）
 * ------------------------------------------------------------------ */

const SPECIAL_TYPES = new Set(['shop', 'elite', 'reward', 'rest', 'spawn']);

function crossCellPenalty(typeA, typeB) {
  if (typeA === 'empty' && typeB === 'empty') return 0;
  if (typeA === 'empty' || typeB === 'empty') return 3;
  if (SPECIAL_TYPES.has(typeA) && SPECIAL_TYPES.has(typeB)) return 36;
  if (SPECIAL_TYPES.has(typeA) || SPECIAL_TYPES.has(typeB)) return 22;
  return 12;
}

function generateLocalRegion({
  region,
  targetCount,
  isStartRegion,
  prepared,
  topology,
  budgetHint,
  globalCounts,
  rng,
  idPrefix,
}) {
  const localCounts = new Map(globalCounts);
  const strips = [];
  const stripById = new Map();
  const links = [];
  const adj = new Map();
  const cellOwners = new Map(); // coordKey -> [{ stripId, cellIndex, type, endFactor, isPortal }]
  const looseness = clamp(Number(topology.looseness ?? 0.55), 0, 1);
  const maxNoLoop = Math.max(0, Number(region.maxNoLoop ?? 0));
  let noLoopPlaced = 0;

  const crossInfo = new Map();
  const addStripState = (strip) => {
    const neighbors = stripNeighbors(strip.cells);
    crossInfo.set(strip.id, { strict: needsStrictCross(strip.cells), axes: strip.cells.map((_, i) => straightAxis(strip.cells, i, neighbors)) });
    strips.push(strip);
    stripById.set(strip.id, strip);
    adj.set(strip.id, new Set());
    if (strip.noLoop) noLoopPlaced += 1;
    for (const cell of strip.cells) {
      const k = coordKey(cell.x, cell.y);
      const list = cellOwners.get(k) || [];
      list.push({
        stripId: strip.id,
        cellIndex: cell.index,
        type: cell.type,
        endFactor: cell.endFactor ?? 0.5,
        isPortal: Boolean(cell.isPortal),
      });
      cellOwners.set(k, list);
    }
    localCounts.set(strip.templateId, (localCounts.get(strip.templateId) || 0) + 1);
  };

  const addLinkState = (link) => {
    const a = link.a.stripId;
    const b = link.b.stripId;
    if (a === b || adj.get(a)?.has(b)) return false;
    links.push(link);
    adj.get(a).add(b);
    adj.get(b).add(a);
    return true;
  };

  // 1. 放置区域起始条带（不选非环条带作为第一条）
  let startPool = isStartRegion
    ? eligiblePoolForRegion(prepared, region.id, 'normal', localCounts, { spawnOnly: true, allowNoLoop: false })
    : [];
  if (!startPool.length) {
    startPool = eligiblePoolForRegion(prepared, region.id, 'normal', localCounts, { allowNoLoop: false });
  }
  if (!startPool.length) {
    startPool = eligiblePoolForRegion(prepared, region.id, 'normal', localCounts, { relaxMax: true, allowNoLoop: false });
  }
  if (!startPool.length) {
    startPool = prepared.filter((e) => !e.template.noLoop);
  }
  if (!startPool.length) startPool = prepared;

  const startEntry = rng.weighted(startPool, (e) => e.template.generation.weight) || startPool[0];
  const startVariant = rng.pick(startEntry.rotations) || startEntry.rotations[0];
  const firstStrip = buildStripInstance({
    id: `${idPrefix}_s1`,
    entry: startEntry,
    variant: startVariant,
    origin: { x: 0, y: 0 },
    regionId: region.id,
    role: isStartRegion ? 'spawn' : 'normal',
  });
  addStripState(firstStrip);

  let curR = firstStrip.resource;
  let curD = firstStrip.difficulty;

  // 2. 逐步生长其余条带
  for (let step = 1; step < targetCount; step += 1) {
    const cycleCount = links.length - strips.length + 1;
    const needCycleNow =
      cycleCount < topology.minLoopsPerRegion &&
      (strips.length >= targetCount - 2 || strips.length >= 2);
    // 只有成环后且未达该区 maxNoLoop 上限时，才允许抽取非环模板
    const allowNoLoopNow = cycleCount >= topology.minLoopsPerRegion && noLoopPlaced < maxNoLoop;

    let pool = eligiblePoolForRegion(prepared, region.id, 'normal', localCounts, {
      allowNoLoop: allowNoLoopNow,
    });
    if (!pool.length) {
      pool = eligiblePoolForRegion(prepared, region.id, 'normal', localCounts, {
        relaxMax: true,
        allowNoLoop: allowNoLoopNow,
      });
    }
    if (!pool.length) {
      pool = prepared.filter((e) => allowNoLoopNow || !e.template.noLoop);
    }
    if (!pool.length) pool = prepared;

    // 预算引导采样排序
    const rem = Math.max(1, targetCount - strips.length);
    const wantR = (budgetHint.targetR - curR) / rem;
    const wantD = (budgetHint.targetD - curD) / rem;

    const scoredPool = pool.map((entry) => {
      const dist =
        Math.abs(entry.metrics.resource - wantR) / Math.max(10, Math.abs(wantR) + 10) +
        Math.abs(entry.metrics.difficulty - wantD) / Math.max(10, Math.abs(wantD) + 10);
      const budgetFactor = 1 / (0.35 + dist);
      // 若允许非环且本区还没放非环条带，适度提升非环模板抽样机会
      const noLoopBoost = allowNoLoopNow && entry.template.noLoop && noLoopPlaced === 0 ? 1.45 : 1;
      return {
        entry,
        weight: Math.max(0.05, entry.template.generation.weight * budgetFactor * noLoopBoost),
      };
    });

    const sampleEntries = [];
    const poolCopy = [...scoredPool];
    while (sampleEntries.length < Math.min(7, scoredPool.length) && poolCopy.length > 0) {
      const pick = rng.weighted(poolCopy, (item) => item.weight);
      if (!pick) break;
      sampleEntries.push(pick.entry);
      poolCopy.splice(poolCopy.indexOf(pick), 1);
    }

    const allExistingCells = strips.flatMap((s) => s.cells);
    const regionCenter = centroidOfCells(allExistingCells);
    const preferCross = topology.allowCrossing && rng.chance(topology.crossChance);
    // 根据松散度动态调节向心拉拢（紧凑强聚集，松散鼓励舒展外扩）
    const centerPull = (1 - looseness) * 3.6 - looseness * 0.85;

    const candidates = [];
    const seenPlacements = new Set();

    const evaluatePlacement = (entry, variant, ox, oy) => {
      const pKey = `${entry.template.id}:${variant.rotation}:${ox}:${oy}`;
      if (seenPlacements.has(pKey)) return;
      seenPlacements.add(pKey);

      const isNoLoopTemplate = Boolean(entry.template.noLoop);
      const placedCells = placeCellsAt(variant.cells, ox, oy);
      const overlapByStrip = new Map();

      for (const cell of placedCells) {
        const k = coordKey(cell.x, cell.y);
        const owners = cellOwners.get(k);
        if (!owners || owners.length === 0) continue;
        if (!topology.allowCrossing) return;
        if (owners.length >= 2) return;
        const owner = owners[0];
        const ownerStrip = stripById.get(owner.stripId);
        // 已有非环条带不能再增加连接；交叉也必须满足双方出入口格限制（选项 A）
        if (!ownerStrip || ownerStrip.noLoop) return;
        if (!cellAllowsConnection(ownerStrip.cells, owner.cellIndex)) return;
        if (!cellAllowsConnection(placedCells, cell.index)) return;
        // 方案 1 硬约束：禁止两个非空格交叉（至少一方必须是通道/空）
        if (cell.type !== 'empty' && owner.type !== 'empty') return;
        const info = crossInfo.get(owner.stripId);
        if (variant.strictCross || info?.strict) {
          const axis = variant.axes[cell.index], otherAxis = info?.axes[owner.cellIndex];
          if (!axis || !otherAxis || axis === otherAxis) return;
        }

        const list = overlapByStrip.get(owner.stripId) || [];
        list.push({
          newIdx: cell.index,
          oldIdx: owner.cellIndex,
          x: cell.x,
          y: cell.y,
          newType: cell.type,
          oldType: owner.type,
          newEnd: variant.endFactors[cell.index] ?? 0.5,
          oldEnd: owner.endFactor ?? 0.5,
        });
        if (list.length > 1) return;
        overlapByStrip.set(owner.stripId, list);
      }

      // 非环条带绝不允许同时交叉 >=2 条已有条带
      if (isNoLoopTemplate && overlapByStrip.size > 1) return;

      // 收集可开门的相邻已有条带（过滤掉非环宿主与非出入口格）
      const doorTargets = new Map();
      for (const cell of placedCells) {
        if (!cellAllowsConnection(placedCells, cell.index)) continue;
        for (const dirId of DIR_IDS) {
          const d = DIRECTIONS[dirId];
          const nk = coordKey(cell.x + d.dx, cell.y + d.dy);
          const owners = cellOwners.get(nk);
          if (!owners) continue;
          for (const owner of owners) {
            if (overlapByStrip.has(owner.stripId)) continue;
            const ownerStrip = stripById.get(owner.stripId);
            if (!ownerStrip || ownerStrip.noLoop) continue;
            if (!cellAllowsConnection(ownerStrip.cells, owner.cellIndex)) continue;
            const list = doorTargets.get(owner.stripId) || [];
            list.push({
              newIdx: cell.index,
              oldIdx: owner.cellIndex,
              dirFromOld: DIRECTIONS[dirId].opposite,
              newEnd: variant.endFactors[cell.index] ?? 0.5,
              oldEnd: owner.endFactor ?? 0.5,
            });
            doorTargets.set(owner.stripId, list);
          }
        }
      }

      const crossIds = [...overlapByStrip.keys()];
      const doorIds = [...doorTargets.keys()];
      if (crossIds.length === 0 && doorIds.length === 0) return;

      // 按松散度偏好在同对条带的多个可选门中择优
      const pickBestDoor = (list) => {
        const sorted = [...list].sort((a, b) => {
          const da = Math.abs((a.newEnd + a.oldEnd) / 2 - looseness);
          const db = Math.abs((b.newEnd + b.oldEnd) / 2 - looseness);
          return da - db;
        });
        return sorted[0];
      };

      const chosenLinks = [];
      let penalty = 0;
      for (const sid of crossIds) {
        const hit = overlapByStrip.get(sid)[0];
        chosenLinks.push({
          type: 'cross',
          targetStripId: sid,
          oldIdx: hit.oldIdx,
          newIdx: hit.newIdx,
          x: hit.x,
          y: hit.y,
          avgEnd: (hit.newEnd + hit.oldEnd) / 2,
        });
        penalty += crossCellPenalty(hit.newType, hit.oldType);
      }

      // 将候选门按松散度匹配度排序（带少量随机扰动）
      const rankedDoorIds = doorIds
        .map((sid) => {
          const bestChoice = pickBestDoor(doorTargets.get(sid));
          const avgEnd = (bestChoice.newEnd + bestChoice.oldEnd) / 2;
          return {
            sid,
            choice: bestChoice,
            avgEnd,
            fit: 1 - Math.abs(avgEnd - looseness) + rng.float(0, 0.12),
          };
        })
        .sort((a, b) => b.fit - a.fit);

      if (chosenLinks.length === 0 && rankedDoorIds.length > 0) {
        const picked = rankedDoorIds.shift();
        chosenLinks.push({
          type: 'door',
          targetStripId: picked.sid,
          oldIdx: picked.choice.oldIdx,
          newIdx: picked.choice.newIdx,
          dirFromOld: picked.choice.dirFromOld,
          avgEnd: picked.avgEnd,
        });
      }

      // 非环条带严格保持度数 = 1，绝不补第二条门！
      if (!isNoLoopTemplate && chosenLinks.length === 1 && rankedDoorIds.length > 0) {
        if (needCycleNow || rng.chance(topology.extraLoopChance)) {
          const picked = rankedDoorIds.shift();
          chosenLinks.push({
            type: 'door',
            targetStripId: picked.sid,
            oldIdx: picked.choice.oldIdx,
            newIdx: picked.choice.newIdx,
            dirFromOld: picked.choice.dirFromOld,
            avgEnd: picked.avgEnd,
          });
        }
      }

      if (isNoLoopTemplate && chosenLinks.length !== 1) return;

      const closesLoop = chosenLinks.length >= 2;
      const meanEndFactor =
        chosenLinks.reduce((sum, lk) => sum + (lk.avgEnd ?? 0.5), 0) / Math.max(1, chosenLinks.length);
      const loosenessScore = (1 - Math.abs(meanEndFactor - looseness)) * 88;
      const cCenter = centroidOfCells(placedCells);
      const distCenter = Math.hypot(cCenter.x - regionCenter.x, cCenter.y - regionCenter.y);

      let score = loosenessScore;
      if (needCycleNow && closesLoop) score += 1000;
      else if (closesLoop) score += 35;

      const hasCross = crossIds.length > 0;
      if (hasCross === preferCross) score += 14;
      if (crossIds.length > 1) score -= 14;
      score -= penalty;
      score -= distCenter * centerPull;
      score += rng.float(0, 6);

      candidates.push({
        entry,
        variant,
        ox,
        oy,
        chosenLinks,
        closesLoop,
        score,
      });
    };

    // 仅从非 noLoop 的已有条带向外生长
    const growableHosts = rng.shuffle(strips.filter((s) => !s.noLoop));
    for (const entry of sampleEntries) {
      for (const variant of rng.shuffle([...entry.rotations])) {
        // 预先筛选并按松散度排序新条带的可连接格
        const newConnectCells = variant.cells
          .map((c, idx) => ({ c, idx, end: variant.endFactors[idx] ?? 0.5 }))
          .filter((item) => !variant.hasPortals || item.c.isPortal)
          .sort((a, b) => Math.abs(a.end - looseness) - Math.abs(b.end - looseness));

        for (const host of growableHosts) {
          if (candidates.length >= 160) break;
          // 宿主可开门端口（过滤出入口格 + 按松散度优先排序）
          const hostPorts = exteriorDoorPorts(host.cells, cellOwners)
            .filter((p) => cellAllowsConnection(host.cells, p.cellIndex))
            .map((p) => ({
              p,
              diff: Math.abs((host.cells[p.cellIndex]?.endFactor ?? 0.5) - looseness) + rng.float(0, 0.15),
            }))
            .sort((a, b) => a.diff - b.diff)
            .slice(0, 10)
            .map((x) => x.p);

          for (const port of hostPorts) {
            for (const { c } of newConnectCells) {
              evaluatePlacement(entry, variant, port.nx - c.rx, port.ny - c.ry);
            }
          }

          // 宿主单格交叉候选（过滤出入口格 + 按松散度优先排序）
          if (topology.allowCrossing) {
            const hostCells = host.cells
              .filter((hc) => cellAllowsConnection(host.cells, hc.index))
              .filter((hc) => (cellOwners.get(coordKey(hc.x, hc.y)) || []).length < 2)
              .map((hc) => ({
                hc,
                diff: Math.abs((hc.endFactor ?? 0.5) - looseness) + rng.float(0, 0.15),
              }))
              .sort((a, b) => a.diff - b.diff)
              .slice(0, 6)
              .map((x) => x.hc);

            for (const hc of hostCells) {
              for (const { c } of newConnectCells) {
                if (hc.type !== 'empty' && c.type !== 'empty') continue;
                evaluatePlacement(entry, variant, hc.x - c.rx, hc.y - c.ry);
              }
            }
          }
        }
      }
    }

    if (!candidates.length) break;
    candidates.sort((a, b) => b.score - a.score);
    const topK = candidates.slice(0, Math.min(3, candidates.length));
    const best = needCycleNow && topK[0].closesLoop ? topK[0] : rng.pick(topK);

    const newStrip = buildStripInstance({
      id: `${idPrefix}_s${strips.length + 1}`,
      entry: best.entry,
      variant: best.variant,
      origin: { x: best.ox, y: best.oy },
      regionId: region.id,
      role: 'normal',
    });
    addStripState(newStrip);
    curR += newStrip.resource;
    curD += newStrip.difficulty;

    for (const lk of best.chosenLinks) {
      if (lk.type === 'door') {
        addLinkState({
          id: `${idPrefix}_l${links.length + 1}`,
          type: 'door',
          a: { stripId: lk.targetStripId, cellIndex: lk.oldIdx },
          b: { stripId: newStrip.id, cellIndex: lk.newIdx },
          dir: lk.dirFromOld,
          isBridgeLink: false,
        });
      } else {
        addLinkState({
          id: `${idPrefix}_l${links.length + 1}`,
          type: 'cross',
          a: { stripId: lk.targetStripId, cellIndex: lk.oldIdx },
          b: { stripId: newStrip.id, cellIndex: lk.newIdx },
          cell: { x: lk.x, y: lk.y },
          isBridgeLink: false,
        });
      }
    }
  }

  // 3. 后置兜底补门成环（跳过非环条带，并严格尊重出入口格）
  let cycleCount = links.length - strips.length + 1;
  if (cycleCount < topology.minLoopsPerRegion) {
    for (let i = 0; i < strips.length && cycleCount < topology.minLoopsPerRegion; i += 1) {
      const a = strips[i];
      if (a.noLoop) continue;
      for (let j = i + 1; j < strips.length && cycleCount < topology.minLoopsPerRegion; j += 1) {
        const b = strips[j];
        if (b.noLoop || adj.get(a.id).has(b.id)) continue;
        const pairs = adjacentCellPairs(a.cells, b.cells).filter(
          (p) => cellAllowsConnection(a.cells, p.aIndex) && cellAllowsConnection(b.cells, p.bIndex),
        );
        if (!pairs.length) continue;
        const pick = rng.pick(pairs);
        if (
          addLinkState({
            id: `${idPrefix}_l${links.length + 1}`,
            type: 'door',
            a: { stripId: a.id, cellIndex: pick.aIndex },
            b: { stripId: b.id, cellIndex: pick.bIndex },
            dir: pick.dir,
            isBridgeLink: false,
          })
        ) {
          cycleCount += 1;
        }
      }
    }
  }

  return {
    region,
    strips,
    links,
    cycles: Math.max(0, links.length - strips.length + 1),
    localCounts,
  };
}

/* ------------------------------------------------------------------ *
 * 阶段 2：区域整体旋转/平移 + 向量差哈希 O(1) 三角刚体拼装
 * ------------------------------------------------------------------ */

function transformLocalRegion(local, rotation, dx = 0, dy = 0) {
  const strips = local.strips.map((strip) => {
    const newCells = strip.cells.map((cell) => {
      const pt = rotatePointCW(cell.x, cell.y, rotation);
      return { ...cell, x: pt.rx + dx, y: pt.ry + dy, openDirs: rotateOpenDirs(cell.openDirs, rotation) };
    });
    const b = boundsOfCells(newCells);
    return {
      ...strip,
      rotation: (strip.rotation + rotation) % 4,
      origin: { x: b.minX, y: b.minY },
      cells: newCells,
    };
  });
  const links = local.links.map((link) => {
    if (link.type === 'door') {
      return {
        ...link,
        a: { ...link.a },
        b: { ...link.b },
        dir: rotateDirCW(link.dir, rotation),
      };
    }
    const pt = rotatePointCW(link.cell.x, link.cell.y, rotation);
    return {
      ...link,
      a: { ...link.a },
      b: { ...link.b },
      cell: { x: pt.rx + dx, y: pt.ry + dy },
    };
  });
  return { ...local, strips, links };
}

function occupiedSetOfStrips(strips) {
  const set = new Set();
  for (const s of strips) {
    for (const c of s.cells) set.add(coordKey(c.x, c.y));
  }
  return set;
}

function paddedHaloOfSet(occSet) {
  const halo = new Set(occSet);
  for (const k of occSet) {
    const [xs, ys] = k.split(',');
    const x = Number(xs);
    const y = Number(ys);
    for (const dirId of DIR_IDS) {
      const d = DIRECTIONS[dirId];
      halo.add(coordKey(x + d.dx, y + d.dy));
    }
  }
  return halo;
}

/**
 * 收集区域可挂载跨区桥接条带的外边界门端口：
 * - 跳过 noLoop 非环条带（桥接绝不连到非环条带上）；
 * - 严格遵循出入口格（isPortal）限制。
 */
function regionExteriorPorts(local) {
  const occ = occupiedSetOfStrips(local.strips);
  const ports = [];
  for (const strip of local.strips) {
    if (strip.noLoop) continue;
    for (const cell of strip.cells) {
      if (!cellAllowsConnection(strip.cells, cell.index)) continue;
      for (const dirId of DIR_IDS) {
        const d = DIRECTIONS[dirId];
        const nx = cell.x + d.dx;
        const ny = cell.y + d.dy;
        if (occ.has(coordKey(nx, ny))) continue;
        ports.push({
          stripId: strip.id,
          cellIndex: cell.index,
          x: cell.x,
          y: cell.y,
          dir: dirId,
          nx,
          ny,
        });
      }
    }
  }
  return ports;
}

/**
 * 在 hostOcc 之外放置一座独立桥接条带（仅用门）：
 * - 若桥无出入口格（!hasPortals）：严格强制一端（首/尾）接区域1，另一端（尾/首）接区域2；
 * - 若桥有出入口格（hasPortals）：两端均须落在出入口格上，且严格分属不同格（若首尾均是出入口格，则强制首尾对端）。
 */
function candidateBridgePlacements(bridgeEntry, hostPort, forbiddenOcc, forbiddenHalo, rng) {
  const results = [];
  const dirOut = hostPort.dir;
  for (const variant of rng.shuffle([...bridgeEntry.rotations])) {
    const len = variant.cells.length;
    const hasPortals = variant.hasPortals;
    const portalIndices = variant.cells
      .map((c, idx) => (c.isPortal ? idx : -1))
      .filter((idx) => idx >= 0);
    const headIdx = variant.headIdx ?? 0;
    const tailIdx = variant.tailIdx ?? Math.max(0, len - 1);

    // 决定合法的 (anchorIdx, allowedExitSet) 对
    const anchorExitPairs = [];
    if (len <= 1) {
      anchorExitPairs.push({ anchorIdx: 0, exitSet: new Set([0]) });
    } else if (!hasPortals) {
      // 无出入口格：严格首接一区、尾接另一区（绝不连在同一端或中段）
      anchorExitPairs.push({ anchorIdx: headIdx, exitSet: new Set([tailIdx]) });
      if (tailIdx !== headIdx) {
        anchorExitPairs.push({ anchorIdx: tailIdx, exitSet: new Set([headIdx]) });
      }
    } else if (portalIndices.length >= 2) {
      // 有 >=2 个出入口格：若首尾恰为出入口格则首尾对端；否则任选两个不同出入口格
      const headIsPortal = Boolean(variant.cells[headIdx]?.isPortal);
      const tailIsPortal = Boolean(variant.cells[tailIdx]?.isPortal);
      if (headIsPortal && tailIsPortal && portalIndices.length === 2) {
        anchorExitPairs.push({ anchorIdx: headIdx, exitSet: new Set([tailIdx]) });
        anchorExitPairs.push({ anchorIdx: tailIdx, exitSet: new Set([headIdx]) });
      } else {
        for (const aIdx of portalIndices) {
          const others = portalIndices.filter((idx) => idx !== aIdx);
          if (others.length) anchorExitPairs.push({ anchorIdx: aIdx, exitSet: new Set(others) });
        }
      }
    } else {
      // 极端容错：只标了 1 个出入口格时，兜底取其对侧端点
      const aIdx = portalIndices[0];
      const opp = aIdx === headIdx ? tailIdx : headIdx;
      anchorExitPairs.push({ anchorIdx: aIdx, exitSet: new Set([opp]) });
    }

    for (const { anchorIdx, exitSet } of rng.shuffle(anchorExitPairs)) {
      const anchorCell = variant.cells[anchorIdx];
      const ox = hostPort.nx - anchorCell.rx;
      const oy = hostPort.ny - anchorCell.ry;
      const placed = placeCellsAt(variant.cells, ox, oy);
      let ok = true;
      for (const c of placed) {
        const k = coordKey(c.x, c.y);
        if (forbiddenOcc.has(k) || (forbiddenHalo && forbiddenHalo.has(k))) {
          ok = false;
          break;
        }
      }
      if (!ok) continue;

      const bridgeOcc = new Set(placed.map((c) => coordKey(c.x, c.y)));
      const exits = [];
      for (const c of placed) {
        if (!exitSet.has(c.index)) continue;
        for (const dId of DIR_IDS) {
          const d = DIRECTIONS[dId];
          const nx = c.x + d.dx;
          const ny = c.y + d.dy;
          const nk = coordKey(nx, ny);
          if (bridgeOcc.has(nk) || forbiddenOcc.has(nk)) continue;
          if (forbiddenHalo && forbiddenHalo.has(nk)) continue;
          exits.push({
            cellIndex: c.index,
            x: c.x,
            y: c.y,
            dir: dId,
            nx,
            ny,
          });
        }
      }
      if (!exits.length) continue;
      results.push({
        entry: bridgeEntry,
        variant,
        ox,
        oy,
        cells: placed,
        bridgeOcc,
        hostLink: {
          hostStripId: hostPort.stripId,
          hostCellIndex: hostPort.cellIndex,
          bridgeCellIndex: anchorIdx,
          dirFromHost: dirOut,
        },
        exits,
      });
    }
  }
  return results;
}

function buildRegionCDeltaIndex(localC, rng) {
  const rotatedVariants = [];
  for (let rot = 0; rot < 4; rot += 1) {
    const transformed = transformLocalRegion(localC, rot, 0, 0);
    const ports = regionExteriorPorts(transformed);
    const occ = occupiedSetOfStrips(transformed.strips);
    const deltaMap = new Map();
    const shuffledPorts = rng.shuffle([...ports]);
    const sampled = shuffledPorts.slice(0, Math.min(54, shuffledPorts.length));
    for (let i = 0; i < sampled.length; i += 1) {
      const p1 = sampled[i];
      for (let j = 0; j < sampled.length; j += 1) {
        if (i === j) continue;
        const p2 = sampled[j];
        const dist = Math.abs(p2.x - p1.x) + Math.abs(p2.y - p1.y);
        if (dist < 1) continue;
        const dk = `${p2.x - p1.x},${p2.y - p1.y}`;
        let bucket = deltaMap.get(dk);
        if (!bucket) {
          bucket = [];
          deltaMap.set(dk, bucket);
        }
        if (bucket.length < 10) {
          bucket.push({ p1, p2, dist });
        }
      }
    }
    rotatedVariants.push({ rot, region: transformed, occ, deltaMap });
  }
  return rotatedVariants;
}

function assembleTriangle({ localA, localB, localC, bridgeEntries, rng }) {
  const [tplAB, tplBC, tplCA] = bridgeEntries;
  const regA = transformLocalRegion(localA, 0, 0, 0);
  const occA = occupiedSetOfStrips(regA.strips);
  const haloA = paddedHaloOfSet(occA);
  const portsA = rng.shuffle(regionExteriorPorts(regA));
  const centerA = centroidOfCells(regA.strips.flatMap((s) => s.cells));

  const cIndex = buildRegionCDeltaIndex(localC, rng);

  const abAttempts = [];
  for (const portA of portsA.slice(0, 24)) {
    const bABList = candidateBridgePlacements(tplAB, portA, occA, null, rng);
    for (const bAB of bABList.slice(0, 5)) {
      const occAB = new Set([...occA, ...bAB.bridgeOcc]);
      let placedB = null;
      for (const exitAB of rng.shuffle([...bAB.exits])) {
        const rotsB = rng.shuffle([0, 1, 2, 3]);
        for (const rotB of rotsB) {
          const candB = transformLocalRegion(localB, rotB, 0, 0);
          const portsB = rng.shuffle(regionExteriorPorts(candB));
          const needDir = DIRECTIONS[exitAB.dir].opposite;
          for (const pB of portsB) {
            if (pB.dir !== needDir) continue;
            const ox = exitAB.nx - pB.x;
            const oy = exitAB.ny - pB.y;
            let ok = true;
            for (const s of candB.strips) {
              for (const c of s.cells) {
                const k = coordKey(c.x + ox, c.y + oy);
                if (occAB.has(k) || haloA.has(k)) {
                  ok = false;
                  break;
                }
              }
              if (!ok) break;
            }
            if (!ok) continue;
            placedB = {
              region: transformLocalRegion(candB, 0, ox, oy),
              exitAB,
              portB: { ...pB, x: pB.x + ox, y: pB.y + oy, nx: pB.nx + ox, ny: pB.ny + oy },
            };
            break;
          }
          if (placedB) break;
        }
        if (placedB) break;
      }
      if (placedB) {
        abAttempts.push({ bAB, ...placedB });
        if (abAttempts.length >= 12) break;
      }
    }
    if (abAttempts.length >= 12) break;
  }

  let bestAssembly = null;

  for (const ab of abAttempts) {
    const regB = ab.region;
    const occB = occupiedSetOfStrips(regB.strips);
    const haloB = paddedHaloOfSet(occB);
    const occA_B_AB = new Set([...occA, ...occB, ...ab.bAB.bridgeOcc]);
    const haloAB = paddedHaloOfSet(ab.bAB.bridgeOcc);
    const centerB = centroidOfCells(regB.strips.flatMap((s) => s.cells));

    const portsAC = regionExteriorPorts(regA)
      .filter((p) => !occA_B_AB.has(coordKey(p.nx, p.ny)) && !haloB.has(coordKey(p.nx, p.ny)))
      .sort((p1, p2) => {
        const cross1 = Math.abs((p1.nx - centerA.x) * (centerB.y - centerA.y) - (p1.ny - centerA.y) * (centerB.x - centerA.x));
        const cross2 = Math.abs((p2.nx - centerA.x) * (centerB.y - centerA.y) - (p2.ny - centerA.y) * (centerB.x - centerA.x));
        return cross2 - cross1;
      });
    const portsBC = regionExteriorPorts(regB)
      .filter((p) => !occA_B_AB.has(coordKey(p.nx, p.ny)) && !haloA.has(coordKey(p.nx, p.ny)))
      .sort((p1, p2) => {
        const cross1 = Math.abs((p1.nx - centerB.x) * (centerA.y - centerB.y) - (p1.ny - centerB.y) * (centerA.x - centerB.x));
        const cross2 = Math.abs((p2.nx - centerB.x) * (centerA.y - centerB.y) - (p2.ny - centerB.y) * (centerA.x - centerB.x));
        return cross2 - cross1;
      });

    const candAC = [];
    for (const pA of portsAC.slice(0, 16)) {
      const list = candidateBridgePlacements(tplCA, pA, occA_B_AB, haloB, rng);
      for (const item of list.slice(0, 3)) {
        let touchesAB = false;
        for (const c of item.cells) {
          if (haloAB.has(coordKey(c.x, c.y))) {
            touchesAB = true;
            break;
          }
        }
        if (!touchesAB) candAC.push(item);
      }
      if (candAC.length >= 18) break;
    }

    const candBC = [];
    for (const pB of portsBC.slice(0, 16)) {
      const list = candidateBridgePlacements(tplBC, pB, occA_B_AB, haloA, rng);
      for (const item of list.slice(0, 3)) {
        let touchesAB = false;
        for (const c of item.cells) {
          if (haloAB.has(coordKey(c.x, c.y))) {
            touchesAB = true;
            break;
          }
        }
        if (!touchesAB) candBC.push(item);
      }
      if (candBC.length >= 18) break;
    }

    for (const bAC of candAC) {
      const haloAC = paddedHaloOfSet(bAC.bridgeOcc);
      for (const bBC of candBC) {
        let bridgesConflict = false;
        for (const c of bBC.cells) {
          if (haloAC.has(coordKey(c.x, c.y))) {
            bridgesConflict = true;
            break;
          }
        }
        if (bridgesConflict) continue;

        const occAllSoFar = new Set([...occA_B_AB, ...bAC.bridgeOcc, ...bBC.bridgeOcc]);
        const forbiddenHaloForC = new Set([...haloA, ...haloB, ...haloAB]);

        for (const q1 of bAC.exits.slice(0, 6)) {
          if (forbiddenHaloForC.has(coordKey(q1.nx, q1.ny)) || occAllSoFar.has(coordKey(q1.nx, q1.ny))) continue;
          const needDir1 = DIRECTIONS[q1.dir].opposite;
          for (const q2 of bBC.exits.slice(0, 6)) {
            if (forbiddenHaloForC.has(coordKey(q2.nx, q2.ny)) || occAllSoFar.has(coordKey(q2.nx, q2.ny))) continue;
            const needDir2 = DIRECTIONS[q2.dir].opposite;
            const dk = `${q2.nx - q1.nx},${q2.ny - q1.ny}`;

            for (const cVar of cIndex) {
              const matches = cVar.deltaMap.get(dk);
              if (!matches) continue;
              for (const pair of matches) {
                if (pair.p1.dir !== needDir1 || pair.p2.dir !== needDir2) continue;
                const ox = q1.nx - pair.p1.x;
                const oy = q1.ny - pair.p1.y;
                let ok = true;
                for (const s of cVar.region.strips) {
                  for (const c of s.cells) {
                    const k = coordKey(c.x + ox, c.y + oy);
                    if (occAllSoFar.has(k) || forbiddenHaloForC.has(k)) {
                      ok = false;
                      break;
                    }
                  }
                  if (!ok) break;
                }
                if (!ok) continue;

                const regC = transformLocalRegion(cVar.region, 0, ox, oy);
                const centerC = centroidOfCells(regC.strips.flatMap((s) => s.cells));

                const dAB = Math.hypot(centerA.x - centerB.x, centerA.y - centerB.y);
                const dBC = Math.hypot(centerB.x - centerC.x, centerB.y - centerC.y);
                const dCA = Math.hypot(centerC.x - centerA.x, centerC.y - centerA.y);
                const avgEdge = (dAB + dBC + dCA) / 3;
                const variance =
                  Math.abs(dAB - avgEdge) + Math.abs(dBC - avgEdge) + Math.abs(dCA - avgEdge);
                const allCells = [
                  ...regA.strips.flatMap((s) => s.cells),
                  ...regB.strips.flatMap((s) => s.cells),
                  ...regC.strips.flatMap((s) => s.cells),
                  ...ab.bAB.cells,
                  ...bAC.cells,
                  ...bBC.cells,
                ];
                const bBox = boundsOfCells(allCells);
                const aspect = Math.max(bBox.width, bBox.height) / Math.max(1, Math.min(bBox.width, bBox.height));
                const score = 200 - variance * 3 - aspect * 18 - (bBox.width + bBox.height) * 0.45;

                if (!bestAssembly || score > bestAssembly.score) {
                  bestAssembly = {
                    score,
                    regA,
                    regB,
                    regC,
                    bAB: ab.bAB,
                    bAB_B: { exitAB: ab.exitAB, portB: ab.portB },
                    bAC,
                    bAC_C: {
                      exitAC: q1,
                      portC: { ...pair.p1, x: pair.p1.x + ox, y: pair.p1.y + oy },
                    },
                    bBC,
                    bBC_C: {
                      exitBC: q2,
                      portC: { ...pair.p2, x: pair.p2.x + ox, y: pair.p2.y + oy },
                    },
                  };
                }
              }
            }
          }
        }
        if (bestAssembly && bestAssembly.score > 120) break;
      }
      if (bestAssembly && bestAssembly.score > 120) break;
    }
    if (bestAssembly && bestAssembly.score > 120) break;
  }

  return bestAssembly;
}

/* ------------------------------------------------------------------ *
 * 阶段 3：预算区间双通道收敛（严守 noLoop / maxNoLoop / isPortal）
 * ------------------------------------------------------------------ */

function findMatchingRotation(entry, targetCells) {
  const targetKey = exactShapeKey(targetCells);
  const targetEdges = exactOpenEdgeKey(targetCells);
  for (const variant of entry.rotations) {
    if (variant.shapeKey === targetKey && exactOpenEdgeKey(variant.cells) === targetEdges) return variant;
  }
  return null;
}

function variantCompatibleWithLinks(strip, variant, links, allStrips) {
  const b = boundsOfCells(strip.cells);
  const placed = placeCellsAt(variant.cells, b.minX, b.minY);
  const byCoord = new Map(placed.map((c) => [coordKey(c.x, c.y), c]));
  const stripById = new Map((allStrips || []).map((s) => [s.id, s]));

  for (const link of links) {
    const isA = link.a.stripId === strip.id;
    const isB = link.b.stripId === strip.id;
    if (!isA && !isB) continue;
    const myEnd = isA ? link.a : link.b;
    const otherEnd = isA ? link.b : link.a;
    const oldCell = strip.cells[myEnd.cellIndex];
    const mapped = oldCell ? byCoord.get(coordKey(oldCell.x, oldCell.y)) : null;
    if (!mapped) return false;

    if (variant.hasPortals && !mapped.isPortal) return false;

    // 方案 1：交叉连接要求至少一方是通道/空
    if (link.type === 'cross' && mapped.type !== 'empty') {
      const otherStrip = stripById.get(otherEnd.stripId);
      const otherCell = otherStrip?.cells?.[otherEnd.cellIndex];
      if (otherCell && otherCell.type !== 'empty') return false;
    }
  }
  return true;
}

function applyTemplateToStripSameShape(strip, entry, variant) {
  const b = boundsOfCells(strip.cells);
  const newPlaced = placeCellsAt(variant.cells, b.minX, b.minY);
  const byCoord = new Map(newPlaced.map((c) => [coordKey(c.x, c.y), c]));
  const remappedCells = strip.cells.map((oldCell, idx) => {
    const hit = byCoord.get(coordKey(oldCell.x, oldCell.y));
    return {
      ...oldCell,
      index: idx,
      rx: hit ? hit.rx : oldCell.rx,
      ry: hit ? hit.ry : oldCell.ry,
      type: hit ? hit.type : oldCell.type,
      difficultyOffset: hit ? (hit.difficultyOffset || 0) : 0,
      isPortal: hit ? Boolean(hit.isPortal) : false,
      openDirs: hit?.openDirs ?? oldCell.openDirs,
      endFactor: hit ? (variant.endFactors?.[hit.index] ?? oldCell.endFactor ?? 0.5) : (oldCell.endFactor ?? 0.5),
      enemyLevel: null,
    };
  });
  strip.templateId = entry.template.id;
  strip.templateName = entry.template.name;
  strip.noLoop = Boolean(entry.template.noLoop) && strip.role === 'normal';
  strip.rotation = variant.rotation;
  strip.resource = entry.metrics.resource;
  strip.difficulty = entry.metrics.difficulty;
  strip.cells = remappedCells;
}

function convergeFloorBudget(map, targetBudget, prepared, profile, rng) {
  const { resourceMin, resourceMax, difficultyMin, difficultyMax } = targetBudget;
  const regionMap = new Map(profile.regions.map((r) => [r.id, r]));

  const totalError = () => {
    let r = 0;
    let d = 0;
    for (const s of map.strips) {
      r += Number(s.resource) || 0;
      d += Number(s.difficulty) || 0;
    }
    return {
      r,
      d,
      err: rangeDist(r, resourceMin, resourceMax) + rangeDist(d, difficultyMin, difficultyMax),
    };
  };

  let cur = totalError();
  if (cur.err === 0) return;

  const counts = new Map();
  const noLoopByRegion = new Map();
  for (const s of map.strips) {
    counts.set(s.templateId, (counts.get(s.templateId) || 0) + 1);
    if (s.noLoop) noLoopByRegion.set(s.regionId, (noLoopByRegion.get(s.regionId) || 0) + 1);
  }

  const computeDegrees = () => {
    const deg = new Map(map.strips.map((s) => [s.id, 0]));
    for (const l of map.links) {
      deg.set(l.a.stripId, (deg.get(l.a.stripId) || 0) + 1);
      deg.set(l.b.stripId, (deg.get(l.b.stripId) || 0) + 1);
    }
    return deg;
  };

  // Pass 1：同形状签名模板互换
  for (let iter = 0; iter < 40 && cur.err > 0; iter += 1) {
    const deg = computeDegrees();
    let bestMove = null;
    for (const strip of rng.shuffle([...map.strips])) {
      const role = strip.role === 'bridge' ? 'bridge' : 'normal';
      const mustSpawn = strip.id === map.spawn?.stripId;
      const stripDeg = deg.get(strip.id) || 0;
      const regionMaxNoLoop = regionMap.get(strip.regionId)?.maxNoLoop ?? 0;

      for (const entry of prepared) {
        if (entry.template.id === strip.templateId) continue;
        if (entry.template.role !== role) continue;
        if (role === 'normal' && !templateMatchesRegion(entry.template, strip.regionId)) continue;
        if (mustSpawn && !entry.template.spawnEligible) continue;
        if (entry.signature !== strip.shapeSignature) continue;
        // 非环硬约束检查
        if (entry.template.noLoop) {
          if (stripDeg !== 1 || mustSpawn || role !== 'normal') continue;
          const nextNoLoop = (noLoopByRegion.get(strip.regionId) || 0) + (strip.noLoop ? 0 : 1);
          if (nextNoLoop > regionMaxNoLoop) continue;
        }
        const cap = entry.template.generation.unique
          ? Math.min(1, entry.template.generation.max)
          : entry.template.generation.max;
        if ((counts.get(entry.template.id) || 0) >= cap) continue;

        const variant = findMatchingRotation(entry, strip.cells);
        if (!variant) continue;
        if (!variantCompatibleWithLinks(strip, variant, map.links, map.strips)) continue;

        const nextR = cur.r - strip.resource + entry.metrics.resource;
        const nextD = cur.d - strip.difficulty + entry.metrics.difficulty;
        const nextErr =
          rangeDist(nextR, resourceMin, resourceMax) +
          rangeDist(nextD, difficultyMin, difficultyMax);
        if (nextErr < cur.err && (!bestMove || nextErr < bestMove.nextErr)) {
          bestMove = { strip, entry, variant, nextErr };
        }
      }
    }
    if (!bestMove) break;
    const { strip, entry, variant } = bestMove;
    counts.set(strip.templateId, Math.max(0, (counts.get(strip.templateId) || 1) - 1));
    if (strip.noLoop) noLoopByRegion.set(strip.regionId, Math.max(0, (noLoopByRegion.get(strip.regionId) || 1) - 1));
    applyTemplateToStripSameShape(strip, entry, variant);
    counts.set(strip.templateId, (counts.get(strip.templateId) || 0) + 1);
    if (strip.noLoop) noLoopByRegion.set(strip.regionId, (noLoopByRegion.get(strip.regionId) || 0) + 1);
    if (strip.id === map.spawn?.stripId) {
      const spIdx = Math.max(0, strip.cells.findIndex((c) => c.type === 'spawn'));
      map.spawn = {
        stripId: strip.id,
        cellIndex: spIdx,
        x: strip.cells[spIdx].x,
        y: strip.cells[spIdx].y,
      };
    }
    cur = totalError();
  }

  if (cur.err === 0) return;

  // Pass 2：末端叶条带（degree === 1 且非桥非出生）换形替换
  for (let iter = 0; iter < 16 && cur.err > 0; iter += 1) {
    const degree = computeDegrees();
    const leaves = rng.shuffle(
      map.strips.filter(
        (s) => s.role === 'normal' && s.id !== map.spawn?.stripId && degree.get(s.id) === 1,
      ),
    );
    let improved = false;

    for (const leaf of leaves) {
      const linkIdx = map.links.findIndex((l) => l.a.stripId === leaf.id || l.b.stripId === leaf.id);
      if (linkIdx < 0) continue;
      const oldLink = map.links[linkIdx];
      const hostId = oldLink.a.stripId === leaf.id ? oldLink.b.stripId : oldLink.a.stripId;
      const host = map.strips.find((s) => s.id === hostId);
      if (!host || host.role === 'bridge' || host.noLoop) continue;

      const regionMaxNoLoop = regionMap.get(leaf.regionId)?.maxNoLoop ?? 0;
      const otherStrips = map.strips.filter((s) => s.id !== leaf.id);
      const otherOcc = occupiedSetOfStrips(otherStrips);
      const foreignStrips = otherStrips.filter((s) => s.role === 'bridge' || s.regionId !== leaf.regionId);
      const foreignHalo = paddedHaloOfSet(occupiedSetOfStrips(foreignStrips));

      const betterTemplates = prepared
        .filter((entry) => {
          if (entry.template.id === leaf.templateId) return false;
          if (entry.template.role !== 'normal') return false;
          if (!templateMatchesRegion(entry.template, leaf.regionId)) return false;
          if (entry.template.noLoop) {
            const nextNoLoop = (noLoopByRegion.get(leaf.regionId) || 0) + (leaf.noLoop ? 0 : 1);
            if (nextNoLoop > regionMaxNoLoop) return false;
          }
          const cap = entry.template.generation.unique
            ? Math.min(1, entry.template.generation.max)
            : entry.template.generation.max;
          if ((counts.get(entry.template.id) || 0) >= cap) return false;
          const nr = cur.r - leaf.resource + entry.metrics.resource;
          const nd = cur.d - leaf.difficulty + entry.metrics.difficulty;
          const ne = rangeDist(nr, resourceMin, resourceMax) + rangeDist(nd, difficultyMin, difficultyMax);
          return ne < cur.err;
        })
        .sort((a, b) => {
          const ea =
            rangeDist(cur.r - leaf.resource + a.metrics.resource, resourceMin, resourceMax) +
            rangeDist(cur.d - leaf.difficulty + a.metrics.difficulty, difficultyMin, difficultyMax);
          const eb =
            rangeDist(cur.r - leaf.resource + b.metrics.resource, resourceMin, resourceMax) +
            rangeDist(cur.d - leaf.difficulty + b.metrics.difficulty, difficultyMin, difficultyMax);
          return ea - eb;
        });

      const hostPorts = rng
        .shuffle(exteriorDoorPorts(host.cells, otherOcc))
        .filter((p) => cellAllowsConnection(host.cells, p.cellIndex));
      let replaced = false;

      for (const entry of betterTemplates.slice(0, 8)) {
        for (const variant of entry.rotations) {
          for (const port of hostPorts.slice(0, 10)) {
            for (let cIdx = 0; cIdx < variant.cells.length; cIdx += 1) {
              const c = variant.cells[cIdx];
              if (variant.hasPortals && !c.isPortal) continue;
              const ox = port.nx - c.rx;
              const oy = port.ny - c.ry;
              const placed = placeCellsAt(variant.cells, ox, oy);
              let ok = true;
              for (const pc of placed) {
                const k = coordKey(pc.x, pc.y);
                if (otherOcc.has(k) || foreignHalo.has(k)) {
                  ok = false;
                  break;
                }
              }
              if (!ok) continue;

              counts.set(leaf.templateId, Math.max(0, (counts.get(leaf.templateId) || 1) - 1));
              if (leaf.noLoop) noLoopByRegion.set(leaf.regionId, Math.max(0, (noLoopByRegion.get(leaf.regionId) || 1) - 1));
              leaf.templateId = entry.template.id;
              leaf.templateName = entry.template.name;
              leaf.noLoop = Boolean(entry.template.noLoop);
              leaf.rotation = variant.rotation;
              leaf.shapeSignature = entry.signature;
              leaf.origin = { x: ox, y: oy };
              leaf.resource = entry.metrics.resource;
              leaf.difficulty = entry.metrics.difficulty;
              leaf.cells = placed.map((pc, pi) => ({
                index: pc.index,
                rx: pc.rx,
                ry: pc.ry,
                x: pc.x,
                y: pc.y,
                type: pc.type,
                difficultyOffset: pc.difficultyOffset || 0,
                isPortal: Boolean(pc.isPortal),
            openDirs: pc.openDirs,
                endFactor: variant.endFactors?.[pi] ?? 0.5,
                enemyLevel: null,
              }));
              map.links[linkIdx] = {
                id: oldLink.id,
                type: 'door',
                a: { stripId: host.id, cellIndex: port.cellIndex },
                b: { stripId: leaf.id, cellIndex: cIdx },
                dir: port.dir,
                isBridgeLink: false,
              };
              counts.set(leaf.templateId, (counts.get(leaf.templateId) || 0) + 1);
              if (leaf.noLoop) noLoopByRegion.set(leaf.regionId, (noLoopByRegion.get(leaf.regionId) || 0) + 1);
              cur = totalError();
              replaced = true;
              improved = true;
              break;
            }
            if (replaced) break;
          }
          if (replaced) break;
        }
        if (replaced) break;
      }
      if (cur.err === 0) break;
    }
    if (!improved) break;
  }
}

/* ------------------------------------------------------------------ *
 * 阶段 4：节点图距离、条带等级与敌人等级计算
 * ------------------------------------------------------------------ */

export function recomputeFloorProgression(map, progression = null, cellTypes = DEFAULT_CELL_TYPES) {
  const prog = progression || map?.progression || { baseLevel: 1, levelPerHop: 0.75, maxLevel: 20 };
  const tMap = cellTypeMap(cellTypes);
  const strips = Array.isArray(map?.strips) ? map.strips : [];
  const links = Array.isArray(map?.links) ? map.links : [];
  const adj = new Map(strips.map((s) => [s.id, new Set()]));
  for (const link of links) {
    const a = link?.a?.stripId;
    const b = link?.b?.stripId;
    if (adj.has(a) && adj.has(b)) {
      adj.get(a).add(b);
      adj.get(b).add(a);
    }
  }

  const distMap = new Map();
  const startId = map?.spawn?.stripId || strips[0]?.id;
  if (startId && adj.has(startId)) {
    distMap.set(startId, 0);
    const queue = [startId];
    while (queue.length) {
      const cur = queue.shift();
      const d = distMap.get(cur);
      for (const next of adj.get(cur) || []) {
        if (!distMap.has(next)) {
          distMap.set(next, d + 1);
          queue.push(next);
        }
      }
    }
  }

  for (const strip of strips) {
    const dist = distMap.has(strip.id) ? distMap.get(strip.id) : 0;
    const level = clamp(
      Math.round(prog.baseLevel) + Math.floor(dist * (Number(prog.levelPerHop) || 0)),
      1,
      Math.max(1, Math.round(prog.maxLevel || 99)),
    );
    strip.distanceFromSpawn = dist;
    strip.level = level;
    for (const cell of strip.cells || []) {
      const def = tMap.get(cell.type);
      if (def?.supportsOffset) {
        cell.enemyLevel = Math.max(1, level + Math.round(Number(cell.difficultyOffset) || 0));
      } else {
        cell.enemyLevel = null;
      }
    }
  }

  if (map) {
    map.progression = {
      baseLevel: prog.baseLevel,
      levelPerHop: prog.levelPerHop,
      maxLevel: prog.maxLevel,
    };
  }
  return map;
}

export function recomputeFloorMetrics(map, cellTypes = DEFAULT_CELL_TYPES) {
  let totalResource = 0;
  let totalDifficulty = 0;
  for (const strip of map?.strips || []) {
    totalResource += Number(strip.resource) || 0;
    totalDifficulty += Number(strip.difficulty) || 0;
  }
  totalResource = Math.round(totalResource * 100) / 100;
  totalDifficulty = Math.round(totalDifficulty * 100) / 100;
  if (map?.budget) {
    map.budget.actual = { resource: totalResource, difficulty: totalDifficulty };
  }
  const validation = validateFloorMap(map);
  map.stats = validation.stats;
  map.issues = validation.issues;
  map.warnings = validation.warnings;
  map.errors = validation.errors;
  return map;
}

/* ------------------------------------------------------------------ *
 * 单次尝试与多轮重试择优入口
 * ------------------------------------------------------------------ */

function generateFloorOnce({ profile, prepared, cellTypes, targetBudget, seed, attempt, createdAt }) {
  const rng = new RNG(seed);
  const [rA, rB, rC] = profile.regions;
  const countA = rng.int(rA.stripMin, rA.stripMax);
  const countB = rng.int(rB.stripMin, rB.stripMax);
  const countC = rng.int(rC.stripMin, rC.stripMax);
  const totalStrips = countA + countB + countC + 3;

  const targetMidR = (targetBudget.resourceMin + targetBudget.resourceMax) / 2;
  const targetMidD = (targetBudget.difficultyMin + targetBudget.difficultyMax) / 2;

  const makeRegionWithCycle = (region, count, isStart, prefix, globalCounts) => {
    let bestLocal = null;
    for (let retry = 0; retry < 6; retry += 1) {
      const subRng = retry === 0 ? rng : new RNG(`${seed}:${prefix}:r${retry}`);
      const local = generateLocalRegion({
        region,
        targetCount: count,
        isStartRegion: isStart,
        prepared,
        topology: profile.topology,
        budgetHint: {
          targetR: (targetMidR * count) / totalStrips,
          targetD: (targetMidD * count) / totalStrips,
        },
        globalCounts,
        rng: subRng,
        idPrefix: prefix,
      });
      if (
        !bestLocal ||
        local.cycles > bestLocal.cycles ||
        (local.cycles === bestLocal.cycles && local.strips.length > bestLocal.strips.length)
      ) {
        bestLocal = local;
      }
      if (local.cycles >= profile.topology.minLoopsPerRegion && local.strips.length >= count) break;
    }
    return bestLocal;
  };

  const globalCounts = new Map();
  const localA = makeRegionWithCycle(rA, countA, true, 'ra', globalCounts);
  for (const s of localA.strips) globalCounts.set(s.templateId, (globalCounts.get(s.templateId) || 0) + 1);
  const localB = makeRegionWithCycle(rB, countB, false, 'rb', globalCounts);
  for (const s of localB.strips) globalCounts.set(s.templateId, (globalCounts.get(s.templateId) || 0) + 1);
  const localC = makeRegionWithCycle(rC, countC, false, 'rc', globalCounts);
  for (const s of localC.strips) globalCounts.set(s.templateId, (globalCounts.get(s.templateId) || 0) + 1);

  let bridgePool = prepared.filter((e) => e.template.role === 'bridge' && e.template.generation.max > 0);
  if (!bridgePool.length) bridgePool = prepared.filter((e) => e.template.role === 'bridge');
  if (!bridgePool.length) bridgePool = prepared.filter((e) => !e.template.noLoop);
  if (!bridgePool.length) bridgePool = prepared;

  const pickBridge = (usedMap) => {
    const avail = bridgePool.filter((e) => {
      const cap = e.template.generation.unique ? Math.min(1, e.template.generation.max) : e.template.generation.max;
      return (usedMap.get(e.template.id) || 0) < Math.max(1, cap);
    });
    const pool = avail.length ? avail : bridgePool;
    const picked = rng.weighted(pool, (e) => e.template.generation.weight) || pool[0];
    usedMap.set(picked.template.id, (usedMap.get(picked.template.id) || 0) + 1);
    return picked;
  };
  const bridgeUsed = new Map();
  const bridgeEntries = [pickBridge(bridgeUsed), pickBridge(bridgeUsed), pickBridge(bridgeUsed)];

  const assembly = assembleTriangle({ localA, localB, localC, bridgeEntries, rng });
  if (!assembly) return null;

  const rawStrips = [
    ...assembly.regA.strips,
    ...assembly.regB.strips,
    ...assembly.regC.strips,
  ];
  const rawLinks = [
    ...assembly.regA.links,
    ...assembly.regB.links,
    ...assembly.regC.links,
  ];

  const makeBridgeStrip = (id, bObj, pairRegions) =>
    buildStripInstance({
      id,
      entry: bObj.entry,
      variant: bObj.variant,
      origin: { x: bObj.ox, y: bObj.oy },
      regionId: 'bridge',
      role: 'bridge',
      bridgeBetween: pairRegions,
    });

  const bridgeAB = makeBridgeStrip('br_ab', assembly.bAB, [rA.id, rB.id]);
  const bridgeBC = makeBridgeStrip('br_bc', assembly.bBC, [rB.id, rC.id]);
  const bridgeCA = makeBridgeStrip('br_ca', assembly.bAC, [rC.id, rA.id]);
  rawStrips.push(bridgeAB, bridgeBC, bridgeCA);

  const bridgeDoorLinks = [
    {
      id: 'lb_1',
      type: 'door',
      a: { stripId: assembly.bAB.hostLink.hostStripId, cellIndex: assembly.bAB.hostLink.hostCellIndex },
      b: { stripId: bridgeAB.id, cellIndex: assembly.bAB.hostLink.bridgeCellIndex },
      dir: assembly.bAB.hostLink.dirFromHost,
      isBridgeLink: true,
    },
    {
      id: 'lb_2',
      type: 'door',
      a: { stripId: bridgeAB.id, cellIndex: assembly.bAB_B.exitAB.cellIndex },
      b: { stripId: assembly.bAB_B.portB.stripId, cellIndex: assembly.bAB_B.portB.cellIndex },
      dir: assembly.bAB_B.exitAB.dir,
      isBridgeLink: true,
    },
    {
      id: 'lb_3',
      type: 'door',
      a: { stripId: assembly.bAC.hostLink.hostStripId, cellIndex: assembly.bAC.hostLink.hostCellIndex },
      b: { stripId: bridgeCA.id, cellIndex: assembly.bAC.hostLink.bridgeCellIndex },
      dir: assembly.bAC.hostLink.dirFromHost,
      isBridgeLink: true,
    },
    {
      id: 'lb_4',
      type: 'door',
      a: { stripId: bridgeCA.id, cellIndex: assembly.bAC_C.exitAC.cellIndex },
      b: { stripId: assembly.bAC_C.portC.stripId, cellIndex: assembly.bAC_C.portC.cellIndex },
      dir: assembly.bAC_C.exitAC.dir,
      isBridgeLink: true,
    },
    {
      id: 'lb_5',
      type: 'door',
      a: { stripId: assembly.bBC.hostLink.hostStripId, cellIndex: assembly.bBC.hostLink.hostCellIndex },
      b: { stripId: bridgeBC.id, cellIndex: assembly.bBC.hostLink.bridgeCellIndex },
      dir: assembly.bBC.hostLink.dirFromHost,
      isBridgeLink: true,
    },
    {
      id: 'lb_6',
      type: 'door',
      a: { stripId: bridgeBC.id, cellIndex: assembly.bBC_C.exitBC.cellIndex },
      b: { stripId: assembly.bBC_C.portC.stripId, cellIndex: assembly.bBC_C.portC.cellIndex },
      dir: assembly.bBC_C.exitBC.dir,
      isBridgeLink: true,
    },
  ];
  rawLinks.push(...bridgeDoorLinks);

  const allBounds = boundsOfCells(rawStrips.flatMap((s) => s.cells));
  const shiftX = 2 - allBounds.minX;
  const shiftY = 2 - allBounds.minY;

  const idMap = new Map();
  const strips = rawStrips.map((strip, idx) => {
    const newId = `s${idx + 1}`;
    idMap.set(strip.id, newId);
    const shiftedCells = strip.cells.map((c) => ({
      ...c,
      x: c.x + shiftX,
      y: c.y + shiftY,
    }));
    const b = boundsOfCells(shiftedCells);
    return {
      ...strip,
      id: newId,
      origin: { x: b.minX, y: b.minY },
      cells: shiftedCells,
    };
  });

  const links = rawLinks.map((link, idx) => {
    const base = {
      id: `l${idx + 1}`,
      type: link.type,
      a: { stripId: idMap.get(link.a.stripId), cellIndex: link.a.cellIndex },
      b: { stripId: idMap.get(link.b.stripId), cellIndex: link.b.cellIndex },
      isBridgeLink: Boolean(link.isBridgeLink),
    };
    if (link.type === 'door') base.dir = link.dir;
    else base.cell = { x: link.cell.x + shiftX, y: link.cell.y + shiftY };
    return base;
  });

  const spawnStrip = strips.find((s) => s.role === 'spawn') || strips[0];
  const spawnCellIdx = Math.max(0, spawnStrip.cells.findIndex((c) => c.type === 'spawn'));
  const spawnCell = spawnStrip.cells[spawnCellIdx];

  const map = {
    id: '',
    kind: 'plotweave-floor-map',
    seed: '',
    profileId: profile.id,
    profileName: profile.name,
    generator: FLOOR_GENERATOR_ID,
    generatorVersion: FLOOR_GENERATOR_VERSION,
    createdAt: createdAt || new Date().toISOString(),
    attempt,
    regions: clone(profile.regions),
    topology: clone(profile.topology),
    progression: clone(profile.progression),
    strips,
    links,
    spawn: {
      stripId: spawnStrip.id,
      cellIndex: spawnCellIdx,
      x: spawnCell.x,
      y: spawnCell.y,
    },
    budget: {
      target: clone(targetBudget),
      actual: { resource: 0, difficulty: 0 },
    },
    stats: null,
    issues: [],
    warnings: [],
    errors: [],
  };

  convergeFloorBudget(map, targetBudget, prepared, profile, rng);
  recomputeFloorProgression(map, profile.progression, cellTypes);
  recomputeFloorMetrics(map, cellTypes);

  return map;
}

export function generateFloorMap({
  profile: rawProfile,
  stripTemplates: rawTemplates,
  cellTypes: rawCellTypes = DEFAULT_CELL_TYPES,
  seed: rawSeed = null,
  attempts = 18,
  createdAt = null,
} = {}) {
  const profile = normalizeFloorProfile(rawProfile);
  const cellTypes = normalizeCellTypes(rawCellTypes);
  const prepared = prepareTemplates(rawTemplates, cellTypes);
  if (!prepared.length) {
    throw new Error('没有可用的条带模板，请先在「条带模板」中创建模板。');
  }

  const seed = normalizeSeed(rawSeed || randomSeed());
  const targetBudget = estimateFloorBudget(profile, prepared.map((p) => p.template), cellTypes);

  let best = null;
  let bestRank = -Infinity;

  for (let attempt = 0; attempt < attempts; attempt += 1) {
    const attemptSeed = attempt === 0 ? seed : `${seed}#${attempt + 1}`;
    const candidate = generateFloorOnce({
      profile,
      prepared,
      cellTypes,
      targetBudget,
      seed: attemptSeed,
      attempt,
      createdAt,
    });
    if (!candidate) continue;

    candidate.seed = seed;
    candidate.id = `floor_${seed.replace(/[^A-Za-z0-9_-]+/g, '_').slice(0, 48)}`;

    const errCount = candidate.errors.length;
    const budgetDist =
      rangeDist(candidate.stats.totalResource, targetBudget.resourceMin, targetBudget.resourceMax) +
      rangeDist(candidate.stats.totalDifficulty, targetBudget.difficultyMin, targetBudget.difficultyMax);
    const inBudget = candidate.stats.resourceInRange && candidate.stats.difficultyInRange;
    const rank =
      (errCount === 0 ? 100000 : -errCount * 10000) +
      (inBudget ? 10000 : -budgetDist * 20) +
      candidate.stats.maxDistance * 2;

    if (rank > bestRank) {
      bestRank = rank;
      best = candidate;
    }
    if (errCount === 0 && inBudget) break;
  }

  if (!best) {
    throw new Error('无法将当前条带模板拼装成三区域三角形单层，请增加条带模板多样性或检查区域配置。');
  }
  return best;
}
