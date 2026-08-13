# M2 技术设计（索引）

## 权威来源

本任务的技术设计以审计任务已审核版本为唯一权威，不在此重复维护：

- **完整设计文档**：`.trellis/tasks/08-10-m2-readiness-audit/design.md`
  - 画布与响应式基线（第 3 节）
  - 页面线框（第 4 节）
  - 推荐 UGUI 层级（第 5 节）
  - 四阶段交互流程（第 6 节）
  - 波形与距离契约（第 7 节）
  - 普通/透视模式（第 8 节）
  - 防卡死帮助（第 9 节）
  - Header/重置/问答（第 10 节）
  - 视觉与素材策略（第 11 节）
  - 低代码实现边界（第 12 节：M2Setup + 4 个 runtime 组件 + 配置化字段）
  - 组件数据流（第 13 节）
  - 完成状态与出口（第 14 节）
  - 验证计划 / 风险与回滚 / 规划审核门槛（第 15~17 节）

- **素材与需求基线**：`.trellis/tasks/08-10-m2-readiness-audit/audit.md`
- **执行清单**：`.trellis/tasks/08-10-m2-readiness-audit/implement.md`

## 2026-08-11 规划同步（用户最新决定）

权威规划已按用户最新决定同步修订，本节为增量摘要，实现时以 08-10 权威文档为准：

- **布局**：A/B/C/D 仅保留为逻辑分区与对象职责，不再表现为四个同权矩形面板；主教学场景全幅化/主导，工具、钢轨、C 模式控件自然融入。
- **B 区**：嵌入主场景的紧凑辅助仪器，1920 基准约 460x240（允许 440–480x220–250），低对比、不纵向贯穿、不抢视觉；单条实时波形与完整距离契约不变。
- **数字人**：右侧常驻无边框 DigitalHumanStage（约 300–320px 宽），默认全身待机；复用 M1 数字人链路（M1DigitalHumanPresenter / M1PressDetector / VideoPlayer / RenderTexture / UI-LumaKey-DigitalHuman 与待机/思考/讲解视频）；与 M1 一致，面板打开时全身显示、不被 Blocker 压暗或拦截；QAPanel 在数字人左侧展开；不保留 Header 文字/头像 QAEntry 作为主入口；不播放 M2 引导动画。
- **Header** 只保留标题与重置；D 区弱化卡片感但保持步骤/控件。
- **门槛 A**：第三版静态线框需重新生成三视口并经用户审核（第二版不作数）；静态审核使用可识别的全身预览或 Play Mode 运行截图，不只放头像。
- **里程碑五更名**：全身数字人与问答复用；静态阶段先预留舞台/构图，实际视频/Presenter/QA 在核心流程独立验收后接入。
- **保留**：无返回 M1、无引导、可配置下一模块出口、尺子/波形/交互已确认合同。
- **复用边界**：不复制 M1 QA/数字人逻辑，优先提取 Editor 公共 Ensure 或参数化复用；M1 行为回归必须检查。
- **2026-08-11（用户决定）：QAPanel 激活时全局暂停游戏**——长按数字人打开 QAPanel 时 `Time.timeScale=0`（含 M2 计时/拖拽/动画），关闭时恢复打开前值；由公共组件 `M1QAPanel` 新增 `pauseGameOnOpen`（默认开）统一实现，M1/M2 共用生效；数字人视频与面板滑入动画走 unscaled 不受影响。已同步至权威 design.md 第 10.3 节与验证计划。

## 核心边界速查

- 结构改动只通过幂等 `Assets/Editor/M2Setup.cs`，纯视觉微调可直接改场景。
- runtime 脚本仅 4 个：`M2FlowController`、`M2ProbeDrag`、`M2RulerDrag`、`M2WaveformGraphic`，各自 ≤150 行。
- FlowController 是步骤与成功状态的唯一所有者；ProbeDrag/RulerDrag 只报告事件，不直接改 UI 状态。
- 波形程序绘制单条实时曲线，不做序列帧。
- 数字人/问答复用 M1 组件与素材链路（M1DigitalHumanPresenter / M1PressDetector / M1QAPanel / M1DeepSeekClient / UI-LumaKey-DigitalHuman），不新增数字人 runtime 脚本；Editor 侧经参数化公共 Ensure 复用，不复制 M1QASetup 整段逻辑。
- 锚点全部以 RailViewport 本地归一化坐标保存，保证占位素材替换时交互坐标可复用。

实现前先按 `trellis-before-dev` skill 读取 `.trellis/spec/` 下的 low-code.md / video-intro.md 等规范。
