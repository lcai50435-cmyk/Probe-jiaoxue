# M2 轨头顶面探测流程重构技术设计

## 1. 设计原则

本次不新增视觉和 runtime 脚本。核心是把旧的“拖拽进度、探头中心、损伤中心、尺子左缘”多套判定，收敛为一套共享几何合同：

```text
ProbeEntryPoint ── calibrated 110mm span ── DamagePoint
       │                                      │
       ├─ Beam start                          ├─ Beam hit target
       ├─ Ruler 0mm target                    ├─ Ruler 110mm target
       └─ Scan distance origin                └─ Red damage center
```

`M2FlowController` 仍是流程和成功状态唯一所有者；探头、尺子、波形和帮助组件只执行职责并报告事件。

## 2. 流程状态

保留公开四阶段，不增加页面步骤；定位阶段增加内部条件：

```text
Couplant
  -> Positioning
       ProbePlacedAt0
       AngleSliderCorrect
       RulerAngleAligned
  -> Scanning
       BeamHitAt110
  -> Measuring
       RulerZeroAligned && Ruler110Aligned
  -> Completed
```

建议在 Flow 中维护 `AngleVerifiedByRuler`；进入 `Scanning` 的条件是 `Placed && AngleCorrect && AngleVerifiedByRuler`。尺子校角成功后由 Flow 锁定 Slider、隐藏/归槽尺子并进入扫描。Reset 统一清空这些条件。

## 3. 几何标定

### 3.1 坐标空间

所有判定统一转换到 `RailViewport` 本地像素空间。不要直接比较不同父级下的 `anchoredPosition`。

- `DamagePoint`：继续从 `RailPerspective` Sprite 中红色损伤的归一化素材坐标换算（`damageUv=(0.4808,0.63)`，2026-08-14 老板反馈后由红椭圆质心 `0.711` 下移至椭圆下部，视口本地约 `(-24,+93)`，使探头下移贴普通视图钢轨踏面）。
- `ProbeEntryPoint`：由 `probeRt` 上配置化的本地归一化锚点换算；**锚定探头发射面（`probeEntryLocal=(0.89,0.04)`，probeFootage 右下楔形，像素验证）**，使校角时发射面卡入尺子 10°槽、主体在尺子左侧无遮挡，测量时尺子 0mm 端轻触发射面、探头与尺子平行无遮挡，检测束从发射面视觉位置发出。
- **扫描线拉平（2026-08-14 老板确认）**：探头入射点与 `DamagePoint` 必须位于同一水平线 `scanLineY = damage.y`；`startLocal` 不再作为几何距离依据（旧值 `(-500,-18)` 与 damage 的欧氏距离为 182mm，150mm 起点从未真正成立），150mm 起点由 `damage - scanDirection*150*ppm` 反算，保证 150→110 合同严格成立。
- 尺子锚点：正式 `尺子正面.png`（1205×213）底边基线锚点——0mm 左端底尖 `(0.005,0.038)`、110mm 竖刻线与底边交点 `(0.73,0.038)`、10°槽尖角 `(0.005,0.136)`（像素验证左端斜面不透明带 x=0–60 y=[176,211] 与此吻合）。坐标均为 Unity 底左原点 UV，运行时结合 `preserveAspect` 的实际渲染矩形换算成本地像素。禁止把透明区域、`110mm` 字样下沿或不同高度点当测量锚点。
- 尺身基准方向：使用 `rulerRt.TransformVector(Vector2.right)`；钢轨方向使用扫描轨迹方向或 `WeldLine` 的法向，二者夹角进入配置容差即视为平行。
- 1920 基准下 `RailViewport` 约 1248×712px，尺子工作态统一 `420x91`（校角与测量同尺寸，2026-08-14 老板确认），同一底边基线的 `0→110` 二维欧氏跨度约 304.5px，`pixelsPerMm≈2.768`。150mm 起点和 110mm 检出点分别需 415px、304.5px，含 120px 宽探头均能落在视口内，无需修改 Scene。

### 3.2 像素与毫米

尺子 `0mm → 110mm` 的渲染像素跨度是唯一比例来源：

