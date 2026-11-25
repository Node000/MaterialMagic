/**
 * 模式二：单层三角地牢生成器视图。
 *
 * 整合左侧方案与种子面板、中央 FloorCanvas、右侧拓扑/预算达标与条带检视微调器。
 */

import { makeFloorFile } from '../core/floor-file.js';
import { exactOpenEdgeKey } from '../core/strip-edges.js';
import { DUNGEON_REGION_PRESETS, clamp } from '../core/constants.js';
import {
  estimateFloorBudget,
} from '../core/floor-generator.js';
import { exactShapeKey, placeCellsAt, uniqueRotations } from '../core/polyomino.js';
import { randomSeed } from '../core/rng.js';
import {
  cellTypeMap,
  computeTemplateMetrics,
  templateMatchesRegion,
} from '../core/strip-templates.js';
import {
  clear,
  downloadBlob,
  downloadText,
  h,
  icon,
  mount,
  slugify,
} from './dom.js';
import { FloorCanvas } from './floor-canvas.js';
import { toast } from './modals.js';

export class FloorEditorView {
  constructor({ root, store }) {
    this.root = root;
    this.store = store;
    this.seedValue = store.currentFloorMap?.seed || randomSeed();

    this.buildShell();
    this.canvas = new FloorCanvas({
      canvas: this.canvasEl,
      host: this.canvasHostEl,
      hoverCard: this.hoverCardEl,
      store,
    });
    this.canvas.onZoomChange = () => this.renderToolbar();

    store.on('change', () => {
      if (store.currentFloorMap?.seed) this.seedValue = store.currentFloorMap.seed;
      this.renderAll();
    });
    store.on('floor-selection', () => {
      this.renderRight();
      this.canvas.render();
    });
    store.on('ui', () => {
      this.renderToolbar();
      this.renderLegend();
      this.canvas.render();
    });

    this.renderAll();
  }

  buildShell() {
    this.leftEl = h('aside', { class: 'panel floor-sidebar' });
    this.toolbarEl = h('div', { class: 'stage-toolbar' });
    this.canvasEl = h('canvas', { id: 'floor-map-canvas' });
    this.emptyEl = h('div', { class: 'canvas-empty', hidden: true });
    this.hoverCardEl = h('div', { class: 'hover-card', hidden: true });
    this.legendEl = h('div', { class: 'floor-cell-legend', 'aria-label': '格子颜色图例' });
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
      { class: 'stage' },
      this.toolbarEl,
      this.canvasHostEl,
      this.statusbarEl,
    );
    this.rightEl = h('aside', { class: 'panel floor-inspector' });

