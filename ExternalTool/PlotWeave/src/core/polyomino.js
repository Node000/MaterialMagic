/**
 * 多格异形条带（Polyomino）几何工具层。
 *
 * 条带不再局限于直线，可以是任意四连通多格集合（直线、L/Z/T/U 折线、矩形等）。
 * 所有函数均为纯逻辑，不依赖 DOM。
 */

import { DIRECTIONS, DIR_IDS } from './constants.js';
import { exactOpenEdgeKey, isOpenChain, rotateOpenDirs, stripNeighbors } from './strip-edges.js';

export function coordKey(x, y) {
  return `${x},${y}`;
}

/** 计算格集合的包围盒。支持 {rx,ry} 或 {x,y}。 */
export function boundsOfCells(cells) {
  const list = Array.isArray(cells) ? cells : [];
  if (!list.length) {
    return { minX: 0, minY: 0, maxX: 0, maxY: 0, width: 0, height: 0 };
  }
  let minX = Infinity;
  let minY = Infinity;
  let maxX = -Infinity;
  let maxY = -Infinity;
  for (const cell of list) {
    const x = Number.isInteger(cell.x) ? cell.x : cell.rx;
    const y = Number.isInteger(cell.y) ? cell.y : cell.ry;
    if (x < minX) minX = x;
    if (y < minY) minY = y;
    if (x > maxX) maxX = x;
    if (y > maxY) maxY = y;
  }
  return {
    minX,
    minY,
    maxX,
    maxY,
    width: maxX - minX + 1,
    height: maxY - minY + 1,
  };
}

/** 计算格集合的质心（格中心坐标）。 */
export function centroidOfCells(cells) {
  const list = Array.isArray(cells) ? cells : [];
  if (!list.length) return { x: 0, y: 0 };
  let sx = 0;
  let sy = 0;
  for (const cell of list) {
    const x = Number.isInteger(cell.x) ? cell.x : cell.rx;
    const y = Number.isInteger(cell.y) ? cell.y : cell.ry;
    sx += x + 0.5;
    sy += y + 0.5;
  }
  return { x: sx / list.length, y: sy / list.length };
}

/**
 * 归一化模板相对坐标：
 * - 去重（保留先出现的格）；
 * - 平移到 min(rx)=0, min(ry)=0；
 * - 按 ry 升序、rx 升序排序，保证稳定序列化。
 */
export function normalizeCells(cells) {
  const seen = new Set();
  const dedup = [];
  for (const raw of Array.isArray(cells) ? cells : []) {
    if (!raw || typeof raw !== 'object') continue;
    const rx = Math.round(Number(raw.rx ?? raw.x ?? 0));
    const ry = Math.round(Number(raw.ry ?? raw.y ?? 0));
    if (!Number.isFinite(rx) || !Number.isFinite(ry)) continue;
    const key = coordKey(rx, ry);
    if (seen.has(key)) continue;
    seen.add(key);
    dedup.push({ ...raw, rx, ry });
  }
  if (!dedup.length) return [];

  let minX = Infinity;
  let minY = Infinity;
  for (const cell of dedup) {
    if (cell.rx < minX) minX = cell.rx;
    if (cell.ry < minY) minY = cell.ry;
  }
  return dedup
    .map((cell) => ({
      ...cell,
      rx: cell.rx - minX,
      ry: cell.ry - minY,
    }))
    .sort((a, b) => a.ry - b.ry || a.rx - b.rx);
}

/** 检查格集合是否四连通（空集合返回 false）。 */
export function isFourConnected(cells) {
  const list = Array.isArray(cells) ? cells : [];
  if (!list.length) return false;
  if (list.length === 1) return true;

  const map = new Map();
  for (const cell of list) {
    const x = Number.isInteger(cell.x) ? cell.x : cell.rx;
    const y = Number.isInteger(cell.y) ? cell.y : cell.ry;
    map.set(coordKey(x, y), { x, y });
  }
  const first = map.values().next().value;
  const visited = new Set([coordKey(first.x, first.y)]);
  const queue = [first];
  while (queue.length) {
    const cur = queue.shift();
    for (const dirId of DIR_IDS) {
      const d = DIRECTIONS[dirId];
      const nk = coordKey(cur.x + d.dx, cur.y + d.dy);
      if (map.has(nk) && !visited.has(nk)) {
        visited.add(nk);
        queue.push(map.get(nk));
      }
    }
  }
  return visited.size === map.size;
}

