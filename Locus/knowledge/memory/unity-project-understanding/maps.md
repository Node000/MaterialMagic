---
id: kd_585c617c-25af-411b-9129-ff8af3b51a23
injectMode: inherit
injectAgents:
- '*'
aiEditMode: inherit
---

# 章节地图与二层探索

## 正式章节共享规则

`Assets/Scripts/Data/RunManager.cs` 持地图/池/RNG，`Assets/Scripts/Data/RunMapNodeModel.cs` 持格子数据；`Assets/Scripts/Simulation/RunSession.cs` 持节点状态、战胜→奖励→自动商店→节点完成与Boss激活判定。HandSystemUI通过 `HandSystemUI.RunSession.cs` 消费转移，自动商店仍归属原节点，离店才提交一次完成，不再各入口重复推进。

`Assets/Scripts/Simulation/RunMapGenerationRules.cs` 是真实一层生成入口：HandSystemUI.BuildDebugMap/BuildChapterMapGrid分别委托 Generate/GenerateGrid；保留教程、设计图、固定关、隐藏、Boss缓存和原RNG顺序，不以未调用的DungeonMapGenerator替换正式拓扑。`RunLevelRules` 共享选关池、敌组、休息模型、格子关卡解析及ResetNodeDeck；重置清临时牌堆并以Deck恢复DrawPile，不触发弃牌或随机。

Chapter1数据仍为宽5高8、起点(2,0)、levelLength18，进阶可修正。方向牌绝对移动，不环绕，不进不可用格。`SecondFloorExplorationSession` 被RunManager真实调用，负责坐标移动及当前格+上下左右可用格揭示；转向不改变绝对方向牌语义。

Boss建图时 ResolveChapterBoss 确定关卡和敌组，chapterBossLevelId/chapterBossGroupIndex随RunPool保存。额外Boss格不等于逐格独立抽组；组mapIconPath优先，缺失回退关卡/通用图标。Session持激活条件，UI保留原揭示动画提交时机。

## 整局脚本入口

`Assets/Scripts/Simulation/GameSessionFactory.cs` 的 Production 使用显式catalog/profile/seed、真实ApplyStartConfig和RunMapGenerationRules；GameSession复用Run/Battle/Command/Event/Rest/Reward/Shop/Bonus/Tutorial各共享会话，不是第二套效果实现。AI只经合法命令改变状态，可见观察不提供牌堆顺序/RNG/隐藏关卡内容；诊断指纹和checkpoint另行处理。

## PC二层与F17条带原型

`Assets/SampleScene_PC_SecondFloor.unity` 使用SecondFloorPCMapGenerator与Resources配置；区域参数以SO为准。HandSystemUI.EnterSecondFloor存在公开加载接口，但普通流程无调用者、场景未入构建表，不能据此声称一层到二层通关已接通。

`Assets/Scripts/SecondFloor/StripDungeonMapGenerator.cs` 与StripDungeonMap/Config/主题数据是另一套独立条带生成器，不可套用PC区域参数。F17 `Assets/Scripts/SecondFloor/SecondFloorStripDungeonFirstPersonPrototype.cs` 的坐标、Turn、MoveForward、Reveal已委托纯 `Assets/Scripts/Simulation/FirstPersonGridExplorationSession.cs`，camera、环境和minimap仍在适配层，未改序列化参数。

F17揭示规则不是十字：当前格+沿朝向连续可走直线，遇墙停止；即时视野不累积。初始朝向按北/东/南/西首个Path；转向不揭Boss，成功进入Boss所属条带才揭示，随后Boss格可走并参与前向射线。Session不引用StripDungeonConfig或Unity对象。原型仍不是正式章节入口/存档协议。

`Assets/Scenes/SecondFloorBattleCameraTest.unity` 是独立的固定格遭遇运镜原型。`SecondFloorBattleCameraPrototype` 复用第一人称格子移动规则，用单相机往返探索姿态和越肩构图；主角和敌人以世界空间立绘朝镜头。它不接正式战斗、随机地图或存档，也不进入构建列表。

## 二层小地图原型

现有平面测试场景 `Assets/Scenes/SecondFloorStripDungeonTest.unity` 已原位改为 WASD 小地图与屏幕日志，不再是点格揭 Boss 的测试。`SecondFloorMinimapGenerator` 复用条带原型导入四个闭环区域，但只沿显式条带边移动；下部三对区域各一条独立连接，上部 U 与下部三组传送房双向配对。`SecondFloorMinimapSession` 不执行房间效果；进入传送房才切换部分及落点。`SecondFloorStripDungeonTestUI` 读键和记日志，`SecondFloorMinimapGraphic` 仅画当前部分。未接正式章节、存档或构建列表，第一人称场景不受影响。
