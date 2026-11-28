/**
 * PlotWeave 浏览器应用入口。
 *
 * 组合 store、侧栏、画布与检视器，处理顶栏 / 工具栏 / 快捷键 / 导入导出。
 * 所有地图与工程逻辑都在 core 层，这里只负责交互编排。
 */

import { APP_ID, APP_VERSION, CONTENT_TYPES, SCHEMA_VERSION } from '../core/constants.js';
import { randomSeed } from '../core/rng.js';
import { previewQuotas } from '../core/generator.js';
import { projectFingerprint } from '../core/project.js';
import { registry } from '../core/registry.js';
import { formatImportErrors, parseBundle, serializeBundle, serializeProject } from '../core/serialization.js';
import { applyFloorFile, makeFloorFile } from '../core/floor-file.js';
import { WorkDirectory, PROJECT_FILE, PREVIEW_FILE } from './work-directory.js';
import { AppStore } from './store.js';
import { MapCanvas } from './canvas.js';
import { Sidebar } from './sidebar.js';
import { Inspector } from './inspector.js';
import { MobileShell } from './mobile.js';
import { TemplateEditorView } from './template-editor.js';
import { FloorEditorView } from './floor-editor.js';
import { MapPoolsEditor } from './map-pools-editor.js';
import { WorldView } from './world-view.js';
import { parseWorldPools, serializeWorldPools } from '../core/world-pools.js';
import { parseTemplateLibrary, serializeTemplateLibrary, validateStripTemplate } from '../core/strip-templates.js';
import { recomputeFloorMetrics, recomputeFloorProgression } from '../core/floor-generator.js';
import {
  clear,
  downloadBlob,
  downloadText,
  h,
  icon,
  iconButton,
  isEditableTarget,
  mount,
  pickFile,
  qs,
  readFileText,
  slugify,
  timestamp,
} from './dom.js';
import {
  confirmDialog,
  openHelp,
  openModal,
  openTextViewer,
  toast,
} from './modals.js';
import { field, numberInput, selectInput, textInput } from './fields.js';
import { applyTheme, getTheme, toggleTheme } from './theme.js';

const TOOLS = [
  { id: 'select', label: '选择', icon: 'cursor' },
  { id: 'spawn', label: '出生点', icon: 'spawn' },
  { id: 'pan', label: '平移', icon: 'arrow' },
];

function brandNode() {
  return h(
    'div',
    { class: 'brand' },
    h('div', { class: 'brand-mark', 'aria-hidden': 'true' }),
    h(
      'div',
      { class: 'brand-copy' },
      h('span', { class: 'brand-name' }, 'PLOTWEAVE'),
      h('span', { class: 'brand-sub' }, '类银河城地牢编辑器'),
    ),
  );
}

function projectMenu(store) {
  const rows = [
    {
      title: '旧单层地图工具',
      hint: '查看与编辑旧地图快照；不能用于验证游戏地图池。',
      action: () => store.setMode('floor'),
    },
    {
      title: '新建空白工程',
      hint: '从最小结构开始：一个区域、一种地块、一个方案。',
      action: async () => {
        const ok = await confirmDialog({
          title: '新建空白工程',
          message: '当前工程与地图会被替换（可撤销）。继续？',
          confirmText: '新建',
        });
        if (!ok) return;
        store.newProject({ blank: true });
        toast('已新建空白工程', { type: 'success' });
      },
    },
    {
      title: '载入内置示例',
      hint: '四个区域、十三种地块、完整内容目录与三个生成方案。',
      action: async () => {
        const ok = await confirmDialog({
          title: '载入示例工程',
          message: '当前工程与地图会被替换（可撤销）。继续？',
          confirmText: '载入',
        });
        if (!ok) return;
        store.newProject({ blank: false });
        toast('示例工程已载入', { type: 'success' });
      },
    },
    {
      title: '清空本地缓存',
      hint: '删除浏览器 localStorage 中的工作区副本；不会影响已导出的文件。',
      action: async () => {
        const ok = await confirmDialog({
          title: '清空本地缓存',
          message: '删除本地保存的工作区并重置为空白工程？建议先导出需要的内容。',
          confirmText: '清空',
          danger: true,
        });
        if (!ok) return;
        store.clearStorage();
        store.newProject({ blank: true });
        toast('本地缓存已清空', { type: 'success' });
      },
    },
    {
      title: '关于 PlotWeave',
      hint: `版本 ${APP_VERSION} · schema v${SCHEMA_VERSION} · 纯前端、零运行时依赖。`,
      action: () =>
        openModal({
          title: '关于 PlotWeave',
          subtitle: `${APP_ID} ${APP_VERSION}`,
          size: 'wide',
          body: h(
            'div',
            { class: 'help-grid' },
            h(
              'div',
              { class: 'help-card' },
              h('h3', null, '它是什么'),
              h('p', null, '一个顶视角地图设计器：用区域做分类与配额，用 1–5 格长条地块组成网络，用子房间标注地块内的特殊区域。'),
              h('p', { style: { marginTop: '6px' } }, '工程与地图都是可读 JSON；生成由种子驱动，可复现、可导出、可移植。'),
            ),
            h(
              'div',
              { class: 'help-card' },
              h('h3', null, '边界'),
              h('p', null, '工具不包含玩法运行时代码。道具、遗物、事件链、敌人与 NPC 只作为目录引用挂在地块 / 子房间上，由宿主项目解释。'),
              h('p', { style: { marginTop: '6px' } }, '核心逻辑位于 src/core，不依赖 DOM；UI、Canvas 与扩展注册表都可以替换。'),
            ),
          ),
          footer: [
            h('div', { class: 'spacer' }),
            h('button', { class: 'btn primary', type: 'button', onclick: () => qs('.modal-close')?.click() }, '关闭'),
          ],
        }),
    },
  ];

  openModal({
    title: '工程',
    subtitle: `${store.project.meta.name} · ${projectFingerprint(store.project)}`,
    body: h(
      'div',
      { class: 'action-row', style: { flexDirection: 'column', alignItems: 'stretch' } },
      ...rows.map((row) =>
        h(
          'button',
          {
            class: 'btn',
            type: 'button',
            style: { height: 'auto', padding: '9px 11px', textAlign: 'left', justifyContent: 'flex-start' },
            onclick: () => {
              qs('.modal-backdrop')?.remove();
              row.action();
            },
          },
          h(
            'span',
            { style: { display: 'flex', flexDirection: 'column', gap: '2px' } },
            h('strong', null, row.title),
            h('span', { class: 'field-hint', style: { gridColumn: 'auto' } }, row.hint),
          ),
        ),
      ),
    ),
  });
}

