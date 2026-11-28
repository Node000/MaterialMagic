/**
 * 完整地图四等级地图池核心逻辑（不依赖 DOM）。
 *
 * - 固定 4 个等级（等级 1 ~ 等级 4），用于存放在地图池里按玩家游戏进度分级抽取；
 * - 每个地图项复用单层三角地图生成配置，并新增「指定出生点区域」（留空/random 则在三区中随机）；
 * - 支持权重抽取、单等级生成、一键生成四张，以及导出/导入单份包含全部四等级配置的 JSON（与 Unity SecondFloorMapPools 格式兼容）。
 */

import { DEFAULT_CELL_TYPES, DUNGEON_REGION_PRESETS, clamp } from './constants.js';
import {
  generateFloorMap,
  normalizeFloorProfile,
  normalizeSpawnRegionSlot,
} from './floor-generator.js';
import { POOL_KIND, POOL_VERSION, RING_SPACING } from './map-pools.js';
import { RNG, normalizeSeed, randomSeed } from './rng.js';

export const WORLD_TIER_COUNT = 4;
export const WORLD_TIER_IDS = Object.freeze(['level-1', 'level-2', 'level-3', 'level-4']);
export const WORLD_TIER_NAMES = Object.freeze(['等级 1', '等级 2', '等级 3', '等级 4']);

const isObject = (v) => v !== null && typeof v === 'object' && !Array.isArray(v);
const clone = (v) => JSON.parse(JSON.stringify(v));
const PRESET_BY_ID = new Map(DUNGEON_REGION_PRESETS.map((p) => [p.id, p]));

const SLOT_CODES = ['A', 'B', 'C'];

export function spawnSlotToJson(slot) {
  if (slot === 'random' || slot === null || slot === undefined || slot === '') return 'random';
  if (slot === 0 || slot === '0' || slot === 'A' || slot === 'a') return 'A';
  if (slot === 1 || slot === '1' || slot === 'B' || slot === 'b') return 'B';
  if (slot === 2 || slot === '2' || slot === 'C' || slot === 'c') return 'C';
  return 'random';
}

export function createWorldMapEntry(tierIndex = 0, existingEntries = [], overrides = {}) {
  const tier = clamp(Math.round(Number(tierIndex) || 0), 0, WORLD_TIER_COUNT - 1);
  const occupied = new Set((existingEntries || []).map((e) => e.id));
  let seq = (existingEntries?.length || 0) + 1;
  while (occupied.has(`tier${tier + 1}_map_${seq}`)) seq += 1;

  const tierPresets = [
    { regions: ['city', 'factory', 'suburb'], stripMin: 5, stripMax: 6, baseLevel: 1, levelPerHop: 0.5, maxLevel: 12 },
    { regions: ['factory', 'city', 'suburb'], stripMin: 5, stripMax: 7, baseLevel: 3, levelPerHop: 0.75, maxLevel: 16 },
    { regions: ['suburb', 'factory', 'space'], stripMin: 6, stripMax: 7, baseLevel: 6, levelPerHop: 0.75, maxLevel: 20 },
    { regions: ['space', 'factory', 'city'], stripMin: 6, stripMax: 8, baseLevel: 9, levelPerHop: 1.0, maxLevel: 25 },
  ][tier];

  const baseRegions = tierPresets.regions.map((regionTypeId) => {
    const preset = PRESET_BY_ID.get(regionTypeId) || DUNGEON_REGION_PRESETS[0];
    return {
      regionTypeId: preset.id,
      name: preset.name,
      shortName: preset.shortName,
      color: preset.color,
      stripMin: tierPresets.stripMin,
      stripMax: tierPresets.stripMax,
      maxNoLoop: 1,
    };
  });

  return normalizeWorldMapEntry(
    {
      id: `tier${tier + 1}_map_${seq}`,
      name: `等级 ${tier + 1} 地图 ${seq}`,
      weight: 1,
      patternLibraryId: 'Sample',
      ringSpacing: 'balanced',
      spawnRegionSlot: 'random',
      regions: baseRegions,
      topology: {
        minLoopsPerRegion: 1,
        minCycleLength: 4,
        extraLoopChance: 0.4,
        allowCrossing: true,
        crossChance: 0.25,
        looseness: 0.55,
        localClustering: true,
      },
      budget: {
        resourceMin: null,
        resourceMax: null,
        difficultyMin: null,
        difficultyMax: null,
      },
      progression: {
        baseLevel: tierPresets.baseLevel,
        levelPerHop: tierPresets.levelPerHop,
        maxLevel: tierPresets.maxLevel,
      },
      ...overrides,
    },
    tier,
  );
}

