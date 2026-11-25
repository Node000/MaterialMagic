/**
 * Canvas 2D 顶视角渲染器。
 *
 * 只读取 store 状态，不直接修改工程；点击出生点 / 选择地块通过 store 方法完成。
 * 视图层可以在不改变核心数据结构的情况下替换成 WebGL、SVG 或别的引擎。
 */

import { withAlpha, CONTENT_TYPES, DIRECTIONS } from '../core/constants.js';
import { mapBounds, plotBounds, plotCenter, cellKey } from '../core/geometry.js';
import { regionById, plotKindById, subRoomKindById } from '../core/rules.js';
import { plotDegree } from '../core/validate-map.js';
import { registry } from '../core/registry.js';
import { h, clamp } from './dom.js';

const MIN_SCALE = 3.5;
const MAX_SCALE = 90;

/**
 * Canvas 不直接读 CSS 变量（离屏导出时上下文不同），所以这里保留一套
 * 与 UI 主题同步的绘制色板。新增主题时只需要扩展这张表。
 */
const PALETTES = {
  dark: {
    background: '#080c10',
    exportBackground: '#0b1016',
    gridFill: 'rgba(255, 255, 255, 0.012)',
    gridLine: 'rgba(100, 124, 144, 0.075)',
    gridMajor: 'rgba(110, 138, 160, 0.15)',
    gridBorder: 'rgba(120, 145, 165, 0.16)',
    regionFill: 0.035,
    regionLabel: 0.34,
    plotFallback: '#8a9aab',
    labelBackdrop: 'rgba(7, 11, 15, 0.72)',
    labelText: 'rgba(231, 239, 247, 0.82)',
    labelSelected: '#ffe9bd',
    selectedMark: '#f4c568',
    selectedOutline: '#f4c568',
    doorLine: 'rgba(198, 218, 234, 0.55)',
    doorLineSelected: 'rgba(244, 197, 104, 0.95)',
    doorMark: 'rgba(234, 242, 249, 0.85)',
    doorMarkSelected: '#ffe1a1',
    doorDot: 'rgba(234, 242, 249, 0.9)',
    crossLine: 'rgba(217, 164, 65, 0.85)',
    crossLineSelected: '#ffe1a1',
    crossFill: 'rgba(8, 12, 16, 0.55)',
    subRoomStroke: 'rgba(10, 15, 21, 0.82)',
    contentOverflow: '#dbe3ec',
    spawnStroke: 'rgba(255, 244, 214, 0.9)',
    spawnFill: 'rgba(217, 164, 65, 0.16)',
    spawnDot: '#ffe4ab',
    spawnText: '#1a1205',
    exportBar: 'rgba(10, 15, 21, 0.94)',
    exportTitle: '#e9eef5',
    exportSub: '#7f8d9d',
    exportBrand: 'rgba(217, 164, 65, 0.72)',
  },
  light: {
    background: '#f4f0e7',
    exportBackground: '#f8f4eb',
    gridFill: 'rgba(90, 70, 40, 0.025)',
    gridLine: 'rgba(90, 80, 65, 0.14)',
    gridMajor: 'rgba(75, 60, 40, 0.22)',
    gridBorder: 'rgba(90, 75, 50, 0.22)',
    regionFill: 0.055,
    regionLabel: 0.62,
    plotFallback: '#8a7a66',
    labelBackdrop: 'rgba(255, 251, 243, 0.86)',
    labelText: '#4a4237',
    labelSelected: '#7a4b00',
    selectedMark: '#b07d1a',
    selectedOutline: '#8a5a00',
    doorLine: 'rgba(70, 85, 95, 0.55)',
    doorLineSelected: 'rgba(176, 125, 26, 0.95)',
    doorMark: 'rgba(55, 65, 72, 0.82)',
    doorMarkSelected: '#8a5a00',
    doorDot: 'rgba(55, 65, 72, 0.88)',
    crossLine: 'rgba(176, 125, 26, 0.9)',
    crossLineSelected: '#8a5a00',
    crossFill: 'rgba(255, 251, 243, 0.75)',
    subRoomStroke: 'rgba(255, 252, 246, 0.92)',
    contentOverflow: '#4a4033',
    spawnStroke: 'rgba(138, 90, 0, 0.9)',
    spawnFill: 'rgba(217, 164, 65, 0.22)',
    spawnDot: '#b07d1a',
    spawnText: '#fff8e8',
    exportBar: 'rgba(248, 244, 235, 0.96)',
    exportTitle: '#2a251d',
    exportSub: '#7a7264',
    exportBrand: 'rgba(151, 103, 15, 0.9)',
  },
};

function paletteFor(theme) {
  return PALETTES[theme === 'light' ? 'light' : 'dark'];
}

