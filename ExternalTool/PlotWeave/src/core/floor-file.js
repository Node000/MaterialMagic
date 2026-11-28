import { DIRECTIONS } from './constants.js';
import { edgeKey, positionKey, cellX, cellY, stripNeighbors } from './strip-edges.js';

export const FLOOR_FILE_VERSION = 1;
export const FLOOR_KIND = 'plotweave-floor-map';
const isInt = (value) => Number.isInteger(value) && value >= -2147483647 && value <= 2147483646;
const checkedPosition = (value) => value && isInt(value.x) && isInt(value.y);

// Flatten a generated/editor map. Only strip openings and real doors remove walls.
export function buildFloorGrid(map) {
  const cells = new Map();
  const edges = new Map();
  const strips = new Map(map.strips.map((s) => [s.id, s]));
  const addEdge = (a, b, type) => {
    const x = Math.min(a.x, b.x), y = Math.min(a.y, b.y);
    const dir = a.x === b.x ? 'S' : 'E';
    const key = edgeKey(x, y, dir);
    if (!edges.has(key) || type === 'door') edges.set(key, { x, y, dir, type });
  };
  for (const strip of map.strips) {
    const neighbors = stripNeighbors(strip.cells);
    for (const [i, cell] of strip.cells.entries()) {
      const { x, y } = cell;
      const key = positionKey(x, y);
      const previous = cells.get(key);
      const stripIds = [...(previous?.stripIds || []), strip.id];
      if (!previous || (previous.type === 'empty' && cell.type !== 'empty')) {
        cells.set(key, { x, y, type: cell.type, ...(cell.enemyLevel != null ? { enemyLevel: cell.enemyLevel } : {}), stripIds });
      } else previous.stripIds = stripIds;
      for (const { index } of neighbors[i]) if (i < index) addEdge(cell, strip.cells[index], 'open');
    }
  }
  for (const link of map.links) {
    if (link.type !== 'door') continue;
    const a = strips.get(link.a.stripId)?.cells[link.a.cellIndex];
    const b = strips.get(link.b.stripId)?.cells[link.b.cellIndex];
    if (!a || !b || Math.abs(a.x - b.x) + Math.abs(a.y - b.y) !== 1) throw Error(`门 ${link.id} 未连接相邻地板格。`);
    addEdge(a, b, 'door');
  }
  const compare = (a, b) => a.y - b.y || a.x - b.x;
  return { cells: [...cells.values()].sort(compare), edges: [...(map.physicalEdges || edges.values())].sort((a, b) => compare(a, b) || a.dir.localeCompare(b.dir)) };
}

export function validateFloorFile(file) {
  if (file?.kind !== FLOOR_KIND || file.schemaVersion !== FLOOR_FILE_VERSION) throw Error('不支持的地图格式或 schemaVersion。');
  if (typeof file.id !== 'string' || !file.id.trim()) throw Error('地图缺少 id。');
  if (!Array.isArray(file.cells) || !file.cells.length || !Array.isArray(file.edges)) throw Error('地图需要 cells 和 edges 数组。');
  const neighbors = new Map();
  for (const cell of file.cells) {
    if (!checkedPosition(cell)) throw Error('格坐标必须是有效整数。');
    const key = positionKey(cell.x, cell.y);
    if (neighbors.has(key)) throw Error(`格 ${key} 重复。`);
    neighbors.set(key, []);
  }
  const start = checkedPosition(file.spawn) && positionKey(file.spawn.x, file.spawn.y);
  if (!neighbors.has(start)) throw Error('出生点不在有效地板上。');
  const seenEdges = new Set();
  for (const edge of file.edges) {
    if (!checkedPosition(edge) || !['E', 'S'].includes(edge.dir) || !['open', 'door'].includes(edge.type)) throw Error('边必须使用 E/S 和 open/door。');
    const key = edgeKey(edge.x, edge.y, edge.dir);
    if (seenEdges.has(key)) throw Error(`边 ${key} 重复。`);
    seenEdges.add(key);
    const { dx, dy } = DIRECTIONS[edge.dir];
    const a = positionKey(edge.x, edge.y), b = positionKey(edge.x + dx, edge.y + dy);
    if (!neighbors.has(a) || !neighbors.has(b)) throw Error(`边 ${key} 的两端必须有地板。`);
    neighbors.get(a).push(b);
    neighbors.get(b).push(a);
  }
  const queue = [start], visited = new Set(queue);
  for (let i = 0; i < queue.length; i += 1) {
    for (const next of neighbors.get(queue[i])) if (!visited.has(next)) { visited.add(next); queue.push(next); }
  }
  if (visited.size !== neighbors.size) throw Error('存在从出生点不可达的地板格。');
  return file;
}

export function makeFloorFile(map) {
  const grid = buildFloorGrid(map);
  const { stats, issues, warnings, errors, cells, edges, strips, physicalEdges, ...metadata } = map;
  // Keep editor membership and generation metadata for round trips, not duplicate open edges.
  const file = {
    ...metadata, kind: FLOOR_KIND, schemaVersion: FLOOR_FILE_VERSION, ...grid,
    strips: strips.map((strip) => ({ ...strip, cells: strip.cells.map(({ openDirs, ...cell }) => cell) })),
  };
  return validateFloorFile(file);
}

