# M3 静态 Scene 与 UGUI 线框实现

> **冻结通知（2026-08-12，覆盖下文旧 Setup 生成要求）**：老板已批准并冻结当前 `Assets/Settings/Scenes/M3.unity`。后续程序、Setup 和 Agent 不得修改、创建、重生成或保存覆盖；`M3Setup` 仅为只读打开器。只有老板可在 Unity Scene 中手工修改视觉。

## 目标

创建独立 `Assets/Settings/Scenes/M3.unity`，通过幂等 `Assets/Editor/M3Setup.cs` 生成轨头侧面探测的静态 UGUI 线框，并输出三种横屏视口截图供审核。本里程碑只建立视觉结构和真实素材构图，不接 M3 状态机、拖拽、波形计算或场景串联。

## 已确认产品决定

- M3 不重复点击涂抹耦合剂；进入时约 2 秒已涂状态展示属于后续运行时实现。
- UI 统一使用“向下偏转 13°”。
- 主流程目标为 120mm，扫描范围为 150→100mm。
- M3 波形区与数字人采用 M2 的同构布局：MainScene 右侧固定 `SupportArea=576px`，数字人舞台位于辅助区上部约 2/3，波形 `460x240` 位于同一辅助区底部并右对齐；左侧为 RailArea。

## 需求

1. `M3Setup` 在 M3 Scene 不存在时用 Editor API 创建最小 Scene；存在时打开并自愈，只保存 M3，不打开或保存 M1/M2。
2. CanvasScaler 为 Scale With Screen Size、1920x1080、Match 0.5；业务 UI 位于 SafeArea。
3. Header 高 80px，只显示“M3 轨头侧面探测”和“重置流程”。
4. MainScene 以钢轨正视图为主体，使用 `Assets/railwayTracks/正视角.png`；同构透明图作为关闭的透视层预置。
5. K2.5 探头、自制定位尺占位、焊缝、伤损、入射/反射声束均为独立节点；静态初始构图表达“轨头侧面 + 向下 13°”。
6. B 区严格复用 M2 布局：位于 MainScene 右侧 `SupportArea` 下部，尺寸约 460x240、右对齐，静态显示目标 120mm、150-100mm 刻度和一条可见波形占位。
7. C 区为 RailArea 左下固定尺寸“普通视图 / 透视视图”分段控件。
8. D 区高约 176px，显示定位引导、13°角度控件占位和“步骤 1/3 · 探头定位”。
9. DigitalHumanStage 严格复用 M2 布局：约 320px 宽、位于同一 SupportArea 上部约 2/3、与波形右边缘一致；使用可识别全身预览，不只放头像。QAPanel 占位位于人物左侧，默认隐藏。
10. QALayer、DigitalHumanStage、ModalLayer 顺序固定，静态节点不增加 M3 runtime 组件和事件监听。
11. 新增 `M3Shot.cs` 输出 1920x1080、1280x720、2436x1125 PNG，不修改保存后的 Canvas 渲染模式。
12. 不修改 M1/M2 Scene、M2 Setup/runtime、Build Settings 或问答逻辑。

## 验收标准

- [ ] `M3Setup` 可从无 Scene 状态创建并保存 `M3.unity`；连续执行两次后层级、组件和场景内容不继续变化。
- [ ] M3 场景仅有一个 Canvas、一个 EventSystem、一个 SafeArea，且无 Missing Script。
- [ ] CanvasScaler 参数符合 1920x1080 / Match 0.5。
- [ ] 场景使用正视角钢轨、K2.5 和可识别全身数字人预览；普通/透明钢轨同位置同尺寸。
- [ ] 波形与数字人符合 M2 同构布局：SupportArea 576px；数字人在上、波形 460x240 在下并右对齐，不横向并排；C 控件融入 RailArea 左下；D 区步骤与“向下 13°”文字完整可读。
- [ ] 三视口截图无重叠、裁切、方框字或文字溢出，触控控件设计尺寸不小于 64px。
- [ ] M3Setup 只含 Editor 搭建逻辑；本轮不新增 M3 runtime 脚本，不复制 M2 runtime 逻辑。
- [ ] M1/M2 场景相对本轮基线无新增 diff；`git diff --check` 通过。

## 范围外

- 2 秒耦合剂动画、探头/尺子拖拽、13°判定、120mm检出、动态波形、透视切换和帮助计时。
- 数字人 VideoPlayer、QAPanel 实际接线和 DeepSeek 配置。
- M2→M3、M3→M4 串联及 Build Settings。
- 正式透明多功能尺素材制作。
