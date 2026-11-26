/**
 * 应用状态仓库。
 *
 * 负责：工程 / 地图集合 / 选择 / 撤销栈 / localStorage 持久化。
 * 不依赖具体 UI，只通过事件通知。核心数据结构仍然由 core 层校验。
 */

import { normalizeProject, validateProject } from '../core/project.js';
import {
  createBlankProject,
  createCatalogItem,
  createPlotGroup,
  createPlotKind,
  createProfile,
  createRegion,
  createSubRoomKind,
} from '../core/project.js';
import { createDefaultProject } from '../core/project.js';
import { profileById } from '../core/rules.js';
import { GENERATOR_ID } from '../core/generator.js';
import { registry } from '../core/registry.js';
import { computeMapStats, validateMap } from '../core/validate-map.js';
import { Emitter } from '../core/events.js';
import { CONTENT_TYPE_IDS } from '../core/constants.js';
import {
  generateFloorMap,
  normalizeFloorProfile,
  recomputeFloorMetrics,
  recomputeFloorProgression,
} from '../core/floor-generator.js';
import { validateFloorMap } from '../core/floor-validator.js';
import {
  normalizeCellTypes,
  normalizeStripTemplate,
  stripTemplateStructureKey,
  validateStripTemplate,
  validateTemplateFamilies,
} from '../core/strip-templates.js';

const STORAGE_KEY = 'plotweave:workspace:v2';
const MAX_HISTORY = 60;

function extensionIssues(scope, payload, context) {
  return registry.runValidators(scope, payload, context).map((issue) => ({
    level: issue?.level === 'warning' ? 'warning' : issue?.level === 'info' ? 'info' : 'error',
    code: issue?.code || `extension:${scope}`,
    message: issue?.message || String(issue),
  }));
}

function errorMessages(issues) {
  return issues.filter((issue) => issue.level === 'error').map((issue) => issue.message);
}

function warningMessages(issues) {
  return issues.filter((issue) => issue.level === 'warning').map((issue) => issue.message);
}
const MAX_MAPS = 12;

function clone(value) {
  if (typeof structuredClone === 'function') return structuredClone(value);
  return JSON.parse(JSON.stringify(value));
}

function revalidate(map, project) {
  if (!map) return;
  const profile = profileById(project, map.profileId) || project.profiles[0];
  const validation = validateMap(project, map, { profile });
  map.stats = validation.stats || computeMapStats(map);
  map.issues = validation.issues;
  map.warnings = validation.issues.filter((issue) => issue.level === 'warning');
  map.errors = validation.issues.filter((issue) => issue.level === 'error');
}

function safeState(raw) {
  if (!raw || typeof raw !== 'object' || !raw.project) return null;
  const project = normalizeProject(raw.project);
  const maps = Array.isArray(raw.maps) ? raw.maps.filter((map) => map && typeof map === 'object') : [];
  for (const map of maps) revalidate(map, project);
  const currentMapId = maps.some((map) => map.id === raw.currentMapId)
    ? raw.currentMapId
    : maps[0]?.id || null;
  const floorMaps = Array.isArray(raw.floorMaps)
    ? raw.floorMaps.filter((m) => m && typeof m === 'object' && Array.isArray(m.strips))
    : [];
  for (const fm of floorMaps) {
    recomputeFloorProgression(fm, fm.progression, project.dungeon.cellTypes);
    recomputeFloorMetrics(fm, project.dungeon.cellTypes);
  }
  const currentFloorMapId = floorMaps.some((m) => m.id === raw.currentFloorMapId)
    ? raw.currentFloorMapId
    : floorMaps[0]?.id || null;
  const selection =
    raw.selection && typeof raw.selection === 'object' ? raw.selection : { type: 'project' };
  const mode = ['templates', 'pools', 'floor', 'world'].includes(raw.mode) ? raw.mode : 'pools';
  return { project, maps, currentMapId, floorMaps, currentFloorMapId, selection, mode };
}

