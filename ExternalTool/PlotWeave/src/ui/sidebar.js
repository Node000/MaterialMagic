/**
 * 左侧结构浏览器：区域 / 地块 / 子房间 / 内容目录 / 生成方案 / 地图历史。
 */

import { CONTENT_TYPES } from '../core/constants.js';
import { h, mount, clear, icon, fmt } from './dom.js';
import { confirmDialog, openFormModal, toast } from './modals.js';

const TABS = [
  { id: 'structure', label: '结构' },
  { id: 'content', label: '内容' },
  { id: 'profiles', label: '方案' },
];

const COLLAPSED_KEY = 'plotweave:collapsed:v1';

function loadCollapsed() {
  try {
    const raw = JSON.parse(window.localStorage.getItem(COLLAPSED_KEY) || '[]');
    return new Set(Array.isArray(raw) ? raw.filter((key) => typeof key === 'string') : []);
  } catch {
    return new Set();
  }
}

function saveCollapsed(value) {
  try {
    window.localStorage.setItem(COLLAPSED_KEY, JSON.stringify([...value]));
  } catch {
    /* 本地存储不可用时只保留当前会话状态。 */
  }
}

function matches(filter, ...values) {
  if (!filter) return true;
  const query = filter.trim().toLowerCase();
  if (!query) return true;
  return values.some((value) => String(value || '').toLowerCase().includes(query));
}

function row({ active, onClick, swatch, round, name, sub, tail, actions = [], indent = 0 }) {
  const depth = Math.max(0, Number(indent) || 0);
  return h(
    'div',
    {
      class: `row${active ? ' active' : ''}${depth > 0 ? ' tree-indent' : ''}`,
      role: 'button',
      tabindex: '0',
      style: depth > 1 ? { paddingLeft: `${30 + (depth - 1) * 14}px` } : undefined,
      onclick: onClick,
      onkeydown: (event) => {
        if (event.key === 'Enter' || event.key === ' ') {
          event.preventDefault();
          onClick?.();
        }
      },
    },
    swatch
      ? h('span', {
          class: `row-swatch${round ? ' round' : ''}`,
          style: { background: swatch },
        })
      : h('span', { class: 'mini-icon' }, '·'),
    h('span', { class: 'row-main' }, h('span', { class: 'row-name' }, name), sub ? h('span', { class: 'row-sub' }, sub) : null),
    tail ? h('span', { class: 'row-tail' }, tail) : null,
    ...actions,
  );
}

function treeCaret(open, onClick, label) {
  return h(
    'button',
    {
      class: `tree-caret${open ? ' open' : ''}`,
      type: 'button',
      title: label,
      'aria-label': label,
      'aria-expanded': String(open),
      onclick: (event) => {
        event.stopPropagation();
        onClick();
      },
    },
    icon('chevron', 11),
  );
}

function treeAction(name, title, onClick) {
  return h(
    'button',
    {
      class: 'tree-action',
      type: 'button',
      title,
      'aria-label': title,
      onclick: (event) => {
        event.stopPropagation();
        onClick();
      },
    },
    icon(name, 11),
  );
}

function sectionHead(title, count, onAdd, addTitle = '新建') {
  return h(
    'div',
    { class: 'group-head' },
    h('span', null, title),
    h('span', { class: 'count' }, String(count)),
    h('span', { class: 'spacer' }),
    onAdd
      ? h(
          'button',
          {
            class: 'btn icon ghost small',
            title: addTitle,
            'aria-label': addTitle,
            onclick: (event) => {
              event.stopPropagation();
              onAdd();
            },
          },
          icon('plus', 12),
        )
      : null,
  );
}

export class Sidebar {
  constructor({ root, store }) {
    this.root = root;
    this.store = store;
    this.filterValue = '';
    this.collapsed = loadCollapsed();
    this.build();
    store.on('change', () => this.renderBody());
    store.on('selection', () => this.renderBody());
    store.on('ui', (patch) => {
      if ('filter' in (patch || {})) {
        this.filterValue = store.ui.filter;
        this.renderBody();
      } else {
        this.renderTabs();
        this.renderBody();
      }
    });
    this.renderTabs();
    this.renderBody();
  }

