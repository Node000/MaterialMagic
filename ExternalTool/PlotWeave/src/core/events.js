/** 极简事件总线。核心与 UI 之间只通过事件和纯数据通信。 */
export class Emitter {
  constructor() {
    this._listeners = new Map();
  }

  on(type, listener) {
    if (typeof listener !== 'function') return () => {};
    if (!this._listeners.has(type)) this._listeners.set(type, new Set());
    this._listeners.get(type).add(listener);
    return () => this.off(type, listener);
  }

  once(type, listener) {
    const unsubscribe = this.on(type, (payload) => {
      unsubscribe();
      listener(payload);
    });
    return unsubscribe;
  }

  off(type, listener) {
    this._listeners.get(type)?.delete(listener);
  }

  emit(type, payload) {
    const listeners = this._listeners.get(type);
    if (!listeners) return;
    for (const listener of [...listeners]) {
      try {
        listener(payload);
      } catch (error) {
        // 一个监听器出错不应阻断其他监听器。
        console.error(`[PlotWeave] listener error for "${type}"`, error);
      }
    }
  }
}
