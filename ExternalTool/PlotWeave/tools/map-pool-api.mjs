import { createHash, randomUUID } from 'node:crypto';
import { mkdir, readFile, readdir, rename, rm, writeFile } from 'node:fs/promises';
import { join } from 'node:path';
import { setTimeout as delay } from 'node:timers/promises';
import { POOL_ID, validateMapPools } from '../src/core/map-pools.js';

const PREFIX = '/api/map-pools/';
const FILE_NAME = 'SecondFloor.map-pools.json';
const PATTERN_SUFFIX = '.patterns.json';
const MAX_BODY = 512 * 1024;
const MAX_APPROVALS = 4096;
const REQUEST_TIMEOUT = 12000;
const sha = bytes => createHash('sha256').update(bytes).digest('hex');
const requestId = () => randomUUID().replaceAll('-', '');
const ignoreMissing = async path => {
  try { return await readFile(path); } catch (error) { if (error.code !== 'ENOENT') throw error; return null; }
};
class ApiError extends Error {
  constructor(status, message, details) { super(message); this.status = status; this.details = details; }
}
function reply(response, status, payload) {
  if (response.destroyed) return;
  response.writeHead(status, { 'content-type': 'application/json; charset=utf-8', 'cache-control': 'no-store', 'x-content-type-options': 'nosniff' });
  response.end(JSON.stringify(payload));
}
async function readBody(request) {
  if (request.headers['content-type']?.split(';')[0] !== 'application/json') throw new ApiError(415, '需要 JSON 请求。');
  let length = 0;
  const chunks = [];
  for await (const chunk of request) {
    length += chunk.length;
    if (length > MAX_BODY) throw new ApiError(413, '地图池配置超过 512 KiB 上限。');
    chunks.push(chunk);
  }
  try { return JSON.parse(Buffer.concat(chunks).toString('utf8')); }
  catch { throw new ApiError(400, '地图池请求不是有效 JSON。'); }
}
function configForPreview(config) {
  const issues = validateMapPools(config);
  if (issues.length) throw new ApiError(422, '地图池配置未通过校验。', issues);
  // A saved configuration may already be pinned. A fresh preview authorizes the current bytes instead.
  return { kind: config.kind, schemaVersion: config.schemaVersion, pools: config.pools.map(p => ({
    id: p.id, name: p.name, ...(p.tier !== undefined ? { tier: p.tier } : {}), entries: p.entries.map(e => ({
      id: e.id, name: e.name, weight: e.weight, patternLibraryId: e.patternLibraryId,
      ...(e.ringSpacing !== undefined ? { ringSpacing: e.ringSpacing } : {}),
      ...(e.spawnRegionSlot !== undefined ? { spawnRegionSlot: e.spawnRegionSlot } : {}),
      ...(e.minCycleLength !== undefined ? { minCycleLength: e.minCycleLength } : {}),
      ...(e.extraLoopChance !== undefined ? { extraLoopChance: e.extraLoopChance } : {}),
      ...(e.localClustering !== undefined ? { localClustering: e.localClustering } : {}),
      regions: e.regions.map(r => ({ regionTypeId: r.regionTypeId, stripMin: r.stripMin, stripMax: r.stripMax, maxNoLoop: r.maxNoLoop })),
      budget: { resourceMin: e.budget.resourceMin, resourceMax: e.budget.resourceMax, difficultyMin: e.budget.difficultyMin, difficultyMax: e.budget.difficultyMax },
      progression: { baseLevel: e.progression.baseLevel, levelPerHop: e.progression.levelPerHop, maxLevel: e.progression.maxLevel },
    })),
  })) };
}

