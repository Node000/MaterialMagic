/**
 * 模式一：条带模板编辑器（v2.1 大字号紧贴画板版）。
 *
 * - 左侧：全局格子配置入口、模板库列表（含多格微缩预览、非环/出入口徽标）、导入/导出；
 * - 中央：大号双层工具栏 + 醒目「总资源值(绿) / 总难度值(红)」高亮卡 + 放大 9×9 墙边画布（标注首/尾与出入口）+ 当前格精修条；
 * - 右侧：紧贴画布的随机单条草稿生成器 + 模板属性（含非环、四区限定：城市/工厂/郊区/太空、资源/难度覆写）。
 */

import {
  DEFAULT_CELL_TYPES,
  DIRECTIONS,
  DIR_IDS,
  DUNGEON_REGION_PRESETS,
  SHAPE_STYLES,
  STRIP_CELLS_MAX,
  STRIP_CELLS_MIN,
  STRIP_ROLES,
  clamp,
} from '../core/constants.js';
import {
  boundsOfCells,
  computeChainOrderMap,
  coordKey,
  normalizeCells,
  rotateCells,
} from '../core/polyomino.js';
import { appendStripCell, removeStripCell, toggleStripEdge } from '../core/strip-edges.js';
import {
  cellTypeMap,
  computeTemplateMetrics,
  createDefaultStripTemplates,
  defaultRandomDraftConfig,
  generateRandomStripDraft,
  normalizeStripTemplate,
  parseTemplateLibrary,
  serializeTemplateLibrary,
  validateStripTemplate,
} from '../core/strip-templates.js';
import {
  clear,
  downloadText,
  h,
  icon,
  mount,
  pickFile,
  readFileText,
  slugify,
} from './dom.js';
import { confirmDialog, openModal, toast } from './modals.js';

const GRID_SIZE = 9;
const clone = (v) => JSON.parse(JSON.stringify(v));

const PORTAL_MODES = [
  { id: 'none', label: '无（全格随机相连）' },
  { id: 'ends', label: '首尾双出入口' },
  { id: 'single_end', label: '仅首格单出入口' },
];

/** 生成条带多格形状的内联 SVG 缩略图。 */
function renderMiniPolyominoSvg(cells, tMap, size = 40) {
  const ns = 'http://www.w3.org/2000/svg';
  const svg = document.createElementNS(ns, 'svg');
  svg.setAttribute('width', String(size));
  svg.setAttribute('height', String(size));
  svg.setAttribute('viewBox', `0 0 ${size} ${size}`);
  svg.classList.add('tpl-mini-svg');

  const norm = normalizeCells(cells || []);
  if (!norm.length) return svg;
  const b = boundsOfCells(norm);
  const pad = 3;
  const span = Math.max(b.width, b.height, 3);
  const cellPx = (size - pad * 2) / span;
  const ox = (size - b.width * cellPx) / 2;
  const oy = (size - b.height * cellPx) / 2;

  for (const c of norm) {
    const def = tMap.get(c.type) || tMap.get('empty');
    const rect = document.createElementNS(ns, 'rect');
    rect.setAttribute('x', (ox + c.rx * cellPx + 0.6).toFixed(1));
    rect.setAttribute('y', (oy + c.ry * cellPx + 0.6).toFixed(1));
    rect.setAttribute('width', Math.max(2, cellPx - 1.2).toFixed(1));
    rect.setAttribute('height', Math.max(2, cellPx - 1.2).toFixed(1));
    rect.setAttribute('rx', '2');
    rect.setAttribute('fill', def?.color || '#6e7f91');
    rect.setAttribute('fill-opacity', c.type === 'empty' ? '0.45' : '0.92');
    if (c.isPortal) {
      rect.setAttribute('stroke', '#4de2d8');
      rect.setAttribute('stroke-width', '1.2');
    }
    svg.append(rect);
  }
  return svg;
}

/** 将归一化条带居中映射到 9×9 画布坐标 (gx, gy)。 */
function centerCellsOnGrid(cells) {
  const norm = normalizeCells(cells);
  const b = boundsOfCells(norm);
  const offsetX = Math.floor((GRID_SIZE - b.width) / 2);
  const offsetY = Math.floor((GRID_SIZE - b.height) / 2);
  return norm.map((c) => ({
    ...c,
    gx: clamp(c.rx + offsetX, 0, GRID_SIZE - 1),
    gy: clamp(c.ry + offsetY, 0, GRID_SIZE - 1),
  }));
}

export class TemplateEditorView {
  constructor({ root, store }) {
    this.root = root;
    this.store = store;
    this.filterText = '';
    this.filterRole = 'all';
    this.brushType = 'combat';
    this.toolMode = 'paint'; // 'paint' | 'select' | 'erase'
    this.selectedCellCoord = null;
    this.draft = null;
    this.draftIsNew = false;
    this.dirty = false;
    this.gridCells = [];
    this.randomConfig = defaultRandomDraftConfig(store.dungeon.cellTypes);

    this.loadTemplate(store.dungeon.stripTemplates[0] || null);
    this.buildShell();
    this.renderAll();

    store.on('change', () => {
      if (this.draft && !this.draftIsNew && !this.dirty) {
        const latest = this.store.dungeon.stripTemplates.find((t) => t.id === this.draft.id);
        if (latest) this.loadTemplate(latest);
        else this.loadTemplate(this.store.dungeon.stripTemplates[0] || null);
      }
      this.renderAll();
    });
  }

  buildShell() {
    this.leftEl = h('aside', { class: 'panel tpl-sidebar' });
    this.centerEl = h('section', { class: 'stage tpl-stage' });
    this.rightEl = h('aside', { class: 'panel tpl-inspector' });
    mount(this.root, this.leftEl, this.centerEl, this.rightEl);
  }

  loadTemplate(template, { isNew = false, dirty = false } = {}) {
    if (!template) {
      this.draft = normalizeStripTemplate(
        {
          id: `draft_${Date.now()}`,
          name: '新建链式条带',
          role: 'normal',
          noLoop: false,
          cells: [
            { rx: 0, ry: 0, type: 'empty', difficultyOffset: 0, isPortal: false },
            { rx: 1, ry: 0, type: 'combat', difficultyOffset: 0, isPortal: false },
            { rx: 2, ry: 0, type: 'empty', difficultyOffset: 0, isPortal: false },
            { rx: 2, ry: 1, type: 'combat', difficultyOffset: 1, isPortal: false },
            { rx: 2, ry: 2, type: 'reward', difficultyOffset: 0, isPortal: false },
          ],
        },
        this.store.dungeon.cellTypes,
      );
      this.draftIsNew = true;
      this.dirty = true;
    } else {
      this.draft = clone(normalizeStripTemplate(template, this.store.dungeon.cellTypes));
      this.draftIsNew = isNew;
      this.dirty = dirty;
    }
    this.gridCells = centerCellsOnGrid(this.draft.cells);
    const tMap = cellTypeMap(this.store.dungeon.cellTypes);
    const firstCombat = this.gridCells.find((c) => tMap.get(c.type)?.supportsOffset);
    const first = firstCombat || this.gridCells[0];
    this.selectedCellCoord = first ? coordKey(first.gx, first.gy) : null;
  }

