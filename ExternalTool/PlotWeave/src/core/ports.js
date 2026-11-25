/**
 * 地块的连接口（port）计算。
 *
 * 一个 port 是“某个地块的某个格子朝向某个方向的一个可用接口”。
 * 生成器只从 port 向外长出新的地块；port 的目标格必须为空。
 */

import { AXES, DIRECTIONS, DIR_IDS, perpendicularDirs } from './constants.js';
import { cellKey, inBounds, portKey, usedSideKey } from './geometry.js';

const END_ANCHORS = new Set(['ends', 'both', 'any']);
const MIDDLE_ANCHORS = new Set(['middle', 'both', 'any']);

export function splitAnchors(mode) {
  return {
    ends: END_ANCHORS.has(mode),
    middle: MIDDLE_ANCHORS.has(mode),
    any: mode === 'any',
    both: mode === 'both',
  };
}

/**
 * 判断某个格子朝某个方向的侧面是否允许连接。
 * @param {object} kind 地块种类
 * @param {'h'|'v'} axis 条带轴向
 * @param {number} length 条带长度
 * @param {number} cellIndex 格子下标
 * @param {string} dirId 从该格朝外的方向
 */
export function sideIsAllowed(kind, axis, length, cellIndex, dirId) {
  const anchors = splitAnchors(kind?.connections?.anchors ?? 'ends');
  const dir = DIRECTIONS[dirId];
  if (!dir) return false;

  if (length === 1) {
    return anchors.ends || anchors.both || anchors.any;
  }

  const isEnd = cellIndex === 0 || cellIndex === length - 1;
  const axisInfo = AXES[axis];
  if (!axisInfo) return false;

  if (isEnd) {
    const towardNeighbor = cellIndex === 0 ? axisInfo.dir : axisInfo.negative;
    if (dirId === towardNeighbor) return false;
    if (anchors.any) return true;
    if (anchors.ends) return true;
    if (anchors.both) {
      const outward = cellIndex === 0 ? axisInfo.negative : axisInfo.dir;
      return dirId === outward;
    }
    return false;
  }

  if (anchors.middle || anchors.both || anchors.any) {
    return dirId !== AXES[axis].dir && dirId !== AXES[axis].negative;
  }
  return false;
}

/**
 * 收集某个地块当前所有可用接口。
 *
 * @param {object} options
 * @param {object} options.plot 地块实例
 * @param {object} options.kind 地块种类
 * @param {number} options.width 地图宽
 * @param {number} options.height 地图高
 * @param {Set<string>} [options.usedSides] 已经用作门的 side key
 * @param {number} [options.degree] 当前度
 * @param {(x:number,y:number)=>boolean} [options.isEmpty] 目标格是否为空
 * @returns {Array<object>}
 */
export function portsForPlot({
  plot,
  kind,
  width,
  height,
  usedSides = new Set(),
  degree = 0,
  isEmpty = () => true,
} = {}) {
  if (!plot || !kind) return [];
  const max = Math.max(0, Number(kind.connections?.max) || 0);
  if (degree >= max) return [];

  const cells = [];
  for (let index = 0; index < plot.length; index += 1) {
    cells.push({
      index,
      x: plot.axis === 'h' ? plot.origin.x + index : plot.origin.x,
      y: plot.axis === 'v' ? plot.origin.y + index : plot.origin.y,
    });
  }

  const ports = [];
  for (const cell of cells) {
    for (const dirId of DIR_IDS) {
      if (!sideIsAllowed(kind, plot.axis, plot.length, cell.index, dirId)) continue;
      if (usedSides.has(usedSideKey(plot.id, cell.index, dirId))) continue;
      const dir = DIRECTIONS[dirId];
      const nx = cell.x + dir.dx;
      const ny = cell.y + dir.dy;
      if (!inBounds(nx, ny, width, height)) continue;
      if (!isEmpty(nx, ny)) continue;
      ports.push({
        key: portKey(plot.id, cell.index, dirId),
        plotId: plot.id,
        cellIndex: cell.index,
        x: cell.x,
        y: cell.y,
        dir: dirId,
        nx,
        ny,
      });
    }
  }
  return ports;
}

/** 一个格子的邻居键集合，便于快速检查交叉 / 相邻。 */
export function neighborKeys(x, y) {
  return DIR_IDS.map((dirId) => cellKey(x + DIRECTIONS[dirId].dx, y + DIRECTIONS[dirId].dy));
}

export { perpendicularDirs };
