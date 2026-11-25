// Desktop Chrome/Edge secure contexts. Remember the handle, never an absolute local path.
const DB = 'plotweave-directory';
const STORE = 'handles';

async function accessHandle(mode, handle) {
  const db = await new Promise((resolve, reject) => {
    const req = indexedDB.open(DB, 1);
    req.onupgradeneeded = () => req.result.createObjectStore(STORE);
    req.onsuccess = () => resolve(req.result);
    req.onerror = () => reject(req.error);
  });
  try {
    return await new Promise((resolve, reject) => {
      const tx = db.transaction(STORE, mode);
      const store = tx.objectStore(STORE);
      const req = mode === 'readonly' ? store.get('active') : store.put(handle, 'active');
      tx.oncomplete = () => resolve(req.result);
      tx.onerror = () => reject(tx.error);
      tx.onabort = () => reject(tx.error);
    });
  } finally { db.close(); }
}

export const PROJECT_FILE = 'project.json';
export const PREVIEW_FILE = 'Preview.floor.json';

export class WorkDirectory {
  handle = null;
  baseline = new Map();
  get available() { return typeof window.showDirectoryPicker === 'function'; }

  async restore() {
    if (!this.available || this.handle) return;
    const handle = await accessHandle('readonly').catch(() => null);
    if (!this.handle && handle) this.handle = handle;
  }

  async choose() {
    const handle = await window.showDirectoryPicker({ id: 'plotweave-patterns', mode: 'readwrite' });
    this.handle = handle;
    this.baseline.clear();
    await accessHandle('readwrite', handle).catch(() => {});
  }

  async authorize() {
    if (!this.handle) throw Error('请先选择 Patterns 工作目录。');
    if (await this.handle.requestPermission({ mode: 'readwrite' }) !== 'granted') throw Error('未授权目录读写。');
  }

  async read(name) {
    try {
      return await (await (await this.handle.getFileHandle(name)).getFile()).text();
    } catch (error) {
      if (error.name === 'NotFoundError') return null;
      throw error;
    }
  }

  async checkWrite(name) {
    const current = await this.read(name);
    if (current !== null && this.baseline.get(name) !== current) {
      throw Error(`${name} 已存在或被外部修改，请先重新读取，避免覆盖。`);
    }
    if (current === null && this.baseline.get(name) != null) throw Error(`${name} 已在外部删除，请先重新读取。`);
  }

  async write(name, text) {
    await this.checkWrite(name);
    const file = await this.handle.getFileHandle(name, { create: true });
    const stream = await file.createWritable();
    try {
      await stream.write(text);
      await stream.close();
    } catch (error) {
      await stream.abort().catch(() => {});
      throw error;
    }
    this.baseline.set(name, text);
  }
}
