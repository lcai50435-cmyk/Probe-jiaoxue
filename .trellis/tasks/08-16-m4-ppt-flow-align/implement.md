# M4 轨腰探测流程对齐 PPT + 复用 M2 —— 执行清单

## 0. 基线保护

- [ ] 记录 `Assets/Settings/Scenes/M2.unity`、`M3.unity`、M4 依赖素材的 SHA-256。
- [ ] 运行现有 M2/M3 RuntimeSmoke，确认实施前基线通过。
- [ ] 确认不修改冻结 Scene；M4 全部使用新建 Scene/Setup。

## 1. M2 脚本参数化（保持 M2 行为不变）

- [ ] `M2FlowController.cs`：
  - 增加 `autoCouplant`、`scanStartMm`、`stepHints`、`stageNames`、`completionMessage`、`normalSpriteName`、`perspectiveSpriteName`、`visibleStepCount`。
  - `Awake` / `ResetAll` / `SwapRailSprites` / `UpdateUi` 改用配置。
  - `autoCouplant=true` 时自动播放耦合剂并进入定位。
  - 默认值保持 M2 当前行为。
- [ ] `M2RulerDrag.cs`：
  - 增加 `rulerTargetUv`、`rulerTargetMm`。
  - `ComputeAnchors` / `CheckMeasure` 使用目标锚点。
  - 默认 `rulerTargetUv=zero` 时回退 `ruler110Uv`，默认 `rulerTargetMm=110`。
- [ ] `M2IdleHelp.cs`：
  - 帮助文案参数化，M4 可配置“10° / 40mm”。
- [ ] 编译无 Error；M2RuntimeSmoke 通过；M2/M3 Scene SHA-256 不变。

## 2. 新建 M4 Scene / Setup

- [ ] 新增 `Assets/Editor/M4Setup.cs`：
  - 幂等创建 `Assets/Settings/Scenes/M4.unity`。
  - 按 M3 正面视角骨架生成 RailArea / SupportArea / ControlDock / QALayer / DigitalHumanStage / ModalLayer。
  - 注入 `Assets/railwayTracks_2/正视角.png` / `正视角透明.png`、`Assets/probeFootage/probeFootage.png`、`尺子正面.png`。
  - 将 `正视角.png` / `正视角透明.png` 同步到 `Assets/Resources/`（或等价处理），保证 `M2FlowController.SwapRailSprites` 的 Resources 加载可用。
  - 生成 4:3 波形窗口，挂 `M2WaveformFx`，配置 55/45/40。
  - 挂载 M2 组件族：`M2FlowController`、`M2ProbeDrag`、`M2RulerDrag`、`M2IdleHelp`、`M2CouplantFx`。
  - 配置 M4 文案、`autoCouplant=true`、`scanStartMm`、`targetAngle=10`、`targetDistance=40`、`damageUv`、尺子 40mm 锚点、素材名。
- [ ] 新增 `Assets/Editor/M4Shot.cs`：三视口截图 + 像素断言 + Scene SHA-256。
- [ ] 连续执行 M4Setup 两次，M4 Scene SHA-256 稳定。
- [ ] M1/M2/M3 Scene 与 Build Settings 哈希不变。

## 3. 几何标定

- [ ] 在 `尺子正面.png` 上像素标定 40mm 锚点，记录 UV。
- [ ] 在 `正视角透明.png` 上标定红色损伤中心 UV。
- [ ] 按“轨腰左上端”视觉位置反算 `scanStartMm`。
- [ ] 验证 `pixelsPerMm = distance(0mm, 40mm) / 40`。
- [ ] 验证探头入射点与红色损伤中心扫描线在同一水平线。

## 4. M4 运行时验证

- [ ] 新增 `Assets/Editor/M4RuntimeSmoke.cs`。
- [ ] 覆盖：
  - 2s 自动耦合剂 → 定位
  - 0° 放置 + 尺子 10° 校角 + 撤尺 → 扫描
  - 波形：55mm 短波、45mm 最高、40mm 锁定
  - 40mm 检出：射线橙色、伤损橙色、蜂鸣一次、探头锁定
  - 尺子 0/40 双点测量
  - Reset 复跑
  - QA/Modal 暂停
  - M5 未配置时“下一模块待接入”
- [ ] M4Shot 三视口输出且通过非空像素断言。

## 5. 回归与提交

- [ ] M2RuntimeSmoke 通过。
- [ ] M3RuntimeSmoke 通过（若 M3 未接 QA/数字人，至少确认不受影响）。
- [ ] M2/M3 Scene SHA-256 与基线一致。
- [ ] `git diff --check` 通过。
- [ ] 更新 `.trellis/spec/unity/` 与 `AGENTS.md` 中的 M4 相关合同摘要。
- [ ] 提交前再次运行 `task.py validate`。

## 回滚

- M2 参数化破坏 M2：回滚 M2 脚本改动，M4 改用独立 M4 脚本方案。
- M4Setup 生成失败：删除新增 M4 文件，不触碰 M2/M3。
- 几何标定不准：只调整 M4 Scene 配置/Setup 参数，不修改素材。