  syncGridToDraft() {
    const normalized = normalizeCells(
      this.gridCells.map((c) => ({
        rx: c.gx,
        ry: c.gy,
        type: c.type,
        difficultyOffset: c.difficultyOffset || 0,
        isPortal: Boolean(c.isPortal),
        tags: c.tags || [],
        openDirs: c.openDirs,
        placedOrder: c.placedOrder,
      })),
    );
    this.draft.cells = normalized;
    if (normalized.some((c) => c.type === 'spawn') && this.draft.role !== 'bridge') {
      this.draft.spawnEligible = true;
      this.draft.noLoop = false;
    }
    this.dirty = true;
  }

  getSelectedGridCell() {
    if (!this.selectedCellCoord) return null;
    return this.gridCells.find((c) => coordKey(c.gx, c.gy) === this.selectedCellCoord) || null;
  }

  renderAll() {
    this.renderLeft();
    this.renderCenter();
    this.renderRight();
  }

  /* ---------------- 左侧：模板库与全局配置 ---------------- */

  renderLeft() {
    const { cellTypes, stripTemplates } = this.store.dungeon;
    const tMap = cellTypeMap(cellTypes);
    const regionName = new Map(DUNGEON_REGION_PRESETS.map((r) => [r.id, r.shortName || r.name]));

    const searchInput = h('input', {
      class: 'input',
      type: 'search',
      placeholder: '搜索模板名称 / 标签…',
      value: this.filterText,
      oninput: () => {
        this.filterText = searchInput.value;
        this.renderLeftList();
      },
    });

    const roleTabs = h(
      'div',
      { class: 'tpl-filter-bar' },
      ...[
        { id: 'all', label: `全部 (${stripTemplates.length})` },
        { id: 'normal', label: '常规' },
        { id: 'noloop', label: '非环' },
        { id: 'bridge', label: '桥接' },
        { id: 'portal', label: '出入口' },
      ].map((tab) =>
        h(
          'button',
          {
            class: `chip-btn${this.filterRole === tab.id ? ' active' : ''}`,
            type: 'button',
            onclick: () => {
              this.filterRole = tab.id;
              this.renderLeft();
            },
          },
          tab.label,
        ),
      ),
    );

    const actions = h(
      'div',
      { class: 'tpl-left-actions' },
      h(
        'button',
        {
          class: 'btn primary',
          type: 'button',
          onclick: () => {
            this.loadTemplate(null);
            this.renderAll();
            toast('已新建 5 格链式草稿，可在画布精修后保存', { type: 'info' });
          },
        },
        icon('plus', 14),
        h('span', null, '新建模板'),
      ),
      h(
        'button',
        {
          class: 'btn',
          type: 'button',
          onclick: () => this.openCellConfigModal(),
        },
        icon('sliders', 14),
        h('span', null, '全局格子配置'),
      ),
      h(
        'button',
        {
          class: 'btn',
          type: 'button',
          title: '导入条带模板库 JSON',
          onclick: () => this.importTemplates(),
        },
        icon('import', 14),
        h('span', null, '导入 JSON'),
      ),
      h(
        'button',
        {
          class: 'btn',
          type: 'button',
          title: '导出条带模板库 JSON',
          onclick: () => this.exportTemplates(),
        },
        icon('export', 14),
        h('span', null, '导出 JSON'),
      ),
    );

    this.leftListEl = h('div', { class: 'panel-body tpl-list' });
    mount(
      this.leftEl,
      h('div', { class: 'tpl-left-head' }, actions, searchInput, roleTabs),
      this.leftListEl,
    );
    this.renderLeftList(tMap, regionName);
  }

  renderLeftList(tMapArg = null, regionNameArg = null) {
    if (!this.leftListEl) return;
    const { cellTypes, stripTemplates } = this.store.dungeon;
    const tMap = tMapArg || cellTypeMap(cellTypes);
    const regionName = regionNameArg || new Map(DUNGEON_REGION_PRESETS.map((r) => [r.id, r.shortName || r.name]));
    const q = this.filterText.trim().toLowerCase();

    const filtered = stripTemplates.filter((tpl) => {
      if (this.filterRole === 'normal' && tpl.role !== 'normal') return false;
      if (this.filterRole === 'bridge' && tpl.role !== 'bridge') return false;
      if (this.filterRole === 'noloop' && !tpl.noLoop) return false;
      if (this.filterRole === 'portal' && !tpl.cells.some((c) => c.isPortal)) return false;
      if (q) {
        const hay = `${tpl.name} ${tpl.id} ${(tpl.tags || []).join(' ')}`.toLowerCase();
        if (!hay.includes(q)) return false;
      }
      return true;
    });

    clear(this.leftListEl);
    if (!filtered.length) {
      this.leftListEl.append(h('div', { class: 'field-hint', style: { padding: '16px' } }, '没有匹配的条带模板。'));
      return;
    }

    for (const tpl of filtered) {
      const m = computeTemplateMetrics(tpl, cellTypes);
      const isActive = !this.draftIsNew && this.draft?.id === tpl.id;
      const scopeLabel =
        tpl.role === 'bridge'
          ? '独立桥'
          : tpl.regionIds.length === 0
            ? '四区通用'
            : tpl.regionIds.map((id) => regionName.get(id) || id).join('/');

      const card = h(
        'div',
        {
          class: `tpl-card${isActive ? ' active' : ''}`,
          role: 'button',
          tabindex: '0',
          onclick: () => {
            this.loadTemplate(tpl);
            this.renderAll();
          },
        },
        renderMiniPolyominoSvg(tpl.cells, tMap, 40),
        h(
          'div',
          { class: 'tpl-card-main' },
          h(
            'div',
            { class: 'tpl-card-title' },
            h('span', { class: 'tpl-card-name' }, tpl.name),
            tpl.role === 'bridge' ? h('span', { class: 'pill bridge' }, '桥') : null,
            tpl.spawnEligible ? h('span', { class: 'pill spawn' }, '起') : null,
            tpl.noLoop ? h('span', { class: 'pill noloop' }, '非环') : null,
            m.portalCount > 0 ? h('span', { class: 'pill portal' }, `口${m.portalCount}`) : null,
          ),
          h('div', { class: 'tpl-card-sub' }, `${m.shapeKind.label} · ${scopeLabel}`),
          h(
            'div',
            { class: 'tpl-card-metrics' },
            h('span', { class: 'metric-badge-r' }, `资源 ${m.resource}${m.isResourceOverridden ? '*' : ''}`),
            h('span', { class: 'metric-badge-d' }, `难度 ${m.difficulty}${m.isDifficultyOverridden ? '*' : ''}`),
          ),
        ),
        h(
          'div',
          { class: 'tpl-card-ops' },
          h(
            'button',
            {
              class: 'tree-action',
              type: 'button',
              title: '复制模板',
              onclick: (event) => {
                event.stopPropagation();
                const copy = clone(tpl);
                copy.name = `${tpl.name} 副本`;
                const saved = this.store.saveStripTemplate(copy, { asNew: true });
                this.loadTemplate(saved);
                this.renderAll();
                toast(`已复制「${saved.name}」`, { type: 'success' });
              },
            },
            icon('copy', 13),
          ),
          h(
            'button',
            {
              class: 'tree-action',
              type: 'button',
              title: '删除模板',
              onclick: async (event) => {
                event.stopPropagation();
                if (stripTemplates.length <= 1) {
                  toast('至少需要保留 1 个条带模板', { type: 'warning' });
                  return;
                }
                const ok = await confirmDialog({
                  title: '删除条带模板',
                  message: `确定删除「${tpl.name}」？此操作可撤销。`,
                  confirmText: '删除',
                  danger: true,
                });
                if (!ok) return;
                this.store.deleteStripTemplate(tpl.id);
                if (this.draft?.id === tpl.id) {
                  this.loadTemplate(this.store.dungeon.stripTemplates[0] || null);
                }
                this.renderAll();
                toast('条带模板已删除', { type: 'success' });
              },
            },
            icon('trash', 13),
          ),
        ),
      );
      this.leftListEl.append(card);
    }
  }

