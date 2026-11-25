/**
 * 可扩展注册表。
 *
 * PlotWeave 的核心生成器、画布叠加层、导出器、校验器都从这里查表。
 * 外部插件只需要拿到 registry，注册一个 id 与回调即可，不必改 UI 源码。
 */

export class ExtensionRegistry {
  constructor() {
    this.generators = new Map();
    this.canvasLayers = new Map();
    this.exporters = new Map();
    this.validators = new Map();
  }

  registerGenerator(id, meta, generate) {
    if (!id || typeof generate !== 'function') {
      throw new Error('registerGenerator(id, meta, generate) 需要一个 id 和生成函数');
    }
    this.generators.set(id, {
      id,
      label: meta?.label ?? id,
      description: meta?.description ?? '',
      version: meta?.version ?? null,
      generate,
      meta: meta ?? {},
    });
    return this;
  }

  getGenerator(id) {
    return this.generators.get(id) ?? null;
  }

  listGenerators() {
    return [...this.generators.values()].map(({ id, label, description, version }) => ({
      id,
      label,
      description,
      version,
    }));
  }

  /** 画布叠加层：draw(frame) 在基础地图、选中态之后调用。 */
  registerCanvasLayer(id, layer) {
    if (!id || typeof layer?.draw !== 'function') {
      throw new Error('registerCanvasLayer(id, { draw }) 需要一个 id 和 draw');
    }
    this.canvasLayers.set(id, {
      id,
      label: layer.label ?? id,
      order: Number(layer.order) || 0,
      draw: layer.draw,
      meta: layer.meta ?? {},
    });
    return this;
  }

  listCanvasLayers() {
    return [...this.canvasLayers.values()].sort((a, b) => a.order - b.order);
  }

  registerExporter(id, exporter) {
    if (!id || typeof exporter?.export !== 'function') {
      throw new Error('registerExporter(id, { export }) 需要一个 id 和 export');
    }
    this.exporters.set(id, {
      id,
      label: exporter.label ?? id,
      description: exporter.description ?? '',
      extension: exporter.extension ?? 'bin',
      mime: exporter.mime ?? 'application/octet-stream',
      export: exporter.export,
      meta: exporter.meta ?? {},
    });
    return this;
  }

  getExporter(id) {
    return this.exporters.get(id) ?? null;
  }

  listExporters() {
    return [...this.exporters.values()].map(({ id, label, description, extension, mime }) => ({
      id,
      label,
      description,
      extension,
      mime,
    }));
  }

  /** 额外的校验器：scope 为 "project" 或 "map"。 */
  registerValidator(id, scope, validate) {
    if (!id || typeof validate !== 'function') {
      throw new Error('registerValidator(id, scope, validate) 需要一个 id 和校验函数');
    }
    this.validators.set(id, { id, scope, validate });
    return this;
  }

  runValidators(scope, payload, context = {}) {
    const results = [];
    for (const validator of this.validators.values()) {
      if (validator.scope !== scope && validator.scope !== 'any') continue;
      try {
        const result = validator.validate(payload, context);
        if (Array.isArray(result)) results.push(...result);
        else if (result) results.push(result);
      } catch (error) {
        results.push({
          level: 'error',
          code: `extension:${validator.id}`,
          message: `扩展校验器 ${validator.id} 抛出异常：${error.message}`,
        });
      }
    }
    return results;
  }
}

export const registry = new ExtensionRegistry();
