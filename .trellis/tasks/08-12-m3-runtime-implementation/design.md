# M3 轨头侧面探测玩法流程重构技术设计

## 1. 设计原则

本次不新增 M3 runtime 脚本和视觉节点。保留现有组件边界，把旧的“60%进度、探头中心、尺子左缘、AngleGuide 单点”收敛为 RailViewport 本地像素空间中的几何合同：

```text
Surface contract:
ProbeEntryPoint ---- 120mm ---- WeldIntersection
       |                              |
       +-- ruler 0mm                  +-- ruler 120mm

Internal hit contract:
ProbeEntryPoint -- beam along -13deg --> DamageHitZone
```

表面距离与内部命中独立计算，由 `M3FlowController` 组合成唯一检出条件。

## 2. 状态机

```text
Intro
  -> Positioning.PlaceProbe
  -> Positioning.AlignAngle
  -> Scanning
  -> DetectedAwaitNext
  -> Measuring
  -> Completed
```

公开 enum 可继续保留 `Intro/Positioning/Scanning/Measuring/Completed`；`Positioning` 的放置/校角子状态和 `Scanning` 的 DetectedAwaitNext 用布尔事实表达，避免增加页面步骤。

| 状态 | 输入 | 离开条件 | 关键行为 |
|---|---|---|---|
| Intro | 无 | 2 秒 scaled 计时 | 所有业务输入锁定 |
| PlaceProbe | 仅探头 | 0°探头进入 150mm 起始容差 | 解锁 Slider 与尺子 |
| AlignAngle | Slider + 尺拖动 | 13° + 槽位 + 平行 | 尺归槽、Slider 锁定、显示 Beam |
| Scanning | 探头沿扫描轴 | 13° + 120mm + BeamHit | 蜂鸣一次、探头/峰值锁定、显示 Next |
| AwaitNext | NextButton | 点击 | 隐藏 Next，尺进入测量态 |
| Measuring | 尺拖动 | 0/120 双点均通过 | 吸附、正确音效、显示结果 |
| Completed | 模块出口/Reset | 出口或重置 | 保留证据直到离开 |

## 3. 坐标与标定

### 3.1 统一坐标空间

所有判定转换到 `RailViewport` 本地像素：

- `ProbeEntryPoint`：探头 Sprite 上配置化 UV，经 preserveAspect 实际渲染矩形映射，再 `TransformPoint` 到 RailViewport。
- `WeldSegment`：由 `WeldLine` RectTransform 的上下端点转换得到，不用节点中心代替整条线。
- `WeldIntersection`：从 ProbeEntryPoint 沿配置化表面测量方向作射线，与 WeldSegment 求交；越界时阻止推进并 `LogError`。
- `DamagePoint/HitZone`：继续按 `正视角透明.png` 的损伤素材坐标映射到 RailViewport；命中区半径配置化。
- `Ruler anchors`：从正式 `2102x455` Sprite 的 0mm、13°槽和 120mm 图形锚点 UV 映射到 preserveAspect 实际渲染矩形。

正式尺当前可确认 0mm 是左尖端；13°槽与 120mm 图形需在实施前完成像素/UV 审计。初值允许从图片读取，但必须通过 Play Mode 双点误差和人工视觉复核后才视为最终标定，不能用 `rect.xMin` 或文字中心替代。

### 3.2 唯一毫米比例

```text
pixelsPerMm = distance(rulerZeroLocal, ruler120Local) / 120
surfaceDistanceMm = distance(ProbeEntryPoint, WeldIntersection) / pixelsPerMm
```

二维欧氏距离是唯一比例，不能取 X 投影。

扫描中心位置需补偿入射点相对探头 Rect 中心的偏置：

```text
entryOffset = ProbeEntryPoint - ProbeRectCenter
scanStartCenter = WeldIntersection - scanDirection * 150 * pixelsPerMm - entryOffset
hitCenter       = WeldIntersection - scanDirection * 120 * pixelsPerMm - entryOffset
```

删除旧 `targetProgress=0.6` 和 `scanEnd=100mm` 的因果关系。拖拽可以用轨迹投影约束位置，但 UI 读数必须从实际几何距离反算。

### 3.3 射线与命中

`IncidentBeam` 起点绑定 ProbeEntryPoint，方向严格来自探头当前角度：

```text
beamDirection = Rotate(referenceDirection, -angleDeg)
beamEnd = ProbeEntryPoint + beamDirection * beamLengthPx
beamHit = DistancePointToSegment(DamagePoint, ProbeEntryPoint, beamEnd) <= hitRadiusPx
```

有效检出：

```text
Scanning
&& AngleCorrect(13deg)
&& abs(surfaceDistanceMm - 120) <= distanceToleranceMm
&& beamHit
```

Beam 不朝 DamagePoint 自动转向。`ReflectedBeam.SetActive(false)` 在初始化/Reset 兜底。普通/透视切换不控制 IncidentBeam，只控制钢轨和内部损伤层。

