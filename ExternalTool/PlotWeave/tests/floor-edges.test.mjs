import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { appendStripCell, removeStripCell, toggleStripEdge, isOpenChain } from '../src/core/strip-edges.js';
import { computeChainOrderMap, uniqueRotations } from '../src/core/polyomino.js';
import { createDefaultStripTemplates, normalizeStripTemplate, serializeTemplateLibrary, parseTemplateLibrary } from '../src/core/strip-templates.js';
import { applyFloorFile, makeFloorFile, validateFloorFile } from '../src/core/floor-file.js';
import { generateFloorMap } from '../src/core/floor-generator.js';

function folded() {
  return [[0, 0], [1, 0], [1, 1], [0, 1]].reduce((cells, [rx, ry]) => appendStripCell(cells, { rx, ry, type: 'empty' }), []);
}

test('新格只连接最晚放置的邻格，封墙和删格不能切断条带', () => {
  const cells = folded();
  assert.deepEqual(cells[3].openDirs, ['E']);
  assert.equal(isOpenChain(cells), true);
  assert.equal(computeChainOrderMap(cells).get(3).isTail, true);
  assert.equal(toggleStripEdge(cells, 0, 0, 'E'), null);
  assert.equal(removeStripCell(cells, 1, 0), null);
  const loop = toggleStripEdge(cells, 0, 0, 'S');
  assert.equal(isOpenChain(loop), false);
  assert.equal(computeChainOrderMap(loop).get(0).isHead, false);
  assert.ok(removeStripCell(loop, 1, 0));
  assert.ok(toggleStripEdge(loop, 0, 0, 'E'));
});

test('同占地不同墙位保留四种旋转，保存并重读保留放置顺序', () => {
  const template = normalizeStripTemplate({ id: 'folded', cells: folded() });
  const variants = uniqueRotations(template.cells);
  assert.equal(variants.length, 4);
  assert.deepEqual(variants[1].cells.find((c) => c.placedOrder === 0).openDirs, ['S']);
  const parsed = parseTemplateLibrary(serializeTemplateLibrary({ stripTemplates: [template] }));
  assert.equal(parsed.ok, true);
  assert.deepEqual(parsed.stripTemplates[0].cells, template.cells);
});

test('从 Unity 使用的同一份 JSON 重读墙、开边和门，且手改开放边能往返', () => {
  const fixture = new URL('../../../Assets/Resources/Data/Maps/Patterns/Preview.floor.json', import.meta.url);
  const file = JSON.parse(readFileSync(fixture, 'utf8'));
  // The preview file is designer-owned; use it for general round-trip, not a particular layout assert.
  assert.deepEqual(makeFloorFile(applyFloorFile(file)).edges, [...file.edges].sort((a,b) => a.y-b.y || a.x-b.x || a.dir.localeCompare(b.dir)));
  const four = { kind: 'plotweave-floor-map', schemaVersion: 1, id: 'wall', spawn: {x:0,y:0},
    cells: folded().map(({rx,ry}) => ({x:rx,y:ry})), edges: [
      {x:0,y:0,dir:'E',type:'open'}, {x:1,y:0,dir:'S',type:'open'}, {x:0,y:1,dir:'E',type:'open'},
    ] };
  assert.equal(applyFloorFile(four).strips[0].cells[0].openDirs.includes('S'), false);
  four.edges.push({x:0,y:0,dir:'S',type:'open'});
  assert.equal(applyFloorFile(four).strips[0].cells[0].openDirs.includes('S'), true);
  assert.throws(() => validateFloorFile({ ...four, edges: [...four.edges, four.edges[0]] }), /重复/);
  assert.throws(() => validateFloorFile({ ...four, edges: [] }), /不可达/);
  assert.throws(() => validateFloorFile({ ...four, edges: [{ x: 1, y: 1, dir: 'E', type: 'door' }] }), /两端/);
});

test('三角生成使用带内墙模板，导出再打开不会把内墙变成通路', () => {
  const templates = createDefaultStripTemplates();
  const tpl = normalizeStripTemplate({ id: 'folded', cells: folded(), generation: { weight: 40, max: 20 } });
  const map = generateFloorMap({ stripTemplates: [...templates, tpl], seed: 'PW-WALL-DEMO' });
  assert.deepEqual(map.errors, []);
  const strip = map.strips.find((s) => s.templateId === tpl.id);
  assert.ok(strip, '固定种子必须实际使用带墙模板');
  assert.equal(strip.cells.reduce((n, c) => n + c.openDirs.length, 0), 6);
  const file = makeFloorFile(map);
  assert.deepEqual(makeFloorFile(applyFloorFile(file)).edges, file.edges);
});
