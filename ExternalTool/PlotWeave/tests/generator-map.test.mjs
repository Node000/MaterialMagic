import test from 'node:test';
import assert from 'node:assert/strict';

import { createBlankProject, createDefaultProject } from '../src/core/project.js';
import { generateMap, previewQuotas } from '../src/core/generator.js';
import { computeMapStats, validateMap } from '../src/core/validate-map.js';
import { cellsFor } from '../src/core/geometry.js';

function blankMap(project) {
  return {
    app: 'plotweave',
    schemaVersion: project.schemaVersion,
    seed: 'MAP-TEST',
    profileId: project.profiles[0].id,
    settings: { grid: { width: 10, height: 10 } },
    plots: [],
    links: [],
    spawn: null,
  };
}

function simplePlot(project, id, origin, axis, length) {
  return {
    id,
    kindId: project.plotKinds[0].id,
    regionId: project.regions[0].id,
    axis,
    origin,
    length,
    cells: cellsFor(origin, axis, length).map(({ x, y }) => ({ x, y })),
    contents: { props: [], relics: [], events: [], enemies: [], npcs: [] },
    subRooms: [],
  };
}

function twoPlotMap(project) {
  const map = blankMap(project);
  const first = simplePlot(project, 'p1', { x: 2, y: 2 }, 'h', 2);
  const second = simplePlot(project, 'p2', { x: 4, y: 2 }, 'v', 2);
  map.plots = [first, second];
  map.links = [
    {
      id: 'l1',
      type: 'door',
      a: { plotId: 'p1', cellIndex: 1 },
      b: { plotId: 'p2', cellIndex: 0 },
      dir: 'E',
    },
  ];
  map.spawn = { plotId: 'p1', cellIndex: 0, x: 2, y: 2 };
  return map;
}

test('合法门连接地图通过校验', () => {
  const project = createBlankProject();
  const map = twoPlotMap(project);
  const result = validateMap(project, map);
  assert.equal(result.ok, true, result.issues.map((issue) => `${issue.code}: ${issue.message}`).join('\n'));
  assert.equal(result.stats.plots, 2);
  assert.equal(result.stats.doors, 1);
  assert.equal(result.stats.reachable, 2);
  assert.equal(result.stats.degrees.max, 1);
});

test('交叉只允许共享单格且轴垂直', () => {
  const project = createBlankProject();
  const map = blankMap(project);
  const first = simplePlot(project, 'p1', { x: 2, y: 2 }, 'h', 3);
  const second = simplePlot(project, 'p2', { x: 3, y: 1 }, 'v', 3);
  map.plots = [first, second];
  map.links = [
    {
      id: 'x1',
      type: 'cross',
      a: { plotId: 'p1', cellIndex: 1 },
      b: { plotId: 'p2', cellIndex: 1 },
      cell: { x: 3, y: 2 },
    },
  ];
  map.spawn = { plotId: 'p1', cellIndex: 0, x: 2, y: 2 };
  const result = validateMap(project, map);
  assert.equal(result.ok, true, result.issues.map((issue) => issue.message).join('\n'));
  assert.equal(result.stats.crossings, 1);
  assert.equal(result.stats.reachable, 2);
});

test('校验能发现重叠、坏门、超度与出生点错误', () => {
  const project = createBlankProject();

  const overlap = blankMap(project);
  overlap.plots = [
    simplePlot(project, 'p1', { x: 2, y: 2 }, 'h', 2),
    simplePlot(project, 'p2', { x: 3, y: 1 }, 'v', 2),
  ];
  overlap.spawn = { plotId: 'p1', cellIndex: 0, x: 2, y: 2 };
  assert.ok(validateMap(project, overlap).errors.some((issue) => issue.code === 'map.overlap.unlinked'));

  const badDoor = twoPlotMap(project);
  badDoor.links[0].b.cellIndex = 1;
  assert.ok(validateMap(project, badDoor).errors.some((issue) => issue.code === 'link.door.adjacency'));

  const tooMany = twoPlotMap(project);
  tooMany.plots[0].kindId = tooMany.plots[1].kindId = project.plotKinds[0].id;
  project.plotKinds[0].connections.max = 0;
  assert.ok(validateMap(project, tooMany).errors.some((issue) => issue.code === 'plot.degree'));

  const badSpawn = twoPlotMap(project);
  badSpawn.spawn = { plotId: 'missing', cellIndex: 0, x: 0, y: 0 };
  assert.ok(validateMap(project, badSpawn).errors.some((issue) => issue.code === 'spawn.plot'));

  const disconnected = twoPlotMap(project);
  disconnected.links = [];
  assert.ok(validateMap(project, disconnected).errors.some((issue) => issue.code === 'map.connectivity'));
});

