import test from 'node:test';
import assert from 'node:assert/strict';

import {
  computeTemplateMetrics,
  createChildStripTemplate,
  createDefaultStripTemplates,
  defaultCellTypes,
  generateRandomStripDraft,
  normalizeCellTypePool,
  normalizeStripTemplate,
  normalizeTemplateGroups,
  parseTemplateLibrary,
  serializeTemplateLibrary,
  stripTemplateStructureKey,
  templateToFile,
  validateStripTemplate,
} from '../src/core/strip-templates.js';
import { hasTwoByTwoBlock, isSimpleChain } from '../src/core/polyomino.js';
import { createDefaultFloorProfile, estimateFloorBudget, generateFloorMap } from '../src/core/floor-generator.js';
import { validateFloorMap } from '../src/core/floor-validator.js';
import { parseBundle, parseProject, serializeBundle, serializeProject } from '../src/core/serialization.js';
import { AppStore } from '../src/ui/store.js';

test('全局格子配置驱动条带资源/难度求和，且战斗格偏移不影响总难度', () => {
  const cellTypes = defaultCellTypes();
  const tpl = normalizeStripTemplate(
    {
      id: 'tpl_test',
      name: '测试折线廊',
      cells: [
        { rx: 0, ry: 0, type: 'empty', difficultyOffset: 0, isPortal: true },
        { rx: 1, ry: 0, type: 'combat', difficultyOffset: 0 },
        { rx: 2, ry: 0, type: 'combat', difficultyOffset: 3 },
        { rx: 2, ry: 1, type: 'reward', difficultyOffset: 0 },
      ],
    },
    cellTypes,
  );

  const m1 = computeTemplateMetrics(tpl, cellTypes);
  assert.equal(m1.autoResource, 35);
  assert.equal(m1.autoDifficulty, 20);
  assert.equal(m1.resource, 35);
  assert.equal(m1.difficulty, 20);
  assert.equal(m1.portalCount, 1);
  assert.equal(m1.isChain, true);

  const editedTypes = cellTypes.map((ct) => (ct.id === 'combat' ? { ...ct, difficulty: 14 } : ct));
  const m2 = computeTemplateMetrics(tpl, editedTypes);
  assert.equal(m2.autoDifficulty, 28);
  assert.equal(m2.difficulty, 28);

  tpl.metrics.resourceOverride = 50;
  tpl.metrics.difficultyOverride = 42;
  const m3 = computeTemplateMetrics(tpl, editedTypes);
  assert.equal(m3.resource, 50);
  assert.equal(m3.difficulty, 42);
  assert.equal(m3.isResourceOverridden, true);
  assert.equal(m3.isDifficultyOverridden, true);
});

test('随机单条草稿生成器严格生成 4-6 格无矩形单链，支持非环与出入口格', () => {
  const cellTypes = defaultCellTypes();
  for (const shapeStyle of ['line', 'polyline', 'mixed']) {
    const a = generateRandomStripDraft(
      { minCells: 4, maxCells: 6, shapeStyle, noLoop: true, portalMode: 'single_end' },
      cellTypes,
      `DRAFT-${shapeStyle}`,
    );
    const b = generateRandomStripDraft(
      { minCells: 4, maxCells: 6, shapeStyle, noLoop: true, portalMode: 'single_end' },
      cellTypes,
      `DRAFT-${shapeStyle}`,
    );
    assert.deepEqual(a, b);
    assert.ok(a.cells.length >= 4 && a.cells.length <= 6);
    assert.equal(isSimpleChain(a.cells), true);
    assert.equal(hasTwoByTwoBlock(a.cells), false);
    assert.equal(a.noLoop, true);
    assert.equal(a.cells.filter((c) => c.isPortal).length, 1);
    const check = validateStripTemplate(a, cellTypes);
    assert.equal(check.ok, true, check.errors.map((e) => e.message).join('\n'));
  }

  // 连通矩形可用于普通条带，但有环时不能作为桥接单链
  const rectTpl = normalizeStripTemplate(
    {
      id: 'rect',
      name: '连通矩形',
      cells: [
        { rx: 0, ry: 0, type: 'empty' },
        { rx: 1, ry: 0, type: 'combat' },
        { rx: 0, ry: 1, type: 'event' },
        { rx: 1, ry: 1, type: 'reward' },
      ],
    },
    cellTypes,
  );
  assert.equal(validateStripTemplate(rectTpl, cellTypes).ok, true);
  assert.equal(validateStripTemplate({ ...rectTpl, role: 'bridge' }, cellTypes).ok, false);
});

