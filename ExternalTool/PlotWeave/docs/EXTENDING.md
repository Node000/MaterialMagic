# 扩展指南

PlotWeave 的扩展都通过 `src/core/registry.js` 的单例 `registry` 完成。插件不修改核心源码，也不依赖 UI 内部结构。

## 1. 载入插件

把文件放到：

```text
PlotWeave/plugins/<id>-plugin.js
```

然后在 URL 上按需载入：

```text
http://127.0.0.1:4173/?plugin=<id>
```

例如：

```text
http://127.0.0.1:4173/?plugin=example
```

应用会执行：

```js
import(`../../plugins/${id}-plugin.js`);
```

加载成功后重绘画布并刷新侧栏/检视器。插件必须是浏览器可执行的原生 ES Module，不能使用 Node API。

也可以在自己的入口里静态引入：

```js
import './plugins/example-plugin.js';
```

## 2. 生成器

```js
import { registry } from '../src/core/registry.js';
import { generateMap } from '../src/core/generator.js';

registry.registerGenerator(
  'my-generator',
  {
    label: '我的生成器',
    version: 1,
    description: '在标准生成器之上做一次后处理。',
  },
  (project, options = {}) => {
    const map = generateMap(project, options);
    // 在这里追加/调整地块，但必须保持：
    // - 每块地 cells 与 origin/axis/length 一致
    // - link 合法、连通、度数不超上限
    // - spawn 合法
    return map;
  },
);
```

契约：

- 入参：`(project, { profile, seed, attempts?, createdAt? })`；
- 返回：完整地图对象（见 `docs/SCHEMA.md`）；
- 推荐在返回前调用 `computeMapStats(map)` 与 `validateMap(project, map)`；
- 不要使用 `Math.random`，所有随机性应来自 `new RNG(seed)`；
- 生成器通过 `profile.generator` 被选择，方案页下拉会自动出现。

注册后，在「方案」检视器里把「生成器」改成 `my-generator`，再生成即可。

## 3. 画布图层

```js
registry.registerCanvasLayer('my-overlay', {
  label: '我的叠加层',
  order: 40,
  draw(ctx, frame) {
    const { map, project, mapX, mapY, scale, selection, ui, exportMode } = frame;
    // 屏幕坐标 = mapX + worldX * scale
    // 世界坐标以格为单位
  },
});
```

- `order` 越小越先绘制；内置图层之后才执行扩展图层。
- `frame` 提供 `map`、`project`、`view`、`mapX`、`mapY`、`scale`、`selection`、`ui`、`exportMode`。
- `exportMode` 为真时是导出 PNG 的离屏重放，可以隐藏交互提示或显示编号。
- 画布每次重绘都会调用所有已注册图层；避免在这里做重计算。

示例插件的 `example-plot-ids` 只在导出时给每块地标注 `p1 / p2 ...`。

## 4. 导出器

```js
registry.registerExporter('my-survey', {
  label: '我的清单',
  description: '纯文本导出。',
  extension: 'txt',
  mime: 'text/plain',
  export({ project, map, store }) {
    return `seed: ${map?.seed}\nplots: ${map?.plots.length}`;
  },
});
```

- 返回值可以是字符串或 `Blob`；返回 `null` 表示取消。
- 字符串会用 `downloadText` 下载，`Blob` 会用 `downloadBlob` 下载。
- 注册后会出现在「导出」弹窗中，排在四个内置选项之后。
- `map` 可能为 `null`（当前没有地图），导出器应自行处理。

## 5. 校验器

```js
registry.registerValidator('my.rule', 'map', (map, context) => {
  if (!map) return [];
  return (map.plots || [])
    .filter((plot) => plot.length > 5)
    .map((plot) => ({
      level: 'error',          // error | warning | info
      code: 'my.rule.too-long',
      message: `地块 ${plot.id} 超过 5 格。`,
    }));
});
```

- `scope`：`project`、`map` 或 `any`。
- 返回单个 issue、issue 数组，或 `null`。
- 校验器抛异常不会中断应用；会被转换成一条 error issue。
- 扩展校验器在 `AppStore.validate()` 中执行，与内置校验结果合并。

## 6. 内容类型

五类内容（`props` / `relics` / `events` / `enemies` / `npcs`）是固定的数据槽位。它们只需要目录条目与 ID 引用，不参与生成算法。若玩法层需要更多字段，建议：

1. 在 `catalog` 条目上附加字段（规范化会保留未知字段）；
2. 或使用工程的 `extensions` 对象；
3. 不要把玩法运行时对象塞进地图 JSON。

## 7. 插件约定

- 文件名必须是 `<id>-plugin.js`，`id` 只能包含小写字母、数字和连字符。
- 插件应自行处理重复注册：注册表以 ID 覆盖，重复载入会替换同 ID 项。
- 不要在插件顶层读取 DOM；应用会在模块加载后调用它。
- 不要在插件里替换 `window.PlotWeave.store`；通过事件或公开的 `store` 方法协作。
- 插件失败会被 `console.error` 捕获并弹出 toast，不影响主应用启动。

## 8. 测试插件

```js
import { registry } from '../src/core/registry.js';
import '../plugins/my-plugin.js';

const exporter = registry.getExporter('my-survey');
const text = exporter.export({ project, map });
```

参见 `tests/extensions.test.mjs`：它验证标准生成器已注册、插件导出器输出、插件校验器接入扩展校验链。
