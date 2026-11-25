/**
 * 工程数据的规范化、校验、迁移与工厂。
 *
 * 这一层不接触 DOM，也不依赖生成器：导入、编辑、测试都可以单独使用。
 * 规范化负责把外部 JSON 收敛成可预测的形状；校验只负责报告问题，
 * 不擅自修改用户数据（除了长度上下限等结构性的 clamp）。
 */

import {
  APP_ID,
  SCHEMA_VERSION,
  PLOT_LENGTH_MIN,
  PLOT_LENGTH_MAX,
  MAX_CONNECTIONS,
  ANCHOR_IDS,
  RARITY_IDS,
  CONTENT_TYPE_IDS,
  CONTENT_TYPES,
  CONTENT_TYPE_MAP,
  REGION_PALETTE,
  SUBROOM_PALETTE,
  GRID_MIN,
  GRID_MAX,
  TARGET_MIN,
  TARGET_MAX,
  clamp,
  isHexColor,
} from './constants.js';
import { createSampleProject } from '../data/sample-project.js';
import { defaultMapPools } from './map-pools.js';
import {
  createDefaultStripTemplates,
  defaultCellTypes,
  normalizeCellTypes,
  normalizeStripTemplate,
} from './strip-templates.js';
import {
  createDefaultFloorProfile,
  normalizeFloorProfile,
} from './floor-generator.js';
import {
  createEmptyWorldConfig,
  normalizeWorldConfig,
} from './world-placeholder.js';

const isObject = (value) => value !== null && typeof value === 'object' && !Array.isArray(value);
const clone = (value) => JSON.parse(JSON.stringify(value));
const asArray = (value) => (Array.isArray(value) ? value : []);
const asString = (value, fallback = '') => (typeof value === 'string' ? value : fallback);
const asNumber = (value, fallback = 0) => {
  const number = Number(value);
  return Number.isFinite(number) ? number : fallback;
};
const asBoolean = (value, fallback = false) => (typeof value === 'boolean' ? value : fallback);
const cleanTags = (value) =>
  [...new Set(asArray(value).map((tag) => String(tag).trim()).filter(Boolean))];
const cleanStrings = (value) =>
  [...new Set(asArray(value).map((item) => String(item).trim()).filter(Boolean))];

/* ------------------------------------------------------------------ *
 * 规范化
 * ------------------------------------------------------------------ */

export function normalizeContents(raw) {
  const source = isObject(raw) ? raw : {};
  return {
    props: cleanStrings(source.props),
    relics: cleanStrings(source.relics),
    events: cleanStrings(source.events),
    enemies: cleanStrings(source.enemies),
    npcs: cleanStrings(source.npcs),
  };
}

export function normalizeRegion(raw, index = 0) {
  const source = isObject(raw) ? raw : {};
  const name = asString(source.name, `区域 ${index + 1}`);
  const color = isHexColor(source.color)
    ? source.color.toLowerCase()
    : REGION_PALETTE[index % REGION_PALETTE.length];
  const generation = isObject(source.generation) ? source.generation : {};
  return {
    ...source,
    id: asString(source.id),
    name,
    shortName: asString(source.shortName, name),
    color,
    description: asString(source.description),
    tags: cleanTags(source.tags),
    hidden: asBoolean(source.hidden),
    generation: {
      min: Math.max(0, Math.round(asNumber(generation.min, 0))),
      max: Math.max(0, Math.round(asNumber(generation.max, 6))),
      weight: Math.max(0, asNumber(generation.weight, 1)),
      order: Math.round(asNumber(generation.order, index)),
    },
  };
}

export function normalizePlotGroup(raw, index = 0) {
  const source = isObject(raw) ? raw : {};
  const name = asString(source.name, `折叠组 ${index + 1}`);
  return {
    ...source,
    id: asString(source.id),
    name,
    regionId: asString(source.regionId),
    parentGroupId: asString(source.parentGroupId),
    description: asString(source.description),
    order: Math.round(asNumber(source.order, index)),
  };
}

