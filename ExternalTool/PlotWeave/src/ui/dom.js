/**
 * 极小的 DOM 工具层。没有框架依赖，方便把 UI 移植到别的宿主。
 */

export function h(tag, attrs = null, ...children) {
  const el = document.createElement(tag);
  if (attrs) {
    for (const [key, value] of Object.entries(attrs)) {
      if (value == null || value === false) continue;
      if (key === 'class' || key === 'className') el.className = value;
      else if (key === 'style' && typeof value === 'object') Object.assign(el.style, value);
      else if (key === 'dataset') Object.assign(el.dataset, value);
      else if (key.startsWith('on') && typeof value === 'function') {
        el.addEventListener(key.slice(2).toLowerCase(), value);
      } else if (key === 'html') el.innerHTML = value;
      else if (key === 'text') el.textContent = value;
      else if (key === 'value' && el instanceof HTMLInputElement) el.value = value;
      else if (value === true) el.setAttribute(key, '');
      else el.setAttribute(key, String(value));
    }
  }
  append(el, children);
  return el;
}

export function append(parent, children) {
  for (const child of children.flat(Infinity)) {
    if (child == null || child === false) continue;
    parent.append(child instanceof Node ? child : document.createTextNode(String(child)));
  }
  return parent;
}

export function clear(el) {
  while (el.firstChild) el.firstChild.remove();
  return el;
}

export function mount(el, ...children) {
  clear(el);
  append(el, children);
  return el;
}

export const qs = (selector, root = document) => root.querySelector(selector);
export const qsa = (selector, root = document) => [...root.querySelectorAll(selector)];

export function fmt(value, digits = 0) {
  const number = Number(value);
  if (!Number.isFinite(number)) return '—';
  return number.toLocaleString('zh-CN', {
    minimumFractionDigits: digits,
    maximumFractionDigits: digits,
  });
}

export function pct(value, digits = 0) {
  return `${fmt((Number(value) || 0) * 100, digits)}%`;
}

export function clamp(value, min, max) {
  return Math.min(max, Math.max(min, value));
}

export function debounce(fn, wait = 180) {
  let timer = 0;
  return (...args) => {
    window.clearTimeout(timer);
    timer = window.setTimeout(() => fn(...args), wait);
  };
}

export function isEditableTarget(target) {
  if (!(target instanceof Element)) return false;
  return Boolean(target.closest('input, textarea, select, [contenteditable="true"]'));
}

export function downloadBlob(filename, blob) {
  const url = URL.createObjectURL(blob);
  const link = h('a', { href: url, download: filename, style: { display: 'none' } });
  document.body.append(link);
  link.click();
  link.remove();
  window.setTimeout(() => URL.revokeObjectURL(url), 1200);
}

export function downloadText(filename, text, mime = 'application/json') {
  downloadBlob(filename, new Blob([text], { type: `${mime};charset=utf-8` }));
}

export function pickFile({ accept = '.json,application/json', multiple = false } = {}) {
  return new Promise((resolve) => {
    const input = h('input', { type: 'file', accept, multiple, style: { display: 'none' } });
    document.body.append(input);
    input.addEventListener('change', () => {
      const files = multiple ? [...(input.files || [])] : input.files?.[0];
      input.remove();
      resolve(files || (multiple ? [] : null));
    });
    input.click();
  });
}

export function readFileText(file) {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(String(reader.result || ''));
    reader.onerror = () => reject(reader.error || new Error('读取文件失败'));
    reader.readAsText(file, 'utf-8');
  });
}