    mount(this.root, this.leftEl, this.centerEl, this.rightEl);
  }

  setTheme(theme) {
    this.canvas?.setTheme(theme);
  }

  fit() {
    this.canvas?.fit();
  }

  runGenerate(customSeed = null) {
    const seed = (customSeed ?? this.seedValue)?.trim() || randomSeed();
    try {
      const map = this.store.generateFloor({ seed });
      this.seedValue = map.seed;
      window.requestAnimationFrame(() => this.canvas.fit());
      const budgetOk = map.stats.resourceInRange && map.stats.difficultyInRange;
      toast(
        `已生成单层三角地图 · ${map.strips.length} 条带 · ${map.seed}`,
        {
          type: map.errors.length === 0 ? 'success' : 'warning',
          detail: budgetOk
            ? `资源 ${map.stats.totalResource} / 难度 ${map.stats.totalDifficulty}（命中区间）`
            : `资源 ${map.stats.totalResource} / 难度 ${map.stats.totalDifficulty}`,
        },
      );
    } catch (error) {
      console.error(error);
      toast(`生成失败：${error.message}`, { type: 'error' });
    }
  }

  renderAll() {
    this.renderLeft();
    this.renderToolbar();
    this.renderLegend();
    this.renderEmptyState();
    this.renderStatusbar();
    this.renderRight();
    this.canvas.render();
  }

  renderLegend() {
    if (!this.legendEl) return;
    const hasMap = Boolean(this.store.currentFloorMap);
    const show = hasMap && !this.store.ui.floorShowHeatmap && this.store.ui.floorShowCells;
    this.legendEl.hidden = !show;
    if (!show) return;
    const cellTypes = this.store.dungeon.cellTypes;
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

  /* ---------------- 左侧：三区域方案 / 预算 / 曲线 / 历史 ---------------- */

  renderLeft() {
    const profile = this.store.activeFloorProfile;
    const { stripTemplates, cellTypes } = this.store.dungeon;
    const est = estimateFloorBudget(profile, stripTemplates, cellTypes);

    // 1. 种子与生成按钮
    const seedInput = h('input', {
      class: 'input mono-input',
      type: 'text',
      value: this.seedValue,
      spellcheck: 'false',
      oninput: () => {
        this.seedValue = seedInput.value;
      },
      onkeydown: (e) => {
        if (e.key === 'Enter') this.runGenerate(seedInput.value);
      },
    });

    const genSection = h(
      'div',
      { class: 'section' },
      h('div', { class: 'section-title' }, h('span', null, '单层三角生成')),
      h(
        'div',
        { class: 'field-row' },
        seedInput,
        h(
          'button',
          {
            class: 'btn small',
            type: 'button',
            title: '换一个随机种子并立即生成',
            onclick: () => {
              this.seedValue = randomSeed();
              this.runGenerate(this.seedValue);
            },
          },
          '🎲 随机',
        ),
      ),
      h(
        'button',
        {
          class: 'btn primary full-width',
          type: 'button',
          style: { marginTop: '8px' },
          onclick: () => this.runGenerate(seedInput.value),
        },
        icon('generate', 14),
        h('span', null, '生成单层地图'),
      ),
    );

    // 2. 三区域配置（从 城市/工厂/郊区/太空 选择 3 个呈三角形）
    const regionsSection = h(
      'div',
      { class: 'section' },
      h('div', { class: 'section-title' }, h('span', null, '三区域配置（城市/工厂/郊区/太空）')),
      ...profile.regions.map((region, idx) =>
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
              onchange: (e) =>
                this.store.updateFloorProfile((p) => {
                  p.regions[idx].color = e.target.value;
                }, '修改区域颜色'),
            }),
            h(
              'select',
              {
                class: 'select',
                onchange: (e) => {
                  const nextId = e.target.value;
                  const preset = DUNGEON_REGION_PRESETS.find((p) => p.id === nextId);
                  if (!preset) return;
                  this.store.updateFloorProfile((p) => {
                    const dupIdx = p.regions.findIndex((r, i) => i !== idx && r.id === nextId);
                    if (dupIdx >= 0) {
                      // 与另一个槽位互换区域类型，避免重复
                      const prevPreset = DUNGEON_REGION_PRESETS.find((pr) => pr.id === p.regions[idx].id);
                      if (prevPreset) {
                        p.regions[dupIdx].id = prevPreset.id;
                        p.regions[dupIdx].name = prevPreset.name;
                        p.regions[dupIdx].shortName = prevPreset.shortName;
                        p.regions[dupIdx].color = prevPreset.color;
                      }
                    }
                    p.regions[idx].id = preset.id;
                    p.regions[idx].name = preset.name;
                    p.regions[idx].shortName = preset.shortName;
                    p.regions[idx].color = preset.color;
                  }, '切换区域类型');
                },
              },
              ...DUNGEON_REGION_PRESETS.map((pr) =>
                h('option', { value: pr.id, selected: pr.id === region.id }, pr.name),
              ),
            ),
            idx === 0 ? h('span', { class: 'pill spawn' }, '出生区') : h('span', { class: 'pill' }, `翼区 ${idx}`),
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
                max: '18',
                value: String(region.stripMin),
                onchange: (e) =>
                  this.store.updateFloorProfile((p) => {
                    const v = clamp(Math.round(Number(e.target.value) || 3), 3, 18);
                    p.regions[idx].stripMin = v;
                    if (p.regions[idx].stripMax < v) p.regions[idx].stripMax = v;
                  }, '修改区域最少条带'),
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
                onchange: (e) =>
                  this.store.updateFloorProfile((p) => {
                    p.regions[idx].stripMax = clamp(
                      Math.round(Number(e.target.value) || region.stripMin),
                      p.regions[idx].stripMin,
                      20,
                    );
                  }, '修改区域最多条带'),
              }),
            ),
            h(
              'label',
              { class: 'mini-field', title: '该区域最多允许出现的非环端点条带数；设为 0 则不生成非环条带' },
              h('span', null, '最多非环'),
              h('input', {
                class: 'input mono-input',
                type: 'number',
                min: '0',
                max: '10',
                value: String(region.maxNoLoop ?? 1),
                onchange: (e) =>
                  this.store.updateFloorProfile((p) => {
                    p.regions[idx].maxNoLoop = clamp(Math.round(Number(e.target.value) || 0), 0, 10);
                  }, '修改区域最多非环条带数'),
              }),
            ),
          ),
        ),
      ),
    );

    // 3. 总资源 / 总难度区间（留空自动计算）
    const budgetSection = h(
      'div',
      { class: 'section' },
      h(
        'div',
        { class: 'section-title' },
        h('span', null, '整图资源与难度区间'),
        !est.autoResource || !est.autoDifficulty
          ? h(
              'button',
              {
                class: 'link-btn',
                type: 'button',
                onclick: () =>
                  this.store.updateFloorProfile((p) => {
                    p.budget = {
                      resourceMin: null,
                      resourceMax: null,
                      difficultyMin: null,
                      difficultyMax: null,
                    };
                  }, '恢复自动预算区间'),
              },
              '全部自动',
            )
          : null,
      ),
      h(
        'div',
        { class: 'field-hint', style: { marginBottom: '6px' } },
        `留空即自动计算。当前预计：资源 ${est.autoResourceMin}–${est.autoResourceMax} · 难度 ${est.autoDifficultyMin}–${est.autoDifficultyMax}`,
      ),
      h(
        'div',
        { class: 'form-row-2' },
        h(
          'label',
          { class: 'mini-field' },
          h('span', null, '总资源下限'),
          h('input', {
            class: 'input mono-input',
            type: 'number',
            min: '0',
            placeholder: `自动 ${est.autoResourceMin}`,
            value: profile.budget.resourceMin ?? '',
            onchange: (e) =>
              this.store.updateFloorProfile((p) => {
                const raw = e.target.value.trim();
                p.budget.resourceMin = raw === '' ? null : Math.max(0, Number(raw) || 0);
              }, '修改资源下限'),
          }),
        ),
        h(
          'label',
          { class: 'mini-field' },
          h('span', null, '总资源上限'),
          h('input', {
            class: 'input mono-input',
            type: 'number',
            min: '0',
            placeholder: `自动 ${est.autoResourceMax}`,
            value: profile.budget.resourceMax ?? '',
            onchange: (e) =>
              this.store.updateFloorProfile((p) => {
                const raw = e.target.value.trim();
                p.budget.resourceMax = raw === '' ? null : Math.max(0, Number(raw) || 0);
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
          h('span', null, '总难度下限'),
          h('input', {
            class: 'input mono-input',
            type: 'number',
            min: '0',
            placeholder: `自动 ${est.autoDifficultyMin}`,
            value: profile.budget.difficultyMin ?? '',
            onchange: (e) =>
              this.store.updateFloorProfile((p) => {
                const raw = e.target.value.trim();
                p.budget.difficultyMin = raw === '' ? null : Math.max(0, Number(raw) || 0);
              }, '修改难度下限'),
          }),
        ),
        h(
          'label',
          { class: 'mini-field' },
          h('span', null, '总难度上限'),
          h('input', {
            class: 'input mono-input',
            type: 'number',
            min: '0',
            placeholder: `自动 ${est.autoDifficultyMax}`,
            value: profile.budget.difficultyMax ?? '',
            onchange: (e) =>
              this.store.updateFloorProfile((p) => {
                const raw = e.target.value.trim();
                p.budget.difficultyMax = raw === '' ? null : Math.max(0, Number(raw) || 0);
              }, '修改难度上限'),
          }),
        ),
      ),
    );

    // 4. 环路、松散度、交叉与距离等级曲线
    const loosenessPct = Math.round((profile.topology.looseness ?? 0.55) * 100);
    const loosenessLabel =
      loosenessPct <= 30 ? '紧凑（偏中段相连）' : loosenessPct >= 70 ? '松散（偏头尾相连）' : '均衡';

    const curveSection = h(
      'div',
      { class: 'section' },
      h('div', { class: 'section-title' }, h('span', null, '结构松散度、拓扑与等级曲线')),
      h(
        'div',
        { class: 'looseness-box' },
        h(
          'div',
          { class: 'looseness-head' },
          h('span', null, '结构松散度'),
          h('strong', { class: 'mono-val' }, `${loosenessPct}% · ${loosenessLabel}`),
        ),
        h('input', {
          type: 'range',
          class: 'looseness-slider',
          min: '0',
          max: '100',
          step: '5',
          value: String(loosenessPct),
          onchange: (e) =>
            this.store.updateFloorProfile((p) => {
              p.topology.looseness = clamp(Number(e.target.value) / 100, 0, 1);
            }, '调整结构松散度'),
        }),
        h(
          'div',
          { class: 'looseness-axis' },
          h('span', null, '0% 紧凑（连接偏中段）'),
          h('span', null, '100% 松散（连接偏头尾）'),
        ),
      ),
      h(
        'div',
        { class: 'form-row-2', style: { marginTop: '8px' } },
        h(
          'label',
          { class: 'mini-field' },
          h('span', null, '每区最少环数'),
          h('input', {
            class: 'input mono-input',
            type: 'number',
            min: '1',
            max: '4',
            value: String(profile.topology.minLoopsPerRegion),
            onchange: (e) =>
              this.store.updateFloorProfile((p) => {
                p.topology.minLoopsPerRegion = clamp(Math.round(Number(e.target.value) || 1), 1, 4);
              }, '修改最少环数'),
          }),
        ),
        h(
          'label',
          { class: 'mini-field' },
          h('span', null, '额外成环概率'),
          h('input', {
            class: 'input mono-input',
            type: 'number',
            min: '0',
            max: '1',
            step: '0.05',
            value: String(profile.topology.extraLoopChance),
            onchange: (e) =>
              this.store.updateFloorProfile((p) => {
                p.topology.extraLoopChance = clamp(Number(e.target.value) || 0, 0, 1);
              }, '修改成环概率'),
          }),
        ),
      ),
      h(
        'div',
        { class: 'form-row-2', style: { marginTop: '6px', alignItems: 'center' } },
        h(
          'label',
          { class: 'checkbox-row' },
          h('input', {
            type: 'checkbox',
            checked: profile.topology.allowCrossing,
            onchange: (e) =>
              this.store.updateFloorProfile((p) => {
                p.topology.allowCrossing = e.target.checked;
              }, '切换区内单格交叉'),
          }),
          h('span', null, '允许区内单格交叉'),
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
            disabled: !profile.topology.allowCrossing,
            value: String(profile.topology.crossChance),
            onchange: (e) =>
              this.store.updateFloorProfile((p) => {
                p.topology.crossChance = clamp(Number(e.target.value) || 0, 0, 1);
              }, '修改交叉倾向'),
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
            value: String(profile.progression.baseLevel),
            onchange: (e) =>
              this.store.updateFloorProfile((p) => {
                p.progression.baseLevel = clamp(Math.round(Number(e.target.value) || 1), 1, 99);
              }, '修改起始等级'),
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
            value: String(profile.progression.levelPerHop),
            onchange: (e) =>
              this.store.updateFloorProfile((p) => {
                p.progression.levelPerHop = clamp(Number(e.target.value) || 0, 0, 5);
              }, '修改每跳增幅'),
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
            value: String(profile.progression.maxLevel),
            onchange: (e) =>
              this.store.updateFloorProfile((p) => {
                p.progression.maxLevel = clamp(Math.round(Number(e.target.value) || 20), 1, 999);
              }, '修改等级上限'),
          }),
        ),
      ),
    );

    // 5. 生成历史
    const historySection =
      this.store.floorMaps.length > 0
        ? h(
            'div',
            { class: 'section' },
            h('div', { class: 'section-title' }, h('span', null, `已生成记录 (${this.store.floorMaps.length})`)),
            ...this.store.floorMaps.map((fm) =>
              h(
                'div',
                {
                  class: `row${fm.id === this.store.currentFloorMap?.id ? ' active' : ''}`,
                  role: 'button',
                  tabindex: '0',
                  onclick: () => this.store.selectFloorMap(fm.id),
                },
                h('span', { class: `status-dot ${fm.errors.length === 0 ? 'ok' : 'err'}` }),
                h(
                  'span',
                  { class: 'row-main' },
                  h('span', { class: 'row-name' }, fm.seed),
                  h(
                    'span',
                    { class: 'row-sub' },
                    `${fm.strips.length} 条 · 资 ${fm.stats?.totalResource} · 难 ${fm.stats?.totalDifficulty}`,
                  ),
                ),
                h(
                  'button',
                  {
                    class: 'tree-action',
                    type: 'button',
                    title: '删除记录',
                    onclick: (e) => {
                      e.stopPropagation();
                      this.store.deleteFloorMap(fm.id);
                    },
                  },
                  icon('trash', 12),
                ),
              ),
            ),
          )
        : null;

    mount(
      this.leftEl,
      h('div', { class: 'panel-body' }, genSection, regionsSection, budgetSection, curveSection, historySection),
    );
  }

  /* ---------------- 中央工具栏、空态与状态栏 ---------------- */

  renderToolbar() {
    const ui = this.store.ui;
    const map = this.store.currentFloorMap;

    const toggles = [
      { key: 'floorShowGrid', label: '网格', icon: 'grid' },
      { key: 'floorShowRegions', label: '区域框', icon: 'eye' },
      { key: 'floorShowCells', label: '格子', icon: 'contents' },
      { key: 'floorShowLevels', label: '等级', icon: 'labels' },
      { key: 'floorShowGraph', label: '节点图', icon: 'layers' },
      { key: 'floorShowHeatmap', label: '距离热力', icon: 'spawn' },
    ];

    const zoomPct = `${Math.round(((this.canvas?.view?.scale || 34) / 34) * 100)}%`;

    mount(
      this.toolbarEl,
      h(
        'div',
        { class: 'tool-group' },
        h('button', { class: 'tool-btn', type: 'button', title: '缩小', onclick: () => this.canvas.zoomBy(1 / 1.2) }, icon('minus', 13)),
        h('button', { class: 'tool-btn', type: 'button', title: '放大', onclick: () => this.canvas.zoomBy(1.2) }, icon('plus', 13)),
        h('button', { class: 'tool-btn', type: 'button', title: '适应画布 (F)', onclick: () => this.canvas.fit() }, icon('fit', 13)),
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
              onclick: () => this.store.toggleUi(t.key),
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
                title: '导出单层地图 JSON',
                onclick: () => {
                  try {
                    downloadText(`${slugify(map.seed || map.id)}-floor-map.json`, JSON.stringify(makeFloorFile(map), null, 2));
                    toast('单层地图 JSON 已导出', { type: 'success' });
                  } catch (error) { toast(error.message, { type: 'error' }); }
                },
              },
              icon('export', 13),
              h('span', null, 'JSON'),
            ),
            h(
              'button',
              {
                class: 'tool-btn',
                type: 'button',
                title: '导出 2x PNG 预览图',
                onclick: async () => {
                  const blob = await this.canvas.toPngBlob({ scale: 2 });
                  if (!blob) return;
                  downloadBlob(`${slugify(map.seed)}-floor-map.png`, blob);
                  toast('单层地图 PNG 已导出', { type: 'success' });
                },
              },
              icon('download', 13),
              h('span', null, 'PNG'),
            ),
          )
        : null,
      h('span', { class: 'seed-label' }, 'SEED ', h('strong', null, map?.seed || '—')),
    );
  }

  renderEmptyState() {
    const hasMap = Boolean(this.store.currentFloorMap);
    this.emptyEl.hidden = hasMap;
    if (hasMap) return;
    mount(
      this.emptyEl,
      h(
        'div',
        { class: 'empty-card' },
        h('div', { class: 'eyebrow' }, 'SINGLE FLOOR / 三角单层地牢'),
        h('h2', null, '生成第一张类银河城单层地图'),
        h(
          'p',
          null,
          '单层地图由三个区域呈三角形构成：每个区域内由多格异形条带形成至少一条环路，区域间有且仅有一条独立桥接条带以门相连，并按节点图距出生点距离自动计算条带与敌人等级。',
        ),
        h(
          'div',
          { class: 'action-row', style: { marginTop: '14px' } },
          h(
            'button',
            {
              class: 'btn primary',
              type: 'button',
              onclick: () => this.runGenerate(),
            },
            icon('generate', 14),
            h('span', null, '立即生成单层地图'),
          ),
          h(
            'button',
            {
              class: 'btn',
              type: 'button',
              onclick: () => this.store.setMode('templates'),
            },
            '先编辑条带模板',
          ),
        ),
      ),
    );
  }

  renderStatusbar() {
    const map = this.store.currentFloorMap;
    if (!map) {
      mount(
        this.statusbarEl,
        h('span', { class: 'status-item' }, '尚未生成单层地图'),
        h('span', { class: 'spacer' }),
        h('span', { class: 'status-item' }, `模板库：${this.store.dungeon.stripTemplates.length} 个条带模板`),
      );
      return;
    }
    const st = map.stats;
    mount(
      this.statusbarEl,
      h(
        'span',
        { class: 'status-item' },
        h('span', { class: `status-dot ${map.errors.length === 0 ? 'ok' : 'err'}` }),
        map.errors.length === 0 ? '拓扑校验通过' : `${map.errors.length} 个拓扑错误`,
      ),
      h('span', { class: 'status-item' }, `条带 ${st.strips}（区 ${st.regionStripCount} + 桥 ${st.bridges}）`),
      h('span', { class: 'status-item' }, `门 ${st.doors} · 交叉 ${st.crossings}`),
      h('span', { class: 'status-item' }, `总资源 ${st.totalResource} · 总难度 ${st.totalDifficulty}`),
      h('span', { class: 'spacer' }),
      h('span', { class: 'status-item' }, `最大跳数 d=${st.maxDistance} · 条带 Lv.${st.minLevel}–${st.maxLevel} · 最高敌人 E${st.maxEnemyLevel}`),
    );
  }

  /* ---------------- 右侧：达标仪表盘 + 选中条带微调 ---------------- */

  renderRight() {
    const map = this.store.currentFloorMap;
    if (!map) {
      mount(
        this.rightEl,
        h(
          'div',
          { class: 'panel-body' },
          h(
            'div',
            { class: 'section' },
            h('div', { class: 'section-title' }, h('span', null, '单层地牢检视器')),
            h('p', { class: 'field-hint' }, '点击左侧「生成单层地图」后，这里将展示三角形拓扑达标情况、总资源/难度区间统计，以及选中条带的等级与各格敌人偏移微调。'),
          ),
        ),
      );
      return;
    }

    const st = map.stats;
    const target = map.budget.target;

    const badge = (ok, text) =>
      h('span', { class: `health-badge ${ok ? 'ok' : 'err'}` }, `${ok ? '✓' : '✕'} ${text}`);

    const budgetBar = (label, value, min, max, inRange, colorVar) => {
      const span = Math.max(1, max * 1.3);
      const valPct = clamp((value / span) * 100, 0, 100);
      const minPct = clamp((min / span) * 100, 0, 100);
      const maxPct = clamp((max / span) * 100, 0, 100);
      return h(
        'div',
        { class: 'budget-meter' },
        h(
          'div',
          { class: 'budget-meter-head' },
          h('span', null, label),
          h(
            'span',
            { class: `mono-val ${inRange ? 'ok' : 'warn'}` },
            `${value} / [${min}, ${max}]`,
          ),
        ),
        h(
          'div',
          { class: 'budget-track' },
          h('div', {
            class: 'budget-band',
            style: { left: `${minPct}%`, width: `${Math.max(2, maxPct - minPct)}%` },
          }),
          h('div', {
            class: 'budget-fill',
            style: { width: `${valPct}%`, background: inRange ? colorVar : 'var(--amber)' },
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
        badge(st.resourceInRange && st.difficultyInRange, '命中资源/难度区间'),
      ),
      budgetBar('总资源量', st.totalResource, target.resourceMin, target.resourceMax, st.resourceInRange, 'var(--green)'),
      budgetBar('总难度量', st.totalDifficulty, target.difficultyMin, target.difficultyMax, st.difficultyInRange, 'var(--red)'),
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
              `${r.strips}条 · ${r.cycles}环 · 非环${r.noLoopCount}/${r.maxNoLoop ?? '∞'} · 资${r.resource} · 难${r.difficulty}`,
            ),
          ),
        ),
        h(
          'div',
          { class: 'region-stat-row' },
          h('span', { class: 'region-stat-name' }, '连接头尾倾向'),
          h(
            'span',
            { class: 'region-stat-meta' },
            `${Math.round((st.avgConnectionEndFactor ?? 0.5) * 100)}%（0%=中段, 100%=头尾）`,
          ),
        ),
      ),
      map.issues.length > 0
        ? h(
            'div',
            { class: 'issue-list', style: { marginTop: '8px' } },
            ...map.issues.slice(0, 6).map((iss) =>
              h('div', { class: `issue ${iss.level}` }, iss.message),
            ),
          )
        : null,
    );

    // 选中条带检视与微调
    const selectedStrip = map.strips.find((s) => s.id === this.store.selectedStripId) || null;
    let stripSection;

    if (!selectedStrip) {
      stripSection = h(
        'div',
        { class: 'section' },
        h('div', { class: 'section-title' }, h('span', null, '选中条带详情')),
        h('p', { class: 'field-hint' }, '在画布中点击任意条带，可查看其跳数距离、条带等级、各格敌人等级，并可手工调整战斗格难度偏移或替换同形状模板。'),
      );
    } else {
      const tMap = cellTypeMap(this.store.dungeon.cellTypes);
      const region = map.regions.find((r) => r.id === selectedStrip.regionId);
      const roleTitle =
        selectedStrip.role === 'bridge'
          ? `独立跨区桥 (${(selectedStrip.bridgeBetween || []).map((id) => map.regions.find((r) => r.id === id)?.shortName || id).join(' ↔ ')})`
          : selectedStrip.role === 'spawn'
            ? `${region?.name || ''} · 出生条带`
            : region?.name || selectedStrip.regionId;

      // 查找可原地互换的同形状模板
      const compatibleTemplates = this.store.dungeon.stripTemplates.filter((tpl) => {
        if (tpl.role !== (selectedStrip.role === 'bridge' ? 'bridge' : 'normal')) return false;
        if (selectedStrip.role !== 'bridge' && !templateMatchesRegion(tpl, selectedStrip.regionId)) return false;
        if (selectedStrip.id === map.spawn?.stripId && !tpl.spawnEligible) return false;
        const m = computeTemplateMetrics(tpl, this.store.dungeon.cellTypes);
        return m.shapeSignature === selectedStrip.shapeSignature;
      });

      stripSection = h(
        'div',
        { class: 'section' },
        h(
          'div',
          { class: 'section-title' },
          h('span', null, `条带 ${selectedStrip.id} · ${selectedStrip.templateName}`),
          h(
            'button',
            {
              class: 'link-btn',
              type: 'button',
              onclick: () => this.store.selectFloorStrip(null),
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
          h('div', { class: 'stat' }, h('div', { class: 'stat-value', style: { color: 'var(--green)' } }, String(selectedStrip.resource)), h('div', { class: 'stat-label' }, '条带资源')),
          h('div', { class: 'stat' }, h('div', { class: 'stat-value', style: { color: 'var(--red)' } }, String(selectedStrip.difficulty)), h('div', { class: 'stat-label' }, '条带难度')),
        ),
        compatibleTemplates.length > 1
          ? h(
              'label',
              { class: 'mini-field', style: { marginTop: '10px' } },
              h('span', null, '原地替换为同形状模板'),
              h(
                'select',
                {
                  class: 'select',
                  onchange: (e) => {
                    const nextTpl = compatibleTemplates.find((t) => t.id === e.target.value);
                    if (!nextTpl) return;
                    this.swapStripTemplateInPlace(selectedStrip.id, nextTpl);
                  },
                },
                ...compatibleTemplates.map((tpl) => {
                  const m = computeTemplateMetrics(tpl, this.store.dungeon.cellTypes);
                  return h(
                    'option',
                    { value: tpl.id, selected: tpl.id === selectedStrip.templateId },
                    `${tpl.name} (资${m.resource} / 难${m.difficulty})`,
                  );
                }),
              ),
            )
          : null,
        h('div', { class: 'weight-grid-head', style: { marginTop: '10px' } }, `格子明细 (${selectedStrip.cells.length} 格)`),
        h(
          'div',
          { class: 'floor-cell-list' },
          ...selectedStrip.cells.map((cell) => {
            const def = tMap.get(cell.type) || tMap.get('empty');
            return h(
              'div',
              { class: 'floor-cell-row' },
              h('span', { class: 'palette-dot', style: { background: def.color } }),
              h('span', { class: 'floor-cell-name' }, `${def.icon} ${def.name}`),
              cell.isPortal ? h('span', { class: 'pill portal' }, '口') : null,
              h('span', { class: 'floor-cell-coord' }, `(${cell.x},${cell.y})`),
              def.supportsOffset
                ? h(
                    'div',
                    { class: 'inline-offset' },
                    h(
                      'button',
                      {
                        class: 'btn small',
                        type: 'button',
                        onclick: () =>
                          this.store.commitFloorMap(
                            (m) => {
                              const stp = m.strips.find((s) => s.id === selectedStrip.id);
                              const c = stp?.cells[cell.index];
                              if (c) c.difficultyOffset = clamp((c.difficultyOffset || 0) - 1, -10, 10);
                            },
                            { label: '调整战斗格难度偏移' },
                          ),
                      },
                      '-',
                    ),
                    h('span', { class: 'enemy-pill' }, `E${cell.enemyLevel} (${cell.difficultyOffset >= 0 ? '+' : ''}${cell.difficultyOffset})`),
                    h(
                      'button',
                      {
                        class: 'btn small',
                        type: 'button',
                        onclick: () =>
                          this.store.commitFloorMap(
                            (m) => {
                              const stp = m.strips.find((s) => s.id === selectedStrip.id);
                              const c = stp?.cells[cell.index];
                              if (c) c.difficultyOffset = clamp((c.difficultyOffset || 0) + 1, -10, 10);
                            },
                            { label: '调整战斗格难度偏移' },
                          ),
                      },
                      '+',
                    ),
                  )
                : h('span', { class: 'field-hint', style: { margin: 0 } }, '—'),
            );
          }),
        ),
      );
    }

    mount(this.rightEl, h('div', { class: 'panel-body' }, overviewSection, stripSection));
  }

  swapStripTemplateInPlace(stripId, nextTpl) {
    const cellTypes = this.store.dungeon.cellTypes;
    const metrics = computeTemplateMetrics(nextTpl, cellTypes);
    const variants = uniqueRotations(nextTpl.cells, nextTpl.allowRotate, { preserveContentOrientation: false });

    const changed = this.store.commitFloorMap(
      (map) => {
        const strip = map.strips.find((s) => s.id === stripId);
        if (!strip) return false;
        const targetKey = exactShapeKey(strip.cells);
        const match = variants.find((v) => v.shapeKey === targetKey && exactOpenEdgeKey(v.cells) === exactOpenEdgeKey(strip.cells));
        if (!match) return false;
        let minX = Infinity;
        let minY = Infinity;
        for (const c of strip.cells) {
          if (c.x < minX) minX = c.x;
          if (c.y < minY) minY = c.y;
        }
        const placed = placeCellsAt(match.cells, minX, minY);
        const byCoord = new Map(placed.map((c) => [`${c.x},${c.y}`, c]));
        strip.templateId = nextTpl.id;
        strip.templateName = nextTpl.name;
        strip.rotation = match.rotation;
        strip.resource = metrics.resource;
        strip.difficulty = metrics.difficulty;
        strip.cells = strip.cells.map((oldCell, idx) => {
          const hit = byCoord.get(`${oldCell.x},${oldCell.y}`);
          return {
            ...oldCell,
            index: idx,
            rx: hit ? hit.rx : oldCell.rx,
            ry: hit ? hit.ry : oldCell.ry,
            type: hit ? hit.type : oldCell.type,
            difficultyOffset: hit ? (hit.difficultyOffset || 0) : 0,
            enemyLevel: null,
          };
        });
        if (strip.id === map.spawn?.stripId) {
          const spIdx = Math.max(0, strip.cells.findIndex((c) => c.type === 'spawn'));
          map.spawn = {
            stripId: strip.id,
            cellIndex: spIdx,
            x: strip.cells[spIdx].x,
            y: strip.cells[spIdx].y,
          };
        }
      },
      { label: `替换条带模板为「${nextTpl.name}」` },
    );
    if (changed) toast(`已将 ${stripId} 替换为「${nextTpl.name}」`, { type: 'success' });
    else toast('该模板没有相同的占地和开放通路。', { type: 'warning' });
  }
}