/** 判断移除指定相对坐标格后，剩余格是否仍四连通（至少剩 1 格）。 */
export function canRemoveCellKeepingConnected(cells, rx, ry) {
  const rest = (cells || []).filter((c) => c.rx !== rx || c.ry !== ry);
  return rest.length > 0 && isFourConnected(rest);
}

/** 检测格集合中是否存在任意 2×2 实心块（用于禁止矩形/团块）。 */
export function hasTwoByTwoBlock(cells) {
  const set = new Set();
  for (const c of cells || []) {
    const x = Number.isInteger(c.x) ? c.x : c.rx;
    const y = Number.isInteger(c.y) ? c.y : c.ry;
    set.add(coordKey(x, y));
  }
  for (const c of cells || []) {
    const x = Number.isInteger(c.x) ? c.x : c.rx;
    const y = Number.isInteger(c.y) ? c.y : c.ry;
    if (
      set.has(coordKey(x + 1, y)) &&
      set.has(coordKey(x, y + 1)) &&
      set.has(coordKey(x + 1, y + 1))
    ) {
      return true;
    }
  }
  return false;
}

/**
 * 判断格集合是否为「无矩形、无分叉、明确首尾的单链条带」：
 * - 四连通；
 * - 无任意 2×2 实心块；
 * - 1 格直接合法；格数 >= 2 时，恰有 2 个度为 1 的首尾端点格，其余格度为 2。
 */
export function isSimpleChain(cells) {
  const list = Array.isArray(cells) ? cells : [];
  if (!list.length) return false;
  if (list.length === 1) return true;
  if (!isFourConnected(list)) return false;
  if (hasTwoByTwoBlock(list)) return false;

  const set = new Set();
  const pts = [];
  for (const c of list) {
    const x = Number.isInteger(c.x) ? c.x : c.rx;
    const y = Number.isInteger(c.y) ? c.y : c.ry;
    const k = coordKey(x, y);
    if (set.has(k)) return false;
    set.add(k);
    pts.push({ x, y });
  }

  let endpoints = 0;
  for (const pt of pts) {
    let deg = 0;
    for (const dirId of DIR_IDS) {
      const d = DIRECTIONS[dirId];
      if (set.has(coordKey(pt.x + d.dx, pt.y + d.dy))) deg += 1;
    }
    if (deg === 1) endpoints += 1;
    else if (deg !== 2) return false;
  }
  return endpoints === 2;
}

/** 判断在指定相对坐标添加一个新格后，是否仍保持无矩形单链。 */
export function canAddCellKeepingChain(cells, rx, ry) {
  const list = Array.isArray(cells) ? cells : [];
  if (list.some((c) => (c.rx ?? c.x) === rx && (c.ry ?? c.y) === ry)) return false;
  return isSimpleChain([...list, { rx, ry }]);
}

/** 判断移除指定相对坐标格后，是否仍保持无矩形单链（至少剩 1 格）。 */
export function canRemoveCellKeepingChain(cells, rx, ry) {
  const rest = (cells || []).filter((c) => (c.rx ?? c.x) !== rx || (c.ry ?? c.y) !== ry);
  return rest.length > 0 && isSimpleChain(rest);
}

/**
 * 计算单链条带从「首格」到「尾格」的顺序信息与每格的端点倾向 endFactor ∈ [0, 1]：
 * - 首格、尾格：endFactor = 1.0；
 * - 最中间格：endFactor = 0.0；
 * - 非单链兜底：按度为 1 的叶格=1.0、其余=0.25。
 * 返回 Map<index, { chainIndex, total, isHead, isTail, endFactor }>
 */
