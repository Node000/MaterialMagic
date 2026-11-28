import test from 'node:test';
import assert from 'node:assert/strict';

import {
  createDefaultFloorProfile,
  generateFloorMap,
} from '../src/core/floor-generator.js';
import { validateFloorMap } from '../src/core/floor-validator.js';
import {
  createDefaultStripTemplates,
  defaultCellTypes,
} from '../src/core/strip-templates.js';
import {
  WORLD_TIER_COUNT,
  createDefaultWorldPools,
  createWorldMapEntry,
  generateWorldTierMap,
  normalizeWorldPools,
  parseWorldPools,
  selectWorldEntry,
  serializeWorldPools,
  validateWorldPools,
} from '../src/core/world-pools.js';

test('最小环长度（按条带数）硬约束生效：设为 4 时不存在 3 条带短环', () => {
  const cellTypes = defaultCellTypes();
  const stripTemplates = createDefaultStripTemplates(cellTypes);
  const profile = createDefaultFloorProfile();
  profile.regions.forEach((r) => {
    r.stripMin = 6;
    r.stripMax = 7;
  });
  profile.topology.minCycleLength = 4;
  profile.topology.extraLoopChance = 0.5;
  profile.topology.localClustering = true;

  for (let i = 1; i <= 15; i += 1) {
    const map = generateFloorMap({ profile, stripTemplates, cellTypes, seed: `MIN-CYCLE-4-${i}` });
    const check = validateFloorMap(map);
    assert.equal(check.ok, true, `Seed ${i} 校验失败：${check.errors.map((e) => e.message).join('; ')}`);
    assert.ok(
      map.stats.minCycleLength >= 4,
      `Seed ${i} 出现短环：minCycleLength=${map.stats.minCycleLength}`,
    );
    for (const r of map.stats.regions) {
      assert.ok(r.cycles >= 1);
      assert.ok(r.minCycleLength >= 4, `区域 ${r.name} minCycleLength=${r.minCycleLength}`);
    }
  }
});

test('绿线额外闭合连接与红区局部堆积在满足最小环长度前提下可增加区域环数与局部连接密度', () => {
  const cellTypes = defaultCellTypes();
  const stripTemplates = createDefaultStripTemplates(cellTypes);

  const baseProfile = createDefaultFloorProfile();
  baseProfile.regions.forEach((r) => {
    r.stripMin = 7;
    r.stripMax = 7;
    r.maxNoLoop = 0;
  });
  baseProfile.topology.minCycleLength = 4;
  baseProfile.topology.extraLoopChance = 0;
  baseProfile.topology.localClustering = false;

  const richProfile = structuredClone(baseProfile);
  richProfile.topology.extraLoopChance = 0.85;
  richProfile.topology.localClustering = true;

  let baseCycles = 0;
  let richCycles = 0;
  let maxClusterDeg = 0;
  for (let i = 1; i <= 10; i += 1) {
    const mBase = generateFloorMap({ profile: baseProfile, stripTemplates, cellTypes, seed: `LOOP-CMP-${i}` });
    const mRich = generateFloorMap({ profile: richProfile, stripTemplates, cellTypes, seed: `LOOP-CMP-${i}` });
    assert.equal(validateFloorMap(mBase).ok, true);
    assert.equal(validateFloorMap(mRich).ok, true);
    assert.ok(mRich.stats.minCycleLength >= 4);
    baseCycles += mBase.stats.regions.reduce((s, r) => s + r.cycles, 0);
    richCycles += mRich.stats.regions.reduce((s, r) => s + r.cycles, 0);
    for (const r of mRich.stats.regions) {
      if (r.maxDegree > maxClusterDeg) maxClusterDeg = r.maxDegree;
    }
  }
  assert.ok(richCycles > baseCycles, `额外闭合连接未提升环数：rich=${richCycles}, base=${baseCycles}`);
  assert.ok(maxClusterDeg >= 3, `局部密集堆积未形成多连接节点：maxDegree=${maxClusterDeg}`);
});