// Canonical cells/edges win on reload. Original overlapping content stays on its owning strip.
export function applyFloorFile(file) {
  validateFloorFile(file);
  const { cells, edges, schemaVersion, ...map } = JSON.parse(JSON.stringify(file));
  map.physicalEdges = edges;
  const strips = new Map((map.strips || []).map((s) => [s.id, {
    ...s,
    familyId: s.familyId || s.templateId || s.id,
    cells: [],
  }]));
  const oldOwners = new Map();
  for (const strip of map.strips || []) for (const cell of strip.cells) {
    const key = positionKey(cell.x, cell.y);
    if (!oldOwners.has(key)) oldOwners.set(key, []);
    oldOwners.get(key).push({ id: strip.id, cell });
  }
  const owners = new Map();
  for (const cell of cells) {
    const key = positionKey(cell.x, cell.y);
    const old = oldOwners.get(key) || [];
    const ids = [...new Set(cell.stripIds?.length ? cell.stripIds : old.length ? old.map((o) => o.id) : ['grid'])];
    if (ids.length > 2) throw Error(`格 ${key} 的条带归属超过两个。`);
    owners.set(key, []);
    const contentOwner = old.find((o) => o.cell.type !== 'empty' && ids.includes(o.id))?.id || ids[0];
    for (const id of ids) {
      if (typeof id !== 'string' || !id) throw Error(`格 ${key} 的条带 ID 无效。`);
      if (!strips.has(id)) strips.set(id, { id, familyId: id, templateId: id, templateName: id, role: 'normal', resource: 0, difficulty: 0, cells: [] });
      const strip = strips.get(id);
      const previous = old.find((o) => o.id === id)?.cell;
      const type = id === contentOwner ? (cell.type || 'empty') : 'empty';
      const next = { ...previous, x: cell.x, y: cell.y, index: strip.cells.length, type, enemyLevel: id === contentOwner ? cell.enemyLevel : null, openDirs: [] };
      strip.cells.push(next);
      owners.get(key).push({ strip, cell: next });
    }
  }
  map.strips = [...strips.values()].filter((s) => s.cells.length);
  for (const strip of map.strips) {
    const x = Math.min(...strip.cells.map(cellX)), y = Math.min(...strip.cells.map(cellY));
    strip.origin = { x, y };
    for (const cell of strip.cells) { cell.rx = cell.x - x; cell.ry = cell.y - y; }
  }
  const oldLinks = file.links || [];
  const oldStrips = new Map((file.strips || []).map((s) => [s.id, s]));
  const oldDoors = new Map();
  for (const link of oldLinks) {
    if (link.type !== 'door' || !DIRECTIONS[link.dir]) continue;
    const cell = oldStrips.get(link.a.stripId)?.cells[link.a.cellIndex];
    if (!cell) continue;
    const key = edgeKey(cell.x, cell.y, link.dir);
    if (!oldDoors.has(key)) oldDoors.set(key, []);
    oldDoors.get(key).push(link);
  }
  const endpoint = (owner) => ({ stripId: owner.strip.id, cellIndex: owner.cell.index });
  map.links = [];
  const usedIds = new Set(oldLinks.map((l) => l.id));
  let serial = 0;
  const newId = () => { let id; do { id = `grid_${++serial}`; } while (usedIds.has(id)); return id; };
  for (const [key, list] of owners) {
    if (list.length !== 2) continue;
    const [a, b] = list;
    const old = oldLinks.find((l) => l.type === 'cross' && positionKey(l.cell?.x, l.cell?.y) === key);
    map.links.push({ id: old?.id || newId(), type: 'cross', a: endpoint(a), b: endpoint(b), cell: { x: a.cell.x, y: a.cell.y } });
  }
  for (const edge of edges) {
    const { dx, dy, opposite } = DIRECTIONS[edge.dir];
    const left = owners.get(positionKey(edge.x, edge.y)), right = owners.get(positionKey(edge.x + dx, edge.y + dy));
    if (edge.type === 'open') {
      for (const a of left) for (const b of right) if (a.strip === b.strip) {
        a.cell.openDirs.push(edge.dir); b.cell.openDirs.push(opposite);
      }
    } else {
      const matches = oldDoors.get(edgeKey(edge.x, edge.y, edge.dir)) || [];
      let restored = false;
      for (const old of matches) {
        const sameDirection = old.dir === edge.dir;
        const a = left.find((o) => o.strip.id === (sameDirection ? old.a : old.b).stripId);
        const b = right.find((o) => o.strip.id === (sameDirection ? old.b : old.a).stripId);
        if (!a || !b) continue;
        map.links.push({ ...old, a: endpoint(a), b: endpoint(b), dir: edge.dir });
        restored = true;
      }
      if (!restored) {
        const a = left[0], b = right.find((o) => o.strip !== a.strip) || right[0];
        map.links.push({ id: newId(), type: 'door', a: endpoint(a), b: endpoint(b), dir: edge.dir });
      }
    }
  }
  const sp = owners.get(positionKey(map.spawn.x, map.spawn.y));
  const start = sp.find((o) => o.strip.id === map.spawn.stripId) || sp[0];
  map.spawn = { ...map.spawn, ...endpoint(start) };
  map.budget ||= { target: {}, actual: {} };
  map.budget.target ||= {};
  return map;
}
