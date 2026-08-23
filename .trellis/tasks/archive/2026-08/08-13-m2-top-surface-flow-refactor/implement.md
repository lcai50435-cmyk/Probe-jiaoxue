# M2 轨头顶面探测流程重构执行计划

## 0. 开工门禁

- [ ] 用户审核并批准 `prd.md`、`design.md`、`implement.md` 后才执行 `task.py start`。
- [ ] 运行 `trellis-before-dev`，加载 Unity 低代码和冻结 Scene 规范。
- [ ] 记录 `Assets/Settings/Scenes/M2.unity` SHA-256；预期实施基线为 `ea42686874ec82720bf90e643b2417822fff2fd70b5597605ad36324adf64fb2`。
- [ ] 确认 M2 Scene 工作区差异仍只有老板已有数字人 `x=-13` 调整；若出现新差异，停止并与老板确认新的保护基线，不自行扩大范围。
- [ ] 不运行 `M2Setup`、`M2FinalCloseout` 或任何保存 M2 Scene 的 Editor 工具。

## 1. 建立共享几何合同

- [x] 使用正式 `尺子正面.png`（1205×213）底边基线默认 UV：0mm 左端底尖 `(0.005,0.038)`、110mm 竖刻线 `(0.73,0.038)`、10°槽尖角 `(0.005,0.136)`（low-code.md 5.4 权威，取代旧多功能尺上沿标定）；通过 Play Mode 数值和人工视觉复核，不得使用透明点、字样位置或不同高度锚点。
- [x] 扫描轨迹线与损伤点同线（`scanLineY = damage.y`，2026-08-14 老板确认），150mm 起点由 `damage - scanDirection*150*ppm` 反算，`startLocal` 不再作为几何距离依据。
- [ ] 在 `M2RulerDrag` 提供 preserveAspect 实际渲染矩形到锚点局部像素的统一换算；`0→110` 比例取二维欧氏距离，不取水平投影。
- [ ] 在 `M2ProbeDrag` 暴露 RailViewport 本地像素下的探头入射点、损伤点、扫描方向和 `pixelsPerMm`。
- [ ] 用尺子 `0→110` 跨度反算 150mm 起点和 110mm 检出点，删除“损伤中心即探头中心/80%扫描进度”的旧校准。
- [ ] 对缺失 `RailViewport`、`RailPerspective`、`BeamLine`、正式尺 Sprite 等必要引用输出明确错误并阻止流程推进。

验证门：Unity 编译通过；编辑器/烟测断言 110mm 时 `distance(ProbeEntryPoint, DamagePoint) == 110 * pixelsPerMm` 且两点不重合。

## 2. 重构定位与校角

- [ ] `M2FlowController` 增加尺子校角事实，扫描门控改为 `Placed && AngleCorrect && AngleVerifiedByRuler`。
- [ ] `M2RulerDrag` 增加 Home/AngleGuide/DistanceMeasure 模式和两个独立完成事件。
- [ ] AngleGuide 同时校验 Slider 10°、尺子 10°槽到探头锚点距离、尺身与钢轨平行角度。
- [ ] 校角成功后吸附确认、尺子归槽、Slider 锁定，探头保持 10°进入扫描。
- [ ] Reset 恢复 Slider 0°、探头 Home、尺子 Home 和校角未完成状态。

验证门：Slider 单独到 10°不能进入扫描；错误尺位不能进入；正确三条件通过后进入且尺子归槽。

## 3. 重构扫描、检测束与波形

- [ ] 探头纵向拖拽使用新几何轨迹，实时距离由入射点到损伤点换算并限制在 150→110mm。
- [ ] Beam 起点绑定探头入射点，长度使用 110mm 标定跨度，方向/命中区与损伤点一致。
- [ ] Flow 删除 `_prevMm` 跨越阈值即检出的旧因果，改为接收“角度正确 + 110mm 容差 + Beam 命中”。
- [ ] 命中后锁定探头输入和 110mm 波形峰值，蜂鸣一次并显示现有“下一步”按钮；只有玩家点击后才进入尺子复测。
- [ ] 确认普通/透视切换只改变可见层，不改变读数或命中结果。