```text
pixelsPerMm = distance(rulerZero, ruler110) / 110
scanDistanceMm = distance(probeEntry, damagePoint) / pixelsPerMm
```

扫描从 150mm 起点移动到 110mm 检出点，并在命中时锁定：

```text
scanStart = damagePoint - scanDirection * 150 * pixelsPerMm
hitPoint  = damagePoint - scanDirection * 110 * pixelsPerMm
```

方向符号应由当前 Scene 的扫描朝向配置，不写死为左右。旧 `CalibrateTrack` 中“损伤中心 = 80% 进度 = 探头中心”逻辑删除。当前读数可继续驱动 B 区波形，但必须由上述几何距离反算。

### 3.3 110mm 检出

检出需同时满足：

1. Flow 处于 `Scanning`，探头角度仍在 10°容差内；
2. `abs(scanDistanceMm - 110) <= distanceToleranceMm`；
3. 绿色检测束线段与以 `DamagePoint` 为中心的命中圆相交。

Beam 的起点为 `ProbeEntryPoint`，长度为尺子 110mm 标定跨度，方向朝 `DamagePoint` 更新。角度 Slider 仍驱动探头 10°教学代理旋转，但不应再让一条固定长度、固定 pivot 的 UI 线与实际命中判定分离。普通视图可隐藏 Beam 图像，命中计算仍保持一致。

## 4. 尺子双模式

`M2RulerDrag` 增加内部模式，复用同一 Scene 尺子：

```csharp
public enum Mode { Home, AngleGuide, DistanceMeasure }
```

### AngleGuide

- 进入条件：探头已以 0°正确放置。
- 尺子从 Home 重挂到 `RailViewport`，使用现有测量态尺寸或独立配置尺寸，不改变 Scene 序列化初态。
- 拖动时同时计算：10°槽到探头校角锚点误差、尺身与钢轨平行角误差、Slider 与 10°误差。
- 三项均通过后吸附，向 Flow 报告校角成功，然后自动 `ResetTool()` 归槽。

### DistanceMeasure

- 进入条件：已经检出并点击现有下一步按钮。
- **水平放置（2026-08-14 老板确认）**：进入测量态时尺子 `localRotation = 0`（与探头移动方向/钢轨平行），删除按 `zero→110` 与 `ProbeEntryPoint→DamagePoint` 向量夹角的自动定向（旧实现因 damage 与 entry 不同线而斜放、盖住探头）；拉平扫描线后两点同线，水平尺子的 0mm/110mm 锚点自然同时命中。
- 同时计算 `rulerZero → ProbeEntryPoint` 和 `ruler110 → DamagePoint` 两个误差；两项均通过才吸附并报告完成，任一单点通过不得完成。
- 不再引用 `WeldLine` 作为成功目标。可保留字段以兼容已有 Scene 序列化，但 runtime 不用于新判定，后续清理必须遵守 Scene 冻结。

### 尺子统一尺寸

- 校角（`ShowAngleGuide`）与测量（`ShowMeasure`）统一使用 `measureSize=420x91`；`angleGuideSize` 字段保留（Scene 已序列化，删除会破坏反序列化）但运行时不再使用。

### 波形简化

- 运行时隐藏 `WaveStateText`/`CurrentDistanceText`（删「峰值锁定/目标 110mm/平直基线/112mm/当前距离」提示词），删除 Flow 中 `waveStateText`/`currentDistanceText` 写入逻辑；隐藏 `WaveGrid` 避免双重网格。
- `M2WaveformGraphic` 程序化绘制：深灰底 + 浅黄绿主网格/青色次网格 + 橙红波形（平直基线 + 110mm 尖峰，检出后锁峰），距离合同 150→110（删除 100 终点与峰后下降段）；不加标题。
- `MeasurementBubble` 中序列化的 `110mm` 文本运行时改为完成反馈文案（不写回 Scene）。

尺子的 Home 初态继续由 `Awake` 缓存，Reset/完成出口恢复缓存，不写死 Scene 坐标。

## 5. 组件职责

### `M2FlowController`

