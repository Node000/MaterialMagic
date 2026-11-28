/**
 * 模式三：完整地图生成视图（四等级地图池 + 左侧分栏列表 + 单条配置编辑 + 右侧四框预览与一键生成）。
 *
 * 与「地图生成池」标签页并存；本页使用网页端单层生成器直接生成与预览，并导出包含全部四等级配置的单份 JSON。
 */

import { DUNGEON_REGION_PRESETS, clamp } from '../core/constants.js';
import { estimateFloorBudget } from '../core/floor-generator.js';
import { RING_SPACING } from '../core/map-pools.js';
import { randomSeed } from '../core/rng.js';
import {
  WORLD_TIER_COUNT,
  WORLD_TIER_NAMES,
  createWorldMapEntry,
  formatSpawnRegionLabel,
  generateWorldTierMap,
  normalizeWorldPools,
  parseWorldPools,
  serializeWorldPools,
  validateWorldPools,
  worldEntryToFloorProfile,
} from '../core/world-pools.js';
import {
  downloadText,
  h,
  icon,
  mount,
  pickFile,
  readFileText,
  slugify,
} from './dom.js';
import { FloorCanvas } from './floor-canvas.js';
import { confirmDialog, toast } from './modals.js';

const RING_SPACING_LABELS = { legacy: '现有', balanced: '均衡', spacious: '舒展' };

export class WorldView {
  constructor({ root, store }) {
    this.root = root;
    this.store = store;

    this.collapsedTiers = new Set();
    this.selectedTierIndex = 0;
    this.selectedEntryId = null;
    this.editorOpen = true;
    this.zoomTierIndex = null;

    // 右侧 4 个框的独立状态：选中的下拉配置 ID（"" 表示全部随机）与已生成结果
    this.slotEntryFilter = ['', '', '', ''];
    this.slotResults = [null, null, null, null];
    this.slotCanvases = [null, null, null, null];
    this.zoomCanvas = null;

    this.buildShell();

    store.on('change', () => {
      this.syncSelection();
      this.renderAll();
    });
    store.on('ui', () => {
      this.renderToolbar();
      this.renderAllCanvases();
    });

    this.syncSelection();
    this.renderAll();
  }

  getPools() {
    return normalizeWorldPools(this.store.worldPools);
  }

  syncSelection() {
    const wp = this.getPools();
    // 校验每框下拉选中项是否仍存在
    for (let i = 0; i < WORLD_TIER_COUNT; i += 1) {
      const wanted = this.slotEntryFilter[i];
      if (wanted && !wp.pools[i]?.entries?.some((e) => e.id === wanted)) {
        this.slotEntryFilter[i] = '';
      }
    }
    // 校验当前选中的编辑条目
    const currentPool = wp.pools[this.selectedTierIndex];
    if (this.selectedEntryId && currentPool?.entries?.some((e) => e.id === this.selectedEntryId)) {
      return;
    }
    if (currentPool?.entries?.length) {
      this.selectedEntryId = currentPool.entries[0].id;
      return;
    }
    for (let i = 0; i < WORLD_TIER_COUNT; i += 1) {
      if (wp.pools[i]?.entries?.length) {
        this.selectedTierIndex = i;
        this.selectedEntryId = wp.pools[i].entries[0].id;
        return;
      }
    }
    this.selectedEntryId = null;
  }

  selectedContext(wp = this.getPools()) {
    const pool = wp.pools[this.selectedTierIndex] || wp.pools[0];
    const entry = pool?.entries?.find((e) => e.id === this.selectedEntryId) || null;
    return { pool, entry, tierIndex: this.selectedTierIndex };
  }

  buildShell() {
    this.listEl = h('aside', { class: 'panel world-tier-sidebar', 'aria-label': '四等级地图池列表' });
    this.editorEl = h('aside', { class: 'panel world-entry-editor', 'aria-label': '地图项配置编辑' });
    this.toolbarEl = h('div', { class: 'stage-toolbar world-toolbar' });
    this.gridEl = h('div', { class: 'world-preview-grid' });
    this.statusbarEl = h('footer', { class: 'statusbar' });
    this.stageEl = h(
      'section',
      { class: 'stage world-stage' },
      this.toolbarEl,
      this.gridEl,
      this.statusbarEl,
    );
    this.zoomOverlayEl = h('div', { class: 'world-zoom-overlay', hidden: true });

    // 构建 4 个固定预览框 DOM 与对应 FloorCanvas
    this.slotEls = [];
    for (let i = 0; i < WORLD_TIER_COUNT; i += 1) {
      const headEl = h('div', { class: 'world-box-head' });
      const canvasEl = h('canvas', { id: `world-tier-canvas-${i + 1}` });
      const emptyEl = h('div', { class: 'world-box-empty' });
      const hoverEl = h('div', { class: 'hover-card', hidden: true });
      const canvasHostEl = h('div', { class: 'world-box-canvas canvas-host' }, canvasEl, emptyEl, hoverEl);
      const footEl = h('div', { class: 'world-box-foot' });
      const boxEl = h('div', { class: 'world-box', 'data-tier': String(i + 1) }, headEl, canvasHostEl, footEl);
      this.gridEl.append(boxEl);

      const canvas = new FloorCanvas({
        canvas: canvasEl,
        host: canvasHostEl,
        hoverCard: hoverEl,
        getMap: () => this.slotResults[i]?.map || null,
        getSelectedStripId: () => null,
        onSelectStrip: () => {},
        getUi: () => this.store.ui,
        getCellTypes: () => this.store.dungeon.cellTypes,
      });
      this.slotCanvases[i] = canvas;
      this.slotEls.push({ boxEl, headEl, canvasHostEl, emptyEl, footEl });
    }

    mount(this.root, this.listEl, this.editorEl, this.stageEl, this.zoomOverlayEl);
    this.syncEditorVisibility();
  }