function roundedRect(ctx, x, y, w, h_, radius) {
  const r = Math.min(radius, w / 2, h_ / 2);
  ctx.beginPath();
  ctx.moveTo(x + r, y);
  ctx.arcTo(x + w, y, x + w, y + h_, r);
  ctx.arcTo(x + w, y + h_, x, y + h_, r);
  ctx.arcTo(x, y + h_, x, y, r);
  ctx.arcTo(x, y, x + w, y, r);
  ctx.closePath();
}

function textWithBackdrop(ctx, text, x, y, color, { font = '11px sans-serif', align = 'center', backdrop } = {}) {
  ctx.save();
  ctx.font = font;
  ctx.textAlign = align;
  ctx.textBaseline = 'middle';
  const metrics = ctx.measureText(text);
  const width = metrics.width + 8;
  const height = 15;
  ctx.fillStyle = backdrop || 'rgba(7, 11, 15, 0.72)';
  roundedRect(ctx, x - width / 2, y - height / 2, width, height, 3);
  ctx.fill();
  ctx.fillStyle = color;
  ctx.fillText(text, x, y);
  ctx.restore();
}

export class MapCanvas {
  constructor({ canvas, host, hoverCard, store }) {
    this.canvas = canvas;
    this.host = host;
    this.hoverCard = hoverCard;
    this.store = store;
    this.ctx = canvas.getContext('2d');
    this.theme = document.documentElement.dataset.theme === 'light' ? 'light' : 'dark';
    this.view = { scale: 22, offsetX: 0, offsetY: 0 };
    this.hover = null;
    this.pointer = null;
    this.pointers = new Map();
    this.pinch = null;
    this.suppressTap = false;
    this.lastTap = null;
    this.dragging = false;
    this.moved = false;
    this.dragStart = null;
    this.lastMapId = null;
    this.animating = false;

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
    canvas.addEventListener('contextmenu', (event) => event.preventDefault());

    this.resizeObserver = new ResizeObserver(() => this.resize());
    this.resizeObserver.observe(host);

    this.store.on('change', () => {
      const map = this.store.currentMap;
      if (map?.id !== this.lastMapId) {
        this.lastMapId = map?.id || null;
        window.requestAnimationFrame(() => this.fit());
      } else {
        this.render();
      }
    });
    this.store.on('selection', () => this.render());
    this.store.on('ui', () => {
      this.updateCursor();
      this.render();
    });

    this.resize();
  }

  setTheme(theme, { render = true } = {}) {
    this.theme = theme === 'light' ? 'light' : 'dark';
    if (render) this.render();
  }

  destroy() {
    this.resizeObserver.disconnect();
    this.canvas.removeEventListener('pointerdown', this.onPointerDown);
    this.canvas.removeEventListener('pointermove', this.onPointerMove);
    this.canvas.removeEventListener('pointerup', this.onPointerUp);
    this.canvas.removeEventListener('pointercancel', this.onPointerUp);
    this.canvas.removeEventListener('wheel', this.onWheel);
    this.canvas.removeEventListener('dblclick', this.onDoubleClick);
    this.canvas.removeEventListener('pointerleave', this.onPointerLeave);
  }

  get map() {
    return this.store.currentMap;
  }

  get width() {
    return this.host.clientWidth || 800;
  }

  get height() {
    return this.host.clientHeight || 600;
  }

  resize() {
    const dpr = window.devicePixelRatio || 1;
    const width = Math.max(1, this.host.clientWidth);
    const height = Math.max(1, this.host.clientHeight);
    this.canvas.width = Math.round(width * dpr);
    this.canvas.height = Math.round(height * dpr);
    this.canvas.style.width = `${width}px`;
    this.canvas.style.height = `${height}px`;
    this.ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    const orientationChanged =
      this.viewport &&
      (this.viewport.width > this.viewport.height) !== (width > height);
    if (this.map && (this.lastMapId !== this.map.id || !this.initialized || orientationChanged)) {
      this.lastMapId = this.map.id;
      this.initialized = true;
      this.fit(false);
      this.render();
    } else {
      this.render();
    }
    this.viewport = { width, height };
  }

  computeFit(width = this.width, height = this.height, padding = 46) {
    const map = this.map;
    if (!map) return { scale: 20, offsetX: width / 2, offsetY: height / 2 };
    const bounds = mapBounds(map.plots) || {
      minX: 0,
      minY: 0,
      maxX: (map.settings?.grid?.width || 1) - 1,
      maxY: (map.settings?.grid?.height || 1) - 1,
    };
    const minX = bounds.minX;
    const minY = bounds.minY;
    const maxX = bounds.maxX + 1;
    const maxY = bounds.maxY + 1;
    const worldW = Math.max(1, maxX - minX);
    const worldH = Math.max(1, maxY - minY);
    const scale = clamp(
      Math.min((width - padding * 2) / worldW, (height - padding * 2) / worldH),
      MIN_SCALE,
      MAX_SCALE,
    );
    return {
      scale,
      offsetX: (width - worldW * scale) / 2 - minX * scale,
      offsetY: (height - worldH * scale) / 2 - minY * scale,
    };
  }