  build() {
    const tabs = h('div', { class: 'tabs', role: 'tablist' });
    const search = h('input', {
      class: 'input',
      type: 'search',
      placeholder: '过滤区域、地块、内容…',
      value: this.filterValue,
      oninput: () => {
        this.filterValue = search.value;
        this.store.setFilter(search.value);
      },
    });
    this.tabsEl = tabs;
    this.searchEl = search;
    this.bodyEl = h('div', { class: 'sidebar-scroll' });
    mount(
      this.root,
      h(
        'div',
        { class: 'panel-head' },
        h(
          'div',
          { class: 'panel-title-row' },
          h('span', { class: 'panel-title' }, '工程结构'),
          h('span', { class: 'spacer' }),
          h(
            'button',
            {
              class: 'btn icon ghost small',
              title: '新建区域',
              'aria-label': '新建区域',
              onclick: () => {
                this.store.setTab('structure');
                const region = this.store.addRegion();
                if (region) toast(`已新建区域「${region.name}」`, { type: 'success' });
              },
            },
            icon('plus', 12),
          ),
        ),
        tabs,
      ),
      h('div', { class: 'sidebar-search' }, search),
      this.bodyEl,
    );
  }

  renderTabs() {
    clear(this.tabsEl);
    for (const tab of TABS) {
      this.tabsEl.append(
        h(
          'button',
          {
            class: `tab${this.store.ui.tab === tab.id ? ' active' : ''}`,
            role: 'tab',
            'aria-selected': String(this.store.ui.tab === tab.id),
            onclick: () => this.store.setTab(tab.id),
          },
          tab.label,
        ),
      );
    }
  }

  renderBody() {
    const tab = this.store.ui.tab;
    if (tab !== 'content') this.searchEl.placeholder = '过滤区域、地块、方案…';
    else this.searchEl.placeholder = '过滤内容名称或 ID…';
    if (tab === 'content') this.renderContent();
    else if (tab === 'profiles') this.renderProfiles();
    else this.renderStructure();
  }

  regionPlotCount(regionId) {
    const map = this.store.currentMap;
    if (!map) return 0;
    return map.plots.filter((plot) => plot.regionId === regionId).length;
  }

  toggleCollapse(key) {
    if (this.collapsed.has(key)) this.collapsed.delete(key);
    else this.collapsed.add(key);
    saveCollapsed(this.collapsed);
    this.renderBody();
  }

  createGroup(region, parentGroup = null) {
    const groups = this.store.project.plotGroups || [];
    const siblings = groups.filter(
      (group) =>
        group.regionId === region.id && (group.parentGroupId || '') === (parentGroup?.id || ''),
    );
    const input = h('input', {
      class: 'input',
      name: 'name',
      value: `折叠组 ${siblings.length + 1}`,
      maxlength: '40',
      placeholder: '例如：入口区 / 核心区',
    });
    openFormModal({
      title: parentGroup ? '新建子折叠组' : '新建折叠组',
      subtitle: parentGroup
        ? `在「${parentGroup.name}」中再建一个可折叠的组。`
        : `把「${region.name}」下的地块收进一个可折叠的组。`,
      fields: [
        h(
          'label',
          { class: 'field stack' },
          h('span', { class: 'field-label' }, '名称'),
          input,
        ),
      ],
      submitText: '创建',
      onSubmit: (data) => {
        const selection = this.store.selection;
        const selectedKind =
          selection.type === 'plotKind' ? this.store.findPlotKind(selection.id) : null;
        const group = this.store.addPlotGroup(
          region.id,
          data.name,
          selectedKind?.regionId === region.id ? selectedKind.id : '',
          parentGroup?.id || '',
        );
        if (group) toast(`已创建折叠组「${group.name}」`, { type: 'success' });
      },
    });
  }

  childGroups(regionId, parentGroupId) {
    return (this.store.project.plotGroups || [])
      .filter(
        (group) =>
          group.regionId === regionId &&
          (group.parentGroupId || '') === (parentGroupId || ''),
      )
      .sort((a, b) => (a.order || 0) - (b.order || 0) || a.id.localeCompare(b.id));
  }