test('computeMapStats 对空地图和退化输入安全', () => {
  const stats = computeMapStats(null);
  assert.equal(stats.plots, 0);
  assert.equal(stats.links, 0);
  assert.equal(stats.degrees.max, 0);
  assert.equal(stats.reachable, 0);
  const invalid = validateMap(createBlankProject(), null);
  assert.equal(invalid.ok, false);
  assert.equal(invalid.errors[0].code, 'map.invalid');
});

test('三个内置方案都达到目标且通过地图校验', () => {
  const project = createDefaultProject();
  const cases = [
    ['standard', 24],
    ['compact', 12],
    ['quiet', 8],
  ];
  for (const [profileId, target] of cases) {
    for (const seed of ['A1', 'B2']) {
      const map = generateMap(project, { profileId, seed });
      const profile = project.profiles.find((candidate) => candidate.id === profileId);
      const result = validateMap(project, map, { profile });
      assert.equal(result.errors.length, 0, `${profileId}/${seed}: ${result.errors.map((issue) => issue.message)}`);
      assert.equal(map.plots.length, target, `${profileId}/${seed} 未达到目标`);
      assert.ok(map.spawn);
      assert.equal(map.stats.reachable, map.plots.length);
      assert.equal(map.stats.degrees.isolated, 0);
      assert.ok(map.plots.every((plot) => plot.length >= 1 && plot.length <= 5));
      assert.ok(map.plots.every((plot) => plot.cells.length === plot.length));
    }
  }
});

test('同一种子完全确定，不同种子产生不同布局', () => {
  const project = createDefaultProject();
  const left = generateMap(project, { profileId: 'standard', seed: 'SAME' });
  const right = generateMap(project, { profileId: 'standard', seed: 'SAME' });
  assert.deepEqual(left.plots, right.plots);
  assert.deepEqual(left.links, right.links);
  assert.deepEqual(left.spawn, right.spawn);
  assert.equal(left.stats.doors, right.stats.doors);
  assert.equal(left.stats.crossings, right.stats.crossings);

  const other = generateMap(project, { profileId: 'standard', seed: 'OTHER' });
  assert.notDeepEqual(
    left.plots.map((plot) => `${plot.id}:${plot.origin.x},${plot.origin.y}`),
    other.plots.map((plot) => `${plot.id}:${plot.origin.x},${plot.origin.y}`),
  );
});

test('配额预览覆盖所有启用区域并保持在容量内', () => {
  const project = createDefaultProject();
  const quotas = previewQuotas(project, project.profiles[0], 'PREVIEW');
  assert.equal(quotas.length, project.regions.length);
  assert.equal(quotas.reduce((sum, item) => sum + item.quota, 0), project.profiles[0].plotTarget);
  for (const item of quotas) {
    assert.ok(item.quota >= 0);
    assert.ok(item.quota <= item.capacity);
  }
});

test('单区域方案不会生成跨区连接', () => {
  const project = createDefaultProject();
  const map = generateMap(project, { profileId: 'quiet', seed: 'QUIET-1' });
  assert.equal(map.stats.crossRegionLinks, 0);
  assert.equal(new Set(map.plots.map((plot) => plot.regionId)).size, 1);
});

test('子房间按池分配并带独立实例 id', () => {
  const project = createDefaultProject();
  const map = generateMap(project, { profileId: 'standard', seed: 'ROOMS' });
  const rooms = map.plots.flatMap((plot) => plot.subRooms || []);
  assert.ok(rooms.length > 0);
  assert.equal(new Set(rooms.map((room) => room.id)).size, rooms.length);
  for (const room of rooms) {
    assert.ok(project.subRoomKinds.some((kind) => kind.id === room.kindId));
    const parent = map.plots.find((plot) => plot.id === room.plotId || plot.subRooms.includes(room));
    assert.ok(parent);
  }
});
