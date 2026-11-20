---
id: kd_626ccfb9-d88b-4cd5-a159-7321fbe65f8b
injectMode: inherit
injectAgents:
- '*'
aiEditMode: inherit
skillEnabled: true
skillSurface: command
---

# 格子地牢渐进改造与几何验证

生命周期：草稿。方法版本：0.1。
适用：本项目Unity 2022.3、URP、Legacy Input、程序条带与Scribble手绘网格；修改地牢运行控制、地形构建、局部揭示或主题绑定时按需加载。
证据：源码/资产阅读及一次初始运行观察已完成；以下重构与优化方法均为候选，尚未执行代表性闭环。不能当作已验证优化方案。

## 用途、前置与入口

预计复用：原型向对象地图迁移、后续地形局部更新、手绘长段性能调查。方法关注拓扑兼容、生成生命周期和视觉回归，不设计具体房间，不替代战斗/保存/UI接入Skill。

- 当前入口：[PROJECT](../../../../Assets/Docs/Workflow/PROJECT.md)、[ARCHITECTURE](../../../../Assets/Docs/Workflow/ARCHITECTURE.md)。现场重新读取目标源码和资产，不从文档复制易变数值。
- 本次调查与证据：[现状调研与改造实施案](../../../../Assets/Docs/二层地图现状调研与改造实施案.md)；对象契约：[代码架构提案](../../../../Assets/Docs/二层地图系统代码架构.md)。二者是未实施提案。
- 必需输入：修改范围、对照seed/配置版本、实际Scene序列化值、资源身份、预期边界变化；需要性能结论时补设备/分辨率/CPU与GPU采样。
- 缺目标设备不阻塞纯对象模型，但不能承诺帧率；缺视觉批准时仅做不改变资源参数的提取与探针。
- 所有Scene/Prefab写入仍遵守美术审阅、Inspector绑定和结构化Unity API规则；本Skill不新增权限。

## K-01：先核对拓扑，再替换运行控制

证据状态：候选。来源：S1、S2和本项目旧生成器。

1. 同seed稳定排序格位/条带并转换为独立草稿；共享交叉点只生成一个Cell，建立显式Connection。
2. 特别枚举跨条带“坐标相邻但不是条带连续段”的接触边。旧原型按邻接可走时，兼容图必须明确导入或逐项解释差异；正式生成禁止无意接触变通路。
3. 旧内容标记不转换为已实现奖励/房间。兼容领域、Boss揭示策略与正式领域规则分开描述。
4. 先对比坐标、起点、邻接与可达集，再接View。通过后才提取纯布局，避免复制算法长期分叉。

成功信号：旧seed拓扑差异为零或每项有批准的新规则；同坐标不连接的测试不能通行；不接View仍可测行动。
失败处理：输出差异两端坐标、条带和连接来源；保留旧入口，不以放宽验证来过关。
限制：旧seed布局一致不证明领域门禁可解；新增环路必须另验所有跨域边，奖励终端不采用“删除所有死路”。
执行证据：无，待V1。重验条件：旧生成器、隐式通行、坐标定义或领域规则变化。

## K-02：连续段计划与局部边界更新

证据状态：候选。来源：S1、S3、S4和现有地形方法。

1. 保留连续段拼装，先输出只读表面段/Bounds/变体键和边界引用，再实例化。
2. 交叉、样式切换、动态门禁和隐藏边界是显式段边界；通行、视线、遮挡分别命名，不能由Renderer或Collider推导规则。
3. 用序列化绑定明确结构层、Scribble层和锚点，不按名字或“最大面积Plane”猜目标。
4. 局部事件只改变相应Boundary和邻近受影响段；保持未受影响SegmentId、Mesh与装饰随机选择。
5. 段长预算以同画面规模和剔除采样决定，不能整图Combine，也不能假设拆段不影响笔触接缝。

成功信号：移动/转向不Rebuild；局部揭示不重建无关段；相同seed和姿态外观可对照。
失败处理：按段定位坐标/朝向/主题/绑定与随机流，保留旧公式；出现非批准视觉变化时不切正式入口。
限制：分段与门框仍需视觉审阅；透明墙面不自动是可靠遮挡体。
执行证据：无，待V2。重验条件：Prefab层级、绑定、尺寸、Shader或边界策略变化。

## K-03：先查生命周期与增长公式，再选渲染优化

证据状态：候选。来源：S4～S6和Scribble生命周期代码。