  syncEditorVisibility() {
    this.editorEl.hidden = !this.editorOpen;
    this.root.classList.toggle('world-editor-collapsed', !this.editorOpen);
  }

  setTheme(theme) {
    for (const c of this.slotCanvases) c?.setTheme(theme);
    this.zoomCanvas?.setTheme(theme);
  }

  resize() {
    for (let i = 0; i < WORLD_TIER_COUNT; i += 1) {
      const c = this.slotCanvases[i];
      if (!c) continue;
      c.resize();
      if (this.slotResults[i]?.map && !c.lastMapId) c.fit();
    }
    this.zoomCanvas?.resize();
  }

  fitAll() {
    for (let i = 0; i < WORLD_TIER_COUNT; i += 1) {
      if (this.slotResults[i]?.map) this.slotCanvases[i]?.fit();
    }
    if (this.zoomTierIndex !== null) this.zoomCanvas?.fit();
  }

  renderAllCanvases() {
    for (const c of this.slotCanvases) c?.render();
    this.zoomCanvas?.render();
  }

  /* ---------------- 生成逻辑（单张 & 一键四张） ---------------- */

  generateSlot(tierIndex, { silent = false } = {}) {
    const wp = this.getPools();
    const entryId = this.slotEntryFilter[tierIndex] || '';
    try {
      const result = generateWorldTierMap({
        worldPools: wp,
        tierIndex,
        entryId,
        seed: randomSeed(),
        stripTemplates: this.store.dungeon.stripTemplates,
        cellTypes: this.store.dungeon.cellTypes,
      });
      this.slotResults[tierIndex] = result;
      this.renderSlot(tierIndex);
      this.renderStatusbar();
      window.requestAnimationFrame(() => {
        this.slotCanvases[tierIndex]?.resize();
        this.slotCanvases[tierIndex]?.fit();
      });
      if (!silent) {
        const spawnRegion = result.map.regions[result.map.spawnRegionSlot ?? 0];
        toast(
          `已生成 ${result.poolName}：${result.entryName}${result.pickedRandomly ? '（随机抽取）' : ''}`,
          {
            type: result.map.errors.length === 0 ? 'success' : 'warning',
            detail: `出生 ${'ABC'[result.map.spawnRegionSlot ?? 0]}·${spawnRegion?.shortName || ''} · ${result.map.strips.length} 条带 · 最小环 ${result.map.stats.minCycleLength ?? '—'}`,
          },
        );
      }
      return { ok: true, result };
    } catch (error) {
      console.error(error);
      if (!silent) toast(`${WORLD_TIER_NAMES[tierIndex]} 生成失败：${error.message}`, { type: 'error' });
      return { ok: false, error };
    }
  }

  generateAllFour() {
    let okCount = 0;
    const errors = [];
    for (let i = 0; i < WORLD_TIER_COUNT; i += 1) {
      const res = this.generateSlot(i, { silent: true });
      if (res.ok) okCount += 1;
      else errors.push(`${WORLD_TIER_NAMES[i]}：${res.error.message}`);
    }
    this.renderStatusbar();
    if (errors.length === 0) {
      toast(`已一键生成全部 4 个等级地图`, { type: 'success' });
    } else {
      toast(`完成 ${okCount}/4 张生成，有 ${errors.length} 项失败`, {
        type: 'warning',
        detail: errors[0],
      });
    }
  }

  /* ---------------- JSON 导出 / 导入 ---------------- */

  exportJson() {
    const wp = this.getPools();
    const issues = validateWorldPools(wp);
    const filename = `${slugify(this.store.project.meta.name || 'materialmagic')}-world-map-pools.json`;
    downloadText(filename, serializeWorldPools(wp));
    if (issues.length) {
      toast('已导出完整地图配置 JSON（含校验提醒）', { type: 'warning', detail: issues[0] });
    } else {
      toast('已导出完整地图配置 JSON', { type: 'success', detail: filename });
    }
  }

  async importJson() {
    const file = await pickFile({ accept: '.json,application/json' });
    if (!file) return;
    let text;
    try {
      text = await readFileText(file);
    } catch (error) {
      toast(`读取文件失败：${error.message}`, { type: 'error' });
      return;
    }
    const parsed = parseWorldPools(text);
    if (!parsed.ok) {
      toast(`导入失败：${parsed.errors[0]}`, { type: 'error' });
      return;
    }
    const total = parsed.worldPools.pools.reduce((sum, p) => sum + p.entries.length, 0);
    const ok = await confirmDialog({
      title: '导入完整地图配置 JSON',
      message: `将导入 4 个等级共 ${total} 条地图配置，替换当前「完整地图」配置（可撤销）。继续？`,
      confirmText: '导入替换',
    });
    if (!ok) return;
    this.store.updateWorldPools((draft) => {
      draft.pools = parsed.worldPools.pools;
    }, '导入完整地图配置 JSON');
    this.slotEntryFilter = ['', '', '', ''];
    this.slotResults = [null, null, null, null];
    this.syncSelection();
    this.renderAll();
    toast(`已导入 ${total} 条四等级地图配置`, {
      type: parsed.warnings?.length ? 'warning' : 'success',
      detail: parsed.warnings?.[0] || '',
    });
  }

