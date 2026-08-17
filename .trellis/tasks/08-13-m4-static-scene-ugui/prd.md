# M4 静态 Scene 与 UGUI 线框

## 目标

以 M3 冻结 Scene 的页面骨架与视觉令牌为基线，通过幂等 `Assets/Editor/M4Setup.cs` 创建独立 `Assets/Settings/Scenes/M4.unity`，完成“第三方位·轨腰部位探测”的静态 UGUI 线框和三视口审核截图。本里程碑只建立视觉与真实节点，不接运行时状态机。

## 已确认合同

- 模块标题：`M4 轨腰部位探测`。
- 初始教学状态：耦合剂已涂；静态图表达定位阶段，运行时 2 秒薄膜动画留节点不播放。
- 定位：K2.5 探头放在轨腰最上端，视觉表达“向上偏转 10°”。
- 扫描：玩家从 80mm 起点移动至 40mm 红色损伤检出点并锁定；80→30mm 仅作为波形参考域和仪器刻度。
- 目标：钢轨红色区域中心是绿色检测束命中、40mm 距离和最终尺子 40mm 锚点的唯一目标；`WeldLine` 仅作视觉参照。
- 波形占位：目标 40mm、刻度 80/30，一条可见峰值曲线；运行时包络留待后续规划。
- 三步显示：`步骤 1/3 · 探头定位`。
- M5 未实现；静态完成出口只预留节点，不创建假 M5。

## 要求

1. M4 属未冻结模块：`M4Setup` 在 Scene 不存在时创建，在存在时按固定名称幂等自愈，只打开/保存 M4。
2. Canvas 使用 `1920x1080 / Match 0.5`，业务 UI 位于 `SafeArea`；页面布局、颜色、尺寸和层级顺序采用 `.trellis/spec/unity/ugui-module-template.md`。
3. Header 高 80px，只显示模块标题和“重置流程”。
4. MainScene 左侧为 RailArea，右侧固定 `SupportArea=576px`；数字人在上、`WaveformArea_B=460x240` 在下并右对齐。
5. RailViewport 使用同构的 `正视角.png` / `正视角透明.png`，两层同位置同尺寸；普通层默认显示，透视层默认隐藏。
6. ToolShelf 为左上局部工具架，提供 `ProbeHome` 与 `RulerHome`；K2.5 和同一把多功能尺全程可识别。工作态节点 `Probe`/`Ruler` 必须各有直接子节点 `bg`，使后续拖拽根与旋转视觉分离；不得创建第二把业务尺。
7. 静态定位构图必须明确区别于 M3：探头以 0°放在轨腰最上端，定位预览表达多功能尺 10°槽校角且尺身基准线平行钢轨底边；D 区使用“向上偏转”，不得出现 13°、120mm、150→100mm 或“轨头侧面”。
8. RailViewport 预置独立 `WeldLine`、`CouplantOverlay`、`DamageMarker`、`BeamLayer/IncidentBeam/ReflectedBeam`、`MeasurementBubble` 节点；`DamageMarker` 对准钢轨红色损伤区域并作为唯一目标，`WeldLine` 不与其混用。默认状态符合定位首帧。
9. `PositionPreview` 如保留，仅作为不可交互引导层，其探头/尺子预览不得被 runtime 当作第二套业务对象；正式交互始终使用 ToolShelf 中的 `Probe` 和 `Ruler`。
10. B 区静态显示目标 40mm、80→30mm 参考刻度和一条程序曲线视觉占位，不挂 runtime Graphic。
11. C 区使用 364x64 普通/透视分段控件；D 区高 176px，显示定位引导、0→20° Slider、10°值和步骤状态，并预留检出后由玩家确认进入尺子复测的既有样式按钮节点。
12. QALayer、DigitalHumanStage、ModalLayer 顺序固定；数字人使用全身预览，QAPanel/Blocker/Modal 默认隐藏。
13. 新增 `M4Shot.cs`，采用 M2Shot 的 Scale With Screen Size 计算、非空像素断言、`finally` 恢复和 Scene SHA-256 不变检查，输出 1920x1080、1280x720、2436x1125。
14. 静态 Scene 不挂 M3/M4 runtime 组件，不注册运行时事件；不修改 M1/M2/M3 Scene、Build Settings 或现有素材。

## 验收标准

- [ ] M4Setup 可从无 Scene 状态创建 M4；连续执行两次后 Scene SHA-256 不再变化。
- [ ] Scene 仅一个 Canvas、EventSystem、SafeArea，无 Missing Script；CanvasScaler 参数正确。
- [ ] M4 层级满足后续 runtime 所需节点合同，Probe/Ruler 根与 `bg` 视觉分离，同一把尺可承担 10°校角和 0/40mm 复测。
- [ ] 红色损伤中心、DamageMarker、IncidentBeam 目标和 40mm 测量终点可使用同一坐标标定；WeldLine 仅作视觉参照。
- [ ] 三视口中钢轨为第一视觉信号；数字人与波形上下排列；无重叠、裁切、方框字或文字溢出。
- [ ] 静态构图清楚表达轨腰最上端、向上 10°、目标 40mm、80→30mm 参考域，且无 M3 残留文案或数字。
- [ ] 普通触控控件设计尺寸 ≥64px；数字人全身可见。
- [ ] M4Shot 对每张图执行非空像素断言，截图前后 M4 Scene 哈希一致。
- [ ] 本轮不新增 `Assets/Scripts/M4*.cs`，M1/M2/M3 Scene 与 Build Settings 哈希相对本轮基线不变。
- [ ] `git diff --check` 通过；老板审核三视口后才冻结 M4 并进入 runtime 规划子任务。

## 范围外

- 耦合剂动画、拖拽、角度判定、动态波形、透视切换、帮助计时、QA/数字人实际接线。
- M4 runtime 脚本、Play Mode 烟测、M3→M4/M4→M5 串联和 Build Settings。
- 修改或重生成冻结的 M2/M3 Scene。
- 外部素材搜索与正式美术重制。