  get fitPadding() {
    return this.host.clientWidth <= 600 ? 18 : 46;
  }

  fit(render = true) {
    this.view = this.computeFit(this.width, this.height, this.fitPadding);
    if (render) this.render();
    this.emitZoom();
  }

  emitZoom() {
    this.host.dispatchEvent(
      new CustomEvent('plotweave:zoom', { detail: { zoom: this.view.scale } }),
    );
  }

  setZoom(scale, center = { x: this.width / 2, y: this.height / 2 }) {
    const next = clamp(scale, MIN_SCALE, MAX_SCALE);
    const worldX = (center.x - this.view.offsetX) / this.view.scale;
    const worldY = (center.y - this.view.offsetY) / this.view.scale;
    this.view.scale = next;
    this.view.offsetX = center.x - worldX * next;
    this.view.offsetY = center.y - worldY * next;
    this.render();
    this.emitZoom();
  }

  zoomBy(factor, center) {
    this.setZoom(this.view.scale * factor, center);
  }

  screenToWorld(sx, sy) {
    return {
      x: (sx - this.view.offsetX) / this.view.scale,
      y: (sy - this.view.offsetY) / this.view.scale,
    };
  }

  screenToCell(sx, sy) {
    const world = this.screenToWorld(sx, sy);
    return { x: Math.floor(world.x), y: Math.floor(world.y) };
  }

  plotAtCell(x, y) {
    if (!this.map) return null;
    return (
      this.map.plots.find((plot) => plot.cells.some((cell) => cell.x === x && cell.y === y)) || null
    );
  }

  localPoint(event) {
    const rect = this.canvas.getBoundingClientRect();
    return { x: event.clientX - rect.left, y: event.clientY - rect.top };
  }

  updateCursor() {
    const tool = this.store.ui.tool;
    this.host.classList.toggle('tool-pan', tool === 'pan');
    this.host.classList.toggle('tool-spawn', tool === 'spawn');
    this.host.classList.toggle('dragging', this.dragging);
  }

  onPointerDown(event) {
    if (!this.map) return;
    if (event.button !== 0 && event.button !== 1) return;
    const point = this.localPoint(event);

    if (event.pointerType === 'touch') {
      this.pointers.set(event.pointerId, point);
      this.canvas.setPointerCapture(event.pointerId);
      if (this.pointers.size > 2) return;
      if (this.pointers.size === 2) {
        this.dragging = false;
        this.dragStart = null;
        this.moved = true;
        this.suppressTap = true;
        this.beginPinch();
        this.updateCursor();
        return;
      }
    }

    this.dragging = true;
    this.moved = false;
    this.dragStart = {
      pointerX: point.x,
      pointerY: point.y,
      offsetX: this.view.offsetX,
      offsetY: this.view.offsetY,
    };
    this.canvas.setPointerCapture(event.pointerId);
    this.updateCursor();
  }

  beginPinch() {
    const points = [...this.pointers.values()].slice(0, 2);
    if (points.length < 2) return;
    const mid = {
      x: (points[0].x + points[1].x) / 2,
      y: (points[0].y + points[1].y) / 2,
    };
    const distance = Math.max(
      1,
      Math.hypot(points[0].x - points[1].x, points[0].y - points[1].y),
    );
    this.pinch = {
      distance,
      scale: this.view.scale,
      worldX: (mid.x - this.view.offsetX) / this.view.scale,
      worldY: (mid.y - this.view.offsetY) / this.view.scale,
    };
  }

  updatePinch() {
    if (!this.pinch) return;
    const points = [...this.pointers.values()].slice(0, 2);
    if (points.length < 2) return;
    const mid = {
      x: (points[0].x + points[1].x) / 2,
      y: (points[0].y + points[1].y) / 2,
    };
    const distance = Math.hypot(points[0].x - points[1].x, points[0].y - points[1].y);
    const scale = clamp(
      this.pinch.scale * (distance / this.pinch.distance),
      MIN_SCALE,
      MAX_SCALE,
    );
    this.view.scale = scale;
    this.view.offsetX = mid.x - this.pinch.worldX * scale;
    this.view.offsetY = mid.y - this.pinch.worldY * scale;
    this.render();
    this.emitZoom();
  }

  onPointerMove(event) {
    const point = this.localPoint(event);
    this.pointer = point;

    if (event.pointerType === 'touch' && this.pointers.has(event.pointerId)) {
      this.pointers.set(event.pointerId, point);
    }
    if (this.pinch) {
      this.updatePinch();
      return;
    }

    if (this.dragging && this.dragStart) {
      const dx = point.x - this.dragStart.pointerX;
      const dy = point.y - this.dragStart.pointerY;
      if (Math.abs(dx) + Math.abs(dy) > 3) this.moved = true;
      if (this.moved) {
        this.view.offsetX = this.dragStart.offsetX + dx;
        this.view.offsetY = this.dragStart.offsetY + dy;
      }
    }

    this.updateHover(point);
    this.render();
  }

