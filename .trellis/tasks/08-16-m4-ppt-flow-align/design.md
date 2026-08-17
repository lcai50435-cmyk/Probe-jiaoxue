# M4 轨腰探测流程对齐 PPT + 复用 M2 —— 技术设计

## 1. 目标

在不复制 M2/M3 状态机的前提下，通过参数化复用 M2 组件族，搭建 M4 轨腰探测模块。M2 冻结 Scene 行为保持不变，M4 使用新建 Scene。

## 2. 组件复用策略

| 组件 | 用途 | M4 处理 |
|---|---|---|
| `M2FlowController` | 流程状态唯一所有者 | 参数化：`autoCouplant`、`scanStartMm`、文案数组、素材名、完成文案 |
| `M2ProbeDrag` | 放置、角度、扫描、检测束、几何距离 | 配置 M4 参数：`startMm=80`（视觉起点反算）、`hitMm=40`、正面视角 `damageUv`、向上 10° 视觉 |
| `M2RulerDrag` | 同一把尺：10° 校角 + 0/40mm 复测 | 增加 `rulerTargetUv` + `rulerTargetMm`；M4 用 40mm 锚点 |
| `M2WaveformFx` | 波形窗口/始波/伤损波 | 不改代码；M4 Scene 配置 `appearMm=55, peakMm=45, stopMm=40` |
| `M2CouplantFx` | 2s 耦合剂薄膜动画 | 由 `M2FlowController.autoCouplant` 自动触发 |
| `M2IdleHelp` | 30s/60s 防卡死自动演示 | 帮助文案参数化，M4 用 10°/40mm |

## 3. 流程状态

```text
Intro(auto 2s couplant)
→ Positioning
   ProbePlacedAt0
   RulerAngleAligned(10°槽 + 平行)
   AngleVerified(10° 稳定)
   RulerRetracted
→ Scanning
   BeamGreen
   Waveform 55→45→40
   DetectAt40
→ Measuring
   Ruler0@ProbeEntry && Ruler40@DamagePoint
→ Completed
```

M4 步骤显示为 3 步：

```text
1/3 探头定位与偏角
2/3 移动探测
3/3 尺子测距
```

## 4. M2FlowController 参数化设计

新增/调整字段，默认值保持 M2 现有行为：

```csharp
public bool autoCouplant;                 // false=M2 按钮涂抹，true=M4 自动 2s
public float scanStartMm = 150f;          // M2=150，M4 由轨腰左上端反算（初始 80）
public string[] stepHints;                // 为空时使用 M2 默认文案
public string[] stageNames;               // 为空时使用 M2 默认阶段名
public string completionMessage = "轨头顶面探测完成";
public string normalSpriteName = "俯视角";
public string perspectiveSpriteName = "俯视角透视";
public int visibleStepCount = 4;          // M2=4，M4=3（不显示 Couplant 步骤）
```

- `Awake` 初始化 `waveformFx?.SetDistanceMm(scanStartMm)`。
- `ResetAll` 使用 `waveformFx?.ResetWave(scanStartMm)`。
- `autoCouplant=true` 时 `Awake` 自动调用 `couplantFx.Play(OnCouplantDone)`，`applyButton` 可不绑定/隐藏。
- `SwapRailSprites` 使用 `normalSpriteName/perspectiveSpriteName`。
- `UpdateUi` 使用 `stepHints/stageNames/visibleStepCount`。

## 5. M2RulerDrag 40mm 参数化

保持 M2 字段兼容，新增：

```csharp
public Vector2 rulerTargetUv = Vector2.zero;  // 0 表示使用旧 ruler110Uv
public float rulerTargetMm = 110f;
```

- `ComputeAnchors()`：
  - `targetUv = rulerTargetUv == Vector2.zero ? ruler110Uv : rulerTargetUv`
  - `_rTarget = AnchorAt(size, targetUv)`
  - `PixelsPerMm = distance(_zero, _rTarget) / rulerTargetMm`
- `CheckMeasure()` 使用 `_rTarget.x - _zero.x` 作为目标跨度。
- M4 配置：
  - `rulerTargetUv ≈ (0.267, 0.038)`（`尺子正面.png` 的 40mm 竖刻线底端，实施时像素复核）
  - `rulerTargetMm = 40`
  - `measureAngleDeg = 0`
  - `measureOffset = Vector2.zero`

## 6. M2ProbeDrag M4 配置

```text
startMm = 80            // 初始值，最终按轨腰左上端反算
hitMm   = 40
damageUv = 正面视角红色损伤中心 UV（与 M3 正面视角同图，实施时复核）
visualTiltAtTarget = 10 或 -10，以“向上 10°”视觉为准
probeBaseAngleDeg / beamBaseAngleDeg 按 M4 探头发射面标定
```

- 射线颜色机制不动：`beamColor` 绿色、`beamDetectedColor` 橙色。
- 检出后 Flow 负责将伤损颜色改为橙色；Reset 恢复红色。

## 7. 伤损变色

- M4 需要独立 `DamageMarker` 或运行时可直接改色的伤损 Image。
- 未检出：红色。
- 检出：橙色。
- Reset：红色。
- 该逻辑放在 `M2FlowController`（或 M4 配置的 `damageMarker` 引用）中，不写回 M4 Scene 序列化状态。

## 8. 波形

- M4 Scene 的 `WaveformArea_B` 结构复制 M2 的 4:3 波形窗口。
- `M2WaveformFx` 序列化参数：
  ```text
  scanMinMm=0
  scanMaxMm=200
  appearMm=55
  peakMm=45
  stopMm=40
  startStrength=0.08
  peakStrength=0.78
  pulseWidth=0.075
  ```
- `M2FlowController.NotifyDistance(mm)` 直接驱动 `waveformFx.SetDistanceMm(mm)`。
- 40mm 检出后不再更新，锁定波形。

## 9. 素材与 Scene

- 钢轨：`Assets/railwayTracks_2/正视角.png` / `Assets/railwayTracks_2/正视角透明.png`
- 探头：`Assets/probeFootage/probeFootage.png`
- 尺子：`尺子正面.png`
- 波形：M2 风格深色仪器屏 + 0~200mm / 0~100 刻度
- 新建 `Assets/Settings/Scenes/M4.unity`
- 新建 `Assets/Editor/M4Setup.cs`：幂等生成 M4 静态 Scene + 挂载 M2 组件族
- 新建 `Assets/Editor/M4Shot.cs`：三视口截图
- 新建 `Assets/Editor/M4RuntimeSmoke.cs`：Play Mode 烟测

## 10. 兼容与风险

- M2 脚本新增字段必须带 M2 默认值，M2 Scene 不重新序列化也能保持现状。
- 不修改 `M2.unity` / `M3.unity`，实施前后校验 SHA-256。
- M2RuntimeSmoke 必须继续通过，防止参数化破坏 M2。
- 40mm 尺子锚点需在 `尺子正面.png` 上做像素级标定；不能靠目测。
- “轨腰左上端”起点以 PPT 视觉为准；最终 `startMm` 由几何反算后写进 M4 Scene/配置。

## 11. 验收工具

- `M4RuntimeSmoke` 覆盖：
  - 自动耦合剂进入定位
  - 0° 放置 + 10° 校角 + 撤尺
  - 波形 55/45/40 状态
  - 40mm 检出后射线橙色、伤损橙色、探头锁定
  - 尺子 0/40 双点测量
  - Reset 复跑
  - QA/Modal 暂停
- `M4Shot` 覆盖 1920x1080 / 1280x720 / 2436x1125。
