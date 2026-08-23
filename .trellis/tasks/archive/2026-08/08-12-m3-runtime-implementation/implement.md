# M3 轨头侧面探测玩法流程重构执行计划

## 0. 开工门禁

- [ ] 老板审核并批准本任务 `prd.md`、`design.md`、`implement.md` 后才开始代码重构。
- [ ] 老板在 Unity 中手工添加扫描阶段 `NextButton`：文案“下一步”、初态隐藏、D 区样式/尺寸参考 M2；不得复用 `EnterNextButton`。
- [ ] 老板在 Unity 中将正式 `多功能尺子.png` 绑定到 `Ruler/bg`，设置 preserveAspect，并处理 `RulerPlaceholderLabel`。
- [ ] 老板保存 M3 Scene 后记录新的 SHA-256 并重新冻结；当前补齐前审计值为 `e07dcaf60894ae628fd40be0ccc5eeafcfb2e88d226d07cb5e7aafc4507c6aaf`，实施不得继续使用旧哈希作为保护基线。
- [ ] 运行 `trellis-before-dev`，加载 Unity 低代码、冻结 Scene、UGUI 和暂停规范。
- [ ] 确认 M3 Scene 工作区差异只包含老板手工前置；不运行 `M3Setup`、`M3FinalCloseout` 或任何保存 M3 Scene 的工具。
- [ ] 确认 Unity 6000.3.21f1 未被其他进程占用，避免 batchmode 与编辑器并发。

## 1. 完成素材与几何标定

- [ ] 在正式 `2102x455` 尺 Sprite 上审计并记录 0mm、13°槽、120mm 三锚点的像素坐标与 Unity 底左 UV；0mm 可从左尖端开始，13°槽/120mm 不使用文字中心代替物理锚点。
- [ ] 在 `M3RulerDrag` 用 preserveAspect 实际渲染矩形映射三锚点；`0->120` 比例取二维欧氏距离。
- [ ] 在 `M3ProbeDrag` 配置探头入射点 UV，并补偿其相对探头 Rect 中心的偏移。
- [ ] 将 `WeldLine` 转为 RailViewport 本地线段，计算测量方向与线段的交点；交点越界时明确报错并阻止流程。
- [ ] 将 `正视角透明.png` 内部损伤位置映射为独立 DamagePoint/HitZone；与 WeldIntersection 分开保存。
- [ ] 用 `0->120` 比例反算 150mm 起始中心和 120mm 命中中心，删除 `targetProgress=0.6` 与 100mm 扫描终点因果。
- [ ] 对缺失 RailViewport、正式尺 Sprite、WeldLine、IncidentBeam、DamageMarker、NextButton 等必要引用 `LogError` 并停止推进。

验证门：Unity 编译通过；Play Mode 数值断言 `distance(ProbeEntryPoint,WeldIntersection)/pixelsPerMm` 在起点为 150mm、命中位为 120mm，且与固定拖拽百分比无关。

## 2. 重构顺序定位与 13°校角

- [ ] `M3FlowController` 将 Positioning 拆为“仅探头放置”和“Slider/尺校角”内部事实；Intro 后 Slider/尺锁定。
- [ ] `M3ProbeDrag` 仅允许 0°探头放到 150mm 起始位；成功后通知 Flow 解锁 Slider/尺。
- [ ] `M3RulerDrag` 增加 Home/AngleGuide/DistanceMeasure/LockedResult 模式。
- [ ] AngleGuide 进入时系统预设尺子方向，玩家只拖动；同时校验 13°槽到探头锚点、尺身与轨头上边缘夹角、Slider 13°。
- [ ] 三项通过后自动确认并归槽；Flow 锁定 Slider、探头保持向下 13°、显示 IncidentBeam 并进入 Scanning。
- [ ] 校角前 IncidentBeam 隐藏，ReflectedBeam 初始化与 Reset 均强制隐藏。

验证门：探头未放置时 Slider/尺不可操作；仅 Slider、仅尺位、错误平行角均失败；三项正例后尺归槽、Slider 锁定、Beam 出现。

## 3. 重构扫描距离与绿色束命中

- [ ] 探头沿配置扫描轴拖动，位置限制为 150->120mm；实时 UI 距离从入射点到熔合线交点的欧氏距离反算。
- [ ] IncidentBeam 起点绑定 ProbeEntryPoint，方向严格使用探头当前 -13°，长度/命中半径配置化，不朝损伤自动转向。
- [ ] 使用点到线段距离判定 Beam 是否穿过内部 DamageHitZone。
- [ ] Flow 删除 `_prevMm` 跨阈值检出，改为组合 Scanning + 13° + 120mm 容差 + BeamHit。
- [ ] 有效命中后立即硬锁探头与 120mm 波形峰值，蜂鸣一次，显示 DetectionBanner/黄色 DamageMarker/NextButton。
- [ ] 普通和透视都显示 IncidentBeam；普通视图命中前不显示内部损伤，命中后显示黄色标记；视图切换不改状态。
- [ ] 任何继续拖动、往返或视图切换不得改变锁定位置、峰值或重复蜂鸣；100mm 不可到达。

验证门：距离正确但 Beam 未命中、Beam 命中但距离错误、角度错误三个负例均不检出；三条件正例只触发一次。

