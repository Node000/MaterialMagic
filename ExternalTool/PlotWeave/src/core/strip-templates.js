/**
 * 条带模板与全局格子配置核心层。
 *
 * - 全局格子配置仅包含「资源值 (resource)」与「难度值 (difficulty)」两个数值属性；
 * - 条带模板为无矩形（无 2×2 实心块）、无分叉、明确首尾的单链多格房间（直线或折线）；
 * - 条带资源与难度默认为所有格子之和，支持单条带手动覆写；
 * - 战斗格难度偏移 (difficultyOffset) 仅用于地图敌人等级计算，不改变条带总难度；
 * - 支持条带级「非环 (noLoop)」属性与格子级「出入口 (isPortal)」附加属性；
 * - 内置单条随机草稿生成器，供编辑器生成后手工精修并保存；
 * - 支持模板库独立 JSON 导入 / 导出。
 */

import { explicitOpenCells, hasConnectedOpenEdges, isOpenChain, validateOpenDirs } from './strip-edges.js';
import {
  APP_ID,
  DEFAULT_CELL_TYPES,
  DIRECTIONS,
  DIR_IDS,
  SHAPE_STYLE_IDS,
  STRIP_CELLS_MAX,
  STRIP_CELLS_MIN,
  STRIP_ROLE_IDS,
  clamp,
  isHexColor,
} from './constants.js';
import {
  analyzeShapeKind,
  boundsOfCells,
  computeChainOrderMap,
  coordKey,
  hasTwoByTwoBlock,
  isSimpleChain,
  normalizeCells,
  shapeSignature,
} from './polyomino.js';
import { RNG, normalizeSeed, randomSeed } from './rng.js';

const clone = (value) => JSON.parse(JSON.stringify(value));
const isObject = (value) => value !== null && typeof value === 'object' && !Array.isArray(value);

function asNum(value, fallback = 0) {
  const n = Number(value);
  return Number.isFinite(n) ? n : fallback;
}

function asOptionalNum(value) {
  if (value === null || value === undefined || value === '') return null;
  const n = Number(value);
  return Number.isFinite(n) ? Math.max(0, Math.round(n * 100) / 100) : null;
}

/* ------------------------------------------------------------------ *
 * 全局格子类型配置
 * ------------------------------------------------------------------ */

export function defaultCellTypes() {
  return clone(DEFAULT_CELL_TYPES);
}

export function normalizeCellType(raw, fallbackIndex = 0) {
  const source = isObject(raw) ? raw : {};
  const id = String(source.id || `cell_${fallbackIndex + 1}`).trim();
  const builtinDef = DEFAULT_CELL_TYPES.find((item) => item.id === id);
  return {
    id,
    name: String(source.name || builtinDef?.name || id).trim() || id,
    short: String(source.short || builtinDef?.short || String(source.name || id)).trim() || '格',
    icon: [...String(source.icon ?? builtinDef?.icon ?? '■')].slice(0, 2).join('') || '■',
    color: isHexColor(source.color) ? source.color.toLowerCase() : (builtinDef?.color || '#7d9fd1'),
    resource: Math.max(0, Math.round(asNum(source.resource, builtinDef?.resource ?? 0) * 100) / 100),
    difficulty: Math.max(0, Math.round(asNum(source.difficulty, builtinDef?.difficulty ?? 0) * 100) / 100),
    supportsOffset: typeof source.supportsOffset === 'boolean'
      ? source.supportsOffset
      : Boolean(builtinDef?.supportsOffset),
    defaultOffset: clamp(Math.round(asNum(source.defaultOffset, builtinDef?.defaultOffset ?? 0)), -10, 10),
    builtin: Boolean(builtinDef),
  };
}

export function normalizeCellTypes(rawList) {
  const input = Array.isArray(rawList) && rawList.length ? rawList : defaultCellTypes();
  const seen = new Set();
  const result = [];
  input.forEach((item, index) => {
    const normalized = normalizeCellType(item, index);
    if (!normalized.id || seen.has(normalized.id)) return;
    seen.add(normalized.id);
    result.push(normalized);
  });
  if (!seen.has('empty')) {
    result.unshift(normalizeCellType(DEFAULT_CELL_TYPES[0], 0));
  }
  // 平滑迁移旧版默认图标（普通战斗 ⚔ -> 🗡 单剑，精英 ★ -> ⚔ 双剑）
  const combat = result.find((ct) => ct.id === 'combat');
  const elite = result.find((ct) => ct.id === 'elite');
  if (combat && elite && combat.icon === '⚔' && elite.icon === '★') {
    combat.icon = '🗡';
    elite.icon = '⚔';
  }
  return result;
}

export function cellTypeMap(cellTypes) {
  const list = Array.isArray(cellTypes) && cellTypes.length ? cellTypes : DEFAULT_CELL_TYPES;
  return new Map(list.map((item) => [item.id, item]));
}

/* ------------------------------------------------------------------ *
 * 条带模板规范化、指标求和与校验
 * ------------------------------------------------------------------ */

/**
 * 规范化单格「模糊类型池」。
 * 池内类型必须是已注册格子类型；spawn 是结构标记，禁止进入随机池。
 * 同一类型重复出现时合并权重，保持首次出现顺序。
 */
export function normalizeCellTypePool(rawPool, typeMap) {
  if (!Array.isArray(rawPool)) return [];
  const merged = new Map();
  for (const raw of rawPool) {
    if (!isObject(raw)) continue;
    const type = String(raw.type || '').trim();
    if (!type || type === 'spawn' || !typeMap.has(type)) continue;
    const weight = Math.max(0.01, Math.round(asNum(raw.weight, 1) * 100) / 100);
    const previous = merged.get(type);
    if (previous) previous.weight = Math.round((previous.weight + weight) * 100) / 100;
    else merged.set(type, { type, weight });
  }
  return [...merged.values()];
}

/** 返回格子的随机类型池；固定类型格返回空数组。 */
export function cellTypePool(cell) {
  return Array.isArray(cell?.typePool) ? cell.typePool : [];
}

/** 将池内权重归一化后的期望值用于预算估算。 */
export function expectedCellMetric(cell, typeMap, key) {
  const pool = cellTypePool(cell);
  if (!pool.length) return Number(typeMap.get(cell?.type)?.[key] || 0);
  const total = pool.reduce((sum, entry) => sum + Math.max(0, Number(entry.weight) || 0), 0);
  if (total <= 0) return 0;
  let value = 0;
  for (const entry of pool) {
    const def = typeMap.get(entry.type);
    if (!def) continue;
    value += (Number(def[key]) || 0) * (Math.max(0, Number(entry.weight) || 0) / total);
  }
  return value;
}

