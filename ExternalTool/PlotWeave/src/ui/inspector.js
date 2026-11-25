/**
 * 右侧属性检视器。
 *
 * 选中什么就编辑什么：工程、区域、地块种类、子房间种类、内容条目、生成方案、
 * 已生成地图，以及地图内的单个地块。所有写回都通过 store 的 commit 通道，
 * 因此会自动进入撤销栈、重新校验并持久化。
 */

import {
  ANCHOR_MODES,
  CONTENT_TYPES,
  CONTENT_TYPE_MAP,
  RARITIES,
  REGION_PALETTE,
  SUBROOM_PALETTE,
} from '../core/constants.js';
import { uniqueId } from '../core/project.js';
import { projectFingerprint } from '../core/project.js';
import { registry } from '../core/registry.js';
import { plotDegree } from '../core/validate-map.js';
import { h, mount, clear, icon, fmt, pct } from './dom.js';
import {
  checkboxInput,
  contentSummary,
  field,
  numberInput,
  openContentPicker,
  projectField,
  selectInput,
  stackedField,
  tagInput,
  textInput,
} from './fields.js';
import { confirmDialog, openFormModal, openTextViewer, toast } from './modals.js';

const labelOf = (list, id, key = 'name') => list.find((item) => item.id === id)?.[key] || id || '—';

function groupOptions(project, regionId, excludeIds = new Set()) {
  const groups = (project.plotGroups || []).filter((group) => group.regionId === regionId);
  const childrenOf = (parentId) =>
    groups
      .filter(
        (group) =>
          (group.parentGroupId || '') === (parentId || '') &&
          !excludeIds.has(group.id),
      )
      .sort((a, b) => (a.order || 0) - (b.order || 0) || a.id.localeCompare(b.id));
  const out = [];
  const seen = new Set();
  const walk = (parentId, depth) => {
    for (const group of childrenOf(parentId)) {
      if (seen.has(group.id)) continue;
      seen.add(group.id);
      out.push({
        value: group.id,
        label: `${'　'.repeat(depth)}${depth ? '└ ' : ''}${group.name}`,
      });
      walk(group.id, depth + 1);
    }
  };
  walk('', 0);
  return out;
}

function section(title, ...children) {
  return h(
    'div',
    { class: 'section' },
    h('div', { class: 'section-title' }, h('span', null, title)),
    ...children,
  );
}

function kv(label, value) {
  return h(
    'div',
    { class: 'kv' },
    h('span', { class: 'k' }, label),
    h('span', { class: 'v' }, value == null || value === '' ? '—' : String(value)),
  );
}

function readonly(label, value, hint = '') {
  return field(
    label,
    h(
      'span',
      { style: { fontFamily: 'var(--mono)', fontSize: '10.5px', color: 'var(--muted)', wordBreak: 'break-all' } },
      value || '—',
    ),
    hint,
  );
}

function hero({ eyebrow, color, title, sub, desc }) {
  return h(
    'div',
    { class: 'inspector-hero' },
    h(
      'div',
      { class: 'eyebrow' },
      color ? h('span', { class: 'swatch', style: { background: color } }) : null,
      eyebrow,
    ),
    h('h2', { class: 'hero-title' }, title || '未命名'),
    sub ? h('div', { class: 'hero-sub' }, sub) : null,
    desc ? h('p', { class: 'hero-desc' }, desc) : null,
  );
}

function statGrid(items) {
  return h(
    'div',
    { class: 'stat-grid' },
    ...items.map(([value, label]) =>
      h(
        'div',
        { class: 'stat' },
        h('div', { class: 'stat-value' }, value),
        h('div', { class: 'stat-label' }, label),
      ),
    ),
  );
}

function issueList(issues, limit = 14) {
  if (!issues || issues.length === 0) {
    return h('div', { class: 'field-hint' }, '没有发现问题。');
  }
  return h(
    'div',
    { class: 'issue-list' },
    ...issues.slice(0, limit).map((issue) =>
      h(
        'div',
        { class: `issue ${issue.level || 'warning'}` },
        h('div', null, issue.message),
        h('code', null, issue.code || ''),
      ),
    ),
    issues.length > limit
      ? h('div', { class: 'field-hint' }, `还有 ${issues.length - limit} 条未显示。`)
      : null,
  );
}

function actionRow(...buttons) {
  return h('div', { class: 'action-row' }, ...buttons);
}

function button(label, onClick, { primary = false, danger = false, title = '' } = {}) {
  return h(
    'button',
    {
      class: `btn small${primary ? ' primary' : ''}${danger ? ' danger' : ''}`,
      type: 'button',
      title,
      onclick: onClick,
    },
    label,
  );
}

function selectOptions(items, labelKey = 'name') {
  return items.map((item) => ({ value: item.id, label: item[labelKey] || item.id }));
}

function checkList(items, selected, onChange, { color = '', empty = '没有可选项。' } = {}) {
  if (items.length === 0) return h('div', { class: 'field-hint' }, empty);
  return h(
    'div',
    { class: 'check-list', style: { display: 'flex', flexDirection: 'column', gap: '3px' } },
    ...items.map((item) => {
      const input = h('input', {
        type: 'checkbox',
        checked: selected.includes(item.id),
      });
      input.addEventListener('change', () => {
        const next = input.checked
          ? [...new Set([...selected, item.id])]
          : selected.filter((id) => id !== item.id);
        onChange(next);
      });
      return h(
        'label',
        { class: 'inline-check' },
        input,
        color && item.color ? h('span', { class: 'dot', style: { color: item.color } }, '●') : null,
        h('span', null, item.name || item.id),
        h(
          'span',
          { class: 'row-tail', style: { marginLeft: 'auto' } },
          item.id,
        ),
      );
    }),
  );
}