export function normalizePlotKind(raw) {
  const source = isObject(raw) ? raw : {};
  const lengthRaw = isObject(source.length) ? source.length : {};
  const minLength = clamp(
    Math.round(asNumber(lengthRaw.min, PLOT_LENGTH_MIN)),
    PLOT_LENGTH_MIN,
    PLOT_LENGTH_MAX,
  );
  const maxLength = Math.max(
    minLength,
    clamp(Math.round(asNumber(lengthRaw.max, minLength)), PLOT_LENGTH_MIN, PLOT_LENGTH_MAX),
  );
  const connectionRaw = isObject(source.connections) ? source.connections : {};
  const subRoomRaw = isObject(source.subRooms) ? source.subRooms : {};
  const subEnabled = asBoolean(subRoomRaw.enabled, false);
  const subMin = Math.max(0, Math.round(asNumber(subRoomRaw.min, 0)));
  const subMax = Math.max(subMin, Math.round(asNumber(subRoomRaw.max, 1)));
  const generationRaw = isObject(source.generation) ? source.generation : {};
  const visualRaw = isObject(source.visual) ? source.visual : {};

  return {
    ...source,
    id: asString(source.id),
    name: asString(source.name, '未命名地块'),
    shortName: asString(source.shortName, asString(source.name, '地块')),
    regionId: asString(source.regionId),
    groupId: asString(source.groupId),
    description: asString(source.description),
    tags: cleanTags(source.tags),
    length: { min: minLength, max: maxLength },
    connections: {
      max: clamp(Math.round(asNumber(connectionRaw.max, 2)), 0, MAX_CONNECTIONS),
      anchors: ANCHOR_IDS.includes(connectionRaw.anchors) ? connectionRaw.anchors : 'ends',
      allowCrossing: asBoolean(connectionRaw.allowCrossing, true),
      allowedKinds: cleanStrings(connectionRaw.allowedKinds),
      allowedTags: cleanTags(connectionRaw.allowedTags),
    },
    subRooms: {
      enabled: subEnabled,
      min: subEnabled ? subMin : 0,
      max: subEnabled ? subMax : 0,
      pool: cleanStrings(subRoomRaw.pool),
    },
    contents: normalizeContents(source.contents),
    generation: {
      weight: Math.max(0, asNumber(generationRaw.weight, 1)),
      max: Math.max(0, Math.round(asNumber(generationRaw.max, 6))),
      unique: asBoolean(generationRaw.unique, false),
    },
    visual: {
      color: isHexColor(visualRaw.color) ? visualRaw.color.toLowerCase() : '',
      icon: asString(visualRaw.icon, '▭'),
    },
    spawnEligible: asBoolean(source.spawnEligible, false),
  };
}

export function normalizeSubRoomKind(raw) {
  const source = isObject(raw) ? raw : {};
  const generation = isObject(source.generation) ? source.generation : {};
  return {
    ...source,
    id: asString(source.id),
    name: asString(source.name, '未命名子房间'),
    description: asString(source.description),
    color: isHexColor(source.color) ? source.color.toLowerCase() : SUBROOM_PALETTE[0],
    tags: cleanTags(source.tags),
    contents: normalizeContents(source.contents),
    generation: {
      weight: Math.max(0.01, asNumber(generation.weight, 1)),
    },
  };
}

export function normalizeCatalogItem(raw, type) {
  const source = isObject(raw) ? raw : {};
  const meta = CONTENT_TYPE_MAP[type] || CONTENT_TYPES.props;
  return {
    ...source,
    id: asString(source.id),
    name: asString(source.name, `未命名${meta.label}`),
    description: asString(source.description),
    tags: cleanTags(source.tags),
    rarity: RARITY_IDS.includes(source.rarity) ? source.rarity : 'common',
    icon: asString(source.icon, meta.icon),
  };
}

export function normalizeProfile(raw) {
  const source = isObject(raw) ? raw : {};
  return {
    ...source,
    id: asString(source.id),
    name: asString(source.name, '未命名生成方案'),
    description: asString(source.description),
    plotTarget: clamp(Math.round(asNumber(source.plotTarget, 16)), TARGET_MIN, TARGET_MAX),
    startRegionId: asString(source.startRegionId),
    startKindId: asString(source.startKindId),
    regionIds: cleanStrings(source.regionIds),
    allowCrossRegion: asBoolean(source.allowCrossRegion, true),
    crossRegionRatio: clamp(asNumber(source.crossRegionRatio, 0.2), 0, 1),
    maxCrossRegionPerPair: clamp(Math.round(asNumber(source.maxCrossRegionPerPair, 2)), 0, 99),
    allowCrossing: asBoolean(source.allowCrossing, true),
    subRoomDensity: clamp(asNumber(source.subRoomDensity, 1), 0, 2),
    generator: asString(source.generator, 'standard'),
  };
}