  /* ---------------- 中央：大号双层工具栏 + 醒目资源/难度卡 + 9×9 单链画布 ---------------- */

  renderCenter() {
    const { cellTypes } = this.store.dungeon;
    const tMap = cellTypeMap(cellTypes);
    const metrics = computeTemplateMetrics(this.draft, cellTypes);
    const check = validateStripTemplate(this.draft, cellTypes);

    // 顶部大号双层工具栏
    const topActionRow = h(
      'div',
      { class: 'tpl-tool-row' },
      h(
        'div',
        { class: 'tool-group' },
        h(
          'button',
          {
            class: `tool-btn lg${this.toolMode === 'select' ? ' active' : ''}`,
            type: 'button',
            title: '选择格（调整偏移与出入口）',
            onclick: () => {
              this.toolMode = 'select';
              this.renderCenter();
            },
          },
          icon('cursor', 15),
          h('span', null, '选择格'),
        ),
        h(
          'button',
          {
            class: `tool-btn lg${this.toolMode === 'erase' ? ' active' : ''}`,
            type: 'button',
            title: '擦除格（保留开放通路的连通性，也可右键擦除）',
            onclick: () => {
              this.toolMode = 'erase';
              this.renderCenter();
            },
          },
          icon('trash', 15),
          h('span', null, '擦除格'),
        ),
        h('button', {
          class: `tool-btn lg${this.toolMode === 'wall' ? ' active' : ''}`,
          type: 'button',
          title: '点击相邻格之间的边来开墙或加墙',
          onclick: () => { this.toolMode = 'wall'; this.renderCenter(); },
        }, '墙体工具'),
      ),
      h(
        'button',
        {
          class: 'btn',
          type: 'button',
          title: '顺时针旋转 90°',
          onclick: () => {
            const rotated = rotateCells(this.draft.cells, 1);
            this.draft.cells = normalizeCells(rotated);
            this.gridCells = centerCellsOnGrid(this.draft.cells);
            this.dirty = true;
            this.renderAll();
          },
        },
        '↻ 旋转 90°',
      ),
      h('span', { class: 'toolbar-spacer' }),
      h(
        'button',
        {
          class: 'btn primary',
          type: 'button',
          onclick: () => this.saveCurrentDraft({ asNew: false }),
        },
        this.draftIsNew ? '💾 保存为模板' : '💾 保存修改',
      ),
      !this.draftIsNew
        ? h(
            'button',
            {
              class: 'btn',
              type: 'button',
              onclick: () => this.saveCurrentDraft({ asNew: true }),
            },
            '另存新模板',
          )
        : null,
    );

    const brushRow = h(
      'div',
      { class: 'tpl-palette lg' },
      ...cellTypes.map((ct) =>
        h(
          'button',
          {
            class: `palette-btn lg${this.toolMode === 'paint' && this.brushType === ct.id ? ' active' : ''}`,
            type: 'button',
            style: { '--brush-color': ct.color },
            title: `${ct.name}（资源 ${ct.resource} / 难度 ${ct.difficulty}）`,
            onclick: () => {
              this.toolMode = 'paint';
              this.brushType = ct.id;
              this.renderCenter();
            },
          },
          h('span', { class: 'palette-icon lg', style: { color: ct.color } }, ct.icon),
          h(
            'span',
            { class: 'palette-copy' },
            h('strong', null, ct.name),
            h('small', null, `资${ct.resource} · 难${ct.difficulty}`),
          ),
        ),
      ),
    );

    const toolbarWrap = h('div', { class: 'tpl-toolbar-double' }, topActionRow, brushRow);

    // 紧贴画布正上方的「总资源值 / 总难度值」专属高亮仪表卡
    const metricsHero = h(
      'div',
      { class: 'tpl-metrics-hero' },
      h(
        'div',
        { class: 'metric-hero-card resource' },
        h('span', { class: 'metric-hero-label' }, '条带资源值'),
        h('strong', { class: 'metric-hero-val' }, String(metrics.resource)),
        h(
          'span',
          { class: 'metric-hero-sub' },
          metrics.isResourceOverridden ? `手动覆写（自动 ${metrics.autoResource}）` : '由全部格子自动求和',
        ),
      ),
      h(
        'div',
        { class: 'metric-hero-card difficulty' },
        h('span', { class: 'metric-hero-label' }, '条带难度值'),
        h('strong', { class: 'metric-hero-val' }, String(metrics.difficulty)),
        h(
          'span',
          { class: 'metric-hero-sub' },
          metrics.isDifficultyOverridden ? `手动覆写（自动 ${metrics.autoDifficulty}）` : '由全部格子自动求和',
        ),
      ),
      h(
        'div',
        { class: 'metric-hero-meta' },
        h('div', { class: 'metric-hero-title' }, this.draft.name),
        h(
          'div',
          { class: 'metric-hero-tags' },
          h('span', { class: 'pill' }, metrics.shapeKind.label),
          this.draft.role === 'bridge' ? h('span', { class: 'pill bridge' }, '独立跨区桥') : null,
          this.draft.noLoop ? h('span', { class: 'pill noloop' }, '非环端点') : null,
          metrics.portalCount > 0
            ? h('span', { class: 'pill portal' }, `出入口 ${metrics.portalCount} 格`)
            : h('span', { class: 'pill' }, '全格自由连接'),
        ),
      ),
    );

    // 9×9 网格：占地和开放通路分别绘制。
    const cellByKey = new Map(this.gridCells.map((c) => [coordKey(c.gx, c.gy), c]));
    const pseudoChain = this.gridCells.map((c) => ({ rx: c.gx, ry: c.gy, openDirs: c.openDirs }));
    const orderMap = computeChainOrderMap(pseudoChain);
    const orderByKey = new Map();
    this.gridCells.forEach((c, idx) => {
      orderByKey.set(coordKey(c.gx, c.gy), orderMap.get(idx));
    });

    const gridEl = h('div', {
      class: 'tpl-grid lg',
      style: { gridTemplateColumns: `repeat(${GRID_SIZE}, 1fr)` },
    });

    for (let gy = 0; gy < GRID_SIZE; gy += 1) {
      for (let gx = 0; gx < GRID_SIZE; gx += 1) {
        const key = coordKey(gx, gy);
        const existing = cellByKey.get(key);
        const isSelected = this.selectedCellCoord === key;

        if (existing) {
          const def = tMap.get(existing.type) || tMap.get('empty');
          const offset = existing.difficultyOffset || 0;
          const chainInfo = orderByKey.get(key);
          const endBadge =
            this.gridCells.length >= 2 && chainInfo
              ? chainInfo.isHead
                ? '首'
                : chainInfo.isTail
                  ? '尾'
                  : null
              : null;

          const tile = h(
            'button',
            {
              class: `tpl-cell lg occupied${isSelected ? ' selected' : ''}${existing.isPortal ? ' is-portal' : ''}`,
              type: 'button',
              style: {
                '--cell-color': def.color,
                background: `${def.color}30`,
                borderColor: isSelected ? 'var(--amber)' : existing.isPortal ? '#4de2d8' : `${def.color}cc`,
              },
              title: `${def.name}（资 ${def.resource} / 难 ${def.difficulty}）${existing.isPortal ? ' · 出入口格' : ''} · 左键操作 / 右键擦除`,
              onclick: () => this.onClickCell(gx, gy),
              oncontextmenu: (event) => {
                event.preventDefault();
                this.tryRemoveGridCell(gx, gy);
              },
            },
            endBadge ? h('span', { class: 'tpl-cell-end' }, endBadge) : null,
            existing.isPortal ? h('span', { class: 'tpl-cell-portal', title: '出入口格' }, '口') : null,
            h('span', { class: 'tpl-cell-icon lg', style: { color: def.color } }, def.icon),
            h('span', { class: 'tpl-cell-label lg' }, def.id === 'empty' ? '通道' : def.name),
            def.supportsOffset && offset !== 0
              ? h(
                  'span',
                  { class: `tpl-cell-offset ${offset > 0 ? 'pos' : 'neg'}` },
                  `${offset > 0 ? '+' : ''}${offset}`,
                )
              : null,
          );
          const sides = DIR_IDS.flatMap((dir) => {
            const { dx, dy } = DIRECTIONS[dir];
            const shared = cellByKey.has(coordKey(gx + dx, gy + dy));
            const open = existing.openDirs?.includes(dir);
            const overlays = open ? [h('span', { class: `tpl-passage tpl-side-${dir}`, 'aria-hidden': 'true' })] : [];
            if (!shared || dir === 'E' || dir === 'S') {
              const interactive = shared && this.toolMode === 'wall';
              overlays.push(h(interactive ? 'button' : 'span', {
                class: `tpl-edge tpl-side-${dir}${open ? ' open' : ''}${interactive ? ' editable' : ''}`,
                ...(interactive ? { type: 'button', title: open ? '添加墙壁' : '拆除墙壁',
                  'aria-label': `${open ? '添加' : '拆除'} (${gx},${gy}) ${dir} 墙壁`,
                  onclick: () => this.onToggleWall(gx, gy, dir) } : { 'aria-hidden': 'true' }),
              }));
            }
            return overlays;
          });
          gridEl.append(h('div', { class: 'tpl-edge-cell' }, tile, ...sides));
        } else {
          // 仅开放能保持「无矩形单链」的相邻空格（明确首尾，绝不会误画出矩形/分叉）
          const canAdd =
            this.gridCells.length === 0 ||
            (this.gridCells.length < STRIP_CELLS_MAX && this.gridCells.some((c) => Math.abs(c.gx - gx) + Math.abs(c.gy - gy) === 1));
          const tile = h(
            'button',
            {
              class: `tpl-cell lg empty-slot${canAdd ? ' addable' : ''}`,
              type: 'button',
              disabled: !canAdd,
              title: canAdd ? '新格只自动连通放置最晚的邻格' : '新格须与当前条带贴边',
              onclick: () => this.onAddGridCell(gx, gy),
            },
            canAdd ? h('span', { class: 'tpl-add-plus' }, '+') : null,
          );
          gridEl.append(tile);
        }
      }
    }

    const banner =
      this.draftIsNew || this.dirty
        ? h(
            'div',
            { class: `tpl-status-banner${this.draftIsNew ? ' draft' : ''}` },
            h(
              'span',
              null,
              this.draftIsNew
                ? '✨ 随机/新建草稿尚未写入模板库 —— 可精修后点击右侧「立即保存」'
                : '● 当前模板有未保存修改',
            ),
            h(
              'button',
              {
                class: 'btn small primary',
                type: 'button',
                onclick: () => this.saveCurrentDraft({ asNew: false }),
              },
              '立即保存',
            ),
          )
        : null;

    // 紧贴画布正下方的「当前选中格精修条」（类型 + 难度偏移 + 出入口格开关）
    const selectedCell = this.getSelectedGridCell();
    const selectedDef = selectedCell ? tMap.get(selectedCell.type) || tMap.get('empty') : null;
    const cellInspectorBar = h(
      'div',
      { class: 'tpl-cell-bar lg' },
      selectedCell && selectedDef
        ? [
            h('span', { class: 'tpl-cell-bar-title' }, `选中格 (${selectedCell.gx},${selectedCell.gy})`),
            h(
              'select',
              {
                class: 'select small-select',
                onchange: (e) => {
                  const nextType = e.target.value;
                  const nextDef = tMap.get(nextType);
                  selectedCell.type = nextType;
                  selectedCell.difficultyOffset = nextDef?.supportsOffset ? (nextDef.defaultOffset || 0) : 0;
                  this.syncGridToDraft();
                  this.renderAll();
                },
              },
              ...cellTypes.map((ct) =>
                h('option', { value: ct.id, selected: ct.id === selectedCell.type }, `${ct.icon} ${ct.name}（资${ct.resource}/难${ct.difficulty}）`),
              ),
            ),
            h(
              'label',
              {
                class: `portal-toggle-btn${selectedCell.isPortal ? ' active' : ''}`,
                title: '若条带存在任意出入口格，则生成时只会在出入口格处与其他条带相连（门与交叉均受此限制）',
              },
              h('input', {
                type: 'checkbox',
                checked: Boolean(selectedCell.isPortal),
                onchange: (e) => {
                  selectedCell.isPortal = e.target.checked;
                  this.syncGridToDraft();
                  this.renderAll();
                },
              }),
              h('span', null, '🚪 设为出入口格'),
            ),
            selectedDef.supportsOffset
              ? h(
                  'div',
                  { class: 'offset-stepper' },
                  h('span', { class: 'muted-label' }, '敌人等级偏移'),
                  h(
                    'button',
                    {
                      class: 'btn small',
                      type: 'button',
                      onclick: () => {
                        selectedCell.difficultyOffset = clamp((selectedCell.difficultyOffset || 0) - 1, -10, 10);
                        this.syncGridToDraft();
                        this.renderAll();
                      },
                    },
                    '-',
                  ),
                  h(
                    'span',
                    { class: 'offset-val' },
                    `${selectedCell.difficultyOffset > 0 ? '+' : ''}${selectedCell.difficultyOffset || 0}`,
                  ),
                  h(
                    'button',
                    {
                      class: 'btn small',
                      type: 'button',
                      onclick: () => {
                        selectedCell.difficultyOffset = clamp((selectedCell.difficultyOffset || 0) + 1, -10, 10);
                        this.syncGridToDraft();
                        this.renderAll();
                      },
                    },
                    '+',
                  ),
                )
              : null,
          ]
        : h('span', { class: 'field-hint', style: { margin: 0 } }, '点击画布中任意格子，即可修改类型、设置出入口格或调整敌人等级偏移'),
    );

    const summaryBar = h(
      'footer',
      { class: 'statusbar tpl-statusbar' },
      check.ok
        ? h('span', { class: 'status-item' }, h('span', { class: 'status-dot ok' }), '开放通路连通性校验通过')
        : h('span', { class: 'status-item' }, h('span', { class: 'status-dot err' }), check.errors[0]?.message || '校验未通过'),
      h('span', { class: 'spacer' }),
      h('span', { class: 'status-item' }, '「+」添加相邻格 · 墙体工具编辑共享边 · 右键擦除'),
    );

    mount(
      this.centerEl,
      toolbarWrap,
      h(
        'div',
        { class: 'tpl-canvas-wrap compact' },
        banner,
        metricsHero,
        h('div', { class: 'tpl-grid-stage' }, gridEl),
        cellInspectorBar,
      ),
      summaryBar,
    );
  }