/* ------------------------------------------------------------------ */

function loadOptionalPlugins(instance) {
  const id = new URLSearchParams(window.location.search).get('plugin');
  if (!id || !/^[a-z0-9-]+$/.test(id)) return;
  import(`../../plugins/${id}-plugin.js`)
    .then(() => {
      instance.syncAll();
      instance.canvas.render();
      toast(`已载入插件「${id}」`, { type: 'success' });
    })
    .catch((error) => {
      console.error('[PlotWeave] 插件载入失败', error);
      toast(`插件「${id}」载入失败：${error.message}`, { type: 'error' });
    });
}

function boot() {
  applyTheme(getTheme(), { persist: false });
  const store = new AppStore();
  const topbar = qs('#topbar');
  const host = qs('#canvas-host');
  const canvasEl = qs('#map-canvas');
  const hoverCard = qs('#hover-card');
  const emptyEl = qs('#canvas-empty');
  const toolbar = qs('#stage-toolbar');
  const statusbar = qs('#statusbar');

  const canvas = new MapCanvas({ canvas: canvasEl, host, hoverCard, store });
  const sidebar = new Sidebar({ root: qs('#sidebar'), store });
  const inspector = new Inspector({ root: qs('#inspector'), store });
  const mobile = new MobileShell({ store, sidebar, inspector, canvas });

  const wsPools = qs('#workspace-pools');
  const wsTemplates = qs('#workspace-templates');
  const wsFloor = qs('#workspace-floor');
  const wsWorld = qs('#workspace-world');

  // 首次启动时若尚无单层地图，自动生成一张默认三角单层供立即预览。
  if (store.floorMaps.length === 0) {
    try {
      store.generateFloor({ seed: 'PW-DEMO-01' });
    } catch (err) {
      console.warn('[PlotWeave] 初始单层地图生成跳过', err);
    }
  }

  const templateEditor = new TemplateEditorView({ root: wsTemplates, store });
  const floorEditor = new FloorEditorView({ root: wsFloor, store });
  const worldView = new WorldView({ root: wsWorld, store });

  const MODE_TABS = [
    { id: 'templates', label: '条带模板' },
    { id: 'pools', label: '地图生成池' },
    { id: 'world', label: '完整地图' },
  ];
  const modeButtons = new Map();
  const modeSwitcher = h(
    'nav',
    { class: 'mode-switcher', role: 'tablist', 'aria-label': '编辑器功能模式' },
    ...MODE_TABS.map((item) => {
      const btn = h(
        'button',
        {
          class: 'mode-tab',
          type: 'button',
          role: 'tab',
          'data-mode': item.id,
          onclick: () => store.setMode(item.id),
        },
        item.label,
      );
      modeButtons.set(item.id, btn);
      return btn;
    }),
  );

  let previousMode;
  function syncModeViews() {
    const mode = store.mode;
    wsPools.hidden = mode !== 'pools';
    if (previousMode === 'pools' && mode !== 'pools') poolsEditor.leave();
    if (mode === 'pools' && previousMode !== mode) void poolsEditor.refresh();
    previousMode = mode;
    wsTemplates.hidden = mode !== 'templates';
    wsFloor.hidden = mode !== 'floor';
    wsWorld.hidden = mode !== 'world';
    for (const [id, btn] of modeButtons) {
      const active = id === mode;
      btn.classList.toggle('active', active);
      btn.setAttribute('aria-selected', String(active));
    }
    if (mode === 'floor') {
      window.requestAnimationFrame(() => {
        floorEditor.canvas.resize();
        if (!floorEditor.canvas.lastMapId && store.currentFloorMap) {
          floorEditor.fit();
        }
      });
    } else if (mode === 'pools') {
      window.requestAnimationFrame(() => {
        poolsEditor.resize();
      });
    } else if (mode === 'world') {
      window.requestAnimationFrame(() => {
        worldView.resize();
      });
    }
  }

  /* ---------------- topbar ---------------- */

  const projectChip = h('button', {
    class: 'project-chip',
    type: 'button',
    title: '工程菜单',
    style: { cursor: 'pointer', textAlign: 'left' },
    onclick: () => projectMenu(store),
  });
  const projectName = h('span', { class: 'project-chip-name' });
  const projectMeta = h('span', { class: 'project-chip-meta' });
  mount(
    projectChip,
    h('span', { class: 'dot', 'aria-hidden': 'true' }),
    h('span', { style: { display: 'flex', flexDirection: 'column', minWidth: 0 } }, projectName, projectMeta),
  );

  const undoBtn = iconButton('undo', '撤销 (Ctrl+Z)', () => {
    const label = store.undo();
    if (label) toast(`已撤销：${label}`, { type: 'info' });
  });
  const redoBtn = iconButton('redo', '重做 (Ctrl+Shift+Z)', () => {
    const label = store.redo();
    if (label) toast(`已重做：${label}`, { type: 'info' });
  });
  const directory = new WorkDirectory();
  let publishedRevision;
  let poolSession;
  async function poolApi(route, body, signal) {
    if (body !== undefined) poolSession ??= crypto.randomUUID();
    const response = await fetch(`/api/map-pools/${route}`, body === undefined ? {} : {
      method: 'POST', headers: { 'content-type': 'application/json', 'x-plotweave-session': poolSession },
      body: JSON.stringify(body), signal,
    });
    if (!response.headers.get('content-type')?.includes('application/json')) throw Error('请用 npm start 启动本机 PlotWeave，再使用地图池预览和发布。');
    const data = await response.json();
    if (!response.ok) throw Object.assign(Error(data.error), { details: data.details });
    return data;
  }
  const poolsEditor = new MapPoolsEditor(wsPools, {
    store,
    getPools: () => store.dungeon.mapPools,
    updatePools: config => store.commitProject(project => { project.dungeon.mapPools = config; }, { label: '编辑地图生成池' }),
    saveDraft: async () => {
      if (!directory.handle) throw Error('草稿已自动存在浏览器。要写入 project.json，请先在「工作目录」中选择 Patterns。');
      await saveDirectory({ map: false });
    },
    listPatterns: async () => {
      const [catalog, published] = await Promise.all([poolApi('patterns'), poolApi('published')]);
      if (publishedRevision === undefined) publishedRevision = published.revision;
      return catalog;
    },
    loadPublished: async () => {
      const { config, revision } = await poolApi('published');
      publishedRevision = revision;
      return config;
    },
    preview: args => poolApi('preview', args),
    explorePreview: (args, signal) => poolApi('explore', args, signal),
    publish: async ({ config }) => {
      if (publishedRevision === undefined) throw Error('请先刷新已发布状态，再发布。');
      const result = await poolApi('publish', { config, revision: publishedRevision });
      publishedRevision = result.revision;
      return result;
    },
  });
  const directoryBtn = h('button', { class: 'btn', type: 'button', onclick: () => directoryMenu() }, '工作目录');
  void directory.restore();
  let directoryBusy = false;
  async function directoryAction(action) {
    if (directoryBusy) return;
    directoryBusy = true;
    try { await action(); }
    catch (error) { if (error.name !== 'AbortError') toast(error.message, { type: 'error' }); }
    finally { directoryBusy = false; }
  }
  function directoryMenu() {
    const actions = [
      ['选择目录', async () => {
        if (!directory.available) throw Error('请使用本机 Chrome / Edge 打开 PlotWeave，或使用下载和导入。');
        await directory.choose();
        await reloadDirectory({ authorize: false });
      }],
      ['重新读取', () => reloadDirectory()],
      ['保存工程', () => saveDirectory({ map: false })],
      ['保存当前地图', () => saveDirectory({ project: false })],
    ];
    let dialog;
    dialog = openModal({
      title: '工作目录',
      subtitle: directory.handle ? `当前：${directory.handle.name}` : '请选择 Assets/Resources/Data/Maps/Patterns',
      body: h('p', null, '工程保存到 project.json；当前地图保存到 Preview.floor.json。Ctrl+S 按当前工作区保存。'),
      footer: h('div', { class: 'tool-group' }, ...actions.map(([label, action]) => h('button', { class: 'btn', type: 'button', onclick: () => {
        dialog.close(); void directoryAction(action);
      } }, label))),
    });
  }
  async function reloadDirectory({ authorize = true } = {}) {
    if (authorize) await directory.authorize();
    const [projectText, mapText] = await Promise.all([directory.read(PROJECT_FILE), directory.read(PREVIEW_FILE)]);
    if (projectText === null && mapText === null) {
      directory.baseline.clear();
      toast('工作目录为空，可以开始保存。', { type: 'info' });
      return;
    }
    const project = projectText === null ? null : parseBundle(projectText);
    if (project && (!project.ok || !project.project)) throw Error(formatImportErrors(project));
    if (project) {
      const { stripTemplates, cellTypes } = project.project.dungeon;
      for (const template of stripTemplates) {
        const check = validateStripTemplate(template, cellTypes);
        if (!check.ok) throw Error(`模板「${template.name}」：${check.errors[0].message}`);
      }
    }
    let map = null;
    if (mapText !== null) {
      const raw = JSON.parse(mapText);
      map = applyFloorFile(raw);
      recomputeFloorMetrics(map, project?.project.dungeon.cellTypes || store.dungeon.cellTypes);
    }
    const ok = await confirmDialog({ title: '重新读取工作目录', message: '以磁盘上的工程和地图替换当前相应内容？未保存的修改不会写入磁盘。', confirmText: '读取磁盘' });
    if (!ok) return;
    if (project) {
      store.loadWorkspace({ project: project.project });
      templateEditor.loadTemplate(store.dungeon.stripTemplates[0] || null);
      templateEditor.renderAll();
    }
    if (map) {
      if (store.mode === 'pools') store.loadFloorMap(map);
      else loadFloorMap(map);
    }
    directory.baseline.set(PROJECT_FILE, projectText);
    directory.baseline.set(PREVIEW_FILE, mapText);
    toast('已读取工作目录', { type: 'success' });
  }
  async function saveDirectory({ project = true, map = true } = {}) {
    await directory.authorize();
    if (project) await directory.checkWrite(PROJECT_FILE);
    if (map) await directory.checkWrite(PREVIEW_FILE);
    if (project && templateEditor.dirty && !templateEditor.saveCurrentDraft()) throw Error('当前条带草稿无效，工程未写入。');
    const preview = map && store.currentFloorMap;
    if (map && !preview && !project) throw Error('当前没有单层地图可保存。');
    const mapText = preview ? JSON.stringify(makeFloorFile(preview), null, 2) + '\n' : null;
    if (project) await directory.write(PROJECT_FILE, serializeProject(store.project) + '\n');
    if (mapText) await directory.write(PREVIEW_FILE, mapText);
    toast(`已保存到 ${directory.handle.name}`, { type: 'success' });
  }

  const importBtn = h(
    'button',
    { class: 'btn', type: 'button', id: 'btn-import', title: '导入工程 / Bundle JSON', onclick: () => importWorkspace() },
    icon('import'),
    h('span', null, '导入'),
  );
  const exportBtn = h(
    'button',
    { class: 'btn', type: 'button', id: 'btn-export', title: '导出工程 / 地图 / PNG', onclick: () => exportWorkspace() },
    icon('export'),
    h('span', null, '导出'),
  );
  const generateBtn = h(
    'button',
    {
      class: 'btn primary',
      type: 'button',
      id: 'btn-generate',
      title: '打开地图生成池 (N)',
      onclick: () => store.setMode('pools'),
    },
    icon('generate'),
    h('span', null, '地图生成池'),
  );

  const themeBtn = h('button', {
    class: 'btn icon ghost',
    type: 'button',
    id: 'btn-theme',
    title: '切换亮色主题',
    'aria-label': '切换亮色主题',
    onclick: () => {
      const theme = toggleTheme();
      canvas.setTheme(theme);
      floorEditor.setTheme(theme);
      poolsEditor.setTheme(theme);
      worldView.setTheme(theme);
      syncThemeButton(theme);
      toast(`已切换到${theme === 'light' ? '亮色' : '暗色'}主题`, { type: 'info' });
    },
  });
  const helpBtn = iconButton('help', '使用说明 (?)', () => openHelp());
  helpBtn.id = 'btn-help';
  const mobileMenuBtn = iconButton('dots', '更多操作', () => openMobileMenu());
  mobileMenuBtn.id = 'btn-mobile-menu';
  mobileMenuBtn.classList.add('mobile-only');
  function syncThemeButton(theme) {
    const light = theme === 'light';
    const label = light ? '切换到暗色主题' : '切换亮色主题';
    themeBtn.title = label;
    themeBtn.setAttribute('aria-label', label);
    themeBtn.replaceChildren(icon(light ? 'sun' : 'moon'));
  }
  syncThemeButton(getTheme());

  function openMobileMenu() {
    let dialog = null;
    const run = (action) => {
      dialog?.close();
      window.setTimeout(action, 0);
    };
    const row = (title, hint, action, { disabled = false } = {}) =>
      h(
        'button',
        {
          class: 'btn mobile-menu-row',
          type: 'button',
          disabled,
          onclick: () => run(action),
        },
        h(
          'span',
          { class: 'mobile-menu-copy' },
          h('strong', null, title),
          h('span', { class: 'field-hint' }, hint),
        ),
        icon('chevron', 12),
      );
    dialog = openModal({
      title: '更多操作',
      subtitle: '移动端快捷入口',
      size: 'narrow',
      body: h(
        'div',
        { class: 'mobile-menu-list' },
        row(
          '撤销',
          '回退上一步工程或地图修改',
          () => {
            const label = store.undo();
            if (label) toast(`已撤销：${label}`, { type: 'info' });
          },
          { disabled: !store.canUndo },
        ),
        row(
          '重做',
          '恢复刚撤销的修改',
          () => {
            const label = store.redo();
            if (label) toast(`已重做：${label}`, { type: 'info' });
          },
          { disabled: !store.canRedo },
        ),
        row('工作目录', '读取和保存 Patterns 中的 JSON', () => directoryMenu()),
    row('导入 JSON', '读取工程或工程 + 地图 Bundle', () => importWorkspace()),
        row('导出', '工程、地图、Bundle 或 PNG', () => exportWorkspace()),
        row('旧单层工具', '编辑旧地图快照，不用于验证游戏配置', () => store.setMode('floor')),
        row(
          getTheme() === 'light' ? '切换到暗色主题' : '切换到亮色主题',
          '同时切换界面与画布配色',
          () => {
            const theme = toggleTheme();
            canvas.setTheme(theme);
            floorEditor.setTheme(theme);
            poolsEditor.setTheme(theme);
            syncThemeButton(theme);
            toast(`已切换到${theme === 'light' ? '亮色' : '暗色'}主题`, { type: 'info' });
          },
        ),
        row('使用说明', '快捷键、概念与操作提示', () => openHelp()),
        row('工程菜单', '新建、载入示例、清空本地缓存', () => projectMenu(store)),
      ),
    });
  }

  mount(
    topbar,
    brandNode(),
    modeSwitcher,
    projectChip,
    h(
      'div',
      { class: 'top-actions' },
      h('div', { class: 'tool-group' }, undoBtn, redoBtn),
      h('span', { class: 'top-divider' }),
      directoryBtn,
      importBtn,
      exportBtn,
      generateBtn,
      themeBtn,
      helpBtn,
      mobileMenuBtn,
    ),
  );

  /* ---------------- stage toolbar ---------------- */

  const toolButtons = new Map();
  const toolGroup = h('div', { class: 'tool-group' });
  for (const tool of TOOLS) {
    const button = h(
      'button',
      {
        class: 'tool-btn',
        type: 'button',
        title: `${tool.label}工具`,
        onclick: () => {
          store.setUi({ tool: tool.id });
        },
      },
      icon(tool.icon),
      h('span', null, tool.label),
    );
    toolButtons.set(tool.id, button);
    toolGroup.append(button);
  }

  const zoomLabel = h('span', { class: 'seed-label', style: { minWidth: '52px', justifyContent: 'center' } }, '100%');
  const zoomOut = h('button', { class: 'tool-btn', type: 'button', title: '缩小', onclick: () => canvas.zoomBy(1 / 1.2) }, icon('minus'));
  const zoomIn = h('button', { class: 'tool-btn', type: 'button', title: '放大', onclick: () => canvas.zoomBy(1.2) }, icon('plus'));
  const fitBtn = h('button', { class: 'tool-btn', type: 'button', title: '适应画布 (F)', onclick: () => canvas.fit() }, icon('fit'));
  const zoomGroup = h('div', { class: 'tool-group' }, zoomOut, zoomIn, fitBtn);

  const toggleButtons = new Map();
  const toggleGroup = h('div', { class: 'tool-group' });
  for (const toggle of [
    { key: 'showGrid', label: '网格', icon: 'grid' },
    { key: 'showLabels', label: '标签', icon: 'labels' },
    { key: 'showContents', label: '内容点', icon: 'contents' },
    { key: 'showRegions', label: '区域色', icon: 'eye' },
  ]) {
    const button = h(
      'button',
      {
        class: 'tool-btn',
        type: 'button',
        title: toggle.label,
        onclick: () => store.toggleUi(toggle.key),
      },
      icon(toggle.icon),
    );
    toggleButtons.set(toggle.key, button);
    toggleGroup.append(button);
  }

  const legend = h('div', { class: 'legend' });
  const seedLabel = h('span', { class: 'seed-label' }, 'SEED ', h('strong', null, '—'));
  mount(
    toolbar,
    toolGroup,
    h('span', { class: 'top-divider' }),
    zoomGroup,
    zoomLabel,
    h('span', { class: 'top-divider' }),
    toggleGroup,
    h('span', { class: 'toolbar-spacer' }),
    legend,
    seedLabel,
  );

  /* ---------------- empty state ---------------- */

  const emptyCard = h(
    'div',
    { class: 'empty-card' },
    h('div', { class: 'eyebrow' }, 'NO MAP / 空白画布'),
    h('h2', null, '从一张空白蓝图开始'),
    h('p', null, '地块是 1–5 格的长条廊道；接口决定它从哪里继续生长。选择一个生成方案，指定种子，地图会以确定性方式展开。'),
    h(
      'div',
      { class: 'action-row', style: { marginTop: '13px' } },
      h(
        'button',
        { class: 'btn primary', type: 'button', onclick: () => openGenerate() },
        icon('generate'),
        h('span', null, '生成第一张地图'),
      ),
      h(
        'button',
        {
          class: 'btn',
          type: 'button',
          onclick: () => {
            store.setTab('profiles');
            store.select({ type: 'profile', id: store.project.profiles[0]?.id });
          },
        },
        '检查生成方案',
      ),
    ),
    h('div', { class: 'hint', style: { marginTop: '11px' } }, '快捷键：N 生成 · F 适应画布 · ? 帮助'),
  );
  mount(emptyEl, emptyCard);

  /* ---------------- generate dialog ---------------- */

  function openGenerate() {
    const { project } = store;
    if (!project.profiles.length) {
      toast('工程没有生成方案，请先新建一个。', { type: 'error' });
      return;
    }
    const selectedProfile =
      store.selection.type === 'profile'
        ? store.findProfile(store.selection.id)
        : project.profiles[0];
    let profile = selectedProfile || project.profiles[0];
    const initialSeed = randomSeed();

    const seedInput = h('input', {
      class: 'input',
      type: 'text',
      value: initialSeed,
      spellcheck: 'false',
      style: { fontFamily: 'var(--mono)' },
    });
    const profileSelect = selectInput(
      project.profiles.map((candidate) => ({
        value: candidate.id,
        label: `${candidate.name} · ${candidate.plotTarget} 块`,
      })),
      profile.id,
      (value) => {
        profile = store.findProfile(value) || profile;
        targetInput.placeholder = String(profile.plotTarget);
        updateHint();
      },
    );
    const targetInput = h('input', {
      class: 'input',
      type: 'number',
      min: '1',
      max: '200',
      placeholder: String(profile.plotTarget),
      style: { fontFamily: 'var(--mono)' },
    });
    const hint = h('div', { class: 'field-hint' });

    function updateHint() {
      const regions = profile.regionIds.length
        ? profile.regionIds
            .map((id) => store.findRegion(id))
            .filter(Boolean)
            .map((region) => region.name)
            .join('、')
        : '全部区域';
      const quotas = previewQuotas(project, profile, seedInput.value || initialSeed);
      const total = quotas.reduce((sum, item) => sum + item.quota, 0);
      hint.textContent = `区域：${regions}。计划配额 ${total} 块（${quotas
        .map((item) => `${item.name} ${item.quota}`)
        .join(' · ')}）。实际结果会受接口、长度与连接上限影响。`;
    }
    seedInput.addEventListener('input', updateHint);
    updateHint();

    const dialog = openModal({
      title: '生成地图',
      subtitle: '同一个工程 + 同一个种子 = 同一张地图。',
      body: h(
        'div',
        { class: 'modal-form' },
        field('生成方案', profileSelect),
        field(
          '目标覆盖',
          h(
            'div',
            null,
            targetInput,
            h('div', { class: 'field-hint', style: { gridColumn: 'auto' } }, '留空则使用方案中的目标数量。'),
          ),
        ),
        field(
          '种子',
          h(
            'div',
            { class: 'field-row' },
            seedInput,
            h(
              'button',
              {
                class: 'btn',
                type: 'button',
                title: '换一个随机种子',
                onclick: () => {
                  seedInput.value = randomSeed();
                  updateHint();
                },
              },
              '换一个',
            ),
          ),
        ),
        field('配额预览', hint),
      ),
      footer: [
        h('div', { class: 'spacer' }),
        h('button', { class: 'btn', type: 'button', onclick: () => dialog.close() }, '取消'),
        h(
          'button',
          {
            class: 'btn primary',
            type: 'button',
            id: 'btn-generate-run',
            onclick: () => {
              const seed = seedInput.value.trim() || randomSeed();
              const overrideTarget = Number(targetInput.value);
              const profileOverride =
                Number.isFinite(overrideTarget) && overrideTarget > 0 && overrideTarget !== profile.plotTarget
                  ? { ...profile, plotTarget: Math.round(overrideTarget) }
                  : null;
              try {
                const map = store.generate({ profileId: profile.id, seed, profileOverride });
                dialog.close();
                toast(`已生成 ${map.plots.length} 块地块 · ${map.seed}`, { type: 'success' });
              } catch (error) {
                console.error(error);
                toast(`生成失败：${error.message}`, { type: 'error' });
              }
            },
          },
          '生成',
        ),
      ],
    });
  }

  /* ---------------- import / export ---------------- */

  function loadFloorMap(map) {
    store.loadFloorMap(map);
    store.setMode('floor');
    window.requestAnimationFrame(() => floorEditor.fit());
  }

  async function importWorkspace() {
    const file = await pickFile({ accept: '.json,application/json' });
    if (!file) return;
    let text;
    try {
      text = await readFileText(file);
    } catch (error) {
      toast(`读取文件失败：${error.message}`, { type: 'error' });
      return;
    }

    // 优先识别独立「条带模板库 JSON」与「单层地图 JSON」
    try {
      const peek = JSON.parse(text);
      if (peek && typeof peek === 'object' && peek.kind === 'plotweave-strip-templates') {
        const parsedTpl = parseTemplateLibrary(text, store.dungeon.cellTypes);
        if (!parsedTpl.ok) {
          toast(`导入模板库失败：${parsedTpl.errors[0] || '格式错误'}`, { type: 'error' });
          return;
        }
        const ok = await confirmDialog({
          title: '导入条带模板库',
          message: `识别到条带模板库（${parsedTpl.stripTemplates.length} 个模板${parsedTpl.cellTypes ? ' + 全局格子配置' : ''}），是否替换当前模板库？（可撤销）`,
          confirmText: '导入替换',
        });
        if (!ok) return;
        store.importTemplateLibrary({
          cellTypes: parsedTpl.cellTypes,
          stripTemplates: parsedTpl.stripTemplates,
          templateGroups: parsedTpl.templateGroups,
          replace: true,
        });
        store.setMode('templates');
        templateEditor.loadTemplate(store.dungeon.stripTemplates[0] || null);
        templateEditor.renderAll();
        toast(`已导入 ${parsedTpl.stripTemplates.length} 个条带模板`, { type: 'success' });
        return;
      }
      if (peek?.kind === 'plotweave-map-pools') {
        const parsedPools = parseWorldPools(text);
        if (!parsedPools.ok) {
          toast(`导入地图池失败：${parsedPools.errors[0]}`, { type: 'error' });
          return;
        }
        const total = parsedPools.worldPools.pools.reduce((sum, p) => sum + p.entries.length, 0);
        const ok = await confirmDialog({
          title: '导入完整地图四等级配置',
          message: `识别到包含 4 个等级共 ${total} 条配置的地图池 JSON，是否导入到「完整地图」？（可撤销）`,
          confirmText: '导入替换',
        });
        if (!ok) return;
        store.updateWorldPools((draft) => {
          draft.pools = parsedPools.worldPools.pools;
        }, '导入完整地图配置 JSON');
        store.setMode('world');
        toast(`已导入 ${total} 条完整地图配置`, { type: 'success' });
        return;
      }
      if (peek?.kind === 'plotweave-floor-map') {
        try {
          const map = peek.schemaVersion !== undefined ? applyFloorFile(peek) : peek;
          if (peek.schemaVersion === undefined) recomputeFloorProgression(map, map.progression, store.dungeon.cellTypes);
          recomputeFloorMetrics(map, store.dungeon.cellTypes);
          loadFloorMap(map);
          toast(`已载入单层地图「${map.seed || map.id}」`, { type: 'success' });
        } catch (error) { toast(`地图读取失败：${error.message}`, { type: 'error' }); }
        return;
      }
    } catch {
      /* 交由后续 parseBundle 统一报错 */
    }

    const parsed = parseBundle(text);
    if (!parsed.ok || !parsed.project) {
      const projectIssues = parsed.projectErrors?.length || 0;
      const mapIssues = parsed.mapErrors?.length || 0;
      const lines = [
        `文件：${file.name} · ${text.length} 字符`,
        projectIssues || mapIssues
          ? `工程问题 ${projectIssues} 个 · 地图问题 ${mapIssues} 个`
          : '没有读到可恢复的工程数据。',
        '',
        formatImportErrors(parsed),
      ];
      let dialog = null;
      const actions = parsed.project
        ? [
            h(
              'button',
              {
                class: 'btn',
                onclick: () => {
                  dialog?.close();
                  store.loadWorkspace({ project: parsed.project, map: null });
                  toast('已导入工程，地图未载入；请检查校验问题后重新生成', {
                    type: 'warning',
                  });
                },
              },
              '仅导入工程',
            ),
          ]
        : [];
      dialog = openTextViewer({
        title: '导入失败',
        subtitle: `文件「${file.name}」未通过 PlotWeave 校验。`,
        text: lines.join('\n'),
        filename: 'plotweave-import-errors.txt',
        actions,
      });
      return;
    }
    if (store.maps.length > 0) {
      const ok = await confirmDialog({
        title: '导入工程',
        message: `将导入「${parsed.project.meta.name}」${parsed.map ? '及其地图' : '（不含地图）'}，当前工程与地图会被替换（可撤销）。继续？`,
        confirmText: '导入',
      });
      if (!ok) return;
    }
    store.loadWorkspace({ project: parsed.project, map: parsed.map || null });
    if (parsed.map) window.requestAnimationFrame(() => canvas.fit());
    toast(`已导入「${parsed.project.meta.name}」`, {
      type: 'success',
      detail: parsed.warnings?.length ? `${parsed.warnings.length} 条兼容性提示` : '',
    });
  }

  async function exportWorkspace() {
    const map = store.currentMap;
    const floorMap = store.currentFloorMap;
    const name = slugify(store.project.meta.name);
    const rows = [
      {
        title: '条带模板库 JSON',
        hint: `包含全局格子配置与全部 ${store.dungeon.stripTemplates.length} 个条带模板，可独立导入复用。`,
        run: () => {
          downloadText(
            `${name}-strip-templates.json`,
            serializeTemplateLibrary({
              cellTypes: store.dungeon.cellTypes,
              stripTemplates: store.dungeon.stripTemplates,
              templateGroups: store.dungeon.templateGroups,
            }),
          );
          toast('条带模板库 JSON 已导出', { type: 'success' });
        },
      },
      {
        title: '当前单层地图 JSON',
        hint: floorMap
          ? `包含三区域三角拓扑、条带实例、门/交叉、跳数距离、条带等级与敌人等级（${floorMap.seed}）。`
          : '当前尚未生成单层地图。',
        disabled: !floorMap,
        run: () => {
          downloadText(`${slugify(floorMap.seed)}-floor-map.json`, JSON.stringify(makeFloorFile(floorMap), null, 2));
          toast('单层地图 JSON 已导出', { type: 'success' });
        },
      },
      {
        title: '当前单层地图 PNG (2x)',
        hint: floorMap ? '导出高清单层三角地牢图（保留当前叠层与主题）。' : '当前尚未生成单层地图。',
        disabled: !floorMap,
        run: async () => {
          const blob = await floorEditor.canvas.toPngBlob({ scale: 2 });
          if (!blob) {
            toast('导出 PNG 失败', { type: 'error' });
            return;
          }
          downloadBlob(`${slugify(floorMap.seed)}-floor-map.png`, blob);
          toast('单层地图 PNG 已导出', { type: 'success' });
        },
      },
      {
        title: '完整地图四等级配置 JSON',
        hint: '包含等级 1 至 等级 4 全部地图池配置（含出生点区域、最小环长度等），单份文件可直接导入或供游戏读取。',
        run: () => {
          downloadText(`${name}-world-map-pools.json`, serializeWorldPools(store.worldPools));
          toast('完整地图配置 JSON 已导出', { type: 'success' });
        },
      },
      {
        title: '完整工程 JSON',
        hint: '包含全部条带模板、全局格子配置、单层方案与基础工程结构。',
        run: () => {
          downloadText(`${name}-project.json`, serializeProject(store.project));
          toast('工程 JSON 已导出', { type: 'success' });
        },
      },
      {
        title: '工程 + Bundle JSON',
        hint: '完整工程归档，可再次导入继续编辑。',
        run: () => {
          downloadText(`${name}-bundle.json`, serializeBundle(store.project, map));
          toast('Bundle 已导出', { type: 'success' });
        },
      },
    ];

    for (const info of registry.listExporters()) {
      const exporter = registry.getExporter(info.id);
      rows.push({
        title: `扩展 · ${info.label}`,
        hint: info.description || `由插件注册的 .${info.extension} 导出器。`,
        run: async () => {
          const result = await exporter.export({ project: store.project, map, store });
          if (result == null) return;
          if (typeof result === 'string') {
            downloadText(`${name}.${info.extension}`, result, info.mime);
          } else if (result instanceof Blob) {
            downloadBlob(`${name}.${info.extension}`, result);
          } else {
            throw new Error('扩展导出器必须返回字符串或 Blob');
          }
          toast(`「${info.label}」已导出`, { type: 'success' });
        },
      });
    }

    openModal({
      title: '导出',
      subtitle: `${store.project.meta.name} · ${map ? map.seed : '无地图'}`,
      body: h(
        'div',
        { class: 'action-row', style: { flexDirection: 'column', alignItems: 'stretch' } },
        ...rows.map((row) =>
          h(
            'button',
            {
              class: 'btn',
              type: 'button',
              disabled: row.disabled,
              style: { height: 'auto', padding: '9px 11px', textAlign: 'left', justifyContent: 'flex-start' },
              onclick: async () => {
                try {
                  await row.run();
                } catch (error) {
                  console.error(error);
                  toast(`导出失败：${error.message}`, { type: 'error' });
                }
              },
            },
            h(
              'span',
              { style: { display: 'flex', flexDirection: 'column', gap: '2px' } },
              h('strong', null, row.title),
              h('span', { class: 'field-hint', style: { gridColumn: 'auto' } }, row.hint),
            ),
          ),
        ),
      ),
      footer: [
        h('div', { class: 'spacer' }),
        h('button', { class: 'btn primary', type: 'button', onclick: () => qs('.modal-close')?.click() }, '完成'),
      ],
    });
  }

  /* ---------------- keyboard ---------------- */

  async function deleteSelection() {
    const selection = store.selection;
    const labels = {
      region: '区域',
      plotKind: '地块种类',
      subRoomKind: '子房间种类',
      catalog: '内容条目',
      profile: '生成方案',
      map: '地图',
    };
    if (!labels[selection.type]) return;
    const entity = store.findSelected();
    const name = entity?.name || store.currentMap?.seed || selection.id;
    const ok = await confirmDialog({
      title: `删除${labels[selection.type]}`,
      message: `确定删除「${name}」？该操作可以撤销。`,
      confirmText: '删除',
      danger: true,
    });
    if (!ok) return;
    let result = { ok: true };
    if (selection.type === 'region') result = store.deleteRegion(selection.id);
    else if (selection.type === 'plotKind') result = store.deletePlotKind(selection.id);
    else if (selection.type === 'subRoomKind') {
      store.deleteSubRoomKind(selection.id);
    } else if (selection.type === 'catalog') {
      store.deleteCatalogItem(selection.contentType, selection.id);
    } else if (selection.type === 'profile') result = store.deleteProfile(selection.id);
    else if (selection.type === 'map') store.deleteMap(selection.id);
    toast(result.ok ? `${labels[selection.type]}已删除` : result.message, {
      type: result.ok ? 'success' : 'error',
    });
  }

  document.addEventListener('keydown', (event) => {
    const mod = event.ctrlKey || event.metaKey;
    const key = event.key.toLowerCase();

    if (mod && key === 's') {
      event.preventDefault();
      if (directory.handle) void directoryAction(() => saveDirectory({ project: store.mode !== 'floor', map: store.mode === 'floor' }));
      else directoryMenu();
      return;
    }
    if (isEditableTarget(event.target)) return;
    if (mod && key === 'z') {
      event.preventDefault();
      if (event.shiftKey) store.redo();
      else store.undo();
      return;
    }
    if (mod && key === 'y') {
      event.preventDefault();
      store.redo();
      return;
    }
    if (mod) return;

    if (key === 'delete' || key === 'backspace') {
      event.preventDefault();
      deleteSelection();
      return;
    }
    if (key === '?') {
      event.preventDefault();
      openHelp();
      return;
    }
    if (key === 'f') {
      if (store.mode === 'floor') floorEditor.fit();
      else if (store.mode === 'pools') poolsEditor.fit();
      else if (store.mode === 'world') worldView.fitAll();
      else canvas.fit();
      return;
    }
    if (key === 'g') {
      if (store.mode === 'floor' || store.mode === 'pools' || store.mode === 'world') store.toggleUi('floorShowGrid');
      else store.toggleUi('showGrid');
      return;
    }
    if (key === 'n') { store.setMode('pools'); return; }
    if (key === '1') store.setMode('templates');
    else if (key === '2') store.setMode('pools');
    else if (key === '3') store.setMode('world');
    else if (key === 'escape') {
      if (store.mode === 'floor') store.selectFloorStrip(null);
      else if (store.mode === 'world') worldView.closeZoom();
      else if (store.mode === 'pools') {
        if (poolsEditor.exploration) poolsEditor.leave({ focus: true });
        else if (poolsEditor.selectedStripId) {
          poolsEditor.selectedStripId = null;
          poolsEditor.renderInspector();
          poolsEditor.canvas?.render();
        }
      } else store.select({ type: 'project' });
    }
  });

  /* ---------------- view sync ---------------- */

  function updateTopbar() {
    projectName.textContent = store.project.meta.name || '未命名工程';
    projectMeta.textContent = `${store.dungeon.stripTemplates.length} 个条带模板 · ${store.dungeon.cellTypes.length} 类格子 · ${store.floorMaps.length} 张单层`;
    undoBtn.disabled = !store.canUndo;
    redoBtn.disabled = !store.canRedo;
    document.title = `${store.project.meta.name || 'PlotWeave'} · 类银河城地牢编辑器`;
  }

  function updateToolbar() {
    for (const [id, button] of toolButtons) {
      button.classList.toggle('active', store.ui.tool === id);
    }
    for (const [key, button] of toggleButtons) {
      button.classList.toggle('active', Boolean(store.ui[key]));
    }
    zoomLabel.textContent = `${Math.round((canvas.view.scale / 24) * 100)}%`;
    seedLabel.querySelector('strong').textContent = store.currentMap?.seed || '—';
  }

  function updateLegend() {
    const regions = store.project.regions.filter((region) => !region.hidden);
    clear(legend);
    for (const region of regions) {
      legend.append(
        h(
          'button',
          {
            class: 'legend-item',
            type: 'button',
            title: `选择区域「${region.name}」`,
            style: { border: 0, background: 'transparent', padding: '0', cursor: 'pointer' },
            onclick: () => store.select({ type: 'region', id: region.id }),
          },
          h('span', { class: 'legend-swatch', style: { background: region.color } }),
          region.shortName || region.name,
        ),
      );
    }
  }

  function updateStatusbar() {
    const map = store.currentMap;
    const validation = store.validate();
    const errors = validation.errors?.length || 0;
    const warnings = validation.warnings?.length || 0;
    const storage = {
      new: '新工作区',
      restored: '已恢复本地工作区',
      saved: '已保存到本地',
      full: '本地存储已满，请导出备份',
      error: '本地存储不可用',
      invalid: '本地缓存损坏，已使用示例',
      idle: '未保存',
    }[store.storageStatus] || '未保存';
    const items = [
      h(
        'span',
        { class: 'status-item' },
        h('span', { class: `status-dot ${errors ? 'err' : warnings ? 'warn' : 'ok'}` }),
        errors ? `${errors} 个错误` : warnings ? `${warnings} 条警告` : '工程有效',
      ),
      map
        ? h(
            'span',
            { class: 'status-item' },
            `${map.plots.length} 地块`,
            h('span', { class: 'status-sep' }, '/'),
            `${map.links.length} 连接`,
            h('span', { class: 'status-sep' }, '/'),
            `${map.stats?.subRooms || 0} 子房间`,
          )
        : h('span', { class: 'status-item' }, '没有地图'),
      h('span', { class: 'status-item' }, `缩放 ${Math.round((canvas.view.scale / 24) * 100)}%`),
      h('span', { class: 'status-spacer' }),
      h('span', { class: 'status-item' }, storage),
      h('span', { class: 'status-item', style: { opacity: 0.65 } }, `v${APP_VERSION}`),
    ];
    mount(statusbar, items);
  }

  function updateEmpty() {
    emptyEl.hidden = Boolean(store.currentMap);
  }

  function syncAll() {
    syncModeViews();
    updateTopbar();
    updateToolbar();
    updateLegend();
    updateStatusbar();
    updateEmpty();
    mobile.sync();
  }

  store.on('change', event => {
    syncAll();
    if (event?.reason === 'workspace') {
      poolSession = undefined;
      publishedRevision = undefined;
      void poolsEditor.refresh({ reset: true, fetchCatalog: store.mode === 'pools' });
    }
    else if (store.mode === 'pools') poolsEditor.render();
  });
  store.on('ui', () => {
    syncModeViews();
    updateToolbar();
    updateStatusbar();
  });
  store.on('selection', () => {
    updateStatusbar();
    mobile.sync();
  });
  store.on('toast', (payload) => {
    toast(payload.message, { type: payload.type || 'info', detail: payload.detail || '' });
  });

  host.addEventListener('plotweave:zoom', () => {
    updateToolbar();
    updateStatusbar();
  });
  host.addEventListener('plotweave:focus-plot', (event) => {
    const plot = event.detail?.plot;
    if (plot) store.select({ type: 'plot', id: plot.id });
  });
  store.on('focus-plot', (plotId) => {
    const plot = store.currentMap?.plots.find((candidate) => candidate.id === plotId);
    if (plot) canvas.zoomToPlot(plot);
  });

  /* ---------------- start ---------------- */

  // 让工具栏第一次拿到画布的真实缩放。
  window.requestAnimationFrame(() => {
    floorEditor.fit();
    canvas.fit();
    syncAll();
  });
  syncAll();

  const app = {
    store,
    canvas,
    sidebar,
    inspector,
    mobile,
    templateEditor,
    poolsEditor,
    floorEditor,
    worldView,
    generate: () => floorEditor.runGenerate(randomSeed()),
    importWorkspace,
    exportWorkspace,
    syncAll,
    theme: {
      get: getTheme,
      set: (theme) => {
        const applied = applyTheme(theme);
        canvas.setTheme(applied);
        floorEditor.setTheme(applied);
        syncThemeButton(applied);
        return applied;
      },
      toggle: () => {
        const next = toggleTheme();
        canvas.setTheme(next);
        floorEditor.setTheme(next);
        syncThemeButton(next);
        return next;
      },
    },
  };

  if (store.storageStatus === 'restored') {
    toast('已恢复上次的工作区', { type: 'info' });
  } else if (store.storageStatus === 'invalid') {
    toast('本地缓存格式不兼容，已载入示例工程。', { type: 'error' });
  }
  console.info(
    `[PlotWeave] ${APP_VERSION} · schema ${SCHEMA_VERSION} · ${CONTENT_TYPES.length} 类内容 · ${timestamp()}`,
  );
  window.PlotWeave = app;
  loadOptionalPlugins(app);
  return app;
}

let app = null;
try {
  app = boot();
} catch (error) {
  console.error('[PlotWeave] 启动失败', error);
  const root = qs('#app') || document.body;
  root.innerHTML = '';
  root.append(
    h(
      'div',
      { style: { padding: '40px', fontFamily: 'system-ui, sans-serif', color: '#e8edf3' } },
      h('h1', { style: { color: '#d97b6c' } }, 'PlotWeave 启动失败'),
      h('pre', { style: { whiteSpace: 'pre-wrap', color: '#9aa7b8' } }, String(error?.stack || error)),
    ),
  );
}
