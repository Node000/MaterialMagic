export const POOL_KIND = 'plotweave-map-pools';
export const POOL_VERSION = 1;
export const POOL_ID = /^[A-Za-z0-9_-]{1,64}$/;
export const PATTERN_HASH = /^[0-9a-f]{64}$/;
export const BUDGET_FIELDS = ['resourceMin', 'resourceMax', 'difficultyMin', 'difficultyMax'];
export const RING_SPACING = ['legacy', 'balanced', 'spacious'];

export function defaultMapPools(profile) {
  const p = profile || {};
  return { kind: POOL_KIND, schemaVersion: POOL_VERSION, pools: [{ id: 'level-1', name: '等级 1', entries: [{
    id: 'standard-triangle', name: '标准三角地图', weight: 1, patternLibraryId: 'Sample',
    regions: (p.regions || ['city', 'factory', 'suburb'].map(id => ({ id }))).map(r => ({
      regionTypeId: r.id, stripMin: r.stripMin ?? 5, stripMax: r.stripMax ?? 6, maxNoLoop: r.maxNoLoop ?? 1,
    })),
    budget: Object.fromEntries(BUDGET_FIELDS.map(key => [key, p.budget?.[key] ?? null])),
    progression: { baseLevel: 1, levelPerHop: .75, maxLevel: 20, ...p.progression },
  }] }] };
}

export function validateMapPools(config) {
  const errors = [];
  const errorsAt = (label, message) => errors.push(`${label}：${message}`);
  const finite = (n, min) => typeof n === 'number' && Number.isFinite(n) && n >= min;
  const integer = (n, min, max) => Number.isInteger(n) && n >= min && n <= max;
  const isId = value => typeof value === 'string' && POOL_ID.test(value);
  const name = value => typeof value === 'string' && value.trim().length > 0;
  if (config?.kind !== POOL_KIND || config.schemaVersion !== POOL_VERSION) return ['不是受支持的地图池配置（v1）。'];
  if (!Array.isArray(config.pools) || config.pools.length === 0 || config.pools.length > 64) return ['需要 1–64 个等级池。'];
  const poolIds = new Set();
  for (const [i, pool] of config.pools.entries()) {
    const pl = `池 ${pool?.id || i + 1}`;
    if (!isId(pool?.id) || poolIds.has(pool.id)) errorsAt(pl, '池 ID 无效或重复。');
    if (!name(pool?.name)) errorsAt(pl, '名称不能为空。');
    poolIds.add(pool?.id);
    if (!Array.isArray(pool?.entries) || pool.entries.length === 0 || pool.entries.length > 64) {
      errorsAt(pl, '需要 1–64 条配方。'); continue;
    }
    const entryIds = new Set();
    for (const [j, e] of pool.entries.entries()) {
      const el = `${pl} / ${e?.id || j + 1}`;
      if (!isId(e?.id) || entryIds.has(e.id)) errorsAt(el, '配方 ID 无效或重复。');
      if (!name(e?.name)) errorsAt(el, '名称不能为空。');
      entryIds.add(e?.id);
      if (!e) continue;
      if (!finite(e.weight, Number.MIN_VALUE)) errorsAt(el, '权重必须为有限正数。');
      if (!isId(e.patternLibraryId)) errorsAt(el, 'Pattern 库 ID 无效。');
      if (e.ringSpacing !== undefined && !RING_SPACING.includes(e.ringSpacing)) errorsAt(el, '环路留白设置无效。');
      if (e.patternSha256 !== undefined && (typeof e.patternSha256 !== 'string' || !PATTERN_HASH.test(e.patternSha256))) errorsAt(el, 'Pattern 摘要无效。');
      if (!Array.isArray(e.regions) || e.regions.length !== 3) errorsAt(el, '必须按 A/B/C 指定三个区域槽。');
      else for (const [k, r] of e.regions.entries()) {
        if (!isId(r?.regionTypeId) || !integer(r.stripMin, 3, 20) || !integer(r.stripMax, r.stripMin, 20) || !integer(r.maxNoLoop, 0, 20))
          errorsAt(el, `槽 ${'ABC'[k]} 的区域 ID、条带范围或末端数量无效。`);
      }
      for (const key of ['resource', 'difficulty']) {
        const min = e.budget?.[`${key}Min`], max = e.budget?.[`${key}Max`];
        if ((min !== null && !finite(min, 0)) || (max !== null && !finite(max, 0)) || (min !== null && max !== null && min > max))
          errorsAt(el, `${key === 'resource' ? '资源' : '难度'}目标区间无效；自动值留空。`);
      }
      const p = e.progression;
      if (!integer(p?.baseLevel, 1, 2147483647) || !integer(p.maxLevel, p.baseLevel, 2147483647) || !finite(p.levelPerHop, 0) || p.levelPerHop > 3.4028234e38)
        errorsAt(el, '等级、每跳等级增长或等级上限无效。');
    }
  }
  return errors;
}