function normalizeMeta(raw) {
  const source = isObject(raw) ? raw : {};
  return {
    ...source,
    name: asString(source.name, '未命名工程'),
    author: asString(source.author),
    description: asString(source.description),
    createdAt: asString(source.createdAt, new Date().toISOString()),
    updatedAt: asString(source.updatedAt, new Date().toISOString()),
  };
}

function normalizeSettings(raw) {
  const source = isObject(raw) ? raw : {};
  const grid = isObject(source.grid) ? source.grid : {};
  return {
    ...source,
    grid: {
      width: clamp(Math.round(asNumber(grid.width, 40)), GRID_MIN, GRID_MAX),
      height: clamp(Math.round(asNumber(grid.height, 30)), GRID_MIN, GRID_MAX),
    },
  };
}

export function createDefaultDungeon() {
  const cellTypes = defaultCellTypes();
  return {
    cellTypes,
    stripTemplates: createDefaultStripTemplates(cellTypes),
    floorProfiles: [createDefaultFloorProfile()],
    mapPools: defaultMapPools(),
    world: createEmptyWorldConfig(),
  };
}

export function normalizeDungeon(raw) {
  const source = isObject(raw) ? raw : {};
  const cellTypes = normalizeCellTypes(source.cellTypes);
  const stripTemplates = Array.isArray(source.stripTemplates)
    ? source.stripTemplates.filter(isObject).map((t, i) => normalizeStripTemplate(t, cellTypes, i))
    : createDefaultStripTemplates(cellTypes);
  const floorProfiles =
    Array.isArray(source.floorProfiles) && source.floorProfiles.length > 0
      ? source.floorProfiles.filter(isObject).map(normalizeFloorProfile)
      : [createDefaultFloorProfile()];
  const world = normalizeWorldConfig(source.world);
  return {
    cellTypes,
    stripTemplates,
    floorProfiles,
    mapPools: source.mapPools === undefined ? defaultMapPools(floorProfiles[0]) : clone(source.mapPools),
    world,
  };
}

export function normalizeProject(raw) {
  const source = isObject(raw) ? clone(raw) : {};
  const catalogsRaw = isObject(source.catalogs) ? source.catalogs : {};
  const catalogs = {};
  for (const type of CONTENT_TYPE_IDS) {
    catalogs[type] = asArray(catalogsRaw[type]).filter(isObject).map((item) => normalizeCatalogItem(item, type));
  }

  return {
    ...source,
    app: asString(source.app, APP_ID),
    schemaVersion: Math.max(1, Math.round(asNumber(source.schemaVersion, SCHEMA_VERSION))),
    meta: normalizeMeta(source.meta),
    settings: normalizeSettings(source.settings),
    regions: asArray(source.regions).filter(isObject).map(normalizeRegion),
    plotGroups: asArray(source.plotGroups).filter(isObject).map(normalizePlotGroup),
    plotKinds: asArray(source.plotKinds).filter(isObject).map(normalizePlotKind),
    subRoomKinds: asArray(source.subRoomKinds).filter(isObject).map(normalizeSubRoomKind),
    catalogs,
    profiles: asArray(source.profiles).filter(isObject).map(normalizeProfile),
    dungeon: normalizeDungeon(source.dungeon),
    extensions: isObject(source.extensions) ? source.extensions : {},
  };
}

export function createDefaultProject() {
  return normalizeProject(createSampleProject());
}

export function migrateProject(raw) {
  const from = isObject(raw) ? Math.round(asNumber(raw.schemaVersion, 0)) : 0;
  const project = normalizeProject(raw);
  const migrated = from !== project.schemaVersion;
  if (migrated) {
    project.meta.updatedAt = new Date().toISOString();
  }
  return { project, migrated, from, to: project.schemaVersion };
}

/* ------------------------------------------------------------------ *
 * 工厂与 ID
 * ------------------------------------------------------------------ */

export function uniqueId(prefix, existing) {
  const ids = existing instanceof Set ? existing : new Set(asArray(existing));
  let index = 1;
  while (ids.has(`${prefix}_${index}`)) index += 1;
  return `${prefix}_${index}`;
}

const regionIds = (project) => new Set(project.regions.map((region) => region.id));
const plotKindIds = (project) => new Set(project.plotKinds.map((kind) => kind.id));

