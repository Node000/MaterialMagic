/**
 * 明暗主题。
 *
 * 主题是纯 UI 偏好，不写入工程或地图 Bundle；单独保存在 localStorage，
 * 这样导出的工程不会因为某个使用者的显示偏好而带上无关字段。
 */

export const THEME_KEY = 'plotweave:theme:v1';
export const THEMES = ['dark', 'light'];
export const THEME_LABELS = { dark: '暗色', light: '亮色' };

export function isTheme(value) {
  return value === 'dark' || value === 'light';
}

export function getTheme() {
  const current = document.documentElement.dataset.theme;
  if (isTheme(current)) return current;
  try {
    const stored = localStorage.getItem(THEME_KEY);
    if (isTheme(stored)) return stored;
  } catch {
    /* localStorage 不可用时退回系统偏好 */
  }
  return window.matchMedia?.('(prefers-color-scheme: light)').matches ? 'light' : 'dark';
}

export function applyTheme(theme, { persist = true } = {}) {
  const next = isTheme(theme) ? theme : 'dark';
  document.documentElement.dataset.theme = next;
  document.documentElement.style.colorScheme = next;
  if (persist) {
    try {
      localStorage.setItem(THEME_KEY, next);
    } catch {
      /* 隐私模式等场景下不阻塞界面 */
    }
  }
  window.dispatchEvent(new CustomEvent('plotweave:themechange', { detail: { theme: next } }));
  return next;
}

export function toggleTheme() {
  return applyTheme(getTheme() === 'light' ? 'dark' : 'light');
}