  /* ---------------- 放大查看 ---------------- */

  openZoom(tierIndex) {
    const result = this.slotResults[tierIndex];
    if (!result?.map) return;
    this.zoomTierIndex = tierIndex;
    this.zoomOverlayEl.hidden = false;

    const canvasEl = h('canvas', { id: 'world-zoom-canvas' });
    const hoverEl = h('div', { class: 'hover-card', hidden: true });
    const hostEl = h('div', { class: 'canvas-host world-zoom-host' }, canvasEl, hoverEl);
    const spawnSlot = result.map.spawnRegionSlot ?? 0;
    const spawnRegion = result.map.regions[spawnSlot];

    mount(
      this.zoomOverlayEl,
      h(
        'div',
        { class: 'world-zoom-modal' },
        h(
          'div',
          { class: 'stage-toolbar' },
          h('span', { class: 'pill spawn' }, result.poolName),
          h('strong', { style: { fontSize: '13.5px' } }, `${result.entryName}${result.pickedRandomly ? '（随机）' : ''}`),
          h('span', { class: 'seed-label' }, '出生 ', h('strong', null, `${'ABC'[spawnSlot]}·${spawnRegion?.shortName || ''}`)),
          h('span', { class: 'seed-label' }, 'SEED ', h('strong', null, result.seed)),
          h('span', { class: 'toolbar-spacer' }),
          h('button', { class: 'btn small', type: 'button', onclick: () => this.zoomCanvas?.fit() }, '适应画布'),
          h('button', { class: 'btn primary small', type: 'button', onclick: () => this.closeZoom() }, '关闭 (Esc)'),
        ),
        hostEl,
      ),
    );

    this.zoomCanvas = new FloorCanvas({
      canvas: canvasEl,
      host: hostEl,
      hoverCard: hoverEl,
      getMap: () => this.slotResults[this.zoomTierIndex]?.map || null,
      getSelectedStripId: () => null,
      onSelectStrip: () => {},
      getUi: () => this.store.ui,
      getCellTypes: () => this.store.dungeon.cellTypes,
    });
    window.requestAnimationFrame(() => {
      this.zoomCanvas?.resize();
      this.zoomCanvas?.fit();
    });
  }

  closeZoom() {
    if (this.zoomTierIndex === null) return;
    this.zoomCanvas?.resizeObserver?.disconnect();
    this.zoomCanvas = null;
    this.zoomTierIndex = null;
    this.zoomOverlayEl.hidden = true;
    this.zoomOverlayEl.replaceChildren();
  }

  /* ---------------- 渲染 ---------------- */

  renderAll() {
    this.renderTierList();
    this.renderEntryEditor();
    this.renderToolbar();
    for (let i = 0; i < WORLD_TIER_COUNT; i += 1) this.renderSlot(i);
    this.renderStatusbar();
  }