export function normalizeStripCell(raw, typeMap) {
  const source = isObject(raw) ? raw : {};
  const rawType = String(source.type || 'empty').trim();
  let typeDef = typeMap.get(rawType) || typeMap.get('empty') || DEFAULT_CELL_TYPES[0];
  const pool = typeDef.id === 'spawn' ? [] : normalizeCellTypePool(source.typePool, typeMap);
  const primary = pool.find((entry) => entry.type === typeDef.id) || pool[0];
  if (primary) typeDef = typeMap.get(primary.type) || typeDef;
  const supportsOffset = typeDef.supportsOffset || pool.some((entry) => typeMap.get(entry.type)?.supportsOffset);
  const offset = supportsOffset
    ? clamp(Math.round(asNum(source.difficultyOffset, typeDef.defaultOffset ?? 0)), -10, 10)
    : 0;
  return {
    rx: Math.round(asNum(source.rx ?? source.x, 0)),
    ry: Math.round(asNum(source.ry ?? source.y, 0)),
    type: typeDef.id,
    difficultyOffset: offset,
    isPortal: Boolean(source.isPortal),
    ...(pool.length >= 2 ? { typePool: pool } : {}),
    ...(Object.hasOwn(source, 'openDirs') ? { openDirs: source.openDirs } : {}),
    ...(Number.isSafeInteger(source.placedOrder) ? { placedOrder: source.placedOrder } : {}),
    tags: Array.isArray(source.tags)
      ? [...new Set(source.tags.map((t) => String(t).trim()).filter(Boolean))]
      : [],
  };
}

function applyTemplateOpenEdges(cells, edges) {
  const result = cells.map((cell) => ({ ...cell, openDirs: [] }));
  const byPosition = new Map(result.map((cell) => [coordKey(cell.rx, cell.ry), cell]));
  const invalid = () => {
    if (result.length) result[0].openDirs = null;
    return result;
  };
  if (!Array.isArray(edges)) return invalid();
  for (const edge of edges) {
    if (!edge || !Number.isInteger(edge.x) || !Number.isInteger(edge.y) || !['E', 'S'].includes(edge.dir)) return invalid();
    const first = byPosition.get(coordKey(edge.x, edge.y));
    const second = byPosition.get(coordKey(edge.x + Number(edge.dir === 'E'), edge.y + Number(edge.dir === 'S')));
    if (!first || !second || first.openDirs.includes(edge.dir)) return invalid();
    first.openDirs.push(edge.dir);
    second.openDirs.push(edge.dir === 'E' ? 'W' : 'N');
  }
  for (const cell of result) cell.openDirs.sort();
  return result;
}

export function templateToFile(template) {
  const cells = template.cells;
  if (!validateOpenDirs(cells)) throw Error(`模板「${template.name}」包含无效开放边。`);
  const explicit = explicitOpenCells(cells);
  return {
    ...template,
    cells: cells.map(({ openDirs, ...cell }) => cell),
    openEdges: explicit.flatMap((cell) => cell.openDirs.filter((dir) => dir === 'E' || dir === 'S')
      .map((dir) => ({ x: cell.rx, y: cell.ry, dir }))),
  };
}

export function normalizeTemplateGroup(raw, index = 0) {
  const source = isObject(raw) ? raw : {};
  const name = String(source.name || `文件夹 ${index + 1}`).trim() || `文件夹 ${index + 1}`;
  return {
    id: String(source.id || `template_group_${index + 1}`).trim(),
    name,
    parentGroupId: typeof source.parentGroupId === 'string' ? source.parentGroupId.trim() : '',
    order: Math.max(0, Math.round(asNum(source.order, index))),
  };
}

export function normalizeTemplateGroups(rawGroups) {
  const seen = new Set();
  return (Array.isArray(rawGroups) ? rawGroups : [])
    .filter(isObject)
    .map((group, index) => normalizeTemplateGroup(group, index))
    .filter((group) => {
      if (!group.id || seen.has(group.id)) return false;
      seen.add(group.id);
      return true;
    });
}

export function normalizeStripTemplate(raw, cellTypes = DEFAULT_CELL_TYPES, index = 0) {
  const source = isObject(raw) ? raw : {};
  const tMap = cellTypeMap(cellTypes);
  const rawCells = Array.isArray(source.cells) && source.cells.length
    ? source.cells
    : [
        { rx: 0, ry: 0, type: 'empty', difficultyOffset: 0, isPortal: false },
        { rx: 1, ry: 0, type: 'combat', difficultyOffset: 0, isPortal: false },
        { rx: 2, ry: 0, type: 'empty', difficultyOffset: 0, isPortal: false },
        { rx: 3, ry: 0, type: 'reward', difficultyOffset: 0, isPortal: false },
        { rx: 4, ry: 0, type: 'empty', difficultyOffset: 0, isPortal: false },
      ];
  const normalized = normalizeCells(rawCells.map((c) => normalizeStripCell(c, tMap)));
  const migrated = Object.hasOwn(source, 'openEdges')
    ? applyTemplateOpenEdges(normalized, source.openEdges)
    : normalized.every((c) => c.openDirs === undefined) ? explicitOpenCells(normalized) : normalized;
  const chainOrder = computeChainOrderMap(migrated);
  const cells = migrated.map((c, i) => ({ ...c, placedOrder: c.placedOrder ?? chainOrder.get(i)?.chainIndex ?? i }));
  const metricsRaw = isObject(source.metrics) ? source.metrics : {};
  const genRaw = isObject(source.generation) ? source.generation : {};
  const role = STRIP_ROLE_IDS.includes(source.role) ? source.role : 'normal';
  const hasSpawn = cells.some((c) => c.type === 'spawn');
  const spawnEligible = role !== 'bridge' && (typeof source.spawnEligible === 'boolean' ? source.spawnEligible : hasSpawn);
  const noLoop = role === 'normal' && !spawnEligible && Boolean(source.noLoop);

  return {
    id: String(source.id || `tpl_${index + 1}`).trim(),
    ...(Object.hasOwn(source, 'parentTemplateId') ? { parentTemplateId: source.parentTemplateId } : {}),
    ...(Object.hasOwn(source, 'visualTypeId') ? { visualTypeId: source.visualTypeId } : {}),
    ...(typeof source.folderId === 'string' && source.folderId.trim() ? { folderId: source.folderId.trim() } : {}),
    ...(source.uiOrder !== null && source.uiOrder !== '' && Number.isFinite(Number(source.uiOrder)) ? { uiOrder: Math.round(Number(source.uiOrder)) } : {}),
    name: String(source.name || `条带模板 ${index + 1}`).trim() || `条带模板 ${index + 1}`,
    description: String(source.description || ''),
    role,
    noLoop,
    spawnEligible,
    regionIds: Array.isArray(source.regionIds)
      ? [...new Set(source.regionIds.map((r) => String(r).trim()).filter(Boolean))]
      : [],
    allowRotate: source.allowRotate !== false,
    cells,
    metrics: {
      resourceOverride: asOptionalNum(metricsRaw.resourceOverride),
      difficultyOverride: asOptionalNum(metricsRaw.difficultyOverride),
    },
    generation: {
      weight: Math.max(0.01, asNum(genRaw.weight, 1)),
      max: clamp(Math.round(asNum(genRaw.max, 6)), 0, 99),
      unique: Boolean(genRaw.unique),
    },
    tags: Array.isArray(source.tags)
      ? [...new Set(source.tags.map((t) => String(t).trim()).filter(Boolean))]
      : [],
  };
}

