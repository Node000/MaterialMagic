#!/usr/bin/env node
/**
 * Node 侧最小示例：不启动浏览器，直接载入内置工程、生成地图并导出 Bundle。
 *
 * 用法：
 *   node tools/export-example.mjs [输出目录]
 *
 * 默认写入 examples/example-bundle.json 与 examples/standard-map.json。
 */

import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createDefaultProject } from '../src/core/project.js';
import { generateMap } from '../src/core/generator.js';
import { createBundle, serializeBundle } from '../src/core/serialization.js';
import { projectFingerprint, validateProject } from '../src/core/project.js';
import { validateMap } from '../src/core/validate-map.js';

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const outDir = resolve(process.argv[2] || resolve(ROOT, 'examples'));
const project = createDefaultProject();
const projectCheck = validateProject(project);
if (!projectCheck.ok) {
  console.error('内置工程校验失败：');
  for (const issue of projectCheck.errors) console.error(`- ${issue.message}`);
  process.exit(1);
}

const map = generateMap(project, { profileId: 'standard', seed: 'EXAMPLE-0001' });
const mapCheck = validateMap(project, map, { profile: project.profiles[0] });
if (mapCheck.errors.length > 0) {
  console.error('示例地图校验失败：');
  for (const issue of mapCheck.errors) console.error(`- ${issue.message}`);
  process.exit(1);
}

mkdirSync(outDir, { recursive: true });
writeFileSync(resolve(outDir, 'example-bundle.json'), serializeBundle(project, map), 'utf8');
writeFileSync(resolve(outDir, 'standard-map.json'), JSON.stringify(map, null, 2), 'utf8');
writeFileSync(resolve(outDir, 'project.json'), JSON.stringify(project, null, 2), 'utf8');

const bundle = createBundle(project, map);
console.log(`已导出到 ${outDir}`);
console.log(`  工程：${project.meta.name} · ${projectFingerprint(project)}`);
console.log(`  地块：${project.regions.length} 区域 / ${project.plotKinds.length} 种地块`);
console.log(`  地图：${map.seed} · ${map.plots.length} 块 · ${map.links.length} 连接`);
console.log(`  bundle：${bundle.kind} v${bundle.schemaVersion}`);