  onPointerUp(event) {
    if (!this.map) return;
    const point = this.localPoint(event);
    if (event.pointerType === 'touch') {
      const wasPinching = Boolean(this.pinch) || this.suppressTap;
      this.pointers.delete(event.pointerId);
      if (this.pinch) this.pinch = null;
      if (wasPinching) {
        this.dragging = false;
        this.dragStart = null;
        this.moved = false;
        if (this.pointers.size === 0) this.suppressTap = false;
        this.updateCursor();
        this.render();
        return;
      }
    }
    const wasDragging = this.dragging;
    const moved = this.moved;
    this.dragging = false;
    this.dragStart = null;
    this.moved = false;
    this.updateCursor();
    if (!wasDragging || moved) return;

    const cell = this.screenToCell(point.x, point.y);
    const plot = this.plotAtCell(cell.x, cell.y);
    if (this.store.ui.tool === 'spawn') {
      const result = this.store.setSpawnAtCell(cell.x, cell.y);
      this.store.emit('toast', {
        message: result.ok ? `出生点已设为 ${plot?.id || ''} (${cell.x}, ${cell.y})` : result.message,
        type: result.ok ? 'success' : 'error',
      });
      this.render();
      return;
    }

    if (plot) {
      const isTouch = event.pointerType === 'touch';
      const now = performance.now();
      const previous = this.lastTap;
      this.store.select({ type: 'plot', id: plot.id });
      if (isTouch) {
        if (
          this.store.ui.tool === 'select' &&
          previous &&
          previous.plotId === plot.id &&
          now - previous.time < 320 &&
          Math.hypot(point.x - previous.x, point.y - previous.y) < 24
        ) {
          this.lastTap = null;
          this.zoomToPlot(plot);
          return;
        }
        this.lastTap = { plotId: plot.id, time: now, x: point.x, y: point.y };
        return;
      }
      this.emitPlotFocus(plot);
    }
  }

  onDoubleClick(event) {
    const point = this.localPoint(event);
    const cell = this.screenToCell(point.x, point.y);
    const plot = this.plotAtCell(cell.x, cell.y);
    if (plot) {
      this.emitPlotFocus(plot);
      this.zoomToPlot(plot);
    } else {
      this.fit();
    }
  }

  onPointerLeave() {
    this.hover = null;
    this.pointer = null;
    this.hoverCard.hidden = true;
    this.render();
  }

  onWheel(event) {
    if (!this.map) return;
    event.preventDefault();
    const point = this.localPoint(event);
    const factor = Math.exp(-event.deltaY * 0.0015);
    this.zoomBy(factor, point);
  }

  emitPlotFocus(plot) {
    this.host.dispatchEvent(new CustomEvent('plotweave:focus-plot', { detail: { plot } }));
  }

  zoomToPlot(plot) {
    const bounds = plotBounds(plot);
    const worldW = Math.max(1, bounds.maxX - bounds.minX + 1);
    const worldH = Math.max(1, bounds.maxY - bounds.minY + 1);
    const scale = clamp(
      Math.min((this.width - 160) / worldW, (this.height - 160) / worldH),
      MIN_SCALE,
      44,
    );
    this.view.scale = scale;
    const center = plotCenter(plot);
    this.view.offsetX = this.width / 2 - center.x * scale;
    this.view.offsetY = this.height / 2 - center.y * scale;
    this.render();
    this.emitZoom();
  }

  updateHover(point) {
    if (!this.map || !point) {
      this.hoverCard.hidden = true;
      return;
    }
    const cell = this.screenToCell(point.x, point.y);
    const plot = this.plotAtCell(cell.x, cell.y);
    if (!plot) {
      this.hover = null;
      this.hoverCard.hidden = true;
      return;
    }
    this.hover = { plotId: plot.id, x: cell.x, y: cell.y };
    const kind = plotKindById(this.store.project, plot.kindId);
    const region = regionById(this.store.project, plot.regionId);
    const degree = plotDegree(this.map, plot.id);
    const rooms = plot.subRooms?.length || 0;
    const contents = CONTENT_TYPES.reduce(
      (total, type) => total + (plot.contents?.[type.id]?.length || 0),
      0,
    );
    this.hoverCard.replaceChildren(
      h('strong', null, kind?.name || plot.kindId),
      h(
        'div',
        null,
        region ? `${region.name} · ${plot.axis === 'h' ? '横向' : '纵向'} ${plot.length} 格` : '',
      ),
      h(
        'div',
        { class: 'mono' },
        `${plot.id} · 格 ${cell.x},${cell.y} · 连接 ${degree}/${kind?.connections.max ?? '?'}`,
      ),
      h(
        'div',
        { class: 'mono' },
        `${rooms} 子房间 · ${contents} 内容${plot.id === this.map.spawn?.plotId ? ' · 出生点' : ''}`,
      ),
    );
    const cardW = 240;
    const x = Math.min(point.x + 16, this.width - cardW - 8);
    const y = Math.min(point.y + 16, this.height - 96);
    this.hoverCard.style.left = `${Math.max(8, x)}px`;
    this.hoverCard.style.top = `${Math.max(8, y)}px`;
    this.hoverCard.hidden = false;
  }