export function createRegion(project) {
  const index = project.regions.length;
  const id = uniqueId('region', regionIds(project));
  return {
    id,
    name: `新区域 ${index + 1}`,
    shortName: `区域 ${index + 1}`,
    color: REGION_PALETTE[index % REGION_PALETTE.length],
    description: '',
    tags: [],
    hidden: false,
    generation: { min: 0, max: 6, weight: 1, order: index },
  };
}

export function createPlotGroup(project, regionId = '', name = '', parentGroupId = '') {
  const groups = project.plotGroups || [];
  const id = uniqueId('group', new Set(groups.map((group) => group.id)));
  const fallbackRegion = project.regions[0]?.id || '';
  const targetRegion = regionId || fallbackRegion;
  const parent = groups.find(
    (group) => group.id === parentGroupId && group.regionId === targetRegion,
  );
  const parentKey = parent?.id || '';
  return {
    id,
    name: String(name || '').trim() || '新折叠组',
    regionId: targetRegion,
    parentGroupId: parentKey,
    description: '',
    order: groups
      .filter(
        (group) =>
          group.regionId === targetRegion && (group.parentGroupId || '') === parentKey,
      )
      .reduce((next, group) => Math.max(next, group.order + 1), 0),
  };
}

export function createPlotKind(project, regionId = '', groupId = '') {
  const id = uniqueId('kind', plotKindIds(project));
  const fallbackRegion = project.regions[0]?.id || '';
  const targetRegion = regionId || fallbackRegion;
  const targetGroup = (project.plotGroups || []).find(
    (group) => group.id === groupId && group.regionId === targetRegion,
  );
  return {
    id,
    name: '新地块',
    shortName: '地块',
    regionId: targetRegion,
    groupId: targetGroup?.id || '',
    description: '',
    tags: [],
    length: { min: 2, max: 3 },
    connections: {
      max: 2,
      anchors: 'ends',
      allowCrossing: true,
      allowedKinds: [],
      allowedTags: [],
    },
    subRooms: { enabled: false, min: 0, max: 0, pool: [] },
    contents: normalizeContents(null),
    generation: { weight: 1, max: 6, unique: false },
    visual: { color: '', icon: '▭' },
    spawnEligible: false,
  };
}

export function createSubRoomKind(project) {
  const id = uniqueId('subroom', new Set(project.subRoomKinds.map((room) => room.id)));
  return {
    id,
    name: '新子房间',
    description: '',
    color: SUBROOM_PALETTE[project.subRoomKinds.length % SUBROOM_PALETTE.length],
    tags: [],
    contents: normalizeContents(null),
    generation: { weight: 1 },
  };
}

export function createProfile(project) {
  const id = uniqueId('profile', new Set(project.profiles.map((profile) => profile.id)));
  const region =
    project.regions.find((candidate) => project.plotKinds.some((kind) => kind.regionId === candidate.id)) ||
    project.regions[0];
  const kind = project.plotKinds.find((candidate) => candidate.regionId === region?.id);
  return {
    id,
    name: '新生成方案',
    description: '',
    plotTarget: 16,
    startRegionId: region?.id || '',
    startKindId: kind?.id || '',
    regionIds: [],
    allowCrossRegion: true,
    crossRegionRatio: 0.2,
    maxCrossRegionPerPair: 2,
    allowCrossing: true,
    subRoomDensity: 1,
    generator: 'standard',
  };
}

export function createCatalogItem(project, type = 'props') {
  const safeType = CONTENT_TYPE_IDS.includes(type) ? type : 'props';
  const meta = CONTENT_TYPE_MAP[safeType];
  const id = uniqueId(safeType.replace(/s$/, ''), new Set((project.catalogs[safeType] || []).map((item) => item.id)));
  return {
    id,
    name: `新${meta.label}`,
    description: '',
    tags: [],
    rarity: 'common',
    icon: meta.icon,
  };
}

/* ------------------------------------------------------------------ *
 * 校验
 * ------------------------------------------------------------------ */

function makeIssue(level, code, message, path = '') {
  return { level, code, message, path };
}

