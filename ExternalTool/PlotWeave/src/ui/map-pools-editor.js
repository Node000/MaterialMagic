import { validateFloorFile } from '../core/floor-file.js';
import { POOL_KIND, POOL_VERSION, validateMapPools } from '../core/map-pools.js';
import { h, mount } from './dom.js';

const SLOTS = ['A', 'B', 'C'];
const REGION_TYPES = ['city', 'factory', 'suburb', 'space'];
const copy = (value) => JSON.parse(JSON.stringify(value));
const entryKey = (pool, entry) => JSON.stringify([pool.id, entry.id]);
const effectiveEntry = ({ id, name, weight, patternLibraryId, regions, budget, progression }) =>
  ({ id, name, weight, patternLibraryId, regions, budget, progression });
const previewKey = (pool, entry, seed) => JSON.stringify([pool.id, effectiveEntry(entry), seed]);
const labelFor = (item, alternate) => [item.name || alternate, item.id].filter(Boolean).join(' · ');
const errorText = (error) => error?.message || String(error);
const emptyConfig = () => ({ kind: POOL_KIND, schemaVersion: POOL_VERSION, pools: [] });
const supportsConfig = (config) => config?.kind === POOL_KIND && config.schemaVersion === POOL_VERSION &&
  Array.isArray(config.pools) && config.pools.every((pool) => Array.isArray(pool?.entries) &&
    pool.entries.every((entry) => entry && entry.budget && entry.progression && entry.regions?.length === 3 &&
      Array.isArray(entry.regions) && entry.regions.every(Boolean)));
const isSeed = (seed) => Number.isInteger(seed) && seed >= -2147483648 && seed <= 2147483647;
const mapCellClass = (type) => {
  if (['combat', 'elite', 'enemy'].includes(type)) return 'enemy';
  if (['shop', 'reward', 'resource', 'treasure'].includes(type)) return 'resource';
  return 'floor';
};

function nextId(prefix, items) {
  const occupied = new Set(items.map(({ id }) => id));
  let serial = 1;
  while (occupied.has(`${prefix}_${serial}`)) serial += 1;
  return `${prefix}_${serial}`;
}

function newEntry(entries) {
  return {
    id: nextId('entry', entries), name: `候选条目 ${entries.length + 1}`, weight: 1, patternLibraryId: '',
    regions: REGION_TYPES.slice(0, 3).map((regionTypeId) => ({ regionTypeId, stripMin: 5, stripMax: 6, maxNoLoop: 1 })),
    budget: { resourceMin: null, resourceMax: null, difficultyMin: null, difficultyMax: null },
    progression: { baseLevel: 1, levelPerHop: 0.75, maxLevel: 20 },
  };
}

function field(label, attrs, callback) {
  const input = h('input', { class: 'input', ...attrs });
  input.addEventListener('input', () => callback(input.type === 'number'
    ? Number.isFinite(input.valueAsNumber) ? input.valueAsNumber : null
    : input.value));
  return h('label', { class: 'pools-field' }, h('span', null, label), input);
}

function choices(label, items, selected, alternate, busy, callback) {
  const control = h('select', {
    class: 'input', disabled: busy || !items.length, onchange: (event) => callback(Number(event.target.value)),
  }, items.length
    ? items.map((item, index) => h('option', { value: index }, labelFor(item, alternate)))
    : h('option', { value: '' }, alternate));
  if (items.length) control.value = String(selected);
  return { control, element: h('label', { class: 'pools-field' }, h('span', null, label), control) };
}

function button(text, callback, attrs = {}) {
  return h('button', { type: 'button', class: 'btn', onclick: callback, ...attrs }, text);
}

const svgNS = 'http://www.w3.org/2000/svg';
function svgElement(tag, attrs) {
  const element = document.createElementNS(svgNS, tag);
  for (const [name, value] of Object.entries(attrs)) element.setAttribute(name, value);
  return element;
}

