# M3 独立 Scene 与 UGUI 技术设计

## 1. 设计目标

在独立 `Assets/Settings/Scenes/M3.unity` 中实现轨头侧面探测。界面保持 M2 的操作连续性，但场景内容改为钢轨正视图、向下 13° 定位和 120mm 检出。M3 不复制 M2 的业务脚本；待 M2 当前修复和验收稳定后，再提取 M2/M3/M4 可共用的探测组件。

## 2. 画布与布局

Canvas 基线：1920x1080、Scale With Screen Size、Match 0.5，全部业务 UI 放在 SafeArea。

1920 基准建议：

| 区域 | 尺寸/位置 |
|---|---|
| 页面边距 | 24px |
| Header | 高 80px，左标题、右重置 |
| ControlDock_D | 底部高 176px |
| DigitalHumanStage | 右侧宽 320px，无边框 |
| MainScene | Header 与 D 区之间，扣除数字人宽度 |
| WaveformArea_B | 主场景右上 460x240，允许 440-480 x 220-250 |
| PerspectiveBar_C | 主场景左下，高 64px |
| 触控控件 | 高度/有效触控区不小于 64px |

宽屏 `2436x1125` 不拉宽 B 区和数字人；新增空间优先给钢轨视口。`1280x720` 由 CanvasScaler 整体缩放，D 区文案最多两行，步骤控件不横向挤压数字人。

```text
┌────────────────────────────────────────────────────────────────────┐
│ M3 轨头侧面探测                                       重置流程     │
├──────────────────────────────────────────────────┬─────────────────┤
│ 主教学场景                                         │ 全身数字人      │
│  工具暂存       紧凑波形仪器                       │ 约 320px        │
│ ┌──────────────────────────────────────────────┐ │ 长按问答        │
│ │ 钢轨正视图 / 焊缝 / 侧面探头 / 定位尺         │ │                 │
│ │ 透视时显示伤损与入射、反射声束                │ │ QAPanel 向左开  │
│ └──────────────────────────────────────────────┘ │                 │
│ [普通视图 | 透视视图]                              │                 │
├──────────────────────────────────────────────────┴─────────────────┤
│ 操作提示              当前控件                  步骤 x/3 + 阶段名  │
└────────────────────────────────────────────────────────────────────┘
```

M3 交互步骤显示为 3 步：定位、扫描、测距。2 秒耦合剂展示属于进入过渡，不占步骤。

## 3. 推荐层级

```text
Canvas
└── SafeArea
    ├── Background
    ├── HeaderBar
    │   ├── ModuleTitle
    │   └── ResetButton
    ├── MainScene
    │   ├── ToolShelf
    │   │   ├── ProbeHome
    │   │   └── RulerHome
    │   ├── RailViewport
    │   │   ├── RailNormal
    │   │   ├── RailPerspective
    │   │   ├── WeldLine
    │   │   ├── CouplantOverlay
    │   │   ├── DamageMarker
    │   │   ├── BeamLayer
    │   │   │   ├── IncidentBeam
    │   │   │   └── ReflectedBeam
    │   │   ├── Probe
    │   │   ├── Ruler
    │   │   └── MeasurementBubble
    │   ├── PerspectiveBar_C
    │   │   └── ViewModeSegment
    │   │       ├── NormalButton
    │   │       └── PerspectiveButton
    │   └── WaveformArea_B
    │       ├── WaveHeader
    │       ├── WaveGrid
    │       ├── WaveGraphic
    │       ├── TargetMarker
    │       └── DetectionBanner
    ├── ControlDock_D
    │   ├── InstructionArea
    │   ├── StepControlArea
    │   │   ├── PositioningControls
    │   │   ├── ScanControls
    │   │   ├── MeasureControls
    │   │   ├── HelpControls
    │   │   └── CompletionControls
    │   └── StepProgress
    ├── QALayer
    │   ├── Blocker
    │   └── QAPanel
    ├── DigitalHumanStage
    │   └── FullBodyView
    └── ModalLayer
        └── ResetConfirmDialog
```

