# PC/PE 场景维护说明

当前使用独立 `Assets/Scenes/SampleScene_PC.unity` 与 `Assets/Scenes/SampleScene_PE.unity`，道具栏有独立MagicSlot_PC/MagicSlot_PE预制体。历史整场复制方案及回滚、逐项迁移记录已保存在 `Assets/Docs/Archive/knowledge-before-rebuild-2026-11-05.zip`；不再作为当前执行清单。

共享脚本/数据修改会影响两端，场景结构、引用、按钮入口和布局须分别确认。PC改动不能直接整场覆盖PE；只补本次需要的结构和Inspector绑定，保留PE的坐标、字号、颜色与触摸布局。先查Prefab来源/Override，不能因PC可用就推断PE已接线。

平台场景选择由SceneTransitionManager控制：Android/iOS编译宏选PE，Editor可forceMobileSceneInEditor。当前磁盘Build Settings启用StartScene和PE、PC禁用；这是当前配置，未在知识重建中改动。PC二层另在Assets根目录且未列入构建。

教程两端共享管理器，PE优先本地化key_pe，当前换牌正文已有中英_pe条目。13页步骤/输入门控按TutorialManagerUI，不能再用旧“PE只有11页”报告判断。

后续任务先读取目标场景Property Tree和共享脚本，再按 `Locus/knowledge/skill/scene-ui-integration/SKILL.md` 接线与验收。触摸确认、实际射线、边界分辨率与真机体验需目标验证；本轮不宣称PC/PE画面或交互一致。
