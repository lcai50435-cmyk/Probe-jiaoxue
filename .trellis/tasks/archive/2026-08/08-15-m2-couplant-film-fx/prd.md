# M2 耦合剂蓝色铁轨薄膜动画

## Goal

将 M2 第一阶段「涂抹耦合剂」的视觉反馈升级为：蓝色铁轨形状薄膜，从左至右慢慢出现，完整覆盖铁轨后停留约 0.5 秒，再淡化消失，随后按现有流程进入「探头定位与偏角」阶段。

## Background

- 现有实现（`M2FlowController.CouplantAnim`，`Assets/Scripts/M2FlowController.cs`）：
  - Scene 冻结节点：`CouplantMask`（`RailViewport` 直接子节点，891×220 居中，默认 inactive）→ `CouplantOverlay`（全屏 stretch + CanvasGroup）→ `bg`（Image，纯色矩形 `(0.45,0.75,0.95,0.35)`，无 sprite）。
  - 动画：localScale.x 0.05→1 展开（pivot=0.5，从中心向两侧，非严格从左至右）+ p>0.8 时 alpha 淡出 + `WaitForSeconds(.2f)` 后隐藏。
- `俯视角.png`（`Assets/railwayTracks_2/`，2469×609 RGBA）可复用为薄膜轮廓：外围背景 alpha≈0~115（透明/半透明羽化），铁轨主体 alpha≈255（浅灰渐变，含两轨之间空隙，即有效 alpha 区域为「铁轨+空隙」实心块）。同目录 `俯视角透视.png` 为透视视图用，与本任务无关。
- `railBg`（`M2FlowController.railBg`，RectTransform 960.523×285.958，anchoredPosition (-24,-32.979)，pivot 0.5）与 `CouplantMask` 同父级（`RailViewport`），运行时同步 rect 即可精确对齐。
- `M2FlowController.cs` 当前 145 行（150 行上限）。
- `Assets/Settings/Scenes/M2.unity` 冻结，不得修改/保存；不新增素材文件。
- 老板 2026-08-15 确认：半透明蓝、出现 2 秒、淡出默认 0.5s、流程衔接不变、**B 方案：新增独立脚本 `M2CouplantFx.cs`**（理由：Flow 已近行数上限、动画职责独立、M3 同款动画未来可复用）。
- `RailBackground/bg` 的 Image 已序列化引用 `俯视角` sub-sprite（fileID 6196224667116800966），`m_PreserveAspect: 0`（拉伸填满 rect）；薄膜 Image 采用同参数即可完全重合。

## Requirements

### R1. 薄膜形状与对齐

- 薄膜形状直接使用 `俯视角.png` 的轮廓：运行时把薄膜 Image.sprite 替换为 `俯视角` 的 sprite（`Resources.LoadAll<Sprite>("俯视角")[0]`，与 `SwapRailSprites` 同源），并染色为**半透明蓝**（老板确认；颜色可配置，默认浅蓝半透明）。
- 薄膜渲染矩形必须与 `railBg` 精确重合：运行时把 `CouplantMask`（或其 Overlay）的 `anchoredPosition`/`sizeDelta`/`pivot` 同步为 `railBg` 的当前值，不写回 Scene。
- 不新增图片素材，不修改 `俯视角.png`。

### R2. 出现动画：从左至右

- 动画必须是从左至右逐渐出现（扫过式揭示），禁止从中心向两侧展开。
- 推荐实现：`Image.type=Filled` + `fillMethod=Horizontal` + `fillOrigin=Left` + `fillAmount` 0→1；或等价 pivot 方案（需处理 pivot 改动后的位置补偿与 Reset 恢复）。
- 出现时长**2 秒**（老板确认），作为 `M2CouplantFx` 的配置字段。

### R3. 停留与淡化

- 完全覆盖（fillAmount=1）后停留 **0.5s**（老板确认，`couplantHoldDuration` 字段），期间薄膜完整可见。
- 停留结束后淡出：`CanvasGroup.alpha` 1→0，时长 **0.5s**（老板确认默认，`couplantFadeDuration` 字段）。
- 淡出结束后隐藏 `CouplantMask`，`CouplantApplied=true`，按现有流程进入 `Stage.Positioning`（按钮文案「已涂抹」、`probeDrag.Unlock()` 等保持原合同）。

