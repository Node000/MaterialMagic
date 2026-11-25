import test from 'node:test';
import assert from 'node:assert/strict';

import { RNG, normalizeSeed, randomSeed } from '../src/core/rng.js';
import {
  cellsFor,
  plotBounds,
  mapBounds,
  degreeMap,
  directionFromTo,
  findCenteredOrigin,
  cellKey,
} from '../src/core/geometry.js';
import { sideIsAllowed, portsForPlot } from '../src/core/ports.js';
import { canConnectKinds, canCrossKinds, crossCapFor } from '../src/core/rules.js';
import { CONTENT_TYPE_IDS, DIRECTIONS } from '../src/core/constants.js';

const kind = (overrides = {}) => ({
  id: 'k',
  regionId: 'r',
  tags: [],
  connections: {
    max: 4,
    anchors: 'both',
    allowCrossing: true,
    allowedKinds: [],
    allowedTags: [],
  },
  ...overrides,
});

test('RNG 对同一种子产生完全一致的序列', () => {
  const a = new RNG('TEST-1');
  const b = new RNG('TEST-1');
  const left = Array.from({ length: 40 }, () => a.float());
  const right = Array.from({ length: 40 }, () => b.float());
  assert.deepEqual(left, right);
  assert.notDeepEqual(left, Array.from({ length: 40 }, () => new RNG('TEST-2').float()));
  for (const value of left) assert.ok(value >= 0 && value < 1);
});

test('RNG 的范围、抽样与种子规范化', () => {
  const rng = new RNG(' ranges ');
  for (let index = 0; index < 200; index += 1) {
    const value = rng.int(2, 5);
    assert.ok(Number.isInteger(value) && value >= 2 && value <= 5);
  }
  assert.equal(rng.chance(0), false);
  assert.equal(rng.chance(1), true);
  assert.equal(rng.pick([7]), 7);
  assert.deepEqual([...rng.shuffle([1, 2, 3])].sort(), [1, 2, 3]);
  assert.equal(normalizeSeed('  A  '), 'A');
  assert.equal(normalizeSeed(''), 'PLOTWEAVE');
  assert.match(randomSeed(), /^PW-[A-Z0-9]{4}-[A-Z0-9]{4}$/);
  assert.equal(new RNG('x').calls, 0);
});

test('cellsFor / plotBounds / mapBounds 按几何展开', () => {
  assert.deepEqual(cellsFor({ x: 2, y: 3 }, 'h', 3), [
    { x: 2, y: 3, index: 0 },
    { x: 3, y: 3, index: 1 },
    { x: 4, y: 3, index: 2 },
  ]);
  assert.deepEqual(cellsFor({ x: 2, y: 3 }, 'v', 2), [
    { x: 2, y: 3, index: 0 },
    { x: 2, y: 4, index: 1 },
  ]);
  const plot = { id: 'p', axis: 'h', origin: { x: 2, y: 3 }, length: 3 };
  assert.deepEqual(plotBounds(plot), { minX: 2, minY: 3, maxX: 4, maxY: 3, width: 3, height: 1 });
  const bounds = mapBounds([
    plot,
    { id: 'q', axis: 'v', origin: { x: 6, y: 1 }, length: 4 },
  ]);
  assert.deepEqual(bounds, { minX: 2, minY: 1, maxX: 6, maxY: 4, width: 5, height: 4 });
  assert.equal(cellKey(2, 3), '2,3');
});

test('方向与中心落点辅助函数', () => {
  assert.equal(directionFromTo({ x: 0, y: 0 }, { x: 0, y: -1 }), 'N');
  assert.equal(directionFromTo({ x: 0, y: 0 }, { x: 1, y: 0 }), 'E');
  assert.equal(directionFromTo({ x: 0, y: 0 }, { x: 0, y: 1 }), 'S');
  assert.equal(directionFromTo({ x: 0, y: 0 }, { x: -1, y: 0 }), 'W');
  assert.equal(directionFromTo({ x: 0, y: 0 }, { x: 1, y: 1 }), null);
  const origin = findCenteredOrigin(9, 7, 'h', 3);
  assert.ok(origin);
  assert.ok(origin.x >= 0 && origin.x + 3 <= 9);
  assert.ok(origin.y >= 0 && origin.y < 7);
  assert.ok(Math.abs(origin.x + 1.5 - 4.5) <= 1);
});

