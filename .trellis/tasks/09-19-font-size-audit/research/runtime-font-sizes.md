# 运行时动态创建文本字号盘点（只读分析）

Scene 里"容器为0/运行时决定"的文本，以及完全由代码创建的 UI，在这里逐个核对。
字号单位 px（1920x1080 业务坐标）。

## 1. 代码创建 UI 的字号与容器（Assets/Scripts/）

| 创建点 | 文本 | 字号 | 容器 | 尺寸策略 | 放大评估 |
|---|---|---|---|---|---|
| `M1QAPanel.AddMessage`（M1~M5 问答气泡） | 问答消息正文 | 28 | 气泡按 `GetPreferredValues(bubbleMaxWidth=480)` 自适应宽高，内边距 32x20 | 气泡随文字生长，列表可滚动 | ✅ 可 +2~4；气泡自动变大不越界（列表宽 580 > 480） |
| `M3DigitalHumanBootstrap`（M3/M4/M5 运行时 QA 面板） | Header 标题"向铁小探提问" | 34 | 400x60（点锚定，PanelWidth=580） | 固定框 | ✅ 可 +2~4（6 字*38=228 ≤ 398，高 45.6 ≤ 58） |
| 同上 | 关闭按钮"×" | 36 | 64x64 | 固定框 | ⚠️ 保持（图标性质，36 已占视觉主体） |
| 同上 | 语音按钮 | 30 | 76x78 | 固定框 | ⚠️ 最多 +2（2 字*32=64 ≤ 74） |
| 同上 | 发送按钮 | 32 | 106x78 | 固定框 | ✅ 可 +2~4（2 字*36=72 ≤ 104） |
| 同上 | 输入框 Placeholder/正文 | 30 | 360x78（内区 336x66） | 固定框 | ❌ 保持（"请输入问题（最多200字）"30px ≈ 352px 已顶满换行） |
| 同上 | 字数计数"0/200" | 22 | 120x24 | 固定框 | ❌ 保持（行盒 26.4 > 24，本就贴边） |
| `ModuleSpeechBubble`（M2~M5 数字人台词云朵） | 台词正文 | 26（代码覆盖 Scene 36） | 云朵为老板手工建（Scene dialog 节点），文字区 300x198（M5 专属 264x198） | `preserveExplicitLineBreaks=true` + 自动缩放 16~26 | ⚠️ 保持或最多 +2（云朵固定，超限自动缩回 26 以下；样式为老板定稿合同） |
| `M2FlowController.EnsureClickHint` | "点击这里"漂浮提示 | 26 | 220x40（锚在涂抹按钮上方） | 固定框 | ✅ 可 +2（4 字*28=112 ≤ 218，高 33.6 ≤ 38） |
| `ModuleHintOverlay.EnsureActionHint` | 操作动作提示（漂浮） | 30 | 600x78（DontSave，悬浮于场景上部） | 固定框，可两行 | ✅ 可 +2~4（两行 72 ≤ 76 内放不下 34，+2 稳妥；增大仍不超框，但与场景元素距离近，需截图确认） |
| `M3ProbeDrag/M4ProbeDrag` | 角度标题"探头偏角" | 24 | AnglePromptLayout 按 slider 布局 | 代码布局 | ⚠️ 最多 +2（与滑条间距耦合） |
| `M5IdleHelp` | "需要/不需要"按钮 | 26 | 160x60 | 固定框 | ✅ 可 +2~4（3 字*30=90 ≤ 158） |
| `ModuleToolShelfLabel`/Scene ToolShelf Chip | 工具名（K2.5 探头/多功能尺子） | 22.4 | 200x50 | 固定框 | ✅ 可 +2~4（"多功能尺子"5 字*26=130 ≤ 198） |
| `M1IntroVideo` 字幕 | 引导/讲解字幕 | 34（webglSubtitleFontSize 可配） | 宽 1100、底边下 20px、最多两行 | 微信三合同定稿 | ❌ 本次不动（合同参数，动则需重新走微信验收） |

## 2. Scene 序列化但运行时会改写文案的（值以代码为准）

| 节点 | Scene 字号 | 运行时行为 |
|---|---|---|
| `M2 ScanControls/Hint`、`MeasureControls/Hint`、`InstructionArea/Text` | 26 | `M2FlowController` 按 Stage 重写文案（DefaultHints），容器固定 360x60 / 420x60 / 648x60 |
| `M3/M4 StepProgress/Text`、`InstructionArea/Title` | 27 | FlowController 重写步骤文案 |
| `M5 StepProgressText`、`InstructionText` | 26 | `M5FlowController` 重写 |
| `M2~M5 ResetConfirmDialog/Title` | 34 | 运行时共享弹窗样式按场景改名（M5 Scene 里仍是旧"M2 流程"字样，运行时正确） |
| `M2 MeasurementBubble/Text` | 28 | 检出后显示"测量完成"（PPT 合同），高度 0 由 Transform 摆放 |
| `M2~M5 DetectionBanner/Text` | 21~24 | 检出横幅，悬浮在波形窗上（无遮挡物，但放大易压波形网格） |

## 3. 结论要点（配合 scene-font-audit.md 全量表）

- **能放大的主流类别**：按钮文字（视图切换 24→26+、重置流程 28→32、弹窗按钮 28~30→32~36、
  完成按钮 30→34、下一步 28→32、开始探测 36→40+）、模块标题 36→40、QA 面板标题 34→38、
  问答气泡 28→30~32、工具架标签 22.4→24~26、帮助面板文字 26→28。
- **不要动的**：波形刻度数字（12/14/16px，容器 20~24px 高，本就贴边）、字数计数 22、
  输入框 Placeholder 30、关闭按钮 ×、引导视频字幕 34（合同）、数字人云朵台词 26（老板定稿样式）。
- **运行时改写文案的提示行**：字号上调前必须核对"最长文案"在该字号下仍单行/不超框
  （如 M2 步骤条最长约 10 字，26→30 后 10*30=300 ≤ 358 仍安全）。
