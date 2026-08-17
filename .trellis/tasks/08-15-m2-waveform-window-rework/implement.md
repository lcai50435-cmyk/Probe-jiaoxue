# M2 波形窗口改造 执行计划

## 0. 开工门禁

- [x] 老板审核并批准 `prd.md`、`design.md`、`implement.md` 后执行 `task.py start`。
- [x] 运行 `trellis-before-dev`，确认 Unity 低代码与冻结 Scene 规范（实施时复核）。
- [x] 记录 `Assets/Settings/Scenes/M2.unity` SHA-256 作为实施基线（工作区当前值，含未提交改动）。
- [x] 不运行任何会写回 M2 Scene 的 Setup/Closeout 工具。

## 1. 新建 `Assets/Scripts/M2WaveformFx.cs`（~140 行）

- [x] 继承 `Graphic`，字段：刻度合同（scanMin/scanMax/ampMin/ampMax/majorDivisions=5）、伤损波联动（appearMm=150/peakMm=115/stopMm=110/startStrength=0.08/peakStrength=0.78/pulseWidth=0.075）、外观（startColor 绿/gridColor 浅绿/baselineColor 绿/bgColor 近黑/lineThickness）、只读状态（Strength/PeakU）。
- [x] `SetDistanceMm(mm)`：三区间高度映射（>150 无波 / 150→115 短波长高 / 115→110 保持最高），`PeakU` 0~200mm 映射，`SetAllDirty()`。
- [x] `ResetWave(mm=150)`：复位状态。
- [x] `OnPopulateMesh`：Fill 深底 → DrawGrid（仅主网格 5 等分，无次网格）→ DrawNoiseBaseline（固定种子锯齿）→ DrawStartWave（竖线 + 常驻脉冲）→ DrawDamageWave（脉冲 at PeakU × Strength）。
- [x] `DrawPulse` 共用函数：陡升 + 指数衰减正弦振荡（始波/伤损波同形同色合同）。
- [x] 无协程/动画，纯状态驱动（暂停天然冻结）。

验证门：离线检查行数 ≤150；无 Scene 写入 API；引用缺失时 LogError 不崩溃。

## 2. 改造 `M2FlowController.cs`（145 → ≤150 行）

- [x] 加字段 `[System.NonSerialized] public M2WaveformFx waveformFx;`。
- [x] `Awake`：`waveformFx = waveform?.gameObject.AddComponent<M2WaveformFx>()`；`if (waveform != null) waveform.enabled = false;`；初始 `waveformFx?.SetDistanceMm(150f)`。
- [x] `Awake` 一次性覆盖 `WaveformArea_B` 尺寸为 4:3（默认 460×345 + y 172.5；备选 320×240）。
- [x] `NotifyDistance(mm)` → `waveformFx?.SetDistanceMm(mm)`。
- [x] `ResetAll()` → `waveformFx?.ResetWave(150f)`。
- [x] 其他流程零改动；`M2WaveformGraphic` 与 M3 零改动。

验证门：Unity 编译无 Error；`wc -l` 两文件 ≤150；M2 Scene SHA 不变。

## 3. 更新 `Assets/Editor/M2RuntimeSmoke.cs`

- [x] 旧橙红断言 → 新断言：`waveformFx != null`、`waveform.enabled == false`、窗口 4:3（sizeDelta 460×345）。
- [x] 联动断言：`SetDistanceMm(150/115/110/100)` 后 `Strength`/`PeakU` 符合合同（0.08@0.75 / 0.78@0.575 / 0.78@0.55 / 检出后锁定）。
- [x] 保留既有流程断言链（耦合剂→定位→扫描→检出→测量→完成）。
- [x] 确认 M3 RuntimeSmoke 不受影响（`M2WaveformGraphic` 零改动）。

验证门：Play Mode 烟测通过（含 Reset 复跑）。

## 4. 更新 `Assets/Editor/M2Shot.cs`（无 Play 截图适配）

- [x] 模拟 Awake 挂载：`WaveGraphic` 节点 `AddComponent<M2WaveformFx>()` + 禁用旧组件 + 反射触发 `OnPopulateMesh`（沿用现有模式）。
- [x] 截图后恢复编辑器临时状态，Scene 哈希校验保持。

验证门：M2Shot 截图波形为新样式（人工确认）；哈希不变。

## 5. 自动化验收

- [x] Unity（6000.3.21f1，有图形设备）编译无 Error + `M2RuntimeSmoke.RunBatch` 通过：
  ```powershell
  & "<Unity.exe>" -batchmode -projectPath . -executeMethod M2.EditorTools.M2RuntimeSmoke.RunBatch -logFile Logs/m2-waveform-rework.log
  ```
- [x] 人工 Play Mode 检查：窗口 4:3；常驻始波/竖线/噪声基线；伤损波 150mm 短波 → 115mm 最高 → 110mm 停止；检出锁定；Reset 复位；QA 暂停冻结。
- [x] 运行 `git diff --check`；确认无新增素材文件、无 M1/M3 Scene 改动。
- [x] 再次计算 M2 Scene SHA-256 == 开工基线；若有差异停止收口并定位写入者，不自动重置。

## 6. 收口

- [x] `trellis-check` 审查：职责边界（Fx 只做波形绘制与联动、Flow 只做流程）、M3 隔离、暂停合同、行数、Scene 哈希。
- [x] 必要时更新 `.trellis/spec/unity/low-code.md` 沉淀「冻结 Scene 的运行时波形组件」模式。
- [x] 老板确认后按 Trellis 流程收口提交（默认不主动 commit，除非老板授权）。
