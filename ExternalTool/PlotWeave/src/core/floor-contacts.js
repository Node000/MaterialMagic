import { edgeKey, positionKey } from './strip-edges.js';

// For the physical, non-overlapping Unity layout. Same-strip partitions and real doors are not contacts.
export function floorSharedWalls(floor) {
  const owners = new Map(floor.cells.filter((cell) => cell.stripIds?.length === 1)
    .map((cell) => [positionKey(cell.x, cell.y), cell.stripIds[0]]));
  const openings = new Set(floor.edges.map(({ x, y, dir }) => edgeKey(x, y, dir)));
  const edges = [];
  const runs = new Map();
  for (const { x, y } of floor.cells) {
    const from = owners.get(positionKey(x, y));
    if (!from) continue;
    for (const [dir, dx, dy] of [['E', 1, 0], ['S', 0, 1]]) {
      const to = owners.get(positionKey(x + dx, y + dy));
      if (!to || from === to || openings.has(edgeKey(x, y, dir))) continue;
      edges.push({ x, y, dir });
      const key = JSON.stringify([...[from, to].sort(), dir, dir === 'E' ? x : y]);
      if (!runs.has(key)) runs.set(key, []);
      runs.get(key).push(dir === 'E' ? y : x);
    }
  }
  let longest = 0;
  for (const positions of runs.values()) {
    positions.sort((a, b) => a - b);
    let run = 0, previous;
    for (const at of positions) {
      run = at === previous + 1 ? run + 1 : 1;
      longest = Math.max(longest, run);
      previous = at;
    }
  }
  return { edges, longest };
}
