/**
 * 表单控件与内容选择器。
 *
 * 控件以「提交时写回」为主（change 事件），避免每次按键都写入撤销栈。
 * 所有字段都通过 core 的 getByPath / setByPath 操作，不绑定具体实体类型。
 */

import { CONTENT_TYPES, CONTENT_TYPE_IDS, emptyContents } from '../core/constants.js';
import { getByPath, setByPath } from '../core/project.js';
import { h, icon } from './dom.js';
import { openModal, toast } from './modals.js';

export function field(label, control, hint = '') {
  return h(
    'div',
    { class: 'field' },
    h('span', { class: 'field-label' }, label),
    control,
    hint ? h('span', { class: 'field-hint' }, hint) : null,
  );
}

export function stackedField(label, control, hint = '') {
  return h(
    'div',
    { class: 'field stack' },
    h('span', { class: 'field-label' }, label),
    control,
    hint ? h('span', { class: 'field-hint' }, hint) : null,
  );
}

function normalizeText(value) {
  return value == null ? '' : String(value);
}

export function textInput(value, onCommit, { placeholder = '', disabled = false } = {}) {
  const input = h('input', {
    class: 'input',
    type: 'text',
    value: normalizeText(value),
    placeholder,
    disabled,
    spellcheck: 'false',
  });
  input.addEventListener('change', () => onCommit(input.value));
  return input;
}

export function textArea(value, onCommit, { placeholder = '', rows = 3, disabled = false } = {}) {
  const area = h('textarea', {
    class: 'textarea',
    rows,
    placeholder,
    disabled,
    spellcheck: 'false',
  });
  area.value = normalizeText(value);
  area.addEventListener('change', () => onCommit(area.value));
  return area;
}

export function numberInput(value, onCommit, { min, max, step = 1, disabled = false } = {}) {
  const input = h('input', {
    class: 'input',
    type: 'number',
    value: value == null || Number.isNaN(Number(value)) ? '' : String(value),
    min,
    max,
    step,
    disabled,
  });
  input.addEventListener('change', () => {
    const parsed = Number(input.value);
    if (!Number.isFinite(parsed)) {
      input.value = value ?? '';
      return;
    }
    let next = parsed;
    if (Number.isFinite(min)) next = Math.max(min, next);
    if (Number.isFinite(max)) next = Math.min(max, next);
    onCommit(next);
  });
  return input;
}

export function selectInput(options, value, onCommit, { disabled = false } = {}) {
  const select = h('select', { class: 'select', disabled });
  for (const option of options) {
    select.append(
      h('option', { value: option.value, selected: String(option.value) === String(value) }, option.label),
    );
  }
  select.addEventListener('change', () => onCommit(select.value));
  return select;
}

export function checkboxInput(checked, onCommit, { disabled = false, label = '' } = {}) {
  const input = h('input', {
    type: 'checkbox',
    checked: Boolean(checked),
    disabled,
  });
  input.addEventListener('change', () => onCommit(input.checked));
  if (!label) return input;
  return h('label', { class: 'inline-check' }, input, h('span', null, label));
}

export function colorInput(value, onCommit, { disabled = false } = {}) {
  const input = h('input', {
    type: 'color',
    value: /^#[0-9a-f]{6}$/i.test(value || '') ? value : '#d9a441',
    disabled,
    title: value || '',
  });
  input.addEventListener('change', () => onCommit(input.value));
  return input;
}

export function tagInput(tags, onCommit, { disabled = false, placeholder = '用逗号分隔' } = {}) {
  const input = h('input', {
    class: 'input',
    type: 'text',
    value: (tags || []).join(', '),
    placeholder,
    disabled,
    spellcheck: 'false',
  });
  input.addEventListener('change', () => {
    const next = input.value
      .split(/[,，\s]+/)
      .map((tag) => tag.trim())
      .filter(Boolean);
    onCommit([...new Set(next)]);
  });
  return input;
}