  onAddGridCell(gx, gy) {
    if (this.gridCells.length >= STRIP_CELLS_MAX) {
      toast(`单条条带最多 ${STRIP_CELLS_MAX} 格`, { type: 'warning' });
      return;
    }
    const tMap = cellTypeMap(this.store.dungeon.cellTypes);
    const typeId = this.toolMode === 'paint' ? this.brushType : 'empty';
    const def = tMap.get(typeId) || tMap.get('empty');
    const next = appendStripCell(this.gridCells.map((c) => ({ ...c, rx: c.gx, ry: c.gy })), {
      rx: gx, ry: gy,
      gx,
      gy,
      type: def.id,
      difficultyOffset: def.supportsOffset ? (def.defaultOffset || 0) : 0,
      isPortal: false,
      tags: [],
    });
    if (!next) return;
    this.gridCells = next;
    this.selectedCellCoord = coordKey(gx, gy);
    this.syncGridToDraft();
    this.renderAll();
  }

  onToggleWall(gx, gy, dir) {
    const next = toggleStripEdge(this.gridCells.map((c) => ({ ...c, rx: c.gx, ry: c.gy })), gx, gy, dir);
    if (!next) {
      toast('这面墙会切断条带，请先开通替代路线。', { type: 'warning' });
      return;
    }
    this.gridCells = next;
    this.syncGridToDraft();
    this.renderAll();
  }

