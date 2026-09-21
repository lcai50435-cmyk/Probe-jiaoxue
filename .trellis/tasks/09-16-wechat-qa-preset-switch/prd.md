# 微信问答预设话术开关（方案 A）

父任务：`09-16-wechat-upload-compliance`（R4 第 4 点老板拍板选方案 A）。

## Goal

给微信端问答加数据源开关：**体验版 = CloudProxy（现状，云函数实时 AI）**；**正式提审版 = PresetLibrary（预设话术库，零网络请求、无生成式内容）**，使个人主体小游戏可以正常通过「深度合成」类目审核。

## Key Decisions

- 开关放 `AiProxyConfig.qaMode`（枚举 CloudProxy=0 / PresetLibrary=1）：该资产本就是微信端问答专属配置，随包序列化，git 可审计当前导出模式；旧资产缺字段反序列化得 0=CloudProxy，体验版行为零变化。
- **安全封闭**：qaMode=PresetLibrary 时绝不走云函数；话术库资产缺失时报「AI 问答暂未开放」而不是回落实时 AI——宁可功能缺失不出合规事故。
- 预设话术匹配：关键词计分（命中数最多者优先），无命中走 defaultAnswer；回复前按 `replyDelay`（unscaled 真实时间，遵守问答暂停合同）模拟思考，复用"正在思考..."气泡逐字替换链路，`M1QAPanel` 零改动。
- Android/Editor 不动：仍走 DeepSeekConfig 直连（Android 仅供 USB 真机验收、永不上传，无合规诉求）。
- 话术文案进 `Assets/Resources/PresetQALibrary.asset`（数据不进代码，规范 §3）；字体由 `WeChatFontSubsetTool` 自动扫描 Resources 资产覆盖，文案改动后重跑一次子集工具即可。

## Requirements

- `AiProxyConfig` 增 `qaMode` 字段与 `UsePresetLibrary` 属性；`M1DeepSeekClient` WebGL 分支：`IsConfigured` 放行预设模式，`ChatAsync` 在预设模式走本地匹配、不发起任何网络请求。
- 新增 `PresetQALibrary`（ScriptableObject，≤150 行）+ 种子话术资产（**26 条主题条目 + 兜底**，2026-09-16 老板确认扩充规模）：问候/感谢/身份/能力、操作引导（下一步/卡住/帮助按钮）、五环节流程、耦合剂、擦拭（《安规》原文）、探头放置与工作原理、角度调整（how/why 两条）、伤损检出、出波距离 110·120·40mm、为什么 110mm（焊缝熔合线印证口径）、多功能尺、波形始波/回波、透视视图、视图切换、焊缝、三位一体、超声波原理、探伤定义、工具清单、M1 仪器选择 + 兜底回复。
- **新工艺长条目（2026-09-21 老板提供原文，26 条）**：「铝热焊缝轨头下颚伤损探测新工艺」五段全文（K2.5 单探头 / 顶面 10°·110mm / 侧面 13°·120mm / 轨腰向上 10°·40mm / 三方位互补验证），双引号 YAML 字符串内嵌 `\n` 分段。触发关键词：铝热焊/铝热/下颚/新工艺/本节课/我们学/K2.5/探头（置于三位一条目之前，"什么是新工艺"同分时优先命中长条目）；已验证 24 问法仿真不破坏原有分流。文本含「颚」字已确认在现有微信字体子集 charset.txt 内，无需重跑子集工具。
- 匹配调优记录：关键词计分同分时靠前条目优先——已用 24 问仿真校准（贪婪词"怎么"从探头条目移除、"调整"从角度 how 条目移除、擦拭条目前置、M1 选择补"仪器"、why 角度补"偏角"），24/24 问法命中预期条目。
- 话术自称「铁小探」，文案不出现"AI"字样（避免触发审核对生成式服务的注意）。

## Acceptance Criteria

- [x] qaMode=CloudProxy（现状默认）时 WebGL 问答行为与当前完全一致（2026-09-19 导出验证）。
- [x] qaMode=PresetLibrary 时：2026-09-21 老板在微信开发者工具实测通过——预设回复（含新工艺长条目触发）+ Network 零请求。
- [x] qaMode=PresetLibrary 且话术库资产缺失：代码路径按未开放处理（安全封闭为设计约束，ChatPresetAsync 首行守卫）。
- [x] Android/Editor 直连路径与问答回归不变（改动全部限定 UNITY_WEBGL 分支，静态核对）。
- [x] Unity 编译无 error CS（2026-09-19 14:05 导出产物二进制验证：global-metadata.dat 含 PresetQALibrary×8 符号、data.unity3d 含 PresetQALibrary.asset；导出成功本身要求编译通过）；预设文案字符全部可由微信字体子集覆盖（371 非ASCII字符逐字核对 charset.txt，无需重跑子集）。

## 导出模式操作单（记入父任务 checklist）

- 体验版导出：`AiProxyConfig.asset` → qaMode=CloudProxy。
- 正式提审版导出：qaMode=PresetLibrary，并重跑微信字体子集。

## Out of Scope

- 云函数/DeepSeek 本身改动；M1QAPanel UI 改动；正式提审流程办理。
