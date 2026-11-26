import { DEFAULT_CELL_TYPES, DUNGEON_REGION_PRESETS, clamp } from '../core/constants.js';
import { FloorExploration, EXPLORATION_RADIUS, explorationDirection } from '../core/floor-exploration.js';
import { validateFloorFile } from '../core/floor-file.js';
import { POOL_KIND, POOL_VERSION, RING_SPACING, validateMapPools } from '../core/map-pools.js';
import { cellTypeMap } from '../core/strip-templates.js';
import { downloadBlob, h, icon, mount, slugify } from './dom.js';
import { FloorCanvas } from './floor-canvas.js';
import { toast } from './modals.js';
import { adaptPoolPreviewToFloorMap, regionPaletteForEntry } from './pool-floor-adapter.js';

const SLOTS = ['A', 'B', 'C'];
const REGION_TYPES = ['city', 'factory', 'suburb', 'space'];
const copy = (value) => JSON.parse(JSON.stringify(value));
const entryKey = (pool, entry) => JSON.stringify([pool.id, entry.id]);
const effectiveEntry = ({ id, name, weight, patternLibraryId, regions, budget, progression, ringSpacing }) => ({
  id,
  name,
  weight,
  patternLibraryId,
  regions,
  budget,
  progression,
  ringSpacing,
});
const ringSpacingLabels = { legacy: '现有', balanced: '均衡', spacious: '舒展' };
const previewKey = (pool, entry, seed) => JSON.stringify([pool.id, effectiveEntry(entry), seed]);
const labelFor = (item, alternate) => [item.name || alternate, item.id].filter(Boolean).join(' · ');
const errorText = (error) => error?.message || String(error);
const emptyConfig = () => ({ kind: POOL_KIND, schemaVersion: POOL_VERSION, pools: [] });
const supportsConfig = (config) =>
  config?.kind === POOL_KIND &&
  config.schemaVersion === POOL_VERSION &&
  Array.isArray(config.pools) &&
  config.pools.every(
    (pool) =>
      Array.isArray(pool?.entries) &&
      pool.entries.every(
        (entry) =>
          entry &&
          entry.budget &&
          entry.progression &&
          entry.regions?.length === 3 &&
          Array.isArray(entry.regions) &&
          entry.regions.every(Boolean),
      ),
  );
const isSeed = (seed) => Number.isInteger(seed) && seed >= -2147483648 && seed <= 2147483647;

function randomInt32Seed() {
  return globalThis.crypto.getRandomValues(new Int32Array(1))[0];
}

function nextId(prefix, items) {
  const occupied = new Set(items.map(({ id }) => id));
  let serial = 1;
  while (occupied.has(`${prefix}_${serial}`)) serial += 1;
  return `${prefix}_${serial}`;
}

function newEntry(entries) {
  return {
    id: nextId('entry', entries),
    name: `候选条目 ${entries.length + 1}`,
    weight: 1,
    patternLibraryId: '',
    regions: REGION_TYPES.slice(0, 3).map((regionTypeId) => ({
      regionTypeId,
      stripMin: 5,
      stripMax: 6,
      maxNoLoop: 1,
    })),
    budget: { resourceMin: null, resourceMax: null, difficultyMin: null, difficultyMax: null },
    progression: { baseLevel: 1, levelPerHop: 0.75, maxLevel: 20 },
  };
}

function miniField(label, attrs, callback, { mono = false, title = null } = {}) {
  const input = h('input', { class: `input${mono ? ' mono-input' : ''}`, ...attrs });
  input.addEventListener('input', () =>
    callback(
      input.type === 'number'
        ? Number.isFinite(input.valueAsNumber)
          ? input.valueAsNumber
          : null
        : input.value,
    ),
  );
  return {
    input,
    element: h('label', { class: 'mini-field', ...(title ? { title } : {}) }, h('span', null, label), input),
  };
}

function selectControl(items, selected, alternate, busy, callback) {
  const control = h(
    'select',
    {
      class: 'select',
      disabled: busy || !items.length,
      onchange: (event) => callback(Number(event.target.value)),
    },
    items.length
      ? items.map((item, index) => h('option', { value: index }, labelFor(item, alternate)))
      : h('option', { value: '' }, alternate),
  );
  if (items.length) control.value = String(selected);
  return control;
}

function button(text, callback, attrs = {}) {
  return h('button', { type: 'button', class: 'btn', onclick: callback, ...attrs }, text);
}

const svgNS = 'http://www.w3.org/2000/svg';
function svgElement(tag, attrs = {}) {
  const element = document.createElementNS(svgNS, tag);
  for (const [name, value] of Object.entries(attrs)) {
    if (value != null) element.setAttribute(name, String(value));
  }
  return element;
}

function floorBounds(cells) {
  let minX = Infinity;
  let minY = Infinity;
  let maxX = -Infinity;
  let maxY = -Infinity;
  for (const { x, y } of cells) {
    minX = Math.min(minX, x);
    minY = Math.min(minY, y);
    maxX = Math.max(maxX, x);
    maxY = Math.max(maxY, y);
  }
  return { minX, minY, maxX, maxY, width: maxX - minX + 1, height: maxY - minY + 1 };
}

function cellDefFor(type, cellTypes = DEFAULT_CELL_TYPES) {
  const tMap = cellTypeMap(cellTypes);
  return tMap.get(type) || tMap.get('empty') || { id: type || 'empty', name: type || '空通道', short: type || '空', icon: '·', color: '#5b6c7d' };
}

function contentName(cell, cellTypes = DEFAULT_CELL_TYPES) {
  if (!cell) return '地板';
  const def = cellDefFor(cell.type, cellTypes);
  const name = def.name || def.short || cell.type || '地板';
  return Number.isFinite(cell.enemyLevel) ? `${def.icon ? `${def.icon} ` : ''}${name} · E${cell.enemyLevel}` : `${def.icon && cell.type !== 'empty' ? `${def.icon} ` : ''}${name}`;
}

