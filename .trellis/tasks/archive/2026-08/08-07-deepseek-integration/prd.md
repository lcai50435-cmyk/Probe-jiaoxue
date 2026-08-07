# DeepSeek 真实接入与数字人动画状态接口预留

## Goal

1. **真实接入**：M1 AI 提问面板接入 DeepSeek API，替换现有模拟回复，实现"边做边问"的真实专业问答。
2. **动画接口预留**：数字人三态动画（待机/思考/说话）的触发点即 AI 回答生命周期，本次接入时把状态定义成事件接口，未来动画组件零侵入订阅。
3. **配置化**：全部 API 参数进 Inspector 字段（apiKey 由用户手填，不入库）。

## 背景与已确认事实

- `M1QAPanel.Send()`（`Assets/Scripts/M1QAPanel.cs:113`）当前为同步模拟回复，注释已标明"接入 DeepSeek 后替换为真实接口返回"。
- 规格书 2.3 数字人状态动画：**待机**（无操作）/ **思考**（AI 大模型生成回答时）/ **讲解**（播报引导、原理讲解时，即"说话"）。
- 规格书 3.3 交互流程：发送问题 → 数字人"思考"动画 + 气泡"正在思考..." → API 返回 → "讲解"动画 + 逐字显示回答 → 完成回待机。
- 已决策（用户确认）：
  - Unity 端 `UnityWebRequest` 直连 DeepSeek OpenAI 兼容 API，**不部署 Fay 后端**；Fay 方案留作后续多端/语音播报备选。
  - API Key 在 Inspector 手填，Setup 不写值、不入 git。
- 动画接口形态：`M1QAPanel` 暴露 `AnswerState` 事件（Idle/Thinking/Speaking），未来动画组件（M1AvatarAnimator 之类）订阅；**本次不实现动画消费方**（YAGNI）。
- **需求变更（2026-08-07，用户发设计稿标注）**：用户要求实现三处交互——关闭按钮可关闭面板、语音按钮可触发语音输入（规格书 3.3：转文字后填入输入框）、发送按钮可发送文字内容。关闭/发送代码已实现（上会话）；**语音方案已确认（用户选方案 1）**：保持占位，点击给出气泡反馈“语音输入功能待接入，请先用文字输入提问”——因 Unity 6（6000.3，`UnityEngine.Windows.Speech` 已移除、无 .NET Framework API Level）Windows 桌面无低成本内置语音识别，真实 ASR 留待后续（Fay/多端方案）。
- **兜底策略**：`M1QAPanel.Awake` 对 `deepSeekClient` 做运行时兜底（`GetComponent ?? AddComponent`），场景未跑 Setup 也可开箱即用；Setup 注入仍优先且幂等。
- 当前 `08-07-m1-ui-visual-optimization` 任务独立进行中（布局/视觉），本任务**不改任何布局与视觉参数**，避免两个任务互相污染。

## Requirements

- **R1** 真实对话：发送问题 → POST `{baseUrl}/chat/completions`（非流式）→ 返回文本逐字显示。
- **R2** 状态反馈：请求期间消息列表显示"正在思考..."气泡，发送按钮置灰，禁止重复发送。
- **R3** 失败兜底（防卡死）：网络错误 / 超时 / HTTP 非 200 / 空回复 → 气泡显示友好错误提示，状态回 Idle，可继续提问。
- **R4** 配置化：`baseUrl`、`apiKey`、`model`、`systemPrompt`、`temperature`、`timeout` 全部 Inspector 字段；`apiKey` 无默认值，留空发送时给明确提示（气泡 + LogWarning），不发起请求。
- **R5** 动画状态接口：`AnswerState` 枚举 { Idle, Thinking, Speaking } + `OnAnswerStateChanged` 事件；Thinking 在请求发出时触发，Speaking 在回复开始逐字时触发，Idle 在逐字完成/失败/超时时触发。
- **R6** 低代码合规：新脚本 `M1DeepSeekClient.cs` ≤150 行、配置驱动；独立组件（M2/M3 可复用）；`M1QASetup` 挂载幂等。
- **R7** 存量兼容：语音按钮点击给提示气泡（真实 ASR 后续接入）；`M1ToolSelection`、`M1PressDetector`、场景布局零改动。

## Acceptance Criteria

- [ ] 编辑器运行：发送问题 → 真实 DeepSeek 回复逐字显示（用真实 apiKey 验证）。
- [ ] 请求期间：发送按钮置灰、消息列表出现"正在思考..."气泡。
- [ ] 断网 / 错误 key / 超时场景：气泡显示错误提示，不卡死，可继续发送。
- [ ] Inspector 可配置 baseUrl/apiKey/model/systemPrompt/temperature/timeout；apiKey 留空发送有明确提示且不发起请求。
- [ ] `OnAnswerStateChanged` 在 Thinking→Speaking→Idle 三个时机正确触发（验收时临时挂 Debug.Log 验证后移除）。
- [ ] `M1DeepSeekClient.cs` ≤150 行；`Tools/M1/Setup AI 提问面板` 重复执行幂等。
- [ ] 与布局任务互不污染：本任务改动文件仅限 M1QAPanel.cs、M1DeepSeekClient.cs（新）、M1QASetup.cs。

## Out of Scope

- 语音输入真实功能（保持占位按钮 + 点击提示气泡，真实 ASR 后续随 Fay/多端方案接入）。
- 数字人动画实现（仅预留事件接口，动画为后续独立任务）。
- Fay 后端部署 / TTS / ASR。
- 流式输出（SSE）与多轮上下文管理（后续按需）。
- M2/M3 场景接入（复用 M1DeepSeekClient 即可，后续任务）。