`RailNormal` 与 `RailPerspective` 位置、尺寸和 preserveAspect 完全一致。伤损和声束使用独立层，不依赖透明底图中已烘焙的红点，以支持红转黄、闪烁和位置替换。效果层全部 `raycastTarget=false`。

层级顺序保持 `QALayer < DigitalHumanStage < ModalLayer`：QA Blocker 不压暗或拦截数字人，重置确认仍可覆盖人物。

## 4. 状态机与交互合同

状态：`Intro -> Positioning -> Scanning -> Measuring -> Completed`。

- `Intro`：显示 2 秒耦合剂薄膜，底层输入锁定；结束后尺子和探头均可用于定位。
- `Positioning`：探头进入轨头侧面起始区、尺子进入 13°定位区、滑块达到向下 13°，三条件满足后进入扫描。
- `Scanning`：探头沿水平归一化轨迹从 150mm 到 100mm 移动。角度偏离时停止前进但保留位置。
- `Measuring`：检出后点击下一步，尺子切换为测距用途；0 刻度对齐熔合线后自动吸附并显示 120mm。
- `Completed`：保留测量结果；点击出口调用 UnityEvent。未配置时只更新提示，不抛异常。

120mm 检出采用“前后帧跨越目标或进入容差”判定，只触发一次蜂鸣。成功状态锁定，但实时波形继续跟随探头展示下降段。

## 5. 波形与透视

波形配置：

- scanStart = 150mm
- growthStart = 140mm
- peakWindowMax = 124mm
- target = 120mm
- peakWindowMin = 118mm
- scanEnd = 100mm

B 区只显示一条程序曲线和当前距离；命中后保留 120mm 目标标记，标题保持“峰值锁定”，曲线仍可下降。

普通/透视切换只改变显示层：普通图、透明图、DamageMarker、BeamLayer。入射/反射声束随探头位置更新，命中后伤损由红转黄并短暂高亮；按功能文档不实现流动粒子。

## 6. 复用策略

M2 当前组件类名和状态绑定 M2，直接挂到 M3 会带来 Couplant 首阶段、步骤文案和尺子显隐错误。推荐顺序：

1. 先完成并冻结 M2 当前修复验收。
2. 提取短小的 `ProbeScanProfile` 配置资产，承载目标角方向、扫描区间、波形窗口、目标距离、步骤文案和进入时耦合剂策略。
3. 将探头拖拽、尺子双用途、波形和 IdleHelp 提取为 M2/M3/M4 共用组件；M2 Setup 先迁移并做回归，再由 M3Setup 复用。
4. 数字人、QA、视频和网络组件直接复用 M1 能力，仅 Editor Ensure 参数化，不新增 M3 数字人 runtime 脚本。

若公共提取导致 M2 回归，回滚公共改造并暂停 M3 runtime 接入；保留 M3 静态 Scene，不复制整套 M2 逻辑作为临时方案。

## 7. Setup 与场景边界

`M3Setup` 固定打开并只保存 `M3.unity`，负责 Canvas、层级、锚点、组件引用、字体和素材注入。素材仅字段为空时注入，用户替换后重跑不覆盖。所有 Button/Slider/runtime event 在组件 `Awake/OnEnable` 幂等绑定，不能依赖 Setup 中的非持久委托。

Scene 初建建议从最小空 Canvas 模板创建，不复制 M2 Scene YAML，以免继承 fileID、监听和 M2 专用节点。

## 8. 风险与回滚

- `Assets/railwayTracks/` 未跟踪：纳管前不得宣称可在其他工作区复现。
- 多功能尺不合格：静态和功能验收可用程序化占位，最终视觉验收必须替换。
- M2 仍在修复：公共组件提取必须后置，避免并行修改冲突。
- 直接打开 M3 无 M2 状态：M3 必须自包含初始化为“耦合剂已涂抹”。
- M4 未实现：出口为空时保持完成状态，不修改 Build Settings、不创建假场景。
