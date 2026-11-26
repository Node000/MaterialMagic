/**
 * 模式二：单层三角地牢 & 地图生成池预览 Canvas 2D 渲染器。
 *
 * 特性：
 * - 多格异形条带的连续外墙轮廓、精确内部墙体与 1px 开放格分隔；
 * - 独立跨区桥接条带金色虚线专属样式 + 跨区门金色加粗高亮；
 * - 任意格单格交叉角落金色交叉徽标；
 * - 条带等级 (Lv.X) 与战斗/精英格最终敌人等级标注 (E{level})；
 * - 可切换：网格、区域框、格子内容、等级标注、节点图叠层 (BFS 跳数 d)、距离热力着色、贴边墙高亮；
 * - 支持滚轮缩放、拖拽平移、点击选中条带、悬停高亮与浮卡、双击自适应、2x PNG 导出。
 */

import { buildFloorGrid } from '../core/floor-file.js';
import { edgeKey } from '../core/strip-edges.js';
import { DEFAULT_CELL_TYPES, withAlpha } from '../core/constants.js';
import { boundsOfCells, centroidOfCells, coordKey } from '../core/polyomino.js';
import { cellTypeMap } from '../core/strip-templates.js';
import { clamp, h } from './dom.js';

const MIN_SCALE = 10;
const MAX_SCALE = 88;

const PALETTES = {
  dark: {
    bg: '#080c10',
    exportBg: '#0b1016',
    grid: 'rgba(100, 124, 144, 0.08)',
    gridMajor: 'rgba(110, 138, 160, 0.16)',
    bridgeColor: '#e2b75c',
    emptyFill: 'rgba(62, 74, 88, 0.34)',
    cellFillAlpha: 0.56,
    cellSelectedAlpha: 0.72,
    wallBacker: 'rgba(6, 10, 15, 0.85)',
    innerLine: 'rgba(8, 12, 18, 0.48)',
    cellText: '#f7fbff',
    cellTextShadow: 'rgba(6, 10, 16, 0.78)',
    selected: '#ffd878',
    hover: '#9ad0f0',
    doorBg: '#0a0e13',
    doorStroke: '#f4f8fc',
    bridgeDoor: '#ffd26a',
    crossStroke: '#ffd26a',
    contactStroke: '#ff5d55',
    contactGlow: 'rgba(255, 93, 85, 0.35)',
    badgeBg: 'rgba(8, 12, 18, 0.9)',
    badgeText: '#eef4fa',
    enemyBg: 'rgba(14, 18, 26, 0.82)',
    enemyText: '#ffe2dc',
    graphEdge: 'rgba(165, 205, 238, 0.82)',
    graphBridge: 'rgba(244, 197, 104, 0.95)',
    graphNodeBg: 'rgba(12, 18, 26, 0.94)',
    textMuted: '#8a9bae',
  },
  light: {
    bg: '#f4f0e7',
    exportBg: '#f8f4eb',
    grid: 'rgba(90, 80, 65, 0.12)',
    gridMajor: 'rgba(75, 60, 40, 0.2)',
    bridgeColor: '#b07d1a',
    emptyFill: 'rgba(160, 150, 136, 0.24)',
    cellFillAlpha: 0.44,
    cellSelectedAlpha: 0.6,
    wallBacker: 'rgba(248, 244, 235, 0.9)',
    innerLine: 'rgba(255, 252, 246, 0.65)',
    cellText: '#1e1812',
    cellTextShadow: 'rgba(255, 252, 246, 0.75)',
    selected: '#a86900',
    hover: '#2b6c94',
    doorBg: '#f8f4eb',
    doorStroke: '#2a251d',
    bridgeDoor: '#b07d1a',
    crossStroke: '#b07d1a',
    contactStroke: '#cf362d',
    contactGlow: 'rgba(207, 54, 45, 0.26)',
    badgeBg: 'rgba(255, 251, 243, 0.94)',
    badgeText: '#2a251d',
    enemyBg: 'rgba(255, 251, 243, 0.9)',
    enemyText: '#9c2028',
    graphEdge: 'rgba(45, 92, 128, 0.78)',
    graphBridge: 'rgba(176, 125, 26, 0.95)',
    graphNodeBg: 'rgba(255, 251, 243, 0.96)',
    textMuted: '#6a6155',
  },
};

function heatColor(ratio) {
  const t = clamp(ratio, 0, 1);
  // 绿青 (#4ea88a) -> 琥珀 (#e0a83e) -> 绯红 (#d9534f)
  let r;
  let g;
  let b;
  if (t < 0.5) {
    const k = t * 2;
    r = Math.round(78 + (224 - 78) * k);
    g = Math.round(168 + (168 - 168) * k);
    b = Math.round(138 + (62 - 138) * k);
  } else {
    const k = (t - 0.5) * 2;
    r = Math.round(224 + (217 - 224) * k);
    g = Math.round(168 + (83 - 168) * k);
    b = Math.round(62 + (79 - 62) * k);
  }
  return `#${((1 << 24) + (r << 16) + (g << 8) + b).toString(16).slice(1)}`;
}