test('内置样例模板库全部 4-6 格（4 格较少）、无矩形单链，JSON 往返无损', () => {
  const cellTypes = defaultCellTypes();
  const templates = createDefaultStripTemplates(cellTypes);
  assert.ok(templates.length >= 16);

  let count4 = 0;
  let count56 = 0;
  for (const tpl of templates) {
    assert.ok(tpl.cells.length >= 4 && tpl.cells.length <= 6, `${tpl.id} 格数 ${tpl.cells.length} 不在 4-6 之间`);
    assert.equal(isSimpleChain(tpl.cells), true, `${tpl.id} 不是无矩形单链`);
    assert.equal(hasTwoByTwoBlock(tpl.cells), false, `${tpl.id} 含有矩形块`);
    if (tpl.cells.length === 4) count4 += 1;
    else count56 += 1;
    const check = validateStripTemplate(tpl, cellTypes);
    assert.equal(check.ok, true, `${tpl.id}: ${check.errors.map((e) => e.message).join('\n')}`);
  }
  assert.ok(count4 > 0 && count4 < count56 / 2, `4 格占比过高：4格=${count4}, 5-6格=${count56}`);
  assert.ok(templates.some((t) => t.noLoop), '样例库应包含非环模板示范');
  assert.ok(templates.some((t) => t.cells.some((c) => c.isPortal)), '样例库应包含出入口格示范');

  const json = serializeTemplateLibrary({ cellTypes, stripTemplates: templates });
  const parsed = parseTemplateLibrary(json, cellTypes);
  assert.equal(parsed.ok, true, parsed.errors.join('\n'));
  assert.equal(parsed.stripTemplates.length, templates.length);
  assert.equal(parsed.cellTypes.length, cellTypes.length);
});

test('模板文件夹可嵌套并随模板库 JSON 往返，folderId 不参与结构签名', () => {
  const groups = normalizeTemplateGroups([
    { id: 'group_city', name: '城市', order: 0 },
    { id: 'group_city_combat', name: '战斗向', parentGroupId: 'group_city', order: 0 },
  ]);
  assert.equal(groups.length, 2);
  assert.equal(groups[1].parentGroupId, 'group_city');

  const [base] = createDefaultStripTemplates(cellTypes);
  const template = normalizeStripTemplate({ ...base, id: 'tpl_folder_test', folderId: 'group_city_combat' }, cellTypes);
  assert.equal(template.folderId, 'group_city_combat');
  const json = serializeTemplateLibrary({ cellTypes, stripTemplates: [template], templateGroups: groups });
  const parsed = parseTemplateLibrary(json, cellTypes);
  assert.equal(parsed.ok, true, parsed.errors.join('\n'));
  assert.deepEqual(parsed.templateGroups, groups);
  assert.equal(parsed.stripTemplates[0].folderId, 'group_city_combat');
  assert.equal(
    stripTemplateStructureKey(parsed.stripTemplates[0]),
    stripTemplateStructureKey(template),
    'folderId 是 UI 归类，不应影响家族结构签名',
  );
});

const cellTypes = defaultCellTypes();
function setupStore(t) {
  const descriptor = Object.getOwnPropertyDescriptor(globalThis, 'localStorage');
  const storage = new Map();
  Object.defineProperty(globalThis, 'localStorage', {
    configurable: true, value: { getItem: (key) => storage.get(key) ?? null, setItem: (key, val) => storage.set(key, val) },
  });
  t.after(() => {
    if (descriptor) Object.defineProperty(globalThis, 'localStorage', descriptor);
    else delete globalThis.localStorage;
  });
  return new AppStore();
}

function family() {
  const [root] = createDefaultStripTemplates(cellTypes);
  root.visualTypeId = 'test_art';
  root.metrics.resourceOverride = 1000;
  const child = { ...createChildStripTemplate(root, '子模板'), id: 'variant' };
  const fight = child.cells.find((cell) => cell.type === 'combat');
  fight.type = 'elite';
  fight.tags = ['伏击'];
  fight.difficultyOffset = 4;
  child.tags = ['夜间'];
  child.description = '同形状，不同战斗内容';
  return { root, child };
}

