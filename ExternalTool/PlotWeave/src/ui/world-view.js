/**
 * 模式三：完整地图生成器占位视图（按需求暂时留空）。
 */

import { WORLD_PLACEHOLDER_STATUS } from '../core/world-placeholder.js';
import { h, mount } from './dom.js';

export class WorldPlaceholderView {
  constructor({ root, store }) {
    this.root = root;
    this.store = store;
    this.render();
  }

  render() {
    mount(
      this.root,
      h(
        'div',
        { class: 'world-placeholder' },
        h(
          'div',
          { class: 'empty-card world-card' },
          h('div', { class: 'eyebrow' }, 'WORLD MAP / 完整地图生成'),
          h('h2', null, WORLD_PLACEHOLDER_STATUS.title),
          h('p', null, WORLD_PLACEHOLDER_STATUS.description),
          h(
            'div',
            { class: 'world-roadmap' },
            h('div', { class: 'world-pill' }, '预留：多层三角地牢串联'),
            h('div', { class: 'world-pill' }, '预留：类银河城能力锁（Key-Lock）'),
            h('div', { class: 'world-pill' }, '预留：大世界全局数值与路线图'),
          ),
          h(
            'div',
            { class: 'action-row', style: { marginTop: '16px' } },
            h(
              'button',
              {
                class: 'btn primary',
                type: 'button',
                onclick: () => this.store.setMode('floor'),
              },
              '前往单层地图生成',
            ),
            h(
              'button',
              {
                class: 'btn',
                type: 'button',
                onclick: () => this.store.setMode('templates'),
              },
              '前往条带模板制作',
            ),
          ),
        ),
      ),
    );
  }
}