  onClickCell(gx, gy) {
    const key = coordKey(gx, gy);
    if (this.toolMode === 'erase') {
      this.tryRemoveGridCell(gx, gy);
      return;
    }
    const cell = this.gridCells.find((c) => coordKey(c.gx, c.gy) === key);
    if (!cell) return;
    this.selectedCellCoord = key;
    if (this.toolMode === 'paint') {
      const tMap = cellTypeMap(this.store.dungeon.cellTypes);
      const def = tMap.get(this.brushType) || tMap.get('empty');
      cell.type = def.id;
      cell.difficultyOffset = def.supportsOffset ? (def.defaultOffset || 0) : 0;
      this.syncGridToDraft();
    }
    this.renderAll();
  }

  tryRemoveGridCell(gx, gy) {
    if (this.gridCells.length <= STRIP_CELLS_MIN) {
      toast('条带至少需要保留 1 个格子', { type: 'warning' });
      return;
    }
    const next = removeStripCell(this.gridCells.map((c) => ({ ...c, rx: c.gx, ry: c.gy })), gx, gy);
    if (!next) {
      toast('擦除这个格会切断条带的开放通路。', { type: 'warning' });
      return;
    }
    this.gridCells = next;
    if (this.selectedCellCoord === coordKey(gx, gy)) {
      const first = this.gridCells[0];
      this.selectedCellCoord = first ? coordKey(first.gx, first.gy) : null;
    }
    this.syncGridToDraft();
    this.renderAll();
  }