function floorBounds(cells) {
  let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
  for (const { x, y } of cells) {
    minX = Math.min(minX, x); minY = Math.min(minY, y);
    maxX = Math.max(maxX, x); maxY = Math.max(maxY, y);
  }
  return { minX, minY, maxX, maxY, width: maxX - minX + 1, height: maxY - minY + 1 };
}

function drawFloor(floor, bounds) {
  const { minX, minY, width, height } = bounds;
  const svg = svgElement('svg', {
    class: 'pools-map', role: 'img', 'aria-label': '物理地板布局：地板格、墙、门与出生点',
    viewBox: `${minX - 0.4} ${minY - 0.4} ${width + 0.8} ${height + 0.8}`,
  });
  const openings = new Map(floor.edges.map(({ x, y, dir, type }) => [`${x},${y}:${dir}`, type]));
  const drawnBorders = new Set();
  const cells = new Map();
  const walls = [], doors = [];
  for (const { x, y, type } of floor.cells) {
    // Batch paths per content type so even dense floors do not need one DOM node per cell.
    const key = mapCellClass(type);
    if (!cells.has(key)) cells.set(key, []);
    cells.get(key).push(`M${x} ${y}h1v1h-1z`);
    for (const [bx, by, dir] of [[x, y, 'E'], [x - 1, y, 'E'], [x, y, 'S'], [x, y - 1, 'S']]) {
      const border = `${bx},${by}:${dir}`;
      if (drawnBorders.has(border)) continue;
      drawnBorders.add(border);
      if (openings.get(border) === 'open') continue;
      const wall = dir === 'E' ? `M${bx + 1} ${by}v1` : `M${bx} ${by + 1}h1`;
      if (openings.get(border) !== 'door') {
        walls.push(wall);
      } else if (dir === 'E') {
        walls.push(`M${bx + 1} ${by}v.22m0 .56v.22`);
        doors.push(`M${bx + 1} ${by + 0.22}v.56`);
      } else {
        walls.push(`M${bx} ${by + 1}h.22m.56 0h.22`);
        doors.push(`M${bx + 0.22} ${by + 1}h.56`);
      }
    }
  }
  for (const [kind, paths] of cells) svg.append(svgElement('path', { class: `pools-map-cell-${kind}`, d: paths.join('') }));
  svg.append(svgElement('path', { class: 'pools-map-walls', d: walls.join('') }));
  svg.append(svgElement('path', { class: 'pools-map-doors', d: doors.join('') }));
  svg.append(svgElement('circle', {
    class: 'pools-map-spawn', cx: floor.spawn.x + 0.5, cy: floor.spawn.y + 0.5, r: 0.33,
  }));
  return svg;
}

function showNumber(value) {
  return Number.isFinite(value) ? value.toLocaleString('zh-CN', { maximumFractionDigits: 2 }) : '—';
}

function stat(title, actual, detail, inRange) {
  return h('div', { class: ['pools-stat', inRange === true ? 'pools-hit' : inRange === false ? 'pools-miss' : ''].join(' ') },
    h('dt', null, title), h('dd', null, actual), h('small', null, detail));
}

function budgetStat(title, key, floor, metrics, entry) {
  const actual = metrics[key] ?? floor.budget?.actual?.[key];
  const target = floor.budget?.target || entry.budget;
  const min = target[`${key}Min`], max = target[`${key}Max`];
  let inRange = metrics[`${key}InRange`];
  if (typeof inRange !== 'boolean' && Number.isFinite(actual) && (Number.isFinite(min) || Number.isFinite(max))) {
    inRange = (!Number.isFinite(min) || actual >= min) && (!Number.isFinite(max) || actual <= max);
  }
  const bounds = min == null ? max == null ? '不限' : `≤ ${showNumber(max)}` : max == null ? `≥ ${showNumber(min)}` : `${showNumber(min)}–${showNumber(max)}`;
  return stat(title, showNumber(actual), `目标 ${bounds}${inRange === true ? ' · 达标' : inRange === false ? ' · 未达标' : ''}`, inRange);
}