export function computeChainOrderMap(cells) {
  const list = Array.isArray(cells) ? cells : [];
  const info = new Map();
  const n = list.length;
  if (n === 0) return info;
  if (n === 1) {
    info.set(0, { chainIndex: 0, total: 1, isHead: true, isTail: true, endFactor: 1 });
    return info;
  }

  const neighbors = stripNeighbors(list);
  const endpoints = list.map((cell, idx) => ({ idx, x: cell.x ?? cell.rx, y: cell.y ?? cell.ry }))
    .filter(({ idx }) => neighbors[idx].length === 1);

  if (isOpenChain(list, neighbors)) {
    endpoints.sort((a, b) => a.y - b.y || a.x - b.x);
    const ordered = [];
    const visited = new Set();
    let cur = endpoints[0].idx;
    while (cur !== undefined && !visited.has(cur)) {
      visited.add(cur);
      ordered.push(cur);
      const next = neighbors[cur].find(({ index }) => !visited.has(index))?.index;
      cur = next;
    }
    const len = ordered.length;
    ordered.forEach((cellIdx, pos) => {
      const endFactor = len <= 1 ? 1 : Math.abs((2 * pos) / (len - 1) - 1);
      info.set(cellIdx, {
        chainIndex: pos,
        total: len,
        isHead: pos === 0,
        isTail: pos === len - 1,
        endFactor: Math.round(endFactor * 1000) / 1000,
      });
    });
    return info;
  }

  // 非单链没有首尾，接口评分使用中性权重。
  list.forEach((_, idx) => {
    info.set(idx, {
      chainIndex: idx,
      total: n,
      isHead: false,
      isTail: false,
      endFactor: 0.5,
    });
  });
  return info;
}

/** 顺时针旋转相对坐标（rotation: 0=0°, 1=90°, 2=180°, 3=270°）。 */
export function rotatePointCW(rx, ry, rotation) {
  const r = ((rotation % 4) + 4) % 4;
  if (r === 1) return { rx: -ry, ry: rx };
  if (r === 2) return { rx: -rx, ry: -ry };
  if (r === 3) return { rx: ry, ry: -rx };
  return { rx, ry };
}

/**
 * 旋转格集合并归一化至第一象限（minX=0, minY=0）。
 * 保留原始 cellIndex，确保旋转后各格的类型与难度偏移一一对应。
 */
export function rotateCells(cells, rotation = 0) {
  const list = Array.isArray(cells) ? cells : [];
  if (!list.length) return [];
  const rotated = list.map((cell, index) => {
    const rx = Number.isInteger(cell.rx) ? cell.rx : cell.x;
    const ry = Number.isInteger(cell.ry) ? cell.ry : cell.y;
    const pt = rotatePointCW(rx, ry, rotation);
    return {
      ...cell,
      sourceIndex: cell.sourceIndex ?? index,
      ...(Array.isArray(cell.openDirs) ? { openDirs: rotateOpenDirs(cell.openDirs, rotation) } : {}),
      rx: pt.rx,
      ry: pt.ry,
    };
  });
  let minX = Infinity;
  let minY = Infinity;
  for (const cell of rotated) {
    if (cell.rx < minX) minX = cell.rx;
    if (cell.ry < minY) minY = cell.ry;
  }
  return rotated.map((cell) => ({
    ...cell,
    rx: cell.rx - minX,
    ry: cell.ry - minY,
  }));
}

/** 特定朝向的格坐标串（仅看几何，不看格子内容）。 */
export function exactShapeKey(cells) {
  const coords = (cells || [])
    .map((c) => {
      const x = Number.isInteger(c.rx) ? c.rx : c.x;
      const y = Number.isInteger(c.ry) ? c.ry : c.y;
      return { x, y };
    })
    .sort((a, b) => a.y - b.y || a.x - b.x);
  if (!coords.length) return '';
  let minX = Infinity;
  let minY = Infinity;
  for (const pt of coords) {
    if (pt.x < minX) minX = pt.x;
    if (pt.y < minY) minY = pt.y;
  }
  return coords.map((pt) => `${pt.x - minX},${pt.y - minY}`).join('|');
}

/** 旋转不变的规范形状签名（4 个旋转中字典序最小者）。 */
export function shapeSignature(cells) {
  if (!cells || !cells.length) return '';
  let best = null;
  for (let r = 0; r < 4; r += 1) {
    const key = exactShapeKey(rotateCells(cells, r));
    if (best === null || key < best) best = key;
  }
  return best || '';
}

/**
 * 预计算去重后的旋转变体。
 * 若 allowRotate=false，则只返回 rotation=0。
 * 注意：不同格内容对称但几何相同的旋转是否需要区分？
 * 对于相同几何形状但内容非对称的模板，保留不同旋转可丰富朝向；
 * 因此若 preserveContentOrientation=true 则按 (坐标+type) 去重，否则按纯几何去重。
 */
