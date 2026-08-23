# M2 波形窗口改造 技术设计

## 1. 设计原则

- **仅 M2、零污染冻结 Scene**：波形视觉由新增 `M2WaveformFx`（程序化 UGUI Graphic，无素材）承担，`M2FlowController.Awake` 运行时 `AddComponent` 挂到冻结节点 `WaveGraphic` 并禁用旧 `M2WaveformGraphic`；一切视觉差异（尺寸、样式、联动）均为运行时覆盖，不写回 Scene。
- **M3 隔离**：`M2WaveformGraphic`（旧样式）与 M3 引用/配置零改动；M3 场景节点照旧。
- **复用优先**：始波与伤损波共用同一脉冲绘制函数（同形同色合同）；深底/网格/线绘制复用现有 `M2WaveformGraphic` 的 Fill/Line 模式。

## 2. 组件架构

### 新增 `Assets/Scripts/M2WaveformFx.cs`（继承 `Graphic`，~140 行）

```csharp
public class M2WaveformFx : Graphic
{
    [Header("刻度合同")]
    public float scanMinMm = 0f, scanMaxMm = 200f;   // X 轴 0~200mm
    public float ampMin = 0f, ampMax = 100f;         // Y 轴 0~100
    public int majorDivisions = 5;                   // 大刻度 5 等分（0/40/80/120/160/200、0/20/40/60/80/100）

    [Header("伤损波联动")]
    public float appearMm = 150f;    // 短波开始出现距离
    public float peakMm = 115f;      // 最高波形距离
    public float stopMm = 110f;      // 检出停止距离
    public float startStrength = 0.08f;   // appearMm 处短波起步峰高
    public float peakStrength = 0.78f;    // 最高峰高（渲染区比例）
    public float pulseWidth = 0.075f;     // 脉冲占窗口宽比例（≈参考图 X 0~15mm/200mm）

    [Header("外观")]
    public Color startColor = 绿;         // 始波/伤损波颜色（同形同色合同）
    public Color gridColor = 浅绿;        // 大刻度主网格
    public Color baselineColor = 绿;      // 底部噪声基线
    public Color bgColor = 近黑;          // 深色底
    public float lineThickness = 2f;

    // 只读状态（供烟测断言）
    public float Strength { get; private set; }   // 0~1
    public float PeakU { get; private set; }      // 伤损波 X 位置（0~1）

    public void SetDistanceMm(float mm);          // 联动入口（Flow/烟测调用）
    public void ResetWave(float mm = 150f);
    protected override void OnPopulateMesh(VertexHelper vh);
    private void DrawGrid(...); private void DrawStartWave(...); private void DrawNoiseBaseline(...);
    private void DrawDamageWave(...); private void DrawPulse(...); // 始波/伤损波共用脉冲形状
}
```

- `SetDistanceMm(mm)`（每次调用重算 `_strength`/`_peakU`，`SetAllDirty()`）：
  - `mm > appearMm` → `Strength=0`（无伤损波）；
  - `appearMm ≥ mm ≥ peakMm` → `Strength = Lerp(startStrength, peakStrength, InverseLerp(appearMm, peakMm, mm))`；
  - `peakMm > mm ≥ stopMm` → `Strength = peakStrength`（保持最高）；
  - `mm < stopMm` → 钳到 `stopMm` 状态（检出锁定，不再变化）。
  - `PeakU = InverseLerp(scanMinMm, scanMaxMm, Clamp(mm, stopMm, appearMm))` → 150mm→0.75、115mm→0.575、110mm→0.55。
- `OnPopulateMesh`：`Fill(bgColor)` → `DrawGrid`（仅主网格 5 等分）→ `DrawNoiseBaseline`（固定种子锯齿）→ `DrawStartWave`（竖线 + 常驻脉冲）→ `DrawDamageWave`（脉冲 at PeakU，高度 Strength）。
- `DrawPulse(vh, rect, x, height)`：始波/伤损波共用——陡升（基线→峰值，占 pulseWidth 前 20%）+ 指数衰减正弦振荡（2~3 个周期，衰减至基线），采样 segments（如 48）逐段描线。
- 深色底沿用 `M2WaveformGraphic.Fill` 的四顶点矩形；网格/线沿用其 `Line` 四顶点描线（私有静态拷贝，避免改旧组件）。
- 无协程/动画：纯状态驱动，天然满足暂停合同（QA/Modal 冻结时无距离输入 → 波形不动）。

### 旧组件处理

- `M2FlowController.Awake`：`waveformFx = waveform != null ? waveform.gameObject.AddComponent<M2WaveformFx>() : null; if (waveform != null) waveform.enabled = false;`（`enabled=false` 后旧 Graphic 不再 `OnPopulateMesh`，节点上仅新组件渲染）。
- `M2WaveformGraphic` 与 M3 保持原样。

