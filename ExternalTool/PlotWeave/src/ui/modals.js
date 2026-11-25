/**
 * 模态框与轻提示。所有对话框都挂到 #modal-root / #toast-root。
 */

import { h, mount, qs, icon } from './dom.js';

const modalRoot = () => qs('#modal-root');
const toastRoot = () => qs('#toast-root');

let openCount = 0;

export function openModal({ title, subtitle = '', body, footer = null, size = '', onClose = null }) {
  const root = modalRoot();
  const backdrop = h('div', { class: 'modal-backdrop' });
  const close = () => {
    if (!backdrop.isConnected) return;
    backdrop.remove();
    openCount = Math.max(0, openCount - 1);
    document.removeEventListener('keydown', onKey);
    if (openCount === 0) document.body.style.removeProperty('overflow');
    onClose?.();
  };
  const onKey = (event) => {
    if (event.key === 'Escape') {
      event.stopPropagation();
      close();
    }
  };

  const modal = h(
    'div',
    { class: `modal ${size}`.trim(), role: 'dialog', 'aria-modal': 'true' },
    h(
      'div',
      { class: 'modal-head' },
      h(
        'div',
        null,
        h('h2', { class: 'modal-title' }, title),
        subtitle ? h('p', { class: 'modal-sub' }, subtitle) : null,
      ),
      h(
        'button',
        { class: 'btn icon ghost modal-close', title: '关闭', 'aria-label': '关闭', onclick: close },
        icon('close'),
      ),
    ),
    h('div', { class: 'modal-body' }, body),
    footer
      ? h('div', { class: 'modal-foot' }, footer)
      : null,
  );

  backdrop.addEventListener('pointerdown', (event) => {
    if (event.target === backdrop) close();
  });
  backdrop.append(modal);
  root.append(backdrop);
  openCount += 1;
  document.body.style.overflow = 'hidden';
  document.addEventListener('keydown', onKey);

  const focusTarget =
    modal.querySelector('input:not([type="hidden"]), textarea, select, .modal-foot .btn.primary') ||
    modal.querySelector('.modal-foot .btn:last-child') ||
    modal.querySelector('button:not(.modal-close)');
  window.setTimeout(() => focusTarget?.focus(), 30);

  return { backdrop, modal, body: modal.querySelector('.modal-body'), footer, close };
}

export function confirmDialog({
  title = '确认操作',
  message = '',
  confirmText = '确认',
  cancelText = '取消',
  danger = false,
} = {}) {
  return new Promise((resolve) => {
    let settled = false;
    const finish = (value) => {
      if (settled) return;
      settled = true;
      resolve(value);
      dialog.close();
    };
    const dialog = openModal({
      title,
      size: 'narrow',
      body: h('p', { style: { margin: '0', color: 'var(--muted)', lineHeight: '1.7' } }, message),
      footer: [
        h('div', { class: 'spacer' }),
        h('button', { class: 'btn', onclick: () => finish(false) }, cancelText),
        h(
          'button',
          { class: `btn ${danger ? 'danger' : 'primary'}`, onclick: () => finish(true) },
          confirmText,
        ),
      ],
      onClose: () => finish(false),
    });
  });
}

export function toast(message, { type = 'info', detail = '', timeout = 3200 } = {}) {
  const root = toastRoot();
  if (!root) return;
  const node = h(
    'div',
    { class: `toast ${type}` },
    message,
    detail ? h('small', null, detail) : null,
  );
  root.append(node);
  window.setTimeout(() => {
    node.style.transition = 'opacity 160ms ease, transform 160ms ease';
    node.style.opacity = '0';
    node.style.transform = 'translateY(4px)';
    window.setTimeout(() => node.remove(), 180);
  }, timeout);
}

