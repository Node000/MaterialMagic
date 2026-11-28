/**
 * 完整地图模块兼容入口（已升级为四等级地图池，详见 world-pools.js）。
 */

import {
  WORLD_TIER_COUNT,
  createDefaultWorldPools,
  normalizeWorldPools,
} from './world-pools.js';

export const WORLD_PLACEHOLDER_STATUS = Object.freeze({
  implemented: true,
  version: 1,
  tiers: WORLD_TIER_COUNT,
  title: '完整地图生成（四等级地图池）',
  description: '按 4 个游戏进度等级组织单层地图配置，支持四框同屏抽样预览与单份 JSON 导出。',
});

export function createEmptyWorldConfig() {
  return createDefaultWorldPools();
}

export function normalizeWorldConfig(raw) {
  return normalizeWorldPools(raw);
}
