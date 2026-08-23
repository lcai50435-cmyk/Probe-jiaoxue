# M3 玩法重构证据审计

## 1. 用户确认的最高优先级合同

2026-08-13 逐项确认：

- 120mm 是探头入射点到本侧焊缝熔合线交点的表面距离，不是内部声束长度。
- 命中后探头立即锁定，不继续到 100mm。
- 保留 Slider；流程为先 0°放探头，再解锁 Slider/尺子完成 13°校角。
- 定位与测量阶段均由系统预设尺子方向，玩家只拖动，不增加旋转控件。
- 正式尺最终为 0mm 对探头入射点、120mm 对熔合线交点，双点同时验证。
- IncidentBeam 普通/透视都显示，严格沿探头 13°方向，不自动朝损伤转向。
- 只保留绿色入射束，黄色 ReflectedBeam 隐藏。
- 表面 120mm 与内部损伤命中分开计算，必须同时满足才蜂鸣。
- 命中后保持 Beam、峰值和黄色标记；点击 NextButton 后才进入测量。
- 因 M2 当前有 NextButton，M3 同样采用该门控；按钮由老板手工补 Scene。
- 30/60 秒帮助走真实交互路径；60 秒帮助不自动点击 Next。
- Reset 重新播放 2 秒 Intro；完成态尺子保持吸附。

## 2. DOCX 证据

来源：`Assets/交互动画素材/M3轨头侧面探测.docx`。

提取流程：

1. 探头正放在轨头侧面，无偏角。
2. 用定位尺把探头向下偏转 13°，定位尺水平线与轨头侧面上边缘平齐。
3. 撤尺，探头保持向下 13°前移；探头入射点距本侧焊缝熔合线 120mm 时出波。
4. 再用定位尺确认出波位置；文档纠正旧图，要求尺沿探头偏角放。
5. 0mm 对探头入射点，120mm 对本侧焊缝熔合线。

该证据支持“先放置后校角”“撤尺后扫描”“表面120mm”“双点复测”。内部绿色束与损伤命中是老板补充的可视化/判定合同。

## 3. 当前代码差距

### `M3FlowController`

- 当前检出由 `_prevMm` 从 150 跨过 120 或进入容差触发。
- 当前 `NotifyDetected()` 直接 `Go(Measuring)`，没有 NextButton 门控。
- 当前 `ApplyView()` 只在透视时显示 BeamLayer，违反普通/透视都显示绿色束。
- `stepHints` 仍含 150->100 与单点测量旧文案。

### `M3ProbeDrag`

- 当前 `CalibrateTrack()` 将 DamagePoint 定义为 60%进度上的探头中心。
- 当前读数由 `Lerp(150,100,t)` 生成，不来自入射点到熔合线交点。
- Incident/ReflectedBeam 仅按角度旋转，没有线段与 DamageHitZone 相交判定。
- 检出后仍可继续 `AutoMoveToMm(100)`。

### `M3RulerDrag`

- 定位只检查尺根到 `AngleGuide` 的单点距离，不检查 13°槽、尺身平行或 Slider。
- 测量只把图像左缘对齐 `WeldLine` 中心；0mm/120mm 双点和目标交点均未实现。
- 当前 `Ruler/bg.Image.m_Sprite` 为空，因此 runtime 无法标定正式尺。

### `M3IdleHelp` / `M3RuntimeSmoke`

- 30 秒帮助直接 `AutoPosition()`，绕过真实槽位/平行判定。
- 烟测明确断言 120mm=60%进度、检出后自动 Measuring、继续到100mm、尺零点对焊缝；均需删除。

## 4. Scene 能力与前置缺口

当前 `Assets/Settings/Scenes/M3.unity` SHA-256：

```text
e07dcaf60894ae628fd40be0ccc5eeafcfb2e88d226d07cb5e7aafc4507c6aaf
```

已有节点/能力：Angle Slider、Probe/bg、Ruler/bg、AngleGuide、WeldLine、IncidentBeam、ReflectedBeam、DamageMarker、DetectionBanner、MeasurementBubble、ProbeHome、RulerHome、普通/透视、波形、Reset、QA/数字人占位与完成出口。

缺口：

1. M3 没有扫描阶段 NextButton；只有 Completed 的 EnterNextButton。
2. `Ruler/bg.Image.m_Sprite={fileID:0}`，且 `RulerPlaceholderLabel` 仍在。

冻结规则要求由老板在 Unity 中手工补 NextButton、绑定正式尺并重新冻结；Agent/runtime/Setup 不得动态自愈。

## 5. 正式尺素材

`Assets/交互动画素材/03 其他素材/多功能尺子.png`：2102x455 RGBA，包含 13°槽、10°槽、110mm 与 120mm 图形。

- 0mm 可从尺身左尖端定义。
- 13°槽可从左侧凹槽物理尖角定义。
- 120mm 图形与右侧槽/箭头组合，不应以绿色“120mm”文字中心作为锚点。
- 实施前必须记录三锚点像素/UV，并在 preserveAspect 实际渲染矩形内做双点 Play Mode 验证与人工视觉复核。

## 6. 工作量判断

无需重做页面、QA、数字人、波形或音效，也不新增 runtime 脚本。核心重构覆盖：

- `M3FlowController`：约 40%-55%，替换检出因果并增加顺序/Next 门控。
- `M3ProbeDrag`：约 65%-80%，重建距离、入射点、交点和 BeamHit。
- `M3RulerDrag`：约 70%-85%，重建双模式和三锚点判定。
- `M3IdleHelp`：约 35%-50%。
- `M3RuntimeSmoke`：约 60%-75%。

整体属于中等偏大的核心逻辑重构，但模块页面与公共链路保留；预计完整实施、Unity 验证和人工复核约 3-4 个开发日，前提是老板先补齐冻结 Scene 两项视觉缺口。
