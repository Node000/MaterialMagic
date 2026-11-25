import { DIRECTIONS, DIR_IDS } from './constants.js';

const key = (x, y) => `${x},${y}`;
export const positionKey = key;
export const cellX = (c) => c.x ?? c.rx;
export const cellY = (c) => c.y ?? c.ry;

export function edgeAt(x, y, dir) {
  if (dir === 'W') return { x: x - 1, y, dir: 'E' };
  if (dir === 'N') return { x, y: y - 1, dir: 'S' };
  return { x, y, dir };
}

export function edgeKey(x, y, dir) {
  const edge = edgeAt(x, y, dir);
  return `${edge.x},${edge.y},${edge.dir}`;
}

export function rotateOpenDirs(dirs, rotation) {
  const clockwise = ['N', 'E', 'S', 'W'];
  return dirs.map((dir) => clockwise[(clockwise.indexOf(dir) + rotation + 4) % 4]).sort();
}

export function stripNeighbors(cells) {
  const byPosition = new Map(cells.map((c, i) => [key(cellX(c), cellY(c)), i]));
  return cells.map((cell) => DIR_IDS.flatMap((dir) => {
    const { dx, dy, opposite } = DIRECTIONS[dir];
    const index = byPosition.get(key(cellX(cell) + dx, cellY(cell) + dy));
    if (index === undefined) return [];
    const other = cells[index];
    if (cell.openDirs !== undefined && (!Array.isArray(cell.openDirs) || !cell.openDirs.includes(dir))) return [];
    if (other.openDirs !== undefined && (!Array.isArray(other.openDirs) || !other.openDirs.includes(opposite))) return [];
    return [{ dir, index }];
  }));
}

export function hasConnectedOpenEdges(cells, neighbors = stripNeighbors(cells)) {
  if (!cells.length) return false;
  const pending = [0];
  const visited = new Set(pending);
  for (let i = 0; i < pending.length; i += 1) {
    for (const { index } of neighbors[pending[i]]) {
      if (visited.has(index)) continue;
      visited.add(index);
      pending.push(index);
    }
  }
  return visited.size === cells.length;
}

export function isOpenChain(cells, neighbors = stripNeighbors(cells)) {
  if (cells.length === 1) return true;
  return hasConnectedOpenEdges(cells, neighbors) && neighbors.filter((n) => n.length === 1).length === 2
    && neighbors.every((n) => n.length === 1 || n.length === 2);
}

export function validateOpenDirs(cells) {
  const byPosition = new Map(cells.map((c) => [key(cellX(c), cellY(c)), c]));
  for (const cell of cells) {
    if (cell.openDirs === undefined) continue;
    if (!Array.isArray(cell.openDirs) || new Set(cell.openDirs).size !== cell.openDirs.length) return false;
    for (const dir of cell.openDirs) {
      const d = Object.hasOwn(DIRECTIONS, dir) && DIRECTIONS[dir];
      if (!d) return false;
      const opposite = byPosition.get(key(cellX(cell) + d.dx, cellY(cell) + d.dy))?.openDirs;
      if (!Array.isArray(opposite) || !opposite.includes(d.opposite)) return false;
    }
  }
  return true;
}

export function explicitOpenCells(cells) {
  const neighbors = stripNeighbors(cells);
  return cells.map((c, i) => ({ ...c, openDirs: neighbors[i].map((n) => n.dir).sort() }));
}

export function toggleStripEdge(cells, x, y, dir) {
  const d = DIRECTIONS[dir];
  const a = cells.find((c) => cellX(c) === x && cellY(c) === y);
  const b = cells.find((c) => cellX(c) === x + d.dx && cellY(c) === y + d.dy);
  if (!a || !b) return null;
  const wasOpen = a.openDirs.includes(dir);
  const next = cells.map((c) => {
    const side = c === a ? dir : c === b ? d.opposite : null;
    if (!side) return c;
    const openDirs = wasOpen ? c.openDirs.filter((s) => s !== side) : [...c.openDirs, side].sort();
    return { ...c, openDirs };
  });
  return hasConnectedOpenEdges(next) ? next : null;
}

export function appendStripCell(cells, newCell) {
  const x = cellX(newCell), y = cellY(newCell);
  if (cells.some((c) => cellX(c) === x && cellY(c) === y)) return null;
  const adjacent = cells.filter((c) => Math.abs(cellX(c) - x) + Math.abs(cellY(c) - y) === 1);
  const latest = adjacent.reduce((a, b) => !a || b.placedOrder > a.placedOrder ? b : a, null);
  if (cells.length && !latest) return null;
  const added = { ...newCell, openDirs: [], placedOrder: 1 + Math.max(-1, ...cells.map((c) => c.placedOrder)) };
  if (!latest) return [added];
  const dir = DIR_IDS.find((side) => x + DIRECTIONS[side].dx === cellX(latest) && y + DIRECTIONS[side].dy === cellY(latest));
  added.openDirs = [dir];
  return [...cells.map((c) => c === latest ? { ...c, openDirs: [...c.openDirs, DIRECTIONS[dir].opposite].sort() } : c), added];
}

export function removeStripCell(cells, x, y) {
  const next = cells.filter((c) => cellX(c) !== x || cellY(c) !== y).map((c) => ({
    ...c,
    openDirs: c.openDirs.filter((dir) => cellX(c) + DIRECTIONS[dir].dx !== x || cellY(c) + DIRECTIONS[dir].dy !== y),
  }));
  return hasConnectedOpenEdges(next) ? next : null;
}

// Occupancy and edge layout both matter when changing a placed strip.
export function exactOpenEdgeKey(cells) {
  if (!cells.length) return '';
  const minX = Math.min(...cells.map(cellX)), minY = Math.min(...cells.map(cellY));
  return explicitOpenCells(cells).flatMap((cell) => cell.openDirs.filter((d) => d === 'E' || d === 'S')
    .map((d) => edgeKey(cellX(cell) - minX, cellY(cell) - minY, d))).sort().join('|');
}

export function straightAxis(cells, index, neighbors = stripNeighbors(cells)) {
  const dirs = neighbors[index]?.map((n) => n.dir);
  if (dirs?.length !== 2 || DIRECTIONS[dirs[0]].opposite !== dirs[1]) return null;
  return dirs.includes('E') ? 'h' : 'v';
}

// Legacy cross shapes stay unchanged unless a strip departs from geometric adjacency.
export function hasInternalWalls(cells) {
  return exactOpenEdgeKey(cells) !== exactOpenEdgeKey(cells.map(({ openDirs, ...cell }) => cell));
}

export function needsStrictCross(cells) {
  return hasInternalWalls(cells) || !isOpenChain(cells);
}