// Family members are complete records. Compare only the fields that affect placement.
export function stripTemplateStructureKey(template) {
  return JSON.stringify([
    template.role, template.noLoop, template.spawnEligible, template.allowRotate,
    [...template.regionIds].sort(),
    [template.generation.weight, template.generation.max, template.generation.unique],
    [...template.cells].sort((a, b) => a.ry - b.ry || a.rx - b.rx).map((cell) => [
      cell.rx, cell.ry, [...(cell.openDirs || [])].sort(), cell.placedOrder,
      cell.isPortal, cell.type === 'spawn',
    ]),
  ]);
}

export function validateTemplateFamilies(templates) {
  const byId = new Map(templates.map((template) => [template.id, template]));
  const duplicateIds = new Set();
  const seen = new Set();
  for (const template of templates) {
    if (seen.has(template.id)) duplicateIds.add(template.id);
    seen.add(template.id);
  }
  const issues = [];
  templates.forEach((template, index) => {
    const add = (code, message, field = 'parentTemplateId') => issues.push({
      level: 'error', code: `template.family.${code}`, message: `模板「${template.name}」${message}`,
      path: `dungeon.stripTemplates[${index}].${field}`,
    });
    if (Object.hasOwn(template, 'visualTypeId') && (typeof template.visualTypeId !== 'string' || !template.visualTypeId.trim())) {
      add('visualType', '的 visualTypeId 必须是非空字符串。', 'visualTypeId');
    }
    if (!Object.hasOwn(template, 'parentTemplateId')) return;
    const { parentTemplateId } = template;
    if (typeof parentTemplateId !== 'string' || !parentTemplateId || parentTemplateId.trim() !== parentTemplateId) {
      add('parentId', '的 parentTemplateId 必须是非空模板 ID。');
      return;
    }
    if (duplicateIds.has(parentTemplateId) || duplicateIds.has(template.id)) {
      add('ambiguous', '的家族 ID 重复，无法判定父子关系。');
      return;
    }
    const parent = byId.get(parentTemplateId);
    if (!parent) add('missing', `的父模板「${parentTemplateId}」不存在。`);
    else if (Object.hasOwn(parent, 'parentTemplateId')) add('nested', '必须直接引用根模板，不能引用自己或其他子模板。');
    else if (Object.hasOwn(template, 'visualTypeId') && template.visualTypeId !== (parent.visualTypeId ?? parent.id)) {
      add('visualType', '的 visualTypeId 必须继承根模板。', 'visualTypeId');
    } else if (!validateOpenDirs(parent.cells) || !validateOpenDirs(template.cells)
      || stripTemplateStructureKey(parent) !== stripTemplateStructureKey(template)) {
      add('structure', `与父模板「${parent.name}」的锁定结构或生成配置不一致。`);
    }
  });
  return issues;
}

export function createChildStripTemplate(source, name) {
  const child = clone(source);
  child.parentTemplateId = source.parentTemplateId ?? source.id;
  child.name = name || `${source.name} 子模板`;
  // A variant starts from its actual cell totals, not from the source's manual overrides.
  child.metrics = { resourceOverride: null, difficultyOverride: null };
  return child;
}

/**
 * 计算条带模板的资源值与难度值：
 * - 自动值为所有格子对应全局类型数值之和；
 * - 若存在手动覆写，则生效值为覆写值；
 * - 战斗格 difficultyOffset 不影响条带总难度。
 */
export function computeTemplateMetrics(template, cellTypes = DEFAULT_CELL_TYPES) {
  const tMap = cellTypeMap(cellTypes);
  const cells = Array.isArray(template?.cells) ? template.cells : [];
  let autoResource = 0;
  let autoDifficulty = 0;
  let portalCount = 0;
  for (const cell of cells) {
    if (cell.isPortal) portalCount += 1;
    autoResource += expectedCellMetric(cell, tMap, 'resource');
    autoDifficulty += expectedCellMetric(cell, tMap, 'difficulty');
  }
  autoResource = Math.round(autoResource * 100) / 100;
  autoDifficulty = Math.round(autoDifficulty * 100) / 100;

  const resourceOverride = asOptionalNum(template?.metrics?.resourceOverride);
  const difficultyOverride = asOptionalNum(template?.metrics?.difficultyOverride);

  return {
    autoResource,
    autoDifficulty,
    resource: resourceOverride !== null ? resourceOverride : autoResource,
    difficulty: difficultyOverride !== null ? difficultyOverride : autoDifficulty,
    isResourceOverridden: resourceOverride !== null,
    isDifficultyOverridden: difficultyOverride !== null,
    cellCount: cells.length,
    portalCount,
    isChain: isOpenChain(cells),
    hasRectBlock: hasTwoByTwoBlock(cells),
    shapeSignature: shapeSignature(cells),
    shapeKind: analyzeShapeKind(cells),
  };
}

export function templateMatchesRegion(template, regionId) {
  if (!template) return false;
  if (!Array.isArray(template.regionIds) || template.regionIds.length === 0) return true;
  return template.regionIds.includes(regionId);
}

