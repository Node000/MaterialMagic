/**
 * 网格、条带与连接的基础几何工具。
 * 所有坐标都是整数格；y 轴向下。
 */

import { AXES, DIRECTIONS, DIR_IDS, clamp } from './constants.js';

export function cellKey(x, y) {
  return `${x},${y}`;
}

export function inBounds(x, y, width, height) {
  return Number.isInteger(x) && Number.isInteger(y) && x >= 0 && y >= 0 && x < width && y < height;
}

export function sideCell(x, y, dirId) {
  const dir = DIRECTIONS[dirId];
  if (!dir) return null;
  return { x: x + dir.dx, y: y + dir.dy };
}

/** 条带占据的格子序列，index 与长度一一对应。 */
export function cellsFor(origin, axis, length) {
  const info = AXES[axis];
  if (!info) return [];
  const dx = axis === 'h' ? 1 : 0;
  const dy = axis === 'v' ? 1 : 0;
  const cells = [];
  for (let i = 0; i < length; i += 1) {
    cells.push({ x: origin.x + dx * i, y: origin.y + dy * i, index: i });
  }
  return cells;
}

export function plotCells(plot) {
  return cellsFor(plot.origin, plot.axis, plot.length);
}

export function plotCenter(plot) {
  const cells = plotCells(plot);
  if (!cells.length) return { x: plot.origin?.x ?? 0, y: plot.origin?.y ?? 0 };
  const mid = cells[Math.floor(cells.length / 2)];
  return { x: mid.x + 0.5, y: mid.y + 0.5 };
}

export function plotBounds(plot) {
  const cells = plotCells(plot);
  if (!cells.length) {
    const x = plot.origin?.x ?? 0;
    const y = plot.origin?.y ?? 0;
    return { minX: x, minY: y, maxX: x, maxY: y, width: 1, height: 1 };
  }
  let minX = Infinity;
  let minY = Infinity;
  let maxX = -Infinity;
  let maxY = -Infinity;
  for (const cell of cells) {
    minX = Math.min(minX, cell.x);
    minY = Math.min(minY, cell.y);
    maxX = Math.max(maxX, cell.x);
    maxY = Math.max(maxY, cell.y);
  }
  return { minX, minY, maxX, maxY, width: maxX - minX + 1, height: maxY - minY + 1 };
}

export function mapBounds(plots) {
  const list = Array.isArray(plots) ? plots : [];
  if (!list.length) return { minX: 0, minY: 0, maxX: 0, maxY: 0, width: 1, height: 1 };
  let minX = Infinity;
  let minY = Infinity;
  let maxX = -Infinity;
  let maxY = -Infinity;
  for (const plot of list) {
    const bounds = plotBounds(plot);
    minX = Math.min(minX, bounds.minX);
    minY = Math.min(minY, bounds.minY);
    maxX = Math.max(maxX, bounds.maxX);
    maxY = Math.max(maxY, bounds.maxY);
  }
  return { minX, minY, maxX, maxY, width: maxX - minX + 1, height: maxY - minY + 1 };
}

export function cellsEqual(a, b) {
  return Boolean(a && b && a.x === b.x && a.y === b.y);
}

export function areAdjacent(a, b) {
  if (!a || !b) return false;
  return Math.abs(a.x - b.x) + Math.abs(a.y - b.y) === 1;
}

export function directionFromTo(from, to) {
  if (!from || !to) return null;
  const dx = to.x - from.x;
  const dy = to.y - from.y;
  for (const id of DIR_IDS) {
    if (DIRECTIONS[id].dx === dx && DIRECTIONS[id].dy === dy) return id;
  }
  return null;
}

export function cellIndexAt(plot, x, y) {
  const cells = plotCells(plot);
  return cells.findIndex((cell) => cell.x === x && cell.y === y);
}

export function usedSideKey(plotId, cellIndex, dirId) {
  return `${plotId}:${cellIndex}:${dirId}`;
}

export function portKey(plotId, cellIndex, dirId) {
  return `${plotId}:${cellIndex}:${dirId}`;
}

/** 依据连接表计算每个地块的度（门与交叉都计一次）。 */
export function degreeMap(map) {
  const degrees = new Map();
  for (const plot of map?.plots ?? []) degrees.set(plot.id, 0);
  for (const link of map?.links ?? []) {
    const a = link?.a?.plotId;
    const b = link?.b?.plotId;
    if (!a || !b || !degrees.has(a) || !degrees.has(b)) continue;
    degrees.set(a, degrees.get(a) + 1);
    degrees.set(b, degrees.get(b) + 1);
  }
  return degrees;
}

export function adjacentPlotIds(map) {
  const adjacency = new Map();
  for (const plot of map?.plots ?? []) adjacency.set(plot.id, new Set());
  for (const link of map?.links ?? []) {
    const a = link?.a?.plotId;
    const b = link?.b?.plotId;
    if (!a || !b || !adjacency.has(a) || !adjacency.has(b)) continue;
    adjacency.get(a).add(b);
    adjacency.get(b).add(a);
  }
  return adjacency;
}

export function plotById(map) {
  return new Map((map?.plots ?? []).map((plot) => [plot.id, plot]));
}

/** 以地块中心为准的布局质心。 */
export function layoutCenter(plots) {
  const list = Array.isArray(plots) ? plots : [];
  if (!list.length) return { x: 0, y: 0 };
  let sx = 0;
  let sy = 0;
  for (const plot of list) {
    const center = plotCenter(plot);
    sx += center.x;
    sy += center.y;
  }
  return { x: sx / list.length, y: sy / list.length };
}

export function distance(a, b) {
  return Math.hypot((a?.x ?? 0) - (b?.x ?? 0), (a?.y ?? 0) - (b?.y ?? 0));
}

/** 在地图的空位里找一个尽量靠近中心的落点；失败返回 null。 */
export function findCenteredOrigin(width, height, axis, length, center = null) {
  const cx = Math.floor(width / 2);
  const cy = Math.floor(height / 2);
  const target = center ?? { x: cx, y: cy };
  const candidates = [];
  if (axis === 'h') {
    for (let x = 0; x <= width - length; x += 1) {
      for (let y = 0; y < height; y += 1) {
        candidates.push({ x, y, d: Math.hypot(x + length / 2 - target.x, y + 0.5 - target.y) });
      }
    }
  } else {
    for (let y = 0; y <= height - length; y += 1) {
      for (let x = 0; x < width; x += 1) {
        candidates.push({ x, y, d: Math.hypot(x + 0.5 - target.x, y + length / 2 - target.y) });
      }
    }
  }
  candidates.sort((a, b) => a.d - b.d);
  return candidates.length ? { x: candidates[0].x, y: candidates[0].y } : null;
}

export function clampOrigin(origin, axis, length, width, height) {
  const maxX = axis === 'h' ? width - length : width - 1;
  const maxY = axis === 'v' ? height - length : height - 1;
  return {
    x: clamp(origin.x, 0, Math.max(0, maxX)),
    y: clamp(origin.y, 0, Math.max(0, maxY)),
  };
}