export class Inspector {
  constructor({ root, store }) {
    this.root = root;
    this.store = store;
    this.build();
    store.on('change', () => this.render());
    store.on('selection', () => this.render());
    this.render();
  }

  build() {
    this.headTitle = h('span', { class: 'panel-title' }, '属性');
    this.headSub = h('span', { class: 'panel-sub' });
    this.body = h('div', { class: 'inspector-scroll' });
    mount(
      this.root,
      h(
        'div',
        { class: 'panel-head' },
        h(
          'div',
          { class: 'panel-title-row' },
          this.headTitle,
          this.headSub,
          h('span', { class: 'spacer' }),
          h(
            'button',
            {
              class: 'btn icon ghost small',
              title: '回到工程属性',
              'aria-label': '回到工程属性',
              onclick: () => this.store.select({ type: 'project' }),
            },
            icon('close', 12),
          ),
        ),
      ),
      this.body,
    );
  }

  render() {
    const scroll = this.body.scrollTop;
    const selection = this.store.selection;
    const selected = this.store.findSelected();
    clear(this.body);

    if (selection.type === 'map' && this.store.currentMap) {
      this.headTitle.textContent = '地图摘要';
      this.headSub.textContent = this.store.currentMap.seed;
      this.renderMap(this.store.currentMap);
    } else if (selected && selection.type === 'region') {
      this.headTitle.textContent = '区域';
      this.headSub.textContent = selected.id;
      this.renderRegion(selected);
    } else if (selected && selection.type === 'plotKind') {
      this.headTitle.textContent = '地块种类';
      this.headSub.textContent = selected.id;
      this.renderPlotKind(selected);
    } else if (selected && selection.type === 'plotGroup') {
      this.headTitle.textContent = '折叠组';
      this.headSub.textContent = selected.id;
      this.renderPlotGroup(selected);
    } else if (selected && selection.type === 'subRoomKind') {
      this.headTitle.textContent = '子房间';
      this.headSub.textContent = selected.id;
      this.renderSubRoomKind(selected);
    } else if (selected && selection.type === 'catalog') {
      this.headTitle.textContent = '内容条目';
      this.headSub.textContent = selected.id;
      this.renderCatalog(selected, selection.contentType);
    } else if (selected && selection.type === 'profile') {
      this.headTitle.textContent = '生成方案';
      this.headSub.textContent = selected.id;
      this.renderProfile(selected);
    } else if (selected && selection.type === 'plot') {
      this.headTitle.textContent = '地图地块';
      this.headSub.textContent = selected.id;
      this.renderPlot(selected);
    } else {
      this.headTitle.textContent = '工程属性';
      this.headSub.textContent = projectFingerprint(this.store.project);
      this.renderProject();
    }
    this.body.scrollTop = scroll;
  }

  /* ------------------------------------------------------------------ */

  renderProject() {
    const { project, maps, currentMap } = this.store;
    const validation = this.store.validate();
    const contentTotal = CONTENT_TYPES.reduce(
      (total, type) => total + (project.catalogs[type.id]?.length || 0),
      0,
    );
    const regionCounts = currentMap?.stats?.regions || [];

    mount(
      this.body,
      hero({
        eyebrow: 'PLOTWEAVE / PROJECT',
        color: '#d9a441',
        title: project.meta.name,
        sub: `${project.app} · schema v${project.schemaVersion} · ${projectFingerprint(project)}`,
        desc: project.meta.description,
      }),
      section(
        '工程',
        projectField(this.store, project.meta, 'name', { label: '名称' }),
        projectField(this.store, project.meta, 'author', { label: '作者' }),
        projectField(this.store, project.meta, 'description', {
          label: '描述',
          type: 'textarea',
          rows: 3,
        }),
      ),
      section(
        '网格',
        projectField(this.store, project.settings.grid, 'width', {
          label: '宽度',
          type: 'number',
          min: 8,
          max: 160,
          unit: '格',
        }),
        projectField(this.store, project.settings.grid, 'height', {
          label: '高度',
          type: 'number',
          min: 8,
          max: 160,
          unit: '格',
        }),
        h(
          'div',
          { class: 'field-hint' },
          '生成时地块不能越界；较大的网格给长条廊道更多回旋空间。',
        ),
      ),
      section(
        '规模',
        statGrid([
          [fmt(project.regions.length), '区域'],
          [fmt(project.plotKinds.length), '地块种类'],
          [fmt(project.subRoomKinds.length), '子房间种类'],
          [fmt(contentTotal), '内容条目'],
          [fmt(maps.length), '本地地图'],
          [fmt(project.profiles.length), '生成方案'],
          [fmt((project.plotGroups || []).length), '折叠组'],
        ]),
      ),
      section(
        '校验',
        h(
          'div',
          { class: 'action-row', style: { marginBottom: '8px' } },
          h(
            'span',
            {
              class: `chip${validation.ok ? ' amber' : ' danger'}`,
            },
            validation.ok ? '工程结构有效' : `${validation.errors?.length || 0} 个错误`,
          ),
          h('span', { class: 'field-hint' }, `${validation.warnings?.length || 0} 条警告`),
        ),
        validation.ok && validation.warnings?.length === 0
          ? h('div', { class: 'field-hint' }, '所有区域、地块与方案引用一致。')
          : actionRow(
              button('查看校验详情', () => {
                openTextViewer({
                  title: '工程校验',
                  subtitle: projectFingerprint(project),
                  text: JSON.stringify(validation, null, 2),
                  filename: 'plotweave-project-validation.json',
                });
              }),
            ),
      ),
      currentMap
        ? section(
            '当前地图',
            kv('种子', currentMap.seed),
            kv('方案', labelOf(project.profiles, currentMap.profileId)),
            kv('地块', currentMap.plots.length),
            kv('区域', regionCounts.length),
            button('查看地图摘要', () =>
              this.store.select({ type: 'map', id: currentMap.id }),
            ),
          )
        : section('当前地图', h('div', { class: 'field-hint' }, '还没有生成地图。')),
    );
  }