export function validateStripTemplate(template, cellTypes = DEFAULT_CELL_TYPES, validRegionIds = null) {
  const issues = [];
  const tMap = cellTypeMap(cellTypes);
  const err = (code, message) => issues.push({ level: 'error', code, message });
  const warn = (code, message) => issues.push({ level: 'warning', code, message });

  if (!isObject(template)) {
    err('template.invalid', '条带模板不是有效对象。');
    return { ok: false, issues, errors: issues, warnings: [] };
  }
  if (!template.id) err('template.id', '条带模板缺少 id。');
  if (!template.name?.trim()) err('template.name', '条带模板缺少名称。');

  const cells = Array.isArray(template.cells) ? template.cells : [];
  if (cells.length < STRIP_CELLS_MIN || cells.length > STRIP_CELLS_MAX) {
    err('template.cells.count', `条带格数需在 ${STRIP_CELLS_MIN}–${STRIP_CELLS_MAX} 之间。`);
  }
  if (!validateOpenDirs(cells)) {
    err('template.edges.invalid', '内部开放边必须成对、且只能连接相邻格。');
  } else if (cells.length && !hasConnectedOpenEdges(cells)) {
    err('template.cells.disconnected', `条带「${template.name}」的开放边没有连通全部格子。`);
  } else if (template.role === 'bridge' && !isOpenChain(cells)) {
    err('template.bridge.chain', '跨区桥的内部通路必须是有首尾的单链。');
  }

  const coordSeen = new Set();
  let portalCount = 0;
  for (const cell of cells) {
    if (cell.isPortal) portalCount += 1;
    const key = coordKey(cell.rx, cell.ry);
    if (coordSeen.has(key)) {
      err('template.cells.duplicate', `条带「${template.name}」存在重复坐标 (${key})。`);
    }
    coordSeen.add(key);
    if (!tMap.has(cell.type)) {
      warn('template.cell.type', `条带「${template.name}」引用了未知格子类型「${cell.type}」。`);
    }
    const rawPool = Array.isArray(cell.typePool) ? cell.typePool : [];
    if (cell.type === 'spawn' && rawPool.length) {
      err('template.cells.spawnPool', `条带「${template.name}」的出生格不能设置随机类型池。`);
    }
    if (rawPool.length) {
      if (rawPool.length < 2) {
        warn('template.cells.poolSmall', `条带「${template.name}」的 (${cell.rx},${cell.ry}) 随机池少于 2 项，将按固定类型处理。`);
      }
      for (const entry of rawPool) {
        if (!isObject(entry)) {
          err('template.cells.poolEntry', `条带「${template.name}」的随机池包含无效项。`);
          continue;
        }
        if (entry.type === 'spawn') {
          err('template.cells.spawnPool', `条带「${template.name}」的随机池不能包含出生格类型。`);
        } else if (!tMap.has(entry.type)) {
          warn('template.cells.poolType', `条带「${template.name}」的随机池引用了未知类型「${entry.type}」。`);
        }
        const weight = Number(entry.weight);
        if (!Number.isFinite(weight) || weight <= 0) {
          err('template.cells.poolWeight', `条带「${template.name}」的随机池权重必须大于 0。`);
        }
      }
    }
  }
  if (validRegionIds instanceof Set && Array.isArray(template.regionIds)) {
    for (const rId of template.regionIds) {
      if (!validRegionIds.has(rId)) {
        warn('template.region.missing', `条带「${template.name}」限定了不存在的区域「${rId}」。`);
      }
    }
  }
  if (template.role === 'bridge' && template.spawnEligible) {
    warn('template.bridge.spawn', `桥接条带「${template.name}」不建议同时勾选为出生点候选。`);
  }
  if (template.role === 'bridge' && template.noLoop) {
    err('template.bridge.noLoop', `桥接条带「${template.name}」不能标记为非环。`);
  }
  if (template.role === 'bridge' && portalCount === 1) {
    warn('template.bridge.onePortal', `桥接条带「${template.name}」只设置了 1 个出入口格，建议设 0 个或至少 2 个（首尾各一）。`);
  }

  const errors = issues.filter((i) => i.level === 'error');
  const warnings = issues.filter((i) => i.level === 'warning');
  return { ok: errors.length === 0, issues, errors, warnings };
}

/* ------------------------------------------------------------------ *
 * 随机条带模板草稿生成器（4–6 格无矩形单链，供编辑器随机生成后精修）
 * ------------------------------------------------------------------ */

export function defaultRandomDraftConfig(cellTypes = DEFAULT_CELL_TYPES) {
  const defaultWeights = {
    empty: 24,
    combat: 34,
    elite: 10,
    shop: 6,
    reward: 12,
    event: 10,
    rest: 4,
    spawn: 0,
  };
  const weights = {};
  for (const ct of cellTypes) {
    weights[ct.id] = defaultWeights[ct.id] ?? 5;
  }
  return {
    minCells: 4,
    maxCells: 6,
    shapeStyle: 'mixed',
    role: 'normal',
    noLoop: false,
    portalMode: 'none', // 'none' | 'ends' | 'single_end'
    spawnEligible: false,
    allowRotate: true,
    regionIds: [],
    offsetMin: -1,
    offsetMax: 2,
    maxSpecialPerStrip: 1,
    weights,
  };
}

function generateLineShape(count) {
  const cells = [];
  for (let i = 0; i < count; i += 1) {
    cells.push({ rx: i, ry: 0 });
  }
  return cells;
}

/** 严格生成无 2×2 实心块、无分叉、明确首尾的弯折单链。 */
function generatePolylineChain(count, rng) {
  if (count <= 2) return generateLineShape(count);
  for (let attempt = 0; attempt < 40; attempt += 1) {
    const coords = [{ rx: 0, ry: 0 }];
    const occupied = new Set([coordKey(0, 0)]);
    let lastDir = null;
    let turned = false;

    while (coords.length < count) {
      const tip = coords[coords.length - 1];
      const dirs = rng.shuffle([...DIR_IDS]);
      // 尽量保证折线至少拐 1 次弯，且不连成直线
      dirs.sort((a, b) => {
        const aTurn = lastDir && a !== lastDir ? 0 : 1;
        const bTurn = lastDir && b !== lastDir ? 0 : 1;
        if (!turned) return aTurn - bTurn;
        return 0;
      });
      let picked = null;
      for (const dirId of dirs) {
        const d = DIRECTIONS[dirId];
        const nx = tip.rx + d.dx;
        const ny = tip.ry + d.dy;
        const k = coordKey(nx, ny);
        if (occupied.has(k)) continue;
        // 严格单链判定：新格的四邻域中只能有当前 tip 这 1 个已占格！
        let adjCount = 0;
        for (const checkDir of DIR_IDS) {
          const cd = DIRECTIONS[checkDir];
          if (occupied.has(coordKey(nx + cd.dx, ny + cd.dy))) adjCount += 1;
        }
        if (adjCount !== 1) continue;
        picked = { rx: nx, ry: ny, dir: dirId };
        break;
      }
      if (!picked) break;
      if (lastDir && picked.dir !== lastDir) turned = true;
      lastDir = picked.dir;
      occupied.add(coordKey(picked.rx, picked.ry));
      coords.push({ rx: picked.rx, ry: picked.ry });
    }

    if (coords.length === count) {
      const norm = normalizeCells(coords);
      const b = boundsOfCells(norm);
      if (b.width > 1 && b.height > 1 && isSimpleChain(norm)) return norm;
    }
  }
  // 兜底：标准 L 型单链
  const leg1 = Math.max(2, Math.ceil(count / 2));
  const cells = [];
  for (let i = 0; i < leg1; i += 1) cells.push({ rx: i, ry: 0 });
  for (let j = 1; j <= count - leg1; j += 1) cells.push({ rx: leg1 - 1, ry: j });
  return normalizeCells(cells);
}

function pickCellCountWeighted(minCells, maxCells, rng) {
  if (minCells === maxCells) return minCells;
  const candidates = [];
  for (let n = minCells; n <= maxCells; n += 1) {
    // 4 格权重偏低，5~6 格权重更高，贴合「4-6 格且 4 格相对较少」
    const w = n === 4 ? 20 : n === 5 ? 45 : n === 6 ? 35 : 22;
    candidates.push({ n, w });
  }
  return rng.weighted(candidates, (c) => c.w).n;
}

