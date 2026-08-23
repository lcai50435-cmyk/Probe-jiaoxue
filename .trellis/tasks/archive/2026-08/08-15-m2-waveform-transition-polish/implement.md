# M2 伤损波联动过渡优化 执行计划

## 0. 门禁

- [ ] 老板选 A+B（已确认）；记录 `M2.unity` SHA-256 基线 = `e64f0f7de8ecc626070802c3a24d4725a90b79e8f4e9c67bd821077d7e28383d`。
- [ ] 复用波形任务验证流程（Unity 6000.3.21f1 batchmode）。

## 1. 改 `Assets/Scripts/M2WaveformFx.cs`（123 → ~133 行）

- [ ] `SetDistanceMm`：高度 `Lerp` → `Lerp(startStrength, peakStrength, SmoothStep(0,1, InverseLerp(appearMm, peakMm, mm)))`；`PeakU` 不动。
- [ ] 新增字段 `noiseAmp = .04f`。
- [ ] `DrawPulse` 加参数 `noiseAmpFrac`：循环内叠加 `Noise(u*3.7f+.5f) * noiseAmpFrac * r.height`；振荡衰减乘 `osc = Lerp(.35,1, InverseLerp(.08,.78,heightFrac))`。
- [ ] 始波调用传 `0f`、伤损波传 `noiseAmp * _strength`。
- [ ] 行数 ≤150；不新增组件/协程。

验证门：离线编译通过（Roslyn rsp）；行数 ≤150。

## 2. 验证

- [ ] M2 RuntimeSmoke 通过（联动断言数值不变：150→0.08@0.75、115→0.78@0.575、110→0.78@0.55、锁定）。
- [ ] M2Shot 截图：伤损波矮/高形态 + 噪声毛刺、始波不变、波形在窗口内。
- [ ] `M2.unity` SHA-256 == 基线；M3 RuntimeSmoke 通过（零改动确认）。
- [ ] `git diff --check`；无新增素材。

## 3. 收口

- [ ] 老板 Play Mode 人工确认平滑效果；trellis-check 审查。
- [ ] 汇报老板后按 Trellis 流程收口提交（默认不主动 commit）。