export function openTextViewer({ title, subtitle = '', text, filename = 'plotweave.json', actions = [] }) {
  const textarea = h('textarea', {
    class: 'textarea import-area',
    readonly: true,
    spellcheck: 'false',
  });
  textarea.value = text;
  const copy = async () => {
    try {
      await navigator.clipboard.writeText(text);
      toast('已复制到剪贴板', { type: 'success' });
    } catch {
      textarea.select();
      document.execCommand('copy');
      toast('已复制到剪贴板', { type: 'success' });
    }
  };
  const dialog = openModal({
    title,
    subtitle,
    size: 'wide',
    body: textarea,
    footer: [
      h('div', { class: 'spacer' }),
      h('button', { class: 'btn', onclick: copy }, '复制'),
      h(
        'button',
        {
          class: 'btn primary',
          onclick: () => {
            const blob = new Blob([text], { type: 'application/json;charset=utf-8' });
            const url = URL.createObjectURL(blob);
            const link = h('a', { href: url, download: filename, style: { display: 'none' } });
            document.body.append(link);
            link.click();
            link.remove();
            window.setTimeout(() => URL.revokeObjectURL(url), 1000);
            toast('已导出文件', { type: 'success', detail: filename });
          },
        },
        '下载',
      ),
      ...actions,
    ],
  });
  return dialog;
}

export function openFormModal({ title, subtitle = '', fields, onSubmit, submitText = '保存', size = '' }) {
  const form = h('form', { class: 'modal-form' }, fields);
  const dialog = openModal({
    title,
    subtitle,
    size,
    body: form,
    footer: [
      h('div', { class: 'spacer' }),
      h('button', { class: 'btn', type: 'button', onclick: () => dialog.close() }, '取消'),
      h('button', { class: 'btn primary', type: 'button', onclick: () => form.requestSubmit() }, submitText),
    ],
  });
  form.addEventListener('submit', (event) => {
    event.preventDefault();
    const data = Object.fromEntries(new FormData(form).entries());
    const result = onSubmit(data, dialog);
    if (result !== false) dialog.close();
  });
  return dialog;
}

export function openHelp() {
  const body = h(
    'div',
    { class: 'help-grid' },
    helpCard('模式一 · 条带模板', [
      h('p', null, '条带支持任意四连通多格形状（直线、L/Z/T/U 折线、矩形等），任意格皆可作为门或单格交叉点。'),
      h('p', { style: { marginTop: '6px' } }, '可在「全局格子配置」统一设定各类格子的资源值与难度值，条带自动求和并支持手动覆写；使用右侧「随机生成条带草稿」后可在画布手工精修并保存、导出 JSON。'),
    ]),
    helpCard('模式二 · 单层三角地图', [
      h('p', null, '每层由 3 个区域呈三角形排布：每区内部至少形成 1 个真实环路（≥3 节点），区域间有且仅有 1 条独立跨区桥接条带且仅用门相连。'),
      h('p', { style: { marginTop: '6px' } }, '资源/难度总区间留空则自动计算，也可手动指定。以出生点为原点计算节点图 BFS 距离，生成条带等级与战斗格敌人等级（叠加难度偏移，不改条带总难度）。'),
    ]),
    helpCard('快捷键与交互', [
      h(
        'ul',
        null,
        h('li', null, '1 / 2 / 3：切换「条带模板 / 单层地图 / 完整地图」。'),
        h('li', null, 'N：立即生成新的单层三角地图；F：自适应画布；G：开关网格。'),
        h('li', null, '单层画布支持滚轮缩放、拖拽平移、点击条带查看或原地替换同形模板与微调偏移。'),
        h('li', null, '工具栏可一键开启「节点图」叠层（查看 BFS 跳数）与「距离热力」。'),
        h('li', null, 'Ctrl+Z / Ctrl+Shift+Z 撤销重做；顶栏太阳/月亮切换亮暗主题。'),
      ),
    ]),
    helpCard('导入导出与架构', [
      h('p', null, '支持独立导入导出「条带模板库 JSON」、导出「单层地图 JSON / 2x PNG」以及完整工程 JSON。'),
      h('p', { style: { marginTop: '6px' } }, '核心算法（polyomino / strip-templates / floor-generator / floor-validator）位于 src/core，纯 ES Modules 无外部依赖，可直接接入游戏引擎或后端。'),
    ]),
  );
  openModal({
    title: 'PlotWeave · 类银河城地牢编辑器说明',
    subtitle: '条带模板制作 · 三区域三角单层生成 · 节点图等级可视化',
    size: 'wide',
    body,
    footer: [
      h('div', { class: 'spacer' }),
      h('button', { class: 'btn primary', onclick: () => qs('.modal-close')?.click() }, '知道了'),
    ],
  });
}

function helpCard(title, children) {
  return h('div', { class: 'help-card' }, h('h3', null, title), ...children);
}

export { mount };
