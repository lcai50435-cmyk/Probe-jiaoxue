# Design：M3 轨头侧面探测按 PPT 对齐流程与波形

## 决策记录（老板已确认）
- 检出伤损后保留“尺子测距”阶段。
- 允许修改 M3 Scene，且画面更新 Scene / Play 同步。
- 峰值点先用 `123mm`。
- 目标点以“伤损”为主。
- 探头起始方向/左右侧按 PPT 调整。

## 总览
```
Intro(可选保留) → Positioning(13°定位) → Scanning(160→120mm 检出锁定) → Measuring(尺子0/120双点) → Completed
```

## 1. Scene 同步改动（M3.unity，老板已授权）
### 1.1 波形窗口
- `WaveformArea_B` 参照 M2 定稿：
  - `sizeDelta = 460x345`，`anchoredPosition.y = 172.5`，保下缘贴底。
  - 删除/隐藏旧 `WaveHeader`、`WaveLine`、`TargetMarker`、`Scale150`、`Scale100`。
  - 旧 `WaveGrid` 子线段移除，`WaveGrid` 全 stretch 并序列化挂载 `M2WaveformFx`（带 `RequireComponent(CanvasRenderer)`）。
  - 新增 `ScaleTexts`：横轴 0.0/40.0/80.0/120.0/160.0/200.0mm，纵轴 0.0/20.0/40.0/60.0/80.0/100.0；锚点/字体参照 M2.unity。
- M3FlowController 的 `waveform` 旧引用替换为 `waveformFx`（M2WaveformFx）。

### 1.2 尺子素材
- `Ruler/bg` 的 Sprite 设为 `Assets/Resources/尺子正面.png`（与 M2 同一素材，含 10°/13° 槽和 0/40/110/120mm 刻度）。
- 删除 `Ruler` 下的 `ScaleText`（“0 50 100 150”）占位，避免与正式尺重叠。
- RulerHome 初态尺寸/锚点保持现有 Scene 值；工作态 `measureSize = 420x91`。

### 1.3 探头起始位
- 按 PPT 将探头起始放在钢轨左侧轨头侧面、无偏角。
- 具体坐标由 M3ProbeDrag 几何标定后确定；若 Scene 初态与代码起点冲突，以 PPT/老板现场目视为准微调。

## 2. Runtime 改动
### 2.1 M3FlowController
- 使用 `M2WaveformFx waveformFx` 替代 `M2WaveformGraphic waveform`。
- 默认步骤提示数组改为：
  - “将 K2.5 探头放置在轨头侧面，无偏角”
  - “用定位尺向下偏转 13°”
  - “向前移动探头至入射点距伤损 120mm”
- `targetDistance = 120`。
- `NotifyDistance` 调用 `waveformFx.SetDistanceMm(mm)`。
- 检出后 `Go(Measuring)` 并锁定探头；`ResetAll` 调用 `waveformFx.ResetWave(160f)`。
- 不再更新 `waveStateText` / `currentDistanceText`（旧节点已删除/隐藏）。

### 2.2 M3ProbeDrag
- `scanStartMm = 160`，`scanEndMm = 120`。
- 引入 M2 式像素几何：
  - `PixelsPerMm` 来自 `M3RulerDrag.PixelsPerMm`（0→120 标定）。
  - 伤损目标点从 `RailPerspective` / `DamageMarker` 标定。
  - 扫描线 y 与伤损同线，起点在伤损左侧 160mm，终点在伤损左侧 120mm。
  - 使用 `RailViewport` 中心原点局部像素承载位置（与 M2/UGUI 模板一致）。
- 检出后 `MoveToProgress` 锁定在 120mm，不再继续到 100mm。
- 射线：
  - 复用 M2 程序化渐变射线 Sprite（绿色）。
  - 检出时 IncidentBeam / ReflectedBeam 切换为橙色。
  - Reset 恢复绿色。
- 射线终点/截断：优先让入射束终点落在伤损或轨缘，不做固定长度穿模；具体按 M3 透视图素材校验。

### 2.3 M3RulerDrag
- 使用 `尺子正面.png` 标定：
  - `zeroUv`：0mm 左端底边。
  - `ruler120Uv`：120mm 绿色刻度。
  - `slot13Uv`：右侧 13° 槽位（用于定位阶段视觉/吸附，若采用夹具式校角）。
- `PixelsPerMm = distance(zero, ruler120) / 120`。
- 测量阶段双点校验：
  - 0 刻度对齐探头入射点。
  - 120mm 刻度对齐伤损点。
  - 吸附后播放正确音效并进入完成。
- 定位阶段：保留现有 AngleGuide 定位目标，但使用正式尺素材；如后续需要更接近 M2 的夹具式校角，再补 13° 槽吸附。

### 2.4 M3IdleHelp
- 自动演示更新为 160→120mm，到达 120 后停止并进入测量/完成。

### 2.5 M3RuntimeSmoke
- 更新断言：
  - 初始距离 160mm。
  - 120mm 检出时探头位置为扫描终点（progress≈1）。
  - 检出后继续调用 `AutoMoveToMm(120f)` 不再移动。
  - `M2WaveformFx` 参数 `appearMm=160 / peakMm=123 / stopMm=120`。
  - 波形在 160/123/120 的 Strength/PeakU 断言。
  - 射线颜色检出后变橙。
  - 尺子测量双点校验通过。

## 3. 复用清单
| 复用点 | 来源 | 用途 |
|---|---|---|
| `M2WaveformFx` | M2 | M3 波形绘制，只改参数 |
| M2 波形窗口结构 | M2.unity | M3 Scene 波形区域参考 |
| 射线渐变 Sprite / 绿→橙 | M2ProbeDrag | M3 射线反馈 |
| 尺子素材 `尺子正面.png` | Assets/Resources | M3 正式尺/角度槽/120mm 刻度 |
| 尺子 mm 标定 / 双点测量 | M2RulerDrag | M3 测距校验 |
| 检出锁定 | M2ProbeDrag/M2FlowController | M3 120mm 停止 |
| 默认提示数组覆盖 | M2FlowController.DefaultHints | 冻结 Scene 文案不写回 |

## 4. 风险与注意事项
- M3 Scene 已冻结；本次修改经老板授权，但必须小心 YAML 手改，改后校验块头配对和 Unity 打开。
- M3 透视图素材的伤损/轨缘/发射面锚点需要实际标定；若素材与 PPT 不一致，需以老板目视微调。
- 波形窗口改 460x345 可能影响右上数字人布局；若重叠，优先保持 M3 现有 460x240 或请老板确认尺寸。