  renderTierList() {
    const prevScroll = this.listBodyEl?.scrollTop ?? 0;
    const wp = this.getPools();
    const totalEntries = wp.pools.reduce((sum, p) => sum + p.entries.length, 0);

    const tierSections = wp.pools.map((pool, tierIdx) => {
      const collapsed = this.collapsedTiers.has(tierIdx);
      const header = h(
        'div',
        { class: 'world-tier-head' },
        h(
          'button',
          {
            class: 'world-tier-toggle',
            type: 'button',
            onclick: () => {
              if (collapsed) this.collapsedTiers.delete(tierIdx);
              else this.collapsedTiers.add(tierIdx);
              this.renderTierList();
            },
          },
          h('span', { class: 'world-tier-caret' }, collapsed ? '▸' : '▾'),
          h('span', { class: 'world-tier-name' }, pool.name),
          h('span', { class: 'pill' }, String(pool.entries.length)),
        ),
        h(
          'button',
          {
            class: 'link-btn',
            type: 'button',
            title: `在${pool.name}中新增地图配置`,
            onclick: () => {
              let createdId = null;
              this.store.updateWorldPools((draft) => {
                const next = createWorldMapEntry(tierIdx, draft.pools[tierIdx].entries);
                draft.pools[tierIdx].entries.push(next);
                createdId = next.id;
              }, `在${pool.name}新增地图配置`);
              this.collapsedTiers.delete(tierIdx);
              this.selectedTierIndex = tierIdx;
              this.selectedEntryId = createdId;
              this.editorOpen = true;
              this.syncEditorVisibility();
              this.renderAll();
            },
          },
          '+ 新增',
        ),
      );

      const rows = collapsed
        ? []
        : pool.entries.length === 0
          ? [h('div', { class: 'field-hint world-tier-empty' }, '暂无配置，点击右上角「+ 新增」添加。')]
          : pool.entries.map((entry, eIdx) => {
              const active = tierIdx === this.selectedTierIndex && entry.id === this.selectedEntryId;
              const minStrips = Math.min(...entry.regions.map((r) => r.stripMin));
              const maxStrips = Math.max(...entry.regions.map((r) => r.stripMax));
              return h(
                'div',
                {
                  class: `row world-entry-row${active ? ' active' : ''}`,
                  role: 'button',
                  tabindex: '0',
                  onclick: () => {
                    this.selectedTierIndex = tierIdx;
                    this.selectedEntryId = entry.id;
                    this.editorOpen = true;
                    this.syncEditorVisibility();
                    this.renderTierList();
                    this.renderEntryEditor();
                  },
                },
                h(
                  'div',
                  { class: 'world-entry-dots' },
                  ...entry.regions.map((r) => h('span', { class: 'palette-dot', style: { background: r.color } })),
                ),
                h(
                  'span',
                  { class: 'row-main' },
                  h('span', { class: 'row-name' }, entry.name),
                  h(
                    'span',
                    { class: 'row-sub' },
                    `权 ${entry.weight} · ${minStrips}-${maxStrips}条 · 环≥${entry.topology.minCycleLength} · 出生:${formatSpawnRegionLabel(entry)}`,
                  ),
                ),
                h(
                  'div',
                  { class: 'world-entry-actions' },
                  h(
                    'button',
                    {
                      class: 'tree-action',
                      type: 'button',
                      title: '复制配置',
                      onclick: (ev) => {
                        ev.stopPropagation();
                        let copiedId = null;
                        this.store.updateWorldPools((draft) => {
                          const src = draft.pools[tierIdx].entries[eIdx];
                          const copy = createWorldMapEntry(tierIdx, draft.pools[tierIdx].entries, {
                            ...JSON.parse(JSON.stringify(src)),
                            id: undefined,
                            name: `${src.name} 副本`,
                          });
                          draft.pools[tierIdx].entries.splice(eIdx + 1, 0, copy);
                          copiedId = copy.id;
                        }, '复制地图配置');
                        this.selectedTierIndex = tierIdx;
                        this.selectedEntryId = copiedId;
                        this.renderAll();
                      },
                    },
                    icon('copy', 12),
                  ),
                  h(
                    'button',
                    {
                      class: 'tree-action',
                      type: 'button',
                      title: '删除配置',
                      onclick: async (ev) => {
                        ev.stopPropagation();
                        const ok = await confirmDialog({
                          title: '删除地图配置',
                          message: `确定从「${pool.name}」删除「${entry.name}」？`,
                          confirmText: '删除',
                          danger: true,
                        });
                        if (!ok) return;
                        this.store.updateWorldPools((draft) => {
                          draft.pools[tierIdx].entries.splice(eIdx, 1);
                        }, '删除地图配置');
                      },
                    },
                    icon('trash', 12),
                  ),
                ),
              );
            });

      return h('div', { class: 'world-tier-group' }, header, ...rows);
    });

    const headBar = h(
      'div',
      { class: 'section world-list-header' },
      h(
        'div',
        { class: 'section-title', style: { marginBottom: 0 } },
        h('span', null, `四等级地图池 (${totalEntries})`),
        h(
          'button',
          {
            class: 'link-btn',
            type: 'button',
            onclick: () => {
              this.editorOpen = !this.editorOpen;
              this.syncEditorVisibility();
              window.requestAnimationFrame(() => this.resize());
              this.renderTierList();
            },
          },
          this.editorOpen ? '收起参数' : '展开参数',
        ),
      ),
    );

    this.listBodyEl = h('div', { class: 'panel-body' }, headBar, ...tierSections);
    mount(this.listEl, this.listBodyEl);
    this.listBodyEl.scrollTop = prevScroll;
  }

