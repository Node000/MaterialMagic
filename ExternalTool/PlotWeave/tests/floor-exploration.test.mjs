import assert from 'node:assert/strict';
import { webcrypto } from 'node:crypto';
import test from 'node:test';
import { FloorExploration, EXPLORATION_RADIUS, explorationDirection } from '../src/core/floor-exploration.js';
import { defaultMapPools } from '../src/core/map-pools.js';
import { MapPoolsEditor } from '../src/ui/map-pools-editor.js';

// The editor sees this natively in browsers; older supported Node versions may not expose it globally.
if (!globalThis.crypto) Object.defineProperty(globalThis, 'crypto', { value: webcrypto, configurable: true });

const cell = (x, y, type, enemyLevel = null) => ({ x, y, type, enemyLevel, stripIds: ['PRIVATE_STRIP'] });
const edge = (x, y, dir, type = 'open') => ({ x, y, dir, type });
const floor = {
  kind: 'plotweave-floor-map', schemaVersion: 1, id: 'private-floor', spawn: { x: 10, y: -3 },
  cells: [cell(10, -3, 'spawn'), cell(11, -3, 'combat', 3), cell(12, -3, 'empty'), cell(12, -2, 'hidden-reward', 99),
    cell(11, -2, 'private-content', 23), cell(10, -2, 'shop')],
  edges: [edge(10, -3, 'E', 'door'), edge(11, -3, 'E'), edge(12, -3, 'S', 'door'), edge(11, -2, 'E', 'door'), edge(10, -2, 'E')],
  budget: { actual: { difficulty: 123456 } },
};
const newFloor = { ...floor, spawn: { x: -150, y: 500 }, cells: [cell(-150, 500, 'rest')], edges: [] };

const find = (walk, x, y) => walk.view().find((c) => c.x === x && c.y === y);
const current = (walk) => walk.view().find((c) => c.current);

test('exploration starts at the canonical spawn and only walks open edges and doors in both directions', () => {
  const walk = new FloorExploration(floor);
  assert.equal(current(walk).type, 'spawn');
  assert.equal(current(walk).borders.E, 'door');
  assert.equal(current(walk).borders.S, 'wall');
  assert.equal(walk.move('S'), false, 'an adjacent shop is not reachable through a wall');
  assert.equal(walk.move('N'), false, 'cannot leave the floor');
  assert.equal(walk.move('diagonal'), false);
  assert.equal(walk.move('E'), true);
  assert.equal(current(walk).type, 'combat');
  assert.equal(walk.move('W'), true);
  assert.equal(walk.move('E'), true);
  assert.equal(walk.move('E'), true);
  assert.equal(walk.move('S'), true);
  assert.equal(walk.move('W'), true);
  assert.equal(current(walk).type, 'private-content');
  assert.equal(walk.move('N'), false, 'there is no edge back to the adjacent combat cell');
});

test('the whole silhouette is shown, but only the two-cell Manhattan diamond reveals content', () => {
  const initial = structuredClone(floor);
  const walk = new FloorExploration(floor);
  assert.equal(EXPLORATION_RADIUS, 2);
  assert.equal(walk.view().length, floor.cells.length);
  assert.equal(find(walk, 11, -3).enemyLevel, 3);
  assert.equal(find(walk, 12, -3).type, 'empty', 'two cells straight ahead');
  assert.equal(find(walk, 11, -2).type, 'private-content', 'one horizontal and one vertical step');
  assert.equal(find(walk, 10, -2).type, 'shop', 'walls affect movement, not geometric sight distance');
  assert.deepEqual(find(walk, 12, -2), { x: 12, y: -2, visible: false, borders: { E: 'outline', S: 'outline' } });
  assert.equal(find(walk, 12, -3).borders.S, 'fog', 'a door to a hidden cell is not disclosed');
  assert.doesNotMatch(JSON.stringify(walk.view()), /hidden-reward|PRIVATE_STRIP|123456|private-floor/);
  walk.move('E');
  assert.equal(find(walk, 12, -2).enemyLevel, 99);
  assert.equal(find(walk, 12, -3).borders.S, 'door');
  walk.move('W');
  assert.equal(find(walk, 12, -2).visible, false, 'an unvisited cell returns to fog');
  assert.deepEqual(floor, initial, 'movement does not mutate the input floor');
});