## 4. 尺子双模式

`M3RulerDrag` 使用内部模式：

```csharp
public enum Mode { Home, AngleGuide, DistanceMeasure, LockedResult }
```

### AngleGuide

- 仅在探头 0°放置成功后进入。
- 系统根据轨头上边缘方向设置尺子旋转，玩家只拖动。
- 同时检查：13°槽锚点到探头校角锚点距离、尺身方向与轨头上边缘夹角、Slider 13°。
- 三项通过后吸附确认并 `ResetTool()` 归槽；Flow 锁定 Slider、显示 Beam、进入扫描。

### DistanceMeasure

- 仅点击 NextButton 后进入。
- 先将尺子本地 `zero->120` 向量旋转到 `ProbeEntryPoint->WeldIntersection` 目标向量。
- 玩家拖动；每帧分别检查 `zero->ProbeEntryPoint` 和 `ruler120->WeldIntersection` 误差。
- 双点都在容差内才吸附并报告完成；单点不通过。
- 完成后切 `LockedResult`，保留尺子姿态；Reset/出口才恢复 Awake 缓存的 Home 父级、Rect、旋转、缩放和 sibling。

## 5. 组件职责

### `M3FlowController`

- 唯一状态所有者：ProbePlaced、AngleVerified、Detected、Measured、蜂鸣锁。
- 负责顺序解锁、NextButton、阶段推进、峰值/标记/完成反馈、Reset 和视图状态。
- 删除 `_prevMm` 跨阈值检出；距离通知只更新读数/波形，命中事件需带几何判定结果。
- 使用运行时默认提示覆盖冻结 Scene 旧提示，不写回 Scene。

### `M3ProbeDrag`

- 负责 0°放置、Slider 视觉、扫描轨迹、入射点、熔合线交点、距离换算、IncidentBeam 和 BeamHit。
- 对外提供 `ProbeEntryPointInRail`、`WeldIntersectionInRail`、`PixelsPerMm`、`BeamHitsDamage`。
- 命中后硬锁位置；不直接蜂鸣、不拥有阶段。
- 删除 ReflectedBeam 更新、60%校准和 100mm 峰后路径。

### `M3RulerDrag`

- 缓存/恢复 Scene Home，映射正式尺 0mm/13°槽/120mm 三锚点。
- 实现 AngleGuide、DistanceMeasure、LockedResult。
- 只报告校角完成和双点完成，不拥有流程状态。

### `M3IdleHelp`

- 30 秒演示按公开 API 依次完成放置、Slider 和尺校角。
- 60 秒演示调用新几何 `AutoMoveToMm(120)`，命中后停止，不点击 Next。
- 使用 `Time.deltaTime`，QA/Modal 暂停时不推进。

### `M2WaveformGraphic`

- 继续消费 150->120mm 几何读数，使用现有 M3 包络参数绘制生长与 120mm 峰值。
- 命中后读数保持 120mm，不进入 118->100mm 下降段；不参与检出判定。

### `M3RuntimeSmoke`

- 删除“120mm=60%进度”“继续到100mm”“尺子零点对焊缝即完成”的断言。
- 增加顺序门控、三条件校角、Beam 方向/命中、表面/内部目标分离、Next 门控、双点负例/正例、锁定和 Reset 复跑。

## 6. 冻结 Scene 与兼容

实施前老板手工完成：

1. 在 D 区添加 M2 同规格 `NextButton`，初始隐藏。
2. 为 `Ruler/bg` 绑定正式多功能尺，处理占位标签并设置 preserveAspect。
3. 保存后提供新 SHA-256，此后重新冻结。

runtime 只绑定引用和切 active/interactable，不创建视觉节点、不注入 Sprite、不保存 Scene。缺少 NextButton、正式尺 Sprite、RailViewport、WeldLine、IncidentBeam、DamageMarker 等必要引用时明确 `LogError` 并阻止流程。

现有序列化字段尽量保留以兼容 Scene；废弃字段可留作反序列化占位但不得参与新判定。M1 QA/数字人、M2 脚本和 Scene 不修改。

## 7. 验证与风险

- **尺子 120mm 图形标定**：图形与右侧槽组合，不可凭文字中心猜锚点；先数值审计，再 Play Mode 双点验证和人工复核。
- **射线方向与表面方向不同**：不要把 Beam 方向用作尺子测量方向；二者必须独立配置和测试。
- **冻结 Scene 污染**：每次 Unity 批处理、烟测和截图前后比较新基线哈希；变化即停止，不自动还原用户改动。
- **150 行限制**：Flow/Probe 已满 150 行，必须先删除旧路径；若仍超限，优先压缩已有样板和复用小型静态数学函数，不新增专用 runtime 脚本绕过。
- **回滚顺序**：双点测量 -> Next 门控 -> 射线命中 -> 几何距离 -> 校角；只回滚本任务代码，不碰老板手工 Scene。