function roundedRect(ctx, x, y, w, h_, r) {
  const rad = Math.min(r, w / 2, h_ / 2);
  ctx.beginPath();
  ctx.moveTo(x + rad, y);
  ctx.arcTo(x + w, y, x + w, y + h_, rad);
  ctx.arcTo(x + w, y + h_, x, y + h_, rad);
  ctx.arcTo(x, y + h_, x, y, rad);
  ctx.arcTo(x, y, x + w, y, rad);
  ctx.closePath();
}

export class FloorCanvas {
  constructor({
    canvas,
    host,
    hoverCard,
    store = null,
    getMap = null,
    getSelectedStripId = null,
    onSelectStrip = null,
    getUi = null,
    getCellTypes = null,
    getContactWalls = null,
  }) {
    this.canvas = canvas;
    this.host = host;
    this.hoverCard = hoverCard;
    this.store = store;
    this._getMap = getMap;
    this._getSelectedStripId = getSelectedStripId;
    this._onSelectStrip = onSelectStrip;
    this._getUi = getUi;
    this._getCellTypes = getCellTypes;
    this._getContactWalls = getContactWalls;

    this.ctx = canvas.getContext('2d');
    this.theme = document.documentElement.dataset.theme === 'light' ? 'light' : 'dark';
    this.view = { scale: 34, offsetX: 40, offsetY: 40 };
    this.hoverStripId = null;
    this.dragging = false;
    this.moved = false;
    this.dragStart = null;
    this.lastMapId = null;
    this.onZoomChange = null;

    this.onPointerDown = this.onPointerDown.bind(this);
    this.onPointerMove = this.onPointerMove.bind(this);
    this.onPointerUp = this.onPointerUp.bind(this);
    this.onWheel = this.onWheel.bind(this);
    this.onDoubleClick = this.onDoubleClick.bind(this);
    this.onPointerLeave = this.onPointerLeave.bind(this);

    canvas.addEventListener('pointerdown', this.onPointerDown);
    canvas.addEventListener('pointermove', this.onPointerMove);
    canvas.addEventListener('pointerup', this.onPointerUp);
    canvas.addEventListener('pointercancel', this.onPointerUp);
    canvas.addEventListener('wheel', this.onWheel, { passive: false });
    canvas.addEventListener('dblclick', this.onDoubleClick);
    canvas.addEventListener('pointerleave', this.onPointerLeave);

    this.resizeObserver = new ResizeObserver(() => this.resize());
    this.resizeObserver.observe(host);
    this.resize();
  }

  getMap() {
    return this._getMap ? this._getMap() : this.store?.currentFloorMap || null;
  }

  getSelectedStripId() {
    return this._getSelectedStripId ? this._getSelectedStripId() : this.store?.selectedStripId || null;
  }

  selectStrip(stripId) {
    if (this._onSelectStrip) this._onSelectStrip(stripId);
    else this.store?.selectFloorStrip(stripId);
  }

  getUi() {
    if (this._getUi) return this._getUi();
    return this.store?.ui || {
      floorShowGrid: true,
      floorShowRegions: true,
      floorShowCells: true,
      floorShowLevels: true,
      floorShowGraph: false,
      floorShowHeatmap: false,
      floorShowContacts: false,
    };
  }

  getCellTypes() {
    return this._getCellTypes ? this._getCellTypes() : this.store?.dungeon?.cellTypes || DEFAULT_CELL_TYPES;
  }

  getContactWalls() {
    return this._getContactWalls ? this._getContactWalls() : null;
  }

  setTheme(theme) {
    this.theme = theme === 'light' ? 'light' : 'dark';
    this.render();
  }

  resize() {
    const rect = this.host.getBoundingClientRect();
    if (!rect.width || !rect.height) return;
    const dpr = Math.min(2, window.devicePixelRatio || 1);
    this.canvas.width = Math.round(rect.width * dpr);
    this.canvas.height = Math.round(rect.height * dpr);
    this.canvas.style.width = `${rect.width}px`;
    this.canvas.style.height = `${rect.height}px`;
    this.ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    this.cssWidth = rect.width;
    this.cssHeight = rect.height;
    this.render();
  }

  worldToScreen(wx, wy) {
    return {
      x: this.view.offsetX + wx * this.view.scale,
      y: this.view.offsetY + wy * this.view.scale,
    };
  }

  screenToWorld(sx, sy) {
    return {
      x: (sx - this.view.offsetX) / this.view.scale,
      y: (sy - this.view.offsetY) / this.view.scale,
    };
  }

  fit() {
    const map = this.getMap();
    if (!map || !map.strips?.length || !this.cssWidth || !this.cssHeight) return;
    const b = map.stats?.bounds || boundsOfCells(map.strips.flatMap((s) => s.cells));
    const pad = 48;
    const scaleX = (this.cssWidth - pad * 2) / Math.max(1, b.width);
    const scaleY = (this.cssHeight - pad * 2) / Math.max(1, b.height);
    const scale = clamp(Math.min(scaleX, scaleY), MIN_SCALE, 52);
    this.view.scale = scale;
    this.view.offsetX = (this.cssWidth - b.width * scale) / 2 - b.minX * scale;
    this.view.offsetY = (this.cssHeight - b.height * scale) / 2 - b.minY * scale;
    this.onZoomChange?.(scale);
    this.render();
  }