export function slugify(input, fallback = 'plotweave') {
  const text = String(input || '')
    .trim()
    .toLowerCase()
    .replace(/[\\/:*?"<>|\s]+/g, '-')
    .replace(/-+/g, '-')
    .replace(/^-|-$/g, '');
  return text || fallback;
}

export function timestamp() {
  const now = new Date();
  const pad = (n) => String(n).padStart(2, '0');
  return `${now.getFullYear()}${pad(now.getMonth() + 1)}${pad(now.getDate())}-${pad(now.getHours())}${pad(now.getMinutes())}`;
}

/* ------------------------------------------------------------------ *
 * 图标：内联 SVG，避免外部资源与字体依赖。
 * ------------------------------------------------------------------ */

const ICONS = {
  cursor: ['m5 3 14 8-6 1.6L10 19 5 3Z'],
  folder: ['M3.5 6.5A1.5 1.5 0 0 1 5 5h4l1.5 1.8H19a1.5 1.5 0 0 1 1.5 1.5v6.2A1.5 1.5 0 0 1 19 16H5a1.5 1.5 0 0 1-1.5-1.5V6.5Z'],
  'folder-plus': ['M3.5 6.5A1.5 1.5 0 0 1 5 5h4l1.5 1.8H19a1.5 1.5 0 0 1 1.5 1.5v6.2A1.5 1.5 0 0 1 19 16H5a1.5 1.5 0 0 1-1.5-1.5V6.5Z', 'M12 10v4', 'M10 12h4'],
  sliders: ['M4 7h6', 'M14 7h6', 'M4 17h10', 'M18 17h2', 'M10 4v6', 'M14 14v6'],
  undo: ['M9 14 4 9l5-5', 'M4 9h9a6 6 0 0 1 0 12h-3'],
  redo: ['m15 14 5-5-5-5', 'M20 9h-9a6 6 0 0 0 0 12h3'],
  import: ['M12 3v12', 'm7 10 5 5 5-5', 'M4 19h16'],
  export: ['M12 21V9', 'm7 14 5-5 5 5', 'M4 5h16'],
  generate: ['M4 7h10', 'M18 7h2', 'M4 17h4', 'M12 17h8', 'M14 4v6', 'M8 14v6'],
  help: ['M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18Z', 'M9.5 9a2.5 2.5 0 1 1 3.4 2.3c-.7.3-1.1 1-1.1 1.8v.4', 'M12 17.2h.01'],
  fit: ['M8 3H4v4', 'M16 3h4v4', 'M8 21H4v-4', 'M16 21h4v-4'],
  plus: ['M12 5v14', 'M5 12h14'],
  minus: ['M5 12h14'],
  grid: ['M4 4h16v16H4z', 'M4 10h16', 'M4 16h16', 'M10 4v16', 'M16 4v16'],
  labels: ['M4 6h16', 'M7 12h10', 'M9 18h6'],
  contents: ['M5 5h6v6H5z', 'M13 5h6v6h-6z', 'M5 13h6v6H5z', 'M13 13h6v6h-6z'],
  layers: ['m12 3 9 5-9 5-9-5 9-5Z', 'm3 12 9 5 9-5', 'm3 16 9 5 9-5'],
  spawn: ['M12 21s7-6.1 7-11a7 7 0 1 0-14 0c0 4.9 7 11 7 11Z', 'M12 12.5a2.5 2.5 0 1 0 0-5 2.5 2.5 0 0 0 0 5Z'],
  close: ['M6 6l12 12', 'M18 6 6 18'],
  trash: ['M4 7h16', 'M9 7V4h6v3', 'M6 7l1 13h10l1-13', 'M10 11v5', 'M14 11v5'],
  chevron: ['m9 6 6 6-6 6'],
  dots: ['M5 12h.01', 'M12 12h.01', 'M19 12h.01'],
  download: ['M12 3v12', 'm7 10 5 5 5-5', 'M4 21h16'],
  upload: ['M12 17V5', 'm7 10 5-5 5 5', 'M4 21h16'],
  copy: ['M8 8h11v11H8z', 'M5 16V5h11'],
  eye: ['M2 12s3.5-6 10-6 10 6 10 6-3.5 6-10 6S2 12 2 12Z', 'M12 14.5a2.5 2.5 0 1 0 0-5 2.5 2.5 0 0 0 0 5Z'],
  sun: [
    'M12 17a5 5 0 1 0 0-10 5 5 0 0 0 0 10Z',
    'M12 2v2',
    'M12 20v2',
    'm4.9 4.9 1.4 1.4',
    'm17.7 17.7 1.4 1.4',
    'M2 12h2',
    'M20 12h2',
    'm4.9 19.1 1.4-1.4',
    'm17.7 6.3 1.4-1.4',
  ],
  moon: ['M20 14.5A8.5 8.5 0 0 1 9.5 4 8.5 8.5 0 1 0 20 14.5Z'],
  arrow: ['M5 12h14', 'm13 6 6 6-6 6'],
  alert: ['M12 4 3 19h18L12 4Z', 'M12 10v4', 'M12 17h.01'],
};

export function icon(name, size = 14) {
  const paths = ICONS[name] || ICONS.dots;
  const ns = 'http://www.w3.org/2000/svg';
  const svg = document.createElementNS(ns, 'svg');
  svg.setAttribute('viewBox', '0 0 24 24');
  svg.setAttribute('width', String(size));
  svg.setAttribute('height', String(size));
  svg.setAttribute('fill', 'none');
  svg.setAttribute('stroke', 'currentColor');
  svg.setAttribute('stroke-width', '1.7');
  svg.setAttribute('stroke-linecap', 'round');
  svg.setAttribute('stroke-linejoin', 'round');
  svg.setAttribute('aria-hidden', 'true');
  for (const d of paths) {
    const path = document.createElementNS(ns, 'path');
    path.setAttribute('d', d);
    svg.append(path);
  }
  return svg;
}

export function iconButton(name, title, onClick, { active = false, disabled = false } = {}) {
  return h(
    'button',
    {
      class: `btn icon ghost${active ? ' active' : ''}`,
      title,
      'aria-label': title,
      disabled,
      onclick: onClick,
    },
    icon(name),
  );
}
