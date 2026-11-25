/**
 * PlotWeave · 全局常量与方向工具
 *
 * 该文件不依赖 DOM，可被浏览器和 Node 测试同时导入。
 */

export const APP_ID = 'plotweave';
export const APP_VERSION = '0.1.0';
export const SCHEMA_VERSION = 1;

export const PLOT_LENGTH_MIN = 1;
export const PLOT_LENGTH_MAX = 5;
export const MAX_CONNECTIONS = 8;

/** 四个正交方向。axis 表示该方向所属的条带轴向。 */
export const DIRECTIONS = Object.freeze({
  N: Object.freeze({ id: 'N', dx: 0, dy: -1, opposite: 'S', axis: 'v', label: '北' }),
  E: Object.freeze({ id: 'E', dx: 1, dy: 0, opposite: 'W', axis: 'h', label: '东' }),
  S: Object.freeze({ id: 'S', dx: 0, dy: 1, opposite: 'N', axis: 'v', label: '南' }),
  W: Object.freeze({ id: 'W', dx: -1, dy: 0, opposite: 'E', axis: 'h', label: '西' }),
});

export const DIR_IDS = Object.freeze(['N', 'E', 'S', 'W']);

export const AXES = Object.freeze({
  h: Object.freeze({ id: 'h', dir: 'E', negative: 'W', label: '横向' }),
  v: Object.freeze({ id: 'v', dir: 'S', negative: 'N', label: '纵向' }),
});

export const AXIS_IDS = Object.freeze(['h', 'v']);

/** 连接锚点策略：地块在哪些格子上开出连接口。 */
export const ANCHOR_MODES = Object.freeze([
  Object.freeze({
    id: 'ends',
    label: '仅端部',
    short: '端部',
    description: '只有条带两端所在的格子可以连接；端部格子的三个外侧都可以成为接口。',
  }),
  Object.freeze({
    id: 'middle',
    label: '仅中段',
    short: '中段',
    description: '只有条带内部的格子可以连接，方向垂直于条带轴。长度小于 3 的地块没有中段接口。',
  }),
  Object.freeze({
    id: 'both',
    label: '端部 + 中段',
    short: '端部+中段',
    description: '端部格与内部格都可以连接，是最常用的分支型地块。',
  }),
  Object.freeze({
    id: 'any',
    label: '任意格 / 任意侧',
    short: '任意',
    description: '所有格子都可以连接；端部格也开放侧向接口，适合大厅和枢纽。',
  }),
]);

export const ANCHOR_IDS = Object.freeze(ANCHOR_MODES.map((m) => m.id));

/** 五类可挂载内容。它们只是目录引用，不进入生成算法。 */
export const CONTENT_TYPES = Object.freeze([
  Object.freeze({ id: 'props', label: '道具', short: '道具', color: '#d9a441', icon: '◆' }),
  Object.freeze({ id: 'relics', label: '遗物', short: '遗物', color: '#b58bd8', icon: '✦' }),
  Object.freeze({ id: 'events', label: '事件链', short: '事件', color: '#6aa9c9', icon: '◈' }),
  Object.freeze({ id: 'enemies', label: '敌人', short: '敌人', color: '#d97b6c', icon: '▲' }),
  Object.freeze({ id: 'npcs', label: 'NPC', short: 'NPC', color: '#7fb685', icon: '●' }),
]);

export const CONTENT_TYPE_IDS = Object.freeze(CONTENT_TYPES.map((t) => t.id));

export const CONTENT_TYPE_MAP = Object.freeze(
  Object.fromEntries(CONTENT_TYPES.map((t) => [t.id, t])),
);

export function emptyContents() {
  return Object.fromEntries(CONTENT_TYPE_IDS.map((id) => [id, []]));
}

export const REGION_PALETTE = Object.freeze([
  '#d9a441',
  '#6aa9c9',
  '#a78bfa',
  '#7fb685',
  '#d97b6c',
  '#c9a0dc',
  '#89b8a5',
  '#d6b36a',
  '#7d9fd1',
  '#c78f8f',
]);

export const SUBROOM_PALETTE = Object.freeze([
  '#c9a227',
  '#b07bd8',
  '#6aa9c9',
  '#7fb685',
  '#d98c6a',
  '#9aa7b8',
  '#c78fb8',
  '#8fa86a',
]);

export const RARITIES = Object.freeze([
  Object.freeze({ id: 'common', label: '常见' }),
  Object.freeze({ id: 'uncommon', label: '少见' }),
  Object.freeze({ id: 'rare', label: '稀有' }),
]);

export const RARITY_IDS = Object.freeze(RARITIES.map((r) => r.id));

export const GRID_MIN = 8;
export const GRID_MAX = 160;
export const TARGET_MIN = 1;
export const TARGET_MAX = 200;

/** 方向转向工具：把方向顺时针 / 逆时针旋转 90 度。 */
export function turnCW(dirId) {
  return { N: 'E', E: 'S', S: 'W', W: 'N' }[dirId];
}

export function turnCCW(dirId) {
  return { N: 'W', W: 'S', S: 'E', E: 'N' }[dirId];
}

/** 与给定方向垂直的两个方向。 */
export function perpendicularDirs(dirId) {
  return [turnCW(dirId), turnCCW(dirId)];
}