test('指定出生点区域（A/B/C）与留空全区随机均正确生效并重算距离', () => {
  const cellTypes = defaultCellTypes();
  const stripTemplates = createDefaultStripTemplates(cellTypes);

  // 指定 B 槽与 C 槽
  for (const slot of [0, 1, 2]) {
    const profile = createDefaultFloorProfile();
    profile.spawnRegionSlot = slot;
    const map = generateFloorMap({ profile, stripTemplates, cellTypes, seed: `SPAWN-SLOT-${slot}` });
    assert.equal(validateFloorMap(map).ok, true);
    assert.equal(map.spawnRegionSlot, slot);
    const spawnStrip = map.strips.find((s) => s.id === map.spawn.stripId);
    assert.equal(spawnStrip.regionId, map.regions[slot].id);
    assert.equal(spawnStrip.distanceFromSpawn, 0);
  }

  // 留空（random）应在多种子下覆盖多个槽位
  const seenSlots = new Set();
  for (let i = 1; i <= 24; i += 1) {
    const profile = createDefaultFloorProfile();
    profile.spawnRegionSlot = 'random';
    const map = generateFloorMap({ profile, stripTemplates, cellTypes, seed: `SPAWN-RAND-${i}` });
    assert.equal(validateFloorMap(map).ok, true);
    seenSlots.add(map.spawnRegionSlot);
    const spawnStrip = map.strips.find((s) => s.id === map.spawn.stripId);
    assert.equal(spawnStrip.regionId, map.regions[map.spawnRegionSlot].id);
  }
  assert.ok(seenSlots.size >= 2, `随机出生区域未覆盖多区：${[...seenSlots].join(',')}`);
});

test('完整地图四等级池：分栏结构、指定/随机抽取、四级生成与单份 JSON 导出导入往返', () => {
  const cellTypes = defaultCellTypes();
  const stripTemplates = createDefaultStripTemplates(cellTypes);
  const wp = createDefaultWorldPools();
  assert.equal(wp.pools.length, WORLD_TIER_COUNT);
  assert.deepEqual(validateWorldPools(wp), []);

  // 给等级 1 追加第二个配置并设更高权重
  const second = createWorldMapEntry(0, wp.pools[0].entries, {
    id: 'tier1_heavy',
    name: '等级 1 密集工厂图',
    weight: 4,
    spawnRegionSlot: 1,
  });
  wp.pools[0].entries.push(second);

  // 指定条目必命中
  const fixed = selectWorldEntry(wp.pools[0], { entryId: 'tier1_heavy', seed: 'PICK-1' });
  assert.equal(fixed.entry.id, 'tier1_heavy');
  assert.equal(fixed.pickedRandomly, false);

  // 留空时按权重随机抽取
  const hits = new Set();
  for (let i = 0; i < 20; i += 1) {
    const r = selectWorldEntry(wp.pools[0], { entryId: '', seed: `PICK-R-${i}` });
    assert.equal(r.pickedRandomly, true);
    hits.add(r.entry.id);
  }
  assert.equal(hits.size, 2);

  // 一键生成四张（分别测试 4 个等级）
  for (let tierIdx = 0; tierIdx < WORLD_TIER_COUNT; tierIdx += 1) {
    const res = generateWorldTierMap({
      worldPools: wp,
      tierIndex: tierIdx,
      entryId: '',
      seed: `WORLD-T${tierIdx + 1}`,
      stripTemplates,
      cellTypes,
    });
    assert.equal(res.tier, tierIdx + 1);
    assert.equal(validateFloorMap(res.map).ok, true);
    assert.ok(res.map.stats.minCycleLength >= 4);
  }

  // 导出单份 JSON -> 重新导入往返保持完全一致
  const jsonText = serializeWorldPools(wp);
  const parsed = parseWorldPools(jsonText);
  assert.equal(parsed.ok, true);
  assert.equal(parsed.worldPools.pools.length, 4);
  assert.equal(parsed.worldPools.pools[0].entries.length, 2);
  assert.equal(parsed.worldPools.pools[0].entries[1].spawnRegionSlot, 1);
  assert.equal(parsed.worldPools.pools[0].entries[0].spawnRegionSlot, 'random');
  assert.equal(parsed.worldPools.pools[0].entries[0].topology.minCycleLength, 4);

  // 校验非法最小环长度（大于区域最少条带数）
  const bad = normalizeWorldPools(wp);
  bad.pools[0].entries[0].topology.minCycleLength = 9;
  const issues = validateWorldPools(bad);
  assert.ok(issues.some((m) => m.includes('最小环长度')));
});