export function normalizeWorldMapEntry(raw, tierIndex = 0) {
  const source = isObject(raw) ? raw : {};
  const tier = clamp(Math.round(Number(tierIndex) || 0), 0, WORLD_TIER_COUNT - 1);
  const spawnRegionSlot = normalizeSpawnRegionSlot(source.spawnRegionSlot, { defaultToRandom: true });

  // 支持从平铺的 Unity 地图池字段（minCycleLength / extraLoopChance / localClustering）合并到 topology
  const rawTopo = isObject(source.topology) ? { ...source.topology } : {};
  if (rawTopo.minCycleLength === undefined && source.minCycleLength !== undefined) {
    rawTopo.minCycleLength = source.minCycleLength;
  }
  if (rawTopo.extraLoopChance === undefined && source.extraLoopChance !== undefined) {
    rawTopo.extraLoopChance = source.extraLoopChance;
  }
  if (rawTopo.localClustering === undefined && source.localClustering !== undefined) {
    rawTopo.localClustering = source.localClustering;
  }

  const profile = normalizeFloorProfile({
    id: source.id || `tier${tier + 1}_map_1`,
    name: source.name || `等级 ${tier + 1} 地图`,
    description: source.description || '',
    spawnRegionSlot,
    regions: Array.isArray(source.regions) ? source.regions : undefined,
    topology: {
      minLoopsPerRegion: 1,
      minCycleLength: 4,
      extraLoopChance: 0.4,
      allowCrossing: true,
      crossChance: 0.25,
      looseness: 0.55,
      localClustering: true,
      ...rawTopo,
    },
    budget: source.budget,
    progression: source.progression,
  });

  const rawWeight = Number(source.weight);
  const weight = Number.isFinite(rawWeight) && rawWeight > 0 ? Math.round(rawWeight * 1000) / 1000 : 1;
  const patternLibraryId = String(source.patternLibraryId || 'Sample').trim() || 'Sample';
  const ringSpacing = RING_SPACING.includes(source.ringSpacing) ? source.ringSpacing : 'balanced';

  return {
    id: profile.id,
    name: profile.name,
    description: profile.description,
    weight,
    patternLibraryId,
    ringSpacing,
    spawnRegionSlot,
    regions: profile.regions,
    topology: profile.topology,
    budget: profile.budget,
    progression: profile.progression,
  };
}

export function createDefaultWorldPools() {
  return {
    kind: POOL_KIND,
    schemaVersion: POOL_VERSION,
    pools: WORLD_TIER_IDS.map((id, tierIdx) => ({
      id,
      name: WORLD_TIER_NAMES[tierIdx],
      tier: tierIdx + 1,
      entries: [createWorldMapEntry(tierIdx, [])],
    })),
  };
}