export function validateProject(project) {
  const issues = [];
  const error = (code, message, path) => issues.push(makeIssue('error', code, message, path));
  const warn = (code, message, path) => issues.push(makeIssue('warning', code, message, path));

  if (!isObject(project)) {
    error('project.invalid', '工程数据不是一个对象。', '');
    return { ok: false, errors: issues, warnings: [], issues };
  }

  if (project.app !== APP_ID) {
    warn('project.app', `工程标识为「${project.app || '空'}」，不是 ${APP_ID}。`, 'app');
  }
  if (project.schemaVersion !== SCHEMA_VERSION) {
    warn(
      'project.schema',
      `工程版本 ${project.schemaVersion} 与当前版本 ${SCHEMA_VERSION} 不一致，保存后会按当前版本写入。`,
      'schemaVersion',
    );
  }
  if (!project.meta?.name?.trim()) {
    error('meta.name', '工程需要一个名称。', 'meta.name');
  }

  const grid = project.settings?.grid;
  if (!grid || grid.width < GRID_MIN || grid.width > GRID_MAX) {
    error('grid.width', `网格宽度需要在 ${GRID_MIN}–${GRID_MAX} 之间。`, 'settings.grid.width');
  }
  if (!grid || grid.height < GRID_MIN || grid.height > GRID_MAX) {
    error('grid.height', `网格高度需要在 ${GRID_MIN}–${GRID_MAX} 之间。`, 'settings.grid.height');
  }

  const regionIdSet = new Set();
  project.regions.forEach((region, index) => {
    const path = `regions[${index}]`;
    if (!region.id) error('region.id', '区域缺少 id。', `${path}.id`);
    if (region.id && regionIdSet.has(region.id)) error('region.id.duplicate', `区域 id「${region.id}」重复。`, `${path}.id`);
    regionIdSet.add(region.id);
    if (!region.name?.trim()) error('region.name', '区域缺少名称。', `${path}.name`);
    if (!isHexColor(region.color)) error('region.color', `区域「${region.name}」的颜色不是 #RRGGBB。`, `${path}.color`);
    if (region.generation.min > region.generation.max) {
      error('region.generation', `区域「${region.name}」的最小数量大于最大数量。`, `${path}.generation`);
    }
    if (region.generation.max < 0) error('region.generation.max', '区域最大数量不能为负数。', `${path}.generation.max`);
  });

  const groupIdSet = new Set();
  const groupById = new Map();
  const groupIndexById = new Map();
  project.plotGroups.forEach((group, index) => {
    const path = `plotGroups[${index}]`;
    if (!group.id) error('plotGroup.id', '折叠组缺少 id。', `${path}.id`);
    if (group.id && groupIdSet.has(group.id)) {
      error('plotGroup.id.duplicate', `折叠组 id「${group.id}」重复。`, `${path}.id`);
    }
    groupIdSet.add(group.id);
    groupById.set(group.id, group);
    groupIndexById.set(group.id, index);
    if (!group.name?.trim()) error('plotGroup.name', '折叠组缺少名称。', `${path}.name`);
    if (!regionIdSet.has(group.regionId)) {
      error('plotGroup.region', `折叠组「${group.name}」指向不存在的区域「${group.regionId}」。`, `${path}.regionId`);
    }
  });

  const reportedCycles = new Set();
  for (const group of project.plotGroups) {
    if (!group.parentGroupId) continue;
    const path = `plotGroups[${groupIndexById.get(group.id)}]`;
    const parent = groupById.get(group.parentGroupId);
    if (!parent) {
      error('plotGroup.parent', `折叠组「${group.name}」指向不存在的上级组「${group.parentGroupId}」。`, `${path}.parentGroupId`);
      continue;
    }
    if (parent.regionId !== group.regionId) {
      error('plotGroup.parent.region', `折叠组「${group.name}」与上级组「${parent.name}」不属于同一区域。`, `${path}.parentGroupId`);
    }
    const chain = new Set();
    let cursor = group;
    while (cursor?.parentGroupId) {
      if (chain.has(cursor.id)) {
        const key = [...chain].sort().join('|');
        if (!reportedCycles.has(key)) {
          reportedCycles.add(key);
          error('plotGroup.cycle', `折叠组「${group.name}」的上级关系形成了循环。`, `${path}.parentGroupId`);
        }
        break;
      }
      chain.add(cursor.id);
      cursor = groupById.get(cursor.parentGroupId);
    }
  }

  const kindIdSet = new Set();
  const subRoomIds = new Set(project.subRoomKinds.map((room) => room.id));
  const catalogIds = {};
  for (const type of CONTENT_TYPE_IDS) {
    catalogIds[type] = new Set((project.catalogs[type] || []).map((item) => item.id));
  }

  project.catalogs &&
    CONTENT_TYPE_IDS.forEach((type) => {
      (project.catalogs[type] || []).forEach((item, index) => {
        const path = `catalogs.${type}[${index}]`;
        if (!item.id) error('content.id', `${CONTENT_TYPES[type].label}缺少 id。`, `${path}.id`);
        if (!item.name?.trim()) error('content.name', `${CONTENT_TYPES[type].label}「${item.id}」缺少名称。`, `${path}.name`);
        if (!RARITY_IDS.includes(item.rarity)) error('content.rarity', `「${item.name}」的稀有度无效。`, `${path}.rarity`);
      });
    });

  project.plotKinds.forEach((kind, index) => {
    const path = `plotKinds[${index}]`;
    if (!kind.id) error('kind.id', '地块缺少 id。', `${path}.id`);
    if (kind.id && kindIdSet.has(kind.id)) error('kind.id.duplicate', `地块 id「${kind.id}」重复。`, `${path}.id`);
    kindIdSet.add(kind.id);
    if (!kind.name?.trim()) error('kind.name', '地块缺少名称。', `${path}.name`);
    if (!regionIdSet.has(kind.regionId)) {
      error('kind.region', `地块「${kind.name}」指向不存在的区域「${kind.regionId}」。`, `${path}.regionId`);
    }
    if (kind.groupId) {
      const group = project.plotGroups.find((candidate) => candidate.id === kind.groupId);
      if (!group) {
        error('kind.group', `地块「${kind.name}」指向不存在的折叠组「${kind.groupId}」。`, `${path}.groupId`);
      } else if (group.regionId !== kind.regionId) {
        error('kind.group.region', `地块「${kind.name}」与折叠组「${group.name}」不属于同一区域。`, `${path}.groupId`);
      }
    }
    if (kind.length.min < PLOT_LENGTH_MIN || kind.length.max > PLOT_LENGTH_MAX) {
      error('kind.length', `地块「${kind.name}」的长度需要落在 ${PLOT_LENGTH_MIN}–${PLOT_LENGTH_MAX} 格。`, `${path}.length`);
    }
    if (kind.length.min > kind.length.max) {
      error('kind.length.order', `地块「${kind.name}」的最小长度大于最大长度。`, `${path}.length`);
    }
    if (kind.connections.max < 0 || kind.connections.max > MAX_CONNECTIONS) {
      error('kind.connections', `地块「${kind.name}」的最大连接数需要在 0–${MAX_CONNECTIONS}。`, `${path}.connections.max`);
    }
    if (!ANCHOR_IDS.includes(kind.connections.anchors)) {
      error('kind.anchors', `地块「${kind.name}」的接口模式无效。`, `${path}.connections.anchors`);
    }
    if (kind.connections.anchors === 'middle' && kind.length.min < 3) {
      warn('kind.anchors.middle', `地块「${kind.name}」可能生成 1–2 格长度，此长度下中段接口不可用。`, `${path}.connections.anchors`);
    }
    for (const target of kind.connections.allowedKinds) {
      if (!kindIdSet.has(target) && !project.plotKinds.some((candidate) => candidate.id === target)) {
        error('kind.allowedKinds', `地块「${kind.name}」允许连接不存在的地块「${target}」。`, `${path}.connections.allowedKinds`);
      }
    }
    if (kind.subRooms.enabled) {
      if (kind.subRooms.min > kind.subRooms.max) {
        error('kind.subRooms', `地块「${kind.name}」的子房间数量下限大于上限。`, `${path}.subRooms`);
      }
      if (kind.subRooms.pool.length === 0) {
        warn('kind.subRooms.pool', `地块「${kind.name}」启用了子房间但没有可选池。`, `${path}.subRooms.pool`);
      }
      for (const roomId of kind.subRooms.pool) {
        if (!subRoomIds.has(roomId)) {
          error('kind.subRooms.pool.ref', `地块「${kind.name}」的子房间池引用了不存在的「${roomId}」。`, `${path}.subRooms.pool`);
        }
      }
    }
    for (const type of CONTENT_TYPE_IDS) {
      for (const itemId of kind.contents[type] || []) {
        if (!catalogIds[type].has(itemId)) {
          error('kind.contents.ref', `地块「${kind.name}」引用了不存在的${CONTENT_TYPES[type].label}「${itemId}」。`, `${path}.contents.${type}`);
        }
      }
    }
    if (kind.connections.max === 0 && !kind.spawnEligible) {
      warn('kind.isolated', `地块「${kind.name}」最大连接数为 0，且不是出生点候选，生成时不会被使用。`, `${path}.connections.max`);
    }
  });

  project.subRoomKinds.forEach((room, index) => {
    const path = `subRoomKinds[${index}]`;
    if (!room.id) error('subroom.id', '子房间缺少 id。', `${path}.id`);
    if (!room.name?.trim()) error('subroom.name', '子房间缺少名称。', `${path}.name`);
    if (!isHexColor(room.color)) error('subroom.color', `子房间「${room.name}」的颜色无效。`, `${path}.color`);
    for (const type of CONTENT_TYPE_IDS) {
      for (const itemId of room.contents[type] || []) {
        if (!catalogIds[type].has(itemId)) {
          error('subroom.contents.ref', `子房间「${room.name}」引用了不存在的${CONTENT_TYPES[type].label}「${itemId}」。`, `${path}.contents.${type}`);
        }
      }
    }
  });

  const profileIds = new Set();
  project.profiles.forEach((profile, index) => {
    const path = `profiles[${index}]`;
    if (!profile.id) error('profile.id', '生成方案缺少 id。', `${path}.id`);
    if (profile.id && profileIds.has(profile.id)) error('profile.id.duplicate', `生成方案 id「${profile.id}」重复。`, `${path}.id`);
    profileIds.add(profile.id);
    if (!profile.name?.trim()) error('profile.name', '生成方案缺少名称。', `${path}.name`);
    if (profile.plotTarget < TARGET_MIN || profile.plotTarget > TARGET_MAX) {
      error('profile.target', `生成方案的目标地块数需要在 ${TARGET_MIN}–${TARGET_MAX}。`, `${path}.plotTarget`);
    }

    const startKind = project.plotKinds.find((kind) => kind.id === profile.startKindId);
    if (!startKind) {
      error('profile.startKind', `生成方案的起始地块「${profile.startKindId}」不存在。`, `${path}.startKindId`);
    } else if (profile.startRegionId && startKind.regionId !== profile.startRegionId) {
      error('profile.startRegion', `起始地块「${startKind.name}」不属于起始区域「${profile.startRegionId}」。`, `${path}.startRegionId`);
    } else if (!startKind.spawnEligible) {
      warn('profile.startKind.spawn', `起始地块「${startKind.name}」没有标记为出生点候选。`, `${path}.startKindId`);
    }
    if (!regionIdSet.has(profile.startRegionId)) {
      error('profile.startRegion.missing', `生成方案的起始区域「${profile.startRegionId}」不存在。`, `${path}.startRegionId`);
    }
    profile.regionIds.forEach((regionId) => {
      if (!regionIdSet.has(regionId)) {
        error('profile.regionIds.ref', `生成方案引用了不存在的区域「${regionId}」。`, `${path}.regionIds`);
      }
    });

    if (profile.allowCrossRegion) {
      if (profile.maxCrossRegionPerPair === 0) {
        warn('profile.cross.perPair', `生成方案「${profile.name}」允许跨区，但每对区域上限为 0，实际不会跨区。`, `${path}.maxCrossRegionPerPair`);
      }
      if (profile.crossRegionRatio === 0) {
        warn('profile.cross.ratio', `生成方案「${profile.name}」允许跨区，但跨区比例为 0。`, `${path}.crossRegionRatio`);
      }
    }

    const active = activeRegionsForProfile(project, profile);
    if (active.length === 0) {
      error('profile.regions.empty', `生成方案「${profile.name}」没有任何可用区域。`, path);
    } else {
      const maxTotal = active.reduce((sum, region) => sum + region.generation.max, 0);
      const minTotal = active.reduce((sum, region) => sum + region.generation.min, 0);
      if (maxTotal < profile.plotTarget) {
        error(
          'profile.quota.max',
          `生成方案「${profile.name}」目标 ${profile.plotTarget} 块，但所选区域最大数量合计只有 ${maxTotal}。`,
          path,
        );
      }
      if (minTotal > profile.plotTarget) {
        warn(
          'profile.quota.min',
          `生成方案「${profile.name}」所选区域最小数量合计 ${minTotal}，高于目标 ${profile.plotTarget}，实际会以目标为准。`,
          path,
        );
      }
      if (!startKind) {
        // 已在上方报告。
      } else if (startKind.length.min > Math.min(grid?.width || 0, grid?.height || 0)) {
        error('profile.start.length', `起始地块的最小长度放不进当前网格。`, `${path}.startKindId`);
      }
      if (active.length > 1 && profile.allowCrossRegion) {
        const cap = crossCapForProfile(profile, profile.plotTarget);
        if (cap <= 0) {
          warn('profile.cross.cap', `生成方案「${profile.name}」的跨区容量为 0，将退化为不跨区。`, path);
        }
      }
    }
  });

  if (!project.plotKinds.some((kind) => kind.spawnEligible)) {
    warn('spawn.none', '没有地块被标记为出生点候选，生成时会退回到第一个可用地块。', 'plotKinds');
  }

  const errors = issues.filter((issue) => issue.level === 'error');
  const warnings = issues.filter((issue) => issue.level === 'warning');
  return { ok: errors.length === 0, errors, warnings, issues };
}