  subtreeGroups(groupId) {
    const groups = this.store.project.plotGroups || [];
    const result = [];
    const seen = new Set();
    const visit = (id) => {
      if (seen.has(id)) return;
      seen.add(id);
      const group = groups.find((candidate) => candidate.id === id);
      if (!group) return;
      result.push(group);
      for (const child of groups.filter(
        (candidate) => candidate.parentGroupId === id && candidate.regionId === group.regionId,
      )) {
        visit(child.id);
      }
    };
    visit(groupId);
    return result;
  }

  subtreeKinds(groupId, regionKinds) {
    const ids = new Set(this.subtreeGroups(groupId).map((group) => group.id));
    return regionKinds.filter((kind) => ids.has(kind.groupId));
  }

  groupMatchesFilter(group, regionKinds, filter) {
    if (!filter) return true;
    const groups = this.subtreeGroups(group.id);
    if (groups.some((candidate) => matches(filter, candidate.name, candidate.id))) return true;
    const ids = new Set(groups.map((candidate) => candidate.id));
    return regionKinds.some(
      (kind) =>
        ids.has(kind.groupId) &&
        matches(filter, kind.name, kind.shortName, kind.id, ...(kind.tags || [])),
    );
  }

  renderGroupNode(group, region, selection, regionKinds, depth, filter, ancestors = new Set()) {
    if (ancestors.has(group.id)) return [];
    const lineage = new Set(ancestors).add(group.id);
    const nodes = [];
    const children = this.childGroups(region.id, group.id);
    const directKinds = regionKinds.filter((kind) => kind.groupId === group.id);
    const totalKinds = this.subtreeKinds(group.id, regionKinds);
    const groupKey = `group:${group.id}`;
    const groupOpen = Boolean(filter) || !this.collapsed.has(groupKey);
    const active = selection.type === 'plotGroup' && selection.id === group.id;
    nodes.push(
      h(
        'div',
        {
          class: `tree-folder${active ? ' active' : ''}`,
          role: 'button',
          tabindex: '0',
          style: { paddingLeft: `${14 + depth * 14}px` },
          onclick: () => this.store.select({ type: 'plotGroup', id: group.id }),
          onkeydown: (event) => {
            if (event.key === 'Enter' || event.key === ' ') {
              event.preventDefault();
              this.store.select({ type: 'plotGroup', id: group.id });
            }
          },
        },
        treeCaret(groupOpen, () => this.toggleCollapse(groupKey), `${groupOpen ? '收起' : '展开'}${group.name}`),
        h('span', { class: 'tree-folder-icon' }, icon('folder', 12)),
        h('span', { class: 'tree-folder-name' }, group.name),
        h('span', { class: 'tree-count' }, String(totalKinds.length)),
        h('span', { class: 'spacer' }),
        treeAction('folder-plus', '在组内新建子折叠组', () => this.createGroup(region, group)),
        treeAction('plus', '在折叠组中新建地块', () => {
          const kind = this.store.addPlotKind(region.id, group.id);
          if (kind) toast(`已在「${group.name}」中新建「${kind.name}」`, { type: 'success' });
        }),
        treeAction('trash', '删除折叠组（地块和子组会上移一级）', async () => {
          const childCount = children.length;
          const ok = await confirmDialog({
            title: '删除折叠组',
            message: `确定删除「${group.name}」？组内 ${directKinds.length} 种地块会移到${group.parentGroupId ? '上级组' : '未归组'}，${childCount} 个子折叠组会上移一级，都不会被删除。`,
            confirmText: '删除',
            danger: true,
          });
          if (!ok) return;
          const result = this.store.deletePlotGroup(group.id);
          toast(result.ok ? '折叠组已删除，地块和子组已上移一级' : result.message, {
            type: result.ok ? 'success' : 'error',
          });
        }),
      ),
    );
    if (!groupOpen) return nodes;
    for (const child of children) {
      nodes.push(
        ...this.renderGroupNode(
          child,
          region,
          selection,
          regionKinds,
          depth + 1,
          filter,
          lineage,
        ),
      );
    }
    for (const kind of directKinds) {
      nodes.push(this.renderKindRow(kind, region, selection, depth + 2));
    }
    return nodes;
  }