const NAME_PREFIXES = {
  normal: {
    line: ['霓虹', '重轨', '荒野', '星穹', '长距', '幽暗'],
    polyline: ['转角', '折影', '曲巷', '伏击', '迂回', '暗渠'],
    single: ['孤室', '节点'],
  },
  bridge: {
    line: ['气闸', '界门', '断轨', '守关'],
    polyline: ['高架', '跨区', '险关', '折桥'],
    single: ['界门'],
  },
};

const NAME_SUFFIXES = {
  combat: '战廊',
  elite: '精英阵',
  shop: '集市',
  reward: '密库',
  event: '秘道',
  rest: '营地',
  spawn: '启程廊',
  empty: '通道',
};

/**
 * 按随机配置生成单条无矩形单链草稿，供用户在 2D 编辑器中预览、精修后保存。
 */
export function generateRandomStripDraft(rawConfig = {}, cellTypes = DEFAULT_CELL_TYPES, seed = null) {
  const tList = normalizeCellTypes(cellTypes);
  const tMap = cellTypeMap(tList);
  const defaults = defaultRandomDraftConfig(tList);
  const config = {
    ...defaults,
    ...rawConfig,
    weights: { ...defaults.weights, ...(rawConfig.weights || {}) },
  };

  const usedSeed = normalizeSeed(seed || randomSeed());
  const rng = new RNG(usedSeed);

  const minCells = clamp(Math.round(asNum(config.minCells, 4)), STRIP_CELLS_MIN, STRIP_CELLS_MAX);
  const maxCells = clamp(Math.round(asNum(config.maxCells, 6)), minCells, STRIP_CELLS_MAX);
  const targetCount = pickCellCountWeighted(minCells, maxCells, rng);

  let style = SHAPE_STYLE_IDS.includes(config.shapeStyle) ? config.shapeStyle : 'mixed';
  if (style === 'mixed') {
    style = rng.weighted(
      [
        { id: 'polyline', w: 68 },
        { id: 'line', w: 32 },
      ],
      (x) => x.w,
    ).id;
  }

  const coords = normalizeCells(
    style === 'line' ? generateLineShape(targetCount) : generatePolylineChain(targetCount, rng),
  );

  const role = STRIP_ROLE_IDS.includes(config.role) ? config.role : 'normal';
  const noLoop = role === 'normal' && !config.spawnEligible && Boolean(config.noLoop);
  const offsetMin = clamp(Math.round(asNum(config.offsetMin, -1)), -10, 10);
  const offsetMax = clamp(Math.round(asNum(config.offsetMax, 2)), offsetMin, 10);
  const maxSpecial = Math.max(0, Math.round(asNum(config.maxSpecialPerStrip, 1)));
  const limitedTypes = new Set(['shop', 'elite', 'rest']);
  const typeCounts = new Map();

  const pickCellType = (excludeEmpty = false) => {
    const pool = tList.filter((ct) => {
      if (ct.id === 'spawn') return false;
      if (excludeEmpty && ct.id === 'empty') return false;
      const w = Math.max(0, asNum(config.weights[ct.id], 0));
      if (w <= 0) return false;
      if (limitedTypes.has(ct.id) && (typeCounts.get(ct.id) || 0) >= maxSpecial) return false;
      return true;
    });
    if (!pool.length) return tMap.get('empty') || tList[0];
    return rng.weighted(pool, (ct) => Math.max(0.01, asNum(config.weights[ct.id], 1)));
  };

  const cells = coords.map((pt) => {
    const ct = pickCellType(false);
    typeCounts.set(ct.id, (typeCounts.get(ct.id) || 0) + 1);
    let difficultyOffset = 0;
    if (ct.supportsOffset) {
      const base = ct.defaultOffset || 0;
      const jitter = rng.int(offsetMin, offsetMax);
      difficultyOffset = clamp(base + (ct.id === 'elite' ? Math.max(0, jitter) : jitter), -10, 10);
    }
    return {
      rx: pt.rx,
      ry: pt.ry,
      type: ct.id,
      difficultyOffset,
      isPortal: false,
      tags: [],
    };
  });

  if (cells.length > 0 && cells.every((c) => c.type === 'empty')) {
    const nonEmpty = pickCellType(true);
    if (nonEmpty && nonEmpty.id !== 'empty') {
      const idx = rng.int(0, cells.length - 1);
      cells[idx].type = nonEmpty.id;
      if (nonEmpty.supportsOffset) {
        cells[idx].difficultyOffset = clamp(nonEmpty.defaultOffset + rng.int(offsetMin, offsetMax), -10, 10);
      }
    }
  }

  const orderMap = computeChainOrderMap(cells);
  if (config.spawnEligible && cells.length > 0 && role !== 'bridge') {
    const headIdx = [...orderMap.entries()].find(([, v]) => v.isHead)?.[0] ?? 0;
    cells[headIdx].type = 'spawn';
    cells[headIdx].difficultyOffset = 0;
  }

  // 出入口格配置（默认 none，大多数条带不设出入口格）
  const portalMode = config.portalMode || 'none';
  if (portalMode === 'ends') {
    for (const [idx, info] of orderMap) {
      if (info.isHead || info.isTail) cells[idx].isPortal = true;
    }
  } else if (portalMode === 'single_end') {
    const headIdx = [...orderMap.entries()].find(([, v]) => v.isHead)?.[0] ?? 0;
    cells[headIdx].isPortal = true;
  }

  const shapeKind = analyzeShapeKind(cells);
  const dominant =
    cells.find((c) => c.type === 'elite')?.type ||
    cells.find((c) => c.type === 'shop')?.type ||
    cells.find((c) => c.type === 'reward')?.type ||
    cells.find((c) => c.type === 'event')?.type ||
    cells.find((c) => c.type === 'combat')?.type ||
    'empty';
  const prefixes = NAME_PREFIXES[role]?.[shapeKind.id] || ['随机'];
  const prefix = rng.pick(prefixes);
  const suffix = role === 'bridge' ? '桥段' : noLoop ? '尽头' : (NAME_SUFFIXES[dominant] || '廊道');

  return normalizeStripTemplate(
    {
      id: `draft_${usedSeed.toLowerCase().replace(/[^a-z0-9]+/g, '')}`,
      name: `${prefix}${suffix}`,
      description: `随机草稿（种子 ${usedSeed}）`,
      role,
      noLoop,
      spawnEligible: Boolean(config.spawnEligible) && role !== 'bridge',
      regionIds: Array.isArray(config.regionIds) ? [...config.regionIds] : [],
      allowRotate: config.allowRotate !== false,
      cells,
      metrics: {
        resourceOverride: null,
        difficultyOverride: null,
      },
      generation: {
        weight: 1,
        max: role === 'bridge' ? 3 : noLoop ? 2 : 5,
        unique: false,
      },
      tags: [
        shapeKind.id === 'line' ? '直线' : '折线',
        ...(noLoop ? ['非环'] : []),
      ],
    },
    tList,
  );
}