  renderRegion(region) {
    const kinds = this.store.project.plotKinds.filter((kind) => kind.regionId === region.id);
    const map = this.store.currentMap;
    const inMap = map?.plots.filter((plot) => plot.regionId === region.id).length || 0;
    mount(
      this.body,
      hero({
        eyebrow: 'REGION / 区域',
        color: region.color,
        title: region.name,
        sub: `${region.id} · ${kinds.length} 种地块 · 当前地图 ${inMap} 块`,
        desc: region.description,
      }),
      section(
        '标识',
        readonly('ID', region.id, 'ID 用于数据引用，修改会断开已有引用。'),
        projectField(this.store, region, 'name', { label: '名称' }),
        projectField(this.store, region, 'shortName', { label: '短名' }),
        projectField(this.store, region, 'color', { label: '颜色', type: 'color' }),
      ),
      section(
        '内容',
        projectField(this.store, region, 'description', {
          label: '描述',
          type: 'textarea',
          rows: 3,
        }),
        projectField(this.store, region, 'tags', { label: '标签', type: 'tags' }),
        projectField(this.store, region, 'hidden', {
          label: '隐藏',
          checkLabel: '不在图例和列表中突出显示',
        }),
      ),
      section(
        '生成配额',
        h(
          'div',
          { class: 'split-2' },
          projectField(this.store, region.generation, 'min', {
            label: '最少',
            type: 'number',
            min: 0,
            max: 80,
          }),
          projectField(this.store, region.generation, 'max', {
            label: '最多',
            type: 'number',
            min: 0,
            max: 80,
          }),
        ),
        projectField(this.store, region.generation, 'weight', {
          label: '权重',
          type: 'number',
          min: 0,
          max: 10,
          step: 0.05,
        }),
        projectField(this.store, region.generation, 'order', {
          label: '顺序',
          type: 'number',
          min: 0,
          max: 99,
        }),
        h(
          'div',
          { class: 'field-hint' },
          '生成器先按权重分配目标数量，再在区域内选择地块种类；min/max 会参与总额约束。',
        ),
      ),
      section(
        '引用',
        kv('地块种类', kinds.length),
        kv(
          '生成方案',
          this.store.project.profiles.filter(
            (profile) => profile.startRegionId === region.id || profile.regionIds.includes(region.id),
          ).length,
        ),
        actionRow(
          button('在此区域新建地块', () => {
            const kind = this.store.addPlotKind(region.id);
            if (kind) toast(`已在「${region.name}」下新建「${kind.name}」`, { type: 'success' });
          }, { primary: true }),
          button('删除区域', async () => {
            const ok = await confirmDialog({
              title: '删除区域',
              message: `确定删除「${region.name}」？区域下仍有地块或被方案引用时会被阻止。`,
              confirmText: '删除',
              danger: true,
            });
            if (!ok) return;
            const result = this.store.deleteRegion(region.id);
            toast(result.ok ? '区域已删除' : result.message, {
              type: result.ok ? 'success' : 'error',
            });
          }, { danger: true }),
        ),
      ),
    );
  }

  renderPlotGroup(group) {
    const { project } = this.store;
    const region = this.store.findRegion(group.regionId);
    const kinds = project.plotKinds.filter((kind) => kind.groupId === group.id);
    const children = (project.plotGroups || [])
      .filter((candidate) => candidate.parentGroupId === group.id)
      .sort((a, b) => (a.order || 0) - (b.order || 0) || a.id.localeCompare(b.id));
    const parent = group.parentGroupId ? this.store.findPlotGroup(group.parentGroupId) : null;
    const color = region?.color || '#d9a441';
    const excluded = new Set([group.id, ...this.store.descendantGroupIds(group.id)]);
    const parentOptions = [
      { value: '', label: '区域根级' },
      ...groupOptions(project, group.regionId, excluded),
    ];
    const createChild = () => {
      const input = h('input', {
        class: 'input',
        name: 'name',
        value: '新折叠组',
        maxlength: '40',
        placeholder: '例如：核心区 / 隐藏支线',
      });
      openFormModal({
        title: '新建子折叠组',
        subtitle: `在「${group.name}」中再建一个可折叠的组。`,
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
          const child = this.store.addPlotGroup(group.regionId, data.name, '', group.id);
          if (child) toast(`已创建子折叠组「${child.name}」`, { type: 'success' });
        },
      });
    };