  saveCurrentDraft({ asNew = false } = {}) {
    const { cellTypes } = this.store.dungeon;
    const check = validateStripTemplate(this.draft, cellTypes);
    if (!check.ok) {
      toast(`保存失败：${check.errors[0]?.message}`, { type: 'error' });
      return false;
    }
    const saved = this.store.saveStripTemplate(this.draft, { asNew: asNew || this.draftIsNew });
    if (saved) {
      this.loadTemplate(saved, { isNew: false, dirty: false });
      this.renderAll();
      toast(`已保存条带模板「${saved.name}」`, { type: 'success' });
    }
    return Boolean(saved);
  }

  /* ---------------- 右侧：随机草稿生成器 + 模板属性 ---------------- */

  renderRight() {
    const { cellTypes } = this.store.dungeon;
    const metrics = computeTemplateMetrics(this.draft, cellTypes);
    const cfg = this.randomConfig;

    // 1. 随机生成草稿配置卡片
    const randomCard = h(
      'div',
      { class: 'section' },
      h('div', { class: 'section-title' }, h('span', null, '🎲 随机生成草稿配置')),
      h(
        'div',
        { class: 'kv-form' },
        h(
          'div',
          { class: 'form-row-2' },
          h(
            'label',
            { class: 'mini-field' },
            h('span', null, '最少格数'),
            h('input', {
              class: 'input mono-input',
              type: 'number',
              min: '1',
              max: String(STRIP_CELLS_MAX),
              value: String(cfg.minCells),
              onchange: (e) => {
                cfg.minCells = clamp(Math.round(Number(e.target.value) || 4), 1, STRIP_CELLS_MAX);
                if (cfg.maxCells < cfg.minCells) cfg.maxCells = cfg.minCells;
                this.renderRight();
              },
            }),
          ),
          h(
            'label',
            { class: 'mini-field' },
            h('span', null, '最多格数'),
            h('input', {
              class: 'input mono-input',
              type: 'number',
              min: String(cfg.minCells),
              max: String(STRIP_CELLS_MAX),
              value: String(cfg.maxCells),
              onchange: (e) => {
                cfg.maxCells = clamp(Math.round(Number(e.target.value) || cfg.minCells), cfg.minCells, STRIP_CELLS_MAX);
                this.renderRight();
              },
            }),
          ),
        ),
        h(
          'div',
          { class: 'form-row-2' },
          h(
            'label',
            { class: 'mini-field' },
            h('span', null, '形状风格（无矩形）'),
            h(
              'select',
              {
                class: 'select',
                onchange: (e) => {
                  cfg.shapeStyle = e.target.value;
                },
              },
              ...SHAPE_STYLES.map((s) => h('option', { value: s.id, selected: cfg.shapeStyle === s.id }, s.label)),
            ),
          ),
          h(
            'label',
            { class: 'mini-field' },
            h('span', null, '模板角色'),
            h(
              'select',
              {
                class: 'select',
                onchange: (e) => {
                  cfg.role = e.target.value;
                  if (cfg.role === 'bridge') cfg.noLoop = false;
                  this.renderRight();
                },
              },
              ...STRIP_ROLES.map((r) => h('option', { value: r.id, selected: cfg.role === r.id }, r.short)),
            ),
          ),
        ),
        h(
          'div',
          { class: 'form-row-2' },
          h(
            'label',
            { class: 'mini-field' },
            h('span', null, '出入口格预设'),
            h(
              'select',
              {
                class: 'select',
                onchange: (e) => {
                  cfg.portalMode = e.target.value;
                },
              },
              ...PORTAL_MODES.map((m) => h('option', { value: m.id, selected: (cfg.portalMode || 'none') === m.id }, m.label)),
            ),
          ),
          h(
            'div',
            { class: 'check-stack' },
            h(
              'label',
              { class: 'checkbox-row' },
              h('input', {
                type: 'checkbox',
                checked: Boolean(cfg.noLoop),
                disabled: cfg.role === 'bridge',
                onchange: (e) => {
                  cfg.noLoop = e.target.checked;
                },
              }),
              h('span', null, '生成为非环条带'),
            ),
          ),
        ),
        h(
          'div',
          { class: 'form-row-2' },
          h(
            'label',
            { class: 'mini-field' },
            h('span', null, '敌人偏移下限'),
            h('input', {
              class: 'input mono-input',
              type: 'number',
              min: '-5',
              max: '5',
              value: String(cfg.offsetMin),
              onchange: (e) => {
                cfg.offsetMin = clamp(Math.round(Number(e.target.value) || 0), -5, 5);
                if (cfg.offsetMax < cfg.offsetMin) cfg.offsetMax = cfg.offsetMin;
              },
            }),
          ),
          h(
            'label',
            { class: 'mini-field' },
            h('span', null, '敌人偏移上限'),
            h('input', {
              class: 'input mono-input',
              type: 'number',
              min: '-5',
              max: '5',
              value: String(cfg.offsetMax),
              onchange: (e) => {
                cfg.offsetMax = clamp(Math.round(Number(e.target.value) || 0), cfg.offsetMin, 5);
              },
            }),
          ),
        ),
        h('div', { class: 'weight-grid-head' }, '各格子类型抽取权重'),
        h(
          'div',
          { class: 'weight-grid' },
          ...cellTypes
            .filter((ct) => ct.id !== 'spawn')
            .map((ct) =>
              h(
                'label',
                { class: 'weight-chip' },
                h('span', { class: 'palette-dot', style: { background: ct.color } }),
                h('span', { class: 'weight-chip-name' }, ct.id === 'empty' ? '通道' : ct.name),
                h('input', {
                  class: 'input weight-input',
                  type: 'number',
                  min: '0',
                  max: '100',
                  value: String(cfg.weights[ct.id] ?? 0),
                  onchange: (e) => {
                    cfg.weights[ct.id] = Math.max(0, Math.round(Number(e.target.value) || 0));
                  },
                }),
              ),
            ),
        ),
        h(
          'button',
          {
            class: 'btn primary full-width',
            type: 'button',
            style: { marginTop: '8px' },
            onclick: () => {
              const draft = generateRandomStripDraft(cfg, cellTypes);
              this.loadTemplate(draft, { isNew: true, dirty: true });
              this.renderAll();
              toast(`已生成随机草稿「${draft.name}」（${draft.cells.length} 格），精修后点击保存`, { type: 'info' });
            },
          },
          icon('generate', 15),
          h('span', null, '随机生成条带草稿'),
        ),
      ),
    );

    // 2. 当前模板属性与资源/难度覆写
    const propCard = h(
      'div',
      { class: 'section' },
      h('div', { class: 'section-title' }, h('span', null, '当前模板属性')),
      h(
        'div',
        { class: 'kv-form' },
        h(
          'label',
          { class: 'mini-field' },
          h('span', null, '模板名称'),
          h('input', {
            class: 'input',
            type: 'text',
            value: this.draft.name,
            oninput: (e) => {
              this.draft.name = e.target.value;
              this.dirty = true;
            },
          }),
        ),
        h(
          'div',
          { class: 'form-row-2' },
          h(
            'label',
            { class: 'mini-field' },
            h('span', null, '条带角色'),
            h(
              'select',
              {
                class: 'select',
                onchange: (e) => {
                  this.draft.role = e.target.value;
                  if (this.draft.role === 'bridge') {
                    this.draft.spawnEligible = false;
                    this.draft.noLoop = false;
                  }
                  this.dirty = true;
                  this.renderAll();
                },
              },
              ...STRIP_ROLES.map((r) => h('option', { value: r.id, selected: this.draft.role === r.id }, r.label)),
            ),
          ),
          h(
            'label',
            { class: 'mini-field' },
            h('span', null, '生成权重 / 上限'),
            h(
              'div',
              { class: 'form-row-2' },
              h('input', {
                class: 'input mono-input',
                type: 'number',
                step: '0.1',
                min: '0.1',
                max: '20',
                title: '生成权重',
                value: String(this.draft.generation.weight),
                onchange: (e) => {
                  this.draft.generation.weight = Math.max(0.1, Number(e.target.value) || 1);
                  this.dirty = true;
                },
              }),
              h('input', {
                class: 'input mono-input',
                type: 'number',
                min: '0',
                max: '99',
                title: '单层出现上限',
                value: String(this.draft.generation.max),
                onchange: (e) => {
                  this.draft.generation.max = clamp(Math.round(Number(e.target.value) || 0), 0, 99);
                  this.dirty = true;
                },
              }),
            ),
          ),
        ),
        h(
          'div',
          { class: 'prop-check-grid' },
          h(
            'label',
            { class: 'checkbox-row' },
            h('input', {
              type: 'checkbox',
              checked: this.draft.allowRotate,
              onchange: (e) => {
                this.draft.allowRotate = e.target.checked;
                this.dirty = true;
              },
            }),
            h('span', null, '允许旋转'),
          ),
          h(
            'label',
            { class: 'checkbox-row' },
            h('input', {
              type: 'checkbox',
              checked: this.draft.spawnEligible,
              disabled: this.draft.role === 'bridge',
              onchange: (e) => {
                this.draft.spawnEligible = e.target.checked;
                if (this.draft.spawnEligible) this.draft.noLoop = false;
                this.dirty = true;
                this.renderAll();
              },
            }),
            h('span', null, '可作出生条带'),
          ),
          h(
            'label',
            {
              class: 'checkbox-row',
              title: '勾选后该条带不会参与成环，仅作为端点性质的支路条带',
            },
            h('input', {
              type: 'checkbox',
              checked: Boolean(this.draft.noLoop),
              disabled: this.draft.role === 'bridge' || this.draft.spawnEligible,
              onchange: (e) => {
                this.draft.noLoop = e.target.checked;
                this.dirty = true;
                this.renderAll();
              },
            }),
            h('span', null, '非环（仅作端点支路）'),
          ),
        ),

        // 适用区域：城市 / 工厂 / 郊区 / 太空（全不勾 = 四区通用）
        this.draft.role !== 'bridge'
          ? h(
              'div',
              { class: 'mini-field' },
              h(
                'span',
                null,
                `适用区域（${this.draft.regionIds.length === 0 ? '四区通用' : `限定 ${this.draft.regionIds.length} 区`}）`,
              ),
              h(
                'div',
                { class: 'region-check-row' },
                ...DUNGEON_REGION_PRESETS.map((r) => {
                  const checked = this.draft.regionIds.includes(r.id);
                  return h(
                    'label',
                    { class: `region-pill-check${checked ? ' checked' : ''}` },
                    h('input', {
                      type: 'checkbox',
                      checked,
                      onchange: (e) => {
                        if (e.target.checked) {
                          if (!this.draft.regionIds.includes(r.id)) this.draft.regionIds.push(r.id);
                        } else {
                          this.draft.regionIds = this.draft.regionIds.filter((id) => id !== r.id);
                        }
                        this.dirty = true;
                        this.renderRight();
                      },
                    }),
                    h('span', { class: 'palette-dot', style: { background: r.color } }),
                    h('span', null, r.name),
                  );
                }),
              ),
            )
          : null,

        // 资源值 & 难度值手动覆写
        h(
          'div',
          { class: 'override-box' },
          h(
            'div',
            { class: 'override-head' },
            h('span', null, '手动覆写资源 / 难度'),
            metrics.isResourceOverridden || metrics.isDifficultyOverridden
              ? h(
                  'button',
                  {
                    class: 'link-btn',
                    type: 'button',
                    onclick: () => {
                      this.draft.metrics.resourceOverride = null;
                      this.draft.metrics.difficultyOverride = null;
                      this.dirty = true;
                      this.renderAll();
                    },
                  },
                  '恢复自动求和',
                )
              : h('span', { class: 'field-hint', style: { margin: 0 } }, '留空 = 自动求和'),
          ),
          h(
            'div',
            { class: 'form-row-2' },
            h(
              'label',
              { class: 'mini-field' },
              h('span', { style: { color: 'var(--green)' } }, `资源值 (自动 ${metrics.autoResource})`),
              h('input', {
                class: 'input mono-input',
                type: 'number',
                min: '0',
                step: '1',
                placeholder: `自动 ${metrics.autoResource}`,
                value: this.draft.metrics.resourceOverride ?? '',
                onchange: (e) => {
                  const raw = e.target.value.trim();
                  this.draft.metrics.resourceOverride = raw === '' ? null : Math.max(0, Number(raw) || 0);
                  this.dirty = true;
                  this.renderCenter();
                  this.renderRight();
                },
              }),
            ),
            h(
              'label',
              { class: 'mini-field' },
              h('span', { style: { color: 'var(--red)' } }, `难度值 (自动 ${metrics.autoDifficulty})`),
              h('input', {
                class: 'input mono-input',
                type: 'number',
                min: '0',
                step: '1',
                placeholder: `自动 ${metrics.autoDifficulty}`,
                value: this.draft.metrics.difficultyOverride ?? '',
                onchange: (e) => {
                  const raw = e.target.value.trim();
                  this.draft.metrics.difficultyOverride = raw === '' ? null : Math.max(0, Number(raw) || 0);
                  this.dirty = true;
                  this.renderCenter();
                  this.renderRight();
                },
              }),
            ),
          ),
        ),
      ),
    );

    mount(this.rightEl, h('div', { class: 'panel-body' }, randomCard, propCard));
  }

