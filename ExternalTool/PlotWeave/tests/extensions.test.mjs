import test from 'node:test';
import assert from 'node:assert/strict';

import { createDefaultProject } from '../src/core/project.js';
import { generateMap } from '../src/core/generator.js';
import { registry } from '../src/core/registry.js';

import '../plugins/example-plugin.js';

test('标准生成器已注册，插件可追加生成器', () => {
  const generators = registry.listGenerators();
  const standard = generators.find((item) => item.id === 'standard');
  assert.ok(standard);
  assert.equal(standard.label, '标准漫游');
  assert.equal(standard.version, 1);
  assert.ok(generators.some((item) => item.id === 'example-wrapped'));

  const project = createDefaultProject();
  const map = registry
    .getGenerator('example-wrapped')
    .generate(project, { profile: project.profiles[0], seed: 'EXT-GEN' });
  assert.equal(map.plots.length, project.profiles[0].plotTarget);
  assert.equal(map.errors.length, 0);
});

test('插件导出器输出可读的地块清单', () => {
  const project = createDefaultProject();
  const map = generateMap(project, { profile: project.profiles[0], seed: 'EXT-0001' });
  const exporter = registry.getExporter('example-survey');
  assert.ok(exporter);
  const text = exporter.export({ project, map });
  assert.match(text, /PlotWeave 地图清单/);
  assert.match(text, /EXT-0001/);
  assert.match(text, /## 前厅/);
  assert.match(text, /连接/);
});

test('插件校验器接入扩展校验链', () => {
  const project = createDefaultProject();
  const map = generateMap(project, { profile: project.profiles[0], seed: 'EXT-0002' });
  const issues = registry.runValidators('map', map, { project, map });
  assert.deepEqual(issues, []);

  const bad = structuredClone(map);
  const plot = bad.plots[0];
  const kind = project.plotKinds.find((item) => item.id === plot.kindId);
  while (plot.subRooms.length <= kind.subRooms.max) {
    plot.subRooms.push({ ...plot.subRooms[0], id: `s${plot.subRooms.length + 1}` });
  }
  const warnings = registry.runValidators('map', bad, { project, map: bad });
  assert.ok(warnings.some((issue) => issue.code === 'example.subroom-cap'));
});
