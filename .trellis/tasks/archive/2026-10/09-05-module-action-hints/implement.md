# 执行计划：M2「点击这里」+ M3/M4 拖尺提示修复

## 前置

- 本任务是父任务 09-03 的 R1+R2 独立交付；prd.md 已含全部验收标准。
- 约束：冻结 M2/M3/M4 Scene 零写回；低代码优先；runtime 新增 ≤150 行（预计全部为存量文件内小改动 + 复用 ModuleHintOverlay）。

## 有序清单

1. **R1 定位**（M2FlowController.cs）：
   - 确认 Couplant 阶段 `applyButton` 可用时（Awake 后）创建「点击这里」DontSave TMP；父节点挂 applyButton 或 flow transform，锚定 applyButton 上方/旁。
   - 显示：Couplant 阶段 + 未点击涂抹时（`CouplantApplied==false`）；点击 `ApplyCouplant()` 首行隐藏（含动画播放期间）；`ResetAll()` 恢复显示。
   - 复用 `ModuleHintOverlay.EnsureActionHint` 风格（字体从 instructionText/applyButtonText 拷贝），文本「点击这里」，样式浅色、raycastTarget=false。
   - 注意：M2 场景 instructionText/applyButton 引用在冻结 Scene 已序列化（非空）；若为空需运行时 FindDeep 兜底（照 M3 先例）。
2. **R2 修复**（M3FlowController.cs:150 / M4FlowController.cs:150）：
   - `NotifyPlacementChanged()` 空实现 → 改为调 `UpdateUi()`（对齐 M2）。核对放置探头回调上下文：`PlaceAtStart` 在 probeDrag 拖放成功调用；`UpdateUi` 会按 `probeDrag.Placed` 刷新 actionHint 到「拖动多功能尺至探头处」。
   - 确认无需 `ShowAngleGuide`（M3/M4 尺子吸附走独立 `CheckPositioning`，不引入）。
   - 复查 M3/M4 `EnterPositioning`→`UpdateUi` 初态与放置后刷新不破坏「尺子吸附→滑动此处调整偏角→撤尺」流转。
3. **编译验证**：runtime 全量 Roslyn/csc 零 error（Unity 6000.3.21f1 程序集）。
4. **Play 验收**：
   - M2：RunBatch/Play 后进 Couplant 见「点击这里」→ 点涂抹按钮消失 → ResetAll 重现。
   - M3/M4：放探头成功 actionHint 即「拖动多功能尺至探头处」→ 拖尺吸附 →「滑动此处调整偏角」。
   - 若 M3/M4 RuntimeSmoke 存在且覆盖放置链，改后重跑确认无回归。
5. **冻结哈希**：任务前后对比 M2/M3/M4 Scene SHA-256；本次零写回。
6. **diff 检查**：`git diff --check` 干净；不触碰未提交历史改动（M2.unity 现有 presenter/probeDrag 字段序列化改动保持原样，不纳入本任务提交——除非老板要求一起审查）。

## 验证命令（提交前必跑）

- csc 编译：参考既有 trellis 会话用 Roslyn 编译 runtime 程序集的方式（零 error CS）。
- Scene 哈希：`M2/M3/M4RuntimeSmoke` 退出 PlayMode 已自动校验哈希（Error 即失败）；也可 `git diff --stat Assets/Settings/Scenes/M{2,3,4}.unity` 人工确认零 diff。
- `git diff --check`。
- Play 冒烟：M2/M3/M4 各跑一次放置→吸附→扫描入口（Editor 人工或 Smoke 工具）。

## 风险

- M2 按钮旁新增 DontSave 文本可能与既有底部操作带重叠：先按按钮 rect 上方定位，Play 目视微调 anchoredPosition（不写回 Scene）。
- M3/M4 `UpdateUi` 触发时机若在 Awake 早期（引用未就绪）需判空；照 M3 instructionText 重绑先例兜底。
- 并行会话正改 M2.unity（未提交改动）：改前先 git diff 记录基线，避免与并行改动冲突；若需先处理冲突交老板决策。
