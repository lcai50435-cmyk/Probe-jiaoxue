# M2 耦合剂蓝色铁轨薄膜动画 执行计划

## 0. 开工门禁

- [ ] 老板审核并批准 `prd.md`、`design.md`、`implement.md` 后执行 `task.py start`。
- [ ] 运行 `trellis-before-dev`，确认 Unity 低代码与冻结 Scene 规范（已在会话加载，实施时复核）。
- [ ] 记录 `Assets/Settings/Scenes/M2.unity` SHA-256，**实施基线 = 工作区当前 `fbb801e4aa5196c96b0ab4b63ab3c442bb56ac8f956f6790fba7a2c4080ab366`**（工作区含老板/既有任务的素材替换与参数调整未提交改动，以工作区值为保护基线；HEAD 基线 `1610da8a…` 仅作参考）。
- [ ] 不运行任何会写回 M2 Scene 的 Setup/Closeout 工具。

## 1. 新建 `Assets/Scripts/M2CouplantFx.cs`（~80 行）

- [ ] 字段：`railBg`、`maskRt`、`film`、`group`、`filmColor`（默认半透明蓝）、`animDuration=2f`、`holdDuration=0.5f`、`fadeDuration=0.5f`。
- [ ] `Bind(...)` 注入引用；`Play(Action onDone)` 幂等入口；`Reset()` 复位。
- [ ] `Setup()`：rect 同步 railBg（anchoredPosition/sizeDelta/pivot）、sprite=俯视角、Filled/Horizontal/Origin Left、color、alpha=1。
- [ ] `Anim` 协程：fillAmount 0→1（animDuration）→ WaitForSeconds(holdDuration) → group.alpha 1→0（fadeDuration）→ 隐藏 mask → onDone。
- [ ] scaled time 全链路（QA/Modal 暂停即冻结）。

验证门：离线检查行数 ≤150；无 Scene 写入 API；缺失引用时 LogError 不崩溃。

## 2. 改造 `M2FlowController.cs`（145 → ~135 行）

- [ ] 删 `CouplantAnim` 协程与 `couplantAnimDuration` 字段。
- [ ] `Awake` 末尾运行时 `AddComponent<M2CouplantFx>()` 并注入 `railBg/couplantMask/couplantOverlay/bg Image/CanvasGroup` 引用。
- [ ] `ApplyCouplant()` 改调 `couplantFx.Play(OnCouplantDone)`；新增 `OnCouplantDone()`（置 `_applying/CouplantApplied`、按钮文案、`probeDrag.Unlock()`、`Go(Positioning)`）。
- [ ] `ResetAll()` 加 `couplantFx?.Reset()`。
- [ ] 其他流程（校角/扫描/测量/完成）零改动。

验证门：Unity 编译无 Error；`wc -l` 两文件 ≤150；M2 Scene SHA 不变。

## 3. 更新 `Assets/Editor/M2RuntimeSmoke.cs`

- [ ] 快进段：`couplantAnimDuration = 0` 改为 `couplantFx.animDuration = couplantFx.holdDuration = couplantFx.fadeDuration = 0f`。
- [ ] 新增断言：薄膜 sprite 非空、fillMethod Horizontal、fillOrigin Left、color 半透明蓝（a<1）、动画后 CouplantApplied && Stage==Positioning。
- [ ] Reset 复跑用例覆盖 `couplantFx.Reset` 复位。
- [ ] 保留耦合剂后进入 Positioning 的既有断言链。

验证门：Play Mode 烟测通过（含 Reset 复跑）。

## 4. 自动化验收

- [ ] Unity（6000.3.21f1，有图形设备）编译无 Error + `M2RuntimeSmoke.RunBatch` 通过：
  ```powershell
  & "<Unity.exe>" -batchmode -projectPath . -executeMethod M2.EditorTools.M2RuntimeSmoke.RunBatch -logFile Logs/m2-couplant-fx.log
  ```
- [ ] 人工 Play Mode 检查：薄膜与铁轨精确重合、严格从左至右铺满（2s）、停留 0.5s、淡出 0.5s、期间 QA 打开冻结、Reset 干净复位。
- [ ] 运行 `git diff --check`；确认无新增素材文件、无 M1/M3 Scene 改动。
- [ ] 再次计算 M2 Scene SHA-256 == 开工基线；若有差异停止收口并定位写入者，不自动重置。

## 5. 收口

- [ ] `trellis-check` 审查：职责边界（Fx 只做动画、Flow 只做流程）、暂停合同、行数、Scene 哈希。
- [ ] 必要时更新 `.trellis/spec/unity/low-code.md` 沉淀「冻结 Scene 的运行时视觉动画组件」模式（如耦合剂薄膜合同）。
- [ ] 老板确认后按 Trellis 流程收口提交（默认不主动 commit，除非老板授权）。
