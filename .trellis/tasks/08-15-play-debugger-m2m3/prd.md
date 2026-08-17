# 实时数值调试器泛化支持 M2/M3

## Goal

将现有的 `M2PlayDebugger`（Play Mode 实时数值调试器）从 **M2 专用**泛化为 **M2/M3 通用**：同一窗口自动识别当前场景的模块组件，按组件类型展示对应字段组，Play 下改动实时生效、画面即时变化。

## Background

- 现有 `Assets/Editor/M2PlayDebugger.cs`（199 行，Editor 工具，豁免行数限制）：
  - 菜单 `Tools/M2/PlayMode 实时调试器`；
  - Play Mode 下实时改 `M2ProbeDrag`（探头几何：probeEntryLocal/damageUv/startLocal/probeBaseAngleDeg/beamBaseAngleDeg/beamLengthZeroMm/visualTiltAtTarget）与 `M2RulerDrag`（尺子：slotUv/zeroUv/ruler110Uv/measureStartLocal/measureAngleDeg/measureOffset/pointTolerancePx/angleToleranceDeg/retractTolerancePx）字段，画面实时变化；调好后退出 Play 在 Inspector 回填。
  - 通过 `FindFirstObjectByType<M2ProbeDrag>()` / `<M2RulerDrag>()` 查找目标，**M2 组件专用**；M3 场景打开会提示"未找到 M2ProbeDrag/M2RulerDrag"。
- M3 组件字段与 M2 差异大，不能直接复用：
  - `M3ProbeDrag`（`Assets/Scripts/M3ProbeDrag.cs`）：可调 `placementTolerance`(Vector2)、`scanStartMm`/`scanEndMm`/`visualTiltAtTarget`(float)；只读实况 `Placed`/`AngleCorrect`/`CurrentDistanceMm`/`ScanStartLocal`/`ScanEndLocal`；刷新需 `CalibrateTrack()`（私有）+ `AutoMoveToMm(CurrentDistanceMm)`。
  - `M3RulerDrag`（`Assets/Scripts/M3RulerDrag.cs`）：可调 `measureSize`/`positioningStart`/`measureStartLocal`(Vector2)、`snapTolerance`/`positioningAngle`(float)；`zeroAnchorLocal` 为运行时计算（GetRenderedImageLeft 覆盖），仅作只读实况；刷新姿态用公开 `ShowPositioning()` / `Show()`（会重置 positioned/aligned 状态，属预期副作用）。
- M2/M3 Scene 均冻结：调试器只在 Play 下改运行时字段、不保存场景，与冻结合同兼容（沿用现状）。

## Requirements

### R1. 泛化入口

- 新增/改造为通用调试器：菜单入口 **`Tools/PlayMode 实时调试器`**（保留旧 `Tools/M2/PlayMode 实时调试器` 菜单项转发到同一窗口，避免老板既有习惯失效）。
- 文件名与类名去 M2 化：新建 `Assets/Editor/PlayDebugger.cs`，删除 `M2PlayDebugger.cs`（git 可见为 rename/delete + add）。

### R2. 按场景自动识别与分区显示

- Play Mode 下按 `FindFirstObjectByType` 分别查找 4 个组件：`M2ProbeDrag` / `M2RulerDrag` / `M3ProbeDrag` / `M3RulerDrag`。
- 窗口按组件存在性分区显示：
  - 【M2 探头几何】/【M2 尺子】：字段与刷新逻辑 = 现 M2PlayDebugger 原样（零行为回归）。
  - 【M3 探头几何】：字段组见 R3；【M3 尺子】：字段组见 R4。
- 全部找不到 → HelpBox 提示"未找到 M2/M3 探头或尺子组件（是否在对应场景？）"，不崩溃。
- 同一窗口同时兼容 M2/M3 同存场景（不假设互斥，但正常只会有其一）。

### R3. M3 探头字段组（M3ProbeDrag）

- 可调字段：
  - `placementTolerance`（Vector2，放置容差）
  - `scanStartMm` / `scanEndMm`（float，扫描起止 mm）
  - `visualTiltAtTarget`（float，角度视觉倾斜）
- 变化后刷新：反射调用私有 `CalibrateTrack()`（重算扫描线/损伤点）；若 `Placed` 则 `AutoMoveToMm(CurrentDistanceMm)` 保持距离重定位（与 M2 一致）。
- 只读实况：`Placed`、`AngleCorrect`、`CurrentDistanceMm`、`ScanStartLocal`、`ScanEndLocal`。

### R4. M3 尺子字段组（M3RulerDrag）

- 可调字段：
  - `measureSize` / `positioningStart` / `measureStartLocal`（Vector2）
  - `snapTolerance`（float，吸附容差）
  - `positioningAngle`（float，定位姿态角）
- 刷新（公开方法，属预期副作用，HelpBox 注明"重摆会重置对齐状态"）：
  - `positioningStart` / `positioningAngle` 变化且 `positioned` → 调 `ShowPositioning()`；
  - `measureSize` / `measureStartLocal` 变化且 `positioned` → 调 `Show()`；
  - `snapTolerance` 仅判定参数，下次拖拽生效，不刷新。
- 只读实况：`zeroAnchorLocal`（运行时计算值）、`unlocked/positioned/aligned` 状态。

### R5. 边界

- 仅改 Editor 脚本（新增/删除 Editor 文件），**M2/M3 运行时组件零改动、冻结 Scene 零改动**。
- Editor 脚本豁免 150 行限制，但保持精简与现有风格（无冗余抽象、不复制粘贴整段 UI 代码——字段行可压缩成紧凑布局）。
- 不新增素材、不改其他模块代码。

## Acceptance Criteria

- [ ] AC1：M2 场景 Play Mode 打开调试器，M2 字段组完整显示，改动实时生效（与现状行为一致，无回归）。
- [ ] AC2：M3 场景 Play Mode 打开调试器，M3 探头/尺子字段组正确显示，探头参数改动实时生效（CalibrateTrack + 保持距离重定位），尺子姿态参数改动重摆生效。
- [ ] AC3：非 Play 模式与组件缺失场景显示 HelpBox 提示，不抛异常。
- [ ] AC4：`Tools/PlayMode 实时调试器` 与旧 `Tools/M2/PlayMode 实时调试器` 两个菜单项均可打开同一窗口。
- [ ] AC5：Unity 编译无 Error；M2/M3 运行时脚本与冻结 Scene SHA-256 不变；M2/M3 RuntimeSmoke 不受影响。
- [ ] AC6：git 层面为 `M2PlayDebugger.cs` 删除 + `PlayDebugger.cs` 新增（Editor 目录，不产生多余文件）。

## Out Of Scope

- 波形（M2WaveformFx / M2WaveformGraphic）、耦合剂、角度滑块等非几何数值的调参（M2 调试器当前也未含，保持现状）。
- M4/M5 组件支持（未来模块接入时按同模式扩展）。
- 保存调参结果到 Scene（仍走"退出 Play 后 Inspector 回填"人工流程）。