test('子模板保留完整条目：同族同级、独立指标、模板库/项目/Bundle/本地存储往返', (t) => {
  const { root, child } = family();
  const sibling = { ...createChildStripTemplate(child), id: 'sibling' };
  assert.equal(sibling.parentTemplateId, root.id);
  assert.equal(stripTemplateStructureKey(root), stripTemplateStructureKey(child));
  assert.equal(child.metrics.resourceOverride, null);
  assert.notEqual(computeTemplateMetrics(child, cellTypes).autoDifficulty, computeTemplateMetrics(root, cellTypes).autoDifficulty);
  const store = setupStore(t);
  assert.equal(store.importTemplateLibrary({ cellTypes, stripTemplates: [child, root, sibling], replace: true }), true);
  assert.deepEqual(new AppStore().dungeon.stripTemplates, [child, root, sibling]);
  const payload = serializeTemplateLibrary(store.dungeon);
  const file = JSON.parse(payload);
  assert.equal(file.schemaVersion, 3);
  assert.equal(file.kind, 'plotweave-strip-templates');
  assert.ok(!Object.hasOwn(file.stripTemplates[1], 'parentTemplateId'));
  assert.deepEqual(file.stripTemplates[0].openEdges, file.stripTemplates[1].openEdges);
  assert.deepEqual(parseTemplateLibrary(payload).stripTemplates, [child, root, sibling]);
  for (const parsed of [parseProject(serializeProject(store.project)), parseBundle(serializeBundle(store.project, null))]) {
    assert.equal(parsed.ok, true);
    assert.deepEqual(parsed.project.dungeon.stripTemplates, [child, root, sibling]);
  }
});

test('拒绝孤儿、嵌套、重复 ID、错误父 ID 和锁定字段不一致', (t) => {
  const { root, child } = family();
  const read = (templates) => parseTemplateLibrary(JSON.stringify({ cellTypes, stripTemplates: templates }));
  const rootFile = templateToFile(root);
  const childFile = templateToFile(child);
  assert.equal(read([childFile]).ok, false);
  assert.equal(read([rootFile, childFile, { ...childFile, id: 'nested', parentTemplateId: child.id }]).ok, false);
  assert.equal(read([rootFile, { ...rootFile }, childFile]).ok, false);
  assert.equal(read([rootFile, { ...childFile, parentTemplateId: null }]).ok, false);
  assert.equal(read([rootFile, { ...childFile, visualTypeId: 'other_art' }]).ok, false);
  for (const alter of [
    (t) => { t.cells[1].isPortal = true; },
    (t) => { t.cells[1].type = 'spawn'; },
    (t) => { t.cells[1].placedOrder += 8; },
    (t) => { t.cells[1].rx += 6; },
    (t) => { t.openEdges.pop(); },
    (t) => { t.allowRotate = false; },
    (t) => { t.role = 'bridge'; },
    (t) => { t.regionIds = ['factory']; },
    (t) => { t.generation.weight += 1; },
    (t) => { t.generation.max += 1; },
    (t) => { t.generation.unique = !t.generation.unique; },
  ]) {
    const invalid = structuredClone(childFile);
    alter(invalid);
    const parsed = read([rootFile, invalid]);
    assert.equal(parsed.ok, false);
    assert.ok(parsed.errors.some((error) => error.includes('锁定结构')), parsed.errors.join('\n'));
  }
  const payload = JSON.parse(serializeProject(setupStore(t).project));
  payload.dungeon.stripTemplates = [childFile];
  assert.equal(parseProject(JSON.stringify(payload)).project, null);
  assert.throws(() => serializeTemplateLibrary({ cellTypes, stripTemplates: [child] }), /不存在/);
});

test('保存锁定父结构、另存独立模板、删除及撤销不产生孤儿，合并重映射父 ID', (t) => {
  const store = setupStore(t);
  const { root, child } = family();
  assert.equal(store.importTemplateLibrary({ stripTemplates: [root], replace: true }), true);
  const saved = store.saveStripTemplate(child, { asChild: true });
  assert.equal(saved.parentTemplateId, root.id);
  assert.equal(store.saveStripTemplate({ ...root, allowRotate: false }), null);
  assert.equal(store.saveStripTemplate({ ...saved, allowRotate: false }), null);
  assert.equal(store.saveStripTemplate({ ...saved, name: '内容修改' }).name, '内容修改');
  const detached = store.saveStripTemplate(saved, { asNew: true });
  assert.notEqual(detached.id, saved.id);
  assert.ok(!Object.hasOwn(detached, 'parentTemplateId'));
  assert.equal(store.deleteStripTemplate(root.id), false);
  assert.equal(store.deleteStripTemplate(saved.id), true);
  assert.equal(store.saveStripTemplate({ ...root, allowRotate: false }).allowRotate, false);
  store.undo();
  store.undo();
  assert.equal(store.dungeon.stripTemplates.find((t) => t.id === saved.id).parentTemplateId, root.id);
  assert.equal(store.saveStripTemplate({ ...root, allowRotate: false }), null);
  store.redo();
  assert.ok(!store.dungeon.stripTemplates.some((t) => t.id === saved.id));
  assert.equal(store.importTemplateLibrary({ stripTemplates: [child, root] }), true);
  const mergedChild = store.dungeon.stripTemplates.find((t) => t.id === child.id);
  assert.equal(mergedChild.parentTemplateId, `${root.id}_2`);
  const before = serializeProject(store.project);
  assert.equal(store.importTemplateLibrary({ stripTemplates: [child], replace: true }), false);
  assert.equal(serializeProject(store.project), before);
});

