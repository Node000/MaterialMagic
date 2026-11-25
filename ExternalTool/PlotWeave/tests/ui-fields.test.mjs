import test from 'node:test';
import assert from 'node:assert/strict';
import { createFieldWriter } from '../src/ui/fields.js';

test('字段写回作用于实体对象，而不是工程根对象', () => {
  const project = {
    name: '工程名',
    catalog: { id: 'flashlight', name: '手电筒', visual: { color: '#111111' } },
  };
  const commits = [];
  const commit = (mutator, label) => {
    commits.push(label);
    return mutator(project);
  };

  createFieldWriter(project.catalog, 'name', commit, '名称')('测试手电');
  assert.equal(project.catalog.name, '测试手电');
  assert.equal(project.name, '工程名');

  createFieldWriter(project.catalog, 'visual.color', commit, '颜色')('#abcdef');
  assert.equal(project.catalog.visual.color, '#abcdef');
  assert.equal(project.visual, undefined);
  assert.deepEqual(commits, ['名称', '颜色']);
});

test('字段写回支持数组索引路径', () => {
  const entity = { tags: ['旧标签', '保留'] };
  const commit = (mutator) => mutator(entity);
  createFieldWriter(entity, 'tags.0', commit, '标签')('新标签');
  assert.deepEqual(entity.tags, ['新标签', '保留']);
});