function drawExploration(cells, cellTypes = DEFAULT_CELL_TYPES) {
  const bounds = floorBounds(cells);
  // Keep the whole map in frame without magnifying very small maps to fill the canvas.
  const width = Math.max(11, bounds.width) + 1.1;
  const height = Math.max(11, bounds.height) + 1.1;
  const left = bounds.minX + (bounds.width - width) / 2;
  const top = bounds.minY + (bounds.height - height) / 2;
  const svg = svgElement('svg', {
    class: 'pools-walk-svg',
    role: 'img',
    'aria-label': '探索地图：高亮框标记当前位置，暗格仅显示地图轮廓',
    viewBox: `${left} ${top} ${width} ${height}`,
  });

  const gridPaths = [];
  for (let x = Math.ceil(left); x <= left + width; x += 1) gridPaths.push(`M${x} ${top}v${height}`);
  for (let y = Math.ceil(top); y <= top + height; y += 1) gridPaths.push(`M${left} ${y}h${width}`);
  svg.append(svgElement('path', { class: 'pools-walk-grid', d: gridPaths.join('') }));

  const walls = new Set();
  const doors = new Set();
  const fog = new Set();
  const silhouette = [];
  const hiddenTiles = [];
  const overlayNodes = [];

  for (const cell of cells) {
    const { x, y, visible, current, type, enemyLevel, borders } = cell;
    for (const [dir, bx, by, axis] of [
      ['N', x, y, 'h'],
      ['E', x + 1, y, 'v'],
      ['S', x, y + 1, 'h'],
      ['W', x, y, 'v'],
    ]) {
      const head = `M${bx} ${by}`;
      if (borders[dir] === 'outline') silhouette.push(`${head}${axis}1`);
      if (borders[dir] === 'wall') walls.add(`${head}${axis}1`);
      if (borders[dir] === 'fog') fog.add(`${head}${axis}1`);
      if (borders[dir] === 'door') {
        walls.add(`${head}${axis}.22${axis === 'h' ? 'm.56 0' : 'm0 .56'}${axis}.22`);
        doors.add(`${head}${axis}.22${axis}.56`);
      }
    }

    if (!visible) {
      hiddenTiles.push(`M${x} ${y}h1v1h-1z`);
      continue;
    }

    const def = cellDefFor(type, cellTypes);
    const isEmpty = !type || type === 'empty';
    const tileRect = svgElement('rect', {
      class: `pools-walk-cell${isEmpty ? ' is-empty' : ''}${current ? ' is-current' : ''}`,
      x,
      y,
      width: 1,
      height: 1,
      style: isEmpty ? null : `fill:${def.color};`,
    });
    const title = svgElement('title');
    const fullName = contentName(cell, cellTypes);
    title.textContent = current ? `当前位置：${fullName}` : fullName;
    tileRect.append(title);
    svg.append(tileRect);

    if (current) {
      overlayNodes.push(
        svgElement('rect', {
          class: 'pools-walk-player-ring',
          x: x + 0.07,
          y: y + 0.07,
          width: 0.86,
          height: 0.86,
          rx: 0.14,
        }),
      );
    }

    if (!isEmpty) {
      const iconText = svgElement('text', {
        class: 'pools-walk-icon',
        x: x + 0.5,
        y: y + (Number.isFinite(enemyLevel) ? 0.36 : 0.41),
      });
      iconText.textContent = def.icon || '•';
      overlayNodes.push(iconText);

      if (Number.isFinite(enemyLevel)) {
        overlayNodes.push(
          svgElement('rect', {
            class: 'pools-walk-enemy-bg',
            x: x + 0.2,
            y: y + 0.58,
            width: 0.6,
            height: 0.3,
            rx: 0.08,
          }),
        );
        const lvl = svgElement('text', {
          class: 'pools-walk-enemy-text',
          x: x + 0.5,
          y: y + 0.74,
        });
        lvl.textContent = `E${enemyLevel}`;
        overlayNodes.push(lvl);
      } else {
        const nameText = svgElement('text', {
          class: 'pools-walk-label',
          x: x + 0.5,
          y: y + 0.76,
        });
        nameText.textContent = Array.from(def.name || def.short || '').slice(0, 4).join('');
        overlayNodes.push(nameText);
      }
    }
  }

  svg.append(svgElement('path', { class: 'pools-walk-fog-fill', d: hiddenTiles.join('') }));
  svg.append(svgElement('path', { class: 'pools-walk-outline', d: silhouette.join('') }));
  svg.append(svgElement('path', { class: 'pools-walk-fog-edge', d: [...fog].join('') }));
  svg.append(svgElement('path', { class: 'pools-walk-walls-backer', d: [...walls].join('') }));
  svg.append(svgElement('path', { class: 'pools-walk-walls', d: [...walls].join('') }));
  svg.append(svgElement('path', { class: 'pools-walk-doors', d: [...doors].join('') }));
  svg.append(...overlayNodes);
  return svg;
}

function showNumber(value) {
  return Number.isFinite(value) ? value.toLocaleString('zh-CN', { maximumFractionDigits: 2 }) : '—';
}

export class MapPoolsEditor {
  constructor(
    root,
    {
      store = null,
      getPools,
      updatePools,
      saveDraft,
      preview,
      explorePreview,
      publish,
      loadPublished,
      listPatterns,
    },
  ) {
    Object.assign(this, {
      root,
      store,
      getPools,
      updatePools,
      saveDraft,
      preview,
      explorePreview,
      publish,
      loadPublished,
      listPatterns,
    });
    this.poolIndex = 0;
    this.entryIndex = 0;
    this.seeds = new Map();
    this.previews = new Map();
    this.patterns = [];
    this.epoch = 0;
    this.statuses = new Map();
    this.initialSeed = randomInt32Seed();
    this.selectedStripId = null;
    this.localUi = {
      floorShowGrid: true,
      floorShowRegions: true,
      floorShowCells: true,
      floorShowLevels: true,
      floorShowGraph: false,
      floorShowHeatmap: false,
      floorShowContacts: false,
    };

    if (this.store?.on) {
      this.store.on('ui', () => {
        if (!this.exploration) {
          this.renderToolbar();
          this.renderLegend();
          this.canvas?.render();
        }
      });
    }

    this.buildShell();
    this.render();
  }

  getUi() {
    if (this.store?.ui) {
      return {
        ...this.store.ui,
        floorShowContacts: this.localUi.floorShowContacts,
      };
    }
    return this.localUi;
  }

  toggleUiKey(key) {
    if (key === 'floorShowContacts') {
      this.localUi.floorShowContacts = !this.localUi.floorShowContacts;
      this.renderToolbar();
      this.renderInspector();
      this.canvas?.render();
      return;
    }
    if (this.store?.toggleUi) {
      this.store.toggleUi(key);
    } else {
      this.localUi[key] = !this.localUi[key];
      this.renderToolbar();
      this.renderLegend();
      this.canvas?.render();
    }
  }

  getCellTypes() {
    return this.store?.dungeon?.cellTypes || DEFAULT_CELL_TYPES;
  }

  getStripTemplates() {
    return this.store?.dungeon?.stripTemplates || [];
  }

  currentResult() {
    const config = this.config();
    if (!supportsConfig(config)) return null;
    const { pool, entry } = this.selection(config);
    return this.resultFor(pool, entry);
  }

  currentFloorMap() {
    return this.currentResult()?.map || null;
  }

  buildShell() {
    this.leftEl = h('aside', { class: 'panel floor-sidebar pools-sidebar' });
    this.toolbarEl = h('div', { class: 'stage-toolbar' });
    this.canvasEl = h('canvas', { id: 'pools-map-canvas' });
    this.emptyEl = h('div', { class: 'canvas-empty', hidden: true });
    this.hoverCardEl = h('div', { class: 'hover-card', hidden: true });
    this.legendEl = h('div', { class: 'floor-cell-legend', 'aria-label': '格子颜色图例', hidden: true });
    this.canvasHostEl = h(
      'div',
      { class: 'canvas-host' },
      this.canvasEl,
      this.emptyEl,
      this.hoverCardEl,
      this.legendEl,
    );
    this.statusbarEl = h('footer', { class: 'statusbar' });
    this.centerEl = h(
      'section',
      { class: 'stage pools-stage' },
      this.toolbarEl,
      this.canvasHostEl,
      this.statusbarEl,
    );
    this.rightEl = h('aside', { class: 'panel floor-inspector pools-inspector' });
    this.walkOverlayEl = h('div', { class: 'pools-walk-overlay', hidden: true });

    mount(this.root, this.leftEl, this.centerEl, this.rightEl, this.walkOverlayEl);

    this.canvas = new FloorCanvas({
      canvas: this.canvasEl,
      host: this.canvasHostEl,
      hoverCard: this.hoverCardEl,
      getMap: () => this.currentFloorMap(),
      getSelectedStripId: () => this.selectedStripId,
      onSelectStrip: (stripId) => {
        this.selectedStripId = stripId;
        this.renderInspector();
      },
      getUi: () => this.getUi(),
      getCellTypes: () => this.getCellTypes(),
      getContactWalls: () => this.currentResult()?.map?.stats?.sharedWalls?.edges || null,
    });
    this.canvas.onZoomChange = () => this.renderToolbar();
  }

