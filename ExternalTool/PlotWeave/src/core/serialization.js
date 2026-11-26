/**
 * 序列化层：工程与「工程 + 地图」bundle 的导入 / 导出。
 *
 * 这一层只负责文本与对象之间的转换，不接触 DOM，也不写文件。
 * UI、CLI、测试可以复用同一套校验结果。
 */

import { APP_ID, SCHEMA_VERSION } from './constants.js';
import {
  migrateProject,
  normalizeProject,
  projectFingerprint,
  validateProject,
} from './project.js';
import { validateMap } from './validate-map.js';
import { templateToFile, validateTemplateFamilies } from './strip-templates.js';

function projectToFile(project) {
  if (!project.dungeon?.stripTemplates) return project;
  const [issue] = validateTemplateFamilies(project.dungeon.stripTemplates);
  if (issue) throw Error(issue.message);
  return { ...project, dungeon: { ...project.dungeon, stripTemplates: project.dungeon.stripTemplates.map(templateToFile) } };
}

function statusFromProject(project, extra = {}) {
  const validation = validateProject(project);
  return {
    ok: validation.ok,
    project,
    errors: validation.errors,
    warnings: validation.warnings,
    fingerprint: projectFingerprint(project),
    migrated: false,
    from: project.schemaVersion,
    to: project.schemaVersion,
    ...extra,
  };
}

function looksLikeMapDocument(value) {
  return Boolean(
    value &&
      typeof value === 'object' &&
      !Array.isArray(value) &&
      !value.project &&
      !value.regions &&
      Array.isArray(value.plots) &&
      Array.isArray(value.links),
  );
}

/** 把工程校验 issue 或地图字符串统一成可读的一行。 */
export function formatImportIssue(issue) {
  if (issue == null) return '未知错误';
  if (typeof issue === 'string') return issue;
  if (typeof issue !== 'object') return String(issue);
  const path = typeof issue.path === 'string' && issue.path ? `${issue.path}：` : '';
  const message = issue.message || issue.code || JSON.stringify(issue);
  return `${path}${message}`;
}

export function formatImportErrors(result) {
  const list = Array.isArray(result?.errors) ? result.errors : [];
  return list.length ? list.map(formatImportIssue).join('\n') : '未知错误';
}

export function serializeProject(project, { pretty = true } = {}) {
  return JSON.stringify(projectToFile(project), null, pretty ? 2 : 0);
}

/**
 * 解析工程 JSON。也接受 bundle（含 project 字段）。
 * 永不抛异常：解析 / 校验失败通过 ok:false 与 errors 返回。
 */
export function parseProject(text) {
  let raw;
  try {
    raw = JSON.parse(String(text ?? ''));
  } catch (error) {
    return {
      ok: false,
      project: null,
      errors: [`JSON 解析失败：${error.message}`],
      warnings: [],
      migrated: false,
      from: 0,
      to: 0,
    };
  }

  const candidate = raw && typeof raw === 'object' && raw.project ? raw.project : raw;
  if (!candidate || typeof candidate !== 'object' || Array.isArray(candidate)) {
    return {
      ok: false,
      project: null,
      errors: ['文件内容不是 PlotWeave 工程对象。'],
      warnings: [],
      migrated: false,
      from: 0,
      to: 0,
    };
  }

  if (candidate.app && candidate.app !== APP_ID) {
    return {
      ok: false,
      project: null,
      errors: [`这不是 PlotWeave 工程（app = ${candidate.app}）。`],
      warnings: [],
      migrated: false,
      from: 0,
      to: 0,
    };
  }

  const { project, migrated, from, to } = migrateProject(candidate);
  const validation = validateProject(project);
  return {
    ok: validation.ok,
    // A malformed family cannot be installed by the bundle's project-only recovery action.
    project: validation.errors.some((issue) => issue.code.startsWith('template.family.')) ? null : project,
    errors: validation.errors,
    warnings: validation.warnings,
    migrated,
    from,
    to,
    fingerprint: projectFingerprint(project),
  };
}

export function createBundle(project, map, extra = {}) {
  return {
    app: APP_ID,
    kind: 'plotweave-bundle',
    schemaVersion: SCHEMA_VERSION,
    exportedAt: new Date().toISOString(),
    projectFingerprint: projectFingerprint(project),
    project,
    map: map || null,
    ...extra,
  };
}

export function serializeBundle(project, map, { pretty = true } = {}) {
  return JSON.stringify({ ...createBundle(project, map), project: projectToFile(project) }, null, pretty ? 2 : 0);
}

/**
 * 解析 bundle 或裸工程。返回 { ok, project, map, errors, warnings }。
 * 地图存在时会按工程重新校验，但不会拒绝导入（地图错误作为 errors 返回，
 * 由 UI 决定是否继续载入）。
 */
export function parseBundle(text) {
  let raw;
  try {
    raw = JSON.parse(String(text ?? ''));
  } catch (error) {
    return { ok: false, project: null, map: null, errors: [`JSON 解析失败：${error.message}`], warnings: [] };
  }

  if (looksLikeMapDocument(raw)) {
    return {
      ok: false,
      project: null,
      map: null,
      errors: ['这是「当前地图 JSON」，不是工程或 Bundle。请导出并导入「工程 + 当前地图 Bundle」，或导入「工程 JSON」。'],
      warnings: [],
    };
  }

  const projectPart = raw && typeof raw === 'object' && raw.project ? raw.project : raw;
  const parsed = parseProject(JSON.stringify(projectPart));
  if (!parsed.project) {
    return { ...parsed, map: null };
  }

  let map = raw && typeof raw === 'object' && raw.map ? raw.map : null;
  const warnings = [...parsed.warnings];
  const projectErrors = [...parsed.errors];
  const mapErrors = [];
  const errors = [...projectErrors];

  if (map && typeof map === 'object') {
    const mapValidation = validateMap(parsed.project, map);
    map.stats = mapValidation.stats;
    map.issues = mapValidation.issues;
    map.warnings = mapValidation.issues.filter((issue) => issue.level === 'warning');
    map.errors = mapValidation.issues.filter((issue) => issue.level === 'error');
    for (const issue of mapValidation.issues) {
      if (issue.level === 'error') {
        const message = `地图：${issue.message}`;
        mapErrors.push(message);
        errors.push(message);
      } else warnings.push(`地图：${issue.message}`);
    }
  } else {
    map = null;
  }

  return {
    ok: errors.length === 0,
    project: parsed.project,
    map,
    errors,
    projectErrors,
    mapErrors,
    warnings,
    migrated: parsed.migrated,
    from: parsed.from,
    to: parsed.to,
    fingerprint: parsed.fingerprint,
  };
}

export function normalizeImportedProject(raw) {
  return statusFromProject(normalizeProject(raw));
}

/** 给 UI / CLI 用的一句话摘要。 */
export function describeImport(result) {
  if (!result?.project) return '未能读取工程。';
  const bits = [`${result.project.meta?.name || '未命名工程'}`];
  if (result.migrated) bits.push(`已从 schema ${result.from} 迁移到 ${result.to}`);
  if (result.errors?.length) bits.push(`${result.errors.length} 个错误`);
  else bits.push('校验通过');
  return bits.join(' · ');
}