  renderEntryEditor() {
    const prevScroll = this.editorBodyEl?.scrollTop ?? 0;
    const wp = this.getPools();
    const { pool, entry, tierIndex } = this.selectedContext(wp);

    if (!pool || !entry) {
      this.editorBodyEl = h(
        'div',
        { class: 'panel-body' },
        h(
          'div',
          { class: 'section' },
          h('div', { class: 'section-title' }, h('span', null, '地图项配置')),
          h('p', { class: 'field-hint' }, '请在左侧任意等级下选择或新增一条地图配置。'),
        ),
      );
      mount(this.editorEl, this.editorBodyEl);
      return;
    }

    const mutateEntry = (fn, label) => {
      this.store.updateWorldPools((draft) => {
        const target = draft.pools[tierIndex]?.entries?.find((e) => e.id === entry.id);
        if (target) fn(target, draft);
      }, label);
    };

    const floorProfile = worldEntryToFloorProfile(entry);
    const est = estimateFloorBudget(floorProfile, this.store.dungeon.stripTemplates, this.store.dungeon.cellTypes);
    const spawnSlotVal = entry.spawnRegionSlot === 'random' ? 'random' : String(entry.spawnRegionSlot ?? 0);

    // 1. 基础属性
    const basicSection = h(
      'div',
      { class: 'section' },
      h(
        'div',
        { class: 'section-title' },
        h('span', null, `${pool.name} · 地图配置`),
        h(
          'button',
          {
            class: 'link-btn',
            type: 'button',
            onclick: () => {
              this.editorOpen = false;
              this.syncEditorVisibility();
              window.requestAnimationFrame(() => this.resize());
              this.renderTierList();
            },
          },
          '收起',
        ),
      ),
      h(
        'div',
        { class: 'form-row-2' },
        h(
          'label',
          { class: 'mini-field' },
          h('span', null, '配置名称'),
          h('input', {
            class: 'input',
            type: 'text',
            value: entry.name,
            onchange: (e) => mutateEntry((t) => { t.name = e.target.value.trim() || t.name; }, '修改地图配置名称'),
          }),
        ),
        h(
          'label',
          { class: 'mini-field' },
          h('span', null, '所属等级'),
          h(
            'select',
            {
              class: 'select',
              onchange: (e) => {
                const nextTier = clamp(Number(e.target.value), 0, WORLD_TIER_COUNT - 1);
                if (nextTier === tierIndex) return;
                this.store.updateWorldPools((draft) => {
                  const list = draft.pools[tierIndex].entries;
                  const idx = list.findIndex((item) => item.id === entry.id);
                  if (idx < 0) return;
                  const [moved] = list.splice(idx, 1);
                  draft.pools[nextTier].entries.push(moved);
                }, '调整地图配置等级');
                this.selectedTierIndex = nextTier;
                this.collapsedTiers.delete(nextTier);
                this.renderAll();
              },
            },
            ...WORLD_TIER_NAMES.map((label, idx) =>
              h('option', { value: String(idx), selected: idx === tierIndex }, label),
            ),
          ),
        ),
      ),
      h(
        'div',
        { class: 'form-row-3', style: { marginTop: '6px' } },
        h(
          'label',
          { class: 'mini-field' },
          h('span', null, '抽取权重'),
          h('input', {
            class: 'input mono-input',
            type: 'number',
            min: '0.1',
            step: '0.5',
            value: String(entry.weight),
            onchange: (e) => mutateEntry((t) => {
              const v = Number(e.target.value);
              t.weight = Number.isFinite(v) && v > 0 ? v : 1;
            }, '修改抽取权重'),
          }),
        ),
        h(
          'label',
          { class: 'mini-field', title: '导出给 Unity 读取时使用的 Pattern 模板库 ID' },
          h('span', null, '模板库 ID'),
          h('input', {
            class: 'input mono-input',
            type: 'text',
            value: entry.patternLibraryId || 'Sample',
            onchange: (e) => mutateEntry((t) => {
              t.patternLibraryId = e.target.value.trim() || 'Sample';
            }, '修改模板库 ID'),
          }),
        ),
        h(
          'label',
          { class: 'mini-field', title: '同步到 Unity 时的环路留白策略' },
          h('span', null, '环路留白'),
          h(
            'select',
            {
              class: 'select',
              onchange: (e) => mutateEntry((t) => { t.ringSpacing = e.target.value; }, '修改环路留白'),
            },
            ...RING_SPACING.map((mode) =>
              h('option', { value: mode, selected: (entry.ringSpacing || 'balanced') === mode }, RING_SPACING_LABELS[mode]),
            ),
          ),
        ),
      ),
    );

    // 2. 出生点区域 + 三区域配置
    const regionsSection = h(
      'div',
      { class: 'section' },
      h('div', { class: 'section-title' }, h('span', null, '出生点与三区域配置')),
      h(
        'label',
        { class: 'mini-field', style: { marginBottom: '8px' } },
        h('span', null, '指定出生点区域（留空则在三区随机）'),
        h(
          'select',
          {
            class: 'select',
            onchange: (e) => mutateEntry((t) => {
              t.spawnRegionSlot = e.target.value === 'random' ? 'random' : Number(e.target.value);
            }, '修改出生点区域'),
          },
          h('option', { value: 'random', selected: spawnSlotVal === 'random' }, '留空（全部区域随机）'),
          ...entry.regions.map((r, i) =>
            h('option', { value: String(i), selected: spawnSlotVal === String(i) }, `${'ABC'[i]} · ${r.name}`),
          ),
        ),
      ),
      ...entry.regions.map((region, idx) =>
        h(
          'div',
          { class: 'floor-region-card' },
          h(
            'div',
            { class: 'floor-region-head' },
            h('input', {
              type: 'color',
              class: 'color-swatch-input',
              value: region.color,
              onchange: (e) => mutateEntry((t) => { t.regions[idx].color = e.target.value; }, '修改区域颜色'),
            }),
            h(
              'select',
              {
                class: 'select',
                onchange: (e) => {
                  const preset = DUNGEON_REGION_PRESETS.find((p) => p.id === e.target.value);
                  if (!preset) return;
                  mutateEntry((t) => {
                    t.regions[idx].regionTypeId = preset.id;
                    t.regions[idx].id = preset.id;
                    t.regions[idx].name = preset.name;
                    t.regions[idx].shortName = preset.shortName;
                    t.regions[idx].color = preset.color;
                  }, '切换区域类型');
                },
              },
              ...DUNGEON_REGION_PRESETS.map((pr) =>
                h('option', { value: pr.id, selected: pr.id === (region.regionTypeId || region.id) }, pr.name),
              ),
            ),
            entry.spawnRegionSlot === 'random'
              ? h('span', { class: 'pill' }, `${'ABC'[idx]} · 随机`)
              : idx === entry.spawnRegionSlot
                ? h('span', { class: 'pill spawn' }, `${'ABC'[idx]} · 出生`)
                : h('span', { class: 'pill' }, `${'ABC'[idx]} · 翼区`),
          ),
          h(
            'div',
            { class: 'form-row-3', style: { marginTop: '7px' } },
            h(
              'label',
              { class: 'mini-field' },
              h('span', null, '最少条带'),
              h('input', {
                class: 'input mono-input',
                type: 'number',
                min: '3',
                max: '20',
                value: String(region.stripMin),
                onchange: (e) => mutateEntry((t) => {
                  const v = clamp(Math.round(Number(e.target.value) || 3), 3, 20);
                  t.regions[idx].stripMin = v;
                  if (t.regions[idx].stripMax < v) t.regions[idx].stripMax = v;
                }, '修改最少条带'),
              }),
            ),
            h(
              'label',
              { class: 'mini-field' },
              h('span', null, '最多条带'),
              h('input', {
                class: 'input mono-input',
                type: 'number',
                min: String(region.stripMin),
                max: '20',
                value: String(region.stripMax),
                onchange: (e) => mutateEntry((t) => {
                  t.regions[idx].stripMax = clamp(Math.round(Number(e.target.value) || t.regions[idx].stripMin), t.regions[idx].stripMin, 20);
                }, '修改最多条带'),
              }),
            ),
            h(
              'label',
              { class: 'mini-field' },
              h('span', null, '最多非环'),
              h('input', {
                class: 'input mono-input',
                type: 'number',
                min: '0',
                max: '20',
                value: String(region.maxNoLoop ?? 1),
                onchange: (e) => mutateEntry((t) => {
                  t.regions[idx].maxNoLoop = clamp(Math.round(Number(e.target.value) || 0), 0, 20);
                }, '修改最多非环'),
              }),
            ),
          ),
        ),
      ),
    );

    // 3. 拓扑结构（最小环长度 / 绿线连接 / 红区堆积 / 松散度 / 交叉）
    const loosenessPct = Math.round((entry.topology.looseness ?? 0.55) * 100);
    const topologySection = h(
      'div',
      { class: 'section' },
      h('div', { class: 'section-title' }, h('span', null, '拓扑结构与环路规则')),
      h(
        'div',
        { class: 'form-row-3' },
        h(
          'label',
          { class: 'mini-field', title: '任何闭合环路至少包含的条带数量（按条带数计）' },
          h('span', null, '最小环长度'),
          h('input', {
            class: 'input mono-input',
            type: 'number',
            min: '3',
            max: '20',
            value: String(entry.topology.minCycleLength ?? 4),
            onchange: (e) => mutateEntry((t) => {
              t.topology.minCycleLength = clamp(Math.round(Number(e.target.value) || 3), 3, 20);
            }, '修改最小环长度'),
          }),
        ),
        h(
          'label',
          { class: 'mini-field' },
          h('span', null, '最少环数'),
          h('input', {
            class: 'input mono-input',
            type: 'number',
            min: '1',
            max: '4',
            value: String(entry.topology.minLoopsPerRegion ?? 1),
            onchange: (e) => mutateEntry((t) => {
              t.topology.minLoopsPerRegion = clamp(Math.round(Number(e.target.value) || 1), 1, 4);
            }, '修改最少环数'),
          }),
        ),
        h(
          'label',
          { class: 'mini-field', title: '绿线闭合连接概率' },
          h('span', null, '额外成环率'),
          h('input', {
            class: 'input mono-input',
            type: 'number',
            min: '0',
            max: '1',
            step: '0.05',
            value: String(entry.topology.extraLoopChance ?? 0.4),
            onchange: (e) => mutateEntry((t) => {
              t.topology.extraLoopChance = clamp(Number(e.target.value) || 0, 0, 1);
            }, '修改额外成环率'),
          }),
        ),
      ),
      h(
        'label',
        { class: 'checkbox-row', style: { marginTop: '8px' } },
        h('input', {
          type: 'checkbox',
          checked: Boolean(entry.topology.localClustering),
          onchange: (e) => mutateEntry((t) => { t.topology.localClustering = e.target.checked; }, '切换局部密集堆积'),
        }),
        h('span', null, '启用局部密集堆积（红区多条带聚拢）'),
      ),
      h(
        'div',
        { class: 'looseness-box', style: { marginTop: '8px' } },
        h(
          'div',
          { class: 'looseness-head' },
          h('span', null, '结构松散度'),
          h('strong', { class: 'mono-val' }, `${loosenessPct}%`),
        ),
        h('input', {
          type: 'range',
          class: 'looseness-slider',
          min: '0',
          max: '100',
          step: '5',
          value: String(loosenessPct),
          onchange: (e) => mutateEntry((t) => { t.topology.looseness = clamp(Number(e.target.value) / 100, 0, 1); }, '调整松散度'),
        }),
      ),
      h(
        'div',
        { class: 'form-row-2', style: { marginTop: '6px', alignItems: 'center' } },
        h(
          'label',
          { class: 'checkbox-row' },
          h('input', {
            type: 'checkbox',
            checked: entry.topology.allowCrossing !== false,
            onchange: (e) => mutateEntry((t) => { t.topology.allowCrossing = e.target.checked; }, '切换区内单格交叉'),
          }),
          h('span', null, '允许区内交叉'),
        ),
        h(
          'label',
          { class: 'mini-field' },
          h('span', null, '交叉倾向'),
          h('input', {
            class: 'input mono-input',
            type: 'number',
            min: '0',
            max: '1',
            step: '0.05',
            disabled: entry.topology.allowCrossing === false,
            value: String(entry.topology.crossChance ?? 0.25),
            onchange: (e) => mutateEntry((t) => { t.topology.crossChance = clamp(Number(e.target.value) || 0, 0, 1); }, '修改交叉倾向'),
          }),
        ),
      ),
    );

    // 4. 预算与等级曲线
    const budgetAndProgSection = h(
      'div',
      { class: 'section' },
      h('div', { class: 'section-title' }, h('span', null, '预算与敌人等级曲线')),
      h(
        'div',
        { class: 'field-hint', style: { marginBottom: '6px' } },
        `留空自动：资 ${est.autoResourceMin}–${est.autoResourceMax} · 难 ${est.autoDifficultyMin}–${est.autoDifficultyMax}`,
      ),
      h(
        'div',
        { class: 'form-row-2' },
        h(
          'label',
          { class: 'mini-field' },
          h('span', null, '资源下限'),
          h('input', {
            class: 'input mono-input',
            type: 'number',
            min: '0',
            placeholder: `自动 ${est.autoResourceMin}`,
            value: entry.budget.resourceMin ?? '',
            onchange: (e) => mutateEntry((t) => {
              const raw = e.target.value.trim();
              t.budget.resourceMin = raw === '' ? null : Math.max(0, Number(raw) || 0);
            }, '修改资源下限'),
          }),
        ),
        h(
          'label',
          { class: 'mini-field' },
          h('span', null, '资源上限'),
          h('input', {
            class: 'input mono-input',
            type: 'number',
            min: '0',
            placeholder: `自动 ${est.autoResourceMax}`,
            value: entry.budget.resourceMax ?? '',
            onchange: (e) => mutateEntry((t) => {
              const raw = e.target.value.trim();
              t.budget.resourceMax = raw === '' ? null : Math.max(0, Number(raw) || 0);
            }, '修改资源上限'),
          }),
        ),
      ),
      h(
        'div',
        { class: 'form-row-2', style: { marginTop: '6px' } },
        h(
          'label',
          { class: 'mini-field' },
          h('span', null, '难度下限'),
          h('input', {
            class: 'input mono-input',
            type: 'number',
            min: '0',
            placeholder: `自动 ${est.autoDifficultyMin}`,
            value: entry.budget.difficultyMin ?? '',
            onchange: (e) => mutateEntry((t) => {
              const raw = e.target.value.trim();
              t.budget.difficultyMin = raw === '' ? null : Math.max(0, Number(raw) || 0);
            }, '修改难度下限'),
          }),
        ),
        h(
          'label',
          { class: 'mini-field' },
          h('span', null, '难度上限'),
          h('input', {
            class: 'input mono-input',
            type: 'number',
            min: '0',
            placeholder: `自动 ${est.autoDifficultyMax}`,
            value: entry.budget.difficultyMax ?? '',
            onchange: (e) => mutateEntry((t) => {
              const raw = e.target.value.trim();
              t.budget.difficultyMax = raw === '' ? null : Math.max(0, Number(raw) || 0);
            }, '修改难度上限'),
          }),
        ),
      ),
      h(
        'div',
        { class: 'form-row-3', style: { marginTop: '8px' } },
        h(
          'label',
          { class: 'mini-field' },
          h('span', null, '起始等级'),
          h('input', {
            class: 'input mono-input',
            type: 'number',
            min: '1',
            max: '99',
            value: String(entry.progression.baseLevel),
            onchange: (e) => mutateEntry((t) => { t.progression.baseLevel = clamp(Math.round(Number(e.target.value) || 1), 1, 99); }, '修改起始等级'),
          }),
        ),
        h(
          'label',
          { class: 'mini-field' },
          h('span', null, '每跳增幅'),
          h('input', {
            class: 'input mono-input',
            type: 'number',
            min: '0',
            max: '5',
            step: '0.25',
            value: String(entry.progression.levelPerHop),
            onchange: (e) => mutateEntry((t) => { t.progression.levelPerHop = clamp(Number(e.target.value) || 0, 0, 5); }, '修改每跳增幅'),
          }),
        ),
        h(
          'label',
          { class: 'mini-field' },
          h('span', null, '等级上限'),
          h('input', {
            class: 'input mono-input',
            type: 'number',
            min: '1',
            max: '999',
            value: String(entry.progression.maxLevel),
            onchange: (e) => mutateEntry((t) => { t.progression.maxLevel = clamp(Math.round(Number(e.target.value) || 20), 1, 999); }, '修改等级上限'),
          }),
        ),
      ),
    );

    this.editorBodyEl = h('div', { class: 'panel-body' }, basicSection, regionsSection, topologySection, budgetAndProgSection);
    mount(this.editorEl, this.editorBodyEl);
    this.editorBodyEl.scrollTop = prevScroll;
  }

