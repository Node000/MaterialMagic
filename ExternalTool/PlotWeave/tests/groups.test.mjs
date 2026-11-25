import test from 'node:test';
import assert from 'node:assert/strict';

import {
  createDefaultProject,
  createPlotGroup,
  createPlotKind,
  normalizeProject,
  validateProject,
} from '../src/core/project.js';

test('折叠组规范化保留字段并给地块补 groupId', () => {
  const raw = createDefaultProject();
  raw.plotGroups = [
    {
      id: 'group_x',
      name: '入口组',
      regionId: raw.regions[0].id,
      parentGroupId: '',
      description: '入口附近',
      order: 3,
      customFlag: true,
    },
  ];
  raw.plotKinds[0].groupId = 'group_x';

  const project = normalizeProject(raw);
  assert.equal(project.plotGroups.length, 1);
  assert.equal(project.plotGroups[0].name, '入口组');
  assert.equal(project.plotGroups[0].parentGroupId, '');
  assert.equal(project.plotGroups[0].customFlag, true);
  assert.equal(project.plotKinds[0].groupId, 'group_x');
});

test('折叠组工厂生成唯一 id，支持同区域嵌套，地块只能进同区域的组', () => {
  const project = createDefaultProject();
  const region = project.regions[0];
  const otherRegion = project.regions[1];
  const first = createPlotGroup(project, region.id, '第一组');
  project.plotGroups.push(first);
  const second = createPlotGroup(project, region.id);
  project.plotGroups.push(second);
  assert.notEqual(first.id, second.id);
  assert.equal(second.name, '新折叠组');
  assert.equal(second.order, 1);

  const child = createPlotGroup(project, region.id, '子组', first.id);
  project.plotGroups.push(child);
  assert.equal(child.parentGroupId, first.id);
  assert.equal(createPlotGroup(project, region.id, '第二个子组', first.id).order, 1);

  const elsewhere = createPlotGroup(project, otherRegion.id, '异区组', first.id);
  assert.equal(elsewhere.parentGroupId, '');

  const inside = createPlotKind(project, region.id, child.id);
  assert.equal(inside.groupId, child.id);
  const outside = createPlotKind(project, otherRegion.id, first.id);
  assert.equal(outside.groupId, '');
});

test('校验折叠组 id、名称、区域与地块归属', () => {
  const project = createDefaultProject();
  const region = project.regions[0];
  const group = createPlotGroup(project, region.id, '校验组');
  project.plotGroups.push(group);
  project.plotKinds[0].groupId = group.id;
  assert.equal(validateProject(project).ok, true);

  const duplicate = createDefaultProject();
  duplicate.plotGroups = [
    createPlotGroup(duplicate, duplicate.regions[0].id, 'A'),
  ];
  duplicate.plotGroups.push({ ...duplicate.plotGroups[0] });
  assert.ok(validateProject(duplicate).errors.some((issue) => issue.code === 'plotGroup.id.duplicate'));

  const emptyName = createDefaultProject();
  emptyName.plotGroups = [createPlotGroup(emptyName, emptyName.regions[0].id, '')];
  emptyName.plotGroups[0].name = '   ';
  assert.ok(validateProject(emptyName).errors.some((issue) => issue.code === 'plotGroup.name'));

  const missingRegion = createDefaultProject();
  missingRegion.plotGroups = [createPlotGroup(missingRegion, 'missing-region', 'X')];
  assert.ok(validateProject(missingRegion).errors.some((issue) => issue.code === 'plotGroup.region'));

  const crossRegion = createDefaultProject();
  crossRegion.plotGroups = [createPlotGroup(crossRegion, crossRegion.regions[0].id, 'A')];
  crossRegion.plotKinds[0].groupId = crossRegion.plotGroups[0].id;
  crossRegion.plotKinds[0].regionId = crossRegion.regions[1].id;
  assert.ok(validateProject(crossRegion).errors.some((issue) => issue.code === 'kind.group.region'));

  const missingParent = createDefaultProject();
  missingParent.plotGroups = [createPlotGroup(missingParent, missingParent.regions[0].id, 'A')];
  missingParent.plotGroups[0].parentGroupId = 'missing-group';
  assert.ok(validateProject(missingParent).errors.some((issue) => issue.code === 'plotGroup.parent'));

  const crossParent = createDefaultProject();
  crossParent.plotGroups = [createPlotGroup(crossParent, crossParent.regions[0].id, '父组')];
  const crossChild = createPlotGroup(crossParent, crossParent.regions[1].id, '子组');
  crossParent.plotGroups.push(crossChild);
  crossChild.parentGroupId = crossParent.plotGroups[0].id;
  assert.ok(validateProject(crossParent).errors.some((issue) => issue.code === 'plotGroup.parent.region'));

  const cyclic = createDefaultProject();
  cyclic.plotGroups = [createPlotGroup(cyclic, cyclic.regions[0].id, 'A')];
  const cyclicB = createPlotGroup(cyclic, cyclic.regions[0].id, 'B');
  cyclic.plotGroups.push(cyclicB);
  cyclic.plotGroups[0].parentGroupId = cyclicB.id;
  cyclicB.parentGroupId = cyclic.plotGroups[0].id;
  assert.ok(validateProject(cyclic).errors.some((issue) => issue.code === 'plotGroup.cycle'));
});
