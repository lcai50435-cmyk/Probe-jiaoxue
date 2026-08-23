# M4 静态 Scene 与 UGUI 线框执行清单

## 0. 基线

- [ ] 记录 M1/M2/M3 Scene、Build Settings 和 M4 依赖素材 SHA-256。
- [ ] 确认 Unity 版本、项目占用状态、中文字体、正视钢轨、K2.5、多功能尺和全身数字人可读取。
- [ ] 确认本轮不新增 runtime 脚本、不修改冻结 Scene 或 Build Settings。

## 1. Editor 实现

- [ ] 新增 `M4Setup.cs` 菜单与 batch 入口，只创建/打开/保存 M4。
- [ ] 幂等生成 Canvas/EventSystem/SafeArea 与 M3 基线同构页面骨架。
- [ ] 生成 RailViewport、ToolShelf/Home、Probe/Ruler 纯根与直接 `bg` 子节点。
- [ ] 注入正视普通/透明钢轨、K2.5、多功能尺、全身数字人和中文字体；确保同一把尺的 10°槽、0mm 和40mm标识在工作态可辨。
- [ ] 生成轨腰最上端 0°放置、尺子向上 10°校角预览和独立伤损/声束/气泡预置节点；DamageMarker 与 IncidentBeam 目标统一到钢轨红色损伤中心，WeldLine 仅作视觉参照。
- [ ] 确认 PositionPreview 只是不可交互提示层，不作为第二套探头/尺子；正式 Probe/Ruler 可支持后续 Home、校角和复测状态。
- [ ] 生成 B 区目标 40mm、80→30mm参考波形，C 区分段控件和 D 区 10° Slider/步骤，并预留检出后进入尺子复测的按钮节点。
- [ ] 生成 QA/数字人/Modal/Completion/Help 静态层级和正确默认状态。
- [ ] 新增 `M4Shot.cs`，实现三视口、非空像素断言、finally 恢复和 Scene 哈希检查。

## 2. Setup 验证

- [ ] Unity 编译无 Error。
- [ ] 从无 M4 Scene 状态执行 Setup 成功。
- [ ] 连续执行 Setup 两次，M4 Scene SHA-256 一致。
- [ ] 层级名称唯一，无 Missing Script；CanvasScaler、层级顺序和触控尺寸符合规范。
- [ ] 静态 Scene 不含 M3/M4 runtime 组件或持久事件监听。
- [ ] M1/M2/M3 Scene 与 Build Settings 哈希保持基线。

## 3. 截图验证

- [ ] 输出并通过非空断言：1920x1080。
- [ ] 输出并通过非空断言：1280x720。
- [ ] 输出并通过非空断言：2436x1125。
- [ ] 人工检查钢轨、工具、数字人、波形、C/D 区无重叠、裁切或文字溢出。
- [ ] 检查 M4 专属文案/数字正确且无 13°、120mm、150→100mm、轨头侧面残留；不得暗示检出后继续扫描至30mm或自动完成40mm测距。
- [ ] 截图前后 M4 Scene SHA-256 不变。

## 4. 审核与冻结

- [ ] 提交三视口给老板审核，根据反馈只调整 M4Setup 并重新生成。
- [ ] 老板批准后记录最终 M4 Scene SHA-256。
- [ ] 将 M4 Scene 设为冻结视觉权威，M4Setup 收缩为只读打开器。
- [ ] 更新父任务状态，并将真实节点/哈希交给 `08-13-m4-runtime-planning`。
- [ ] `git diff --check` 通过。

## 回滚

- Setup 编译或幂等失败：删除本任务新建的 M4 文件，不触碰 M1/M2/M3。
- 视觉审核未通过：继续调整 M4Setup 和 M4 Scene，不启动 runtime 规划。
- 截图链路失败：修复 M4Shot，不以仅生成 PNG 文件作为通过依据。
