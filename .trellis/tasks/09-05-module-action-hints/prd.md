# M2 涂抹按钮「点击这里」+ M3/M4 校角拖尺提示修复

父任务：`09-03-trial-check-regression-fix`（R1 + R2 的独立交付子任务）。

## Goal

体验版回归检查发现两处操作提示缺失，本任务修复并沉淀可重复验收命令：
- M2 Couplant 阶段「涂抹耦合剂」按钮旁缺「点击这里」提示。
- M3/M4 校角阶段放探头后缺「拖动多功能尺至探头处」提示（中间态不显示）。

## Requirements

### R1：M2 Couplant 阶段「点击这里」提示（2026-09-03 老板定稿）

- 进入 M2 后（Couplant 阶段），在「涂抹耦合剂」按钮上方/旁创建 **DontSave TMP 文本「点击这里」**。
- 显示时机：M2 进入/ResetAll 后即出现；玩家点击「涂抹耦合剂」按钮后消失（点击瞬间即可，不必等动画完成）。
- 冻结 M2 Scene 零写回：按钮旁提示用运行时 DontSave 节点；按钮定位用运行时取 `applyButton` 位置。
- 不采用金色脉动（M1 PulseHighlight 是防卡死自动演示，语义不同）。

关键事实（父 prd 已查证）：
- `M2FlowController.CurrentActionHint()` 对 Couplant 返回空（M2FlowController.cs:292）；`DefaultHints[0]` 空（:44）。
- 涂抹按钮 `applyButton`（:21/58），点击走 `ApplyCouplant()` → `couplantFx.Play(OnCouplantDone)`（:137-145）；ResetAll 在 :255-262 恢复 Couplant 阶段并重显。
- 建议接入点：点击消失可在 `ApplyCouplant()` 首行置隐藏；ResetAll 重显；初始显示在 `UpdateUi()`/Couplant 进入处。定位可参考 `ModuleHintOverlay.EnsureActionHint`（ModuleHintOverlay.cs:58）的 DontSave 文本套路，但锚定 `applyButton`。

### R2：M3/M4 校角阶段「拖动多功能尺至探头处」提示

- 根因（已查证）：M3/M4 `NotifyPlacementChanged()` 空实现（M3FlowController.cs:150 / M4FlowController.cs:150），探头放置成功不触发 `UpdateUi()`，actionHint 停在「拖动探头」，中间「拖动多功能尺至探头处」阶段不显示。
- M3/M4 `CurrentActionHint()` Positioning 分支文案已存在（M3FlowController.cs:305-314；M4FlowController.cs:303-312），无需加文案。
- 修复：M3/M4 `NotifyPlacementChanged()` 改为触发 `UpdateUi()`（对齐 M2，M2FlowController.cs:154），使提示按流程流转：`拖动探头 → 拖动多功能尺至探头处 → 滑动此处调整偏角 → 拖动多功能尺返回工具栏`。
- 注意：M3/M4 校角流程先放探头再拖尺吸附，`EnterPositioning()` 后 `probeDrag.Placed=false`；放探头成功 → `NotifyPlacementChanged`（当前空）→ 需刷新到「拖动多功能尺至探头处」。同时确认放探头回调里是否还有其它需一并处理的状态（对照 M2 的 `NotifyPlacementChanged` 里 `ShowAngleGuide` 行为，M3/M4 尺子吸附是独立 `CheckPositioning` 路径，不需要额外引导函数）。
- M3/M4 Scene 零改动。

## Acceptance Criteria

- [ ] R1：Play M2，Couplant 阶段涂抹按钮旁可见「点击这里」；点击「涂抹耦合剂」后提示消失；ResetAll 后重现；M2.unity 字节级零写回（SHA-256 对比任务前后）。
- [ ] R2：Play M3 与 M4，放探头成功后立即出现「拖动多功能尺至探头处」；拖尺吸附后切「滑动此处调整偏角」；M3/M4 Scene 零改动。
- [ ] 回归：M2/M3/M4 原有 Positioning→Scanning→Measuring 全流程提示流转无回归；M2 不受 R1 改动影响。
- [ ] 代码质量：无新增专用脚本超出必要（优先复用 ModuleHintOverlay 或代码默认）；runtime 新增代码 ≤150 行约束。
- [ ] 编译零错误（runtime 全量 Roslyn/csc），`git diff --check` 干净。

## Out Of Scope

- M2/M3/M4 Scene YAML 任何改动。
- 问题 3（iPhone 引导）与问题 4（防再犯）——见兄弟子任务。
- 提示文案的 PPT 级内容调整（已有 08-23-lines-pptx-update 任务）。