test('度数与边索引只统计真实 link', () => {
  const map = {
    plots: [{ id: 'a' }, { id: 'b' }, { id: 'c' }],
    links: [
      { type: 'door', a: { plotId: 'a' }, b: { plotId: 'b' } },
      { type: 'cross', a: { plotId: 'a' }, b: { plotId: 'c' } },
      { type: 'door', a: { plotId: 'a' }, b: { plotId: 'missing' } },
    ],
  };
  const degrees = degreeMap(map);
  assert.equal(degrees.get('a'), 2);
  assert.equal(degrees.get('b'), 1);
  assert.equal(degrees.get('c'), 1);
});

test('接口模式遵守端部 / 中段语义', () => {
  const both = kind();
  assert.equal(sideIsAllowed(both, 'h', 3, 0, 'W'), true);
  assert.equal(sideIsAllowed(both, 'h', 3, 0, 'N'), true);
  assert.equal(sideIsAllowed(both, 'h', 3, 0, 'E'), false);
  assert.equal(sideIsAllowed(both, 'h', 3, 1, 'N'), true);
  assert.equal(sideIsAllowed(both, 'h', 3, 1, 'E'), false);
  assert.equal(sideIsAllowed(both, 'h', 3, 2, 'E'), true);

  const ends = kind({ connections: { ...both.connections, anchors: 'ends' } });
  assert.equal(sideIsAllowed(ends, 'h', 3, 0, 'N'), true);
  assert.equal(sideIsAllowed(ends, 'h', 3, 1, 'N'), false);

  const middle = kind({ connections: { ...both.connections, anchors: 'middle' } });
  assert.equal(sideIsAllowed(middle, 'h', 3, 0, 'N'), false);
  assert.equal(sideIsAllowed(middle, 'h', 3, 1, 'N'), true);

  const single = kind({ connections: { ...both.connections, anchors: 'middle' } });
  assert.equal(sideIsAllowed(single, 'h', 1, 0, 'N'), false);
  assert.equal(sideIsAllowed(kind(), 'h', 1, 0, 'N'), true);
  assert.equal(DIRECTIONS.N.dy, -1);
});

test('portsForPlot 过滤边界、占用与已用 side', () => {
  const plot = { id: 'p', axis: 'h', origin: { x: 1, y: 1 }, length: 3 };
  const ports = portsForPlot({
    plot,
    kind: kind(),
    width: 5,
    height: 5,
    usedSides: new Set(['p:1:N']),
    isEmpty: (x, y) => !(x === 4 && y === 1),
  });
  assert.equal(ports.length, 6);
  assert.equal(new Set(ports.map((port) => port.key)).size, ports.length);
  assert.equal(ports.some((port) => port.dir === 'E' && port.cellIndex === 2), false);
  assert.equal(ports.some((port) => port.cellIndex === 1 && port.dir === 'N'), false);
  assert.equal(
    portsForPlot({
      plot,
      kind: kind({ connections: { ...kind().connections, max: 0 } }),
      width: 5,
      height: 5,
    }).length,
    0,
  );
});

test('连接规则双向检查 allowedKinds / allowedTags', () => {
  const a = kind({ id: 'a', tags: ['stone'] });
  const b = kind({ id: 'b', tags: ['wood'] });
  assert.equal(canConnectKinds(a, b), true);
  a.connections.allowedKinds = ['b'];
  assert.equal(canConnectKinds(a, b), true);
  assert.equal(canConnectKinds(b, a), true);
  b.connections.allowedKinds = ['other'];
  assert.equal(canConnectKinds(a, b), false);
  a.connections.allowedKinds = [];
  b.connections.allowedKinds = [];

  const tagged = kind({ id: 't', tags: ['stone'], connections: { ...a.connections, allowedKinds: [], allowedTags: ['stone'] } });
  assert.equal(canConnectKinds(tagged, kind({ id: 'u', tags: ['stone'] })), true);
  assert.equal(canConnectKinds(tagged, kind({ id: 'u', tags: ['wood'] })), false);

  const noCross = kind({ id: 'nc', connections: { ...a.connections, allowCrossing: false } });
  assert.equal(canCrossKinds(noCross, b, { allowCrossing: true }), false);
  assert.equal(canCrossKinds(a, b, { allowCrossing: false }), false);
  assert.equal(crossCapFor({ crossRegionRatio: 0.2, maxCrossRegionPerPair: 3 }, 24), 4);
  assert.equal(crossCapFor({ allowCrossRegion: false }, 24), 0);
  assert.equal(crossCapFor({ crossRegionRatio: 0.2, maxCrossRegionPerPair: 0 }, 24), 0);
});

test('内容目录类型固定为五类', () => {
  assert.deepEqual([...CONTENT_TYPE_IDS], ['props', 'relics', 'events', 'enemies', 'npcs']);
});