export class MapPoolsEditor {
  constructor(root, { getPools, updatePools, saveDraft, preview, publish, loadPublished, listPatterns }) {
    Object.assign(this, { root, getPools, updatePools, saveDraft, preview, publish, loadPublished, listPatterns });
    this.poolIndex = 0;
    this.entryIndex = 0;
    this.seeds = new Map();
    this.previews = new Map();
    this.patterns = [];
    this.epoch = 0;
    this.statuses = new Map();
    this.initialSeed = globalThis.crypto.getRandomValues(new Int32Array(1))[0];
    this.render();
  }

  config() {
    return this.getPools() ?? emptyConfig();
  }

  selection(config = this.config()) {
    this.poolIndex = Math.max(0, Math.min(this.poolIndex, config.pools.length - 1));
    const pool = config.pools[this.poolIndex];
    this.entryIndex = Math.max(0, Math.min(this.entryIndex, (pool?.entries.length ?? 0) - 1));
    return { pool, entry: pool?.entries[this.entryIndex] };
  }

  seedFor(pool, entry) {
    const key = entryKey(pool, entry);
    return this.seeds.has(key) ? this.seeds.get(key) : this.initialSeed;
  }

  resultFor(pool, entry) {
    if (!pool || !entry) return null;
    return this.previews.get(previewKey(pool, entry, this.seedFor(pool, entry))) ?? null;
  }

  setStatus(channel, text, level = 'info', details = []) {
    if (text) this.statuses.set(channel, { text, level, details });
    else this.statuses.delete(channel);
  }

  async refresh({ reset = false, fetchCatalog = true } = {}) {
    if (reset) {
      this.epoch += 1;
      this.statuses.clear();
      this.patterns = [];
      this.seeds.clear();
      this.previews.clear();
      this.poolIndex = this.entryIndex = 0;
      this.render();
    }
    if (!fetchCatalog || !this.listPatterns) return;
    const epoch = this.epoch;
    if (this.catalogTask?.epoch === epoch) return this.catalogTask.promise;
    const promise = Promise.resolve().then(async () => {
      try {
        const response = await this.listPatterns();
        if (epoch !== this.epoch) return;
        if (!Array.isArray(response?.patterns) || !response.patterns.every((item) => typeof item?.id === 'string' &&
          (item.regionTypeIds == null || (Array.isArray(item.regionTypeIds) && item.regionTypeIds.every((id) => typeof id === 'string'))))) {
          throw Error('模板库目录返回了无效的格式。');
        }
        this.patterns = response.patterns;
        this.setStatus('catalog', null);
      } catch (error) {
        if (epoch === this.epoch) this.setStatus('catalog', `无法读取模板库目录：${errorText(error)}`, 'error', error?.details);
      } finally {
        if (epoch === this.epoch) {
          this.catalogTask = null;
          this.syncCatalog();
          this.renderState();
        }
      }
    });
    this.catalogTask = { epoch, promise };
    return promise;
  }

  modify(change, { rebuild = true } = {}) {
    if (this.busy) return;
    const config = copy(this.config());
    const { pool, entry } = this.selection(config);
    this.inUpdate = true;
    this.setStatus('action', null);
    try {
      change(config, pool, entry);
      this.updatePools(config);
      // Keep previews of other unchanged entries, not old versions of edited/removed ones.
      const liveKeys = new Set(config.pools.flatMap((item) => item.entries.map((candidate) =>
        previewKey(item, candidate, this.seedFor(item, candidate)))));
      for (const key of this.previews.keys()) if (!liveKeys.has(key)) this.previews.delete(key);
    } catch (error) {
      this.setStatus('action', `修改草稿失败：${errorText(error)}`, 'error', error?.details);
    } finally {
      this.inUpdate = false;
      if (rebuild) this.render();
      else {
        this.syncChoices();
        this.syncCatalog();
        this.renderState();
      }
    }
  }