验证门：手动拖动在 110mm 几何位置检出一次并锁定；继续拖动不能改变探头位置、峰值或重复蜂鸣；点击“下一步”后进入测量。

## 4. 重构最终双点测量

- [ ] 进入 Measuring 时尺子切到 DistanceMeasure，0mm 和 110mm 两个锚点目标分别绑定 ProbeEntryPoint 与 DamagePoint。
- [ ] 删除以 `WeldLine`/尺子左缘单点吸附为完成条件的 runtime 路径。
- [ ] 进入测量态时用“可见上沿 0→110 锚点向量”到“探头入射点→损伤点目标向量”的夹角自动定向；真实拖拽保持方向，玩家平移 0mm 可见点即可使 110mm 可见刻线同时命中。
- [ ] 单独对准 0mm、单独对准 110mm、尺子方向反向均不得完成。
- [ ] 两点均在容差后自动吸附、显示既有 110mm 结果并进入 Completed。
- [ ] 完成出口和 Reset 后尺子恢复 Scene Home 初态、尺寸、旋转、sibling 和置灰锁定。

验证门：覆盖三个负例和一个双点正例；完成后出口行为保持原样。

## 5. 更新防卡死与运行时文案

- [ ] 30 秒帮助调用公开交互 API 完整演示放置、10°和尺子校角，不直接伪造成功状态。
- [ ] 60 秒帮助按新几何比例移动到 110mm，和手动流程在同一位置触发命中。
- [ ] QA/重置 Modal 打开时帮助、拖拽、耦合剂动画和自动演示暂停，关闭后恢复原状态。
- [ ] 测量阶段运行时提示采用“0mm 对探头入射点、110mm 对红色损伤”，不写回冻结 Scene。

验证门：自动帮助与手动结果完全一致；QA/Modal 暂停期间位置和计时不变。

## 6. 自动化验收

- [ ] 更新 `M2RuntimeSmoke`，移除旧的 80%/探头中心落损伤/尺子对焊缝断言。
- [ ] 增加初态、耦合剂、Slider 单条件失败、尺子校角成功、110mm 可见间距、Beam 命中、检出锁定、蜂鸣单次、双点负例/正例、Reset 后复跑断言。
- [ ] 保留 QA 暂停恢复、正式尺 Home、M1→M2 加载和未配置下一模块回归。
- [ ] 在有图形设备的 Unity 6000.3.21f1 环境运行 Play Mode 烟测；工具不得保存 Scene。
- [ ] 必要时运行现有三视口截图，只检查无运行时重排/溢出；截图前后比较 Scene 哈希。

建议命令（Unity 路径按本机安装位置替换）：

```powershell
& "<Unity.exe>" -batchmode -projectPath . -executeMethod M2.EditorTools.M2RuntimeSmoke.RunBatch -logFile Logs/m2-flow-refactor.log
& "<Unity.exe>" -batchmode -projectPath . -executeMethod M2.EditorTools.M2RuntimeSmoke.RunM1ToM2Batch -logFile Logs/m2-link-smoke.log
```

## 7. 最终检查与回滚点

- [ ] 运行 `trellis-check` 做规格、职责、数据流和测试审查。
- [ ] `wc -l` 确认涉及的 M2 runtime 脚本各不超过 150 行。
- [ ] 运行 `git diff --check`，检查无 Assets 路径 runtime 加载、无新增 M2 runtime 脚本、无 M1/M3 Scene 改动。
- [ ] 再次计算 M2 Scene SHA-256，必须与开工基线完全一致；若变化，停止收口并定位写入来源，不自动重置文件。
- [ ] 人工 Play Mode 检查尺子 10°槽对位可辨、110mm 两端可辨、绿色束确实从探头指向红色损伤、横屏触控可操作。
- [ ] 验收失败时按“几何合同 → 定位门控 → 扫描命中 → 双点测量 → 帮助/回归”的反向顺序回滚本任务代码，不碰冻结 Scene 和用户已有变化。
