#!/usr/bin/env node
/**
 * 可选的真实浏览器冒烟测试：通过 Chrome DevTools Protocol 驱动已启动的页面。
 *
 * 终端 A：
 *   node tools/serve.mjs
 * 终端 B（Chrome 需允许远程调试）：
 *   chrome --headless=new --remote-debugging-port=9222 http://127.0.0.1:4173/
 * 终端 C：
 *   node tools/ui-smoke.mjs --port 9222 --out .smoke
 *
 * 这不是 npm test 的一部分；它依赖本机 Chrome，仅用于改 UI 后的视觉回归。
 */

import { mkdirSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { smokeDeadline } from './smoke-deadline.mjs';

const deadline = smokeDeadline();
const args = process.argv.slice(2);
const argValue = (name, fallback) => {
  const index = args.indexOf(name);
  return index >= 0 ? args[index + 1] : fallback;
};
const port = Number(argValue('--port', '9222'));
const urlPart = argValue('--url', '4173');
const query = argValue('--query', '');
const outDir = resolve(argValue('--out', '.smoke'));
mkdirSync(outDir, { recursive: true });

const sleep = (ms) => new Promise((resolvePromise) => setTimeout(resolvePromise, ms));
const targets = await (await deadline.fetch(`http://127.0.0.1:${port}/json/list`)).json();
const target = targets.find((item) => item.type === 'page' && item.url.includes(urlPart));
if (!target) {
  console.error(`找不到匹配 ${urlPart} 的页面，先启动 Chrome 并打开应用。`);
  process.exit(1);
}

const socket = new WebSocket(target.webSocketDebuggerUrl);
await new Promise((resolvePromise, reject) => {
  socket.addEventListener('open', resolvePromise, { once: true });
  socket.addEventListener('error', reject, { once: true });
});

let nextId = 0;
const pending = new Map();
const consoleErrors = [];
const exceptions = [];
socket.addEventListener('message', (event) => {
  const message = JSON.parse(event.data);
  if (message.id && pending.has(message.id)) {
    const { resolve: resolvePromise, reject } = pending.get(message.id);
    pending.delete(message.id);
    if (message.error) reject(new Error(message.error.message));
    else resolvePromise(message.result);
    return;
  }
  if (message.method === 'Runtime.consoleAPICalled' && message.params.type === 'error') {
    consoleErrors.push(message.params.args.map((arg) => arg.value ?? arg.description).join(' '));
  }
  if (message.method === 'Runtime.exceptionThrown') {
    exceptions.push(message.params.exceptionDetails.text);
  }
});

function send(method, params = {}) {
  const id = ++nextId;
  return new Promise((resolvePromise, reject) => {
    pending.set(id, { resolve: resolvePromise, reject });
    socket.send(JSON.stringify({ id, method, params }));
  });
}

async function evaluate(expression) {
  const result = await send('Runtime.evaluate', {
    expression,
    awaitPromise: true,
    returnByValue: true,
    userGesture: true,
  });
  if (result.exceptionDetails) {
    throw new Error(result.exceptionDetails.exception?.description || result.exceptionDetails.text);
  }
  return result.result.value;
}

async function screenshot(name) {
  const { data } = await send('Page.captureScreenshot', { format: 'png' });
  const file = resolve(outDir, name);
  writeFileSync(file, Buffer.from(data, 'base64'));
  console.log(`截图：${file}`);
}

async function waitFor(expression, timeout = 5000) {
  const started = Date.now();
  while (Date.now() - started < timeout) {
    if (await evaluate(expression)) return true;
    await sleep(100);
  }
  return false;
}

await send('Runtime.enable');
await send('Page.enable');
if (query) {
  const targetUrl = new URL(target.url);
  targetUrl.search = query.replace(/^\?/, '');
  await send('Page.navigate', { url: targetUrl.href });
  await sleep(500);
} else {
  await send('Page.reload', { ignoreCache: true });
  await sleep(800);
}
if (!(await waitFor('Boolean(window.PlotWeave)'))) {
  console.error('应用没有完成启动。');
  await screenshot('boot-failure.png');
  process.exit(1);
}

const bootError = await evaluate(
  "document.querySelector('#app')?.textContent?.includes('启动失败') || false",
);
if (bootError) {
  console.error('页面显示启动失败。');
  await screenshot('boot-failure.png');
  process.exit(1);
}

await evaluate("document.querySelector('#btn-generate').click()");
if (!(await waitFor("window.PlotWeave.store.mode === 'pools' && Boolean(window.PlotWeave.poolsEditor.previewBtn)"))) {
  throw Error('地图生成池没有打开。');
}
await screenshot('map-pools.png');
await evaluate(`(() => {
  const { store, floorEditor } = window.PlotWeave;
  store.setMode('floor');
  floorEditor.runGenerate('SMOKE-0001');
})()`);
if (!(await waitFor("window.PlotWeave.store.currentFloorMap?.seed === 'SMOKE-0001'", 4000))) {
  throw Error('旧单层工具没有生成测试地图。');
}
await screenshot('floor-generated.png');
// The former top-level generation modal is now a map-pool shortcut. Keep testing
// the older plot model below without relying on an unreachable modal.
await evaluate("window.PlotWeave.store.generate({ seed: 'SMOKE-0001' })");
await sleep(500);

const summary = await evaluate(`(() => {
  const { store, canvas } = window.PlotWeave;
  const map = store.currentMap;
  const errors = map.errors?.length || 0;
  return {
    seed: map.seed,
    plots: map.plots.length,
    links: map.links.length,
    crossings: map.links.filter((link) => link.type === 'cross').length,
    subRooms: map.plots.reduce((sum, plot) => sum + (plot.subRooms?.length || 0), 0),
    errors,
    warnings: map.warnings?.length || 0,
    spawn: map.spawn,
    scale: canvas.view.scale,
    profile: map.profileId,
  };
})()`);
console.log('生成结果：', summary);
await screenshot('generated.png');

await evaluate(`(() => {
  const { store } = window.PlotWeave;
  const map = store.currentMap;
  const target = map.plots.find((plot) => plot.id !== map.spawn?.plotId) || map.plots[0];
  store.select({ type: 'plot', id: target.id });
  return target.id;
})()`);
await sleep(300);
await screenshot('plot-selected.png');

await evaluate("document.querySelector('#btn-export').click()");
if (await waitFor("Boolean(document.querySelector('.modal'))")) {
  const labels = await evaluate(
    "[...document.querySelectorAll('.modal button, .modal .export-option')].map((node) => node.textContent.trim()).filter(Boolean)",
  );
  console.log('导出弹窗：', labels);
  await sleep(220);
  await screenshot('export-modal.png');
  await evaluate("document.querySelector('.modal-close').click()");
}

const openedPicker = await evaluate(
  "(() => { const button = [...document.querySelectorAll('button')].find((node) => node.textContent.includes('编辑实例内容')); if (!button) return false; button.click(); return true; })()",
);
if (openedPicker && (await waitFor("Boolean(document.querySelector('.content-picker'))"))) {
  await sleep(220);
  await screenshot('content-picker.png');
  await evaluate("document.querySelector('.modal-close').click()");
}

const spawnResult = await evaluate(`(() => {
  const { store } = window.PlotWeave;
  const map = store.currentMap;
  const kindById = new Map(store.project.plotKinds.map((kind) => [kind.id, kind]));
  const candidate = map.plots.find((plot) => kindById.get(plot.kindId)?.spawnEligible);
  if (!candidate) return { ok: false, message: '没有可出生地块' };
  const result = store.setSpawnAtCell(candidate.cells[0].x, candidate.cells[0].y);
  return { ok: result.ok, message: result.message, plotId: candidate.id, spawn: store.currentMap.spawn };
})()`);
console.log('出生点：', spawnResult);

const dataUrl = await evaluate(`(async () => {
  const blob = await window.PlotWeave.canvas.toPngBlob({ scale: 1.5, padding: 40 });
  return await new Promise((resolvePromise) => {
    const reader = new FileReader();
    reader.onload = () => resolvePromise(reader.result);
    reader.readAsDataURL(blob);
  });
})()`);
if (typeof dataUrl === 'string' && dataUrl.startsWith('data:image/png;base64,')) {
  const file = resolve(outDir, 'map-export.png');
  writeFileSync(file, Buffer.from(dataUrl.split(',')[1], 'base64'));
  console.log(`PNG 导出：${file}`);
}

const themeCheck = await evaluate(`(() => {
  const before = document.documentElement.dataset.theme;
  const button = document.querySelector('#btn-theme');
  if (!button) return { ok: false, reason: '顶栏没有主题按钮' };
  button.click();
  const after = document.documentElement.dataset.theme;
  const canvasTheme = window.PlotWeave.canvas.theme;
  const buttonTitle = button.title;
  button.click();
  const restored = document.documentElement.dataset.theme === before;
  return {
    ok: before !== after && after === canvasTheme && restored && Boolean(buttonTitle),
    before,
    after,
    canvasTheme,
    buttonTitle,
    restored,
  };
})()`);
console.log('主题切换：', themeCheck);
if (!themeCheck.ok) {
  console.error('主题切换失败：', themeCheck);
  process.exitCode = 4;
}

const fieldEdit = await evaluate(`(() => {
  const { store } = window.PlotWeave;
  const item = store.project.catalogs.props[0];
  if (!item) return { ok: false, reason: '没有可测试的内容条目' };
  const original = item.name;
  const rootBefore = store.project.name;
  store.select({ type: 'catalog', id: item.id, contentType: 'props' });
  const input = document.querySelector('#inspector input.input');
  if (!input) return { ok: false, reason: '右侧栏没有输入框' };
  input.value = original + '-E2E';
  input.dispatchEvent(new Event('change', { bubbles: true }));
  const changed = store.project.catalogs.props.find((entry) => entry.id === item.id)?.name === original + '-E2E';
  const rootUnchanged = store.project.name === rootBefore;
  const restore = document.querySelector('#inspector input.input');
  restore.value = original;
  restore.dispatchEvent(new Event('change', { bubbles: true }));
  const restored = store.project.catalogs.props.find((entry) => entry.id === item.id)?.name === original;
  return { ok: changed && restored && rootUnchanged, changed, restored, rootUnchanged, original };
})()`);
console.log('右侧栏字段编辑：', fieldEdit);
if (!fieldEdit.ok) {
  console.error('右侧栏字段写回失败：', fieldEdit);
  process.exitCode = 3;
}

const splitLayout = await evaluate(`(() => {
  const { store } = window.PlotWeave;
  store.select({ type: 'plotKind', id: store.project.plotKinds[0].id });
  const inputs = [...document.querySelectorAll('#inspector .split-2 input[type=number]')];
  const widths = inputs.map((input) => Math.round(input.getBoundingClientRect().width));
  return { count: inputs.length, widths, ok: inputs.length >= 2 && Math.min(...widths) >= 90 };
})()`);
console.log('最短/最长输入宽度：', splitLayout);
if (!splitLayout.ok) {
  console.error('最短/最长输入仍被挤压：', splitLayout);
  process.exitCode = 5;
}

const storage = await evaluate(`(() => {
  const raw = localStorage.getItem('plotweave:workspace:v1');
  return raw ? JSON.parse(raw) : null;
})()`);
console.log('本地存储：', storage ? `maps=${storage.maps?.length || 0}` : '空');
console.log('console.error：', consoleErrors.length ? consoleErrors : '无');
console.log('未捕获异常：', exceptions.length ? exceptions : '无');

deadline.close(socket);
if (exceptions.length || consoleErrors.length) process.exitCode = 2;