test('浏览器单层生成器按家族展开子模板，家族共享权重与数量上限', () => {
  const roots = createDefaultStripTemplates(cellTypes);
  const child = { ...createChildStripTemplate(roots[0]), id: 'variant' };
  child.cells = child.cells.map((cell) => (cell.type === 'combat' ? { ...cell, type: 'elite' } : cell));
  const profile = createDefaultFloorProfile();
  const withChild = [child, ...roots];

  // 子模板改变了家族的平均资源/难度，预算应随之变化，而不是被忽略。
  assert.notDeepEqual(estimateFloorBudget(profile, withChild, cellTypes), estimateFloorBudget(profile, roots, cellTypes));

  let seenChild = false;
  let maxFamilyCount = 0;
  for (let i = 0; i < 40; i += 1) {
    const map = generateFloorMap({ profile, stripTemplates: withChild, cellTypes, seed: `FAMILY-JS-${i}` });
    const familyStrips = map.strips.filter((s) => s.templateId === roots[0].id || s.templateId === child.id);
    seenChild ||= familyStrips.some((s) => s.templateId === child.id);
    maxFamilyCount = Math.max(maxFamilyCount, familyStrips.length);
    assert.equal(validateFloorMap(map).ok, true, `种子 FAMILY-JS-${i} 校验失败`);
  }

  assert.equal(seenChild, true, '家族子模板应能在浏览器单层生成中被选中');
  assert.equal(maxFamilyCount, 1, 'unique 家族的数量上限应按家族共享');
});


test('真正的单格模糊类型池：规范化、期望指标与生成时按种子抽取', () => {
  const baseTypes = defaultCellTypes();
  const tpl = normalizeStripTemplate(
    {
      id: 'tpl_pool',
      name: '模糊类型测试条带',
      role: 'normal',
      cells: [
        { rx: 0, ry: 0, type: 'empty', isPortal: true },
        { rx: 1, ry: 0, type: 'combat', typePool: [{ type: 'combat', weight: 2 }, { type: 'elite', weight: 1 }] },
        { rx: 2, ry: 0, type: 'reward' },
      ],
    },
    baseTypes,
  );
  const pool = tpl.cells[1].typePool;
  assert.equal(pool.length, 2);
  assert.equal(pool[0].type, 'combat');
  assert.equal(pool[1].type, 'elite');

  const expected = computeTemplateMetrics(tpl, baseTypes);
  const fixed = normalizeStripTemplate({ ...tpl, cells: tpl.cells.map((cell, i) => i === 1 ? { ...cell, typePool: undefined } : cell) }, baseTypes);
  const fixedMetrics = computeTemplateMetrics(fixed, baseTypes);
  assert.ok(expected.autoResource > fixedMetrics.autoResource);
  assert.ok(expected.autoDifficulty > fixedMetrics.autoDifficulty);

  const merged = normalizeCellTypePool([
    { type: 'combat', weight: 2 },
    { type: 'combat', weight: 3 },
    { type: 'spawn', weight: 1 },
    { type: 'unknown', weight: 1 },
  ], new Map(baseTypes.map((ct) => [ct.id, ct])));
  assert.deepEqual(merged, [{ type: 'combat', weight: 5 }]);

  const spawn = normalizeStripTemplate({ ...tpl, cells: [{ ...tpl.cells[0], type: 'spawn', typePool: pool }] }, baseTypes);
  assert.equal(spawn.cells[0].typePool, undefined);

  const root = createDefaultStripTemplates(baseTypes)[0];
  const combatIndex = root.cells.findIndex((cell) => cell.type === 'combat');
  assert.ok(combatIndex >= 0);
  const custom = normalizeStripTemplate({
    ...root,
    id: 'tpl_pool_generated',
    name: '模糊生成测试',
    parentTemplateId: undefined,
    cells: root.cells.map((cell, i) => i === combatIndex
      ? { ...cell, typePool: [{ type: 'combat', weight: 1 }, { type: 'elite', weight: 1 }] }
      : cell),
  }, baseTypes);

  const profile = createDefaultFloorProfile();
  let sawTemplate = false;
  let sawElite = false;
  for (let i = 0; i < 16; i += 1) {
    const map = generateFloorMap({ profile, stripTemplates: [custom], cellTypes: baseTypes, seed: `TYPE-POOL-${i}` });
    for (const strip of map.strips) {
      if (strip.templateId !== custom.id) continue;
      sawTemplate = true;
      if (strip.cells.some((cell) => cell.type === 'elite')) sawElite = true;
    }
    assert.equal(validateFloorMap(map).ok, true, `TYPE-POOL-${i} 校验失败`);
  }
  assert.equal(sawTemplate, true);
  assert.equal(sawElite, true, '模糊池应在至少一个种子中抽到 elite');
});