  render() {
    if (!this.map) {
      this.ctx.clearRect(0, 0, this.width, this.height);
      return;
    }
    this.drawScene(this.ctx, this.width, this.height, this.view, { exportMode: false });
  }

  drawScene(ctx, width, height, view, { exportMode = false } = {}) {
    const map = this.map;
    const project = this.store.project;
    if (!map) return;
    const scale = view.scale;
    const palette = paletteFor(this.theme);
    const mapX = (x) => view.offsetX + x * scale;
    const mapY = (y) => view.offsetY + y * scale;
    const cellRect = (x, y) => ({
      x: mapX(x),
      y: mapY(y),
      w: scale,
      h: scale,
    });

    // 背景
    ctx.save();
    ctx.fillStyle = exportMode ? palette.exportBackground : palette.background;
    ctx.fillRect(0, 0, width, height);

    // 地图网格范围
    const grid = map.settings.grid;
    const gx = mapX(0);
    const gy = mapY(0);
    const gw = grid.width * scale;
    const gh = grid.height * scale;
    ctx.fillStyle = palette.gridFill;
    ctx.fillRect(gx, gy, gw, gh);
    ctx.strokeStyle = palette.gridBorder;
    ctx.lineWidth = 1;
    ctx.strokeRect(gx + 0.5, gy + 0.5, gw, gh);

    if (this.store.ui.showGrid !== false || exportMode) {
      const step = scale >= 18 ? 1 : scale >= 9 ? 2 : 5;
      ctx.strokeStyle = palette.gridLine;
      ctx.lineWidth = 1;
      ctx.beginPath();
      for (let x = 0; x <= grid.width; x += step) {
        const px = Math.round(mapX(x)) + 0.5;
        ctx.moveTo(px, gy);
        ctx.lineTo(px, gy + gh);
      }
      for (let y = 0; y <= grid.height; y += step) {
        const py = Math.round(mapY(y)) + 0.5;
        ctx.moveTo(gx, py);
        ctx.lineTo(gx + gw, py);
      }
      ctx.stroke();
      // 每 5 格加粗
      ctx.strokeStyle = palette.gridMajor;
      ctx.beginPath();
      for (let x = 0; x <= grid.width; x += 5) {
        const px = Math.round(mapX(x)) + 0.5;
        ctx.moveTo(px, gy);
        ctx.lineTo(px, gy + gh);
      }
      for (let y = 0; y <= grid.height; y += 5) {
        const py = Math.round(mapY(y)) + 0.5;
        ctx.moveTo(gx, py);
        ctx.lineTo(gx + gw, py);
      }
      ctx.stroke();
    }
    ctx.restore();

    // 区域色底
    if (this.store.ui.showRegions !== false) {
      for (const plot of map.plots) {
        const region = regionById(project, plot.regionId);
        if (!region) continue;
        ctx.fillStyle = withAlpha(region.color, palette.regionFill);
        for (const cell of plot.cells) {
          const rect = cellRect(cell.x, cell.y);
          ctx.fillRect(rect.x, rect.y, rect.w, rect.h);
        }
      }
    }

    // 区域名（质心）
    if (this.store.ui.showRegions !== false) {
      const groups = new Map();
      for (const plot of map.plots) {
        const entry = groups.get(plot.regionId) || { count: 0, sx: 0, sy: 0 };
        const center = plotCenter(plot);
        entry.count += 1;
        entry.sx += center.x;
        entry.sy += center.y;
        groups.set(plot.regionId, entry);
      }
      ctx.save();
      ctx.textAlign = 'center';
      ctx.textBaseline = 'middle';
      ctx.font = `600 ${Math.max(9, Math.min(13, scale * 0.5))}px "Segoe UI", sans-serif`;
      for (const [regionId, entry] of groups) {
        const region = regionById(project, regionId);
        if (!region) continue;
        const x = mapX(entry.sx / entry.count + 0.5);
        const y = mapY(entry.sy / entry.count + 0.5);
        ctx.fillStyle = withAlpha(region.color, palette.regionLabel);
        const text = (region.shortName || region.name).split('').join(' ');
        ctx.fillText(text, x, y);
      }
      ctx.restore();
    }

    // 地块间连接（先画，避免压住格子）
    const selectedId = this.store.selection.type === 'plot' ? this.store.selection.id : null;
    for (const link of map.links || []) {
      if (link.type === 'door') this.drawDoor(ctx, map, link, mapX, mapY, scale, selectedId, palette);
      else this.drawCross(ctx, map, link, mapX, mapY, scale, selectedId, palette);
    }

    // 子房间底色 / 地块格
    for (const plot of map.plots) {
      this.drawPlot(ctx, plot, {
        mapX,
        mapY,
        scale,
        selected: plot.id === selectedId,
        hovered: this.hover?.plotId === plot.id,
        project,
        palette,
      });
    }

    // 子房间标记
    for (const plot of map.plots) {
      for (const room of plot.subRooms || []) {
        this.drawSubRoom(ctx, room, { mapX, mapY, scale, project, palette, hovered: this.hover?.plotId === plot.id });
      }
    }

    // 内容标记
    if (this.store.ui.showContents !== false) {
      for (const plot of map.plots) this.drawContents(ctx, plot, { mapX, mapY, scale, project, palette });
    }

    // 出生点
    if (map.spawn) this.drawSpawn(ctx, map.spawn, { mapX, mapY, scale, palette });

    // 地块名
    if (this.store.ui.showLabels !== false) {
      for (const plot of map.plots) {
        this.drawPlotLabel(ctx, plot, { mapX, mapY, scale, project, palette, selected: plot.id === selectedId });
      }
    }

    // 扩展图层
    try {
      for (const layer of registry.listCanvasLayers()) {
        ctx.save();
        layer.draw(ctx, {
          map,
          project: this.store.project,
          view,
          mapX,
          mapY,
          scale,
          selection: this.store.selection,
          ui: this.store.ui,
          exportMode,
        });
        ctx.restore();
      }
    } catch (error) {
      console.warn('[PlotWeave] 扩展图层绘制失败', error);
    }

    // 导出时的标题与图例
    if (exportMode) {
      ctx.save();
      ctx.fillStyle = palette.exportBar;
      ctx.fillRect(0, 0, width, 46);
      ctx.fillStyle = palette.exportTitle;
      ctx.font = '600 16px "Segoe UI", sans-serif';
      ctx.textAlign = 'left';
      ctx.textBaseline = 'middle';
      ctx.fillText(project.meta?.name || 'PlotWeave', 18, 18);
      ctx.fillStyle = palette.exportSub;
      ctx.font = '10px Consolas, monospace';
      ctx.fillText(`seed ${map.seed} · ${map.plots.length} plots · ${map.links.length} links`, 18, 34);
      ctx.textAlign = 'right';
      ctx.fillStyle = palette.exportBrand;
      ctx.fillText('PLOTWEAVE', width - 18, 24);
      ctx.restore();
    }
  }

