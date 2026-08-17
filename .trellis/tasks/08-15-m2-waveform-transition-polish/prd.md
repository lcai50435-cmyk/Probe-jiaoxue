# M2 伤损波联动过渡优化（平滑曲线 + 真实感）

## Goal

优化 M2 波形窗伤损波联动过渡（老板 2026-08-15 反馈「波形变化生硬」）：
- **A 平滑曲线**：伤损波高度随距离的过渡从线性改为平滑（SmoothStep/EaseInOut），长高过程有加速-减速真实感；位置仍精确映射 0~200mm 刻度（教学合同，不平滑化）。
- **B 真实感增强**：伤损波随高度叠加幅度噪声（矮波干净、高波带毛刺）、峰形随高度演化、150mm 出现时从 0 渐显（不突兀冒出）。
- 仅改 `M2WaveformFx.cs` 的绘制与联动；Scene（`M2.unity` 波形窗口区域已直做）与 M3 零改动。

## Background

- 当前 `M2WaveformFx.SetDistanceMm(mm)` 高度映射（`Assets/Scripts/M2WaveformFx.cs`，123 行）：
  - `t = mm >= peakMm ? InverseLerp(appearMm=150, peakMm=115, mm) : 1f`；
  - `_strength = Lerp(startStrength=0.08, peakStrength=0.78, t)`——**线性**；
  - `_peakU = InverseLerp(0, 200, Clamp(mm, 110, 150))`——**位置线性映射**（须保持精确，教学合同：150mm→75%、115mm→57.5%、110mm→55%）。
- `DrawPulse(vh, d, centerU, heightFrac, widthFrac)` 共用函数：陡升 20% + 指数衰减余弦振荡 2.5 周期，48 段采样；始波/伤损波同形同色。
- 波形绘制已加内边距（左右 2%/上下 4.5%，防溢出，`M2WaveformFx` 123 行）。
- 老板 2026-08-15 确认：**A+B 组合**（不含 C 检出过冲反馈）。

## Requirements

### R1. 高度过渡平滑（方案 A）

- 高度映射从线性改为平滑：`s = Mathf.SmoothStep(0f, 1f, InverseLerp(appearMm, peakMm, mm))`，`Strength = Lerp(startStrength, peakStrength, s)`。
- 保持合同：150mm 时短波起步（≈8% 峰高，可配置）、115mm 时最高（78%）、115~110mm 保持最高、<110mm 检出锁定不变。
- **位置 `PeakU` 不平滑化**：仍按 0~200mm 精确映射（150→0.75、115→0.575、110→0.55），保证伤损波始终在正确刻度上。

### R2. 真实感增强（方案 B）

- **幅度噪声调制**：伤损波绘制时按当前 `Strength` 叠加固定种子幅度噪声（振幅 = Strength × 可配置系数，如 0.02~0.05 渲染区高；矮波几乎无噪、高波明显毛刺）。固定种子（同 `DrawNoise` 的正弦叠加模式，无闪烁）。
- **峰形随高度演化**：`DrawPulse` 的峰顶圆润/振荡幅度随 `heightFrac` 变化——矮波（<20%）窄小、高波（≥60%）饱满（可配置两个端点，插值）。
- **150mm 渐显**：`appearMm` 处 `Strength` 起步值从当前 `startStrength=0.08` 改为「渐显」语义——可保留 0.08 起步但过渡在开头段更平缓（SmoothStep 已含），或加显式 `fadeInMm`（如 150→148mm 内 0→0.08）二选一，以平滑不突兀为准（设计定）。
- 始波（常驻）形态零改动。

### R3. 烟测适配

- `M2RuntimeSmoke.cs` 联动数值断言：`SetDistanceMm(150f)` → `Strength ≈ 0.08` 保持（SmoothStep(0)=0 → Lerp(0.08,0.78,0)=0.08 ✓ 不破坏）；`SetDistanceMm(115f)` → `0.78`（SmoothStep(1)=1 ✓）；`SetDistanceMm(110f)` → `0.78`；检出锁定不变。
- 若加 `fadeInMm` 渐显，断言按新起步值更新（150mm 可能 <0.08，烟测容差相应调整）。

### R4. 实现边界

- 仅改 `Assets/Scripts/M2WaveformFx.cs`（≤150 行，当前 123，加 B 真实感预计 135~148，超限则拆 `DrawPulse` 细化参数）与 `Assets/Editor/M2RuntimeSmoke.cs`（断言）。
- **Scene 与 M3 零改动**（`M2.unity` SHA-256 不变 = `e64f0f7de8ecc626070802c3a24d4725a90b79e8f4e9c67bd821077d7e28383d`）。
- 不新增素材、不新增组件、无协程（纯状态驱动，QA/Modal 暂停天然冻结合同不变）。

## Acceptance Criteria

- [ ] AC1：伤损波从 150mm 到 115mm 高度过渡平滑（无线性生硬感），115~110mm 保持最高，110mm 检出锁定；位置始终精确对应刻度（150→75%、115→57.5%、110→55%）。
- [ ] AC2：伤损波随高度叠加噪声（高波明显、矮波干净），峰形随高度演化，150mm 出现平滑不突兀。
- [ ] AC3：始波/竖线/噪声基线/刻度/网格等常驻内容零变化；波形仍完全在窗口内（内边距合同保持）。
- [ ] AC4：`M2WaveformFx.cs` ≤150 行；Unity 编译无 Error。
- [ ] AC5：`M2.unity` SHA-256 == `e64f0f7d…`；M3 及 M3 RuntimeSmoke 零改动/通过。
- [ ] AC6：M2 RuntimeSmoke 通过（联动断言按 R3 更新）。

## Out Of Scope

- 方案 C（110mm 检出过冲/闪烁反馈）——老板未选，另议。
- 位置移动平滑化（破坏 0~200mm 刻度合同，不做）。
- 始波/常驻波形形态调整、M3 波形、刻度文字、Scene 结构。