/* ------------------------------------------------------------------ *
 * 内置样例条带模板库（18 条：全部 4–6 格无矩形单链，4 格占少数，含非环与出入口格示范）
 * ------------------------------------------------------------------ */

export function createDefaultStripTemplates(cellTypes = DEFAULT_CELL_TYPES) {
  const raw = [
    // === 出生条带（2 条，均为 5 格明确首尾单链） ===
    {
      id: 'tpl_spawn_street5',
      name: '霓虹启程街',
      role: 'normal',
      noLoop: false,
      spawnEligible: true,
      regionIds: [],
      cells: [
        { rx: 0, ry: 0, type: 'spawn', difficultyOffset: 0, isPortal: false },
        { rx: 1, ry: 0, type: 'empty', difficultyOffset: 0, isPortal: false },
        { rx: 2, ry: 0, type: 'event', difficultyOffset: 0, isPortal: false },
        { rx: 2, ry: 1, type: 'combat', difficultyOffset: -1, isPortal: false },
        { rx: 2, ry: 2, type: 'empty', difficultyOffset: 0, isPortal: false },
      ],
      generation: { weight: 1.2, max: 1, unique: true },
      tags: ['出生', '折线', '5格'],
    },
    {
      id: 'tpl_spawn_concourse5',
      name: '轨道出发廊',
      role: 'normal',
      noLoop: false,
      spawnEligible: true,
      regionIds: [],
      cells: [
        { rx: 0, ry: 0, type: 'spawn', difficultyOffset: 0, isPortal: false },
        { rx: 1, ry: 0, type: 'empty', difficultyOffset: 0, isPortal: false },
        { rx: 2, ry: 0, type: 'combat', difficultyOffset: -1, isPortal: false },
        { rx: 3, ry: 0, type: 'event', difficultyOffset: 0, isPortal: false },
        { rx: 4, ry: 0, type: 'empty', difficultyOffset: 0, isPortal: false },
      ],
      generation: { weight: 1.0, max: 1, unique: true },
      tags: ['出生', '直线', '5格'],
    },

    // === 独立跨区桥接条带（3 条：1 条 4 格 + 2 条 5 格） ===
    {
      id: 'tpl_bridge_airlock4',
      name: '失压气闸桥',
      role: 'bridge',
      noLoop: false,
      spawnEligible: false,
      regionIds: [],
      cells: [
        { rx: 0, ry: 0, type: 'empty', difficultyOffset: 0, isPortal: true },
        { rx: 1, ry: 0, type: 'combat', difficultyOffset: 1, isPortal: false },
        { rx: 2, ry: 0, type: 'event', difficultyOffset: 0, isPortal: false },
        { rx: 3, ry: 0, type: 'empty', difficultyOffset: 0, isPortal: true },
      ],
      generation: { weight: 0.9, max: 2, unique: false },
      tags: ['桥接', '直线', '4格', '双口'],
    },
    {
      id: 'tpl_bridge_overpass5',
      name: '跨区高架桥',
      role: 'bridge',
      noLoop: false,
      spawnEligible: false,
      regionIds: [],
      cells: [
        { rx: 0, ry: 0, type: 'empty', difficultyOffset: 0, isPortal: true },
        { rx: 1, ry: 0, type: 'combat', difficultyOffset: 1, isPortal: false },
        { rx: 1, ry: 1, type: 'elite', difficultyOffset: 2, isPortal: false },
        { rx: 2, ry: 1, type: 'reward', difficultyOffset: 0, isPortal: false },
        { rx: 3, ry: 1, type: 'empty', difficultyOffset: 0, isPortal: true },
      ],
      generation: { weight: 1.2, max: 2, unique: false },
      tags: ['桥接', '折线', '5格', '双口'],
    },
    {
      id: 'tpl_bridge_cargo5',
      name: '重轨货运桥',
      role: 'bridge',
      noLoop: false,
      spawnEligible: false,
      regionIds: [],
      cells: [
        { rx: 0, ry: 0, type: 'empty', difficultyOffset: 0, isPortal: false },
        { rx: 1, ry: 0, type: 'combat', difficultyOffset: 0, isPortal: false },
        { rx: 2, ry: 0, type: 'empty', difficultyOffset: 0, isPortal: false },
        { rx: 3, ry: 0, type: 'combat', difficultyOffset: 1, isPortal: false },
        { rx: 4, ry: 0, type: 'empty', difficultyOffset: 0, isPortal: false },
      ],
      generation: { weight: 1.1, max: 3, unique: false },
      tags: ['桥接', '直线', '5格'],
    },

    // === 常规条带 · 4 格（少数，仅 1 条常规 + 1 条非环） ===
    {
      id: 'tpl_line4_market',
      name: '中转补给廊',
      role: 'normal',
      noLoop: false,
      regionIds: [],
      cells: [
        { rx: 0, ry: 0, type: 'empty', difficultyOffset: 0, isPortal: false },
        { rx: 1, ry: 0, type: 'shop', difficultyOffset: 0, isPortal: false },
        { rx: 2, ry: 0, type: 'event', difficultyOffset: 0, isPortal: false },
        { rx: 3, ry: 0, type: 'empty', difficultyOffset: 0, isPortal: false },
      ],
      generation: { weight: 0.75, max: 2, unique: false },
      tags: ['直线', '4格', '补给'],
    },
    {
      id: 'tpl_noloop_vault4',
      name: '封存密库',
      role: 'normal',
      noLoop: true,
      regionIds: [],
      cells: [
        { rx: 0, ry: 0, type: 'empty', difficultyOffset: 0, isPortal: true },
        { rx: 1, ry: 0, type: 'combat', difficultyOffset: 1, isPortal: false },
        { rx: 2, ry: 0, type: 'elite', difficultyOffset: 2, isPortal: false },
        { rx: 2, ry: 1, type: 'reward', difficultyOffset: 0, isPortal: false },
      ],
      generation: { weight: 0.85, max: 2, unique: false },
      tags: ['折线', '4格', '非环', '单口'],
    },

    // === 常规条带 · 5 格（主力：含同形不同内容 + 非环示范） ===
    {
      id: 'tpl_line5_patrol',
      name: '警戒直廊',
      role: 'normal',
      noLoop: false,
      regionIds: [],
      cells: [
        { rx: 0, ry: 0, type: 'empty', difficultyOffset: 0, isPortal: false },
        { rx: 1, ry: 0, type: 'combat', difficultyOffset: 0, isPortal: false },
        { rx: 2, ry: 0, type: 'empty', difficultyOffset: 0, isPortal: false },
        { rx: 3, ry: 0, type: 'combat', difficultyOffset: 1, isPortal: false },
        { rx: 4, ry: 0, type: 'reward', difficultyOffset: 0, isPortal: false },
      ],
      generation: { weight: 1.3, max: 5, unique: false },
      tags: ['直线', '5格', '战斗'],
    },
    {
      id: 'tpl_line5_bazaar',
      name: '地下商业道',
      role: 'normal',
      noLoop: false,
      regionIds: [],
      cells: [
        { rx: 0, ry: 0, type: 'empty', difficultyOffset: 0, isPortal: false },
        { rx: 1, ry: 0, type: 'shop', difficultyOffset: 0, isPortal: false },
        { rx: 2, ry: 0, type: 'empty', difficultyOffset: 0, isPortal: false },
        { rx: 3, ry: 0, type: 'rest', difficultyOffset: 0, isPortal: false },
        { rx: 4, ry: 0, type: 'event', difficultyOffset: 0, isPortal: false },
      ],
      generation: { weight: 0.85, max: 2, unique: false },
      tags: ['直线', '5格', '补给'],
    },
    {
      id: 'tpl_l5_ambush',
      name: '拐角伏击巷',
      role: 'normal',
      noLoop: false,
      regionIds: [],
      cells: [
        { rx: 0, ry: 0, type: 'empty', difficultyOffset: 0, isPortal: false },
        { rx: 1, ry: 0, type: 'combat', difficultyOffset: 0, isPortal: false },
        { rx: 2, ry: 0, type: 'combat', difficultyOffset: 1, isPortal: false },
        { rx: 2, ry: 1, type: 'empty', difficultyOffset: 0, isPortal: false },
        { rx: 2, ry: 2, type: 'reward', difficultyOffset: 0, isPortal: false },
      ],
      generation: { weight: 1.3, max: 5, unique: false },
      tags: ['折线', 'L型', '5格'],
    },
    {
      id: 'tpl_l5_camp',
      name: '荒郊休整道',
      role: 'normal',
      noLoop: false,
      regionIds: [],
      cells: [
        { rx: 0, ry: 0, type: 'empty', difficultyOffset: 0, isPortal: false },
        { rx: 1, ry: 0, type: 'event', difficultyOffset: 0, isPortal: false },
        { rx: 2, ry: 0, type: 'empty', difficultyOffset: 0, isPortal: false },
        { rx: 2, ry: 1, type: 'rest', difficultyOffset: 0, isPortal: false },
        { rx: 2, ry: 2, type: 'combat', difficultyOffset: 0, isPortal: false },
      ],
      generation: { weight: 1.0, max: 3, unique: false },
      tags: ['折线', 'L型', '5格', '休整'],
    },
    {
      id: 'tpl_z5_conduit',
      name: '电缆折管廊',
      role: 'normal',
      noLoop: false,
      regionIds: [],
      cells: [
        { rx: 0, ry: 0, type: 'empty', difficultyOffset: 0, isPortal: false },
        { rx: 1, ry: 0, type: 'combat', difficultyOffset: 0, isPortal: false },
        { rx: 1, ry: 1, type: 'event', difficultyOffset: 0, isPortal: false },
        { rx: 2, ry: 1, type: 'combat', difficultyOffset: 1, isPortal: false },
        { rx: 3, ry: 1, type: 'empty', difficultyOffset: 0, isPortal: false },
      ],
      generation: { weight: 1.25, max: 5, unique: false },
      tags: ['折线', 'Z型', '5格'],
    },
    {
      id: 'tpl_noloop_nest5',
      name: '单口精英巢',
      role: 'normal',
      noLoop: true,
      regionIds: [],
      cells: [
        { rx: 0, ry: 0, type: 'empty', difficultyOffset: 0, isPortal: true },
        { rx: 1, ry: 0, type: 'combat', difficultyOffset: 0, isPortal: false },
        { rx: 1, ry: 1, type: 'empty', difficultyOffset: 0, isPortal: false },
        { rx: 1, ry: 2, type: 'elite', difficultyOffset: 2, isPortal: false },
        { rx: 2, ry: 2, type: 'reward', difficultyOffset: 0, isPortal: false },
      ],
      generation: { weight: 0.95, max: 2, unique: false },
      tags: ['折线', '5格', '非环', '单口'],
    },

    // === 常规条带 · 6 格（舒展折线/长廊 + 同形/出入口/非环） ===
    {
      id: 'tpl_z6_assembly',
      name: '流水装配线',
      role: 'normal',
      noLoop: false,
      regionIds: [],
      cells: [
        { rx: 0, ry: 0, type: 'empty', difficultyOffset: 0, isPortal: false },
        { rx: 1, ry: 0, type: 'combat', difficultyOffset: 0, isPortal: false },
        { rx: 2, ry: 0, type: 'empty', difficultyOffset: 0, isPortal: false },
        { rx: 2, ry: 1, type: 'elite', difficultyOffset: 2, isPortal: false },
        { rx: 2, ry: 2, type: 'reward', difficultyOffset: 0, isPortal: false },
        { rx: 3, ry: 2, type: 'empty', difficultyOffset: 0, isPortal: false },
      ],
      generation: { weight: 1.15, max: 4, unique: false },
      tags: ['折线', 'Z型', '6格'],
    },
    {
      id: 'tpl_u6_dock',
      name: '环轨停泊湾',
      role: 'normal',
      noLoop: false,
      regionIds: [],
      cells: [
        { rx: 0, ry: 0, type: 'empty', difficultyOffset: 0, isPortal: false },
        { rx: 0, ry: 1, type: 'combat', difficultyOffset: 0, isPortal: false },
        { rx: 1, ry: 1, type: 'event', difficultyOffset: 0, isPortal: false },
        { rx: 2, ry: 1, type: 'empty', difficultyOffset: 0, isPortal: false },
        { rx: 3, ry: 1, type: 'combat', difficultyOffset: 1, isPortal: false },
        { rx: 3, ry: 0, type: 'reward', difficultyOffset: 0, isPortal: false },
      ],
      generation: { weight: 1.05, max: 4, unique: false },
      tags: ['折线', 'U型', '6格'],
    },
    {
      id: 'tpl_s6_tunnel',
      name: '地下检修渠',
      role: 'normal',
      noLoop: false,
      regionIds: [],
      cells: [
        { rx: 0, ry: 0, type: 'empty', difficultyOffset: 0, isPortal: false },
        { rx: 1, ry: 0, type: 'combat', difficultyOffset: 0, isPortal: false },
        { rx: 1, ry: 1, type: 'empty', difficultyOffset: 0, isPortal: false },
        { rx: 2, ry: 1, type: 'event', difficultyOffset: 0, isPortal: false },
        { rx: 2, ry: 2, type: 'combat', difficultyOffset: 1, isPortal: false },
        { rx: 3, ry: 2, type: 'empty', difficultyOffset: 0, isPortal: false },
      ],
      generation: { weight: 1.15, max: 4, unique: false },
      tags: ['折线', 'S型', '6格'],
    },
    {
      id: 'tpl_l6_gauntlet',
      name: '双闸试炼廊',
      role: 'normal',
      noLoop: false,
      regionIds: [],
      cells: [
        { rx: 0, ry: 0, type: 'empty', difficultyOffset: 0, isPortal: true },
        { rx: 1, ry: 0, type: 'combat', difficultyOffset: 0, isPortal: false },
        { rx: 2, ry: 0, type: 'elite', difficultyOffset: 2, isPortal: false },
        { rx: 3, ry: 0, type: 'reward', difficultyOffset: 0, isPortal: false },
        { rx: 3, ry: 1, type: 'rest', difficultyOffset: 0, isPortal: false },
        { rx: 3, ry: 2, type: 'empty', difficultyOffset: 0, isPortal: true },
      ],
      generation: { weight: 0.9, max: 3, unique: false },
      tags: ['折线', 'L型', '6格', '双口'],
    },
    {
      id: 'tpl_line6_express',
      name: '高速磁浮廊',
      role: 'normal',
      noLoop: false,
      regionIds: [],
      cells: [
        { rx: 0, ry: 0, type: 'empty', difficultyOffset: 0, isPortal: false },
        { rx: 1, ry: 0, type: 'combat', difficultyOffset: 0, isPortal: false },
        { rx: 2, ry: 0, type: 'event', difficultyOffset: 0, isPortal: false },
        { rx: 3, ry: 0, type: 'empty', difficultyOffset: 0, isPortal: false },
        { rx: 4, ry: 0, type: 'combat', difficultyOffset: 1, isPortal: false },
        { rx: 5, ry: 0, type: 'empty', difficultyOffset: 0, isPortal: false },
      ],
      generation: { weight: 1.0, max: 3, unique: false },
      tags: ['直线', '6格'],
    },
    {
      id: 'tpl_noloop_blackmarket6',
      name: '隐秘黑市尽头',
      role: 'normal',
      noLoop: true,
      regionIds: [],
      cells: [
        { rx: 0, ry: 0, type: 'empty', difficultyOffset: 0, isPortal: true },
        { rx: 1, ry: 0, type: 'combat', difficultyOffset: 0, isPortal: false },
        { rx: 2, ry: 0, type: 'event', difficultyOffset: 0, isPortal: false },
        { rx: 3, ry: 0, type: 'empty', difficultyOffset: 0, isPortal: false },
        { rx: 3, ry: 1, type: 'shop', difficultyOffset: 0, isPortal: false },
        { rx: 3, ry: 2, type: 'reward', difficultyOffset: 0, isPortal: false },
      ],
      generation: { weight: 0.85, max: 2, unique: false },
      tags: ['折线', '6格', '非环', '单口', '补给'],
    },
  ];
  return raw.map((item, idx) => normalizeStripTemplate(item, cellTypes, idx));
}

