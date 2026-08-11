# Journal - lcai (Part 1)

> AI development session journal
> Started: 2026-08-07

---


## 2026-08-10 — trellis-implement: 数字人全身头像与问答动画联动（08-10-digital-human-avatar-qa-integration）

已实施（4 改 + 1 新增 runtime 组件 + meta）：
- `Assets/Scripts/M1PressDetector.cs`：新增 `OnShortPress`；长按触发后置 `_longTriggered` 抑制抬手短按（R4 互斥）。
- `Assets/Scripts/M1QAPanel.cs`：新增 `OnPanelVisibilityChanged(bool)`（Open 开始 true / 完全滑出隐藏 false）；新增 `bindPressTarget` 兼容开关（Setup 置 false，输入移交 Presenter）。
- `Assets/Scripts/M1DigitalHumanPresenter.cs`（新，148 行）：短按切全身/头像（面板打开忽略）；长按记形态+自动展开+开面板；AnswerState Idle/Thinking/Speaking 切三视频循环；面板关闭后恢复提问前形态（请求未结束延后到 Idle）；强制 `audioOutputMode=None`。
- `Assets/Editor/M1QASetup.cs`：Ensure ChatArea（右侧预留 344px）+ QAPanel 重挂其下（580 宽、hiddenOffsetX 960）；Ensure DigitalHumanStage（根级最后兄弟，320 宽/24 边距，盖过 Blocker）；FullBodyView（RawImage+LumaKey+AspectRatioFitter(1080/1450)+VideoPlayer+透明点击层+PressDetector）；AvatarView（A-05 头像 120px+PressDetector）；Presenter 注入；隐藏旧背景圆；仅引用三个指定 MP4，不加载 WebM；幂等自愈。
- `Assets/Editor/M1LayoutPolish.cs`：虚构 `QAPanel/Panel/...` 路径全部改为真实层级；面板宽 780→580、输入行排布同步（输入 360/语音 388/发送 472）。

验证：runtime+editor 全量 csc 编译零错误（Unity 6000.3.21f1 程序集）；`git diff --check` 干净；Presenter 148 行 ≤150；无残留 `QAPanel/Panel` 路径；状态视频 None/开场引导 Direct 保持；素材无 WebM；前置任务三个 mp4.meta（importAudio:0）未触碰；未输出 API Key。

未完成（人工验收）：项目被运行中的编辑器占用（Unity.exe PID 36436），未跑批处理 Setup。需用户在编辑器内：等脚本重编译 → 运行 Tools/M1/Setup AI 提问面板 两次（验证幂等）→ PlayMode 验证三态动画/长短按/自动展开恢复/三分辨率布局。

## 2026-08-10 — trellis-check 复核（数字人任务第二轮）

复核结论（3 项修复已确认/修正）：
1. **RT mipmap 可靠性**：原「GPU 每帧兜底」表述无依据，已修正。Unity 6000.3 反射确认 API 名为 `RenderTexture.GenerateMips()`（旧名 GenerateMipMaps）；Presenter 在 `Update` 中显式逐帧调用（仅 fullbody 可见时），配合 `useMipMap+autoGenerateMips=true` 原生分辨率 RT，确定性、平台无关；同步改写 video-intro.md 与 AGENTS.md 摘要。
2. **常驻材质**：`EnsureResidentLumaKeyMaterial` 改为从共享 intro 材质 `new Material(introMat)` 迁移创建（继承 Color/stencil 等全部属性），仅收窄 `_KeySmooth=0.006`（阈值 0.02 不变），独立保存 `UI-LumaKey-DigitalHuman.mat`；存在则保留用户调参；FullBodyView 自愈条件覆盖「仍指向 intro 材质」的旧场景。
3. **同位切换 (0,-248)**：Setup 中 FullBody/Avatar 均 anchor(0.5,0.5)+pivot(0.5,0.5)+pos(0,-248)，重跑无条件自愈；当前场景 AvatarView 仍是旧右上角布局（anchor 1,1 / pos -16,-16）、FullBodyView RawImage 仍指向 intro 材质 —— **必须重跑 Setup 自愈**（不手改 YAML）。

验证：runtime+editor 全量 Roslyn 编译零错误（Unity 6000.3.21f1）；Presenter 148 行 ≤150；场景无重复节点、无 `QAPanel/Panel` 虚假路径、无 WebM 引用；状态 VideoPlayer `audioOutputMode=None`、开场引导保持 Direct；三个 clip guid 严格对应指定 MP4；QAPanel `_busy` 重置移除与 R7 一致（旧 Close 本不停止协程，重置是遗留保险）；`git diff --check` 仅剩 Unity YAML `m_Name: ` 尾随空格（生成格式）。

用户待办：编辑器内重编译后运行 `Tools/M1/Setup AI 提问面板` 两次（幂等验证），再人工 PlayMode 验收三态/长短按/自动展开恢复/三分辨率；首轮验收截图未在本机找到，-248 中心值按 R13 设计意图核对。