  zoomBy(factor, anchorX = null, anchorY = null) {
    const cx = anchorX ?? (this.cssWidth || 400) / 2;
    const cy = anchorY ?? (this.cssHeight || 300) / 2;
    const before = this.screenToWorld(cx, cy);
    const nextScale = clamp(this.view.scale * factor, MIN_SCALE, MAX_SCALE);
    this.view.scale = nextScale;
    this.view.offsetX = cx - before.x * nextScale;
    this.view.offsetY = cy - before.y * nextScale;
    this.onZoomChange?.(nextScale);
    this.render();
  }

  hitTestStrip(sx, sy) {
    const map = this.getMap();
    if (!map || !map.strips?.length) return null;
    const w = this.screenToWorld(sx, sy);
    const gx = Math.floor(w.x);
    const gy = Math.floor(w.y);
    const hits = [];
    for (const strip of map.strips) {
      const cell = strip.cells.find((c) => c.x === gx && c.y === gy);
      if (cell) hits.push({ strip, cell });
    }
    if (!hits.length) return null;
    // 如果当前选中的条带在交叉格里，优先返回另一条方便交替点击
    if (hits.length > 1 && hits[0].strip.id === this.getSelectedStripId()) {
      return hits[1];
    }
    return hits[0];
  }

  onPointerDown(event) {
    if (event.button !== 0 && event.button !== 1) return;
    this.dragging = true;
    this.moved = false;
    this.dragStart = {
      x: event.clientX,
      y: event.clientY,
      ox: this.view.offsetX,
      oy: this.view.offsetY,
    };
    this.canvas.setPointerCapture?.(event.pointerId);
  }

  onPointerMove(event) {
    const rect = this.canvas.getBoundingClientRect();
    const sx = event.clientX - rect.left;
    const sy = event.clientY - rect.top;

    if (this.dragging && this.dragStart) {
      const dx = event.clientX - this.dragStart.x;
      const dy = event.clientY - this.dragStart.y;
      if (Math.hypot(dx, dy) > 4) this.moved = true;
      this.view.offsetX = this.dragStart.ox + dx;
      this.view.offsetY = this.dragStart.oy + dy;
      this.hideHoverCard();
      this.render();
      return;
    }

    const hit = this.hitTestStrip(sx, sy);
    const nextId = hit?.strip?.id || null;
    if (nextId !== this.hoverStripId) {
      this.hoverStripId = nextId;
      this.render();
    }
    if (hit) {
      this.showHoverCard(hit, sx, sy);
    } else {
      this.hideHoverCard();
    }
  }

  onPointerUp(event) {
    const rect = this.canvas.getBoundingClientRect();
    const sx = event.clientX - rect.left;
    const sy = event.clientY - rect.top;
    if (this.dragging && !this.moved) {
      const hit = this.hitTestStrip(sx, sy);
      this.selectStrip(hit?.strip?.id || null);
      this.render();
    }
    this.dragging = false;
    this.dragStart = null;
  }

  onWheel(event) {
    event.preventDefault();
    const rect = this.canvas.getBoundingClientRect();
    const sx = event.clientX - rect.left;
    const sy = event.clientY - rect.top;
    const factor = event.deltaY < 0 ? 1.14 : 1 / 1.14;
    this.zoomBy(factor, sx, sy);
  }

  onDoubleClick() {
    this.fit();
  }

  onPointerLeave() {
    if (this.hoverStripId) {
      this.hoverStripId = null;
      this.render();
    }
    this.hideHoverCard();
  }

