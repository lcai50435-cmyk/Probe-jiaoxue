# DeepSeek 真实接入与数字人动画状态接口预留 — 技术设计

## 1. 架构与边界

```
M1QAPanel (UI 层, 既有)
  ├── OnAnswerStateChanged 事件  ← 动画组件未来订阅（本次不实现消费方）
  └── 持有 M1DeepSeekClient 引用（Inspector 注入）
        └── UnityWebRequest → DeepSeek OpenAI 兼容 API（非流式）
```

改动文件：
- **新增** `Assets/Scripts/M1DeepSeekClient.cs`（runtime，~110 行）：唯一网络层，配置驱动，M2/M3 复用。
- **修改** `Assets/Scripts/M1QAPanel.cs`：`Send()` 同步模拟 → 异步真实请求；新增 AnswerState 枚举与事件；`TypeText` 支持复用"思考"气泡。
- **修改** `Assets/Editor/M1QASetup.cs`：确保场景存在 `M1DeepSeekClient` 组件并注入引用（幂等）。
- **零改动**：场景布局、M1ToolSelection、M1PressDetector、语音按钮逻辑。

## 2. 状态机（核心契约）

```
Send(question)
  ├─ apiKey 为空 → 错误气泡（不发请求）
  ├─ AddMessage(用户气泡)
  ├─ Thinking:  AddMessage("正在思考...") 记录为 _thinkingBubble；OnAnswerStateChanged(Thinking)
  ├─ ChatAsync 协程：
  │    ├─ 成功 → Speaking: 复用 _thinkingBubble 逐字显示回复；OnAnswerStateChanged(Speaking)
  │    │        → 逐字完成 → Idle: OnAnswerStateChanged(Idle)
  │    └─ 失败/超时 → Idle: _thinkingBubble 文本替换为错误提示（不逐字）；OnAnswerStateChanged(Idle)
  └─ 全程 _busy=true：发送按钮置灰、Send() 直接 return（防重入）
```

要点：
- "正在思考..."气泡不删除，回复到达后**复用该气泡对象**逐字替换文本（避免中途删气泡的列表跳动）。
- `TypeText` 增加可选参数 `bubble`（已有气泡则复用），保持现有逐字/尺寸/滚动逻辑不变。
- `_typing` 语义扩展为 `_busy`（请求中或逐字中），`UpdateSendInteractable` 沿用。

## 3. 契约

### M1DeepSeekClient（Inspector 字段，均带默认值，apiKey 除外）
| 字段 | 默认值 | 说明 |
|---|---|---|
| `baseUrl` | `https://api.deepseek.com/v1` | OpenAI 兼容端点 |
| `apiKey` | 空 | 用户手填，Setup 不写值 |
| `model` | `deepseek-chat` | 对话模型 |
| `temperature` | 1.0f | 0~2 |
| `systemPrompt` | "你是'铁小探'，钢轨探伤仿真教学助手…" | 人设，Inspector 可改 |
| `timeout` | 30f | 秒，超时走失败分支 |

接口：`public IEnumerator ChatAsync(string userMessage, Action<string> onSuccess, Action<string> onError)`
- POST `{baseUrl}/chat/completions`，Header：`Content-Type: application/json`、`Authorization: Bearer {apiKey}`
- 请求体 `{ "model":…, "messages":[{"role":"system","content":…},{"role":"user","content":…}], "temperature":… }`（JsonUtility 序列化）
- 响应解析 `choices[0].message.content`；非 200 / 解析失败 → onError(中文提示)

### M1QAPanel 新增
```csharp
public enum AnswerState { Idle, Thinking, Speaking }
public event Action<AnswerState> OnAnswerStateChanged;
public M1DeepSeekClient deepSeekClient;   // Inspector 注入
```
- 事件仅用于状态通知；动画组件未来订阅后自行切换序列帧（待机/思考/说话）。

### M1QASetup 新增
- `画板` 上确保 `M1DeepSeekClient` 组件存在（无则 AddComponent），`comp.deepSeekClient` 注入引用，apiKey 不动（保留用户手填值，幂等）。

## 4. 权衡与风险

- **非流式**：简单可靠，教学演示延迟可接受；后续需要打字机流式体验时再改 SSE（契约不变，只改 ChatAsync 内部）。
- **apiKey 在 Inspector**：会随场景文件保存（含 git），演示项目可接受；风险与对策：场景文件不入公开仓库 / 上线前换后端代理。验收时提示用户。
- **单轮问答**：无多轮上下文（每次只带 system+本次 user）。规格书未要求多轮记忆，YAGNI。
- **无法本环境跑 Unity**：代码由用户在编辑器验证（acceptance 见 prd.md）。
- **回滚**：`git checkout -- Assets/Scripts/M1QAPanel.cs Assets/Editor/M1QASetup.cs` + 删除新脚本；Setup 幂等可重跑。

## 5. 明确不做（YAGNI）

- 动画消费方（仅事件接口）、语音输入、Fay 部署、SSE 流式、多轮上下文、错误重试/限流。