  drawPlot(ctx, plot, { mapX, mapY, scale, selected, hovered, project, palette }) {
    const region = regionById(project, plot.regionId);
    const kind = plotKindById(project, plot.kindId);
    const base = /^#[0-9a-f]{6}$/i.test(kind?.visual?.color || '')
      ? kind.visual.color
      : region?.color || palette.plotFallback;
    const gap = Math.max(1.2, scale * 0.075);
    const radius = Math.max(1.5, Math.min(4, scale * 0.16));
    const cells = plot.cells;

    // 连接同一地块相邻格的「廊道带」
    ctx.fillStyle = withAlpha(base, selected ? 0.34 : 0.22);
    for (let index = 0; index < cells.length - 1; index += 1) {
      const a = cells[index];
      const b = cells[index + 1];
      const ax = mapX(a.x);
      const ay = mapY(a.y);
      const bx = mapX(b.x);
      const by = mapY(b.y);
      const x = Math.min(ax, bx) + (plot.axis === 'h' ? scale - gap : gap);
      const y = Math.min(ay, by) + (plot.axis === 'v' ? scale - gap : gap);
      const w = plot.axis === 'h' ? gap + 1 : scale - gap * 2;
      const h_ = plot.axis === 'v' ? gap + 1 : scale - gap * 2;
      ctx.fillRect(x, y, w, h_);
    }

    for (const cell of cells) {
      const x = mapX(cell.x) + gap;
      const y = mapY(cell.y) + gap;
      const w = scale - gap * 2;
      const h_ = scale - gap * 2;
      roundedRect(ctx, x, y, w, h_, radius);
      ctx.fillStyle = withAlpha(base, selected ? 0.46 : hovered ? 0.38 : 0.27);
      ctx.fill();
      ctx.lineWidth = selected ? 2 : 1;
      ctx.strokeStyle = selected ? palette.selectedOutline : withAlpha(base, 0.58);
      ctx.stroke();

      // 轴向细线
      if (scale >= 12) {
        ctx.strokeStyle = withAlpha(base, selected ? 0.55 : 0.25);
        ctx.lineWidth = 1;
        ctx.beginPath();
        if (plot.axis === 'h') {
          ctx.moveTo(x + w * 0.28, y + h_ / 2);
          ctx.lineTo(x + w * 0.72, y + h_ / 2);
        } else {
          ctx.moveTo(x + w / 2, y + h_ * 0.28);
          ctx.lineTo(x + w / 2, y + h_ * 0.72);
        }
        ctx.stroke();
      }
    }

    // 选中角标
    if (selected && scale >= 10) {
      const first = cells[0];
      ctx.fillStyle = palette.selectedMark;
      ctx.beginPath();
      ctx.arc(mapX(first.x) + scale * 0.78, mapY(first.y) + scale * 0.22, Math.max(2, scale * 0.06), 0, Math.PI * 2);
      ctx.fill();
    }
  }