export class AppStore extends Emitter {
  constructor() {
    super();
    this.state = {
      project: createDefaultProject(),
      maps: [],
      currentMapId: null,
      floorMaps: [],
      currentFloorMapId: null,
      selectedStripId: null,
      selection: { type: 'project', tab: 'structure' },
      ui: {
        mode: 'pools',
        tab: 'structure',
        filter: '',
        showGrid: true,
        showLabels: true,
        showContents: true,
        showRegions: true,
        floorShowGrid: true,
        floorShowCells: true,
        floorShowLevels: true,
        floorShowGraph: false,
        floorShowHeatmap: false,
        floorShowRegions: true,
        tool: 'select',
        zoom: 1,
      },
    };
    this.history = [];
    this.future = [];
    this.storageStatus = 'idle';
    this.#load();
  }

  get project() {
    return this.state.project;
  }

  get dungeon() {
    return this.state.project.dungeon;
  }

  get maps() {
    return this.state.maps;
  }

  get currentMap() {
    return this.state.maps.find((map) => map.id === this.state.currentMapId) || null;
  }

  get floorMaps() {
    return this.state.floorMaps;
  }

  get currentFloorMap() {
    return this.state.floorMaps.find((m) => m.id === this.state.currentFloorMapId) || null;
  }

  get selectedStripId() {
    return this.state.selectedStripId;
  }

  get activeFloorProfile() {
    return this.dungeon.floorProfiles[0];
  }

  get mode() {
    return this.state.ui.mode;
  }

  get selection() {
    return this.state.selection;
  }

  get ui() {
    return this.state.ui;
  }

  get canUndo() {
    return this.history.length > 0;
  }

  get canRedo() {
    return this.future.length > 0;
  }

  /* ---------------- persistence ---------------- */