test('模板文件夹移动/删除时模板归到上级或未归类，不删除模板', (t) => {
  const store = setupStore(t);
  const city = store.addTemplateGroup({ name: '城市' });
  const core = store.addTemplateGroup({ name: '核心', parentGroupId: city.id });
  assert.ok(city && core);
  const target = store.dungeon.stripTemplates[0];
  assert.equal(store.setTemplateFolder(target.id, core.id), true);
  assert.equal(store.dungeon.stripTemplates.find((tpl) => tpl.id === target.id).folderId, core.id);

  assert.equal(store.deleteTemplateGroup(core.id), true);
  assert.equal(store.dungeon.stripTemplates.find((tpl) => tpl.id === target.id).folderId, city.id);

  assert.equal(store.deleteTemplateGroup(city.id), true);
  assert.equal(store.dungeon.stripTemplates.find((tpl) => tpl.id === target.id).folderId, undefined);
  assert.equal(store.dungeon.stripTemplates.length, 19);
});

test('模板 uiOrder 只影响左侧排序，不改变模板数组顺序和结构签名', (t) => {
  const store = setupStore(t);
  const [first, second] = store.dungeon.stripTemplates.filter((tpl) => !tpl.parentTemplateId);
  const beforeKey = stripTemplateStructureKey(second);
  assert.equal(store.moveStripTemplate(second.id, -1), true);
  const updatedFirst = store.dungeon.stripTemplates.find((tpl) => tpl.id === first.id);
  const updatedSecond = store.dungeon.stripTemplates.find((tpl) => tpl.id === second.id);
  assert.equal(updatedSecond.uiOrder, 0);
  assert.equal(updatedFirst.uiOrder, 1);
  assert.equal(store.dungeon.stripTemplates[0].id, first.id, 'uiOrder 不应移动底层数组，避免改变生成顺序');
  assert.equal(stripTemplateStructureKey(updatedSecond), beforeKey);
  assert.equal(store.moveStripTemplate(updatedSecond.id, -1), false, '已到顶时不应移动');
});

test('子模板 uiOrder 只在同一父模板内排序', (t) => {
  const store = setupStore(t);
  const parent = store.dungeon.stripTemplates.find((tpl) => !tpl.parentTemplateId);
  const makeChild = (name) => {
    const raw = structuredClone(parent);
    raw.id = '';
    raw.name = name;
    raw.parentTemplateId = parent.id;
    delete raw.uiOrder;
    return store.saveStripTemplate(raw, { asChild: true });
  };
  const childA = makeChild('子排序 A');
  const childB = makeChild('子排序 B');
  assert.ok(childA && childB);
  const before = store.dungeon.stripTemplates.map((tpl) => tpl.id);
  assert.equal(store.moveStripTemplate(childA.id, -1), true);
  const updatedA = store.dungeon.stripTemplates.find((tpl) => tpl.id === childA.id);
  const updatedB = store.dungeon.stripTemplates.find((tpl) => tpl.id === childB.id);
  assert.equal(updatedA.uiOrder, 0);
  assert.equal(updatedB.uiOrder, 1);
  assert.deepEqual(store.dungeon.stripTemplates.map((tpl) => tpl.id), before);
});
