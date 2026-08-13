# QAPanel 激活时全局暂停游戏（timeScale=0）

## 目标

玩家长按数字人打开 QAPanel 时，游戏应处于暂停状态；关闭面板后恢复。该行为由公共组件 `M1QAPanel` 统一实现，M1 / M2 共用生效。

## 需求

1. QAPanel 激活（Open）时全局暂停：`Time.timeScale = 0`，涵盖 M2 计时、拖拽与动画；关闭（Close）时恢复**打开前**的 timeScale 值（不硬编码 1，避免覆盖引导等场景的设置）。
2. 暂停期间不受影响：数字人视频（VideoPlayer 不受 timeScale 影响，问答动画照常播放）、面板滑入/滑出动画（`unscaledDeltaTime`）、逐字显示（`WaitForSecondsRealtime`）、DeepSeek 请求（`UnityWebRequest`）、长按检测（`unscaledTime`）、Blocker 输入锁定。
3. 提供配置开关 `pauseGameOnOpen`（默认 `true`），Setup/Inspector 可关闭。
4. M1 行为回归：M1 打开 QA 时游戏也暂停（新增期望行为）；`M1IntroVideo` 引导期间 QA 入口被全屏遮罩挡住，无并发 timeScale 冲突。

## 实现（已完成）

- `Assets/Scripts/M1QAPanel.cs`：新增 `pauseGameOnOpen`（默认 true）+ `_paused` / `_timeScaleBefore` 状态，`Open()` 调 `ApplyPause(true)`、`Close()` 调 `ApplyPause(false)`。
- 文档同步：权威 `.trellis/tasks/08-10-m2-readiness-audit/design.md`（层级约束 / 10.3 / 验证计划三处）、本任务父任务 `08-11-m2-implementation` 的 `design.md`（2026-08-11 同步段落）与 `prd.md`（验收标准第 5 条）。

## 编译验证（已完成）

- Unity 编辑器自动重编译：`Assembly-CSharp.dll` / `Assembly-CSharp-Editor.dll` 均成功更新（07:58:07）。
- Unity 批处理（`-batchmode -quit`）执行 `M2Setup.SetupM2Batch`：`error CS` = 0，Setup 完成，场景哈希运行前后一致（幂等）。
- 顺带修复两个阻塞编译的既有错误（非本需求引入，见范围外）：`M1Setup.cs` CS0136（局部变量 `introCanvas` 重名，改名 `selfCanvas`）；`M2RulerDrag.cs` CS0034（Unity 6 Vector2/Vector3 混合运算符歧义，改逐分量运算）。

## 验收标准

- [ ] Play Mode：长按数字人打开 QAPanel 后 `Time.timeScale == 0`，底层输入不可操作；关闭后恢复 `1`。
- [ ] 面板打开期间数字人思考/讲解视频照常播放，面板滑入动画流畅（unscaled）。
- [ ] M1 场景：打开 QA 同样暂停，引导流程无异常，关闭后恢复。
- [ ] `M1QAPanel.pauseGameOnOpen` 置 false 时行为与改动前一致（可配置性）。
- [ ] `git diff --check` 对本次改动文件无新增告警。

## 范围外

- 未授权 `git commit` / `git push`。
- 不修改 M2 场景结构与 M2 runtime 流程（M2FlowController 等由父任务推进；QA 暂停行为在里程碑五接入时自动生效）。
- 顺带修复的 M1Setup.cs / M2RulerDrag.cs 编译错误属既有工作区问题，已最小修复，不在本任务重复展开。