  renderKindRow(kind, region, selection, indent) {
    const length = `${kind.length.min}–${kind.length.max}`;
    return row({
      active: selection.type === 'plotKind' && selection.id === kind.id,
      onClick: () => this.store.select({ type: 'plotKind', id: kind.id }),
      swatch: kind.visual?.color || region.color,
      name: kind.name,
      sub: `${length} 格 · 上限 ${kind.connections.max} · ${kind.connections.anchors}`,
      tail: kind.generation.max ? `×${kind.generation.max}` : '×0',
      indent,
    });
  }

  renderStructure() {
    const { project } = this.store;
    const filter = this.filterValue;
    const selection = this.store.selection;
    const nodes = [];

    const regions = project.regions.filter((region) =>
      matches(filter, region.name, region.shortName, region.id, ...(region.tags || [])),
    );
    nodes.push(
      sectionHead('区域', project.regions.length, () => {
        const region = this.store.addRegion();
        if (region) toast(`已新建区域「${region.name}」`, { type: 'success' });
      }, '新建区域'),
    );
    for (const region of regions) {
      const kindCount = project.plotKinds.filter((kind) => kind.regionId === region.id).length;
      nodes.push(
        row({
          active: selection.type === 'region' && selection.id === region.id,
          onClick: () => this.store.select({ type: 'region', id: region.id }),
          swatch: region.color,
          name: region.name,
          sub: `${kindCount} 种地块 · ${this.regionPlotCount(region.id)} 块`,
          tail: region.hidden ? '隐藏' : '',
        }),
      );
    }
    if (regions.length === 0) nodes.push(h('div', { class: 'empty-hint' }, '没有匹配的区域。'));

    const filteredKinds = project.plotKinds.filter((kind) =>
      matches(filter, kind.name, kind.shortName, kind.id, ...(kind.tags || [])),
    );
    nodes.push(sectionHead('地块种类', filteredKinds.length, () => {
      const selectedRegion =
        selection.type === 'region'
          ? this.store.findRegion(selection.id)
          : project.regions[0] || null;
      const kind = this.store.addPlotKind(selectedRegion?.id);
      if (kind) toast(`已新建地块「${kind.name}」`, { type: 'success' });
    }, '新建地块种类'));

    const groups = (project.plotGroups || []).slice().sort((a, b) => (a.order || 0) - (b.order || 0));

    for (const region of project.regions) {
      const regionKinds = filteredKinds.filter((kind) => kind.regionId === region.id);
      const regionGroups = groups.filter((group) => group.regionId === region.id);
      const groupIds = new Set(regionGroups.map((group) => group.id));
      const roots = regionGroups.filter(
        (group) => !group.parentGroupId || !groupIds.has(group.parentGroupId),
      );
      const visibleRoots = roots.filter((group) =>
        this.groupMatchesFilter(group, regionKinds, filter),
      );
      const regionMatches = matches(filter, region.name, region.id);
      if (regionKinds.length === 0 && visibleRoots.length === 0 && !regionMatches) continue;

      const regionKey = `region:${region.id}`;
      const regionOpen = Boolean(filter) || !this.collapsed.has(regionKey);
      const regionKindCount = project.plotKinds.filter((kind) => kind.regionId === region.id).length;
      nodes.push(
        h(
          'div',
          {
            class: 'tree-region-head',
            role: 'button',
            tabindex: '0',
            onclick: () => this.toggleCollapse(regionKey),
            onkeydown: (event) => {
              if (event.key === 'Enter' || event.key === ' ') {
                event.preventDefault();
                this.toggleCollapse(regionKey);
              }
            },
          },
          treeCaret(regionOpen, () => this.toggleCollapse(regionKey), `${regionOpen ? '收起' : '展开'}${region.name}`),
          h('span', { class: 'tree-dot', style: { color: region.color } }, '●'),
          h('span', { class: 'tree-region-name' }, region.name),
          h('span', { class: 'tree-count' }, String(regionKindCount)),
          h('span', { class: 'spacer' }),
          treeAction('plus', '在此区域新建折叠组', () => this.createGroup(region)),
        ),
      );
      if (!regionOpen) continue;

      for (const group of visibleRoots) {
        nodes.push(
          ...this.renderGroupNode(group, region, selection, regionKinds, 0, filter),
        );
      }

      const ungrouped = regionKinds.filter(
        (kind) => !kind.groupId || !groupIds.has(kind.groupId),
      );
      if (regionGroups.length > 0 && ungrouped.length > 0) {
        nodes.push(h('div', { class: 'tree-subhead' }, '未归组'));
      }
      for (const kind of ungrouped) {
        nodes.push(this.renderKindRow(kind, region, selection, false));
      }
    }
    if (filteredKinds.length === 0) nodes.push(h('div', { class: 'empty-hint' }, '没有匹配的地块种类。'));

    const subRooms = project.subRoomKinds.filter((room) =>
      matches(filter, room.name, room.id, ...(room.tags || [])),
    );
    nodes.push(sectionHead('子房间', project.subRoomKinds.length, () => {
      const room = this.store.addSubRoomKind();
      if (room) toast(`已新建子房间「${room.name}」`, { type: 'success' });
    }, '新建子房间'));

    for (const room of subRooms) {
      const usage = project.plotKinds.filter((kind) => kind.subRooms.pool.includes(room.id)).length;
      nodes.push(
        row({
          active: selection.type === 'subRoomKind' && selection.id === room.id,
          onClick: () => this.store.select({ type: 'subRoomKind', id: room.id }),
          swatch: room.color,
          round: true,
          name: room.name,
          sub: `${usage} 个地块引用`,
        }),
      );
    }
    if (subRooms.length === 0) nodes.push(h('div', { class: 'empty-hint' }, '没有匹配的子房间。'));

    mount(this.bodyEl, ...nodes);
  }