  showHoverCard(hit, sx, sy) {
    if (!this.hoverCard) return;
    const { strip, cell } = hit;
    const map = this.getMap();
    const tMap = cellTypeMap(this.getCellTypes());
    const def = tMap.get(cell.type) || tMap.get('empty') || { icon: '·', name: cell.type || '空通道' };
    const region = (map?.regions || []).find((r) => r.id === strip.regionId);
    const regionLabel =
      strip.role === 'bridge'
        ? `独立跨区桥 (${(strip.bridgeBetween || []).map((id) => map.regions.find((r) => r.id === id)?.shortName || id).join(' ↔ ') || '跨区'})`
        : region?.name || strip.regionId || '区域';

    const titleText =
      strip.templateName && strip.templateName !== strip.id
        ? `${strip.templateName} (${strip.id})`
        : `条带 ${strip.id}`;

    const rows = [
      h('div', { class: 'hover-title' }, `${titleText}${strip.noLoop ? ' · 非环端点' : ''}`),
      h('div', { class: 'hover-sub' }, `${regionLabel} · 跳数 d=${strip.distanceFromSpawn ?? 0} · 条带 Lv.${strip.level ?? 1}`),
      h(
        'div',
        { class: 'hover-sub' },
        `当前格：${def.icon} ${def.name} (${cell.x},${cell.y})${cell.isPortal ? ' [出入口]' : ''}${cell.enemyLevel != null ? ` · 敌人 Lv.${cell.enemyLevel}${cell.difficultyOffset ? ` (偏移 ${cell.difficultyOffset > 0 ? '+' : ''}${cell.difficultyOffset})` : ''}` : ''}`,
      ),
    ];
    if (Number.isFinite(strip.resource) || Number.isFinite(strip.difficulty)) {
      rows.push(h('div', { class: 'hover-sub' }, `资源 ${strip.resource ?? 0} · 难度 ${strip.difficulty ?? 0}`));
    }
    this.hoverCard.replaceChildren(...rows);
    this.hoverCard.hidden = false;
    const left = clamp(sx + 16, 12, Math.max(12, (this.cssWidth || 400) - 260));
    const top = clamp(sy + 16, 12, Math.max(12, (this.cssHeight || 300) - 116));
    this.hoverCard.style.left = `${left}px`;
    this.hoverCard.style.top = `${top}px`;
  }

  hideHoverCard() {
    if (this.hoverCard) this.hoverCard.hidden = true;
  }

  render() {
    const map = this.getMap();
    const mapKey = map ? map._renderKey || map.id : null;
    if (map && mapKey !== this.lastMapId) {
      this.lastMapId = mapKey;
      if (this.cssWidth > 0 && this.cssHeight > 0) this.fit();
    } else if (!map) {
      this.lastMapId = null;
    }
    this.drawScene(this.ctx, this.cssWidth || 600, this.cssHeight || 400, { exportMode: false });
  }