  previewProblem(pool, entry, configIssues) {
    if (!pool || !entry) return '请选择要预览的关卡池和候选条目。';
    if (!isSeed(this.seedFor(pool, entry))) return '预览种子必须是有符号 32 位整数。';
    return configIssues.length ? '请先修正整份草稿的校验问题，再生成预览。' : null;
  }

  async run(label, operation) {
    if (this.busy) return;
    const epoch = this.epoch;
    this.busy = label;
    this.setStatus('action', null);
    this.render();
    try {
      await operation(() => this.epoch === epoch);
    } catch (error) {
      if (this.epoch === epoch) this.setStatus('action', `${label}失败：${errorText(error)}`, 'error', error?.details);
    } finally {
      this.busy = null;
      this.render();
    }
  }

  async previewSelected() {
    const config = copy(this.config());
    const { pool, entry } = this.selection(config);
    if (this.previewProblem(pool, entry, validateMapPools(config))) return;
    const seed = this.seedFor(pool, entry);
    const key = previewKey(pool, entry, seed);
    const { id: poolId } = pool, { id: entryId } = entry;
    await this.run('生成预览', async (current) => {
      this.previews.delete(key);
      this.renderState();
      const response = await this.preview({ config, poolId, entryId, seed });
      if (!current()) return;
      const floor = validateFloorFile(response?.floor);
      if (!response.metrics || typeof response.metrics !== 'object') throw Error('预览结果缺少指标。');
      this.previews.set(key, { floor, metrics: response.metrics, bounds: floorBounds(floor.cells), entry, seed });
      this.setStatus('action', `已生成「${entry.name}」的预览。`, 'success');
    });
  }

  save() {
    const config = copy(this.config());
    return this.run('保存草稿', async (current) => {
      await this.saveDraft(config);
      if (current()) this.setStatus('action', '关卡地图池草稿已保存；游戏配置未改动。', 'success');
    });
  }

  applyPublished() {
    if (this.busy || !window.confirm('读取已发布配置会替换当前浏览器中的关卡池草稿。是否继续？')) return;
    return this.run('读取已发布配置', async (current) => {
      const config = await this.loadPublished();
      if (!current()) return;
      if (config === null) {
        this.setStatus('action', '尚无已发布的关卡地图池；当前草稿未改动。');
        return;
      }
      const issues = validateMapPools(config);
      if (issues.length) {
        const error = Error('已发布配置未通过校验，当前草稿未改动。');
        error.details = issues;
        throw error;
      }
      this.updatePools(copy(config));
      this.previews.clear();
      this.seeds.clear();
      this.poolIndex = this.entryIndex = 0;
      this.setStatus('action', '已用已发布配置更新当前草稿，原预览已失效。', 'success');
    });
  }

  publishCurrent() {
    const config = copy(this.config());
    const { pool, entry } = this.selection(config);
    if (this.busy || !this.resultFor(pool, entry) || !this.readiness(config).ready) return;
    if (!window.confirm('发布会把当前关卡地图池写入游戏配置。此操作不会把地图池绑定到正式章节。是否继续？')) return;
    return this.run('发布地图池', async (current) => {
      try {
        const response = await this.publish({ config });
        if (current()) this.setStatus('action', `关卡地图池已发布${response?.path ? `：${response.path}` : '。'}`, 'success');
      } catch (error) {
        if (current() && Array.isArray(error.details)) {
          const needsPreview = new Set(error.details);
          for (const item of config.pools) for (const candidate of item.entries) {
            if (needsPreview.has(`${item.id} / ${candidate.id}`)) {
              this.previews.delete(previewKey(item, candidate, this.seedFor(item, candidate)));
            }
          }
        }
        throw error;
      }
    });
  }

  readiness(config) {
    let total = 0, approved = 0, empty = 0;
    for (const pool of config.pools) {
      if (!pool.entries.length) empty += 1;
      for (const entry of pool.entries) {
        total += 1;
        if (this.resultFor(pool, entry)) approved += 1;
      }
    }
    const issues = validateMapPools(config);
    return { total, approved, empty, issues, ready: total > 0 && total === approved && issues.length === 0 };
  }