### R4. Reset 与暂停

- Reset 必须恢复：fillAmount=0、alpha=1、CouplantMask 隐藏、rect/pivot 复位，可重新涂抹。
- 动画期间暂停（QA/Modal）合同沿用现有：协程用 `Time.deltaTime` 推进，暂停即停；`WaitForSeconds` 段在暂停时不推进（协程不受 timeScale 影响则需确认——若需要严格暂停，改用 unscaled 判断或沿用现有约定）。

### R5. 实现边界

- 不修改/保存 `M2.unity`，不新增素材。
- **B 方案（老板确认）**：新增独立 runtime 脚本 `Assets/Scripts/M2CouplantFx.cs`（约 70~90 行，配置驱动），承担薄膜动画全部职责（对齐、sprite、Filled 揭示、停留、淡出、Reset 复位）；`M2FlowController` 只保留门控、阶段推进与按钮/音效状态。
- `M2CouplantFx` 由 Flow 在 `Awake` 运行时 `AddComponent` + 注入引用（不写入 Scene，保住 AC7）；组件只改运行时状态，不触碰冻结 Scene。
- `M2FlowController.cs` 删除旧 `CouplantAnim` 协程与 `couplantAnimDuration` 字段（迁至新组件），预计净减约 10 行（145 → ~135）。
- `M2RuntimeSmoke.cs` 同步更新：快进断言改为把 `M2CouplantFx` 的 anim/hold/fade 三时长置零，并增加薄膜 sprite/颜色/fill 断言。
- 其他 M2 runtime 脚本（`M2ProbeDrag`/`M2RulerDrag`/`M2IdleHelp`）如无必要不修改。

## Acceptance Criteria

- [x] AC1：涂抹后出现蓝色铁轨形状薄膜，与 `railBg` 显示的铁轨精确重合，无错位/拉伸变形。
- [x] AC2：动画严格从左至右逐步出现（左缘先显现，向右铺满）。
- [x] AC3：完整覆盖后停留约 0.5s，再淡化消失；随后进入「探头定位与偏角」阶段，按钮变「已涂抹」。
- [x] AC4：Reset 后恢复初态，可重新涂抹；涂抹动画被 Reset 中断时无残留状态。
- [x] AC5：QA/Modal 暂停期间动画不推进，恢复后继续。
- [x] AC6：Unity 编译无 Error；`M2CouplantFx.cs` ≤150 行（68）、`M2FlowController.cs` ≤150 行（149）。
- [x] AC7：`Assets/Settings/Scenes/M2.unity` SHA-256 实施前后不变（fbb801e4…）；无新增素材文件；`M2RuntimeSmoke` 通过（PASS）。

## Out Of Scope

- M3 的 Intro 耦合剂动画同步（如需，另立任务）。
- 透视视图下的薄膜表现（耦合剂阶段固定普通视图）。
- 修改钢轨/探头/尺子素材、修改 Scene、调整 10°/110mm/150mm 教学参数。

## 实施记录（2026-08-15 定稿）

- 定稿参数：`coverRect=(0.005, 0.222, 0.993, 0.553)`（铁轨主体实心块，四边贴钢轨/铁轨边缘）、`filmColor=(0.55, 0.80, 0.96, 0.45)` 浅蓝、`animDuration=2s`、`holdDuration=0.5s`、`fadeDuration=0.5s`。
- 实现：`M2CouplantFx.cs`（68 行）由 `M2FlowController.Awake` 运行时 `AddComponent` 挂载（`[System.NonSerialized]` 防写回冻结 Scene）；`Sprite.Create` 从 `俯视角.png` 切出 coverRect 子区域保持铁轨形状与边缘羽化；Filled 从左至右揭示 + CanvasGroup 淡出。
- 验收：`M2RuntimeSmoke.RunBatch` 全链路 PASS；M2.unity SHA-256 前后一致（fbb801e4…）；两脚本 ≤150 行。