## 4. 接入下一步门控

- [ ] `M3FlowController` 增加 `nextButton` 引用与幂等 runtime 绑定。
- [ ] Intro/Positioning/Scanning 未命中时 NextButton 隐藏；有效命中后显示。
- [ ] 点击后立即隐藏，阶段切 Measuring，并调用尺子 `ShowMeasure()`；点击前尺子保持 Home 锁定。
- [ ] `EnterNextButton` 保持 Completed 出口语义，不参与 Scanning->Measuring。
- [ ] 进入 Measuring 后继续保留探头、Beam、黄色标记和 120mm 峰值。

验证门：命中前点击路径不可达；命中后按钮可点击一次；点击前/后尺子模式与按钮显隐正确。

## 5. 重构 0/120mm 双点测量

- [ ] DistanceMeasure 进入时计算本地 `zero->120` 到目标 `ProbeEntryPoint->WeldIntersection` 的旋转并自动设置尺子方向。
- [ ] 玩家拖动时分别计算 rulerZero 到 ProbeEntryPoint、ruler120 到 WeldIntersection 的误差。
- [ ] 删除 `GetRenderedImageLeft + WeldLine中心` 单点成功路径；保留兼容字段但不参与新判定。
- [ ] 仅 0mm 对准、仅 120mm 对准两个负例不得完成；双点均在容差才自动吸附。
- [ ] 成功后播放一次 correctClip、显示 MeasurementBubble、进入 Completed 并切 LockedResult。
- [ ] Completed 保留尺子吸附姿态；Reset/异常中断/EnterNextModule 才恢复 Awake 缓存的 Home 状态与最后 sibling。

验证门：两个单点负例、偏移负例和一个双点正例；成功音效一次，完成态证据完整。

## 6. 更新帮助、暂停、文案与重置

- [ ] 30 秒帮助调用公开 API 顺序演示 0°放置、Slider 13°、尺子校角和归槽，不直接通知成功。
- [ ] 60 秒帮助调用新几何 `AutoMoveToMm(120)`，触发真实命中与 NextButton 后停止，不自动点击。
- [ ] QA/Modal 暂停期间 Intro、idle、帮助动画、角度稳定计时和拖拽不推进；关闭后恢复原状态。
- [ ] 使用 runtime 默认提示覆盖冻结 Scene 中“150->100”“0刻度对焊缝”等旧文案，不保存 Scene。
- [ ] Reset 清除所有流程事实、蜂鸣锁、标记、Beam、Next、结果和帮助，恢复 Home/0°/150mm/普通视图并重新播放 Intro；QA 历史保留。

验证门：自动与手动命中位置一致；暂停期间位置/计时不变；Reset 后完整复跑通过。

## 7. 更新自动化验收

- [ ] 重写 `M3RuntimeSmoke`，移除 120mm=60%进度、峰后100mm、自动进Measuring、尺零点对焊缝等旧断言。
- [ ] 增加 Intro 顺序锁、0°放置、Slider 单条件失败、尺槽/平行负例、校角成功与 Beam 出现断言。
- [ ] 增加 150/120mm 几何、表面/内部目标分离、Beam 方向、三类命中负例、一次蜂鸣和硬锁断言。
- [ ] 增加 Next 显隐/点击门控、0/120 双点负例/正例、完成证据和 Reset 后复跑断言。
- [ ] 保留普通/透视、QA 暂停恢复、Home 初态、数字人层级和未配置 M4 出口回归。
- [ ] 烟测只驱动公开 API/测试入口，不保存 Scene；执行前后比较新冻结 SHA-256。

建议命令：

```powershell
& "<Unity.exe>" -batchmode -projectPath . -executeMethod M3.EditorTools.M3RuntimeSmoke.RunBatch -logFile Logs/m3-flow-refactor.log
```

## 8. 最终检查

- [ ] 运行 `trellis-check` 做规格、职责、数据流和测试审查。
- [ ] `wc -l` 确认 `M3FlowController`、`M3ProbeDrag`、`M3RulerDrag`、`M3IdleHelp` 各 <=150 行。
- [ ] `git diff --check` 通过；确认无新增 M3 runtime 脚本、无 runtime Assets 路径加载、无 M1/M2 业务改动。
- [ ] 三视口检查 1920x1080、1280x720、2436x1125：NextButton、尺子和文本无重叠裁切，槽位与 0/120 锚点可辨，触控区 >=64px。
- [ ] 最终再次计算 M3 Scene 哈希，必须等于老板手工前置后的新基线；若变化，停止收口并定位写入来源，不自动重置。
- [ ] 人工 Play Mode 复核尺子锚点、13°束方向、内部命中、表面 120mm、双点吸附和完成证据。

## 回滚点

- 双点测量异常：只回滚 DistanceMeasure 新路径。
- Next 门控异常：回滚 Scanning->Measuring 门控，不恢复自动进测量旧合同。
- Beam 命中异常：回滚线段相交实现，保留几何距离合同并继续定位。
- 距离异常：回滚锚点/交点换算，不修改正式素材或冻结 Scene。
- 任何回滚都不得覆盖老板手工 NextButton/尺子配置，也不得操作 M1/M2 Scene。