  syncChoices() {
    const config = this.config();
    const { pool } = this.selection(config);
    const update = (control, items, label) => {
      for (const [index, item] of items.entries()) {
        if (control?.options[index]) control.options[index].textContent = labelFor(item, label);
      }
    };
    update(this.poolChoice, config.pools, '未命名关卡池');
    update(this.entryChoice, pool?.entries ?? [], '未命名候选条目');
  }

  syncCatalog() {
    if (!this.root.contains(this.patternOptions) || !this.root.contains(this.regionOptions) || !supportsConfig(this.config())) return;
    const { entry } = this.selection();
    mount(this.patternOptions, this.patterns.map(({ id }) => h('option', { value: id })));
    const selected = this.patterns.find((item) => item.id === entry?.patternLibraryId);
    const ids = selected?.regionTypeIds ?? this.patterns.flatMap((item) => item.regionTypeIds ?? []);
    mount(this.regionOptions, [...new Set([...REGION_TYPES, ...ids])].map((id) => h('option', { value: id })));
  }

  renderState() {
    if (!this.statusEl) return;
    const config = this.config();
    if (!supportsConfig(config)) return;
    const { pool, entry } = this.selection(config);
    const progress = this.readiness(config);
    const invalid = this.previewProblem(pool, entry, progress.issues);
    const result = this.resultFor(pool, entry);
    this.previewBtn.disabled = Boolean(this.busy || !this.preview || invalid);
    this.publishBtn.disabled = Boolean(this.busy || !this.publish || !result || !progress.ready);
    this.progressEl.textContent = `已预览 ${progress.approved} / ${progress.total} 个当前条目${progress.empty ? ` · ${progress.empty} 个空池` : ''}${progress.ready ? ' · 可以发布' : ''}`;
    this.invalidEl.textContent = this.busy ? `${this.busy}中…` : invalid || (result ? '此预览仅验证当前种子，其他种子的结果可能不同。' : '此条目还没有当前设置及种子的预览。');
    mount(this.statusEl,
      [...this.statuses.values()].map(({ text, level, details }) => h('div', {
        class: `pools-notice pools-${level}`, role: level === 'error' ? 'alert' : 'status',
      }, text, Array.isArray(details) && details.length ? h('ul', null, details.map((detail) => h('li', null, String(detail)))) : null)),
      progress.total > 0 && progress.issues.length > 0 && h('details', { class: 'pools-notice pools-error' },
        h('summary', null, `草稿校验问题 × ${progress.issues.length} · ${progress.issues[0]}`),
        progress.issues.length > 1 && h('ul', null, progress.issues.slice(1).map((issue) => h('li', null, issue)))));
    if (this.drawnResult === result) return;
    this.drawnResult = result;
    if (!result) {
      mount(this.previewEl, h('div', { class: 'pools-preview-empty' }, '选择候选条目并生成预览，以查看实际地板和预算。'));
      return;
    }
    const { floor, metrics, bounds, seed } = result;
    mount(this.previewEl,
      h('p', { class: 'pools-subtle' }, `预览种子：${floor.seed ?? seed}`),
      h('dl', { class: 'pools-stats' },
        stat('物理格数', showNumber(metrics.tileCount ?? floor.cells.length), `边 ${floor.edges.length} 条`),
        stat('地图范围', `${bounds.width} × ${bounds.height}`, `(${bounds.minX}, ${bounds.minY}) → (${bounds.maxX}, ${bounds.maxY})`),
        budgetStat('实际资源', 'resource', floor, metrics, result.entry),
        budgetStat('实际难度', 'difficulty', floor, metrics, result.entry)),
      drawFloor(floor, bounds),
      h('p', { class: 'pools-subtle pools-legend' }, '蓝色地板 · 红色战斗 · 紫色商店/奖励 · 细线墙面 · 琥珀色门 · 绿色圆点出生点'));
  }