  drawDoor(ctx, map, link, mapX, mapY, scale, selectedId, palette) {
    const plotA = map.plots.find((plot) => plot.id === link.a.plotId);
    const plotB = map.plots.find((plot) => plot.id === link.b.plotId);
    if (!plotA || !plotB) return;
    const cellA = plotA.cells[link.a.cellIndex];
    const cellB = plotB.cells[link.b.cellIndex];
    if (!cellA || !cellB) return;
    const ax = mapX(cellA.x) + scale / 2;
    const ay = mapY(cellA.y) + scale / 2;
    const bx = mapX(cellB.x) + scale / 2;
    const by = mapY(cellB.y) + scale / 2;
    const selected = selectedId === plotA.id || selectedId === plotB.id;
    const dir = DIRECTIONS[link.dir] || { dx: 1, dy: 0 };

    ctx.save();
    ctx.strokeStyle = selected ? palette.doorLineSelected : palette.doorLine;
    ctx.lineWidth = Math.max(1, scale * 0.045);
    ctx.beginPath();
    ctx.moveTo(ax, ay);
    ctx.lineTo(bx, by);
    ctx.stroke();

    // 门的位置在共享边中点
    const mx = (ax + bx) / 2;
    const my = (ay + by) / 2;
    const nx = -dir.dy;
    const ny = dir.dx;
    const arm = Math.max(2.5, scale * 0.14);
    ctx.strokeStyle = selected ? palette.doorMarkSelected : palette.doorMark;
    ctx.lineWidth = Math.max(1, scale * 0.04);
    ctx.beginPath();
    ctx.moveTo(mx + nx * arm, my + ny * arm);
    ctx.lineTo(mx - nx * arm, my - ny * arm);
    ctx.stroke();

    ctx.fillStyle = selected ? palette.doorMarkSelected : palette.doorDot;
    ctx.beginPath();
    ctx.arc(mx, my, Math.max(1.3, scale * 0.035), 0, Math.PI * 2);
    ctx.fill();
    ctx.restore();
  }

  drawCross(ctx, map, link, mapX, mapY, scale, selectedId, palette) {
    const plotA = map.plots.find((plot) => plot.id === link.a.plotId);
    if (!plotA) return;
    const cell = plotA.cells[link.a.cellIndex] || link.cell;
    if (!cell) return;
    const x = mapX(cell.x) + scale / 2;
    const y = mapY(cell.y) + scale / 2;
    const selected = selectedId === plotA.id || selectedId === link.b.plotId;
    const radius = Math.max(3, scale * 0.22);
    ctx.save();
    ctx.strokeStyle = selected ? palette.crossLineSelected : palette.crossLine;
    ctx.fillStyle = palette.crossFill;
    ctx.lineWidth = Math.max(1, scale * 0.045);
    ctx.beginPath();
    ctx.moveTo(x, y - radius);
    ctx.lineTo(x + radius, y);
    ctx.lineTo(x, y + radius);
    ctx.lineTo(x - radius, y);
    ctx.closePath();
    ctx.fill();
    ctx.stroke();
    const arm = radius * 0.55;
    ctx.beginPath();
    ctx.moveTo(x - arm, y - arm);
    ctx.lineTo(x + arm, y + arm);
    ctx.moveTo(x + arm, y - arm);
    ctx.lineTo(x - arm, y + arm);
    ctx.stroke();
    ctx.restore();
  }

  drawSubRoom(ctx, room, { mapX, mapY, scale, project, palette, hovered }) {
    const kind = subRoomKindById(project, room.kindId);
    if (!kind) return;
    const x = mapX(room.x) + scale / 2;
    const y = mapY(room.y) + scale / 2;
    const radius = Math.max(2.4, Math.min(6, scale * 0.19));
    ctx.save();
    ctx.translate(x, y);
    ctx.rotate(Math.PI / 4);
    ctx.fillStyle = withAlpha(kind.color || '#c9a227', hovered ? 0.96 : 0.82);
    ctx.strokeStyle = palette.subRoomStroke;
    ctx.lineWidth = 1;
    ctx.fillRect(-radius, -radius, radius * 2, radius * 2);
    ctx.strokeRect(-radius, -radius, radius * 2, radius * 2);
    ctx.restore();
  }

