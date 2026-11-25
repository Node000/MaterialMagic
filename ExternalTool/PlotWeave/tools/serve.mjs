#!/usr/bin/env node
/**
 * 零依赖静态服务器。
 *
 * 用法：
 *   node tools/serve.mjs            # http://127.0.0.1:4173
 *   node tools/serve.mjs --port 8080 --open
 *
 * 只服务 PlotWeave 目录内的文件；不做目录列表，不做构建，不写磁盘。
 */

import http from 'node:http';
import { createReadStream, statSync } from 'node:fs';
import { dirname, extname, join, normalize, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';
import { exec } from 'node:child_process';
import { networkInterfaces } from 'node:os';
import { createMapPoolApi } from './map-pool-api.mjs';

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const MIME = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.mjs': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.json': 'application/json; charset=utf-8',
  '.svg': 'image/svg+xml',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.jpeg': 'image/jpeg',
  '.webp': 'image/webp',
  '.ico': 'image/x-icon',
  '.woff2': 'font/woff2',
  '.txt': 'text/plain; charset=utf-8',
  '.md': 'text/markdown; charset=utf-8',
};

function parseArgs(argv) {
  const options = { port: 4173, host: '127.0.0.1', open: false };
  for (let index = 0; index < argv.length; index += 1) {
    const arg = argv[index];
    if (arg === '--open') options.open = true;
    else if (arg === '--port' || arg === '-p') options.port = Number(argv[++index]);
    else if (arg === '--host') options.host = argv[++index];
    else if (arg === '--help' || arg === '-h') options.help = true;
  }
  if (!Number.isFinite(options.port) || options.port <= 0) options.port = 4173;
  return options;
}

function openBrowser(url) {
  const command =
    process.platform === 'win32'
      ? `start "" "${url}"`
      : process.platform === 'darwin'
        ? `open "${url}"`
        : `xdg-open "${url}"`;
  exec(command, (error) => {
    if (error) console.warn(`[PlotWeave] 无法自动打开浏览器：${error.message}`);
  });
}

function lanAddresses() {
  const found = [];
  for (const list of Object.values(networkInterfaces())) {
    for (const item of list || []) {
      if (item.family === 'IPv4' && !item.internal) found.push(item.address);
    }
  }
  return [...new Set(found)];
}

function resolvePath(urlPath) {
  let pathname;
  try {
    pathname = decodeURIComponent(new URL(urlPath, 'http://localhost').pathname);
  } catch {
    return null;
  }
  if (pathname === '/' || pathname === '') pathname = '/index.html';
  const candidate = normalize(join(ROOT, pathname));
  if (candidate !== ROOT && !candidate.startsWith(ROOT + sep)) return null;
  try {
    const stat = statSync(candidate);
    if (stat.isDirectory()) return resolvePath(join(pathname, 'index.html'));
    return candidate;
  } catch {
    return null;
  }
}

const options = parseArgs(process.argv.slice(2));
if (options.help) {
  console.log(`PlotWeave 静态服务器

  node tools/serve.mjs [--port 4173] [--host 127.0.0.1] [--open]
  npm run mobile                 # 监听 0.0.0.0，供同一局域网手机访问

工程目录：${ROOT}`);
  process.exit(0);
}

const mapPoolApi = createMapPoolApi({ projectRoot: resolve(ROOT, '..', '..'), ...options });
const server = http.createServer(async (request, response) => {
  if (await mapPoolApi(request, response)) return;
  if (request.method !== 'GET' && request.method !== 'HEAD') {
    response.writeHead(405, { allow: 'GET, HEAD' });
    response.end('Method Not Allowed');
    return;
  }
  const file = resolvePath(request.url || '/');
  if (!file) {
    response.writeHead(404, { 'content-type': 'text/plain; charset=utf-8' });
    response.end('404 Not Found');
    return;
  }
  const ext = extname(file).toLowerCase();
  const type = MIME[ext] || 'application/octet-stream';
  response.writeHead(200, {
    'content-type': type,
    'cache-control': 'no-store',
    'x-content-type-options': 'nosniff',
  });
  if (request.method === 'HEAD') {
    response.end();
    return;
  }
  const stream = createReadStream(file);
  stream.on('error', (error) => {
    console.error(`[PlotWeave] 读取失败 ${file}: ${error.message}`);
    if (!response.headersSent) response.writeHead(500);
    response.end('Internal Server Error');
  });
  stream.pipe(response);
});

server.listen(options.port, options.host, () => {
  const localUrl = `http://127.0.0.1:${options.port}/`;
  const urls = [localUrl];
  if (options.host === '0.0.0.0' || options.host === '::') {
    for (const address of lanAddresses()) urls.push(`http://${address}:${options.port}/`);
  } else if (options.host !== '127.0.0.1' && options.host !== 'localhost') {
    urls[0] = `http://${options.host}:${options.port}/`;
  }
  console.log(`\n  PlotWeave 已启动\n  ${urls.join('\n  ')}\n  Ctrl+C 停止\n`);
  if (options.open) openBrowser(localUrl);
});

server.on('error', (error) => {
  if (error.code === 'EADDRINUSE') {
    console.error(`[PlotWeave] 端口 ${options.port} 已被占用，换一个：node tools/serve.mjs --port 4174`);
  } else {
    console.error(`[PlotWeave] 服务器错误：${error.message}`);
  }
  process.exitCode = 1;
});
