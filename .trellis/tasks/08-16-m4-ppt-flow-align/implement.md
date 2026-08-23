# Implement：M4 = 复制 M3 基线 + 参数替换

## 0. 基线保护（先行）

- 记录 M3.unity SHA-256（已记录：`e9a7926e...`）。
- 确认 `feature/m4-rail-web` 分支干净。

## 1. 复制 runtime 脚本（M3 → M4）

1. `cp` M3 的 5 个脚本为 M4 命名（FlowController/ProbeDrag/RulerDrag/IdleHelp/DigitalHumanVideo）。
2. 全局替换：`namespace M3` → `namespace M4`、`M3FlowController` → `M4FlowController` 等全部类型引用。
3. 参数替换：
   - FlowController：`targetAngle=10`、`targetDistance=40`；波形 `appearMm=55/peakMm=45/stopMm=40`；`NotifyDistance` 内 `Lerp(160f,120f,...)` → `Lerp(55f,40f,...)`；DefaultHints/StageNames/completionText 改 M4 轨腰文案。
   - ProbeDrag：`scanStartMm=55/scanEndMm=40`；`visualTiltAtTarget=10`；向上方向（`ApplyAngleVisual` 符号反转，见 design 3.1）；`scanStartY` 轨腰标定值（先用 M3 的 107 起调）；`beamLengthZeroMm` 保持默认/Scene 值。
   - RulerDrag：`ruler120Uv` → `ruler40Uv`（像素标定）；`PixelsPerMm` 除法 120→40；`measureAngleDeg=0/positioningAngle=0`（水平）。
   - IdleHelp：自动演示距离/角度文案。

## 2. 复制 Scene（M4.unity）

1. `cp M3.unity M4.unity`；meta 复制改名（Unity 重导生成新 guid）。
2. YAML 替换脚本 guid：M3 脚本 guid（`059f97c6...` 等，查 M3*.cs.meta）→ M4 脚本新 guid（复制后由 Unity 生成，先手写占位或跑一次 Unity 重导）。
3. 标题文案 M3→M4；`M2WaveformFx` 序列化参数改 55/45/40。
4. 探头/尺子初态按几何标定微调（第二步后）。

## 3. 复制 Editor 工具

- M3Setup → M4Setup（只读打开器形态）、M3RuntimeSmoke → M4RuntimeSmoke（断言改 M4 参数）、M3Shot → M4Shot（三视口）、M3FinalCloseout → M4FinalCloseout（如有）。

## 4. 标定与验证

1. 尺子 40mm 刻度 UV：像素采样 `尺子正面.png`（预期 x≈342/1205≈0.284）。
2. 编译验证：Unity batch compile（项目现有方式）或 Editor 打开无报错。
3. Play Mode：M4RuntimeSmoke 全 PASS（波形 55/45/40、射线橙、伤损橙、尺子 40mm 双点、55→40 锁定）。
4. M4Shot 三视口 PNG 非空。
5. M3.unity SHA-256 复验 == 基线；M3 脚本/Editor 文件 git status 无改动。

## 5. 文档与收尾

- 更新 `.trellis/spec/unity/low-code.md`（M4 合同段）与 `ugui-module-template.md`（M4 采用 M3 基线确认）。
- 同步 AGENTS.md 摘要。
- 老板目视确认后提交（Phase 3.4）并归档任务。

## 验收命令

- `sha256sum Assets/Settings/Scenes/M3.unity`（前后一致）
- `git status --short`（M3 三套文件零改动）
- M4 脚本编译 + M4RuntimeSmoke + M4Shot（项目现有 Unity 批处理方式）
