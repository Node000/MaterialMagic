import assert from 'node:assert/strict';
import { mkdir, mkdtemp, readdir, readFile, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { setTimeout as delay } from 'node:timers/promises';
import test from 'node:test';
import { defaultMapPools } from '../src/core/map-pools.js';
import { createMapPoolApi } from '../tools/map-pool-api.mjs';

test('exploration API reuses preview generation without returning global metrics or authorizing publication', async () => {
  const projectRoot = await mkdtemp(join(tmpdir(), 'plotweave-exploration-test-'));
  try {
    const maps = join(projectRoot, 'Assets', 'Resources', 'Data', 'Maps');
    const patterns = join(maps, 'Patterns');
    await mkdir(patterns, { recursive: true });
    await writeFile(join(patterns, 'Sample.patterns.json'), '{}');
    const spool = join(projectRoot, 'Library', 'Locus', 'tmp', 'plotweave-map-pools');
    const host = '127.0.0.1', port = 4173;
    const api = createMapPoolApi({ projectRoot, host, port });
    const config = defaultMapPools();
    async function call(route, body) {
      const request = {
        url: `/api/map-pools/${route}`, method: 'POST', socket: { remoteAddress: host },
        headers: { host: `${host}:${port}`, origin: `http://${host}:${port}`, 'content-type': 'application/json',
          'x-plotweave-session': '33333333-3333-4333-8333-333333333333' },
        async *[Symbol.asyncIterator]() { yield Buffer.from(JSON.stringify(body)); },
      };
      const response = { destroyed: false, writeHead(status) { this.status = status; }, end(text) { this.body = JSON.parse(text); } };
      await api(request, response);
      return response;
    }
    const args = { config, poolId: config.pools[0].id, entryId: config.pools[0].entries[0].id, seed: 42 };
    const floor = { kind: 'plotweave-floor-map', schemaVersion: 1, id: 'mock', spawn: { x: 0, y: 0 },
      cells: [{ x: 0, y: 0, type: 'spawn' }], edges: [] };
    const explore = call('explore', args);
    let requestFile;
    // Simulate the generator bridge entirely in a temporary project, without touching Unity.
    const deadline = Date.now() + 3000;
    while (!requestFile && Date.now() < deadline) {
      const files = await readdir(spool).catch((error) => { if (error.code === 'ENOENT') return []; throw error; });
      requestFile = files.find((file) => file.endsWith('.request.json'));
      if (!requestFile) await delay(15);
    }
    assert.ok(requestFile, 'exploration queued a generation request');
    const request = JSON.parse(await readFile(join(spool, requestFile), 'utf8'));
    assert.equal(request.seed, args.seed);
    assert.equal(request.entryId, args.entryId);
    const answer = JSON.stringify({ floor: { ...floor, bounds: { width: 100 } }, metrics: { resource: 1000 }, diagnostics: {} });
    await writeFile(join(spool, requestFile.replace('.request.json', '.response.json')), answer);
    const reply = await explore;
    assert.equal(reply.status, 200);
    assert.deepEqual(reply.body, { floor });
    const publish = await call('publish', { config, revision: null });
    assert.equal(publish.status, 409, 'same session is not approved by exploration');
    assert.deepEqual(publish.body.details, [`${args.poolId} / ${args.entryId}`]);
    assert.deepEqual(await readdir(maps), ['Patterns']);
  } finally {
    await rm(projectRoot, { recursive: true, force: true });
  }
});
