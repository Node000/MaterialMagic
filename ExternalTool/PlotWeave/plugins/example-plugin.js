/**
 * PlotWeave 扩展示例。
 *
 * 直接把本文件作为 ES Module 引入即可；应用也支持用
 * `index.html?plugin=example` 按需加载本文件。
 *
 * 这里演示四类扩展点：
 *   1. 生成器：包装标准生成器，让方案页出现第二个可选生成器；
 *   2. 画布图层：只在导出 PNG 时给每块地标注编号；
 *   3. 导出器：导出一份纯文本地块清单；
 *   4. 校验器：子房间数量超过地块种类上限时给出警告。
 */

import { registry } from '../src/core/registry.js';
import { generateMap } from '../src/core/generator.js';
import { plotCenter } from '../src/core/geometry.js';
import { CONTENT_TYPES } from '../src/core/constants.js';

registry.registerGenerator(
  'example-wrapped',
  {
    label: '示例 · 标准生成器包装',
    version: 1,
    description: '演示插件如何注册生成器；实际生成仍委托给标准生成器。',
  },
  (project, options = {}) => generateMap(project, options),
);

registry.registerCanvasLayer('example-plot-ids', {
  label: '示例 · 导出地块编号',
  order: 40,
  draw(ctx, { map, mapX, mapY, scale, exportMode }) {
    if (!exportMode || !map?.plots?.length) return;
    const fontSize = Math.max(9, scale * 0.24);
    ctx.save();
    ctx.font = `600 ${fontSize}px "Cascadia Mono", Consolas, monospace`;
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    for (const plot of map.plots) {
      const center = plotCenter(plot);
      const x = mapX + center.x * scale;
      const y = mapY + center.y * scale;
      const width = ctx.measureText(plot.id).width + fontSize * 0.8;
      ctx.fillStyle = 'rgba(7, 11, 16, 0.78)';
      ctx.fillRect(x - width / 2, y - fontSize * 0.75, width, fontSize * 1.5);
      ctx.fillStyle = '#e8edf3';
      ctx.fillText(plot.id, x, y);
    }
    ctx.restore();
  },
});

registry.registerExporter('example-survey', {
  label: '示例 · 地块清单',
  description: '按区域列出地块、连接与子房间的纯文本清单。',
  extension: 'txt',
  mime: 'text/plain',
  export({ project, map }) {
    if (!map) return '当前没有地图。\n';
    const lines = [
      `PlotWeave 地图清单 · ${map.seed}`,
      `工程：${project.meta.name} · ${project.meta.author || '未署名'}`,
      `地块 ${map.plots.length} · 连接 ${map.links.length} · 子房间 ${
        map.plots.reduce((sum, plot) => sum + (plot.subRooms?.length || 0), 0)
      }`,
      '',
    ];
    for (const region of project.regions) {
      const plots = map.plots.filter((plot) => plot.regionId === region.id);
      if (!plots.length) continue;
      lines.push(`## ${region.name} (${region.id}) · ${plots.length} 块`);
      for (const plot of plots) {
        const kind = project.plotKinds.find((item) => item.id === plot.kindId);
        const links = map.links.filter(
          (link) => link.a.plotId === plot.id || link.b.plotId === plot.id,
        );
        const rooms = (plot.subRooms || []).map((room) => room.kindId).join(', ') || '无';
        lines.push(
          `- ${plot.id} ${kind?.name || plot.kindId} @ (${plot.origin.x},${plot.origin.y}) ` +
            `${plot.axis === 'h' ? '横' : '纵'}${plot.length} · 连接 ${links.length} · 子房间 ${rooms}`,
        );
        for (const contentType of CONTENT_TYPES) {
          const ids = plot.contents?.[contentType.id] || [];
          if (ids.length) lines.push(`    ${contentType.label}: ${ids.join(', ')}`);
        }
      }
      lines.push('');
    }
    return lines.join('\n');
  },
});

registry.registerValidator('example.subroom-cap', 'map', (map, context = {}) => {
  if (!map || !context.project) return [];
  const issues = [];
  for (const plot of map.plots || []) {
    const kind = context.project.plotKinds.find((item) => item.id === plot.kindId);
    const max = kind?.subRooms?.max ?? 0;
    const count = plot.subRooms?.length || 0;
    if (kind && count > max) {
      issues.push({
        level: 'warning',
        code: 'example.subroom-cap',
        message: `地块 ${plot.id} 有 ${count} 个子房间，超过「${kind.name}」上限 ${max}。`,
      });
    }
  }
  return issues;
});
