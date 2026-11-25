import test from 'node:test';
import assert from 'node:assert/strict';

import {
  createBlankProject,
  createDefaultProject,
  createPlotKind,
  createRegion,
  normalizeProject,
  projectFingerprint,
  validateProject,
  getByPath,
  setByPath,
} from '../src/core/project.js';
import {
  createBundle,
  formatImportErrors,
  parseBundle,
  parseProject,
  serializeBundle,
  serializeProject,
} from '../src/core/serialization.js';
import { generateMap } from '../src/core/generator.js';

test('内置示例工程与空白工程均可通过校验', () => {
  for (const project of [createDefaultProject(), createBlankProject()]) {
    const result = validateProject(project);
    assert.equal(result.ok, true, result.errors.map((issue) => issue.message).join('\n'));
    assert.equal(result.errors.length, 0);
    assert.ok(Array.isArray(result.warnings));
  }
});

test('工程规范化补默认值并保留扩展字段', () => {
  const raw = createBlankProject();
  delete raw.settings;
  delete raw.meta.author;
  raw.extensions = { example: { enabled: true } };
  raw.futureField = { keep: 1 };
  const project = normalizeProject(raw);
  assert.ok(project.settings.grid.width >= 8);
  assert.equal(typeof project.meta.author, 'string');
  assert.equal(project.schemaVersion, 1);
  assert.equal(project.extensions.example.enabled, true);
  assert.deepEqual(project.futureField, { keep: 1 });
});

test('校验会捕获重复 id、非法长度与断开引用', () => {
  const duplicate = createDefaultProject();
  duplicate.regions.push({ ...duplicate.regions[0] });
  assert.ok(validateProject(duplicate).errors.some((issue) => issue.code.includes('duplicate')));

  const badLength = createDefaultProject();
  badLength.plotKinds[0].length = { min: 0, max: 9 };
  assert.ok(validateProject(badLength).errors.some((issue) => issue.path.includes('length')));

  const badKind = createDefaultProject();
  badKind.plotKinds[0].regionId = 'missing-region';
  assert.ok(validateProject(badKind).errors.some((issue) => issue.code.includes('region')));

  const badProfile = createDefaultProject();
  badProfile.profiles[0].startKindId = 'missing-kind';
  assert.ok(validateProject(badProfile).errors.some((issue) => issue.path.includes('startKindId')));
});

test('指纹忽略 updatedAt，但会随实际内容变化', () => {
  const original = createDefaultProject();
  const fingerprint = projectFingerprint(original);
  const touched = normalizeProject(original);
  touched.meta.updatedAt = '2099-01-01T00:00:00.000Z';
  assert.equal(projectFingerprint(touched), fingerprint);
  touched.regions[0].name = `${touched.regions[0].name}改`;
  assert.notEqual(projectFingerprint(touched), fingerprint);
});

test('工厂与点路径工具行为稳定', () => {
  const project = createDefaultProject();
  const count = project.regions.length;
  const region = createRegion(project);
  assert.ok(region.id);
  assert.equal(project.regions.length, count, '工厂不应自动 push');
  const kind = createPlotKind(project, region.id);
  assert.equal(kind.regionId, region.id);
  assert.equal(kind.connections.anchors, 'ends');
  assert.equal(getByPath(project, 'settings.grid.width'), project.settings.grid.width);
  setByPath(project, 'settings.grid.width', 52);
  assert.equal(project.settings.grid.width, 52);
  setByPath(project, 'plotKinds.0.name', '改名');
  assert.equal(project.plotKinds[0].name, '改名');
});

test('工程 JSON 往返保持指纹与数据', () => {
  const project = createDefaultProject();
  const text = serializeProject(project);
  const parsed = parseProject(text);
  assert.equal(parsed.ok, true);
  assert.equal(projectFingerprint(parsed.project), projectFingerprint(project));
  assert.deepEqual(parsed.project.plotKinds[0], project.plotKinds[0]);
});

test('Bundle 往返保留地图，坏 JSON 不抛出', () => {
  const project = createDefaultProject();
  const map = generateMap(project, { profileId: 'standard', seed: 'BUNDLE-1' });
  const text = serializeBundle(project, map);
  const parsed = parseBundle(text);
  assert.equal(parsed.ok, true, parsed.errors.join('\n'));
  assert.equal(parsed.map.seed, map.seed);
  assert.equal(parsed.map.plots.length, map.plots.length);
  assert.equal(parsed.map.links.length, map.links.length);
  assert.deepEqual(parsed.map.spawn, map.spawn);

  const broken = parseProject('{not json');
  assert.equal(broken.ok, false);
  assert.ok(broken.errors.length > 0);

  const empty = parseBundle('null');
  assert.equal(empty.ok, false);

  const bundle = createBundle(project, map);
  assert.equal(bundle.kind, 'plotweave-bundle');
  assert.equal(bundle.schemaVersion, project.schemaVersion);
});

test('Bundle 中的地图会按工程重新校验并记录问题', () => {
  const project = createDefaultProject();
  const map = generateMap(project, { profileId: 'standard', seed: 'BUNDLE-2' });
  map.plots[0].kindId = 'missing-kind';
  const parsed = parseBundle(serializeBundle(project, map));
  assert.equal(parsed.ok, false);
  assert.ok(parsed.map.errors.some((issue) => issue.code === 'plot.kind'));
});

test('导入错误会格式化为可读文本，并识别单独的地图 JSON', () => {
  const project = createDefaultProject();
  project.meta.name = '';
  const parsedProject = parseProject(serializeProject(project));
  assert.equal(parsedProject.ok, false);
  const message = formatImportErrors(parsedProject);
  assert.ok(message.includes('工程需要一个名称'));
  assert.ok(!message.includes('[object Object]'));

  const map = generateMap(createDefaultProject(), { profileId: 'standard', seed: 'MAP-ONLY-1' });
  const parsedMap = parseBundle(JSON.stringify(map));
  assert.equal(parsedMap.ok, false);
  assert.equal(parsedMap.project, null);
  assert.ok(formatImportErrors(parsedMap).includes('当前地图 JSON'));
});
