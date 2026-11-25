import test from 'node:test';
import assert from 'node:assert/strict';

import {
  createDefaultFloorProfile,
  estimateFloorBudget,
  generateFloorMap,
  recomputeFloorProgression,
} from '../src/core/floor-generator.js';
import { validateFloorMap } from '../src/core/floor-validator.js';
import { computeChainOrderMap } from '../src/core/polyomino.js';
import {
  createDefaultStripTemplates,
  defaultCellTypes,
} from '../src/core/strip-templates.js';

test('单层三角地牢确定性 & 默认使用城市/工厂/郊区预设', () => {
  const cellTypes = defaultCellTypes();
  const stripTemplates = createDefaultStripTemplates(cellTypes);
  const profile = createDefaultFloorProfile();
  assert.deepEqual(
    profile.regions.map((r) => r.id),
    ['city', 'factory', 'suburb'],
  );

  const a = generateFloorMap({ profile, stripTemplates, cellTypes, seed: 'TRI-SEED-1', createdAt: '2026-01-01T00:00:00.000Z' });
  const b = generateFloorMap({ profile, stripTemplates, cellTypes, seed: 'TRI-SEED-1', createdAt: '2026-01-01T00:00:00.000Z' });
  assert.deepEqual(a, b);
});

test('批量 60 个种子全部满足三区三角、唯一门桥、每区有环、非环度数=1、出入口格严格生效与距离等级不变量', () => {
  const cellTypes = defaultCellTypes();
  const stripTemplates = createDefaultStripTemplates(cellTypes);
  const profile = createDefaultFloorProfile();

  let inBudgetCount = 0;
  let totalCrossings = 0;
  let totalNoLoop = 0;

  for (let i = 1; i <= 60; i += 1) {
    const seed = `MV-FLOOR-${i}`;
    const map = generateFloorMap({ profile, stripTemplates, cellTypes, seed });
    const validation = validateFloorMap(map);

    assert.equal(
      validation.ok,
      true,
      `Seed ${seed} 校验失败：\n${validation.errors.map((e) => e.message).join('\n')}`,
    );
    assert.equal(map.stats.triangleValid, true, `Seed ${seed} 三角桥不合法`);
    assert.equal(map.stats.allRegionsHaveCycle, true, `Seed ${seed} 存在无环区域`);
    assert.equal(map.stats.bridges, 3);
    assert.equal(map.stats.bridgeDoors, 6);
    assert.equal(map.stats.allReachable, true);
    assert.ok(map.stats.maxCellStack <= 2);

    // 显式验证：所有 3 条桥接条带均严格从首尾不同两端分别连接两个区域
    for (const bridge of map.strips.filter((s) => s.role === 'bridge')) {
      const bLinks = map.links.filter((l) => l.a.stripId === bridge.id || l.b.stripId === bridge.id);
      assert.equal(bLinks.length, 2);
      const idxs = bLinks.map((l) => (l.a.stripId === bridge.id ? l.a.cellIndex : l.b.cellIndex));
      assert.notEqual(idxs[0], idxs[1], `Seed ${seed} 桥 ${bridge.id} 两门连在同一格`);
      const order = computeChainOrderMap(bridge.cells);
      const e1 = order.get(idxs[0]);
      const e2 = order.get(idxs[1]);
      assert.ok(
        (e1?.isHead && e2?.isTail) || (e1?.isTail && e2?.isHead),
        `Seed ${seed} 桥 ${bridge.templateName}(${bridge.id}) 未在首尾两端连接区域`,
      );
    }

    // 显式验证：所有交叉连接至少一方是通道/空（方案 1：禁止双非空交叉）
    const stripById = new Map(map.strips.map((s) => [s.id, s]));
    for (const link of map.links.filter((l) => l.type === 'cross')) {
      const ca = stripById.get(link.a.stripId)?.cells?.[link.a.cellIndex];
      const cb = stripById.get(link.b.stripId)?.cells?.[link.b.cellIndex];
      assert.ok(
        ca?.type === 'empty' || cb?.type === 'empty',
        `Seed ${seed} 交叉 ${link.id} 出现了双非空格重叠 (${ca?.type} ∩ ${cb?.type})`,
      );
    }

    const spawnStrip = map.strips.find((s) => s.id === map.spawn.stripId);
    assert.equal(spawnStrip.distanceFromSpawn, 0);
    assert.equal(spawnStrip.level, profile.progression.baseLevel);

    for (const strip of map.strips) {
      const expectedLevel = Math.min(
        profile.progression.maxLevel,
        Math.max(1, profile.progression.baseLevel + Math.floor(strip.distanceFromSpawn * profile.progression.levelPerHop)),
      );
      assert.equal(strip.level, expectedLevel);
      for (const cell of strip.cells) {
        if (cell.type === 'combat' || cell.type === 'elite') {
          assert.equal(cell.enemyLevel, Math.max(1, strip.level + cell.difficultyOffset));
        } else {
          assert.equal(cell.enemyLevel, null);
        }
      }
    }

    if (map.stats.resourceInRange && map.stats.difficultyInRange) inBudgetCount += 1;
    totalCrossings += map.stats.crossings;
    totalNoLoop += map.stats.totalNoLoopCount;
  }

  assert.ok(inBudgetCount >= 55, `预算区间命中率过低：${inBudgetCount}/60`);
  assert.ok(totalCrossings > 0, '60 次生成中应出现区域内单格交叉');
  assert.ok(totalNoLoop > 0, '启用非环上限时应生成非环端点条带');
});