## 3. M2FlowController.cs 改动（145 → ≤150 行）

- 加字段：`[System.NonSerialized] public M2WaveformFx waveformFx;`（1 行）。
- `Awake`：挂载新组件 + 禁用旧组件（2 行）；初始 `waveformFx?.SetDistanceMm(150f)`（替换原 `waveform?.SetDistanceMm(150f)`）。
- `NotifyDistance(mm)`：`waveformFx?.SetDistanceMm(mm)`（替换原调用）。
- `ResetAll()`：`waveformFx?.ResetWave(150f)`（替换原 `waveform?.SetDistanceMm(150f)`）。
- 窗口 4:3（运行时，仅在 Awake 一次）：`waveformAreaRt.sizeDelta = new Vector2(460, 345); waveformAreaRt.anchoredPosition = new Vector2(-273, 172.5);`（保下缘贴底；`WaveformArea_B` 引用取 `waveform.transform.parent.parent` 或按节点名查找——WaveGraphic 父为 WaveformArea_B）。备选 320×240（位置不变）。
- 其余流程（耦合剂/定位/扫描/测量/完成）零改动。
- 行数核算：145 + 1（字段）+ 2（Awake）+ 2（替换净 0）+ 2（窗口 4:3）≈ **150**。若超限，将窗口 4:3 覆盖移入 `M2WaveformFx`（其持 `RectTransform` 引用自设尺寸），Flow 保持 145~148。

## 4. 视觉细节

- **主网格**：横竖各 6 条线（5 等分，对应大刻度 0/40/80/120/160/200mm 与 0/20/40/60/80/100），颜色浅绿（参考图大刻度观感，alpha 0.25 附近），线宽 1。**无次网格**。
- **始波**：青绿竖线（X=2% 宽，贯穿 0~100% 高，线宽 1.5）+ 绿色脉冲（X 0~7.5% 宽，峰顶 95% 高，振荡衰减 2~3 个周期）。
- **噪声基线**：Y=2% 高处的固定锯齿（伪随机固定种子或固定正弦叠加，幅度 1.5% 高，周期 8~12），绿色低亮。
- **伤损波**：`DrawPulse` 于 `PeakU`，高度 `Strength`（150mm 时 8% 短波可见 → 115mm 78% 最高 → 保持到 110mm）。
- **4:3 适配**：`WaveformArea_B` sizeDelta 运行时改 460×345 后，stretch 子节点（bg/WaveHeader/WaveGrid/WaveGraphic）自动适配；WaveGraphic 渲染区 460×297；坐标全部基于 `rectTransform.rect` 比例计算，分辨率无关。

## 5. 烟测/截图适配（Editor，行数豁免）

- `M2RuntimeSmoke.cs`：
  - 旧断言（`waveform.waveColor.r>.8 && g<.4` 橙红）→ 替换为：`_flow.waveformFx != null`、`_flow.waveform.enabled == false`、`WaveformArea_B.sizeDelta == (460,345)`（或 320×240，随方案）。
  - 联动断言：`_flow.waveformFx.SetDistanceMm(150f)` → `Strength ≈ 0.08`、`PeakU ≈ 0.75`；`SetDistanceMm(115f)` → `Strength ≈ 0.78`、`PeakU ≈ 0.575`；`SetDistanceMm(110f)` → `Strength ≈ 0.78`、`PeakU ≈ 0.55`；`SetDistanceMm(100f)`（检出后）→ 状态不再变。
  - 保留既有流程断言链（耦合剂→定位→扫描→检出→测量→完成）。
- `M2Shot.cs`（无 Play 截图）：找到 `WaveGraphic` 节点后模拟 Awake 挂载——`AddComponent<M2WaveformFx>()`、禁用旧组件、触发 `OnPopulateMesh`（沿用现有反射模式），截图后恢复（编辑器临时对象不保存，Scene 哈希不变）。

## 6. 行数核算

| 文件 | 当前 | 目标 |
|---|---|---|
| M2WaveformFx.cs（新增） | — | ~140 ≤150 ✓ |
| M2FlowController.cs | 145 | ≤150（+1 字段 +2 Awake +2 窗口，替换净 0） |

## 7. 风险与回滚

- **Scene 污染**：实施前后计算 `M2.unity` SHA-256，变化立即停止定位写入者。
- **M3 回归**：`M2WaveformGraphic` 零改动；M3 RuntimeSmoke 全量通过验证隔离。
- **组件顺序**：`AddComponent` 在 Awake 执行（Flow 引用已序列化）；`waveform` 为空时 LogError 不崩溃（无波形渲染，流程可继续）。
- **窗口裁切**：4:3 后 WaveGraphic 渲染区变高，若与右下数字人/操作带视觉冲突，回退备选 320×240 方案（位置不变）。
- **回滚点**：新组件 → Flow 改造 → 烟测/截图适配，逐块验证；失败只回滚本任务代码。
