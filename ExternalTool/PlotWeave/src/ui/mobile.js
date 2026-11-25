/**
 * 移动端外壳：底部导航 + 上拉面板。
 *
 * 桌面端仍是三栏布局；窄屏下侧栏与检视器变成底部抽屉，画布占满主区域。
 * 这里只负责面板开关与导航状态，不触碰工程 / 地图数据。
 */

import { h, icon, mount, qs } from './dom.js';

const MOBILE_QUERY = '(max-width: 900px)';
const TAB_LABELS = { structure: '工程结构', content: '内容目录', profiles: '生成方案' };
const TARGETS = [
  { id: 'structure', label: '结构', icon: 'folder', panel: 'sidebar' },
  { id: 'content', label: '内容', icon: 'contents', panel: 'sidebar' },
  { id: 'profiles', label: '方案', icon: 'layers', panel: 'sidebar' },
  { id: 'inspector', label: '属性', icon: 'sliders', panel: 'inspector' },
];

function labelForSelection(store) {
  const selection = store.selection;
  if (!selection || selection.type === 'project') return '工程属性';
  const selected = store.findSelected();
  if (selection.type === 'plot') return `地图地块 · ${selection.id}`;
  return selected?.name ? `${selected.name}` : '属性';
}

export class MobileShell {
  constructor({ store, sidebar, inspector, canvas }) {
    this.store = store;
    this.sidebar = sidebar;
    this.inspector = inspector;
    this.canvas = canvas;
    this.nav = qs('#mobile-nav');
    this.scrim = qs('#mobile-scrim');
    this.buttons = new Map();
    this.panel = null;
    this.media = window.matchMedia(MOBILE_QUERY);

    this.buildNav();
    this.sheetBars = {
      sidebar: this.addSheetBar(sidebar.root, '工程结构'),
      inspector: this.addSheetBar(inspector.root, '属性'),
    };
    this.scrim.addEventListener('click', () => this.close());
    this.media.addEventListener('change', () => {
      if (!this.media.matches) this.close();
      this.sync();
    });
    this.onKey = (event) => {
      if (event.key === 'Escape' && this.panel) this.close();
    };
    document.addEventListener('keydown', this.onKey);
    store.on('selection', () => this.sync());
    store.on('ui', () => this.sync());
    this.sync();
  }

  get isMobile() {
    return this.media.matches;
  }

  buildNav() {
    mount(
      this.nav,
      ...TARGETS.map((target) => {
        const button = h(
          'button',
          {
            class: 'mobile-nav-btn',
            type: 'button',
            'data-panel': target.id,
            'aria-label': target.label,
            onclick: () => this.toggle(target.id),
          },
          h('span', { class: 'mobile-nav-icon' }, icon(target.icon, 19)),
          h('span', { class: 'mobile-nav-label' }, target.label),
        );
        this.buttons.set(target.id, button);
        return button;
      }),
    );
  }

  addSheetBar(root, title) {
    const titleEl = h('strong', { class: 'mobile-sheet-title' }, title);
    const bar = h(
      'div',
      { class: 'mobile-sheet-head' },
      h('span', { class: 'mobile-sheet-grip', 'aria-hidden': 'true' }),
      titleEl,
      h(
        'button',
        {
          class: 'mobile-sheet-close',
          type: 'button',
          'aria-label': '收起面板',
          onclick: () => this.close(),
        },
        icon('close', 13),
        h('span', null, '收起'),
      ),
    );
    root.prepend(bar);
    return titleEl;
  }

  toggle(target) {
    if (target === 'inspector') {
      if (this.panel === 'inspector') this.close();
      else this.open('inspector');
      return;
    }
    if (this.panel === 'sidebar' && this.store.ui.tab === target) {
      this.close();
      return;
    }
    this.store.setTab(target);
    this.open('sidebar');
  }

  open(panel) {
    this.panel = panel;
    document.body.classList.toggle('mobile-sidebar-open', panel === 'sidebar');
    document.body.classList.toggle('mobile-inspector-open', panel === 'inspector');
    document.body.classList.add('mobile-sheet-open');
    this.scrim.hidden = false;
    this.sync();
    this.canvas?.render();
  }

  close() {
    this.panel = null;
    document.body.classList.remove('mobile-sidebar-open', 'mobile-inspector-open', 'mobile-sheet-open');
    this.scrim.hidden = true;
    this.sync();
  }

  sync() {
    if (!this.isMobile && this.panel) {
      this.panel = null;
      document.body.classList.remove('mobile-sidebar-open', 'mobile-inspector-open', 'mobile-sheet-open');
      this.scrim.hidden = true;
    }
    const tab = this.store.ui.tab;
    for (const target of TARGETS) {
      const button = this.buttons.get(target.id);
      if (!button) continue;
      const active =
        target.id === 'inspector'
          ? this.panel === 'inspector'
          : this.panel === 'sidebar' && tab === target.id;
      button.classList.toggle('active', active);
      button.setAttribute('aria-pressed', String(active));
    }
    const inspectorButton = this.buttons.get('inspector');
    inspectorButton?.classList.toggle(
      'has-selection',
      Boolean(this.store.selection && this.store.selection.type !== 'project'),
    );
    if (this.sheetBars) {
      this.sheetBars.sidebar.textContent = TAB_LABELS[tab] || '工程结构';
      this.sheetBars.inspector.textContent = labelForSelection(this.store);
    }
  }
}
