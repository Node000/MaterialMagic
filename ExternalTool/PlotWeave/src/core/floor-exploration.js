import { DIRECTIONS } from './constants.js';
import { validateFloorFile } from './floor-file.js';
import { edgeKey, positionKey } from './strip-edges.js';

const STEPS = ['N', 'E', 'S', 'W'];
export const EXPLORATION_RADIUS = 2;

/** A private walk on canonical physical cells/edges, independent of the preview and draft. */
export class FloorExploration {
  #cells;
  #edges;
  #current;
  #visited = new Set();

  constructor(floor) {
    validateFloorFile(floor);
    this.#cells = new Map(floor.cells.map((cell) => [positionKey(cell.x, cell.y), cell]));
    this.#edges = new Map(floor.edges.map(({ x, y, dir, type }) => [edgeKey(x, y, dir), type]));
    this.#current = this.#cells.get(positionKey(floor.spawn.x, floor.spawn.y));
    this.#visited.add(positionKey(this.#current.x, this.#current.y));
  }

  #next(cell, dir) {
    const type = this.#edges.get(edgeKey(cell.x, cell.y, dir));
    if (type !== 'open' && type !== 'door') return null;
    const { dx, dy } = DIRECTIONS[dir];
    return this.#cells.get(positionKey(cell.x + dx, cell.y + dy)) ?? null;
  }

  #isRevealed(x, y, key) {
    const distance = Math.abs(x - this.#current.x) + Math.abs(y - this.#current.y);
    return distance <= EXPLORATION_RADIUS || this.#visited.has(key);
  }

  move(dir) {
    if (!STEPS.includes(dir)) return false;
    const next = this.#next(this.#current, dir);
    if (!next) return false;
    this.#current = next;
    this.#visited.add(positionKey(next.x, next.y));
    return true;
  }

  view() {
    const cells = [];
    // Keep the entire footprint fixed; fog conceals contents and interior walls/doors, not geometry.
    for (const [key, cell] of this.#cells) {
      const { x, y } = cell;
      const visible = this.#isRevealed(x, y, key);
      const borders = {};
      for (const dir of STEPS) {
        const { dx, dy } = DIRECTIONS[dir];
        const nx = x + dx, ny = y + dy;
        const neighborKey = positionKey(nx, ny);
        if (!this.#cells.has(neighborKey)) borders[dir] = visible ? 'wall' : 'outline';
        else if (visible) {
          borders[dir] = this.#isRevealed(nx, ny, neighborKey)
            ? this.#edges.get(edgeKey(x, y, dir)) ?? 'wall' : 'fog';
        }
      }
      cells.push(visible
        ? { x, y, visible, current: cell === this.#current, type: cell.type, enemyLevel: cell.enemyLevel, borders }
        : { x, y, visible, borders });
    }
    return cells;
  }
}

export function explorationDirection(event) {
  if (event.defaultPrevented || event.isComposing || event.ctrlKey || event.metaKey || event.altKey || event.shiftKey) return null;
  switch (event.key?.toLowerCase()) {
    case 'w': case 'arrowup': return 'N';
    case 'd': case 'arrowright': return 'E';
    case 's': case 'arrowdown': return 'S';
    case 'a': case 'arrowleft': return 'W';
    default: return null;
  }
}