  /* ---------------- 全局格子类型配置弹窗 ---------------- */

  openCellConfigModal() {
    let working = clone(this.store.dungeon.cellTypes);
    const bodyWrap = h('div', { class: 'cell-config-body' });

    const renderTable = () => {
      clear(bodyWrap);
      bodyWrap.append(
        h(
          'div',
          { class: 'field-hint', style: { marginBottom: '10px' } },
          '此处配置每种格子的全局「资源值」与「难度值」。保存后，所有未手动覆写的条带模板将自动按格子求和更新。',
        ),
      );
      const table = h('div', { class: 'cell-config-table' });
      table.append(
        h(
          'div',
          { class: 'cell-config-row head' },
          h('span', null, '颜色'),
          h('span', null, '图标'),
          h('span', null, '名称'),
          h('span', null, '资源值'),
          h('span', null, '难度值'),
          h('span', null, '等级偏移'),
          h('span', null, ''),
        ),
      );

      working.forEach((ct, index) => {
        const row = h(
          'div',
          { class: 'cell-config-row' },
          h('input', {
            type: 'color',
            class: 'color-swatch-input',
            value: ct.color,
            oninput: (e) => {
              ct.color = e.target.value;
            },
          }),
          h('input', {
            class: 'input center-input',
            type: 'text',
            maxlength: '4',
            value: ct.icon,
            oninput: (e) => {
              ct.icon = e.target.value || '■';
            },
          }),
          h('input', {
            class: 'input',
            type: 'text',
            value: ct.name,
            oninput: (e) => {
              ct.name = e.target.value;
              ct.short = e.target.value || ct.id;
            },
          }),
          h('input', {
            class: 'input mono-input',
            type: 'number',
            min: '0',
            step: '1',
            value: String(ct.resource),
            oninput: (e) => {
              ct.resource = Math.max(0, Number(e.target.value) || 0);
            },
          }),
          h('input', {
            class: 'input mono-input',
            type: 'number',
            min: '0',
            step: '1',
            value: String(ct.difficulty),
            oninput: (e) => {
              ct.difficulty = Math.max(0, Number(e.target.value) || 0);
            },
          }),
          h(
            'label',
            { class: 'checkbox-row center-check' },
            h('input', {
              type: 'checkbox',
              checked: Boolean(ct.supportsOffset),
              onchange: (e) => {
                ct.supportsOffset = e.target.checked;
              },
            }),
            h('span', null, '支持'),
          ),
          !ct.builtin
            ? h(
                'button',
                {
                  class: 'btn icon ghost small',
                  type: 'button',
                  title: '删除自定义类型',
                  onclick: () => {
                    working.splice(index, 1);
                    renderTable();
                  },
                },
                icon('trash', 13),
              )
            : h('span', { class: 'field-hint', style: { margin: 0, textAlign: 'center' } }, '内置'),
        );
        table.append(row);
      });
      bodyWrap.append(table);
    };
    renderTable();

    const dialog = openModal({
      title: '全局格子类型配置',
      subtitle: '配置每种格子的资源值与难度值，条带自动求和',
      size: 'wide',
      body: bodyWrap,
      footer: [
        h(
          'button',
          {
            class: 'btn',
            type: 'button',
            onclick: () => {
              const id = `custom_${working.length + 1}`;
              working.push({
                id,
                name: `自定义格 ${working.length + 1}`,
                short: '自',
                icon: '◇',
                color: '#a78bfa',
                resource: 10,
                difficulty: 10,
                supportsOffset: false,
                defaultOffset: 0,
                builtin: false,
              });
              renderTable();
            },
          },
          '+ 新增格子类型',
        ),
        h(
          'button',
          {
            class: 'btn',
            type: 'button',
            onclick: () => {
              working = clone(DEFAULT_CELL_TYPES);
              renderTable();
            },
          },
          '恢复默认数值',
        ),
        h('div', { class: 'spacer' }),
        h('button', { class: 'btn', type: 'button', onclick: () => dialog.close() }, '取消'),
        h(
          'button',
          {
            class: 'btn primary',
            type: 'button',
            onclick: () => {
              this.store.setCellTypes(working);
              this.randomConfig = {
                ...defaultRandomDraftConfig(working),
                ...this.randomConfig,
                weights: {
                  ...defaultRandomDraftConfig(working).weights,
                  ...(this.randomConfig.weights || {}),
                },
              };
              dialog.close();
              this.renderAll();
              toast('全局格子配置已更新，所有条带数值已重算', { type: 'success' });
            },
          },
          '保存配置',
        ),
      ],
    });
  }

