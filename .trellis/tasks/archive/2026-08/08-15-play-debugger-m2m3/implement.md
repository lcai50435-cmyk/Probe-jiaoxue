# 实时数值调试器泛化支持 M2/M3 执行计划

## 0. 门禁

- [ ] 老板批准 prd 后 `task.py start`（当前已创建并激活，等 review）。
- [ ] 记录 M2/M3 Scene SHA-256 基线（实施前后对比）。
- [ ] 复核 M2PlayDebugger 现有逻辑（199 行）与 M2/M3 组件字段（已核对，见 prd Background）。

## 1. 新建 `Assets/Editor/PlayDebugger.cs`（通用，Editor 豁免行数）

- [ ] 类 `PlayDebugger : EditorWindow`，菜单：
  - `Tools/PlayMode 实时调试器`（主入口，`GetWindow<PlayDebugger>("实时数值调试器")`）
  - `Tools/M2/PlayMode 实时调试器`（旧入口转发到同一窗口）
- [ ] `OnEnable/OnDisable` 挂 EditorApplication.update → Play 中 Repaint（沿用）。
- [ ] 非 Play → Info HelpBox（沿用文案）。
- [ ] Play → 查 4 组件：M2ProbeDrag / M2RulerDrag / M3ProbeDrag / M3RulerDrag；全空 → Error HelpBox。
- [ ] M2 两分区：从现 M2PlayDebugger 原样迁移字段与刷新逻辑（BeginChangeCheck + InvokePrivate CalibrateTrack / ComputeAnchors + 姿态刷新 + 实况区）。
- [ ] M3 探头分区：placementTolerance / scanStartMm / scanEndMm / visualTiltAtTarget；变化 → InvokePrivate CalibrateTrack + `if (Placed) AutoMoveToMm(CurrentDistanceMm)`；实况区 Placed/AngleCorrect/CurrentDistanceMm/ScanStart/ScanEnd。
- [ ] M3 尺子分区：measureSize / positioningStart / measureStartLocal / snapTolerance / positioningAngle；刷新按 prd R4（positioned 时 ShowPositioning/Show，snapTolerance 不刷新）；实况 zeroAnchorLocal/unlocked/positioned/aligned。
- [ ] HelpBox 注明 M3 重摆副作用与"退出 Play 回填 Inspector"流程。

验证门：离线检查字段名与 M2/M3 组件完全一致（编译依赖）；无 Scene 写入 API。

## 2. 删除 `Assets/Editor/M2PlayDebugger.cs`

- [ ] git rm（或删除文件），确保无残留引用（类名不再被其他文件引用——rg 确认）。

验证门：rg "M2PlayDebugger" 无命中（除 git 历史）。

## 3. 自动化验证

- [ ] Unity batchmode 编译无 Error（`-executeMethod` 任一现有 smoke 触发编译即可，或直接 `-batchmode -quit`）。
- [ ] M2 RuntimeSmoke / M3 RuntimeSmoke 通过（确认运行时零改动）。
- [ ] M2/M3 Scene SHA-256 == 基线。
- [ ] `git diff --check`。

## 4. 人工验证（老板或助手 Play Mode）

- [ ] M2 场景 Play → Tools/PlayMode 实时调试器 → 改探头/尺子数值画面实时变化（无回归）。
- [ ] M3 场景 Play → 同窗口 → M3 字段组显示；改 scanStartMm/visualTiltAtTarget/placementTolerance 实时生效；改尺子 positioningStart/measureStartLocal 重摆。
- [ ] 非 Play 打开显示 Info 提示。

## 5. 收口

- [ ] trellis-check 审查（职责边界、M2 行为零回归、M3 字段正确、Scene 哈希）。
- [ ] 老板确认后提交（默认不主动 commit）。