function crossCapForProfile(profile, target) {
  return Math.floor(Math.max(0, target) * Math.max(0, profile.crossRegionRatio));
}

function activeRegionsForProfile(project, profile) {
  const kindsByRegion = new Map();
  for (const kind of project.plotKinds) {
    if (kind.connections.max === 0 && !kind.spawnEligible) continue;
    if (!kindsByRegion.has(kind.regionId)) kindsByRegion.set(kind.regionId, []);
    kindsByRegion.get(kind.regionId).push(kind);
  }
  const wanted = profile.regionIds?.length ? new Set(profile.regionIds) : null;
  return project.regions.filter((region) => {
    if (wanted && !wanted.has(region.id)) return false;
    if (region.hidden) return false;
    return (kindsByRegion.get(region.id) || []).length > 0;
  });
}

/* ------------------------------------------------------------------ *
 * 指纹与路径
 * ------------------------------------------------------------------ */

function stableStringify(value) {
  if (Array.isArray(value)) return `[${value.map((item) => stableStringify(item)).join(',')}]`;
  if (isObject(value)) {
    const keys = Object.keys(value).sort();
    return `{${keys.map((key) => `${JSON.stringify(key)}:${stableStringify(value[key])}`).join(',')}}`;
  }
  return JSON.stringify(value);
}

