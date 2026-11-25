/**
 * 设计数据与生成规则之间的纯函数层。
 * 这里不创建地图，只回答“这个地块能否和那个地块相连”之类的问题。
 */

import { SCHEMA_VERSION, clamp } from './constants.js';

export function regionById(project, id) {
  return (project?.regions ?? []).find((region) => region.id === id) ?? null;
}

export function plotKindById(project, id) {
  return (project?.plotKinds ?? []).find((kind) => kind.id === id) ?? null;
}

export function subRoomKindById(project, id) {
  return (project?.subRoomKinds ?? []).find((kind) => kind.id === id) ?? null;
}

export function profileById(project, id) {
  return (project?.profiles ?? []).find((profile) => profile.id === id) ?? null;
}

export function plotKindsForRegion(project, regionId) {
  return (project?.plotKinds ?? []).filter((kind) => kind.regionId === regionId);
}

export function regionsWithKinds(project) {
  const ids = new Set((project?.plotKinds ?? []).map((kind) => kind.regionId));
  return (project?.regions ?? []).filter((region) => ids.has(region.id));
}

function intersects(a, b) {
  const set = new Set(Array.isArray(a) ? a : []);
  return (Array.isArray(b) ? b : []).some((item) => set.has(item));
}

/**
 * 两种地块是否允许建立任何连接（门或交叉）。
 * - allowedKinds 为空表示不限制；
 * - allowedTags 为空表示不限制，否则对方至少有一个同标签。
 * - 两个方向分别检查，因此不对称的限制会自然收窄。
 */
export function canConnectKinds(a, b) {
  if (!a || !b) return false;
  const aRules = a.connections ?? {};
  const bRules = b.connections ?? {};

  const aAllowed = Array.isArray(aRules.allowedKinds) ? aRules.allowedKinds : [];
  if (aAllowed.length && !aAllowed.includes(b.id)) return false;
  const bAllowed = Array.isArray(bRules.allowedKinds) ? bRules.allowedKinds : [];
  if (bAllowed.length && !bAllowed.includes(a.id)) return false;

  const aTags = Array.isArray(aRules.allowedTags) ? aRules.allowedTags : [];
  if (aTags.length && !intersects(aTags, b.tags)) return false;
  const bTags = Array.isArray(bRules.allowedTags) ? bRules.allowedTags : [];
  if (bTags.length && !intersects(bTags, a.tags)) return false;

  return true;
}

export function canCrossKinds(a, b, profile) {
  if (!a || !b) return false;
  if (profile && profile.allowCrossing === false) return false;
  if (a.connections?.allowCrossing === false || b.connections?.allowCrossing === false) return false;
  return canConnectKinds(a, b);
}

export function regionPairKey(a, b) {
  return [a, b].sort().join('::');
}

export function crossCapFor(profile, target) {
  if (!profile || profile.allowCrossRegion === false) return 0;
  const ratio = clamp(Number(profile.crossRegionRatio) || 0, 0, 1);
  const perPair = Math.max(0, Number(profile.maxCrossRegionPerPair) || 0);
  if (perPair <= 0) return 0;
  return Math.floor(Math.max(0, target) * ratio);
}

/** 解析生成配置；找不到时回退到第一个，再不行返回 null。 */
export function resolveProfile(project, profileId) {
  const profiles = project?.profiles ?? [];
  return profiles.find((profile) => profile.id === profileId) ?? profiles[0] ?? null;
}

export function activeRegionsForProfile(project, profile) {
  const allowed = new Set(Array.isArray(profile?.regionIds) ? profile.regionIds : []);
  const regions = regionsWithKinds(project);
  return regions.filter((region) => {
    if (Number(region.generation?.max) <= 0) return false;
    if (allowed.size && !allowed.has(region.id)) return false;
    return true;
  });
}

export function kindGenerationCount(map, kindId) {
  return (map?.plots ?? []).filter((plot) => plot.kindId === kindId).length;
}

export function regionGenerationCount(map, regionId) {
  return (map?.plots ?? []).filter((plot) => plot.regionId === regionId).length;
}

export function normalizeProfileForGeneration(profile) {
  if (!profile) return null;
  return {
    ...profile,
    plotTarget: clamp(Number(profile.plotTarget) || 1, 1, 200),
    crossRegionRatio: clamp(Number(profile.crossRegionRatio) || 0, 0, 1),
    maxCrossRegionPerPair: Math.max(0, Number(profile.maxCrossRegionPerPair) || 0),
    allowCrossRegion: profile.allowCrossRegion !== false,
    allowCrossing: profile.allowCrossing !== false,
    subRoomDensity: clamp(Number(profile.subRoomDensity) || 1, 0, 3),
    generator: profile.generator || 'standard',
  };
}

export function projectSchemaLabel(project) {
  return `${project?.schemaVersion ?? SCHEMA_VERSION}`;
}