- 保存 `AngleVerifiedByRuler`、`Detected`、`Measured` 等流程事实。
- 接收尺子校角/测量事件和探头几何检测事件。
- 负责阶段推进、Slider 锁定、蜂鸣一次、步骤文案运行时更新、Reset。
- 删除基于 `_prevMm` 跨越 110 的检出因果；距离通知只更新波形（不再写提示词文本）。

### `M2ProbeDrag`

- 管理放置、10°视觉、扫描轨迹、ProbeEntryPoint、DamagePoint、毫米换算和 Beam 几何。
- 在定位阶段角度可调；校角成功后只允许纵向扫描。
- 每次移动报告当前几何距离和是否命中，不直接蜂鸣或推进阶段。
- 对外提供尺子与烟测可读取的 `ProbeEntryPointInRail`、`DamagePointInRail`、`PixelsPerMm`。

### `M2RulerDrag`

- 缓存/恢复 Scene Home；标定正式 Sprite 三锚点。
- 实现 `AngleGuide` 和 `DistanceMeasure` 两种工作模式。
- 仅报告 `OnAngleAligned` / `OnDistanceAligned`，不拥有流程状态。

### `M2WaveformGraphic`

- 消费 150→110mm 几何距离并绘制基线、生长和 110mm 锁定峰值；目标玩法不再进入峰后下降区间。
- 不参与命中判定。若现有 API 足够则不修改。

### `M2IdleHelp`

- 30 秒演示通过公开 API依次放置、调角、移动尺子到校角姿态；不得直接调用 Flow 成功通知绕过几何判定。
- 60 秒演示通过 `AutoMoveToMm(110)`，该 API 使用新几何比例。
- QA/Modal 暂停期间沿用 `Time.deltaTime` 停止推进。

### `M2RuntimeSmoke`

- 更新旧“110mm = 80%探头中心落到损伤”断言。
- 增加 Slider 单独 10°不能过关、尺子校角后过关、单点测量失败、双点测量成功、110mm 间距和 Beam 命中断言。
- 只在 Play Mode 驱动公开/测试入口，不保存 Scene。

## 6. 配置与兼容

- 现有 Scene 字段引用必须继续可反序列化；新增 Inspector 字段提供能在当前 M2 工作态尺寸下工作的默认值。
- 因 Scene 冻结，不能依赖把新增字段写入 Scene。实现必须让默认字段值可用，并在缺少现有节点时 `Debug.LogError` 后停止推进，不能动态创建视觉节点。
- `stepHints` 的默认测量提示改为“0mm 对准探头入射点，110mm 对准损伤”；Scene 已序列化旧数组时，Flow 可在阶段进入时使用代码默认合同更新现有 `instructionText`，但不得保存 Scene。
- 冻结 Scene 中现有 150/100 波形刻度不写回修改；运行时扫描在 110mm 检出后结束，因此 100mm 仅是旧视觉刻度，不是可到达的玩法终点。
- QA、数字人、普通/透视按钮、完成出口和 M1→M2 配置保持原合同。

## 7. 风险与回滚

- **素材标定误差**：三锚点已有像素/UV 初值和空间预算；实施先用烟测数值验证，再做人工视觉复核。比例使用 preserveAspect 渲染矩形中同一可见刻度基线上两锚点的二维欧氏距离；若两点高度不同，视为标定错误而不是有效斜距。仅允许微调代码默认 UV，不修改 Sprite 或 Scene。
- **分辨率映射**：1248px 预算基于 1920 逻辑画布；CanvasScaler 会等比缩放到目标视口。三视口验收仍需确认 SafeArea 下几何换算一致。
- **150 行限制**：Flow/Probe 已满 150 行。实施时以删除旧逻辑、压缩重复帮助方法和职责转移换空间；不以新增脚本绕过限制。
- **工作区 Scene 污染**：实施前后计算 SHA-256；一旦变化立即停止，不自动还原用户改动，由差异定位写入者。
- **回滚点**：每完成 Flow/Probe/Ruler 一个职责块即运行 Unity 编译和最小烟测；失败时只回滚本任务对应代码块，不操作用户已有 Scene 变化。