1. 分开计数Prefab实例化、OnEnable、设置面积、Rebuild、Mesh创建/销毁和可见Renderer；仅关Renderer不代表组件未生成网格。
2. 单独比较1/3/6格墙与地：记录线数量、采样、引导模式、地面行列、顶点/索引/Bounds。避免尺寸比例同时乘两维而误称线性增长。
3. 确认后再引入一次配置/一次生成入口；保留共用组件默认语义，核查所有使用方。共享Mesh必须明确缓存所有权，不能单实例OnDestroy销毁共享资产。
4. 实例引用顶点和、唯一Mesh内存、实际绘制量分别统计；CPU生成峰值、稳定CPU/GPU与透明overdraw分别测，不用其中一项代替另一项。
5. URP中先核对SRP Batcher实际路径。只有相同Mesh/Material且存在足够重复时比较实例化；不盲加MPB或破坏SRP兼容。缓存先测命中率，分区/LOD按实测瓶颈选择。

成功信号：需要显示的动态表面初始化一次、无用副本不生成、所有权清楚；优化同输入有实测收益且视觉批准。
失败处理：没有GPU/设备数据就保留未验证状态；无收益撤候选优化，不降画质掩盖问题。
限制：固定数据下减少顶点不能自动推出更快帧率；密度/采样与Shader效果调整需要美术批准。
执行证据：无，待V3；已有初始规模观察只说明需要调查，不证明本方法已运行。
重验条件：Scribble算法、组件生命周期、URP/Shader、资源密度或平台改变。

## 验证与晋级

| ID | 在项目根执行的检查 | 成功信号 | 状态 |
| --- | --- | --- | --- |
| V1 | 后续新增适配测试，比较现有Seed1/31及1～100格位、邻接、起点与可达；Session手工图测试 | 无未解释拓扑差异，单步/转向独立于View，正式门禁无绕路 | 未执行，不是已有测试结果 |
| V2 | 后续新增局部更新探针；同seed/资源/姿态/分辨率截图；记录变更前后段和Mesh身份 | 移动零Rebuild，无关段不变，视觉对照通过 | 未执行 |
| V3 | 后续隔离1/3/6格探针；统计初始化与Mesh生命周期；按profiler方法采样固定镜头和局部更新 | 无重复构建/泄漏；规模可解释；CPU/GPU结论有平台数据 | 未执行；绝对预算待测 |

只有执行并检查相应成功信号后，才将对应K条目晋级为“本项目已验证”，记录条件与证据。编译、静态文档检查、初始截图均不能替代行为或性能验收。

## 来源与适配决定

均于2026-11-12实际读取，正文范围和详细决定见调查案第4节。

- S1：[Red Blob Games — Introduction to A*](https://www.redblobgames.com/pathfinding/a-star/introduction.html)，图表示/BFS。采用显式节点边和等权可达分析，不为单格移动引入NavMesh。
- S2：[Rooms and Mazes](https://journal.stuffwithstuff.com/2014/12/21/rooms-and-mazes/)，区域连接/环路/死路。采用阶段与连通分析，不删除奖励终端。
- S3：[Dungeon Architect — Connections](https://docs.dungeonarchitect.dev/unity/snap-grid-flow/sgf-connections/)，门/墙与局部原点。借鉴边界构件，不安装插件或改成碰撞触发通行。
- S4：[Unity 2022.3 Combining meshes](https://docs.unity3d.com/2022.3/Documentation/Manual/combining-meshes.html)，合并失去局部剔除。采用分段权衡，不整图合并。
- S5：[Unity 2022.3 SRP Batcher](https://docs.unity3d.com/2022.3/Documentation/Manual/SRPBatcher.html)、[GPU instancing](https://docs.unity3d.com/2022.3/Documentation/Manual/GPUInstancing.html)，兼容与性能限制。实际路径优先，不以开关代替采样。
- S6：[Unity 2022.3 Graphics performance fundamentals](https://docs.unity3d.com/2022.3/Documentation/Manual/OptimizingGraphicsPerformance.html)，先定位CPU/GPU。透明层成本需实测，不承诺缓存/池/LOD必然有效。

## 维护

入口登记于[项目Skill索引](../INDEX.md)，草稿状态与验证摘要同步。触发/步骤与已有Skill重叠时合并；平台或主路径有反证时标需重验/已失效并保留反例，不因编辑文字晋级。一次调查数值留调查文档，不复制为长期预算。项目方法不修改用户级learn-and-make，也不授权实施产品提案。
