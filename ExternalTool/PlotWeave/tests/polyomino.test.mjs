import test from 'node:test';
import assert from 'node:assert/strict';

import {
  analyzeShapeKind,
  canAddCellKeepingChain,
  canRemoveCellKeepingChain,
  canRemoveCellKeepingConnected,
  computeChainOrderMap,
  hasTwoByTwoBlock,
  isFourConnected,
  isSimpleChain,
  normalizeCells,
  overlapCells,
  rotateCells,
  shapeSignature,
  uniqueRotations,
} from '../src/core/polyomino.js';

test('polyomino 归一化、四连通、无矩形单链与首尾顺序判定', () => {
  const lShape = normalizeCells([
    { rx: 3, ry: 2, type: 'empty' },
    { rx: 4, ry: 2, type: 'combat' },
    { rx: 5, ry: 2, type: 'combat' },
    { rx: 5, ry: 3, type: 'reward' },
    { rx: 5, ry: 4, type: 'empty' },
    { rx: 4, ry: 2, type: 'shop' }, // 重复格应去重
  ]);
  assert.equal(lShape.length, 5);
  assert.equal(isFourConnected(lShape), true);
  assert.equal(hasTwoByTwoBlock(lShape), false);
  assert.equal(isSimpleChain(lShape), true);
  assert.equal(canRemoveCellKeepingConnected(lShape, 0, 0), true);
  assert.equal(canRemoveCellKeepingChain(lShape, 0, 0), true);
  assert.equal(canRemoveCellKeepingChain(lShape, 1, 0), false); // 割点不可移除

  // 往拐角内侧 (1,1) 添加会形成 2×2 块 -> 应阻止
  assert.equal(canAddCellKeepingChain(lShape, 1, 1), false);
  // 往中段 (1,-1) 添加会形成 T 分叉 -> 应阻止
  assert.equal(canAddCellKeepingChain(lShape, 1, -1), false);
  // 往尾部 (2,3) 延续单链 -> 允许
  assert.equal(canAddCellKeepingChain(lShape, 2, 3), true);

  // 首尾端点 endFactor = 1，最中段 = 0
  const order = computeChainOrderMap(lShape);
  const factors = [...order.values()].map((v) => v.endFactor).sort((a, b) => a - b);
  assert.deepEqual(factors, [0, 0.5, 0.5, 1, 1]);

  // 2×2 矩形必须被拒绝
  const rect22 = [
    { rx: 0, ry: 0 },
    { rx: 1, ry: 0 },
    { rx: 0, ry: 1 },
    { rx: 1, ry: 1 },
  ];
  assert.equal(hasTwoByTwoBlock(rect22), true);
  assert.equal(isSimpleChain(rect22), false);
});

test('polyomino 旋转去重与旋转不变形状签名', () => {
  const line5 = [
    { rx: 0, ry: 0 },
    { rx: 1, ry: 0 },
    { rx: 2, ry: 0 },
    { rx: 3, ry: 0 },
    { rx: 4, ry: 0 },
  ];
  assert.equal(uniqueRotations(line5, true).length, 2);
  assert.equal(analyzeShapeKind(line5).id, 'line');

  const l5 = [
    { rx: 0, ry: 0 },
    { rx: 1, ry: 0 },
    { rx: 2, ry: 0 },
    { rx: 2, ry: 1 },
    { rx: 2, ry: 2 },
  ];
  assert.equal(uniqueRotations(l5, true).length, 4);
  assert.equal(analyzeShapeKind(l5).id, 'polyline');
  assert.equal(shapeSignature(l5), shapeSignature(rotateCells(l5, 1)));
  assert.equal(shapeSignature(l5), shapeSignature(rotateCells(l5, 2)));
  assert.equal(shapeSignature(l5), shapeSignature(rotateCells(l5, 3)));
});

test('overlapCells 精准识别单格交叉', () => {
  const a = [
    { x: 0, y: 0 },
    { x: 1, y: 0 },
    { x: 2, y: 0 },
  ];
  const b = [
    { x: 1, y: -1 },
    { x: 1, y: 0 },
    { x: 1, y: 1 },
  ];
  const hits = overlapCells(a, b);
  assert.equal(hits.length, 1);
  assert.equal(hits[0].x, 1);
  assert.equal(hits[0].y, 0);
});
