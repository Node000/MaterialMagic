/**
 * 完整地图生成器占位模块（预留扩展接口，暂不实现具体算法）。
 *
 * 后续可在此扩展：
 * - 多层单层地图的纵向/横向衔接（电梯/竖井/捷径门）；
 * - 类银河城能力锁（Key-Lock）与跨层回溯路线；
 * - 全局大世界进度曲线与 Boss/终点配置。
 */

export const WORLD_PLACEHOLDER_STATUS = Object.freeze({
  implemented: false,
  version: 0,
  title: '完整地图生成（待规划）',
  description: '此模块按需求暂时留空，用于未来将多层三角地牢串联为完整类银河城大世界。',
});

export function createEmptyWorldConfig() {
  return {
    enabled: false,
    layers: [],
    connections: [],
    notes: '',
  };
}

export function normalizeWorldConfig(raw) {
  if (!raw || typeof raw !== 'object') return createEmptyWorldConfig();
  return {
    enabled: Boolean(raw.enabled),
    layers: Array.isArray(raw.layers) ? raw.layers : [],
    connections: Array.isArray(raw.connections) ? raw.connections : [],
    notes: typeof raw.notes === 'string' ? raw.notes : '',
  };
}
