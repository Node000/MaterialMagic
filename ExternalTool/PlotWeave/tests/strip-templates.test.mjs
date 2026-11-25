import test from 'node:test';
import assert from 'node:assert/strict';

import {
  computeTemplateMetrics,
  createDefaultStripTemplates,
  defaultCellTypes,
  generateRandomStripDraft,
  normalizeStripTemplate,
  parseTemplateLibrary,
  serializeTemplateLibrary,
  validateStripTemplate,
} from '../src/core/strip-templates.js';
import { hasTwoByTwoBlock, isSimpleChain } from '../src/core/polyomino.js';

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