export function createMapPoolApi({ projectRoot, host, port }) {
  const maps = join(projectRoot, 'Assets', 'Resources', 'Data', 'Maps');
  const patterns = join(maps, 'Patterns');
  const published = join(maps, FILE_NAME);
  const spool = join(projectRoot, 'Library', 'Locus', 'tmp', 'plotweave-map-pools');
  const approvals = new Set();
  let publishing = false;
  let previewActive = false;
  const patternHash = async id => {
    if (typeof id !== 'string' || !POOL_ID.test(id)) throw new ApiError(422, '无效 Pattern 库 ID。');
    const bytes = await ignoreMissing(join(patterns, id + PATTERN_SUFFIX));
    if (bytes === null) throw new ApiError(422, `找不到 Patterns/${id}${PATTERN_SUFFIX}，请先从条带模板页导出。`);
    return sha(bytes);
  };
  const approved = (client, poolId, entry, hash) => sha(JSON.stringify([client, poolId, entry, hash]));
  async function generate(config, poolId, entryId, seed, libraryId, hash, response) {
    if (previewActive) throw new ApiError(409, '已有预览请求在运行，请等它完成。');
    previewActive = true;
    const id = requestId();
    const input = join(spool, `${id}.request.json`);
    const claimed = join(spool, `${id}.processing.json`);
    const output = join(spool, `${id}.response.json`);
    const temp = join(spool, `${id}.tmp`);
    try {
      await mkdir(spool, { recursive: true });
      await writeFile(temp, JSON.stringify({ config, poolId, entryId, seed, patternLibraryId: libraryId, patternSha256: hash }), { flag: 'wx' });
      await rename(temp, input);
      const deadline = Date.now() + REQUEST_TIMEOUT;
      while (Date.now() < deadline && !response.destroyed) {
        const result = await ignoreMissing(output);
        if (result !== null) {
          const data = JSON.parse(result.toString('utf8'));
          if (data.error) throw new ApiError(422, data.error);
          if (data.floor?.kind !== 'plotweave-floor-map' || !data.metrics) throw new Error('Unity 返回了无效预览。');
          return data;
        }
        await delay(100);
      }
      throw new ApiError(503, 'Unity 没有响应。请打开此工程并保持在 Edit Mode，等编译完成后再试。');
    } finally {
      try { for (const file of [input, claimed, output, temp]) await rm(file, { force: true }); }
      finally { previewActive = false; }
    }
  }
  return async function route(request, response) {
    const path = request.url?.split('?')[0];
    if (!path?.startsWith(PREFIX)) return false;
    const origin = request.headers.origin;
    const expectedHost = request.headers.host;
    if (host !== '127.0.0.1' || !['127.0.0.1', '::ffff:127.0.0.1'].includes(request.socket.remoteAddress) ||
        ![`127.0.0.1:${port}`, `localhost:${port}`].includes(expectedHost) ||
        (origin && origin !== `http://${expectedHost}`) || (request.method === 'POST' && origin !== `http://${expectedHost}`)) {
      reply(response, 403, { error: '地图池预览和发布仅允许本机默认服务（npm start / npm run serve）。' });
      return true;
    }
    try {
      if (request.method === 'GET' && path === `${PREFIX}patterns`) {
        const entries = await readdir(patterns, { withFileTypes: true });
        const catalog = [];
        for (const file of entries) {
          if (!file.isFile() || !file.name.endsWith(PATTERN_SUFFIX)) continue;
          const id = file.name.slice(0, -PATTERN_SUFFIX.length);
          if (!POOL_ID.test(id)) continue;
          let data;
          try { data = JSON.parse((await readFile(join(patterns, file.name), 'utf8')).replace(/^\uFEFF/, '')); }
          catch (error) { throw new ApiError(422, `${file.name}: ${error.message}`); }
          if (data?.kind !== 'plotweave-strip-templates' || data.schemaVersion !== 3 || !Array.isArray(data.stripTemplates))
            throw new ApiError(422, `${file.name} 不是 v3 条带模板库。`);
          catalog.push({ id, regionTypeIds: [...new Set(data.stripTemplates.flatMap(t => t.regionIds || []))].sort() });
        }
        reply(response, 200, { patterns: catalog.sort((a, b) => a.id.localeCompare(b.id)) });
      } else if (request.method === 'GET' && path === `${PREFIX}published`) {
        const bytes = await ignoreMissing(published);
        reply(response, 200, { config: bytes === null ? null : JSON.parse(bytes.toString('utf8')), revision: bytes === null ? null : sha(bytes) });
      } else if (request.method === 'POST' && ['preview', 'explore', 'publish'].some((route) => path === `${PREFIX}${route}`)) {
        const client = request.headers['x-plotweave-session'];
        if (typeof client !== 'string' || !/^[0-9a-f-]{36}$/.test(client)) throw new ApiError(400, '缺少本页校验会话，请刷新 PlotWeave。');
        const body = await readBody(request);
        const config = configForPreview(body?.config);
        if (path !== `${PREFIX}publish`) {
          const { poolId, entryId, seed } = body;
          const entry = config.pools.find(p => p.id === poolId)?.entries.find(e => e.id === entryId);
          if (!entry || !Number.isInteger(seed) || seed < -2147483648 || seed > 2147483647) throw new ApiError(422, '配方不存在，或种子不是 32 位整数。');
          const hash = await patternHash(entry.patternLibraryId);
          // Unity checks the selected entry and the bytes it loaded before generating.
          entry.patternSha256 = hash;
          const data = await generate(config, poolId, entryId, seed, entry.patternLibraryId, hash, response);
          delete entry.patternSha256;
          if (hash !== await patternHash(entry.patternLibraryId)) throw new ApiError(409, '预览期间 Pattern 库变更，请重新预览。');
          // An exploration reroll is not a release preview and cannot authorize a publish.
          if (path === `${PREFIX}preview`) {
            if (approvals.size >= MAX_APPROVALS) approvals.delete(approvals.values().next().value);
            approvals.add(approved(client, poolId, entry, hash));
            reply(response, 200, data);
          } else {
            const { kind, schemaVersion, id, spawn, cells, edges } = data.floor;
            reply(response, 200, { floor: { kind, schemaVersion, id, spawn, cells, edges } });
          }
        } else {
          if (publishing) throw new ApiError(409, '已有发布请求运行。');
          publishing = true;
          try {
            const old = await ignoreMissing(published);
            if (body.revision !== (old === null ? null : sha(old))) throw new ApiError(409, '游戏配置已在外部变化；请刷新已发布状态，确认后重试。');
            const missing = [];
            for (const pool of config.pools) for (const entry of pool.entries) {
              const hash = await patternHash(entry.patternLibraryId);
              if (!approvals.has(approved(client, pool.id, entry, hash))) missing.push(`${pool.id} / ${entry.id}`);
              entry.patternSha256 = hash;
            }
            if (missing.length) throw new ApiError(409, '请先预览所有要发布的当前配方（修改或替换 Pattern 后需重验）。', missing);
            const bytes = Buffer.from(JSON.stringify(config, null, 2) + '\n');
            const temp = join(maps, `.${FILE_NAME}.${requestId()}.tmp`);
            try {
              await writeFile(temp, bytes, { flag: 'wx' });
              const current = await ignoreMissing(published);
              if (body.revision !== (current === null ? null : sha(current))) throw new ApiError(409, '游戏配置已在外部变化，未写入。');
              await rename(temp, published);
            } finally { await rm(temp, { force: true }); }
            reply(response, 200, { path: `Assets/Resources/Data/Maps/${FILE_NAME}`, revision: sha(bytes) });
          } finally { publishing = false; }
        }
      } else reply(response, 404, { error: '未知地图池 API。' });
    } catch (error) {
      if (!error.status) console.error('[PlotWeave 地图池]', error);
      reply(response, error.status || 500, { error: error.message, details: error.details });
    }
    return true;
  };
}
