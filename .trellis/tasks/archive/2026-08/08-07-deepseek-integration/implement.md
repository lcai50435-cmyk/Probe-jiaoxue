# DeepSeek 真实接入与数字人动画状态接口预留 — 实施计划

## 实施清单（按序）

1. **新建 `Assets/Scripts/M1DeepSeekClient.cs`**（runtime，≤150 行，namespace M1）：
   - Inspector 字段：`baseUrl`(https://api.deepseek.com/v1)、`apiKey`(空)、`model`(deepseek-chat)、`temperature`(1.0)、`systemPrompt`(铁小探人设)、`timeout`(30)。
   - `public IEnumerator ChatAsync(string userMessage, Action<string> onSuccess, Action<string> onError)`：
     UnityWebRequest POST `{baseUrl}/chat/completions`，Bearer 认证，JsonUtility 序列化/解析，非 200 或解析失败 → onError(中文提示)；timeout 用 `request.timeout`。
   - 挂到 `画板`（Setup 负责，脚本自身不查找场景）。

2. **修改 `Assets/Scripts/M1QAPanel.cs`**：
   - 新增 `public enum AnswerState { Idle, Thinking, Speaking }`、`public event Action<AnswerState> OnAnswerStateChanged;`、`public M1DeepSeekClient deepSeekClient;` 字段。
   - `Send()` 改造：
     - `_busy` 防重入（替换原 `_typing` 语义，发送中+逐字中都置 true）。
     - apiKey 为空 → 错误气泡 + LogWarning，不发起请求。
     - AddMessage 用户气泡 → `AddMessage(false, "正在思考...")` 记 `_thinkingBubble` + 触发 Thinking → `StartCoroutine(deepSeekClient.ChatAsync(question, …))`。
     - 成功回调：触发 Speaking，`StartTyping(reply, _thinkingBubble)`（复用思考气泡逐字）。
     - 失败回调：`_thinkingBubble` 文本替换错误提示 + `ApplyBubbleSize` 刷新尺寸，触发 Idle。
     - 逐字完成 → 触发 Idle。
   - `StartTyping` / `TypeText` 增加可选参数 `MessageBubble reuseBubble = null`：传入则复用（跳过 AddMessage），逻辑其余不变。
   - 语音按钮保持占位（零改动）。

3. **修改 `Assets/Editor/M1QASetup.cs`**：
   - `SetupQAPanel()` 内：`画板` 确保 `M1DeepSeekClient` 组件存在（无则 AddComponent），`comp.deepSeekClient` 注入；**不写 apiKey**（保留手填值）；SetDirty + 保存场景。

4. **静态检查**：确认 `Assets/Scripts/*.cs`、`Assets/Editor/*.cs` 无编译错误（IDE/用户 Unity 控制台）；`M1DeepSeekClient.cs` 行数 ≤150。

5. **人工验证（用户 Unity 编辑器）**：
   - 打开场景 M1，`画板` Inspector：确认 `M1DeepSeekClient` 组件存在、`M1QAPanel.deepSeekClient` 已引用；填写 apiKey。
   - 运行：发送问题 → 消息列表出现"正在思考..." → 真实回复逐字显示；期间发送按钮置灰。
   - 逐字完成后按钮恢复；再发一条验证正常。
   - 失败场景：清空 apiKey 发送 → 明确提示且不发请求；断网发送 → 错误气泡、不卡死。
   - 事件验证：临时在 `OnAnswerStateChanged` 挂 Debug.Log（或 Console 观察）确认 Thinking→Speaking→Idle 顺序，验证后移除。
   - 幂等：重跑 `Tools/M1/Setup AI 提问面板` 无报错、无重复组件、apiKey 保留。

## 验证命令

- 无 CLI 可用（Unity 项目）；以编辑器人工检查对照 prd.md Acceptance Criteria 逐条勾选。
- 静态检查：`rg -n "AnswerState|ChatAsync|deepSeekClient" Assets/` 确认改动点齐全；`wc -l Assets/Scripts/M1DeepSeekClient.cs` 确认 ≤150。

## 风险文件与回滚点

- `Assets/Scripts/M1DeepSeekClient.cs`（新）、`Assets/Scripts/M1QAPanel.cs`、`Assets/Editor/M1QASetup.cs`。
- 回滚：`git checkout --` 上述文件 + 删除新脚本；Setup 幂等可重跑。
- 注意：工作区另有 m1-ui-visual-optimization 任务的未提交改动（M1QASetup.cs 面板宽度/字号常量），两任务改动同文件时以各自 prd 为界，提交时注意分离（可交互式 add 或先合并视觉任务验收）。

## 复查项（验收阶段）

- 真实 apiKey 对话质量与人设生效情况（systemPrompt 可调）。
- 逐字速度（typeSpeed 0.035s）在长回答下的观感；如需可改 Inspector。
- apiKey 入库风险提示（场景文件含 key，教学演示可接受；上线前建议改后端代理）。