  drawContents(ctx, plot, { mapX, mapY, scale, project, palette }) {
    if (scale < 10) return;
    const contents = plot.contents || {};
    const types = CONTENT_TYPES.filter((type) => (contents[type.id] || []).length > 0);
    if (types.length === 0) return;
    const cell = plot.cells[Math.min(plot.cells.length - 1, Math.floor(plot.cells.length / 2))];
    const x = mapX(cell.x) + scale * 0.16;
    const y = mapY(cell.y) + scale - scale * 0.13;
    const r = Math.max(1.2, Math.min(2.4, scale * 0.045));
    let offset = 0;
    for (const type of types) {
      const count = contents[type.id].length;
      ctx.save();
      ctx.fillStyle = type.color;
      ctx.globalAlpha = 0.9;
      for (let index = 0; index < Math.min(count, 4); index += 1) {
        ctx.beginPath();
        ctx.arc(x + offset + index * (r * 2.4), y, r, 0, Math.PI * 2);
        ctx.fill();
      }
      if (count > 4) {
        ctx.globalAlpha = 0.8;
        ctx.fillStyle = palette.contentOverflow;
        ctx.font = `600 ${Math.max(6, r * 2.4)}px Consolas, monospace`;
        ctx.textAlign = 'left';
        ctx.textBaseline = 'middle';
        ctx.fillText(`+${count - 4}`, x + offset + 4 * (r * 2.4), y);
      }
      ctx.restore();
      offset += Math.min(count, 4) * (r * 2.4) + 4;
    }
  }

  drawSpawn(ctx, spawn, { mapX, mapY, scale, palette }) {
    const x = mapX(spawn.x) + scale / 2;
    const y = mapY(spawn.y) + scale / 2;
    const r = Math.max(3, scale * 0.24);
    ctx.save();
    ctx.strokeStyle = palette.spawnStroke;
    ctx.fillStyle = palette.spawnFill;
    ctx.lineWidth = Math.max(1.2, scale * 0.045);
    ctx.beginPath();
    ctx.arc(x, y, r, 0, Math.PI * 2);
    ctx.fill();
    ctx.stroke();
    ctx.beginPath();
    ctx.arc(x, y, r * 0.45, 0, Math.PI * 2);
    ctx.fillStyle = palette.spawnDot;
    ctx.fill();
    ctx.fillStyle = palette.spawnText;
    ctx.font = `700 ${Math.max(6, r * 1.1)}px "Segoe UI", sans-serif`;
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.fillText('S', x, y + 0.5);
    ctx.restore();
  }

  drawPlotLabel(ctx, plot, { mapX, mapY, scale, project, palette, selected }) {
    if (scale < 8.5) return;
    const kind = plotKindById(project, plot.kindId);
    if (!kind) return;
    const center = plotCenter(plot);
    const x = mapX(center.x + 0.5);
    const y = mapY(center.y + 0.5);
    const label = kind.shortName || kind.name;
    const font = `600 ${Math.max(8, Math.min(11, scale * 0.38))}px "Segoe UI", "Microsoft YaHei", sans-serif`;

    if (plot.axis === 'v') {
      ctx.save();
      ctx.translate(x, y);
      ctx.rotate(-Math.PI / 2);
      textWithBackdrop(ctx, label, 0, 0, selected ? palette.labelSelected : palette.labelText, {
        font,
        backdrop: palette.labelBackdrop,
      });
      ctx.restore();
    } else {
      textWithBackdrop(ctx, label, x, y, selected ? palette.labelSelected : palette.labelText, {
        font,
        backdrop: palette.labelBackdrop,
      });
    }
  }

  async toPngBlob({ scale = 2, padding = 52 } = {}) {
    const map = this.map;
    if (!map) return null;
    const project = this.store.project;
    const bounds = mapBounds(map.plots) || {
      minX: 0,
      minY: 0,
      maxX: map.settings.grid.width - 1,
      maxY: map.settings.grid.height - 1,
    };
    const grid = map.settings.grid;
    const minX = Math.min(bounds.minX, 0);
    const minY = Math.min(bounds.minY, 0);
    const maxX = Math.max(bounds.maxX + 1, grid.width);
    const maxY = Math.max(bounds.maxY + 1, grid.height);
    const worldW = maxX - minX;
    const worldH = maxY - minY;
    const baseScale = clamp(Math.min(1500 / worldW, 1000 / worldH), 8, 34);
    const viewScale = baseScale * scale;
    const width = Math.ceil(worldW * viewScale + padding * 2);
    const height = Math.ceil(worldH * viewScale + padding * 2 + 46);
    const canvas = document.createElement('canvas');
    canvas.width = width;
    canvas.height = height;
    const ctx = canvas.getContext('2d');
    const view = {
      scale: viewScale,
      offsetX: padding - minX * viewScale,
      offsetY: padding + 46 - minY * viewScale,
    };
    this.drawScene(ctx, width, height, view, { exportMode: true });
    return new Promise((resolve) => canvas.toBlob((blob) => resolve(blob), 'image/png'));
  }
}