export function uniqueRotations(cells, allowRotate = true, { preserveContentOrientation = false } = {}) {
  const rotations = allowRotate ? [0, 1, 2, 3] : [0];
  const seen = new Set();
  const variants = [];
  for (const rotation of rotations) {
    const rotated = rotateCells(cells, rotation);
    const orientationKey = preserveContentOrientation
      ? rotated
          .map((c) => `${c.rx},${c.ry}:${c.type || ''}:${c.difficultyOffset || 0}`)
          .sort()
          .join('|')
      : exactShapeKey(rotated);
    const dedupKey = `${orientationKey}/${exactOpenEdgeKey(rotated)}`;
    if (seen.has(dedupKey)) continue;
    seen.add(dedupKey);
    const bounds = boundsOfCells(rotated);
    variants.push({
      rotation,
      shapeKey: exactShapeKey(rotated),
      cells: rotated,
      width: bounds.width,
      height: bounds.height,
    });
  }
  return variants;
}

/** 将模板旋转变体放到世界坐标 origin=(ox, oy)，输出带 index、x、y 的格数组。 */
export function placeCellsAt(rotatedCells, ox, oy) {
  return rotatedCells.map((cell, index) => ({
    ...cell,
    index,
    x: ox + cell.rx,
    y: oy + cell.ry,
  }));
}

/** 分析条带形状类别，供 UI 标签显示。 */
export function analyzeShapeKind(cells) {
  const list = normalizeCells(cells);
  if (list.length <= 1) return { id: 'single', label: '单格' };
  const b = boundsOfCells(list);
  if (b.width === 1 || b.height === 1) return { id: 'line', label: `直线 ${list.length}格` };
  if (hasTwoByTwoBlock(list)) return { id: 'rect', label: `含矩形块 ${list.length}格` };
  if (isSimpleChain(list)) return { id: 'polyline', label: `折线 ${list.length}格` };
  return { id: 'branch', label: `分叉 ${list.length}格` };
}

/** 计算两组世界格之间的所有重叠格。 */
export function overlapCells(cellsA, cellsB) {
  const mapA = new Map();
  cellsA.forEach((cell, index) => mapA.set(coordKey(cell.x, cell.y), { cell, index }));
  const overlaps = [];
  cellsB.forEach((cell, bIndex) => {
    const hit = mapA.get(coordKey(cell.x, cell.y));
    if (hit) {
      overlaps.push({
        aIndex: hit.index,
        bIndex,
        x: cell.x,
        y: cell.y,
        aCell: hit.cell,
        bCell: cell,
      });
    }
  });
  return overlaps;
}

/** 计算两组世界格之间的所有正交相邻格对（dir 从 A 指向 B）。 */
export function adjacentCellPairs(cellsA, cellsB) {
  const mapB = new Map();
  cellsB.forEach((cell, index) => mapB.set(coordKey(cell.x, cell.y), { cell, index }));
  const pairs = [];
  cellsA.forEach((cell, aIndex) => {
    for (const dirId of DIR_IDS) {
      const d = DIRECTIONS[dirId];
      const hit = mapB.get(coordKey(cell.x + d.dx, cell.y + d.dy));
      if (hit) {
        pairs.push({
          aIndex,
          bIndex: hit.index,
          dir: dirId,
          aCell: cell,
          bCell: hit.cell,
        });
      }
    }
  });
  return pairs;
}

/**
 * 收集一组世界格对外暴露的所有“可开门边界口”。
 * 每个 port 表示：从 ownerCell=(x,y) 朝 dir 跨一步到达空格 (nx, ny)。
 */
export function exteriorDoorPorts(worldCells, occupiedSet) {
  const ports = [];
  const seen = new Set();
  for (const cell of worldCells) {
    for (const dirId of DIR_IDS) {
      const d = DIRECTIONS[dirId];
      const nx = cell.x + d.dx;
      const ny = cell.y + d.dy;
      const nKey = coordKey(nx, ny);
      if (occupiedSet.has(nKey)) continue;
      const pKey = `${cell.stripId || ''}:${cell.index}:${dirId}`;
      if (seen.has(pKey)) continue;
      seen.add(pKey);
      ports.push({
        stripId: cell.stripId || null,
        cellIndex: cell.index,
        x: cell.x,
        y: cell.y,
        dir: dirId,
        nx,
        ny,
        cell,
      });
    }
  }
  return ports;
}