  render() {
    // updatePools may synchronously emit a store change. Avoid replacing the focused field on each keystroke.
    if (this.inUpdate) return;
    const config = this.config();
    if (!supportsConfig(config)) {
      mount(this.root, h('p', { class: 'pools-notice pools-error', role: 'alert' }, '当前草稿不是支持的关卡地图池格式。'));
      return;
    }
    const { pool, entry } = this.selection(config);
    this.previewBtn = button('生成预览', () => this.previewSelected(), { class: 'btn primary' });
    this.publishBtn = button('发布到游戏配置', () => this.publishCurrent());
    this.progressEl = h('p', { class: 'pools-subtle', role: 'status' });
    this.invalidEl = h('p', { class: 'pools-subtle', role: 'status' });
    this.statusEl = h('div', { class: 'pools-notices' });
    this.previewEl = h('div', { class: 'pools-preview' });
    this.patternOptions = this.regionOptions = null;
    this.drawnResult = undefined;
    const pools = choices('关卡池', config.pools, this.poolIndex, '无关卡池', this.busy, (index) => {
      this.poolIndex = index;
      this.entryIndex = 0;
      this.render();
    });
    const entries = choices('候选条目', pool?.entries ?? [], this.entryIndex, '无候选条目', this.busy, (index) => {
      this.entryIndex = index;
      this.render();
    });
    this.poolChoice = pools.control;
    this.entryChoice = entries.control;
    const poolField = (label, key) => field(label, { value: pool[key], disabled: this.busy }, (value) =>
      this.modify((_config, item) => { item[key] = value; }, { rebuild: false }));
    const selector = h('section', { class: 'pools-card', 'aria-label': '选择和编辑关卡池' },
      h('div', { class: 'pools-row' }, pools.element,
        button('新增关卡池', () => this.modify((draft) => {
          const id = nextId('level', draft.pools);
          draft.pools.push({ id, name: `关卡池 ${draft.pools.length + 1}`, entries: [] });
          this.poolIndex = draft.pools.length - 1;
          this.entryIndex = 0;
        }), { disabled: this.busy })),
      pool && h('div', { class: 'pools-row' }, poolField('关卡池 ID', 'id'), poolField('关卡池名称', 'name'),
        button('删除关卡池', () => {
          if (window.confirm(`删除关卡池「${pool.name || pool.id}」及其中的 ${pool.entries.length} 个条目？`)) {
            this.modify((draft) => { draft.pools.splice(this.poolIndex, 1); this.entryIndex = 0; });
          }
        }, { class: 'btn danger', disabled: this.busy })),
      pool && h('div', { class: 'pools-row' }, entries.element,
        button('新增候选条目', () => this.modify((_draft, item) => {
          item.entries.push(newEntry(item.entries));
          this.entryIndex = item.entries.length - 1;
        }), { disabled: this.busy }),
        entry && button('删除条目', () => {
          if (window.confirm(`删除候选条目「${entry.name || entry.id}」？`)) {
            this.modify((_draft, item) => { item.entries.splice(this.entryIndex, 1); });
          }
        }, { class: 'btn danger', disabled: this.busy })),
      !pool && h('p', { class: 'pools-subtle' }, '先新增关卡池，再为其添加带权重的候选条目。'));
    const settings = entry ? this.renderEntry(pool, entry) : null;
    const scrollTop = this.editorEl?.scrollTop ?? 0;
    this.editorEl = h('div', { class: 'map-pools-editor' },
      h('header', { class: 'pools-header' },
        h('div', null, h('h2', null, '关卡地图池'), h('p', { class: 'pools-subtle' }, '为每个关卡定义可按权重抽取的候选方案，逐条预览后再发布。')),
        h('div', { class: 'pools-actions' },
          this.loadPublished && button('读取已发布', () => this.applyPublished(), { disabled: this.busy }),
          this.saveDraft && button('保存草稿', () => this.save(), { disabled: this.busy }),
          this.publishBtn)),
      this.progressEl, this.statusEl,
      h('div', { class: 'pools-columns' }, h('div', { class: 'pools-settings-column' }, selector, settings),
        h('section', { class: 'pools-card pools-preview-card', 'aria-label': '地图预览' },
          h('div', { class: 'pools-row pools-preview-header' }, h('h3', null, '物理地板预览'), this.previewBtn),
          this.invalidEl, this.previewEl)));
    mount(this.root, this.editorEl);
    this.editorEl.scrollTop = scrollTop;
    this.syncCatalog();
    this.renderState();
  }