test('walked cells stay revealed across the fixed full-map projection', () => {
  const len = 13;
  const long = { ...floor, spawn: { x: 100, y: 150 },
    cells: Array.from({ length: len }, (_, i) => cell(100 + i, 150, i === 1 ? 'combat' : 'empty', i === 1 ? 7 : null)),
    edges: Array.from({ length: len - 1 }, (_, i) => edge(100 + i, 150, 'E')) };
  const walk = new FloorExploration(long);
  const footprint = walk.view().map(({ x, y }) => [x, y]);
  assert.equal(footprint.length, len);
  assert.equal(find(walk, 112, 150).visible, false);
  for (let i = 1; i < len; i += 1) assert.equal(walk.move('E'), true);
  assert.deepEqual(walk.view().map(({ x, y }) => [x, y]), footprint);
  assert.equal(current(walk).x, 112);
  assert.equal(find(walk, 101, 150).enemyLevel, 7, 'walked content stays visible even far away');
  assert.equal(find(walk, 100, 150).visible, true, 'spawn counts as walked');
  assert.equal(find(walk, 101, 150).borders.E, 'open');
});

test('movement key parsing excludes composition, modifiers and unrelated commands', () => {
  for (const [key, direction] of Object.entries({ W: 'N', a: 'W', s: 'S', d: 'E', ArrowDown: 'S', ArrowRight: 'E' })) {
    assert.equal(explorationDirection({ key }), direction);
  }
  for (const modifier of ['ctrlKey', 'metaKey', 'altKey', 'shiftKey', 'isComposing', 'defaultPrevented']) {
    assert.equal(explorationDirection({ key: 's', [modifier]: true }), null);
  }
  assert.equal(explorationDirection({ key: 'Escape' }), null);
});

function editorHarness(explorePreview) {
  const config = defaultMapPools();
  const result = { floor, seed: 0 };
  const fail = () => assert.fail('exploration must not edit the draft, publish, or refresh the regular previews');
  const editor = Object.assign(Object.create(MapPoolsEditor.prototype), {
    root: { hidden: false }, poolIndex: 0, entryIndex: 0, initialSeed: 0, explorePreview,
    getPools: () => config, resultFor: () => result, seeds: new Map(), previews: new Map(),
    updatePools: fail, preview: fail, publish: fail, saveDraft: fail,
    walkPanel: { focus() {} }, renderExploration() {}, syncExploration() {}, drawExplorationView() {}, render() {},
  });
  editor.startExploration();
  return { editor, config, result };
}

test('exploration reroll is transactional and only resets fog on successful valid output', async () => {
  let request;
  const { editor, config, result } = editorHarness(async (args) => {
    request = args;
    return { floor: newFloor, metrics: { secret: 'ignore' } };
  });
  const configBefore = structuredClone(config);
  const { exploration: session } = editor;
  session.walk.move('E');
  session.walk.move('E');
  const before = session.walk.view();
  const oldWalk = session.walk;
  const success = editor.explorePreview;
  editor.explorePreview = async () => { throw Error('private generator details'); };
  await editor.rerollExploration();
  assert.deepEqual(session.walk.view(), before);
  assert.equal(session.walk, oldWalk);
  assert.equal(session.seed, 0);
  assert.equal(session.error, true);
  assert.equal(session.request, null);
  editor.explorePreview = async () => ({ floor: { ...newFloor, spawn: { x: 0, y: 0 } } });
  await editor.rerollExploration();
  assert.equal(session.walk, oldWalk);
  assert.equal(session.error, true);
  editor.explorePreview = success;
  await editor.rerollExploration();
  assert.notEqual(session.walk, oldWalk);
  assert.equal(session.seed, request.seed);
  assert.notEqual(session.seed, 0);
  assert.equal(session.error, false);
  assert.equal(session.walk.view().length, 1);
  assert.equal(current(session.walk).type, 'rest');
  assert.equal(find(session.walk, 10, -3), undefined, 'old footprint and visited cells are discarded');
  assert.deepEqual(config, configBefore);
  assert.equal(editor.exploration.result, result);
  assert.equal(editor.seeds.size + editor.previews.size, 0);
});

test('exploration ignores late generation after leaving or recipe identity changes', async () => {
  for (const invalidate of [
    (editor) => { editor.root.hidden = true; editor.leave(); },
    (editor) => { editor.resultFor = () => ({ floor, seed: 0 }); editor.clearExploration(); },
  ]) {
    let resolve, signal, calls = 0;
    const { editor } = editorHarness((args, requestSignal) => {
      calls++;
      signal = requestSignal;
      return new Promise((done) => { resolve = done; });
    });
    const oldSession = editor.exploration;
    const oldWalk = oldSession.walk;
    const pending = editor.rerollExploration();
    await editor.rerollExploration();
    assert.equal(calls, 1, 'there is only one in-flight exploration request');
    invalidate(editor);
    assert.equal(signal.aborted, true);
    resolve({ floor: newFloor });
    await pending;
    assert.equal(oldSession.walk, oldWalk);
    assert.equal(oldSession.seed, 0);
    assert.equal(oldSession.request, null);
  }
});