    mount(
      this.body,
      hero({
        eyebrow: 'PLOT GROUP / 折叠组',
        color,
        title: group.name,
        sub: `${group.id} · ${region?.name || '未归属区域'} · ${kinds.length} 种地块 · ${children.length} 个子组`,
        desc: group.description,
      }),
      section(
        '标识',
        readonly('ID', group.id),
        projectField(this.store, group, 'name', { label: '名称' }),
        readonly('区域', region?.name || group.regionId),
        field(
          '上级折叠组',
          selectInput(
            parentOptions,
            parent?.id || '',
            (value) => {
              const result = this.store.movePlotGroup(group.id, value);
              toast(result.ok ? '折叠组已移动' : result.message, {
                type: result.ok ? 'success' : 'error',
              });
            },
          ),
          '子折叠组会跟随当前组；不能移动到自己的子组下。',
        ),
        projectField(this.store, group, 'description', {
          label: '描述',
          type: 'textarea',
          rows: 3,
        }),
      ),
      section(
        '子折叠组',
        ...(children.length
          ? children.map((child) =>
              h(
                'div',
                {
                  class: 'row',
                  style: { cursor: 'pointer' },
                  onclick: () => this.store.select({ type: 'plotGroup', id: child.id }),
                },
                h('span', { class: 'mini-icon' }, icon('folder', 12)),
                h(
                  'div',
                  { class: 'row-main' },
                  h('div', { class: 'row-name' }, child.name),
                  h(
                    'div',
                    { class: 'row-sub' },
                    `${project.plotKinds.filter((kind) => kind.groupId === child.id).length} 种地块 · ${child.id}`,
                  ),
                ),
              ),
            )
          : [h('div', { class: 'field-hint' }, '还没有子折叠组。')]),
        actionRow(button('新建子折叠组', createChild)),
      ),
      section(
        '组内地块',
        ...(kinds.length
          ? kinds.map((kind) =>
              h(
                'div',
                {
                  class: 'row',
                  style: { cursor: 'pointer' },
                  onclick: () => this.store.select({ type: 'plotKind', id: kind.id }),
                },
                h('span', {
                  class: 'row-swatch',
                  style: { background: kind.visual?.color || color },
                }),
                h(
                  'div',
                  { class: 'row-main' },
                  h('div', { class: 'row-name' }, kind.name),
                  h('div', { class: 'row-sub' }, `${kind.length.min}–${kind.length.max} 格 · ${kind.id}`),
                ),
              ),
            )
          : [h('div', { class: 'field-hint' }, '组内还没有直接归属的地块；子组内的地块不计入这里。')]),
        actionRow(
          button('在组内新建地块', () => {
            const kind = this.store.addPlotKind(group.regionId, group.id);
            if (kind) toast(`已在「${group.name}」中新建「${kind.name}」`, { type: 'success' });
          }, { primary: true }),
          button('删除折叠组', async () => {
            const destination = parent ? `上级组「${parent.name}」` : '「未归组」';
            const ok = await confirmDialog({
              title: '删除折叠组',
              message: `确定删除「${group.name}」？组内 ${kinds.length} 种地块会移到${destination}，${children.length} 个子折叠组会上移一级，都不会被删除。`,
              confirmText: '删除',
              danger: true,
            });
            if (!ok) return;
            const result = this.store.deletePlotGroup(group.id);
            toast(result.ok ? '折叠组已删除，地块和子组已上移一级' : result.message, {
              type: result.ok ? 'success' : 'error',
            });
          }, { danger: true }),
        ),
      ),
    );
  }

  renderPlotKind(kind) {
    const { project } = this.store;
    const region = this.store.findRegion(kind.regionId);
    const otherKinds = project.plotKinds.filter((candidate) => candidate.id !== kind.id);
    const allowed = kind.connections.allowedKinds || [];
    const pool = kind.subRooms.pool || [];
    const usedInMaps = this.store.maps.reduce(
      (total, map) => total + map.plots.filter((plot) => plot.kindId === kind.id).length,
      0,
    );

    mount(
      this.body,
      hero({
        eyebrow: 'PLOT KIND / 地块种类',
        color: kind.visual?.color || region?.color,
        title: kind.name,
        sub: `${kind.id} · ${region?.name || '未归属区域'} · ${kind.length.min}–${kind.length.max} 格 · 上限 ${kind.connections.max}`,
        desc: kind.description,
      }),
      section(
        '标识',
        readonly('ID', kind.id),
        projectField(this.store, kind, 'name', { label: '名称' }),
        projectField(this.store, kind, 'shortName', { label: '短名' }),
        field(
          '区域',
          selectInput(selectOptions(project.regions), kind.regionId, (value) => {
            this.store.commitProject((projectRoot) => {
              const target = projectRoot.plotKinds.find((candidate) => candidate.id === kind.id);
              if (!target) return;
              target.regionId = value;
              const group = (projectRoot.plotGroups || []).find(
                (candidate) => candidate.id === target.groupId,
              );
              if (group && group.regionId !== value) target.groupId = '';
            }, { label: '修改地块区域' });
          }),
          '切换区域时，若折叠组不属于新区域会自动移出。',
        ),
        field(
          '折叠组',
          selectInput(
            [
              { value: '', label: '未归组' },
              ...groupOptions(project, kind.regionId),
            ],
            kind.groupId || '',
            (value) => {
              this.store.commitProject((projectRoot) => {
                const target = projectRoot.plotKinds.find((candidate) => candidate.id === kind.id);
                if (target) target.groupId = value;
              }, { label: '移动到折叠组' });
            },
          ),
        ),
        projectField(this.store, kind, 'visual.color', {
          label: '颜色',
          type: 'color',
          hint: '留空时继承区域色。',
        }),
        projectField(this.store, kind, 'visual.icon', { label: '图标' }),
        projectField(this.store, kind, 'spawnEligible', {
          label: '出生点',
          checkLabel: '允许玩家出生在这个地块上',
        }),
      ),
      section(
        '布局',
        projectField(this.store, kind, 'description', {
          label: '描述',
          type: 'textarea',
          rows: 3,
        }),
        projectField(this.store, kind, 'tags', { label: '标签', type: 'tags' }),
        h(
          'div',
          { class: 'split-2' },
          projectField(this.store, kind.length, 'min', {
            label: '最短',
            type: 'number',
            min: 1,
            max: 5,
            unit: '格',
          }),
          projectField(this.store, kind.length, 'max', {
            label: '最长',
            type: 'number',
            min: 1,
            max: 5,
            unit: '格',
          }),
        ),
      ),
      section(
        '连接',
        projectField(this.store, kind.connections, 'max', {
          label: '连接上限',
          type: 'number',
          min: 0,
          max: 8,
          unit: '条',
        }),
        projectField(this.store, kind.connections, 'anchors', {
          label: '接口位置',
          type: 'select',
          options: ANCHOR_MODES.map((mode) => ({
            value: mode.id,
            label: `${mode.label} — ${mode.description}`,
          })),
        }),
        projectField(this.store, kind.connections, 'allowCrossing', {
          label: '允许交叉',
          checkLabel: '允许另一条地块垂直穿过本地块的格子',
        }),
        stackedField(
          '可连接种类',
          checkList(
            otherKinds,
            allowed,
            (next) => {
              this.store.commitProject(
                (draft) => {
                  const target = draft.plotKinds.find((candidate) => candidate.id === kind.id);
                  if (target) target.connections.allowedKinds = next;
                },
                { label: '修改可连接种类' },
              );
            },
            { empty: '没有其他地块种类。' },
          ),
          '留空表示不限制；勾选后只允许与这些种类相连（双向检查）。',
        ),
        stackedField(
          '可连接标签',
          tagInput(kind.connections.allowedTags, (next) => {
            this.store.commitProject(
              (draft) => {
                const target = draft.plotKinds.find((candidate) => candidate.id === kind.id);
                if (target) target.connections.allowedTags = next;
              },
              { label: '修改可连接标签' },
            );
          }, { placeholder: '例如 public, hub' }),
          '双方标签至少有一个交集才允许连接。',
        ),
      ),
      section(
        '子房间',
        projectField(this.store, kind.subRooms, 'enabled', {
          label: '启用',
          checkLabel: '生成时在本地块上安排子房间',
        }),
        h(
          'div',
          { class: 'split-2' },
          projectField(this.store, kind.subRooms, 'min', {
            label: '最少',
            type: 'number',
            min: 0,
            max: 5,
          }),
          projectField(this.store, kind.subRooms, 'max', {
            label: '最多',
            type: 'number',
            min: 0,
            max: 5,
          }),
        ),
        stackedField(
          '子房间池',
          checkList(
            project.subRoomKinds,
            pool,
            (next) => {
              this.store.commitProject(
                (draft) => {
                  const target = draft.plotKinds.find((candidate) => candidate.id === kind.id);
                  if (target) target.subRooms.pool = next;
                },
                { label: '修改子房间池' },
              );
            },
            { color: true, empty: '还没有定义子房间种类。' },
          ),
          '生成器从池中按权重抽取；实际数量还会受方案子房间密度影响。',
        ),
      ),
      section(
        '挂载内容',
        contentSummary(project, kind.contents),
        actionRow(
          button('编辑内容', () => {
            openContentPicker({
              project,
              title: `编辑「${kind.name}」的内容`,
              subtitle: '这些条目会复制到每张新生成地图的地块实例上。',
              contents: kind.contents,
              onSave: (next) => {
                this.store.commitProject(
                  (draft) => {
                    const target = draft.plotKinds.find((candidate) => candidate.id === kind.id);
                    if (target) target.contents = next;
                  },
                  { label: '修改地块内容' },
                );
              },
            });
          }, { primary: true }),
          button('复制地块', () => {
            let copy = null;
            this.store.commitProject(
              (draft) => {
                const source = draft.plotKinds.find((candidate) => candidate.id === kind.id);
                if (!source) return false;
                copy = JSON.parse(JSON.stringify(source));
                copy.id = uniqueId(
                  `${kind.regionId}_kind`,
                  draft.plotKinds.map((candidate) => candidate.id),
                );
                copy.name = `${source.name} 副本`;
                copy.shortName = `${source.shortName || source.name}副本`;
                copy.spawnEligible = false;
                copy.generation.max = Math.max(1, Math.min(3, copy.generation.max || 2));
                copy.contents = JSON.parse(JSON.stringify(source.contents || {}));
                draft.plotKinds.push(copy);
              },
              { label: '复制地块种类' },
            );
            if (copy) {
              this.store.select({ type: 'plotKind', id: copy.id });
              toast(`已复制为「${copy.name}」`, { type: 'success' });
            }
          }),
          button('删除', async () => {
            const ok = await confirmDialog({
              title: '删除地块种类',
              message: `确定删除「${kind.name}」？已有地图中的该地块会被重新校验并可能报错。`,
              confirmText: '删除',
              danger: true,
            });
            if (!ok) return;
            const result = this.store.deletePlotKind(kind.id);
            toast(result.ok ? '地块种类已删除' : result.message, {
              type: result.ok ? 'success' : 'error',
            });
          }, { danger: true }),
        ),
      ),
      section(
        '生成',
        projectField(this.store, kind.generation, 'weight', {
          label: '权重',
          type: 'number',
          min: 0,
          max: 10,
          step: 0.05,
        }),
        projectField(this.store, kind.generation, 'max', {
          label: '数量上限',
          type: 'number',
          min: 0,
          max: 80,
          unit: '块',
        }),
        projectField(this.store, kind.generation, 'unique', {
          label: '唯一',
          checkLabel: '同一张地图最多出现一次',
        }),
        kv('已用于地图', `${usedInMaps} 块`),
      ),
    );
  }

  renderSubRoomKind(room) {
    const usage = this.store.project.plotKinds.filter((kind) =>
      kind.subRooms.pool.includes(room.id),
    ).length;
    const totalInMaps = this.store.maps.reduce(
      (total, map) =>
        total +
        map.plots.reduce(
          (count, plot) => count + (plot.subRooms || []).filter((item) => item.kindId === room.id).length,
          0,
        ),
      0,
    );
    mount(
      this.body,
      hero({
        eyebrow: 'SUBROOM / 子房间',
        color: room.color,
        title: room.name,
        sub: `${room.id} · ${usage} 个地块种类引用 · 地图中 ${totalInMaps} 间`,
        desc: room.description,
      }),
      section(
        '标识',
        readonly('ID', room.id),
        projectField(this.store, room, 'name', { label: '名称' }),
        projectField(this.store, room, 'color', { label: '颜色', type: 'color' }),
        projectField(this.store, room, 'description', {
          label: '描述',
          type: 'textarea',
          rows: 3,
        }),
        projectField(this.store, room, 'tags', { label: '标签', type: 'tags' }),
      ),
      section(
        '挂载内容',
        contentSummary(this.store.project, room.contents),
        actionRow(
          button('编辑内容', () => {
            openContentPicker({
              project: this.store.project,
              title: `编辑「${room.name}」的内容`,
              contents: room.contents,
              onSave: (next) => {
                this.store.commitProject(
                  (draft) => {
                    const target = draft.subRoomKinds.find((candidate) => candidate.id === room.id);
                    if (target) target.contents = next;
                  },
                  { label: '修改子房间内容' },
                );
              },
            });
          }, { primary: true }),
        ),
        h('div', { class: 'field-hint' }, '子房间内容在生成时复制到具体的房间实例上。'),
      ),
      section(
        '生成',
        projectField(this.store, room.generation, 'weight', {
          label: '抽取权重',
          type: 'number',
          min: 0,
          max: 10,
          step: 0.05,
        }),
        actionRow(
          button('删除子房间', async () => {
            const ok = await confirmDialog({
              title: '删除子房间种类',
              message: `确定删除「${room.name}」？所有地块池中的引用会被同时移除。`,
              confirmText: '删除',
              danger: true,
            });
            if (!ok) return;
            this.store.deleteSubRoomKind(room.id);
            toast('子房间种类已删除', { type: 'success' });
          }, { danger: true }),
        ),
      ),
    );
  }

  renderCatalog(item, contentType) {
    const type = CONTENT_TYPE_MAP[contentType] || CONTENT_TYPES[0];
    const usage = this.store.contentUsage(contentType, item.id);
    mount(
      this.body,
      hero({
        eyebrow: `${type.label.toUpperCase()} / 内容条目`,
        color: type.color,
        title: item.name,
        sub: `${item.id} · ${type.label}`,
        desc: item.description,
      }),
      section(
        '标识',
        readonly('ID', item.id),
        projectField(this.store, item, 'name', { label: '名称' }),
        projectField(this.store, item, 'rarity', {
          label: '稀有度',
          type: 'select',
          options: RARITIES.map((rarity) => ({ value: rarity.id, label: rarity.label })),
        }),
        projectField(this.store, item, 'icon', { label: '图标', placeholder: '◆' }),
      ),
      section(
        '说明',
        projectField(this.store, item, 'description', {
          label: '描述',
          type: 'textarea',
          rows: 4,
        }),
        projectField(this.store, item, 'tags', { label: '标签', type: 'tags' }),
      ),
      section(
        '使用情况',
        kv('地块种类引用', usage.kinds),
        kv('子房间引用', usage.rooms),
        h(
          'div',
          { class: 'field-hint' },
          usage.total === 0
            ? '尚未挂到任何地块或子房间上。'
            : '引用只保存条目 ID；条目内容变更会自动反映到设计视图中。',
        ),
        actionRow(
          button('删除条目', async () => {
            const ok = await confirmDialog({
              title: '删除内容条目',
              message: `确定删除「${item.name}」？所有地块、子房间和已生成地图中的引用会被清除。`,
              confirmText: '删除',
              danger: true,
            });
            if (!ok) return;
            this.store.deleteCatalogItem(contentType, item.id);
            toast('内容条目已删除', { type: 'success' });
          }, { danger: true }),
        ),
      ),
    );
  }

  renderProfile(profile) {
    const { project } = this.store;
    const generators = registry.listGenerators();
    const regionKinds = project.plotKinds.filter(
      (kind) => !profile.startRegionId || kind.regionId === profile.startRegionId,
    );
    const activeRegions = profile.regionIds.length ? profile.regionIds : project.regions.map((r) => r.id);
    const capacity = activeRegions.reduce((sum, regionId) => {
      const region = this.store.findRegion(regionId);
      return sum + (region?.generation.max || 0);
    }, 0);
    mount(
      this.body,
      hero({
        eyebrow: 'PROFILE / 生成方案',
        color: '#d9a441',
        title: profile.name,
        sub: `${profile.id} · 目标 ${profile.plotTarget} 块 · ${profile.generator}`,
        desc: profile.description,
      }),
      section(
        '标识',
        readonly('ID', profile.id),
        projectField(this.store, profile, 'name', { label: '名称' }),
        projectField(this.store, profile, 'description', {
          label: '描述',
          type: 'textarea',
          rows: 3,
        }),
      ),
      section(
        '规模',
        projectField(this.store, profile, 'plotTarget', {
          label: '目标地块',
          type: 'number',
          min: 1,
          max: 200,
          unit: '块',
        }),
        kv('区域容量上限', capacity),
        h(
          'div',
          { class: 'field-hint' },
          capacity < profile.plotTarget
            ? `目标超过所选区域容量（${capacity}），生成器会尽量接近。`
            : '生成器按区域配额分配目标数量。',
        ),
        projectField(this.store, profile, 'generator', {
          label: '生成器',
          type: 'select',
          options: generators.map((generator) => ({
            value: generator.id,
            label: `${generator.label || generator.id}${generator.version ? ` v${generator.version}` : ''}`,
          })),
        }),
      ),
      section(
        '区域',
        checkList(
          project.regions,
          profile.regionIds,
          (next) => {
            this.store.commitProject(
              (draft) => {
                const target = draft.profiles.find((candidate) => candidate.id === profile.id);
                if (target) target.regionIds = next;
              },
              { label: '修改方案区域' },
            );
          },
          { color: true },
        ),
        h('div', { class: 'field-hint' }, '全部不勾选表示使用工程内所有区域。'),
        projectField(this.store, profile, 'startRegionId', {
          label: '起始区域',
          type: 'select',
          options: selectOptions(project.regions),
        }),
        projectField(this.store, profile, 'startKindId', {
          label: '起始地块',
          type: 'select',
          options: selectOptions(regionKinds, 'name'),
        }),
      ),
      section(
        '跨区域',
        projectField(this.store, profile, 'allowCrossRegion', {
          label: '允许跨区',
          checkLabel: '允许少数地块连接不同区域',
        }),
        projectField(this.store, profile, 'crossRegionRatio', {
          label: '跨区比例',
          type: 'number',
          min: 0,
          max: 1,
          step: 0.01,
          hint: '相对于目标地块总数的跨区连接上限。',
        }),
        projectField(this.store, profile, 'maxCrossRegionPerPair', {
          label: '每对上限',
          type: 'number',
          min: 0,
          max: 20,
          unit: '条',
        }),
      ),
      section(
        '形态',
        projectField(this.store, profile, 'allowCrossing', {
          label: '允许交叉',
          checkLabel: '允许地块垂直交叉共享格子',
        }),
        projectField(this.store, profile, 'subRoomDensity', {
          label: '子房间密度',
          type: 'number',
          min: 0,
          max: 3,
          step: 0.05,
        }),
        h(
          'div',
          { class: 'field-hint' },
          '密度为 0 时不生成子房间；大于 1 会增加每个地块的房间数量（受地块长度限制）。',
        ),
        actionRow(
          button('用此方案生成', () => this.store.generate({ profileId: profile.id }), {
            primary: true,
          }),
          button('删除方案', async () => {
            const ok = await confirmDialog({
              title: '删除生成方案',
              message: `确定删除「${profile.name}」？至少需要保留一个方案。`,
              confirmText: '删除',
              danger: true,
            });
            if (!ok) return;
            const result = this.store.deleteProfile(profile.id);
            toast(result.ok ? '生成方案已删除' : result.message, {
              type: result.ok ? 'success' : 'error',
            });
          }, { danger: true }),
        ),
      ),
    );
  }

  renderMap(map) {
    const { project } = this.store;
    const stats = map.stats || {};
    const profile = this.store.findProfile(map.profileId);
    const regions = stats.regions || [];
    const issues = map.issues || [];
    const spawnPlot = map.spawn ? map.plots.find((plot) => plot.id === map.spawn.plotId) : null;
    const spawnKind = spawnPlot ? this.store.findPlotKind(spawnPlot.kindId) : null;

    mount(
      this.body,
      hero({
        eyebrow: 'MAP / 已生成地图',
        color: '#6aa9c9',
        title: map.seed,
        sub: `${map.id} · ${profile?.name || map.profileId} · ${map.plots.length} 块地块`,
        desc: `网格 ${map.settings?.grid?.width || '—'} × ${map.settings?.grid?.height || '—'}，${stats.links || map.links.length} 条连接。`,
      }),
      section(
        '统计',
        statGrid([
          [fmt(map.plots.length), '地块'],
          [fmt(stats.links ?? map.links.length), '连接'],
          [fmt(stats.doors ?? 0), '门'],
          [fmt(stats.crossings ?? 0), '交叉'],
          [fmt(stats.subRooms ?? 0), '子房间'],
          [stats.fillRatio != null ? pct(stats.fillRatio) : '—', '网格占用'],
        ]),
        kv('平均连接度', stats.degrees ? fmt(stats.degrees.average, 2) : '—'),
        kv('最大连接度', stats.degrees ? fmt(stats.degrees.max) : '—'),
        kv('孤立地块', stats.degrees ? fmt(stats.degrees.isolated) : '—'),
        kv('区域数', regions.length),
        kv('跨区连接', fmt(stats.crossRegionLinks ?? 0)),
      ),
      section(
        '区域构成',
        ...regions.map((regionStat) => {
          const region = this.store.findRegion(regionStat.regionId);
          return h(
            'div',
            {
              class: 'kv',
              style: { cursor: 'pointer' },
              onclick: () => this.store.select({ type: 'region', id: regionStat.regionId }),
            },
            h(
              'span',
              { class: 'k' },
              h('span', { style: { color: region?.color || '#fff' } }, '●'),
              ` ${region?.name || regionStat.regionId}`,
            ),
            h(
              'span',
              { class: 'v' },
              `${regionStat.plots} 块 · ${regionStat.subRooms} 间`,
            ),
          );
        }),
      ),
      section(
        '出生点',
        spawnPlot
          ? h(
              'div',
              null,
              kv('地块', `${spawnKind?.name || spawnPlot.kindId} · ${spawnPlot.id}`),
              kv('格子', `(${map.spawn.x}, ${map.spawn.y}) · #${map.spawn.cellIndex + 1}`),
              actionRow(
                button('在画布中定位', () => this.store.emit('focus-plot', spawnPlot.id)),
                button('切换到出生点工具', () => this.store.setUi({ tool: 'spawn' })),
              ),
            )
          : h('div', { class: 'field-hint' }, '尚未指定出生点。'),
      ),
      section(
        '校验',
        issueList(issues, 12),
        h(
          'div',
          { class: 'field-hint', style: { marginTop: '8px' } },
          `渲染与统计基于当前工程规则；修改工程后地图会自动重新校验。`,
        ),
      ),
      section(
        '操作',
        actionRow(
          button('同种子再生成', () => {
            this.store.generate({ profileId: map.profileId, seed: map.seed });
            toast('已用相同种子重新生成', { type: 'success' });
          }),
          button('复制种子', async () => {
            try {
              await navigator.clipboard.writeText(map.seed);
              toast('种子已复制', { type: 'success' });
            } catch {
              toast('无法访问剪贴板', { type: 'error' });
            }
          }),
          button('导出报告', () => {
            openTextViewer({
              title: '地图报告',
              subtitle: map.seed,
              text: JSON.stringify(
                {
                  id: map.id,
                  seed: map.seed,
                  profileId: map.profileId,
                  stats: map.stats,
                  spawn: map.spawn,
                  issues: map.issues,
                },
                null,
                2,
              ),
              filename: `${map.id}-report.json`,
            });
          }),
        ),
      ),
    );
  }

  renderPlot(plot) {
    const map = this.store.currentMap;
    if (!map) return;
    const kind = this.store.findPlotKind(plot.kindId);
    const region = this.store.findRegion(plot.regionId);
    const degree = plotDegree(map, plot.id);
    const issues = (map.issues || []).filter(
      (issue) => issue.plotId === plot.id || issue.plotIds?.includes(plot.id),
    );
    const subRooms = (plot.subRooms || []).map((room) => ({
      ...room,
      kind: this.store.findSubRoomKind(room.kindId),
    }));

    mount(
      this.body,
      hero({
        eyebrow: 'MAP PLOT / 地图地块',
        color: kind?.visual?.color || region?.color,
        title: kind?.name || plot.kindId,
        sub: `${plot.id} · ${region?.name || plot.regionId} · ${plot.length} 格 · 连接度 ${degree}/${kind?.connections.max ?? '?'}`,
        desc: kind?.description,
      }),
      section(
        '几何',
        kv('种类', kind ? `${kind.name} (${kind.id})` : plot.kindId),
        kv('区域', region ? `${region.name} (${region.id})` : plot.regionId),
        kv('轴向', plot.axis === 'h' ? '横向' : '纵向'),
        kv('原点', `(${plot.origin.x}, ${plot.origin.y})`),
        kv('长度', `${plot.length} 格`),
        kv('格子', plot.cells.map((cell) => `(${cell.x},${cell.y})`).join(' ')),
        actionRow(
          button('查看地块种类', () => this.store.select({ type: 'plotKind', id: plot.kindId })),
          button('在画布中聚焦', () => this.store.emit('focus-plot', plot.id)),
        ),
      ),
      section(
        '出生点',
        map.spawn?.plotId === plot.id
          ? h('div', { class: 'field-hint' }, `当前出生点位于 (${map.spawn.x}, ${map.spawn.y})，格 #${map.spawn.cellIndex + 1}。`)
          : button(
              '把出生点设到这个地块',
              () => {
                const first = plot.cells.find((cell) => {
                  const occupied = plot.subRooms?.some(
                    (room) => room.x === cell.x && room.y === cell.y,
                  );
                  return !occupied;
                });
                const target = first || plot.cells[0];
                if (!target) return;
                const result = this.store.setSpawnAtCell(target.x, target.y);
                toast(result.ok ? '出生点已更新' : result.message, {
                  type: result.ok ? 'success' : 'error',
                });
              },
              { primary: true },
            ),
      ),
      section(
        '子房间',
        subRooms.length
          ? h(
              'div',
              { class: 'content-summary' },
              ...subRooms.map((room) =>
                h(
                  'div',
                  { class: 'content-line' },
                  h('span', { class: 'type', style: { color: room.kind?.color || '#fff' } }, '◆ 房间'),
                  h('span', { class: 'items' }, room.kind?.name || room.kindId),
                  h('span', { class: 'row-tail' }, `(${room.x},${room.y})`),
                ),
              ),
            )
          : h('div', { class: 'field-hint' }, '这个地块没有子房间。'),
        h('div', { class: 'field-hint' }, '子房间由生成器按地块种类的池和方案密度安排。'),
      ),
      section(
        '内容（地图实例）',
        contentSummary(this.store.project, plot.contents),
        actionRow(
          button('编辑实例内容', () => {
            openContentPicker({
              project: this.store.project,
              title: `编辑地图地块「${plot.id}」`,
              subtitle: '只影响当前这张地图；重新生成会回到地块种类的默认内容。',
              contents: plot.contents,
              onSave: (next) => {
                this.store.commitMap(
                  (draft) => {
                    const target = draft.plots.find((candidate) => candidate.id === plot.id);
                    if (target) target.contents = next;
                  },
                  { label: '修改地图地块内容' },
                );
              },
            });
          }, { primary: true }),
          button('从种类恢复', async () => {
            const ok = await confirmDialog({
              title: '恢复内容',
              message: '把当前地块的实例内容重置为该地块种类的默认内容？',
              confirmText: '恢复',
            });
            if (!ok) return;
            this.store.commitMap(
              (draft) => {
                const target = draft.plots.find((candidate) => candidate.id === plot.id);
                if (target) target.contents = JSON.parse(JSON.stringify(kind?.contents || {}));
              },
              { label: '恢复地块内容' },
            );
          }),
        ),
      ),
      section('校验', issueList(issues)),
    );
  }
}