  renderContent() {
    const { project } = this.store;
    const filter = this.filterValue;
    const selection = this.store.selection;
    const nodes = [];

    for (const type of CONTENT_TYPES) {
      const items = (project.catalogs[type.id] || []).filter((item) =>
        matches(filter, item.name, item.id, item.rarity, ...(item.tags || [])),
      );
      nodes.push(
        h(
          'div',
          { class: 'group-head' },
          h('span', { style: { color: type.color } }, `${type.icon} ${type.label}`),
          h('span', { class: 'count' }, String((project.catalogs[type.id] || []).length)),
          h('span', { class: 'spacer' }),
          h(
            'button',
            {
              class: 'btn icon ghost small',
              title: `新建${type.label}`,
              onclick: () => {
                const item = this.store.addCatalogItem(type.id);
                if (item) toast(`已新建${type.label}「${item.name}」`, { type: 'success' });
              },
            },
            icon('plus', 12),
          ),
        ),
      );
      for (const item of items) {
        const usage = this.store.contentUsage(type.id, item.id);
        nodes.push(
          row({
            active:
              selection.type === 'catalog' &&
              selection.id === item.id &&
              selection.contentType === type.id,
            onClick: () => this.store.select({ type: 'catalog', id: item.id, contentType: type.id }),
            swatch: type.color,
            round: true,
            name: item.name,
            sub: `${item.id} · ${item.rarity || 'common'}`,
            tail: usage.total ? `${usage.total} 处` : '未使用',
          }),
        );
      }
      if (items.length === 0) {
        nodes.push(
          h(
            'div',
            { class: 'empty-hint' },
            filter ? '没有匹配的条目。' : '目录为空。点击 + 新建条目。',
          ),
        );
      }
    }
    mount(this.bodyEl, nodes);
  }

