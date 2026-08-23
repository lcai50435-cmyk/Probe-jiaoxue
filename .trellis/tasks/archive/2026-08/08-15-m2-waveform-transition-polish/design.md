# M2 伤损波联动过渡优化 技术设计

## 1. 设计原则

- 仅改 `M2WaveformFx.cs`（123 → ~133 行）：A 高度 SmoothStep、B 噪声调制 + 峰形演化 + 渐显；位置 `PeakU` 保持精确映射（教学合同）。
- 纯状态驱动无协程（QA/Modal 暂停冻结合同不变）；Scene/M3 零改动。
- 复用现有 `Noise(u)`（固定正弦叠加，无闪烁）与 `DrawPulse`（始波/伤损波共用）。

## 2. 改动明细

### `SetDistanceMm(mm)`（A 平滑）
```csharp
var t = mm >= peakMm ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(appearMm, peakMm, mm)) : 1f;
_strength = mm > appearMm ? 0f : Mathf.Lerp(startStrength, peakStrength, t);
_peakU = Mathf.InverseLerp(scanMinMm, scanMaxMm, Mathf.Clamp(mm, stopMm, appearMm)); // 不变
```
- `SmoothStep` 保证：150mm→0（t=0）、115mm→1（t=1）、中间先缓后快再缓（渐显+到位自然）；烟测数值 0.08/0.78 不破坏（端点值不变）。

### `DrawPulse` 增强（B 真实感）
- 签名加 `float noiseAmpFrac`（噪声幅度，渲染区高比例）：
  - 始波调用传 `0f`（常驻始波保持干净，参考图语义）；
  - 伤损波调用传 `noiseAmp * _strength`（噪声 ∝ 高度：矮波几乎无噪、高波明显毛刺）。
- 循环内：`y += Noise(u * 3.7f + .5f) * noiseAmpFrac * r.height;`（固定种子，无闪烁）。
- 峰形演化：振荡衰减幅度 `osc = Lerp(.35f, 1f, InverseLerp(.08f, .78f, heightFrac))`——矮波振荡弱（干净短波）、高波饱满（多周期回波结构）；始波 heightFrac=0.85 → osc≈1（零变化）。

### 新增字段
```csharp
public float noiseAmp = .04f; // 伤损波噪声幅度（渲染区高比例，Inspector 可调）
```

### 渐显
- 不额外加 `fadeInMm`：SmoothStep 开头段已含渐入缓启动，`startStrength=0.08` 起步保持（烟测断言不变）。

## 3. 行数核算

| 文件 | 当前 | 目标 |
|---|---|---|
| M2WaveformFx.cs | 123 | ~133 ≤150 ✓ |
| M2RuntimeSmoke.cs | — | 断言数值不变（端点值不变），不改 |

## 4. 验证

- M2RuntimeSmoke 联动断言（150→0.08@0.75、115→0.78@0.575、110→0.78@0.55、<110 锁定）**数值不变**，应直接通过。
- M2Shot 截图：伤损波矮波干净/高波毛刺、始波形态不变、波形仍在窗口内（内边距合同）。
- `M2.unity` SHA-256 == `e64f0f7d…`（Scene 零改动）。

## 5. 风险

- **SmoothStep 端点行为**：`SmoothStep(0,1,0)=0`、`SmoothStep(0,1,1)=1` 精确，烟测容差（±0.02/±0.01）不受影响。
- **噪声幅度上限**：`noiseAmp * _strength` ≤ 0.04 × 1 = 4% 渲染区高，叠加在脉冲包络上不越界（内边距已保证）。
- 回滚点：仅 `M2WaveformFx.cs` 单文件，失败还原即可。