export function directionFromDelta(dx, dy) {
  if (dx === 1 && dy === 0) return 'E';
  if (dx === -1 && dy === 0) return 'W';
  if (dx === 0 && dy === 1) return 'S';
  if (dx === 0 && dy === -1) return 'N';
  return null;
}

/** 把一个 #rrggbb 颜色加上透明度，返回 rgba()。 */
export function withAlpha(hex, alpha) {
  if (!/^#[0-9a-f]{6}$/i.test(hex || '')) return `rgba(160, 170, 180, ${alpha})`;
  const n = Number.parseInt(hex.slice(1), 16);
  const r = (n >> 16) & 255;
  const g = (n >> 8) & 255;
  const b = n & 255;
  return `rgba(${r}, ${g}, ${b}, ${alpha})`;
}

export function clamp(value, min, max) {
  return Math.min(max, Math.max(min, value));
}

export function isHexColor(value) {
  return typeof value === 'string' && /^#[0-9a-f]{6}$/i.test(value);
}

/* ------------------------------------------------------------------ *
 * 类银河城地牢编辑器常量（条带模板 / 单层三角地图）
 * ------------------------------------------------------------------ */

export const FLOOR_GENERATOR_ID = 'triangle-floor';
export const FLOOR_GENERATOR_VERSION = 2;
export const STRIP_CELLS_MIN = 1;
export const STRIP_CELLS_MAX = 16;

export const STRIP_ROLES = Object.freeze([
  Object.freeze({ id: 'normal', label: '常规区域条带', short: '常规' }),
  Object.freeze({ id: 'bridge', label: '独立跨区桥接', short: '桥接' }),
]);

export const STRIP_ROLE_IDS = Object.freeze(STRIP_ROLES.map((r) => r.id));

export const SHAPE_STYLES = Object.freeze([
  Object.freeze({ id: 'mixed', label: '混合（直线+折线）' }),
  Object.freeze({ id: 'polyline', label: '折线（L/Z/S/U）' }),
  Object.freeze({ id: 'line', label: '直线走廊' }),
]);

export const SHAPE_STYLE_IDS = Object.freeze(SHAPE_STYLES.map((s) => s.id));

/** 四种内置地牢区域类型（城市、工厂、郊区、太空）。 */
export const DUNGEON_REGION_PRESETS = Object.freeze([
  Object.freeze({ id: 'city', name: '城市', shortName: '城市', color: '#d9a441', description: '街区、商铺与高架廊道' }),
  Object.freeze({ id: 'factory', name: '工厂', shortName: '工厂', color: '#d97b6c', description: '流水线、重工车间与精英防线' }),
  Object.freeze({ id: 'suburb', name: '郊区', shortName: '郊区', color: '#7fb685', description: '外围遗迹、事件频发与休整营地' }),
  Object.freeze({ id: 'space', name: '太空', shortName: '太空', color: '#6aa9c9', description: '轨道舱段、气闸与深空高压区' }),
]);

export const DUNGEON_REGION_IDS = Object.freeze(DUNGEON_REGION_PRESETS.map((r) => r.id));

/** 全局默认格子类型配置（仅包含资源值与难度值两个数值属性）。 */
export const DEFAULT_CELL_TYPES = Object.freeze([
  Object.freeze({
    id: 'empty',
    name: '通道 / 空',
    short: '空',
    icon: '·',
    color: '#6e7f91',
    resource: 0,
    difficulty: 0,
    supportsOffset: false,
    defaultOffset: 0,
    builtin: true,
  }),
  Object.freeze({
    id: 'combat',
    name: '战斗',
    short: '战斗',
    icon: '🗡',
    color: '#d97b6c',
    resource: 5,
    difficulty: 10,
    supportsOffset: true,
    defaultOffset: 0,
    builtin: true,
  }),
  Object.freeze({
    id: 'elite',
    name: '精英',
    short: '精英',
    icon: '⚔',
    color: '#c95c78',
    resource: 15,
    difficulty: 25,
    supportsOffset: true,
    defaultOffset: 2,
    builtin: true,
  }),
  Object.freeze({
    id: 'shop',
    name: '商店',
    short: '商店',
    icon: '⚖',
    color: '#d9a441',
    resource: 20,
    difficulty: 0,
    supportsOffset: false,
    defaultOffset: 0,
    builtin: true,
  }),
  Object.freeze({
    id: 'reward',
    name: '奖励',
    short: '奖励',
    icon: '◆',
    color: '#7fb685',
    resource: 25,
    difficulty: 0,
    supportsOffset: false,
    defaultOffset: 0,
    builtin: true,
  }),
  Object.freeze({
    id: 'event',
    name: '事件',
    short: '事件',
    icon: '?',
    color: '#6aa9c9',
    resource: 10,
    difficulty: 5,
    supportsOffset: false,
    defaultOffset: 0,
    builtin: true,
  }),
  Object.freeze({
    id: 'spawn',
    name: '出生点',
    short: '出生点',
    icon: '⚑',
    color: '#f4c568',
    resource: 0,
    difficulty: 0,
    supportsOffset: false,
    defaultOffset: 0,
    builtin: true,
  }),
  Object.freeze({
    id: 'rest',
    name: '休整',
    short: '休整',
    icon: '✚',
    color: '#89b8a5',
    resource: 15,
    difficulty: 0,
    supportsOffset: false,
    defaultOffset: 0,
    builtin: true,
  }),
]);