  /* ---------------- 模板库 JSON 导入 / 导出 ---------------- */

  exportTemplates() {
    const json = serializeTemplateLibrary({
      cellTypes: this.store.dungeon.cellTypes,
      stripTemplates: this.store.dungeon.stripTemplates,
    });
    const name = slugify(this.store.project.meta.name || 'dungeon');
    downloadText(`${name}-strip-templates.json`, json);
    toast(`已导出 ${this.store.dungeon.stripTemplates.length} 个条带模板 JSON`, { type: 'success' });
  }

  async importTemplates() {
    const file = await pickFile({ accept: '.json,application/json' });
    if (!file) return;
    let text;
    try {
      text = await readFileText(file);
    } catch (error) {
      toast(`读取文件失败：${error.message}`, { type: 'error' });
      return;
    }
    const parsed = parseTemplateLibrary(text, this.store.dungeon.cellTypes);
    if (!parsed.ok) {
      toast(`导入失败：${parsed.errors[0] || '格式错误'}`, { type: 'error' });
      return;
    }
    const replace = await confirmDialog({
      title: '导入条带模板库',
      message: `解析到 ${parsed.stripTemplates.length} 个条带模板${parsed.cellTypes ? '（含全局格子配置）' : ''}。是否替换现有模板库？（可随时 Ctrl+Z 撤销）`,
      confirmText: '导入并替换',
      cancelText: '取消',
    });
    if (!replace) return;
    this.store.importTemplateLibrary({
      cellTypes: parsed.cellTypes,
      stripTemplates: parsed.stripTemplates,
      replace: true,
    });
    this.loadTemplate(this.store.dungeon.stripTemplates[0] || null);
    this.renderAll();
    toast(`已导入 ${parsed.stripTemplates.length} 个条带模板`, { type: 'success' });
  }
}
