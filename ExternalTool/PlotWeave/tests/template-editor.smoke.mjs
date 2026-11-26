// Run against an isolated PlotWeave page in Chrome with remote debugging enabled (Node 22+).
// node tests/template-editor.smoke.mjs [debugPort=9222] [pagePort=4173]
import { smokeDeadline } from '../tools/smoke-deadline.mjs';

const deadline = smokeDeadline();
const [debugPort = '9222', pagePort = '4173'] = process.argv.slice(2);
const targets = await (await deadline.fetch(`http://127.0.0.1:${debugPort}/json/list`)).json();
const target = targets.find((entry) => entry.type === 'page' && new URL(entry.url).port === pagePort);
if (!target) throw Error(`No PlotWeave page on port ${pagePort}`);
const socket = new WebSocket(target.webSocketDebuggerUrl);
await new Promise((resolve, reject) => {
  socket.addEventListener('open', resolve, { once: true });
  socket.addEventListener('error', reject, { once: true });
});

try {
  const result = await new Promise((resolve) => {
    socket.addEventListener('message', ({ data }) => resolve(JSON.parse(data)), { once: true });
    socket.send(JSON.stringify({ id: 1, method: 'Runtime.evaluate', params: {
      expression: `(${exerciseTemplateEditor})()`, awaitPromise: true, returnByValue: true,
    } }));
  });
  if (result.error || result.result.exceptionDetails) {
    throw Error(result.error?.message || result.result.exceptionDetails.exception?.description);
  }
  console.log(result.result.result.value);
} finally {
  deadline.close(socket);
}

async function exerciseTemplateEditor() {
  const ensure = (pass, why) => { if (!pass) throw Error(why); };
  const { store, templateEditor } = window.PlotWeave;
  const initialProject = structuredClone(store.project);
  const initialMode = store.mode;
  const originalHistory = [...store.history];
  const originalFuture = [...store.future];
  const wait = () => new Promise((resolve) => setTimeout(resolve, 10));
  const field = (selector, value) => {
    const field = document.querySelector(selector);
    field.value = value;
    field.dispatchEvent(new Event('input', { bubbles: true }));
  };
  const byId = (id) => document.querySelector(`.tpl-card[data-template-id="${CSS.escape(id)}"]`);
  const select = (id) => byId(id).querySelector('.tpl-card-select').click();
  const create = (id) => byId(id).querySelector('[title="创建子模板"]').click();
  const children = (id) => store.dungeon.stripTemplates.filter((t) => t.parentTemplateId === id);
  try {
    store.setMode('templates');
    const root = store.dungeon.stripTemplates.find((t) => !t.parentTemplateId && t.cells.some((c) => c.type === 'spawn'));
    ensure(root && byId(root.id), 'Existing root visible');
    select(root.id);
    await wait();
    create(root.id);
    await wait();
    ensure(templateEditor.draftIsNew && templateEditor.dirty && templateEditor.draft.parentTemplateId === root.id, 'Child draft retains root ID');
    ensure(document.querySelector('[title="顺时针旋转 90°"]').disabled, 'Rotation is disabled for a child');
    ensure(document.querySelector('[title="生成权重"]').disabled, 'Family generation fields are disabled');
    ensure(!document.querySelector('.tpl-cell.empty-slot:not(:disabled)'), 'All empty cells are locked');
    const spawn = templateEditor.gridCells.find((c) => c.type === 'spawn');
    templateEditor.onClickCell(spawn.gx, spawn.gy);
    ensure(templateEditor.getSelectedGridCell().type === 'spawn' && document.querySelector('.tpl-cell-bar select').disabled, 'Spawn cell cannot be replaced');
    const other = templateEditor.gridCells.find((c) => c.type === 'empty');
    templateEditor.onClickCell(other.gx, other.gy);
    ensure(templateEditor.getSelectedGridCell().type === 'combat', 'Normal cell contents remain editable');
    field('.tpl-inspector input[type=text]', '烟测独特子模板');
    field('.tpl-inspector textarea', '浏览器往返');
    field('.tpl-cell-bar input[type=text]', '烟测');
    document.querySelector('.tpl-stage .tpl-tool-row .btn.primary').click();
    await wait();
    const first = children(root.id).find((t) => t.name === '烟测独特子模板');
    ensure(first?.description === '浏览器往返' && first.cells.some((c) => c.tags.includes('烟测')), 'Edited draft content saved');
    ensure(byId(first.id)?.classList.contains('tpl-card-child') && byId(first.id).querySelector('svg rect'), 'Indented child has own content thumbnail');
    byId(root.id).querySelector('.tpl-family-toggle').click();
    ensure(!byId(first.id) && templateEditor.draft.id === first.id, 'Arrow collapses without changing selection');
    select(root.id);
    await wait();
    ensure(byId(first.id) && templateEditor.draft.id === root.id && templateEditor.structureLocked, 'Parent click expands and locks original');
    const search = '.tpl-sidebar input[type=search]';
    field(search, '烟测独特');
    ensure(byId(root.id) && byId(first.id), 'Searching for child retains parent context');
    field(search, '');
    create(first.id);
    await wait();
    ensure(templateEditor.draft.parentTemplateId === root.id, 'Creating from child flattens to a sibling');
    select(root.id);
    await wait();
    document.querySelector('.modal-foot button:not(.primary)').click();
    await wait();
    ensure(templateEditor.draftIsNew && templateEditor.draft.parentTemplateId === root.id, 'Cancelling draft discard preserves parent ID');
    document.querySelector('.tpl-stage .tpl-tool-row .btn.primary').click();
    await wait();
    const sibling = templateEditor.draft.id;
    store.undo();
    ensure(!byId(sibling), 'Undo removes saved sibling');
    store.redo();
    ensure(byId(sibling) && children(root.id).some((t) => t.id === sibling), 'Redo restores sibling relationship');
    return 'PlotWeave template family browser smoke passed';
  } finally {
    store.loadWorkspace({ project: initialProject });
    store.setMode(initialMode);
    store.history = originalHistory;
    store.future = originalFuture;
  }
}