  drawScene(ctx, width, height, { exportMode = false, viewOverride = null } = {}) {
    const pal = PALETTES[this.theme === 'light' ? 'light' : 'dark'];
    const map = this.getMap();
    const ui = this.getUi();
    const view = viewOverride || this.view;

    ctx.save();
    ctx.fillStyle = exportMode ? pal.exportBg : pal.bg;
    ctx.fillRect(0, 0, width, height);

    if (!map || !map.strips?.length) {
      ctx.restore();
      return;
    }

    const scale = view.scale;
    const ox = view.offsetX;
    const oy = view.offsetY;
    const toX = (wx) => ox + wx * scale;
    const toY = (wy) => oy + wy * scale;

    // 1. 世界网格
    if (ui.floorShowGrid) {
      const startX = Math.floor(-ox / scale) - 1;
      const endX = Math.ceil((width - ox) / scale) + 1;
      const startY = Math.floor(-oy / scale) - 1;
      const endY = Math.ceil((height - oy) / scale) + 1;
      ctx.lineWidth = 1;
      for (let gx = startX; gx <= endX; gx += 1) {
        const sx = Math.round(toX(gx)) + 0.5;
        ctx.strokeStyle = gx % 5 === 0 ? pal.gridMajor : pal.grid;
        ctx.beginPath();
        ctx.moveTo(sx, 0);
        ctx.lineTo(sx, height);
        ctx.stroke();
      }
      for (let gy = startY; gy <= endY; gy += 1) {
        const sy = Math.round(toY(gy)) + 0.5;
        ctx.strokeStyle = gy % 5 === 0 ? pal.gridMajor : pal.grid;
        ctx.beginPath();
        ctx.moveTo(0, sy);
        ctx.lineTo(width, sy);
        ctx.stroke();
      }
    }

    const regionMap = new Map((map.regions || []).map((r) => [r.id, r]));
    const tMap = cellTypeMap(this.getCellTypes());
    const maxDist = Math.max(1, map.stats?.maxDistance || 1);

    // 2. 区域背景光晕框（凸显三角形三大区域）
    if (ui.floorShowRegions) {
      for (const region of map.regions || []) {
        const rStrips = map.strips.filter((s) => s.role !== 'bridge' && s.regionId === region.id);
        if (!rStrips.length) continue;
        const b = boundsOfCells(rStrips.flatMap((s) => s.cells));
        const pad = 0.38;
        const rx = toX(b.minX - pad);
        const ry = toY(b.minY - pad);
        const rw = (b.width + pad * 2) * scale;
        const rh = (b.height + pad * 2) * scale;
        ctx.fillStyle = withAlpha(region.color, this.theme === 'light' ? 0.08 : 0.06);
        ctx.strokeStyle = withAlpha(region.color, 0.34);
        ctx.lineWidth = 1.4;
        ctx.setLineDash([6, 5]);
        roundedRect(ctx, rx, ry, rw, rh, Math.max(8, scale * 0.3));
        ctx.fill();
        ctx.stroke();
        ctx.setLineDash([]);

        // 区域名称角标
        ctx.fillStyle = withAlpha(region.color, this.theme === 'light' ? 0.95 : 0.92);
        ctx.font = `700 ${clamp(Math.round(scale * 0.44), 13, 18)}px sans-serif`;
        ctx.textAlign = 'left';
        ctx.textBaseline = 'bottom';
        ctx.fillText(
          `${region.name}${region.role === 'start' ? ' · 出生区' : ''}`,
          rx + 8,
          ry - 5,
        );
      }
    }

    // 统计交叉格（共享同坐标的 2 条条带）
    const cellOwners = new Map();
    for (const strip of map.strips) {
      for (const cell of strip.cells) {
        const k = coordKey(cell.x, cell.y);
        const arr = cellOwners.get(k) || [];
        arr.push({ strip, cell });
        cellOwners.set(k, arr);
      }
    }

    const colorOfStrip = (strip) => {
      if (ui.floorShowHeatmap) {
        return heatColor((strip.distanceFromSpawn || 0) / maxDist);
      }
      if (strip.role === 'bridge') return pal.bridgeColor;
      return regionMap.get(strip.regionId)?.color || '#8a9aab';
    };

    const selectedId = exportMode ? null : this.getSelectedStripId();
    const hoverId = exportMode ? null : this.hoverStripId;

    const fillColorOfCell = (strip, cell, isSelected) => {
      if (ui.floorShowHeatmap) {
        const hc = heatColor((strip.distanceFromSpawn || 0) / maxDist);
        return withAlpha(hc, isSelected ? 0.68 : 0.48);
      }
      if (!ui.floorShowCells || cell.type === 'empty') {
        return isSelected ? withAlpha(colorOfStrip(strip), 0.34) : pal.emptyFill;
      }
      const def = tMap.get(cell.type);
      if (!def) return pal.emptyFill;
      return withAlpha(def.color, isSelected ? pal.cellSelectedAlpha : pal.cellFillAlpha);
    };

    // 3. 满格平铺绘制每个世界格（有内容格完整覆盖空通道格）
    for (const [, owners] of cellOwners) {
      const winner = owners.find((o) => o.cell.type !== 'empty') || owners[0];
      const anySelected = owners.some((o) => o.strip.id === selectedId);
      const { strip, cell } = winner;
      const x = toX(cell.x);
      const y = toY(cell.y);

      ctx.fillStyle = fillColorOfCell(strip, cell, anySelected);
      ctx.fillRect(x, y, scale, scale);
    }

    // 3.5 使用与导出一致的物理边：开放边画 1px 内部格分隔线，封闭边按条带分层绘制防粘连粗外墙与条带内墙
    const { edges } = buildFloorGrid(map);
    const openEdges = new Map(edges.map((e) => [edgeKey(e.x, e.y, e.dir), e.type]));
    const stripCellSets = new Map(
      map.strips.map((s) => [s.id, new Set(s.cells.map((c) => coordKey(c.x, c.y)))]),
    );

    // 开放边 1px 清晰分隔线
    ctx.save();
    ctx.strokeStyle = pal.innerLine;
    ctx.lineWidth = 1;
    for (const edge of edges) {
      if (edge.type !== 'open') continue;
      if (edge.dir === 'E') {
        const sx = Math.round(toX(edge.x + 1)) + 0.5;
        const sy = toY(edge.y);
        ctx.beginPath();
        ctx.moveTo(sx, sy + 1);
        ctx.lineTo(sx, sy + scale - 1);
        ctx.stroke();
      } else if (edge.dir === 'S') {
        const sx = toX(edge.x);
        const sy = Math.round(toY(edge.y + 1)) + 0.5;
        ctx.beginPath();
        ctx.moveTo(sx + 1, sy);
        ctx.lineTo(sx + scale - 1, sy);
        ctx.stroke();
      }
    }
    ctx.restore();

    // 按层级绘制条带外墙 + 条带内部墙（普通 -> 桥接 -> 悬停 -> 选中）
    const orderedStrips = [
      ...map.strips.filter((s) => s.id !== selectedId && s.id !== hoverId && s.role !== 'bridge'),
      ...map.strips.filter((s) => s.id !== selectedId && s.id !== hoverId && s.role === 'bridge'),
      ...map.strips.filter((s) => s.id === hoverId && s.id !== selectedId),
      ...map.strips.filter((s) => s.id === selectedId),
    ];

    for (const strip of orderedStrips) {
      const baseColor = colorOfStrip(strip);
      const isSelected = strip.id === selectedId;
      const isHover = strip.id === hoverId;
      const localSet = stripCellSets.get(strip.id);

      // 判定某格某方向是否需要绘制墙面：
      // - 物理边为 wall（即既不是 open 也不是 door，包含条带外墙和条带内墙）；
      // - 门边也先画底层墙轮廓，随后步骤 4 在正中盖上门框，视觉更稳重；
      // - 选中条带时，在交叉格也描出该条带自身完整轮廓，方便辨认范围。
      const isWallSide = (cell, dir, nx, ny) => {
        const edgeType = openEdges.get(edgeKey(cell.x, cell.y, dir));
        if (edgeType !== 'open') return true;
        if (isSelected && !localSet.has(coordKey(nx, ny))) return true;
        return false;
      };

      const traceWalls = () => {
        ctx.beginPath();
        const drawnLocal = new Set();
        for (const cell of strip.cells) {
          const x = toX(cell.x);
          const y = toY(cell.y);
          const checks = [
            ['N', cell.x, cell.y - 1, x, y, x + scale, y],
            ['E', cell.x + 1, cell.y, x + scale, y, x + scale, y + scale],
            ['S', cell.x, cell.y + 1, x, y + scale, x + scale, y + scale],
            ['W', cell.x - 1, cell.y, x, y, x, y + scale],
          ];
          for (const [dir, nx, ny, x1, y1, x2, y2] of checks) {
            if (!isWallSide(cell, dir, nx, ny)) continue;
            const k = edgeKey(cell.x, cell.y, dir);
            if (drawnLocal.has(k)) continue;
            drawnLocal.add(k);
            ctx.moveTo(x1, y1);
            ctx.lineTo(x2, y2);
          }
        }
      };

      ctx.save();
      ctx.strokeStyle = pal.wallBacker;
      ctx.lineWidth = isSelected ? 5.2 : 4.2;
      ctx.lineJoin = 'round';
      ctx.lineCap = 'round';
      traceWalls();
      ctx.stroke();

      ctx.strokeStyle = isSelected
        ? pal.selected
        : isHover
          ? pal.hover
          : strip.role === 'bridge'
            ? pal.bridgeColor
            : withAlpha(baseColor, 0.96);
      ctx.lineWidth = isSelected ? 3.2 : isHover ? 2.8 : strip.role === 'bridge' ? 2.6 : 2.3;
      if (strip.role === 'bridge' && !isSelected) ctx.setLineDash([6, 4]);
      traceWalls();
      ctx.stroke();
      ctx.setLineDash([]);
      ctx.restore();
    }

    // 3.8 可选：未开门的条带间贴边墙高亮（地图生成池环路留白诊断）
    const contactEdges = ui.floorShowContacts ? this.getContactWalls() : null;
    if (contactEdges && contactEdges.length) {
      ctx.save();
      ctx.lineCap = 'round';
      ctx.beginPath();
      for (const edge of contactEdges) {
        if (edge.dir === 'E') {
          const sx = toX(edge.x + 1);
          const sy = toY(edge.y);
          ctx.moveTo(sx, sy);
          ctx.lineTo(sx, sy + scale);
        } else {
          const sx = toX(edge.x);
          const sy = toY(edge.y + 1);
          ctx.moveTo(sx, sy);
          ctx.lineTo(sx + scale, sy);
        }
      }
      ctx.strokeStyle = pal.contactGlow;
      ctx.lineWidth = 7;
      ctx.stroke();
      ctx.strokeStyle = pal.contactStroke;
      ctx.lineWidth = 3.2;
      ctx.stroke();
      ctx.restore();
    }

    // 4. 绘制门（区分普通门与跨区桥门）与轻量角落交叉标记
    const bridgeDoorKeys = new Set();
    const stripById = new Map(map.strips.map((s) => [s.id, s]));
    for (const link of map.links || []) {
      if (link.type !== 'door') continue;
      const a = stripById.get(link.a?.stripId);
      const b = stripById.get(link.b?.stripId);
      if (!a || !b) continue;
      const isBridge = Boolean(link.isBridgeLink || a.role === 'bridge' || b.role === 'bridge');
      if (!isBridge) continue;
      const ca = a.cells[link.a.cellIndex];
      const cb = b.cells[link.b.cellIndex];
      if (!ca || !cb) continue;
      const x = Math.min(ca.x, cb.x);
      const y = Math.min(ca.y, cb.y);
      const dir = ca.x === cb.x ? 'S' : 'E';
      bridgeDoorKeys.add(edgeKey(x, y, dir));
    }

    for (const edge of edges) {
      if (edge.type !== 'door') continue;
      const key = edgeKey(edge.x, edge.y, edge.dir);
      const horizontal = edge.dir === 'S';
      const nx = edge.x + (horizontal ? 0 : 1);
      const ny = edge.y + (horizontal ? 1 : 0);
      const ownersA = cellOwners.get(coordKey(edge.x, edge.y)) || [];
      const ownersB = cellOwners.get(coordKey(nx, ny)) || [];
      const isBridgeLink =
        bridgeDoorKeys.has(key) ||
        ownersA.some((o) => o.strip.role === 'bridge') ||
        ownersB.some((o) => o.strip.role === 'bridge');

      const mx = toX(edge.x + (horizontal ? 0.5 : 1));
      const my = toY(edge.y + (horizontal ? 1 : 0.5));
      const span = Math.max(6, scale * 0.34);
      const thick = isBridgeLink ? Math.max(5, scale * 0.18) : Math.max(4, scale * 0.14);
      const w = horizontal ? span : thick;
      const h_ = horizontal ? thick : span;

      ctx.save();
      ctx.fillStyle = pal.doorBg;
      ctx.strokeStyle = isBridgeLink ? pal.bridgeDoor : pal.doorStroke;
      ctx.lineWidth = isBridgeLink ? 2.2 : 1.5;
      ctx.fillRect(mx - w / 2, my - h_ / 2, w, h_);
      ctx.strokeRect(mx - w / 2, my - h_ / 2, w, h_);
      ctx.restore();
    }

    for (const link of map.links || []) {
      if (link.type !== 'cross' || !link.cell) continue;
      const kx = toX(link.cell.x) + Math.max(6, scale * 0.17);
      const ky = toY(link.cell.y) + Math.max(6, scale * 0.17);
      const r = Math.max(3, scale * 0.08);
      ctx.save();
      ctx.strokeStyle = pal.crossStroke;
      ctx.lineWidth = 1.6;
      ctx.beginPath();
      ctx.moveTo(kx - r, ky - r);
      ctx.lineTo(kx + r, ky + r);
      ctx.moveTo(kx + r, ky - r);
      ctx.lineTo(kx - r, ky + r);
      ctx.stroke();
      ctx.restore();
    }

    // 5. 绘制每格高对比图标 + 全称 / 战斗格敌人等级（交叉格由有内容格完整显示）
    if (ui.floorShowCells && scale >= 14) {
      for (const [, owners] of cellOwners) {
        const winner = owners.find((o) => o.cell.type !== 'empty') || owners[0];
        const { cell } = winner;
        const anyPortal = owners.some((o) => o.cell.isPortal);
        const cx = toX(cell.x + 0.5);
        const cy = toY(cell.y + 0.5);

        if (anyPortal) {
          ctx.save();
          ctx.strokeStyle = 'rgba(77, 226, 216, 0.92)';
          ctx.lineWidth = 1.8;
          ctx.strokeRect(toX(cell.x) + 3, toY(cell.y) + 3, scale - 6, scale - 6);
          ctx.restore();
        }

        const def = tMap.get(cell.type) || tMap.get('empty') || { icon: '·', name: cell.type || '' };
        if (cell.type !== 'empty') {
          const showSub = scale >= 24;
          ctx.save();
          ctx.fillStyle = pal.cellText;
          ctx.shadowColor = pal.cellTextShadow;
          ctx.shadowBlur = 3;
          ctx.font = `700 ${clamp(Math.round(scale * (showSub ? 0.38 : 0.46)), 10, 22)}px sans-serif`;
          ctx.textAlign = 'center';
          ctx.textBaseline = 'middle';
          ctx.fillText(def.icon, cx, showSub ? cy - scale * 0.17 : cy);

          if (showSub && !(ui.floorShowLevels && cell.enemyLevel != null)) {
            const fullLabel = def.name || def.short || '';
            const labelScale = fullLabel.length >= 3 ? 0.21 : 0.25;
            ctx.font = `700 ${clamp(Math.round(scale * labelScale), 8, 12)}px sans-serif`;
            ctx.fillText(fullLabel, cx, cy + scale * 0.25);
          }
          ctx.restore();
        }

        if (ui.floorShowLevels && cell.enemyLevel != null && scale >= 20) {
          const label = `E${cell.enemyLevel}`;
          ctx.save();
          ctx.font = `800 ${clamp(Math.round(scale * 0.29), 10, 13)}px var(--mono), monospace`;
          const tw = ctx.measureText(label).width + 8;
          const th = clamp(Math.round(scale * 0.35), 14, 17);
          const bx = cx - tw / 2;
          const by = toY(cell.y + 1) - th - 2;
          ctx.fillStyle = pal.enemyBg;
          roundedRect(ctx, bx, by, tw, th, 4);
          ctx.fill();
          ctx.fillStyle = pal.enemyText;
          ctx.textAlign = 'center';
          ctx.textBaseline = 'middle';
          ctx.fillText(label, cx, by + th / 2 + 0.5);
          ctx.restore();
        }
      }
    }

    // 6. 出生点光圈强调
    if (map.spawn) {
      const sx = toX(map.spawn.x + 0.5);
      const sy = toY(map.spawn.y + 0.5);
      ctx.save();
      ctx.strokeStyle = '#f4c568';
      ctx.lineWidth = 2.2;
      ctx.beginPath();
      ctx.arc(sx, sy, scale * 0.42, 0, Math.PI * 2);
      ctx.stroke();
      ctx.restore();
    }

    // 7. 条带等级标签（Lv.X / 端 Lv.X / 桥 Lv.X）
    if (ui.floorShowLevels && !ui.floorShowGraph && scale >= 14) {
      for (const strip of map.strips) {
        const c = centroidOfCells(strip.cells);
        const sx = toX(c.x);
        const sy = toY(c.y) - scale * 0.38;
        const text =
          strip.role === 'bridge'
            ? `桥 Lv.${strip.level ?? 1}`
            : strip.noLoop
              ? `端 Lv.${strip.level ?? 1}`
              : `Lv.${strip.level ?? 1}`;
        ctx.save();
        ctx.font = `700 ${clamp(Math.round(scale * 0.33), 11, 14)}px var(--mono), monospace`;
        const w_ = ctx.measureText(text).width + 12;
        const h_ = 19;
        ctx.fillStyle = pal.badgeBg;
        ctx.strokeStyle = strip.id === selectedId ? pal.selected : withAlpha(colorOfStrip(strip), 0.85);
        ctx.lineWidth = 1.2;
        roundedRect(ctx, sx - w_ / 2, sy - h_ / 2, w_, h_, 5);
        ctx.fill();
        ctx.stroke();
        ctx.fillStyle = pal.badgeText;
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        ctx.fillText(text, sx, sy + 0.5);
        ctx.restore();
      }
    }

    // 8. 抽象节点图叠层（条带 -> 节点圆 + BFS 距离 d + 条带等级 Lv）
    if (ui.floorShowGraph) {
      const centers = new Map();
      for (const strip of map.strips) {
        const c = centroidOfCells(strip.cells);
        centers.set(strip.id, { x: toX(c.x), y: toY(c.y) });
      }
      // 边
      for (const link of map.links || []) {
        const pa = centers.get(link.a?.stripId);
        const pb = centers.get(link.b?.stripId);
        if (!pa || !pb) continue;
        ctx.save();
        ctx.strokeStyle = link.isBridgeLink ? pal.graphBridge : link.type === 'cross' ? pal.crossStroke : pal.graphEdge;
        ctx.lineWidth = link.isBridgeLink ? 3 : 2;
        if (link.isBridgeLink) ctx.setLineDash([7, 4]);
        else if (link.type === 'cross') ctx.setLineDash([3, 3]);
        ctx.beginPath();
        ctx.moveTo(pa.x, pa.y);
        ctx.lineTo(pb.x, pb.y);
        ctx.stroke();
        ctx.setLineDash([]);
        ctx.restore();
      }
      // 节点
      for (const strip of map.strips) {
        const pt = centers.get(strip.id);
        const r = clamp(scale * 0.44, 13, 21);
        ctx.save();
        ctx.fillStyle = pal.graphNodeBg;
        ctx.strokeStyle = strip.id === selectedId ? pal.selected : colorOfStrip(strip);
        ctx.lineWidth = strip.id === map.spawn?.stripId ? 3 : 2;
        ctx.beginPath();
        ctx.arc(pt.x, pt.y, r, 0, Math.PI * 2);
        ctx.fill();
        ctx.stroke();

        ctx.fillStyle = pal.badgeText;
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        ctx.font = `700 ${Math.round(r * 0.62)}px var(--mono), monospace`;
        ctx.fillText(`d${strip.distanceFromSpawn ?? 0}`, pt.x, pt.y - r * 0.26);
        ctx.font = `600 ${Math.round(r * 0.52)}px var(--mono), monospace`;
        ctx.fillStyle = colorOfStrip(strip);
        ctx.fillText(`L${strip.level ?? 1}`, pt.x, pt.y + r * 0.34);
        ctx.restore();
      }
    }

    // 导出 PNG 底部信息条
    if (exportMode) {
      const barH = 40;
      ctx.fillStyle = 'rgba(10, 15, 21, 0.9)';
      ctx.fillRect(0, height - barH, width, barH);
      ctx.fillStyle = '#eef4fa';
      ctx.font = '600 13px sans-serif';
      ctx.textAlign = 'left';
      ctx.textBaseline = 'middle';
      ctx.fillText(
        `${map.profileName || '单层三角地牢'} · SEED ${map.seed ?? '—'} · 条带 ${map.strips.length} · 资源 ${map.stats?.totalResource ?? '—'} · 难度 ${map.stats?.totalDifficulty ?? '—'}`,
        16,
        height - barH / 2,
      );
      ctx.fillStyle = '#d9a441';
      ctx.textAlign = 'right';
      ctx.fillText('PLOTWEAVE DUNGEON EDITOR', width - 16, height - barH / 2);
    }

    ctx.restore();
  }

  async toPngBlob({ scale = 2 } = {}) {
    const map = this.getMap();
    if (!map || !map.strips?.length) return null;
    const b = map.stats?.bounds || boundsOfCells(map.strips.flatMap((s) => s.cells));
    const cellPx = 40;
    const pad = 64;
    const width = Math.max(760, Math.round(b.width * cellPx + pad * 2));
    const height = Math.max(540, Math.round(b.height * cellPx + pad * 2 + 40));

    const off = document.createElement('canvas');
    off.width = width * scale;
    off.height = height * scale;
    const ctx = off.getContext('2d');
    ctx.setTransform(scale, 0, 0, scale, 0, 0);

    const viewOverride = {
      scale: cellPx,
      offsetX: (width - b.width * cellPx) / 2 - b.minX * cellPx,
      offsetY: (height - 40 - b.height * cellPx) / 2 - b.minY * cellPx,
    };
    this.drawScene(ctx, width, height, { exportMode: true, viewOverride });
    return new Promise((resolve) => off.toBlob(resolve, 'image/png'));
  }
}