  renderToolbar() {
    const ui = this.store.ui;
    const toggles = [
      { key: 'floorShowGrid', label: '网格', icon: 'grid' },
      { key: 'floorShowRegions', label: '区域框', icon: 'eye' },
      { key: 'floorShowCells', label: '格子', icon: 'contents' },
      { key: 'floorShowLevels', label: '等级', icon: 'labels' },
      { key: 'floorShowGraph', label: '节点图', icon: 'layers' },
      { key: 'floorShowHeatmap', label: '距离热力', icon: 'spawn' },
    ];

    mount(
      this.toolbarEl,
      h(
        'button',
        {
          class: 'btn primary',
          type: 'button',
          id: 'btn-world-generate-all',
          onclick: () => this.generateAllFour(),
        },
        icon('generate', 14),
        h('span', null, '一键生成四张'),
      ),
      h(
        'button',
        {
          class: 'tool-btn',
          type: 'button',
          title: '全部适应画布 (F)',
          onclick: () => this.fitAll(),
        },
        icon('fit', 13),
        h('span', null, '适应'),
      ),
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
              onclick: () => this.store.toggleUi(t.key),
            },
            icon(t.icon, 13),
            h('span', null, t.label),
          ),
        ),
      ),
      h('span', { class: 'toolbar-spacer' }),
      h(
        'div',
        { class: 'tool-group' },
        h(
          'button',
          {
            class: 'tool-btn',
            type: 'button',
            title: '导入完整地图配置 JSON',
            onclick: () => void this.importJson(),
          },
          icon('import', 13),
          h('span', null, '导入 JSON'),
        ),
        h(
          'button',
          {
            class: 'tool-btn',
            type: 'button',
            id: 'btn-world-export-json',
            title: '导出包含全部四等级地图配置的单份 JSON 文件',
            onclick: () => this.exportJson(),
          },
          icon('export', 13),
          h('span', null, '保存 JSON'),
        ),
      ),
    );
  }

  renderSlot(tierIndex) {
    const wp = this.getPools();
    const pool = wp.pools[tierIndex];
    const els = this.slotEls[tierIndex];
    const result = this.slotResults[tierIndex];
    const selectedFilter = this.slotEntryFilter[tierIndex] || '';

    const selectEl = h(
      'select',
      {
        class: 'select world-box-select',
        'aria-label': `${pool.name}地图配置选择`,
        onchange: (e) => {
          this.slotEntryFilter[tierIndex] = e.target.value;
        },
      },
      h('option', { value: '', selected: selectedFilter === '' }, '全部随机（留空）'),
      ...pool.entries.map((entry) =>
        h('option', { value: entry.id, selected: selectedFilter === entry.id }, `${entry.name} (权 ${entry.weight})`),
      ),
    );

    mount(
      els.headEl,
      h('span', { class: 'pill spawn world-box-tier' }, pool.name),
      selectEl,
      h(
        'button',
        {
          class: 'btn primary small',
          type: 'button',
          disabled: pool.entries.length === 0,
          onclick: () => this.generateSlot(tierIndex),
        },
        icon('generate', 12),
        h('span', null, '生成'),
      ),
      h(
        'button',
        {
          class: 'tree-action',
          type: 'button',
          title: '适应此框画布',
          disabled: !result?.map,
          onclick: () => this.slotCanvases[tierIndex]?.fit(),
        },
        icon('fit', 12),
      ),
      h(
        'button',
        {
          class: 'tree-action',
          type: 'button',
          title: '放大查看此张地图',
          disabled: !result?.map,
          onclick: () => this.openZoom(tierIndex),
        },
        icon('plus', 12),
      ),
    );

    els.emptyEl.hidden = Boolean(result?.map);
    if (!result?.map) {
      mount(
        els.emptyEl,
        h(
          'div',
          { class: 'world-box-empty-inner' },
          h('div', { class: 'field-hint' }, pool.entries.length ? '点击右上「生成」或顶部「一键生成四张」预览地图' : '该等级暂无地图配置'),
        ),
      );
      mount(els.footEl, h('span', { class: 'field-hint', style: { margin: 0 } }, `${pool.entries.length} 条可选配置 · 未生成`));
    } else {
      const m = result.map;
      const st = m.stats;
      const spawnSlot = m.spawnRegionSlot ?? 0;
      const spawnReg = m.regions[spawnSlot];
      const totalCycles = st.regions.reduce((sum, r) => sum + (r.cycles || 0), 0);
      mount(
        els.footEl,
        h('span', { class: `status-dot ${m.errors.length === 0 ? 'ok' : 'err'}` }),
        h('span', { class: 'world-foot-name', title: result.entryName }, `${result.entryName}${result.pickedRandomly ? ' (随机)' : ''}`),
        h('span', { class: 'world-foot-meta' }, `出生 ${'ABC'[spawnSlot]}·${spawnReg?.shortName || ''}`),
        h('span', { class: 'world-foot-meta' }, `${st.strips}条 · ${totalCycles}环${st.minCycleLength ? `(≥${st.minCycleLength})` : ''}`),
        h('span', { class: 'world-foot-meta' }, `资${st.totalResource} / 难${st.totalDifficulty}`),
        h('span', { class: 'world-foot-seed' }, result.seed),
      );
    }

    this.slotCanvases[tierIndex]?.render();
  }

  renderStatusbar() {
    const wp = this.getPools();
    const issues = validateWorldPools(wp);
    const totalEntries = wp.pools.reduce((sum, p) => sum + p.entries.length, 0);
    const readyCount = this.slotResults.filter((r) => Boolean(r?.map)).length;
    mount(
      this.statusbarEl,
      h(
        'span',
        { class: 'status-item' },
        h('span', { class: `status-dot ${issues.length ? 'warn' : 'ok'}` }),
        issues.length ? issues[0] : `四等级共 ${totalEntries} 条配置 · 格式有效`,
      ),
      h('span', { class: 'spacer' }),
      h('span', { class: 'status-item' }, `已生成预览 ${readyCount}/4`),
    );
  }
}

export const WorldPlaceholderView = WorldView;