export function normalizeWorldPools(raw) {
  const source = isObject(raw) ? raw : {};
  const rawPools = Array.isArray(source.pools) ? source.pools : null;
  if (!rawPools) return createDefaultWorldPools();

  const pools = WORLD_TIER_IDS.map((defaultId, tierIdx) => {
    const matched =
      rawPools.find((p) => isObject(p) && (p.tier === tierIdx + 1 || p.id === defaultId)) ||
      (isObject(rawPools[tierIdx]) ? rawPools[tierIdx] : null);
    const rawEntries = Array.isArray(matched?.entries) ? matched.entries.filter(isObject) : [];
    const seenIds = new Set();
    const entries = rawEntries.map((item, idx) => {
      const entry = normalizeWorldMapEntry(item, tierIdx);
      let id = entry.id || `tier${tierIdx + 1}_map_${idx + 1}`;
      if (seenIds.has(id)) {
        let s = 2;
        while (seenIds.has(`${id}_${s}`)) s += 1;
        id = `${id}_${s}`;
      }
      seenIds.add(id);
      return { ...entry, id };
    });
    return {
      id: defaultId,
      name: String(matched?.name || WORLD_TIER_NAMES[tierIdx]).trim() || WORLD_TIER_NAMES[tierIdx],
      tier: tierIdx + 1,
      entries,
    };
  });

  return {
    kind: POOL_KIND,
    schemaVersion: POOL_VERSION,
    pools,
  };
}

export function worldEntryToFloorProfile(entry) {
  const normalized = normalizeWorldMapEntry(entry);
  return normalizeFloorProfile({
    id: normalized.id,
    name: normalized.name,
    description: normalized.description,
    spawnRegionSlot: normalized.spawnRegionSlot,
    regions: normalized.regions,
    topology: normalized.topology,
    budget: normalized.budget,
    progression: normalized.progression,
  });
}

export function selectWorldEntry(pool, { entryId = '', seed = null } = {}) {
  const entries = Array.isArray(pool?.entries) ? pool.entries : [];
  if (!entries.length) return { entry: null, pickedRandomly: false };
  const wanted = String(entryId || '').trim();
  if (wanted) {
    const exact = entries.find((e) => e.id === wanted);
    if (exact) return { entry: exact, pickedRandomly: false };
  }
  const rng = new RNG(`${normalizeSeed(seed || randomSeed())}:world-pick:${pool.id || 'tier'}`);
  const picked = rng.weighted(entries, (e) => Math.max(0.001, Number(e.weight) || 1)) || entries[0];
  return { entry: picked, pickedRandomly: true };
}

export function generateWorldTierMap({
  worldPools,
  tierIndex,
  entryId = '',
  seed = null,
  stripTemplates,
  cellTypes = DEFAULT_CELL_TYPES,
  attempts = 18,
} = {}) {
  const normalized = normalizeWorldPools(worldPools);
  const idx = clamp(Math.round(Number(tierIndex) || 0), 0, WORLD_TIER_COUNT - 1);
  const pool = normalized.pools[idx];
  if (!pool || !pool.entries.length) {
    throw new Error(`${WORLD_TIER_NAMES[idx]} 当前没有任何地图配置，请先在左侧新增配置。`);
  }
  const finalSeed = normalizeSeed(seed || randomSeed());
  const { entry, pickedRandomly } = selectWorldEntry(pool, { entryId, seed: finalSeed });
  const profile = worldEntryToFloorProfile(entry);
  const map = generateFloorMap({
    profile,
    stripTemplates,
    cellTypes,
    seed: finalSeed,
    attempts,
  });
  return {
    tier: idx + 1,
    tierIndex: idx,
    poolId: pool.id,
    poolName: pool.name,
    entryId: entry.id,
    entryName: entry.name,
    pickedRandomly,
    seed: map.seed,
    map,
  };
}

