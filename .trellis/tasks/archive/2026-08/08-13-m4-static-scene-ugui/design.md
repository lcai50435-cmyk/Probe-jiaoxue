# M4 静态 Scene 与 UGUI 线框设计

## 1. 文件边界

新增：

- `Assets/Editor/M4Setup.cs`：创建并幂等维护 M4 静态 Scene。
- `Assets/Editor/M4Shot.cs`：三视口离屏截图与像素/哈希验证。
- `Assets/Settings/Scenes/M4.unity` 及 `.meta`。

不新增 runtime 脚本，不修改 M1/M2/M3 Scene、Build Settings 或现有素材。M3 冻结 Scene 只作为人工视觉权威和节点合同参考，禁止复制 YAML/fileID。

## 2. 页面骨架

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
    │   │   │   │   └── Probe
    │   │   │   │       └── bg
    │   │   │   └── RulerHome
    │   │   │       └── Ruler
    │   │   │           └── bg
    │   │   ├── RailViewport
    │   │   │   ├── RailNormal
    │   │   │   ├── RailPerspective
    │   │   │   ├── WeldLine
    │   │   │   ├── CouplantOverlay
    │   │   │   ├── DamageMarker
    │   │   │   ├── BeamLayer
    │   │   │   │   ├── IncidentBeam
    │   │   │   │   └── ReflectedBeam
    │   │   │   ├── MeasurementBubble
    │   │   │   └── PositionPreview
    │   │   │       ├── ProbePreview
    │   │   │       │   └── bg
    │   │   │       ├── RulerPreview
    │   │   │       │   └── bg
    │   │   │       └── AngleGuide
    │   │   └── PerspectiveBar_C
    │   └── SupportArea
    │       └── WaveformArea_B
    ├── ControlDock_D
    │   ├── InstructionArea
    │   ├── PositioningControls
    │   │   └── AngleTrack
    │   ├── StepProgress
    │   ├── CompletionPanel
    │   └── HelpPanel
    ├── QALayer
    ├── DigitalHumanStage
    └── ModalLayer
```

ToolShelf 中的 Home 工具定义正式初态。运行时只允许这一个 `Probe` 和一个 `Ruler` 在 Home、定位校角和距离复测之间切换。`PositionPreview` 仅用于静态线框展示正确构图，默认不可交互；后续只能作为提示层或关闭，禁止把 `ProbePreview` / `RulerPreview` 当作第二套业务对象。

## 3. 布局与视觉

沿用 M3 验收基线：页面 24px 边距、Header 80px、Dock 176px、区域间距 16px、SupportArea 576px、数字人约 320px、波形 460x240、C 区 364x64。

M4 专属构图：

- 正视钢轨居中占 RailViewport 主体。
- 定位预览放在焊缝一侧的轨腰上缘，不覆盖焊缝；探头先以 0°放置，多功能尺 10°槽、探头和尺身基准线共同表达“向上 10°”，尺身基准线应平行钢轨底边。
- 多功能尺优先使用透明 `多功能尺子.png`，按比例缩放，确保 10°槽、0mm 与40mm标识可识别；不使用带蓝背景的 JPG。
- AngleGuide 使用 Accent 橙色，文字为“向上偏转 10°”；钢轨红色区域和对应 DamageMarker 是唯一损伤目标，WeldLine 仅作视觉参照。
- IncidentBeam 从探头入射点朝红色损伤中心构图；ReflectedBeam 仅作视觉预留，不承担命中判定。
- 波形屏为 Screen 深色，单条 Wave 绿色曲线，目标 40mm 黄色标记，左右刻度 80/30；30mm 是参考刻度，不表达玩家必须越过 40mm继续扫描。
- 静态首帧不展示透视声束、伤损高亮、测量气泡、完成面板、帮助面板、QA Blocker 或 Modal。

## 4. Setup 合同

- Scene 不存在时用 Editor API 新建，创建 Canvas/EventSystem 后保存到 M4 路径。
- Scene 已存在时只打开 M4，通过父级直接子节点名称 Ensure；不得递归误命中同名 `bg`。
- 每次自愈布局、字号、颜色、静态文案、Slider 范围和权威底图。
- 可替换的探头/尺子/数字人素材仅在字段为空时注入；普通/透明钢轨为权威底图，可每次校正。
- runtime 事件监听保持空；静态 Button/Slider 只有组件和外观。
- 连续运行两次比较 M4 Scene SHA-256，必须一致。

## 5. 截图合同

`M4Shot` 参考 M2Shot 的完整实现：

- 使用图形设备 batchmode，不用 `-nographics`。
- 根据 CanvasScaler referenceResolution/match 计算逻辑画布尺寸。
- 每个视口渲染后采样像素，纯色图抛错。
- `finally` 恢复 Canvas、CanvasScaler、Camera、RectTransform 和临时预览状态，不保存 Scene。
- 截图前后比较 M4 Scene SHA-256。

输出：`Logs/m4-shot_1920x1080.png`、`m4-shot_1280x720.png`、`m4-shot_2436x1125.png`。

## 6. 冻结与后续规划

老板批准三视口后记录 Scene SHA-256，将 M4 设为视觉权威，并把 `M4Setup` 收缩为只读打开器。随后启动 `08-13-m4-runtime-planning`，重新核查冻结 Scene 的真实节点后编写 runtime 规划。

## 7. 风险与回滚

- Unity 被已有 Editor 占用：不并发 batchmode，改用菜单入口。
- 多功能尺缩小后 0mm/40mm/10°不可读：调整静态尺寸和工具架构图，不换成蓝底 JPG。
- 红色损伤中心与 WeldLine 视觉接近：仍保留独立 DamageMarker，以红色损伤中心作为 runtime 唯一目标，禁止退化为 WeldLine 单点判定。
- 宽屏构图过空：新增空间只给 RailArea，不拉伸 SupportArea、波形或 Dock。
- Setup 失败：删除本任务新增的 M4 Scene/Editor 文件，不触碰 M1/M2/M3。