/* ------------------------------------------------------------------ *
 * 模板库 JSON 独立序列化 / 反序列化
 * ------------------------------------------------------------------ */

export function serializeTemplateLibrary({ cellTypes, stripTemplates, templateGroups = [] }, { pretty = true } = {}) {
  const types = normalizeCellTypes(cellTypes);
  const templates = (stripTemplates || []).map((t, i) => normalizeStripTemplate(t, types, i));
  const [issue] = validateTemplateFamilies(templates);
  if (issue) throw Error(issue.message);
  const payload = {
    app: APP_ID,
    kind: 'plotweave-strip-templates',
    schemaVersion: 3,
    exportedAt: new Date().toISOString(),
    cellTypes: types,
    templateGroups: normalizeTemplateGroups(templateGroups),
    stripTemplates: templates.map(templateToFile),
  };
  return JSON.stringify(payload, null, pretty ? 2 : 0);
}

export function parseTemplateLibrary(text, fallbackCellTypes = DEFAULT_CELL_TYPES) {
  let raw;
  try {
    raw = JSON.parse(String(text ?? ''));
  } catch (error) {
    return {
      ok: false,
      cellTypes: null,
      stripTemplates: [],
      errors: [`JSON 解析失败：${error.message}`],
      warnings: [],
    };
  }

  let rawCellTypes = null;
  let rawTemplates = null;
  let rawTemplateGroups = [];
  if (Array.isArray(raw)) {
    rawTemplates = raw;
  } else if (isObject(raw)) {
    if (Array.isArray(raw.stripTemplates)) {
      rawTemplates = raw.stripTemplates;
      rawCellTypes = raw.cellTypes || null;
      rawTemplateGroups = raw.templateGroups || [];
    } else if (isObject(raw.dungeon) && Array.isArray(raw.dungeon.stripTemplates)) {
      rawTemplates = raw.dungeon.stripTemplates;
      rawCellTypes = raw.dungeon.cellTypes || null;
      rawTemplateGroups = raw.dungeon.templateGroups || [];
    } else if (isObject(raw.project?.dungeon) && Array.isArray(raw.project.dungeon.stripTemplates)) {
      rawTemplates = raw.project.dungeon.stripTemplates;
      rawCellTypes = raw.project.dungeon.cellTypes || null;
      rawTemplateGroups = raw.project.dungeon.templateGroups || [];
    }
  }

  if (!Array.isArray(rawTemplates) || rawTemplates.length === 0) {
    return {
      ok: false,
      cellTypes: null,
      stripTemplates: [],
      errors: ['文件中未找到有效的 stripTemplates 条带模板数组。'],
      warnings: [],
    };
  }

  const cellTypes = normalizeCellTypes(rawCellTypes || fallbackCellTypes);
  const errors = [];
  const warnings = [];
  const stripTemplates = rawTemplates.map((item, index) => normalizeStripTemplate(item, cellTypes, index));
  for (const issue of validateTemplateFamilies(stripTemplates)) errors.push(issue.message);
  const reservedIds = new Set(stripTemplates.map((template) => template.id));
  const seenIds = new Set();
  for (const template of stripTemplates) {
    if (seenIds.has(template.id)) {
      let suffix = 2;
      while (reservedIds.has(`${template.id}_${suffix}`)) suffix += 1;
      const id = `${template.id}_${suffix}`;
      warnings.push(`模板 id「${template.id}」重复，已重命名为「${id}」。`);
      template.id = id;
      reservedIds.add(id);
    }
    seenIds.add(template.id);
    const check = validateStripTemplate(template, cellTypes);
    for (const issue of check.errors) errors.push(issue.message);
    for (const issue of check.warnings) warnings.push(issue.message);
  }

  return {
    ok: errors.length === 0,
    cellTypes: rawCellTypes ? cellTypes : null,
    stripTemplates,
    templateGroups: normalizeTemplateGroups(rawTemplateGroups),
    errors,
    warnings,
  };
}
