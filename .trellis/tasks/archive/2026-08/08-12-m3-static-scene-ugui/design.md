# M3 静态 Scene 与 UGUI 线框设计

## 结构边界

本里程碑新增两个 Editor 文件：

- `Assets/Editor/M3Setup.cs`：创建/维护独立 M3 Scene 和静态层级。
- `Assets/Editor/M3Shot.cs`：离屏渲染三视口截图。

不新增 runtime 脚本。M3Setup 不引用 `M2FlowController`、`M2ProbeDrag` 等组件，也不向 Button/Slider 注册事件。

## Scene 创建

`SetupM3Batch` / 菜单入口先判断 `Assets/Settings/Scenes/M3.unity`：

- 不存在：`EditorSceneManager.NewScene(EmptyScene)`，创建 Canvas、CanvasScaler、GraphicRaycaster、EventSystem 和 InputSystemUIInputModule，然后保存为 M3。
- 已存在：`OpenScene(..., Single)`，Ensure 根组件和业务层级。

每次只保存当前 M3 Scene。对象通过父级下的固定名称 Ensure；存在对象只重设权威布局和静态属性，不重复增加组件。

## 1920 基准布局

- 页面边距 24，区域间距 16。
- Header：顶部 80。
- Dock：底部 176。
- MainScene：Header 与 Dock 之间。
- MainScene 复用 M2 的左右分区：左侧 `RailArea`，右侧 `SupportArea=576px`，间距 16px。
- DigitalHumanStage：宽约 320px，位于 SupportArea 上部约 2/3，右对齐页面边距。
- WaveformArea_B：460x240，位于同一 SupportArea 底部并右对齐，与数字人右边缘一致。
- RailViewport：RailArea 主要区域，钢轨图保持 2292:740 比例。
- PerspectiveBar_C：RailArea 左下，约 364x64。

波形与数字人不得横向并排；宽屏扩展只增加 RailArea 周围空间，SupportArea、B 区、D 区和人物舞台保持 M2 的稳定尺寸。CanvasScaler 负责 1280x720 缩放。

## 层级

```text
Canvas
└── SafeArea
    ├── Background
    ├── HeaderBar
    │   ├── ModuleTitle
    │   └── ResetButton
    ├── MainScene
    │   ├── RailArea
    │   │   ├── ToolShelf
    │   │   │   ├── ProbeHome
    │   │   │   └── RulerHome
    │   │   ├── RailViewport
    │   │   │   ├── RailNormal
    │   │   │   ├── RailPerspective
    │   │   │   ├── WeldLine
    │   │   │   ├── CouplantOverlay
    │   │   │   ├── DamageMarker
    │   │   │   ├── BeamLayer
    │   │   │   │   ├── IncidentBeam
    │   │   │   │   └── ReflectedBeam
    │   │   │   ├── Ruler
    │   │   │   ├── Probe
    │   │   │   └── MeasurementBubble
    │   │   └── PerspectiveBar_C
    │   └── SupportArea
    │       └── WaveformArea_B
    │           ├── WaveHeader
    │           ├── WaveGrid
    │           ├── WaveLine
    │           ├── TargetMarker
    │           └── DetectionBanner
    ├── ControlDock_D
    │   ├── InstructionArea
    │   ├── PositioningControls
    │   └── StepProgress
    ├── QALayer
    │   ├── Blocker
    │   └── QAPanel
    ├── DigitalHumanStage
    │   └── FullBodyPreview
    └── ModalLayer
        └── ResetConfirmDialog
```

## 静态视觉

- 只使用浅灰页面、白色教学面和低对比深色仪器，避免单一蓝色主题。
- 钢轨是第一视觉信号；探头、尺子和 13°角线位于轨头侧面右侧。
- 波形占位由若干 Image 线段组成，不依赖 runtime Graphic。
- 定位尺使用简化矩形/斜边占位，标记 13°与 120mm；明确标注“功能占位”。
- 数字人静态预览使用 `Assets/交互动画素材/额外/数字人.png`，该图为可识别全身透明 PNG；保持比例，不加头像卡片；舞台位置与 M2 一致，位于 SupportArea 上方、波形正上方。
- `RailPerspective`、DamageMarker、BeamLayer、MeasurementBubble、Blocker、QAPanel、ModalLayer 默认隐藏，但节点和布局预置完整。

## 幂等与验证

- 组件使用 `GetComponent ?? AddComponent`。
- 所有 TMP 通过 `GetComponentsInChildren<TextMeshProUGUI>(true)` 重指向中文字体。
- 图片引用每次自愈为指定静态素材；本里程碑没有用户替换后的稳定资产配置，因此布局素材由 Setup 权威维护。
- 截图工具临时切换 Canvas 到 ScreenSpaceCamera，finally 恢复，不保存场景。
- 两次 Setup 后比较场景 SHA-256；截图分别检查三个视口。

## 风险

- `Assets/railwayTracks/` 当前未跟踪：本轮使用但不修改图片；交付状态会明确要求纳管。
- Unity 项目可能被已打开 Editor 锁定：优先尝试 batchmode；锁定时提供菜单执行路径，并不强行并发。
- 现有工作区 M1/M2 有改动：执行前后记录其文件哈希，确保本轮未触碰。