/**
 * 生成一个「相对实体对象」的字段写回函数。
 *
 * projectField/mapField 接收的是实体对象（区域、地块种类、目录条目、地图地块），
 * path 也是相对该对象的路径。Store 的 mutator 参数是工程/地图根，因此这里不能用
 * 根对象配相对路径写回，否则会静默写到根对象的同名字段。
 *
 * 独立导出便于无 DOM 的单元测试覆盖这个边界。
 */
export function createFieldWriter(object, path, commit, label = path) {
  return (next) => commit(() => setByPath(object, path, next), label);
}

/**
 * 将一个字段绑定到工程实体。spec.type 支持 text / textarea / number / select /
 * checkbox / color / tags。写回后统一走 store.commitProject。
 */
export function projectField(store, object, path, spec = {}) {
  return boundField({
    object,
    path,
    spec,
    commit: (mutator, label) => store.commitProject(mutator, { label }),
  });
}

export function mapField(store, object, path, spec = {}) {
  return boundField({
    object,
    path,
    spec,
    commit: (mutator, label) => store.commitMap(mutator, { label }),
  });
}

function boundField({ object, path, spec, commit }) {
  const value = getByPath(object, path);
  const label = spec.label || path;
  const write = createFieldWriter(object, path, commit, label);
  let control;

  switch (spec.type) {
    case 'number': {
      const unit = spec.unit ? h('span', { class: 'unit' }, spec.unit) : null;
      control = h(
        'div',
        { class: 'field-row' },
        numberInput(value, write, spec),
        unit,
      );
      break;
    }
    case 'select':
      control = selectInput(spec.options || [], value, write, spec);
      break;
    case 'checkbox':
      control = checkboxInput(Boolean(value), write, {
        ...spec,
        label: spec.checkLabel || '',
      });
      break;
    case 'color':
      control = h(
        'div',
        { class: 'field-row' },
        colorInput(value, write, spec),
        h(
          'span',
          { class: 'unit', style: { fontFamily: 'var(--mono)' } },
          value || '继承区域色',
        ),
      );
      break;
    case 'tags':
      control = tagInput(value, write, spec);
      break;
    case 'textarea':
      return stackedField(label, textArea(value, write, spec), spec.hint);
    default:
      control = textInput(value, write, spec);
  }

  if (spec.stack) return stackedField(label, control, spec.hint);
  return field(label, control, spec.hint);
}

/* ------------------------------------------------------------------ *
 * 内容摘要与选择器
 * ------------------------------------------------------------------ */

export function contentSummary(project, contents) {
  const safe = contents || emptyContents();
  const lines = [];
  for (const type of CONTENT_TYPES) {
    const ids = safe[type.id] || [];
    if (ids.length === 0) continue;
    const names = ids.map((id) => {
      const item = (project.catalogs[type.id] || []).find((candidate) => candidate.id === id);
      return item?.name || id;
    });
    lines.push(
      h(
        'div',
        { class: 'content-line' },
        h(
          'span',
          { class: 'type', style: { color: type.color } },
          `${type.icon} ${type.short}`,
        ),
        h('span', { class: 'items', title: names.join('、') }, names.join('、')),
        h('span', { class: 'row-tail' }, String(ids.length)),
      ),
    );
  }
  return lines.length
    ? h('div', { class: 'content-summary' }, lines)
    : h('div', { class: 'empty-hint' }, '这个实体还没有挂载内容。');
}

export function contentCount(contents) {
  return CONTENT_TYPE_IDS.reduce((total, type) => total + (contents?.[type]?.length || 0), 0);
}