  setTheme(theme) {
    this.canvas?.setTheme(theme);
  }

  resize() {
    this.canvas?.resize();
    if (this.currentFloorMap() && !this.canvas?.lastMapId) {
      this.canvas?.fit();
    }
  }

  fit() {
    this.canvas?.fit();
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
      this.clearExploration();
      this.epoch += 1;
      this.statuses.clear();
      this.patterns = [];
      this.seeds.clear();
      this.previews.clear();
      this.selectedStripId = null;
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
        if (
          !Array.isArray(response?.patterns) ||
          !response.patterns.every(
            (item) =>
              typeof item?.id === 'string' &&
              (item.regionTypeIds == null ||
                (Array.isArray(item.regionTypeIds) && item.regionTypeIds.every((id) => typeof id === 'string'))),
          )
        ) {
          throw Error('模板库目录返回了无效的格式。');
        }
        this.patterns = response.patterns;
        this.setStatus('catalog', null);
      } catch (error) {
        if (epoch === this.epoch) {
          this.setStatus('catalog', `无法读取模板库目录：${errorText(error)}`, 'error', error?.details);
        }
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
    if (this.busy || this.exploration) return;
    const config = copy(this.config());
    const { pool, entry } = this.selection(config);
    this.inUpdate = true;
    this.setStatus('action', null);
    try {
      change(config, pool, entry);
      this.updatePools(config);
      // 保留其他未改条目的预览缓存，清除被修改或删除条目的旧预览
      const liveKeys = new Set(
        config.pools.flatMap((item) =>
          item.entries.map((candidate) => previewKey(item, candidate, this.seedFor(item, candidate))),
        ),
      );
      for (const key of this.previews.keys()) {
        if (!liveKeys.has(key)) this.previews.delete(key);
      }
    } catch (error) {
      this.setStatus('action', `修改草稿失败：${errorText(error)}`, 'error', error?.details);
    } finally {
      this.inUpdate = false;
      if (rebuild) {
        this.selectedStripId = null;
        this.render();
      } else {
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
    if (this.busy || this.exploration) return;
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
    const { id: poolId } = pool;
    const { id: entryId } = entry;
    await this.run('生成预览', async (current) => {
      this.previews.delete(key);
      this.selectedStripId = null;
      this.renderState();
      const response = await this.preview({ config, poolId, entryId, seed });
      if (!current()) return;
      const floor = validateFloorFile(response?.floor);
      if (!response.metrics || typeof response.metrics !== 'object') throw Error('预览结果缺少指标。');
      const map = adaptPoolPreviewToFloorMap({
        floor,
        metrics: response.metrics,
        layout: response.layout || null,
        entry,
        seed,
        cellTypes: this.getCellTypes(),
        stripTemplates: this.getStripTemplates(),
      });
      this.previews.set(key, {
        floor,
        metrics: response.metrics,
        layout: response.layout || null,
        bounds: floorBounds(floor.cells),
        entry,
        seed,
        map,
      });
      this.setStatus('action', `已生成「${entry.name}」的预览（种子 ${seed}）。`, 'success');
      window.requestAnimationFrame(() => this.canvas?.fit());
    });
  }

  explorationIsCurrent(session) {
    if (this.exploration !== session || this.root.hidden) return false;
    const config = this.config();
    if (!supportsConfig(config)) return false;
    const { pool, entry } = this.selection(config);
    return this.resultFor(pool, entry) === session.result && !this.previewProblem(pool, entry, validateMapPools(config));
  }

  startExploration() {
    if (this.busy || this.root.hidden || this.exploration) return;
    const config = this.config();
    if (!supportsConfig(config)) return;
    const { pool, entry } = this.selection(config);
    const result = this.resultFor(pool, entry);
    if (!result || this.previewProblem(pool, entry, validateMapPools(config))) return;
    this.exploration = { result, seed: result.seed, walk: new FloorExploration(result.floor) };
    this.renderExploration();
    this.walkPanel.focus({ preventScroll: true });
  }

  clearExploration() {
    const session = this.exploration;
    this.exploration = null;
    session?.request?.abort();
    this.walkPanel = this.rerollBtn = this.walkSeedEl = this.walkStatusEl = this.walkPositionEl = null;
    if (this.walkOverlayEl) {
      this.walkOverlayEl.hidden = true;
      this.walkOverlayEl.replaceChildren();
    }
    this.root?.classList?.remove('is-exploring');
  }

  leave({ focus = false } = {}) {
    if (!this.exploration) return;
    this.clearExploration();
    this.render();
    if (focus) this.exploreBtn?.focus({ preventScroll: true });
  }

  async rerollExploration(session = this.exploration) {
    if (!session || !this.explorationIsCurrent(session) || session.request || !this.explorePreview) return;
    const config = copy(this.config());
    const { pool, entry } = this.selection(config);
    let seed = randomInt32Seed();
    if (seed === session.seed) seed = (seed + 1) | 0;
    session.request = new AbortController();
    session.error = false;
    this.syncExploration(session);
    try {
      const response = await this.explorePreview(
        { config, poolId: pool.id, entryId: entry.id, seed },
        session.request.signal,
      );
      if (!this.explorationIsCurrent(session)) return;
      // 校验并构造通过后再替换当前种子与迷雾进度
      const walk = new FloorExploration(response?.floor);
      Object.assign(session, { walk, seed });
      this.drawExplorationView(session);
    } catch {
      if (this.explorationIsCurrent(session)) session.error = true;
    } finally {
      session.request = null;
      if (this.explorationIsCurrent(session)) this.syncExploration(session);
    }
  }

  syncExploration(session) {
    this.rerollBtn.disabled = Boolean(session.request || !this.explorePreview);
    this.walkSeedEl.textContent = `测试种子：${session.seed}`;
    // 生成错误可能含全图诊断，不暴露在探索 UI 中
    this.walkStatusEl.textContent = session.request
      ? '正在生成新地图…'
      : session.error
        ? '新地图生成失败，已保留当前探索。'
        : '';
  }

  drawExplorationView(session) {
    const cells = session.walk.view();
    mount(this.walkPanel, drawExploration(cells, this.getCellTypes()));
    this.walkPositionEl.textContent = `当前位置：${contentName(
      cells.find(({ current }) => current),
      this.getCellTypes(),
    )}`;
  }

  renderExploration() {
    if (this.walkPanel && this.walkOverlayEl.contains(this.walkPanel)) return;
    const session = this.exploration;
    this.root.classList.add('is-exploring');
    this.walkOverlayEl.hidden = false;

    this.rerollBtn = button(
      '生成新种子',
      () => {
        this.walkPanel.focus({ preventScroll: true });
        void this.rerollExploration(session);
      },
      { class: 'btn primary small' },
    );
    this.walkSeedEl = h('span', { class: 'seed-label' });
    this.walkStatusEl = h('span', { class: 'status-item pools-walk-status', role: 'status' });
    this.walkPositionEl = h('span', { class: 'status-item pools-walk-pos', role: 'status' });
    this.walkPanel = h('div', {
      class: 'pools-walk-panel',
      tabindex: 0,
      'aria-label': '探索操作区',
      'aria-describedby': 'pools-walk-help',
      onkeydown: (event) => {
        if (event.target !== this.walkPanel || this.exploration !== session || this.root.hidden) return;
        const dir = explorationDirection(event);
        if (!dir) return;
        event.preventDefault();
        if (session.walk.move(dir)) this.drawExplorationView(session);
      },
    });
    this.walkPanel.addEventListener('pointerdown', () => this.walkPanel.focus({ preventScroll: true }));

    const controls = [
      ['N', '↑', '向上'],
      ['W', '←', '向左'],
      ['S', '↓', '向下'],
      ['E', '→', '向右'],
    ];
    const directions = controls.map(([dir, label, name]) =>
      button(
        label,
        () => {
          if (this.exploration !== session || this.root.hidden) return;
          this.walkPanel.focus({ preventScroll: true });
          if (session.walk.move(dir)) this.drawExplorationView(session);
        },
        { class: 'tool-btn', 'aria-label': name, title: name },
      ),
    );

    const walkStage = h(
      'section',
      { class: 'stage pools-walk-stage', 'aria-label': '探索测试' },
      h(
        'div',
        { class: 'stage-toolbar' },
        h('span', { class: 'pill spawn' }, '探索测试'),
        this.walkSeedEl,
        this.walkStatusEl,
        h('span', { class: 'toolbar-spacer' }),
        this.rerollBtn,
        button('退出探索', () => this.leave({ focus: true }), { class: 'btn small' }),
      ),
      h(
        'div',
        { class: 'canvas-host pools-walk-host' },
        this.walkPanel,
        h(
          'div',
          { class: 'floor-cell-legend pools-walk-help-chip', id: 'pools-walk-help' },
          `WASD / 方向键移动 · 视野 ${EXPLORATION_RADIUS} 格（曼哈顿距离）· 走过的格子保持可见`,
        ),
      ),
      h(
        'footer',
        { class: 'statusbar' },
        this.walkPositionEl,
        h('span', { class: 'spacer' }),
        h('div', { class: 'tool-group', role: 'group', 'aria-label': '探索移动' }, ...directions),
      ),
    );

    mount(this.walkOverlayEl, walkStage);
    this.drawExplorationView(session);
    this.syncExploration(session);
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
      this.selectedStripId = null;
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
        if (current()) {
          this.setStatus('action', `关卡地图池已发布${response?.path ? `：${response.path}` : '。'}`, 'success');
        }
      } catch (error) {
        if (current() && Array.isArray(error.details)) {
          const needsPreview = new Set(error.details);
          for (const item of config.pools) {
            for (const candidate of item.entries) {
              if (needsPreview.has(`${item.id} / ${candidate.id}`)) {
                this.previews.delete(previewKey(item, candidate, this.seedFor(item, candidate)));
              }
            }
          }
        }
        throw error;
      }
    });
  }

  readiness(config) {
    let total = 0;
    let approved = 0;
    let empty = 0;
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
    if (!this.leftEl?.contains(this.patternOptions) || !this.leftEl?.contains(this.regionOptions) || !supportsConfig(this.config())) {
      return;
    }
    const { entry } = this.selection();
    mount(
      this.patternOptions,
      this.patterns.map(({ id }) => h('option', { value: id })),
    );
    const selected = this.patterns.find((item) => item.id === entry?.patternLibraryId);
    const ids = selected?.regionTypeIds ?? this.patterns.flatMap((item) => item.regionTypeIds ?? []);
    mount(
      this.regionOptions,
      [...new Set([...REGION_TYPES, ...ids])].map((id) => {
        const preset = DUNGEON_REGION_PRESETS.find((r) => r.id === id);
        return h('option', { value: id }, preset ? `${id} (${preset.name})` : id);
      }),
    );
  }

  /* ---------------- 左侧面板：关卡池 / 候选条目 / 三区域 / 预算 / 曲线 ---------------- */

  renderLeft(config, pool, entry) {
    const prevScroll = this.leftBodyEl?.scrollTop ?? 0;
    this.patternOptions = null;
    this.regionOptions = null;

    const poolSelect = selectControl(config.pools, this.poolIndex, '无关卡池', this.busy, (index) => {
      this.poolIndex = index;
      this.entryIndex = 0;
      this.selectedStripId = null;
      this.render();
    });
    const entrySelect = selectControl(pool?.entries ?? [], this.entryIndex, '无候选条目', this.busy, (index) => {
      this.entryIndex = index;
      this.selectedStripId = null;
      this.render();
    });
    this.poolChoice = poolSelect;
    this.entryChoice = entrySelect;

    // 1. 关卡池与预览生成
    const seedVal = pool && entry ? this.seedFor(pool, entry) : '';
    const seedInput = h('input', {
      class: 'input mono-input',
      type: 'number',
      min: -2147483648,
      max: 2147483647,
      step: 1,
      value: seedVal ?? '',
      disabled: Boolean(this.busy || !pool || !entry),
      placeholder: '32 位整数种子',
      oninput: () => {
        const selected = this.selection();
        if (!selected.entry) return;
        const value = Number.isFinite(seedInput.valueAsNumber) ? seedInput.valueAsNumber : null;
        const oldSeed = this.seedFor(selected.pool, selected.entry);
        this.seeds.set(entryKey(selected.pool, selected.entry), value);
        if (oldSeed !== value) {
          this.previews.delete(previewKey(selected.pool, selected.entry, oldSeed));
          this.selectedStripId = null;
        }
        this.setStatus('action', null);
        this.renderState();
      },
      onkeydown: (e) => {
        if (e.key === 'Enter') void this.previewSelected();
      },
    });
    this.seedInputEl = seedInput;

    this.previewBtn = h(
      'button',
      {
        class: 'btn primary full-width',
        type: 'button',
        style: { marginTop: '8px' },
        onclick: () => void this.previewSelected(),
      },
      icon('generate', 14),
      h('span', null, '生成预览'),
    );

    const poolSection = h(
      'div',
      { class: 'section' },
      h(
        'div',
        { class: 'section-title' },
        h('span', null, '关卡池与预览生成'),
        h(
          'button',
          {
            class: 'link-btn',
            type: 'button',
            disabled: Boolean(this.busy),
            onclick: () =>
              this.modify((draft) => {
                const id = nextId('level', draft.pools);
                draft.pools.push({ id, name: `关卡池 ${draft.pools.length + 1}`, entries: [] });
                this.poolIndex = draft.pools.length - 1;
                this.entryIndex = 0;
              }),
          },
          '+ 新增关卡池',
        ),
      ),
      h(
        'div',
        { class: 'kv-form' },
        h(
          'label',
          { class: 'mini-field' },
          h('span', null, '当前关卡池'),
          h(
            'div',
            { class: 'field-row' },
            poolSelect,
            pool
              ? h(
                  'button',
                  {
                    class: 'tree-action pools-icon-danger',
                    type: 'button',
                    title: '删除当前关卡池',
                    disabled: Boolean(this.busy),
                    onclick: () => {
                      if (window.confirm(`删除关卡池「${pool.name || pool.id}」及其中的 ${pool.entries.length} 个条目？`)) {
                        this.modify((draft) => {
                          draft.pools.splice(this.poolIndex, 1);
                          this.entryIndex = 0;
                        });
                      }
                    },
                  },
                  icon('trash', 13),
                )
              : null,
          ),
        ),
        pool
          ? h(
              'div',
              { class: 'form-row-2' },
              miniField('关卡池 ID', { value: pool.id ?? '', disabled: Boolean(this.busy) }, (v) =>
                this.modify((_c, item) => {
                  item.id = v;
                }, { rebuild: false }),
              { mono: true }).element,
              miniField('关卡池名称', { value: pool.name ?? '', disabled: Boolean(this.busy) }, (v) =>
                this.modify((_c, item) => {
                  item.name = v;
                }, { rebuild: false }),
              ).element,
            )
          : h('p', { class: 'field-hint' }, '先新增关卡池，再为其添加带权重的候选方案。'),
        pool
          ? h(
              'label',
              { class: 'mini-field' },
              h(
                'span',
                { class: 'pools-field-head' },
                h('span', null, '候选方案条目'),
                h(
                  'button',
                  {
                    class: 'link-btn',
                    type: 'button',
                    disabled: Boolean(this.busy),
                    onclick: () =>
                      this.modify((_draft, item) => {
                        item.entries.push(newEntry(item.entries));
                        this.entryIndex = item.entries.length - 1;
                      }),
                  },
                  '+ 新增条目',
                ),
              ),
              h(
                'div',
                { class: 'field-row' },
                entrySelect,
                entry
                  ? h(
                      'button',
                      {
                        class: 'tree-action pools-icon-danger',
                        type: 'button',
                        title: '删除当前候选条目',
                        disabled: Boolean(this.busy),
                        onclick: () => {
                          if (window.confirm(`删除候选条目「${entry.name || entry.id}」？`)) {
                            this.modify((_draft, item) => {
                              item.entries.splice(this.entryIndex, 1);
                            });
                          }
                        },
                      },
                      icon('trash', 13),
                    )
                  : null,
              ),
            )
          : null,
      ),
      pool && entry
        ? h(
            'div',
            { style: { marginTop: '10px' } },
            h(
              'div',
              { class: 'field-row' },
              seedInput,
              h(
                'button',
                {
                  class: 'btn small',
                  type: 'button',
                  disabled: Boolean(this.busy),
                  title: '换一个随机 32 位整数种子并立即预览',
                  onclick: () => {
                    const selected = this.selection();
                    if (!selected.entry) return;
                    const nextSeed = randomInt32Seed();
                    const oldSeed = this.seedFor(selected.pool, selected.entry);
                    this.seeds.set(entryKey(selected.pool, selected.entry), nextSeed);
                    if (oldSeed !== nextSeed) {
                      this.previews.delete(previewKey(selected.pool, selected.entry, oldSeed));
                    }
                    seedInput.value = String(nextSeed);
                    this.setStatus('action', null);
                    this.renderState();
                    if (!this.previewProblem(selected.pool, selected.entry, validateMapPools(this.config()))) {
                      void this.previewSelected();
                    }
                  },
                },
                '🎲 随机',
              ),
            ),
            this.previewBtn,
          )
        : null,
    );

    const sections = [poolSection];

    if (pool && entry) {
      this.patternOptions = h('datalist', { id: 'map-pool-pattern-options' });
      this.regionOptions = h('datalist', { id: 'map-pool-region-options' });

      const fieldAt = (label, path, attrs = {}, opts = {}) => {
        const value = path.reduce((item, key) => item?.[key], entry);
        return miniField(
          label,
          { value: value ?? '', disabled: Boolean(this.busy), ...attrs },
          (next) =>
            this.modify(
              (_config, _pool, item) => {
                const parent = path.slice(0, -1).reduce((obj, key) => obj[key], item);
                parent[path[path.length - 1]] = next;
              },
              { rebuild: false },
            ),
          opts,
        ).element;
      };
      const numeric = (label, path, attrs = {}, opts = {}) =>
        fieldAt(label, path, { type: 'number', min: 0, ...attrs }, { mono: true, ...opts });

      // 2. 候选方案基础配置
      const recipeSection = h(
        'div',
        { class: 'section' },
        h('div', { class: 'section-title' }, h('span', null, '候选方案属性')),
        h(
          'div',
          { class: 'kv-form' },
          h(
            'div',
            { class: 'form-row-2' },
            fieldAt('条目 ID', ['id'], {}, { mono: true }),
            fieldAt('条目名称', ['name']),
          ),
          h(
            'div',
            { class: 'form-row-2' },
            numeric('抽取权重', ['weight'], { step: 'any' }),
            fieldAt('模板库 ID', ['patternLibraryId'], {
              list: 'map-pool-pattern-options',
              placeholder: '如 Sample',
            }, { mono: true }),
          ),
          h(
            'label',
            { class: 'mini-field' },
            h('span', null, '环路留白策略'),
            h(
              'select',
              {
                class: 'select',
                disabled: Boolean(this.busy),
                onchange: (event) =>
                  this.modify(
                    (_config, _pool, item) => {
                      item.ringSpacing = event.target.value;
                    },
                    { rebuild: false },
                  ),
              },
              ...RING_SPACING.map((mode) =>
                h('option', { value: mode, selected: (entry.ringSpacing ?? 'legacy') === mode }, ringSpacingLabels[mode]),
              ),
            ),
          ),
          h('div', { class: 'field-hint' }, '均衡 / 舒展尽量减少环路两侧长边贴墙；现有沿用原生成方式。'),
        ),
        this.patternOptions,
        this.regionOptions,
      );

      // 3. 三区域配置（与旧单层工具风格完全统一）
      const slotPalettes = regionPaletteForEntry(entry);
      const regionsSection = h(
        'div',
        { class: 'section' },
        h('div', { class: 'section-title' }, h('span', null, '三区域顺序（A → B → C）')),
        h('div', { class: 'field-hint', style: { marginBottom: '8px' } }, 'A 为出生区；三槽位可复用相同区域类型 ID。'),
        ...SLOTS.map((slot, index) => {
          const pal = slotPalettes[index];
          return h(
            'div',
            { class: 'floor-region-card' },
            h(
              'div',
              { class: 'floor-region-head' },
              h('span', { class: 'palette-dot', style: { background: pal.color } }),
              h('input', {
                class: 'input mono-input',
                type: 'text',
                list: 'map-pool-region-options',
                value: entry.regions[index]?.regionTypeId ?? '',
                disabled: Boolean(this.busy),
                placeholder: '区域类型 ID',
                oninput: (event) =>
                  this.modify(
                    (_config, _pool, item) => {
                      item.regions[index].regionTypeId = event.target.value;
                    },
                    { rebuild: false },
                  ),
              }),
              index === 0
                ? h('span', { class: 'pill spawn' }, `${slot} · 出生区`)
                : h('span', { class: 'pill' }, `${slot} · 翼区`),
            ),
            h(
              'div',
              { class: 'form-row-3', style: { marginTop: '7px' } },
              numeric('最少条带', ['regions', index, 'stripMin'], { min: 3, max: 20 }),
              numeric('最多条带', ['regions', index, 'stripMax'], { min: 3, max: 20 }),
              numeric('最多非环', ['regions', index, 'maxNoLoop'], { max: 20 }, { title: '非环端点条带上限' }),
            ),
          );
        }),
      );

      // 4. 目标预算区间
      const budgetSection = h(
        'div',
        { class: 'section' },
        h('div', { class: 'section-title' }, h('span', null, '整图资源与难度预算')),
        h('div', { class: 'field-hint', style: { marginBottom: '6px' } }, '上下限可留空表示不限；未达预算仍可预览与发布。'),
        h(
          'div',
          { class: 'form-row-2' },
          numeric('总资源下限', ['budget', 'resourceMin'], { step: 'any', placeholder: '不限' }),
          numeric('总资源上限', ['budget', 'resourceMax'], { step: 'any', placeholder: '不限' }),
        ),
        h(
          'div',
          { class: 'form-row-2', style: { marginTop: '6px' } },
          numeric('总难度下限', ['budget', 'difficultyMin'], { step: 'any', placeholder: '不限' }),
          numeric('总难度上限', ['budget', 'difficultyMax'], { step: 'any', placeholder: '不限' }),
        ),
      );

      // 5. 敌人等级进程
      const progSection = h(
        'div',
        { class: 'section' },
        h('div', { class: 'section-title' }, h('span', null, '敌人等级进程')),
        h(
          'div',
          { class: 'form-row-3' },
          numeric('起始等级', ['progression', 'baseLevel'], { min: 1 }),
          numeric('每跳增幅', ['progression', 'levelPerHop'], { step: 'any' }),
          numeric('等级上限', ['progression', 'maxLevel'], { min: 1 }),
        ),
      );

      sections.push(recipeSection, regionsSection, budgetSection, progSection);
    }

    this.leftBodyEl = h('div', { class: 'panel-body' }, ...sections);
    mount(this.leftEl, this.leftBodyEl);
    this.leftBodyEl.scrollTop = prevScroll;
  }

  /* ---------------- 中央画布工具栏、图例、空态与底部状态栏 ---------------- */

  renderToolbar() {
    if (!this.toolbarEl) return;
    const ui = this.getUi();
    const config = this.config();
    const validConfig = supportsConfig(config);
    const { pool, entry } = validConfig ? this.selection(config) : { pool: null, entry: null };
    const progress = validConfig ? this.readiness(config) : null;
    const invalid = validConfig ? this.previewProblem(pool, entry, progress.issues) : '不支持的地图池格式';
    const result = validConfig ? this.resultFor(pool, entry) : null;
    const map = result?.map || null;
    const wallCount = map?.stats?.sharedWalls?.edges?.length ?? 0;

    const toggles = [
      { key: 'floorShowGrid', label: '网格', icon: 'grid' },
      { key: 'floorShowRegions', label: '区域框', icon: 'eye' },
      { key: 'floorShowCells', label: '格子', icon: 'contents' },
      { key: 'floorShowLevels', label: '等级', icon: 'labels' },
      { key: 'floorShowGraph', label: '节点图', icon: 'layers' },
      { key: 'floorShowHeatmap', label: '距离热力', icon: 'spawn' },
      { key: 'floorShowContacts', label: map ? `贴边(${wallCount})` : '贴边', icon: 'alert', disabled: Boolean(map && wallCount === 0) },
    ];

    const zoomPct = `${Math.round(((this.canvas?.view?.scale || 34) / 34) * 100)}%`;

    this.exploreBtn = h(
      'button',
      {
        class: 'btn pools-explore-btn',
        type: 'button',
        title: result ? '进入当前地图的探索测试' : '生成有效预览后可开始探索',
        disabled: Boolean(this.busy || invalid || !result),
        onclick: () => this.startExploration(),
      },
      icon('spawn', 17),
      h('span', null, '探索测试'),
    );

    mount(
      this.toolbarEl,
      this.exploreBtn,
      h(
        'div',
        { class: 'tool-group' },
        h('button', { class: 'tool-btn', type: 'button', title: '缩小', onclick: () => this.canvas?.zoomBy(1 / 1.2) }, icon('minus', 13)),
        h('button', { class: 'tool-btn', type: 'button', title: '放大', onclick: () => this.canvas?.zoomBy(1.2) }, icon('plus', 13)),
        h('button', { class: 'tool-btn', type: 'button', title: '适应画布', onclick: () => this.canvas?.fit() }, icon('fit', 13)),
      ),
      h('span', { class: 'seed-label', style: { minWidth: '48px', justifyContent: 'center' } }, zoomPct),
      h('span', { class: 'top-divider' }),
      h(
        'div',
        { class: 'tool-group' },
        ...toggles.map((t) =>
          h(
            'button',
            {
              class: `tool-btn${ui[t.key] ? ' active' : ''}`,
              type: 'button',
              title: t.label,
              disabled: Boolean(t.disabled),
              onclick: () => this.toggleUiKey(t.key),
            },
            icon(t.icon, 13),
            h('span', null, t.label),
          ),
        ),
      ),
      h('span', { class: 'toolbar-spacer' }),
      map
        ? h(
            'div',
            { class: 'tool-group' },
            h(
              'button',
              {
                class: 'tool-btn',
                type: 'button',
                title: '导出 2x PNG 预览图',
                onclick: async () => {
                  const blob = await this.canvas?.toPngBlob({ scale: 2 });
                  if (!blob) return;
                  downloadBlob(`${slugify(entry?.id || 'pool')}-${result.seed}-preview.png`, blob);
                  toast('地图池预览 PNG 已导出', { type: 'success' });
                },
              },
              icon('download', 13),
              h('span', null, 'PNG'),
            ),
          )
        : null,
      h('span', { class: 'seed-label' }, 'SEED ', h('strong', null, result ? String(result.floor.seed ?? result.seed) : '—')),
    );
  }

  renderLegend() {
    if (!this.legendEl) return;
    const ui = this.getUi();
    const hasMap = Boolean(this.currentFloorMap());
    const show = hasMap && !ui.floorShowHeatmap && ui.floorShowCells;
    this.legendEl.hidden = !show;
    if (!show) return;
    const cellTypes = this.getCellTypes();
    mount(
      this.legendEl,
      ...cellTypes.map((ct) =>
        h(
          'span',
          { class: 'floor-legend-chip' },
          h('span', {
            class: 'floor-legend-swatch',
            style: { background: ct.id === 'empty' ? 'rgba(90, 106, 122, 0.55)' : ct.color },
          }),
          h('span', null, ct.name),
        ),
      ),
    );
  }

  renderEmptyState(pool, entry, invalid, result) {
    if (!this.emptyEl) return;
    const hasMap = Boolean(result?.map);
    this.emptyEl.hidden = hasMap;
    if (hasMap) return;

    mount(
      this.emptyEl,
      h(
        'div',
        { class: 'empty-card' },
        h('div', { class: 'eyebrow' }, 'MAP POOL / 关卡地图池预览'),
        h('h2', null, this.busy ? `${this.busy}中…` : entry ? `预览「${entry.name || entry.id}」` : '选择或新增关卡池方案'),
        h(
          'p',
          null,
          invalid ||
            '通过 Unity 运行时同款生成器预览真实物理地板、三区域三角拓扑、格子内容、敌人等级与预算达标情况；支持缩放拖拽、条带检视、贴边高亮与迷雾探索测试。',
        ),
        entry
          ? h(
              'div',
              { class: 'action-row', style: { marginTop: '14px' } },
              h(
                'button',
                {
                  class: 'btn primary',
                  type: 'button',
                  disabled: Boolean(this.busy || !this.preview || invalid),
                  onclick: () => void this.previewSelected(),
                },
                icon('generate', 14),
                h('span', null, '立即生成预览'),
              ),
              this.loadPublished
                ? h(
                    'button',
                    {
                      class: 'btn',
                      type: 'button',
                      disabled: Boolean(this.busy),
                      onclick: () => void this.applyPublished(),
                    },
                    '读取已发布配置',
                  )
                : null,
            )
          : null,
      ),
    );
  }

  renderStatusbar(progress, result) {
    if (!this.statusbarEl) return;
    if (!result?.map) {
      mount(
        this.statusbarEl,
        h(
          'span',
          { class: 'status-item' },
          h('span', { class: `status-dot ${progress?.ready ? 'ok' : progress?.issues?.length ? 'err' : 'warn'}` }),
          progress
            ? `已预览 ${progress.approved} / ${progress.total} 个条目${progress.empty ? ` · ${progress.empty} 个空池` : ''}${progress.ready ? ' · 可发布' : ''}`
            : '尚未生成预览',
        ),
        h('span', { class: 'spacer' }),
        h('span', { class: 'status-item' }, `已发布模板库：${this.patterns.length} 份`),
      );
      return;
    }

    const map = result.map;
    const st = map.stats;
    const budgetBad = st.resourceInRange === false || st.difficultyInRange === false;
    mount(
      this.statusbarEl,
      h(
        'span',
        { class: 'status-item' },
        h('span', { class: `status-dot ${budgetBad ? 'warn' : 'ok'}` }),
        budgetBad ? '预算超出目标区间' : '预览有效',
      ),
      h('span', { class: 'status-item' }, `物理格 ${st.tileCount} · 条带 ${st.strips}（区 ${st.regionStripCount} + 桥 ${st.bridges}）`),
      h('span', { class: 'status-item' }, `门 ${st.doors} · 交叉 ${st.crossings} · 贴边 ${st.sharedWalls.edges.length}（最长 ${st.sharedWalls.longest}）`),
      h('span', { class: 'status-item' }, `总资源 ${showNumber(st.totalResource)} · 总难度 ${showNumber(st.totalDifficulty)}`),
      h('span', { class: 'spacer' }),
      h(
        'span',
        { class: 'status-item' },
        `最大跳数 d=${st.maxDistance} · 条带 Lv.${st.minLevel}–${st.maxLevel}${st.maxEnemyLevel ? ` · 最高敌人 E${st.maxEnemyLevel}` : ''}`,
      ),
    );
  }

  /* ---------------- 右侧检视面板：发布工作流 + 拓扑预算仪表盘 + 选中条带详情 ---------------- */

  renderInspector() {
    if (!this.rightEl) return;
    const prevScroll = this.rightBodyEl?.scrollTop ?? 0;
    const config = this.config();
    if (!supportsConfig(config)) {
      mount(
        this.rightEl,
        h(
          'div',
          { class: 'panel-body' },
          h('div', { class: 'issue error', role: 'alert' }, '当前草稿不是支持的关卡地图池格式。'),
        ),
      );
      return;
    }

    const { pool, entry } = this.selection(config);
    const progress = this.readiness(config);
    const invalid = this.previewProblem(pool, entry, progress.issues);
    const result = this.resultFor(pool, entry);
    const map = result?.map || null;

    this.publishBtn = h(
      'button',
      {
        class: 'btn primary full-width',
        type: 'button',
        disabled: Boolean(this.busy || !this.publish || !result || !progress.ready),
        onclick: () => void this.publishCurrent(),
      },
      icon('export', 14),
      h('span', null, '发布到游戏配置'),
    );

    const statusNodes = [...this.statuses.values()].map(({ text, level, details }) =>
      h(
        'div',
        {
          class: `issue ${level === 'error' ? 'error' : level === 'success' ? 'ok' : 'info'}`,
          role: level === 'error' ? 'alert' : 'status',
        },
        h('div', null, text),
        Array.isArray(details) && details.length
          ? h('ul', { class: 'pools-issue-details' }, ...details.map((d) => h('li', null, String(d))))
          : null,
      ),
    );

    if (progress.total > 0 && progress.issues.length > 0) {
      statusNodes.push(
        h(
          'details',
          { class: 'issue error' },
          h('summary', null, `草稿校验问题 × ${progress.issues.length} · ${progress.issues[0]}`),
          progress.issues.length > 1
            ? h('ul', { class: 'pools-issue-details' }, ...progress.issues.slice(1).map((issue) => h('li', null, issue)))
            : null,
        ),
      );
    }

    // 1. 发布与工作流操作
    const workflowSection = h(
      'div',
      { class: 'section' },
      h(
        'div',
        { class: 'section-title' },
        h('span', null, '发布与工作流'),
        h(
          'span',
          { class: `pill${progress.ready ? ' spawn' : ''}` },
          progress.ready ? `✓ ${progress.approved}/${progress.total} 可发布` : `${progress.approved}/${progress.total} 已预览`,
        ),
      ),
      this.publishBtn,
      this.saveDraft || this.loadPublished
        ? h(
            'div',
            { class: 'form-row-2', style: { marginTop: '8px' } },
            this.saveDraft
              ? button('保存草稿', () => void this.save(), { class: 'btn small full-width', disabled: Boolean(this.busy) })
              : null,
            this.loadPublished
              ? button('读取已发布', () => void this.applyPublished(), { class: 'btn small full-width', disabled: Boolean(this.busy) })
              : null,
          )
        : null,
      h(
        'div',
        { class: 'field-hint', style: { marginTop: '8px' }, role: 'status' },
        this.busy
          ? `${this.busy}中…`
          : invalid || (result ? '此预览仅验证当前种子，其他种子的结果可能不同。' : '当前条目尚未生成此配置与种子的预览。'),
      ),
      statusNodes.length ? h('div', { class: 'issue-list', style: { marginTop: '8px' } }, ...statusNodes) : null,
    );

    const sections = [workflowSection];

    // 2. 拓扑与数值达标总览（与旧单层检视器一致）
    if (!map) {
      sections.push(
        h(
          'div',
          { class: 'section' },
          h('div', { class: 'section-title' }, h('span', null, '单层地图检视器')),
          h(
            'p',
            { class: 'field-hint' },
            '点击左侧「生成预览」后，这里将展示三区域拓扑达标情况、总资源/难度进度条、贴边诊断统计，以及画布中选中条带的逐格明细。',
          ),
        ),
      );
    } else {
      const st = map.stats;
      const target = map.budget.target || {};
      const walls = st.sharedWalls;
      const ui = this.getUi();

      const badge = (ok, text) =>
        h('span', { class: `health-badge ${ok === true ? 'ok' : ok === false ? 'err' : 'neutral'}` }, `${ok === true ? '✓' : ok === false ? '✕' : '•'} ${text}`);

      const budgetBar = (label, value, min, max, inRange, colorVar) => {
        const hasMin = Number.isFinite(min);
        const hasMax = Number.isFinite(max);
        const refMax = Math.max(1, hasMax ? max : 0, hasMin ? min * 1.4 : 0, Number.isFinite(value) ? value : 1);
        const span = Math.max(1, refMax * 1.25);
        const valPct = clamp(((Number(value) || 0) / span) * 100, 0, 100);
        const minPct = hasMin ? clamp((min / span) * 100, 0, 100) : 0;
        const maxPct = hasMax ? clamp((max / span) * 100, 0, 100) : 100;
        const boundsText = !hasMin
          ? !hasMax
            ? '不限'
            : `≤ ${showNumber(max)}`
          : !hasMax
            ? `≥ ${showNumber(min)}`
            : `[${showNumber(min)}, ${showNumber(max)}]`;

        return h(
          'div',
          { class: 'budget-meter' },
          h(
            'div',
            { class: 'budget-meter-head' },
            h('span', null, label),
            h(
              'span',
              { class: `mono-val ${inRange === false ? 'warn' : 'ok'}` },
              `${showNumber(value)} / ${boundsText}`,
            ),
          ),
          h(
            'div',
            { class: 'budget-track' },
            hasMin || hasMax
              ? h('div', {
                  class: 'budget-band',
                  style: { left: `${minPct}%`, width: `${Math.max(2, maxPct - minPct)}%` },
                })
              : null,
            h('div', {
              class: 'budget-fill',
              style: { width: `${valPct}%`, background: inRange === false ? 'var(--amber)' : colorVar },
            }),
          ),
        );
      };

      const overviewSection = h(
        'div',
        { class: 'section' },
        h('div', { class: 'section-title' }, h('span', null, '拓扑与数值达标总览')),
        h(
          'div',
          { class: 'health-badges' },
          badge(st.triangleValid, '三区三角唯一独立桥'),
          badge(st.allRegionsHaveCycle, '每区至少 1 环'),
          badge(st.allReachable, `可达 ${st.reachable}/${st.strips}`),
          badge(
            st.resourceInRange === false || st.difficultyInRange === false
              ? false
              : st.resourceInRange === true || st.difficultyInRange === true
                ? true
                : null,
            st.resourceInRange === false || st.difficultyInRange === false ? '预算未达目标' : '预算区间达标',
          ),
        ),
        budgetBar('总资源量', st.totalResource, target.resourceMin, target.resourceMax, st.resourceInRange, 'var(--green)'),
        budgetBar('总难度量', st.totalDifficulty, target.difficultyMin, target.difficultyMax, st.difficultyInRange, 'var(--red)'),
        h(
          'div',
          { class: 'stat-grid', style: { marginTop: '8px' } },
          h('div', { class: 'stat' }, h('div', { class: 'stat-value' }, showNumber(st.tileCount)), h('div', { class: 'stat-label' }, `物理格（边 ${st.edgeCount}）`)),
          h('div', { class: 'stat' }, h('div', { class: 'stat-value' }, `${st.bounds.width} × ${st.bounds.height}`), h('div', { class: 'stat-label' }, `(${st.bounds.minX},${st.bounds.minY})→(${st.bounds.maxX},${st.bounds.maxY})`)),
        ),
        h(
          'label',
          { class: 'checkbox-row pools-contact-row', style: { marginTop: '10px' } },
          h('input', {
            type: 'checkbox',
            checked: Boolean(ui.floorShowContacts),
            disabled: !walls.edges.length,
            onchange: () => this.toggleUiKey('floorShowContacts'),
          }),
          h('span', null, `高亮贴边 · ${walls.edges.length} 面条带间未开门墙 · 最长连续 ${walls.longest} 格`),
        ),
        h(
          'div',
          { class: 'region-stats-list' },
          ...st.regions.map((r) =>
            h(
              'div',
              { class: 'region-stat-row' },
              h('span', { class: 'palette-dot', style: { background: r.color } }),
              h('span', { class: 'region-stat-name' }, r.name),
              h(
                'span',
                { class: 'region-stat-meta' },
                `${r.strips}条 · ${r.cycles}环 · 非环${r.noLoopCount}/${r.maxNoLoop ?? '∞'} · 资${showNumber(r.resource)} · 难${showNumber(r.difficulty)}`,
              ),
            ),
          ),
          h(
            'div',
            { class: 'region-stat-row' },
            h('span', { class: 'palette-dot', style: { background: '#e2b75c' } }),
            h('span', { class: 'region-stat-name' }, '跨区独立桥'),
            h('span', { class: 'region-stat-meta' }, `${st.bridges} 条 · 门 ${st.doors} · 交叉 ${st.crossings}`),
          ),
        ),
      );

      sections.push(overviewSection);

      // 3. 选中条带检视（与旧单层检视器一致）
      const selectedStrip = map.strips.find((s) => s.id === this.selectedStripId) || null;
      if (!selectedStrip) {
        sections.push(
          h(
            'div',
            { class: 'section' },
            h('div', { class: 'section-title' }, h('span', null, '选中条带详情')),
            h(
              'p',
              { class: 'field-hint' },
              '在画布中点击任意条带，可查看其所属区域、距出生跳数、条带等级与逐格敌人等级明细。',
            ),
          ),
        );
      } else {
        const tMap = cellTypeMap(this.getCellTypes());
        const region = map.regions.find((r) => r.id === selectedStrip.regionId);
        const roleTitle =
          selectedStrip.role === 'bridge'
            ? `独立跨区桥 (${(selectedStrip.bridgeBetween || []).map((id) => map.regions.find((r) => r.id === id)?.shortName || id).join(' ↔ ') || '跨区'})`
            : selectedStrip.role === 'spawn'
              ? `${region?.name || ''} · 出生条带`
              : region?.name || selectedStrip.regionId || '区域条带';

        sections.push(
          h(
            'div',
            { class: 'section' },
            h(
              'div',
              { class: 'section-title' },
              h(
                'span',
                null,
                selectedStrip.templateName && selectedStrip.templateName !== selectedStrip.id
                  ? `条带 ${selectedStrip.id} · ${selectedStrip.templateName}`
                  : `条带 ${selectedStrip.id}`,
              ),
              h(
                'button',
                {
                  class: 'link-btn',
                  type: 'button',
                  onclick: () => {
                    this.selectedStripId = null;
                    this.renderInspector();
                    this.canvas?.render();
                  },
                },
                '取消选择',
              ),
            ),
            h('div', { class: 'field-hint', style: { marginBottom: '8px' } }, roleTitle),
            h(
              'div',
              { class: 'stat-grid' },
              h('div', { class: 'stat' }, h('div', { class: 'stat-value' }, `d=${selectedStrip.distanceFromSpawn}`), h('div', { class: 'stat-label' }, '距出生跳数')),
              h('div', { class: 'stat' }, h('div', { class: 'stat-value' }, `Lv.${selectedStrip.level}`), h('div', { class: 'stat-label' }, '条带等级')),
              h('div', { class: 'stat' }, h('div', { class: 'stat-value', style: { color: 'var(--green)' } }, showNumber(selectedStrip.resource)), h('div', { class: 'stat-label' }, '条带资源')),
              h('div', { class: 'stat' }, h('div', { class: 'stat-value', style: { color: 'var(--red)' } }, showNumber(selectedStrip.difficulty)), h('div', { class: 'stat-label' }, '条带难度')),
            ),
            h('div', { class: 'weight-grid-head', style: { marginTop: '10px' } }, `格子明细 (${selectedStrip.cells.length} 格)`),
            h(
              'div',
              { class: 'floor-cell-list' },
              ...selectedStrip.cells.map((cell) => {
                const def = tMap.get(cell.type) || tMap.get('empty') || { icon: '·', name: cell.type, color: '#5b6c7d' };
                return h(
                  'div',
                  { class: 'floor-cell-row' },
                  h('span', { class: 'palette-dot', style: { background: def.color } }),
                  h('span', { class: 'floor-cell-name' }, `${def.icon} ${def.name}`),
                  h('span', { class: 'floor-cell-coord' }, `(${cell.x},${cell.y})`),
                  cell.enemyLevel != null
                    ? h('span', { class: 'enemy-pill' }, `E${cell.enemyLevel}`)
                    : h('span', { class: 'field-hint', style: { margin: 0 } }, '—'),
                );
              }),
            ),
          ),
        );
      }
    }

    this.rightBodyEl = h('div', { class: 'panel-body' }, ...sections);
    mount(this.rightEl, this.rightBodyEl);
    this.rightBodyEl.scrollTop = prevScroll;
  }

  renderState() {
    if (this.exploration) return;
    const config = this.config();
    if (!supportsConfig(config)) return;
    const { pool, entry } = this.selection(config);
    const progress = this.readiness(config);
    const invalid = this.previewProblem(pool, entry, progress.issues);
    const result = this.resultFor(pool, entry);

    if (this.previewBtn) this.previewBtn.disabled = Boolean(this.busy || !this.preview || invalid);
    this.renderToolbar();
    this.renderLegend();
    this.renderEmptyState(pool, entry, invalid, result);
    this.renderStatusbar(progress, result);
    this.renderInspector();
    this.canvas?.render();
  }

  renderPreview() {
    this.renderState();
  }

  render() {
    // updatePools 可能同步派发 store change；避免按键时销毁当前聚焦输入框
    if (this.inUpdate) return;
    if (this.exploration) {
      if (this.explorationIsCurrent(this.exploration)) {
        this.renderExploration();
        return;
      }
      this.clearExploration();
    }
    const config = this.config();
    if (!supportsConfig(config)) {
      this.renderInspector();
      return;
    }
    const { pool, entry } = this.selection(config);
    this.renderLeft(config, pool, entry);
    this.syncCatalog();
    this.renderState();
  }
}