  renderEntry(pool, entry) {
    this.patternOptions = h('datalist', { id: 'map-pool-pattern-options' });
    this.regionOptions = h('datalist', { id: 'map-pool-region-options' });
    const fieldAt = (label, path, attrs = {}) => {
      const value = path.reduce((item, key) => item?.[key], entry);
      return field(label, { value: value ?? '', disabled: this.busy, ...attrs }, (next) => this.modify((_config, _pool, item) => {
        const parent = path.slice(0, -1).reduce((object, key) => object[key], item);
        parent[path[path.length - 1]] = next;
      }, { rebuild: false }));
    };
    const numeric = (label, path, options = {}) => fieldAt(label, path, { type: 'number', min: 0, ...options });
    const seedField = field('预览种子', { type: 'number', min: -2147483648, max: 2147483647, step: 1,
      value: this.seedFor(pool, entry) ?? '', disabled: this.busy }, (value) => {
      const selected = this.selection();
      if (!selected.entry) return;
      const oldSeed = this.seedFor(selected.pool, selected.entry);
      this.seeds.set(entryKey(selected.pool, selected.entry), value);
      if (oldSeed !== value) this.previews.delete(previewKey(selected.pool, selected.entry, oldSeed));
      this.setStatus('action', null);
      this.renderState();
    });
    return h('section', { class: 'pools-card', 'aria-label': '编辑候选条目' },
      h('h3', null, '候选方案'),
      h('div', { class: 'pools-row' }, fieldAt('条目 ID', ['id']), fieldAt('条目名称', ['name']), numeric('抽取权重', ['weight'], { step: 'any' })),
      h('div', { class: 'pools-row' }, fieldAt('模板库 ID', ['patternLibraryId'], { list: 'map-pool-pattern-options', placeholder: '选择或填写已发布的模板库 ID' }), seedField),
      this.patternOptions, this.regionOptions,
      h('h4', null, '区域顺序 A → B → C'),
      h('p', { class: 'pools-subtle' }, 'A 是出生区；三个槽位可复用相同的区域类型 ID。'),
      SLOTS.map((slot, index) => h('fieldset', { class: 'pools-region' },
        h('legend', null, index === 0 ? `${slot} · 出生区` : `${slot} · 翼区`),
        h('div', { class: 'pools-row' },
          fieldAt('区域类型 ID', ['regions', index, 'regionTypeId'], { list: 'map-pool-region-options' }),
          numeric('条带最少', ['regions', index, 'stripMin'], { min: 3, max: 20 }),
          numeric('条带最多', ['regions', index, 'stripMax'], { min: 3, max: 20 }),
          numeric('非环条带上限', ['regions', index, 'maxNoLoop'], { max: 20 })))),
      h('h4', null, '目标预算'),
      h('p', { class: 'pools-subtle' }, '上下限可留空表示不限；未达预算仍可查看和发布。'),
      h('div', { class: 'pools-row' },
        numeric('资源下限', ['budget', 'resourceMin'], { step: 'any' }),
        numeric('资源上限', ['budget', 'resourceMax'], { step: 'any' }),
        numeric('难度下限', ['budget', 'difficultyMin'], { step: 'any' }),
        numeric('难度上限', ['budget', 'difficultyMax'], { step: 'any' })),
      h('h4', null, '敌人等级进程'),
      h('div', { class: 'pools-row' },
        numeric('初始等级', ['progression', 'baseLevel'], { min: 1 }),
        numeric('每跳等级增量', ['progression', 'levelPerHop'], { step: 'any' }),
        numeric('等级上限', ['progression', 'maxLevel'], { min: 1 })));
  }
}