export function serializeWorldPools(worldPools) {
  const normalized = normalizeWorldPools(worldPools);
  const payload = {
    kind: POOL_KIND,
    schemaVersion: POOL_VERSION,
    pools: normalized.pools.map((pool) => ({
      id: pool.id,
      name: pool.name,
      tier: pool.tier,
      entries: pool.entries.map((entry) => ({
        id: entry.id,
        name: entry.name,
        weight: entry.weight,
        patternLibraryId: entry.patternLibraryId || 'Sample',
        ringSpacing: entry.ringSpacing || 'balanced',
        spawnRegionSlot: spawnSlotToJson(entry.spawnRegionSlot),
        minCycleLength: entry.topology.minCycleLength,
        extraLoopChance: entry.topology.extraLoopChance,
        localClustering: Boolean(entry.topology.localClustering),
        regions: entry.regions.map((r) => ({
          regionTypeId: r.regionTypeId || r.id,
          stripMin: r.stripMin,
          stripMax: r.stripMax,
          maxNoLoop: r.maxNoLoop,
        })),
        budget: {
          resourceMin: entry.budget.resourceMin,
          resourceMax: entry.budget.resourceMax,
          difficultyMin: entry.budget.difficultyMin,
          difficultyMax: entry.budget.difficultyMax,
        },
        progression: {
          baseLevel: entry.progression.baseLevel,
          levelPerHop: entry.progression.levelPerHop,
          maxLevel: entry.progression.maxLevel,
        },
        topology: clone(entry.topology),
      })),
    })),
  };
  return JSON.stringify(payload, null, 2) + '\n';
}

export function validateWorldPools(worldPools) {
  const errors = [];
  if (!isObject(worldPools) || worldPools.kind !== POOL_KIND || worldPools.schemaVersion !== POOL_VERSION) {
    return ['不是受支持的完整地图池配置（plotweave-map-pools v1）。'];
  }
  if (!Array.isArray(worldPools.pools) || worldPools.pools.length !== WORLD_TIER_COUNT) {
    errors.push(`需要恰好 ${WORLD_TIER_COUNT} 个等级池（等级 1 至 等级 4）。`);
    return errors;
  }
  for (let i = 0; i < WORLD_TIER_COUNT; i += 1) {
    const pool = worldPools.pools[i];
    const label = pool?.name || WORLD_TIER_NAMES[i];
    if (!Array.isArray(pool?.entries) || pool.entries.length === 0) {
      errors.push(`${label} 至少需要 1 条地图配置。`);
      continue;
    }
    const seen = new Set();
    for (const entry of pool.entries) {
      const eLabel = `${label} / ${entry?.name || entry?.id || '未命名'}`;
      if (!entry?.id || seen.has(entry.id)) errors.push(`${eLabel}：条目 ID 无效或重复。`);
      seen.add(entry?.id);
      if (!(Number(entry?.weight) > 0)) errors.push(`${eLabel}：权重必须为正数。`);
      const minStrips = Math.min(...(entry?.regions || []).map((r) => Number(r?.stripMin) || 3));
      const minCycle = Number(entry?.topology?.minCycleLength ?? 3);
      if (minCycle > minStrips) {
        errors.push(`${eLabel}：最小环长度 (${minCycle}) 不能大于区域最少条带数 (${minStrips})。`);
      }
    }
  }
  return errors;
}

export function parseWorldPools(jsonText) {
  let raw;
  try {
    raw = typeof jsonText === 'string' ? JSON.parse(jsonText.replace(/^\uFEFF/, '')) : jsonText;
  } catch (error) {
    return { ok: false, errors: [`JSON 解析失败：${error.message}`], worldPools: null };
  }
  if (!isObject(raw) || raw.kind !== POOL_KIND || !Array.isArray(raw.pools)) {
    return { ok: false, errors: ['文件不是有效的地图池配置 JSON（缺少 kind: "plotweave-map-pools" 或 pools）。'], worldPools: null };
  }
  const worldPools = normalizeWorldPools(raw);
  return { ok: true, errors: [], warnings: validateWorldPools(worldPools), worldPools };
}

export function formatSpawnRegionLabel(entry) {
  if (!entry) return '随机';
  if (entry.spawnRegionSlot === 'random' || entry.spawnRegionSlot === null || entry.spawnRegionSlot === undefined) {
    return '随机';
  }
  const idx = clamp(Number(entry.spawnRegionSlot) || 0, 0, 2);
  const region = entry.regions?.[idx];
  return `${SLOT_CODES[idx]}·${region?.shortName || region?.name || region?.regionTypeId || ''}`;
}
