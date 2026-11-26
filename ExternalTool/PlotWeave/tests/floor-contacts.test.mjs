import assert from 'node:assert/strict';
import test from 'node:test';
import { floorSharedWalls } from '../src/core/floor-contacts.js';
import { defaultMapPools, validateMapPools } from '../src/core/map-pools.js';

test('counts uninterrupted cross-strip walls, excluding doors and internal partitions', () => {
  const cells = Array.from({ length: 6 }, (_, y) => [
    { x: -2, y, stripIds: ['a'] }, { x: -1, y, stripIds: ['b'] },
  ]).flat();
  const { edges, longest } = floorSharedWalls({ cells, edges: [{ x: -2, y: 2, dir: 'E', type: 'door' }] });
  assert.deepEqual(edges.map(({ y }) => y), [0, 1, 3, 4, 5]);
  assert.equal(longest, 3);
});

test('ring spacing is optional in existing recipes; new modes are strictly validated', () => {
  const config = defaultMapPools();
  const [entry] = config.pools[0].entries;
  assert.deepEqual(validateMapPools(config), []);
  for (const mode of ['legacy', 'balanced', 'spacious']) {
    entry.ringSpacing = mode;
    assert.deepEqual(validateMapPools(config), []);
  }
  for (const value of ['anything', null, 1]) {
    entry.ringSpacing = value;
    assert.match(validateMapPools(config).join(), /环路留白/);
  }
});