  #load() {
    try {
      const text = localStorage.getItem(STORAGE_KEY);
      if (!text) {
        this.storageStatus = 'new';
        return;
      }
      const parsed = safeState(JSON.parse(text));
      if (!parsed) {
        this.storageStatus = 'invalid';
        return;
      }
      this.state.project = parsed.project;
      this.state.maps = parsed.maps;
      this.state.currentMapId = parsed.currentMapId;
      this.state.floorMaps = parsed.floorMaps;
      this.state.currentFloorMapId = parsed.currentFloorMapId;
      this.state.selection = parsed.selection;
      this.state.ui.mode = parsed.mode;
      this.storageStatus = 'restored';
    } catch (error) {
      console.warn('[PlotWeave] 读取本地工作区失败', error);
      this.storageStatus = 'error';
    }
  }

  save() {
    try {
      const payload = {
        version: 1,
        savedAt: new Date().toISOString(),
        project: this.state.project,
        maps: this.state.maps.slice(0, MAX_MAPS),
        currentMapId: this.state.currentMapId,
        floorMaps: this.state.floorMaps.slice(0, MAX_MAPS),
        currentFloorMapId: this.state.currentFloorMapId,
        mode: this.state.ui.mode,
        selection: this.state.selection,
      };
      localStorage.setItem(STORAGE_KEY, JSON.stringify(payload));
      this.storageStatus = 'saved';
      return true;
    } catch (error) {
      console.warn('[PlotWeave] 保存本地工作区失败', error);
      this.storageStatus = 'full';
      return false;
    }
  }

  clearStorage() {
    try {
      localStorage.removeItem(STORAGE_KEY);
    } catch {
      /* ignore */
    }
  }

  /* ---------------- history ---------------- */

  #snapshot() {
    return {
      project: clone(this.state.project),
      maps: clone(this.state.maps),
      currentMapId: this.state.currentMapId,
      floorMaps: clone(this.state.floorMaps),
      currentFloorMapId: this.state.currentFloorMapId,
      selectedStripId: this.state.selectedStripId,
      selection: clone(this.state.selection),
    };
  }

  #pushHistory(label) {
    this.history.push({ label, at: Date.now(), ...this.#snapshot() });
    if (this.history.length > MAX_HISTORY) this.history.shift();
    this.future = [];
  }

  #restore(snapshot) {
    this.state.project = normalizeProject(snapshot.project);
    this.state.maps = snapshot.maps;
    this.state.currentMapId = snapshot.currentMapId;
    this.state.floorMaps = snapshot.floorMaps || [];
    this.state.currentFloorMapId = snapshot.currentFloorMapId || null;
    this.state.selectedStripId = snapshot.selectedStripId || null;
    this.state.selection = snapshot.selection;
    for (const map of this.state.maps) revalidate(map, this.state.project);
    for (const fm of this.state.floorMaps) {
      recomputeFloorProgression(fm, fm.progression, this.state.project.dungeon.cellTypes);
      recomputeFloorMetrics(fm, this.state.project.dungeon.cellTypes);
    }
    this.save();
    this.emit('change', { reason: 'restore' });
  }

  undo() {
    if (this.history.length === 0) return null;
    const entry = this.history.pop();
    this.future.push({
      label: entry.label,
      at: Date.now(),
      ...this.#snapshot(),
    });
    this.#restore(entry);
    return entry.label;
  }

  redo() {
    if (this.future.length === 0) return null;
    const entry = this.future.pop();
    this.history.push({
      label: entry.label,
      at: Date.now(),
      ...this.#snapshot(),
    });
    this.#restore(entry);
    return entry.label;
  }

  /* ---------------- generic commits ---------------- */

  /**
   * 修改工程。mutator 直接原地修改 state.project，返回 false 可取消。
   */
  commitProject(mutator, { label = '编辑工程', history = true, silent = false } = {}) {
    const before = history ? this.#snapshot() : null;
    const result = mutator(this.state.project);
    if (result === false) return false;
    this.state.project = normalizeProject(this.state.project);
    this.state.project.meta.updatedAt = new Date().toISOString();
    if (before) {
      this.history.push({ label, at: Date.now(), ...before });
      if (this.history.length > MAX_HISTORY) this.history.shift();
      this.future = [];
    }
    this.save();
    if (!silent) this.emit('change', { reason: 'project', label });
    return true;
  }

  /**
   * 修改当前地图（出生点、显示内容等）。修改后自动重算统计与校验。
   */
  commitMap(mutator, { label = '编辑地图', history = true } = {}) {
    const map = this.currentMap;
    if (!map) return false;
    const before = history ? this.#snapshot() : null;
    const result = mutator(map, this.state.project);
    if (result === false) return false;
    revalidate(map, this.state.project);
    if (before) {
      this.history.push({ label, at: Date.now(), ...before });
      if (this.history.length > MAX_HISTORY) this.history.shift();
      this.future = [];
    }
    this.save();
    this.emit('change', { reason: 'map', label });
    return true;
  }

  /* ---------------- selection / ui ---------------- */

  select(selection) {
    this.state.selection = { ...this.state.selection, ...selection };
    this.emit('selection', this.state.selection);
  }

  setTab(tab) {
    this.state.ui.tab = tab;
    this.state.selection = { ...this.state.selection, tab };
    this.emit('ui', { tab });
  }

  setFilter(filter) {
    this.state.ui.filter = filter;
    this.emit('ui', { filter });
  }

  setUi(patch, { silent = false } = {}) {
    Object.assign(this.state.ui, patch);
    if (!silent) this.emit('ui', patch);
  }

  toggleUi(key) {
    this.state.ui[key] = !this.state.ui[key];
    this.emit('ui', { [key]: this.state.ui[key] });
    return this.state.ui[key];
  }

  /* ---------------- lookup helpers ---------------- */

  findRegion(id) {
    return this.project.regions.find((region) => region.id === id) || null;
  }

  findPlotKind(id) {
    return this.project.plotKinds.find((kind) => kind.id === id) || null;
  }

  findPlotGroup(id) {
    return (this.project.plotGroups || []).find((group) => group.id === id) || null;
  }

  descendantGroupIds(id) {
    const groups = this.project.plotGroups || [];
    const result = new Set();
    const visit = (parentId) => {
      for (const group of groups) {
        if (group.parentGroupId !== parentId || result.has(group.id)) continue;
        result.add(group.id);
        visit(group.id);
      }
    };
    visit(id);
    return result;
  }

  findSubRoomKind(id) {
    return this.project.subRoomKinds.find((room) => room.id === id) || null;
  }

  findProfile(id) {
    return this.project.profiles.find((profile) => profile.id === id) || null;
  }

  findCatalogItem(type, id) {
    return (this.project.catalogs[type] || []).find((item) => item.id === id) || null;
  }

  findSelected() {
    const selection = this.state.selection;
    switch (selection.type) {
      case 'region':
        return this.findRegion(selection.id);
      case 'plotKind':
        return this.findPlotKind(selection.id);
      case 'plotGroup':
        return this.findPlotGroup(selection.id);
      case 'subRoomKind':
        return this.findSubRoomKind(selection.id);
      case 'profile':
        return this.findProfile(selection.id);
      case 'catalog':
        return this.findCatalogItem(selection.contentType, selection.id);
      case 'plot':
        return this.currentMap?.plots.find((plot) => plot.id === selection.id) || null;
      default:
        return null;
    }
  }

  /* ---------------- entities ---------------- */

  addRegion() {
    let created = null;
    this.commitProject((project) => {
      created = createRegion(project);
      project.regions.push(created);
    }, { label: '新建区域' });
    if (created) this.select({ type: 'region', id: created.id });
    return created;
  }

  deleteRegion(id) {
    const region = this.findRegion(id);
    if (!region) return { ok: false, message: '区域不存在。' };
    const kinds = this.project.plotKinds.filter((kind) => kind.regionId === id);
    const selectedGroup =
      this.state.selection.type === 'plotGroup'
        ? this.findPlotGroup(this.state.selection.id)
        : null;
    if (kinds.length > 0) {
      return { ok: false, message: `该区域下还有 ${kinds.length} 种地块，请先移动或删除它们。` };
    }
    const usedByProfile = this.project.profiles.filter(
      (profile) => profile.startRegionId === id || profile.regionIds.includes(id),
    );
    if (usedByProfile.length > 0) {
      return { ok: false, message: `生成方案「${usedByProfile[0].name}」仍引用该区域。` };
    }
    this.commitProject((project) => {
      project.regions = project.regions.filter((candidate) => candidate.id !== id);
      project.plotGroups = project.plotGroups.filter((group) => group.regionId !== id);
    }, { label: '删除区域' });
    if (this.state.selection.id === id || selectedGroup?.regionId === id) {
      this.select({ type: 'project' });
    }
    return { ok: true };
  }

  addPlotKind(regionId, groupId = '') {
    let created = null;
    this.commitProject((project) => {
      created = createPlotKind(project, regionId, groupId);
      project.plotKinds.push(created);
    }, { label: '新建地块种类' });
    if (created) this.select({ type: 'plotKind', id: created.id });
    return created;
  }

  addPlotGroup(regionId, name, kindId = '', parentGroupId = '') {
    let created = null;
    this.commitProject((project) => {
      created = createPlotGroup(project, regionId, name, parentGroupId);
      project.plotGroups.push(created);
      if (kindId) {
        const kind = project.plotKinds.find((candidate) => candidate.id === kindId);
        if (kind && kind.regionId === created.regionId) kind.groupId = created.id;
      }
    }, { label: '新建折叠组' });
    if (created) this.select({ type: 'plotGroup', id: created.id });
    return created;
  }

  movePlotGroup(id, parentGroupId = '') {
    const group = this.findPlotGroup(id);
    if (!group) return { ok: false, message: '折叠组不存在。' };
    const parent = parentGroupId ? this.findPlotGroup(parentGroupId) : null;
    if (parentGroupId && !parent) return { ok: false, message: '上级折叠组不存在。' };
    if (parent && parent.regionId !== group.regionId) {
      return { ok: false, message: '不能把折叠组移动到其他区域。' };
    }
    if (parentGroupId === id) return { ok: false, message: '折叠组不能以自己为上级。' };
    let cursor = parent;
    const seen = new Set();
    while (cursor) {
      if (seen.has(cursor.id)) break;
      seen.add(cursor.id);
      if (cursor.id === id) return { ok: false, message: '不能把折叠组移动到自己的子组下。' };
      cursor = cursor.parentGroupId ? this.findPlotGroup(cursor.parentGroupId) : null;
    }
    this.commitProject((project) => {
      const target = project.plotGroups.find((candidate) => candidate.id === id);
      if (target) target.parentGroupId = parent?.id || '';
    }, { label: '移动折叠组' });
    return { ok: true };
  }

  deletePlotGroup(id) {
    const group = this.findPlotGroup(id);
    if (!group) return { ok: false, message: '折叠组不存在。' };
    const parentId = group.parentGroupId && this.findPlotGroup(group.parentGroupId)
      ? group.parentGroupId
      : '';
    this.commitProject((project) => {
      for (const child of project.plotGroups) {
        if (child.parentGroupId === id) child.parentGroupId = parentId;
      }
      for (const kind of project.plotKinds) {
        if (kind.groupId === id) kind.groupId = parentId;
      }
      project.plotGroups = project.plotGroups.filter((candidate) => candidate.id !== id);
    }, { label: '删除折叠组' });
    if (this.state.selection.id === id) this.select({ type: 'region', id: group.regionId });
    return { ok: true };
  }

  deletePlotKind(id) {
    const kind = this.findPlotKind(id);
    if (!kind) return { ok: false, message: '地块种类不存在。' };
    this.commitProject((project) => {
      project.plotKinds = project.plotKinds.filter((candidate) => candidate.id !== id);
      for (const profile of project.profiles) {
        if (profile.startKindId === id) {
          const fallback = project.plotKinds.find(
            (candidate) => candidate.regionId === profile.startRegionId,
          );
          profile.startKindId = fallback?.id || project.plotKinds[0]?.id || '';
        }
      }
      for (const candidate of project.plotKinds) {
        candidate.connections.allowedKinds = candidate.connections.allowedKinds.filter(
          (other) => other !== id,
        );
      }
    }, { label: '删除地块种类' });
    for (const map of this.state.maps) revalidate(map, this.project);
    if (this.state.selection.id === id) this.select({ type: 'project' });
    return { ok: true };
  }

  addSubRoomKind() {
    let created = null;
    this.commitProject((project) => {
      created = createSubRoomKind(project);
      project.subRoomKinds.push(created);
    }, { label: '新建子房间' });
    if (created) this.select({ type: 'subRoomKind', id: created.id });
    return created;
  }

  deleteSubRoomKind(id) {
    this.commitProject((project) => {
      project.subRoomKinds = project.subRoomKinds.filter((candidate) => candidate.id !== id);
      for (const kind of project.plotKinds) {
        kind.subRooms.pool = kind.subRooms.pool.filter((roomId) => roomId !== id);
      }
    }, { label: '删除子房间' });
    if (this.state.selection.id === id) this.select({ type: 'project' });
    return { ok: true };
  }

  addProfile() {
    let created = null;
    this.commitProject((project) => {
      created = createProfile(project);
      project.profiles.push(created);
    }, { label: '新建生成方案' });
    if (created) this.select({ type: 'profile', id: created.id });
    return created;
  }

  deleteProfile(id) {
    if (this.project.profiles.length <= 1) {
      return { ok: false, message: '至少保留一个生成方案。' };
    }
    this.commitProject((project) => {
      project.profiles = project.profiles.filter((candidate) => candidate.id !== id);
    }, { label: '删除生成方案' });
    if (this.state.selection.id === id) this.select({ type: 'project' });
    return { ok: true };
  }

  addCatalogItem(type) {
    let created = null;
    this.commitProject((project) => {
      created = createCatalogItem(project, type);
      project.catalogs[type].push(created);
    }, { label: '新建内容条目' });
    if (created) this.select({ type: 'catalog', id: created.id, contentType: type });
    return created;
  }

  deleteCatalogItem(type, id) {
    this.commitProject((project) => {
      project.catalogs[type] = (project.catalogs[type] || []).filter((item) => item.id !== id);
      for (const kind of project.plotKinds) {
        kind.contents[type] = kind.contents[type].filter((itemId) => itemId !== id);
      }
      for (const room of project.subRoomKinds) {
        room.contents[type] = room.contents[type].filter((itemId) => itemId !== id);
      }
    }, { label: '删除内容条目' });
    for (const map of this.state.maps) {
      for (const plot of map.plots) {
        if (plot.contents?.[type]) {
          plot.contents[type] = plot.contents[type].filter((itemId) => itemId !== id);
        }
        for (const room of plot.subRooms || []) {
          if (room.contents?.[type]) {
            room.contents[type] = room.contents[type].filter((itemId) => itemId !== id);
          }
        }
      }
      revalidate(map, this.project);
    }
    if (this.state.selection.id === id) this.select({ type: 'project' });
    return { ok: true };
  }

  /* ---------------- generation / maps ---------------- */

  generate({ profileId, seed, profileOverride = null } = {}) {
    const profile = profileOverride || this.findProfile(profileId) || this.project.profiles[0];
    if (!profile) throw new Error('工程没有可用的生成方案。');
    const generator =
      registry.getGenerator(profile.generator) || registry.getGenerator(GENERATOR_ID);
    if (!generator) throw new Error(`找不到生成器「${profile.generator}」。`);
    const map = generator.generate(this.project, { profile, seed });
    const before = this.#snapshot();
    this.state.maps.unshift(map);
    this.state.maps = this.state.maps.slice(0, MAX_MAPS);
    this.state.currentMapId = map.id;
    this.history.push({ label: `生成地图 ${map.seed}`, at: Date.now(), ...before });
    if (this.history.length > MAX_HISTORY) this.history.shift();
    this.future = [];
    this.state.selection = { type: 'map', id: map.id };
    this.save();
    this.emit('change', { reason: 'generate' });
    return map;
  }

  selectMap(id) {
    if (!this.state.maps.some((map) => map.id === id)) return false;
    this.state.currentMapId = id;
    this.state.selection = { type: 'map', id };
    this.save();
    this.emit('change', { reason: 'map-select' });
    return true;
  }

  deleteMap(id) {
    const index = this.state.maps.findIndex((map) => map.id === id);
    if (index < 0) return false;
    const before = this.#snapshot();
    this.state.maps.splice(index, 1);
    if (this.state.currentMapId === id) {
      this.state.currentMapId = this.state.maps[0]?.id || null;
      this.state.selection = { type: 'project' };
    }
    this.history.push({ label: '删除地图', at: Date.now(), ...before });
    this.save();
    this.emit('change', { reason: 'map-delete' });
    return true;
  }

  /**
   * 把玩家出生点设置为某个格子。只允许 spawnEligible 的地块。
   */
  setSpawnAtCell(x, y) {
    const map = this.currentMap;
    if (!map) return { ok: false, message: '当前没有地图。' };
    const plot = map.plots.find((candidate) =>
      candidate.cells.some((cell) => cell.x === x && cell.y === y),
    );
    if (!plot) return { ok: false, message: '这里没有地块。' };
    const kind = this.findPlotKind(plot.kindId);
    if (!kind?.spawnEligible) {
      return { ok: false, message: `「${kind?.name || '该地块'}」没有开放出生点。` };
    }
    const cellIndex = plot.cells.findIndex((cell) => cell.x === x && cell.y === y);
    if (cellIndex < 0) return { ok: false, message: '格子在当前地块之外。' };
    this.commitMap((current) => {
      current.spawn = { plotId: plot.id, cellIndex, x, y };
    }, { label: '设置出生点' });
    return { ok: true, plot, cellIndex };
  }

  /* ---------------- workspace ---------------- */

  newProject({ blank = true } = {}) {
    const before = this.#snapshot();
    this.state.project = blank ? createBlankProject() : createDefaultProject();
    this.state.maps = [];
    this.state.currentMapId = null;
    this.state.selection = { type: 'project' };
    this.history.push({ label: blank ? '新建空白工程' : '载入示例工程', at: Date.now(), ...before });
    this.future = [];
    this.save();
    this.emit('change', { reason: 'workspace' });
  }

  loadWorkspace({ project, map = null }) {
    const before = this.#snapshot();
    this.state.project = normalizeProject(project);
    this.state.maps = map ? [map] : [];
    this.state.currentMapId = map?.id || null;
    this.state.selection = { type: map ? 'map' : 'project', id: map?.id };
    for (const current of this.state.maps) revalidate(current, this.state.project);
    this.history.push({ label: '导入工作区', at: Date.now(), ...before });
    this.future = [];
    this.save();
    this.emit('change', { reason: 'workspace' });
  }

  contentUsage(type, id) {
    const kinds = this.project.plotKinds.filter((kind) =>
      (kind.contents[type] || []).includes(id),
    ).length;
    const rooms = this.project.subRoomKinds.filter((room) =>
      (room.contents[type] || []).includes(id),
    ).length;
    return { kinds, rooms, total: kinds + rooms };
  }

  validate() {
    const projectCheck = validateProject(this.project);
    const projectExtras = extensionIssues('project', this.project, { project: this.project });
    if (!this.currentMap) {
      const issues = [...projectCheck.issues, ...projectExtras];
      return {
        ok: projectCheck.ok && !projectExtras.some((issue) => issue.level === 'error'),
        errors: [...projectCheck.errors, ...errorMessages(projectExtras)],
        warnings: [...projectCheck.warnings, ...warningMessages(projectExtras)],
        issues,
        stats: null,
      };
    }
    const mapCheck = validateMap(this.project, this.currentMap, {
      profile: profileById(this.project, this.currentMap.profileId),
    });
    const mapExtras = extensionIssues('map', this.currentMap, {
      project: this.project,
      map: this.currentMap,
    });
    const extras = [...projectExtras, ...mapExtras];
    return {
      ok:
        projectCheck.ok &&
        mapCheck.ok &&
        !extras.some((issue) => issue.level === 'error'),
      errors: [...projectCheck.errors, ...mapCheck.errors, ...errorMessages(extras)],
      warnings: [...projectCheck.warnings, ...mapCheck.warnings, ...warningMessages(extras)],
      issues: [...projectCheck.issues, ...mapCheck.issues, ...extras],
      stats: mapCheck.stats,
    };
  }

  contentTypes() {
    return CONTENT_TYPE_IDS;
  }

  /* ---------------- 类银河城地牢编辑器通道 ---------------- */

  setMode(mode) {
    if (!['templates', 'pools', 'floor', 'world'].includes(mode)) return;
    if (this.state.ui.mode === mode) return;
    this.state.ui.mode = mode;
    this.save();
    this.emit('ui', { mode });
  }

  setCellTypes(nextCellTypes) {
    return this.commitProject(
      (project) => {
        project.dungeon.cellTypes = normalizeCellTypes(nextCellTypes);
      },
      { label: '更新全局格子配置' },
    );
  }

  saveStripTemplate(rawTemplate, { asNew = false, asChild = false } = {}) {
    let saved = null;
    this.commitProject(
      (project) => {
        const list = project.dungeon.stripTemplates;
        const types = project.dungeon.cellTypes;
        const normalized = normalizeStripTemplate(rawTemplate, types, list.length);
        const existingIdx = (asNew || asChild) ? -1 : list.findIndex((t) => t.id === normalized.id);
        const next = [...list];
        if (existingIdx >= 0) {
          const existing = list[existingIdx];
          if (normalized.parentTemplateId !== existing.parentTemplateId) return false;
          const structureLocked = existing.parentTemplateId || list.some((t) => t.parentTemplateId === existing.id);
          if (structureLocked && stripTemplateStructureKey(normalized) !== stripTemplateStructureKey(existing)) return false;
          next[existingIdx] = normalized;
        } else {
          if (asChild) {
            if (typeof normalized.parentTemplateId !== 'string' || !normalized.parentTemplateId) return false;
          } else if (asNew) {
            delete normalized.parentTemplateId;
          } else if (Object.hasOwn(normalized, 'parentTemplateId')) return false;
          const ids = new Set(list.map((t) => t.id));
          if (!normalized.id || ids.has(normalized.id) || normalized.id.startsWith('draft_')) {
            let n = list.length + 1;
            while (ids.has(`tpl_${n}`)) n += 1;
            normalized.id = `tpl_${n}`;
          }
          next.unshift(normalized);
        }
        if (!validateStripTemplate(normalized, types).ok || validateTemplateFamilies(next).length) return false;
        project.dungeon.stripTemplates = next;
        saved = normalized;
      },
      { label: asChild ? '新增子模板' : asNew ? '另存条带模板' : '保存条带模板' },
    );
    return saved;
  }

  deleteStripTemplate(id) {
    return this.commitProject(
      (project) => {
        const list = project.dungeon.stripTemplates;
        if (list.length <= 1 || list.some((t) => t.parentTemplateId === id)) return false;
        const idx = list.findIndex((t) => t.id === id);
        if (idx < 0) return false;
        list.splice(idx, 1);
      },
      { label: '删除条带模板' },
    );
  }

  importTemplateLibrary({ cellTypes = null, stripTemplates = [], replace = false } = {}) {
    return this.commitProject(
      (project) => {
        const types = cellTypes?.length ? normalizeCellTypes(cellTypes) : project.dungeon.cellTypes;
        const incoming = stripTemplates.map((t, i) => normalizeStripTemplate(t, types, i));
        if (!incoming.length || validateTemplateFamilies(incoming).length
          || incoming.some((t) => !validateStripTemplate(t, types).ok)) return false;
        const existing = replace ? [] : project.dungeon.stripTemplates.map((t, i) => normalizeStripTemplate(t, types, i));
        const occupied = new Set(existing.map((t) => t.id));
        const reserved = new Set([...occupied, ...incoming.map((t) => t.id)]);
        const renamed = new Map();
        for (const template of incoming) {
          const original = template.id;
          if (occupied.has(original)) {
            let suffix = 2;
            while (reserved.has(`${original}_${suffix}`)) suffix += 1;
            template.id = `${original}_${suffix}`;
            reserved.add(template.id);
          }
          if (!renamed.has(original)) renamed.set(original, template.id);
          occupied.add(template.id);
        }
        for (const template of incoming) {
          if (template.parentTemplateId) {
            const oldParent = template.parentTemplateId;
            const parent = incoming.find((t) => t.id === renamed.get(oldParent));
            if (!parent.visualTypeId && template.visualTypeId === oldParent && oldParent !== parent.id) {
              // Preserve implicit art identity of a root renamed by merge.
              parent.visualTypeId = oldParent;
            }
            template.parentTemplateId = parent.id;
          }
        }
        const combined = [...existing, ...incoming];
        if (validateTemplateFamilies(combined).length) return false;
        project.dungeon.cellTypes = types;
        project.dungeon.stripTemplates = combined;
      },
      { label: replace ? '替换条带模板库' : '合并导入条带模板' },
    );
  }

  updateFloorProfile(mutator, label = '编辑单层方案') {
    return this.commitProject(
      (project) => {
        const current = project.dungeon.floorProfiles[0];
        mutator(current);
        project.dungeon.floorProfiles[0] = normalizeFloorProfile(current);
      },
      { label },
    );
  }

  generateFloor({ seed = null } = {}) {
    const profile = this.activeFloorProfile;
    const map = generateFloorMap({
      profile,
      stripTemplates: this.dungeon.stripTemplates,
      cellTypes: this.dungeon.cellTypes,
      seed,
    });
    const before = this.#snapshot();
    const existingIdx = this.state.floorMaps.findIndex((m) => m.id === map.id);
    if (existingIdx >= 0) this.state.floorMaps.splice(existingIdx, 1);
    this.state.floorMaps.unshift(map);
    if (this.state.floorMaps.length > MAX_MAPS) this.state.floorMaps.length = MAX_MAPS;
    this.state.currentFloorMapId = map.id;
    this.state.selectedStripId = null;
    this.history.push({ label: `生成单层地图 ${map.seed}`, at: Date.now(), ...before });
    this.future = [];
    this.save();
    this.emit('change', { reason: 'floor-generate', map });
    return map;
  }

  loadFloorMap(map) {
    this.#pushHistory('读取单层地图');
    this.state.floorMaps = [map, ...this.state.floorMaps.filter((m) => m.id !== map.id)].slice(0, MAX_MAPS);
    this.state.currentFloorMapId = map.id;
    this.state.selectedStripId = null;
    this.save();
    this.emit('change', { reason: 'floor-import' });
  }

  selectFloorMap(id) {
    if (!this.state.floorMaps.some((m) => m.id === id)) return;
    this.state.currentFloorMapId = id;
    this.state.selectedStripId = null;
    this.save();
    this.emit('change', { reason: 'floor-select' });
  }

  deleteFloorMap(id) {
    const idx = this.state.floorMaps.findIndex((m) => m.id === id);
    if (idx < 0) return;
    this.#pushHistory('删除单层地图');
    this.state.floorMaps.splice(idx, 1);
    if (this.state.currentFloorMapId === id) {
      this.state.currentFloorMapId = this.state.floorMaps[0]?.id || null;
      this.state.selectedStripId = null;
    }
    this.save();
    this.emit('change', { reason: 'floor-delete' });
  }

  selectFloorStrip(stripId) {
    if (this.state.selectedStripId === stripId) return;
    this.state.selectedStripId = stripId || null;
    this.emit('floor-selection', { stripId: this.state.selectedStripId });
  }

  commitFloorMap(mutator, { label = '编辑单层地图' } = {}) {
    const map = this.currentFloorMap;
    if (!map) return false;
    const before = this.#snapshot();
    const result = mutator(map, this.state.project);
    if (result === false) return false;
    recomputeFloorProgression(map, map.progression, this.dungeon.cellTypes);
    recomputeFloorMetrics(map, this.dungeon.cellTypes);
    this.history.push({ label, at: Date.now(), ...before });
    if (this.history.length > MAX_HISTORY) this.history.shift();
    this.future = [];
    this.save();
    this.emit('change', { reason: 'floor-edit', label });
    return true;
  }
}

export { STORAGE_KEY };
