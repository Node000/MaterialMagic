/**
 * 可复现的种子随机数。
 *
 * 使用 xmur3 把任意字符串散列成 32 位种子，再用 mulberry32 产生序列。
 * 生成器内不调用 Math.random，因此同一份工程 + 同一个种子必然得到同一张地图。
 */

function xmur3(str) {
  let h = 1779033703 ^ str.length;
  for (let i = 0; i < str.length; i += 1) {
    h = Math.imul(h ^ str.charCodeAt(i), 3432918353);
    h = (h << 13) | (h >>> 19);
  }
  return function nextHash() {
    h = Math.imul(h ^ (h >>> 16), 2246822507);
    h = Math.imul(h ^ (h >>> 13), 3266489909);
    h ^= h >>> 16;
    return h >>> 0;
  };
}

function mulberry32(a) {
  return function nextFloat() {
    a |= 0;
    a = (a + 0x6d2b79f5) | 0;
    let t = Math.imul(a ^ (a >>> 15), 1 | a);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

/** 把用户输入规范成字符串种子；空值回退到固定种子。 */
export function normalizeSeed(seed) {
  if (seed === undefined || seed === null) return 'PLOTWEAVE';
  const text = String(seed).trim();
  return text.length ? text : 'PLOTWEAVE';
}

export class RNG {
  constructor(seed = 'PLOTWEAVE') {
    this.seed = normalizeSeed(seed);
    const hashed = xmur3(this.seed);
    this._next = mulberry32(hashed());
    this._calls = 0;
  }

  /** [0, 1) */
  float() {
    this._calls += 1;
    return this._next();
  }

  /** 闭区间整数 */
  int(min, max) {
    const lo = Math.ceil(Math.min(min, max));
    const hi = Math.floor(Math.max(min, max));
    if (hi < lo) return lo;
    return lo + Math.floor(this.float() * (hi - lo + 1));
  }

  chance(probability) {
    return this.float() < probability;
  }

  pick(items) {
    if (!Array.isArray(items) || items.length === 0) return undefined;
    return items[Math.floor(this.float() * items.length)];
  }

  /**
   * 按权重抽取。weightFn 返回负数 / NaN 时按 0 处理。
   * 全部权重为 0 时退化为均匀抽取。
   */
  weighted(items, weightFn = (item) => item?.weight ?? 1) {
    if (!Array.isArray(items) || items.length === 0) return undefined;
    const weights = items.map((item) => {
      const value = Number(weightFn(item));
      return Number.isFinite(value) && value > 0 ? value : 0;
    });
    const total = weights.reduce((sum, value) => sum + value, 0);
    if (total <= 0) return this.pick(items);
    let cursor = this.float() * total;
    for (let i = 0; i < items.length; i += 1) {
      cursor -= weights[i];
      if (cursor < 0) return items[i];
    }
    return items[items.length - 1];
  }

  /** 返回打乱后的新数组，不修改输入。 */
  shuffle(items) {
    const result = Array.isArray(items) ? items.slice() : [];
    for (let i = result.length - 1; i > 0; i -= 1) {
      const j = Math.floor(this.float() * (i + 1));
      [result[i], result[j]] = [result[j], result[i]];
    }
    return result;
  }

  /** 不重复地按权重抽取 count 个。不足时返回所有可用项。 */
  sample(items, count, weightFn = (item) => item?.weight ?? 1) {
    const pool = Array.isArray(items) ? items.slice() : [];
    const result = [];
    const target = Math.max(0, Math.min(count, pool.length));
    while (result.length < target && pool.length) {
      const item = this.weighted(pool, weightFn);
      const index = pool.indexOf(item);
      if (index >= 0) pool.splice(index, 1);
      result.push(item);
    }
    return result;
  }

  /** 给测试 / 调试用的调用次数。 */
  get calls() {
    return this._calls;
  }
}

/** 生成一个适合展示给人看的随机种子。 */
export function randomSeed() {
  const alphabet = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
  let text = '';
  const cryptoObj = globalThis.crypto;
  if (cryptoObj?.getRandomValues) {
    const bytes = new Uint8Array(8);
    cryptoObj.getRandomValues(bytes);
    for (const byte of bytes) text += alphabet[byte % alphabet.length];
  } else {
    for (let i = 0; i < 8; i += 1) text += alphabet[Math.floor(Math.random() * alphabet.length)];
  }
  return `PW-${text.slice(0, 4)}-${text.slice(4)}`;
}