export function openContentPicker({ project, title, subtitle = '', contents, onSave }) {
  const draft = Object.fromEntries(
    CONTENT_TYPE_IDS.map((type) => [type, new Set(contents?.[type] || [])]),
  );
  const search = h('input', {
    class: 'input',
    type: 'search',
    placeholder: '按名称或 ID 过滤…',
    style: { marginBottom: '11px' },
  });
  const grid = h('div', { class: 'content-picker' });
  const groups = [];

  for (const type of CONTENT_TYPES) {
    const items = project.catalogs[type.id] || [];
    const list = h('div', { class: 'picker-list' });
    const count = h('span', { class: 'row-tail' }, '0');
    const group = h('div', { class: 'picker-group' });
    const rows = [];

    const sync = () => {
      count.textContent = String(draft[type.id].size);
      for (const row of rows) {
        const checkbox = row.querySelector('input[type="checkbox"]');
        if (checkbox) checkbox.checked = draft[type.id].has(row.dataset.id);
      }
      // 处理目录里已不存在的旧引用。
      const known = new Set(items.map((item) => item.id));
      const orphans = [...draft[type.id]].filter((id) => !known.has(id));
      orphanBox.replaceChildren();
      if (orphans.length > 0) {
        orphanBox.hidden = false;
        orphanBox.append(
          h('span', { class: 'field-hint' }, '目录中已不存在：'),
          ...orphans.map((id) =>
            h(
              'button',
              {
                class: 'chip danger',
                style: { cursor: 'pointer' },
                title: '移除这条旧引用',
                onclick: () => {
                  draft[type.id].delete(id);
                  sync();
                },
              },
              id,
            ),
          ),
        );
      } else {
        orphanBox.hidden = true;
      }
    };

    for (const item of items) {
      const checkbox = h('input', {
        type: 'checkbox',
        checked: draft[type.id].has(item.id),
      });
      const row = h(
        'label',
        { class: 'picker-item', dataset: { id: item.id, search: `${item.name} ${item.id}`.toLowerCase() } },
        checkbox,
        h('span', null, item.name),
        h('span', { class: 'rid' }, item.id),
      );
      checkbox.addEventListener('change', () => {
        if (checkbox.checked) draft[type.id].add(item.id);
        else draft[type.id].delete(item.id);
        sync();
      });
      rows.push(row);
      list.append(row);
    }

    if (items.length === 0) {
      list.append(h('div', { class: 'picker-item empty' }, '目录为空，可先到「内容」页添加。'));
    }

    const orphanBox = h('div', { class: 'chips', style: { padding: '0 9px 7px' }, hidden: true });
    group.append(
      h(
        'div',
        { class: 'picker-head', style: { color: type.color } },
        h('span', null, `${type.icon} ${type.label}`),
        count,
        h('span', { class: 'spacer' }),
        h(
          'button',
          {
            class: 'btn small ghost',
            type: 'button',
            onclick: () => {
              const visible = rows.filter((row) => !row.hidden);
              const allOn = visible.every((row) => draft[type.id].has(row.dataset.id));
              for (const row of visible) {
                if (allOn) draft[type.id].delete(row.dataset.id);
                else draft[type.id].add(row.dataset.id);
              }
              sync();
            },
          },
          '全选 / 反选',
        ),
        h(
          'button',
          {
            class: 'btn small ghost',
            type: 'button',
            onclick: () => {
              draft[type.id].clear();
              sync();
            },
          },
          '清空',
        ),
      ),
      list,
      orphanBox,
    );
    group.dataset.type = type.id;
    groups.push({ type, rows, group });
    grid.append(group);
    sync();
  }

  search.addEventListener('input', () => {
    const query = search.value.trim().toLowerCase();
    for (const { rows } of groups) {
      for (const row of rows) {
        row.hidden = Boolean(query) && !row.dataset.search.includes(query);
      }
    }
  });

  const body = h('div', null, search, grid);
  const dialog = openModal({
    title,
    subtitle: subtitle || '内容只作为目录引用挂在地块 / 子房间上，不参与生成算法。',
    size: 'wide',
    body,
    footer: [
      h(
        'span',
        { class: 'field-hint', style: { marginRight: 'auto' } },
        '保存后写入当前实体；已生成地图中的副本不会自动同步。',
      ),
      h('button', { class: 'btn', type: 'button', onclick: () => dialog.close() }, '取消'),
      h(
        'button',
        {
          class: 'btn primary',
          type: 'button',
          onclick: () => {
            const next = Object.fromEntries(
              CONTENT_TYPE_IDS.map((type) => [type, [...draft[type]].filter(Boolean)]),
            );
            onSave(next);
            dialog.close();
            toast('内容已更新', { type: 'success' });
          },
        },
        '保存内容',
      ),
    ],
  });
  return dialog;
}

export { icon };