  renderProfiles() {
    const { project } = this.store;
    const selection = this.store.selection;
    const filter = this.filterValue;
    const nodes = [];

    const profiles = project.profiles.filter((profile) =>
      matches(filter, profile.name, profile.id, profile.description),
    );
    nodes.push(sectionHead('生成方案', project.profiles.length, () => {
      const profile = this.store.addProfile();
      if (profile) toast(`已新建方案「${profile.name}」`, { type: 'success' });
    }, '新建生成方案'));
    for (const profile of profiles) {
      const regions = profile.regionIds.length
        ? profile.regionIds.map((id) => this.store.findRegion(id)?.name || id).join('、')
        : '全部区域';
      nodes.push(
        row({
          active: selection.type === 'profile' && selection.id === profile.id,
          onClick: () => this.store.select({ type: 'profile', id: profile.id }),
          swatch: '#d9a441',
          round: true,
          name: profile.name,
          sub: `目标 ${profile.plotTarget} 块 · ${regions}`,
          tail: profile.allowCrossRegion ? `跨区 ${Math.round(profile.crossRegionRatio * 100)}%` : '禁跨区',
        }),
      );
    }
    if (profiles.length === 0) nodes.push(h('div', { class: 'empty-hint' }, '没有匹配的生成方案。'));

    nodes.push(
      h(
        'div',
        { class: 'group-head' },
        h('span', null, '地图历史'),
        h('span', { class: 'count' }, String(this.store.maps.length)),
        h('span', { class: 'spacer' }),
      ),
    );
    if (this.store.maps.length === 0) {
      nodes.push(h('div', { class: 'empty-hint' }, '还没有生成过地图。点击顶栏「生成地图」。'));
    }
    for (const map of this.store.maps) {
      const isCurrent = map.id === this.store.state.currentMapId;
      nodes.push(
        row({
          active: isCurrent,
          onClick: () => this.store.selectMap(map.id),
          swatch: '#6aa9c9',
          round: true,
          name: map.seed,
          sub: `${map.plots.length} 块 · ${map.links.length} 连接 · ${map.stats?.regions || 0} 区域`,
          tail: isCurrent ? '当前' : '',
          actions: [
            h(
              'button',
              {
                class: 'btn icon ghost small',
                title: '删除这张地图',
                onclick: async (event) => {
                  event.stopPropagation();
                  const ok = await confirmDialog({
                    title: '删除地图',
                    message: `确定删除种子为「${map.seed}」的地图？该操作可以撤销。`,
                    confirmText: '删除',
                    danger: true,
                  });
                  if (ok) {
                    const before = map.id;
                    const index = this.store.maps.findIndex((candidate) => candidate.id === before);
                    if (index >= 0) {
                      this.store.deleteMap(before);
                      toast('地图已删除', { type: 'success' });
                    }
                  }
                },
              },
              icon('trash', 12),
            ),
          ],
        }),
      );
    }

    nodes.push(
      h(
        'div',
        { class: 'group-head' },
        h('span', null, '工程'),
        h('span', { class: 'spacer' }),
      ),
    );
    nodes.push(
      h(
        'div',
        { style: { padding: '2px 6px 10px' } },
        h(
          'div',
          { class: 'kv' },
          h('span', { class: 'k' }, '地块'),
          h('span', { class: 'v' }, String(project.plotKinds.length)),
        ),
        h(
          'div',
          { class: 'kv' },
          h('span', { class: 'k' }, '区域'),
          h('span', { class: 'v' }, String(project.regions.length)),
        ),
        h(
          'div',
          { class: 'kv' },
          h('span', { class: 'k' }, '子房间'),
          h('span', { class: 'v' }, String(project.subRoomKinds.length)),
        ),
        h(
          'div',
          { class: 'kv' },
          h('span', { class: 'k' }, '网格'),
          h(
            'span',
            { class: 'v' },
            `${project.settings.grid.width} × ${project.settings.grid.height}`,
          ),
        ),
        h(
          'div',
          { class: 'kv' },
          h('span', { class: 'k' }, '内容条目'),
          h(
            'span',
            { class: 'v' },
            fmt(
              CONTENT_TYPES.reduce((total, type) => total + (project.catalogs[type.id]?.length || 0), 0),
            ),
          ),
        ),
      ),
    );

    mount(this.bodyEl, nodes);
  }
}