test('区域 maxNoLoop = 0 时严格不生成任何非环条带', () => {
  const cellTypes = defaultCellTypes();
  const stripTemplates = createDefaultStripTemplates(cellTypes);
  const profile = createDefaultFloorProfile();
  profile.regions[0].maxNoLoop = 0;
  profile.regions[1].maxNoLoop = 0;
  profile.regions[2].maxNoLoop = 0;

  for (let i = 1; i <= 15; i += 1) {
    const map = generateFloorMap({ profile, stripTemplates, cellTypes, seed: `NO-NOLOOP-${i}` });
    assert.equal(map.errors.length, 0, map.errors.map((e) => e.message).join('\n'));
    assert.equal(map.stats.totalNoLoopCount, 0);
  }
});

test('结构松散度 looseness 显著控制连接位置倾向（高松散度偏头尾，低松散度偏中段）', () => {
  const cellTypes = defaultCellTypes();
  const stripTemplates = createDefaultStripTemplates(cellTypes);

  const tightProfile = createDefaultFloorProfile();
  tightProfile.topology.looseness = 0.05;

  const looseProfile = createDefaultFloorProfile();
  looseProfile.topology.looseness = 0.95;

  let tightSum = 0;
  let looseSum = 0;
  const N = 16;
  for (let i = 1; i <= N; i += 1) {
    const mTight = generateFloorMap({ profile: tightProfile, stripTemplates, cellTypes, seed: `LOOSE-CMP-${i}` });
    const mLoose = generateFloorMap({ profile: looseProfile, stripTemplates, cellTypes, seed: `LOOSE-CMP-${i}` });
    assert.equal(mTight.errors.length, 0);
    assert.equal(mLoose.errors.length, 0);
    tightSum += mTight.stats.avgConnectionEndFactor;
    looseSum += mLoose.stats.avgConnectionEndFactor;
  }
  const avgTight = tightSum / N;
  const avgLoose = looseSum / N;
  assert.ok(
    avgLoose - avgTight >= 0.2,
    `松散度区分不足：紧凑均值=${avgTight.toFixed(3)}, 松散均值=${avgLoose.toFixed(3)}`,
  );
});

test('手动指定资源与难度区间时，求解器将总值收敛入目标区间', () => {
  const cellTypes = defaultCellTypes();
  const stripTemplates = createDefaultStripTemplates(cellTypes);
  const profile = createDefaultFloorProfile();
  const est = estimateFloorBudget(profile, stripTemplates, cellTypes);
  profile.budget = {
    resourceMin: est.expectedResource - 30,
    resourceMax: est.expectedResource + 30,
    difficultyMin: est.expectedDifficulty - 30,
    difficultyMax: est.expectedDifficulty + 30,
  };

  let hits = 0;
  for (let i = 1; i <= 15; i += 1) {
    const map = generateFloorMap({ profile, stripTemplates, cellTypes, seed: `BUDGET-${i}` });
    assert.equal(map.errors.length, 0);
    if (map.stats.resourceInRange && map.stats.difficultyInRange) hits += 1;
  }
  assert.ok(hits >= 13, `手动窄区间命中次数不足：${hits}/15`);
});

test('手工修改战斗格 difficultyOffset 重算等级时，敌人等级生效且条带难度总和不变', () => {
  const cellTypes = defaultCellTypes();
  const stripTemplates = createDefaultStripTemplates(cellTypes);
  const profile = createDefaultFloorProfile();
  const map = generateFloorMap({ profile, stripTemplates, cellTypes, seed: 'OFFSET-CHECK' });

  const targetStrip = map.strips.find((s) => s.cells.some((c) => c.type === 'combat' || c.type === 'elite'));
  assert.ok(targetStrip);
  const combatCell = targetStrip.cells.find((c) => c.type === 'combat' || c.type === 'elite');
  const beforeDifficulty = targetStrip.difficulty;
  combatCell.difficultyOffset = 4;
  recomputeFloorProgression(map, profile.progression, cellTypes);

  assert.equal(combatCell.enemyLevel, Math.max(1, targetStrip.level + 4));
  assert.equal(targetStrip.difficulty, beforeDifficulty);
});