export function projectFingerprint(project) {
  const snapshot = clone(project);
  if (snapshot.meta) delete snapshot.meta.updatedAt;
  const text = stableStringify(snapshot);
  let hash = 0x811c9dc5;
  for (let index = 0; index < text.length; index += 1) {
    hash ^= text.charCodeAt(index);
    hash = Math.imul(hash, 0x01000193) >>> 0;
  }
  return `pw_${hash.toString(16).padStart(8, '0')}`;
}

export function getByPath(object, path) {
  if (!path) return object;
  return path.split('.').reduce((current, key) => (current == null ? undefined : current[key]), object);
}

export function setByPath(object, path, value) {
  if (!path) return object;
  const keys = path.split('.');
  let cursor = object;
  for (let index = 0; index < keys.length - 1; index += 1) {
    const key = keys[index];
    if (cursor[key] == null || typeof cursor[key] !== 'object') {
      cursor[key] = /^\d+$/.test(keys[index + 1]) ? [] : {};
    }
    cursor = cursor[key];
  }
  cursor[keys[keys.length - 1]] = value;
  return object;
}

export function createBlankProject() {
  const project = normalizeProject({
    app: APP_ID,
    schemaVersion: SCHEMA_VERSION,
    meta: { name: '未命名工程' },
    settings: { grid: { width: 40, height: 30 } },
    regions: [],
    plotKinds: [],
    subRoomKinds: [],
    catalogs: {},
    profiles: [],
    extensions: {},
  });
  const region = createRegion(project);
  region.name = '起始区域';
  region.shortName = '起始';
  region.generation = { min: 1, max: 8, weight: 1, order: 0 };
  project.regions.push(region);

  const kind = createPlotKind(project, region.id);
  kind.name = '起始地块';
  kind.shortName = '起始';
  kind.length = { min: 2, max: 3 };
  kind.spawnEligible = true;
  project.plotKinds.push(kind);

  const profile = createProfile(project);
  profile.name = '默认生成';
  profile.startRegionId = region.id;
  profile.startKindId = kind.id;
  profile.plotTarget = 8;
  project.profiles.push(profile);

  return normalizeProject(project);
}
