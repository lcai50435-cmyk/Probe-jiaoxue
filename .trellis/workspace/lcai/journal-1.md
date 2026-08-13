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

## 2026-08-11 — M2 里程碑一静态线框第二版（task 08-11-m2-implementation）

按用户审核反馈修订 `Assets/Editor/M2Setup.cs` 并重新生成 M2.unity + 三视口截图：

1. **QAEntry 裁切修复**：根因是旧锚点 pos(-24) + 宽120 使右边缘 1932 > Canvas 1920。改为 104x64、pos(-76)，右边缘 1872；按钮改用 `Assets/DigitalHuman/A-05 折叠态头像.PNG`（Multiple 子资产名 `[A-05 折叠态头像_0]` 兼容加载，M1QASetup 的裸 LoadAssetAtPath<Sprite> 对 Multiple 会返回 null 需留意），旧 "问答" 文字子节点幂等移除；点击区 104x64 ≥ 64px，不创建全身数字人。
2. **B 区 150mm 与目标线**：CurrentDistanceText pos(-24)/宽170、TargetDistanceText pos(-208)/宽160（间距 14px 不重叠）；110mm 目标竖线从占位 x=0 移到归一化 0.8（=(150-110)/(150-100)），不再遮挡 150 刻度；WaveGraphic 内新增平直基线占位 BaselinePlaceholder（绿色横线）。
3. **探头/尺子迁移**：K2.5 探头实体入 ProbeHome 暂存位（RailViewport 内旧 Probe 删除）；尺子实体入 RulerHome 常驻、置灰锁定（alpha 0.6 + 刻度文字），RailViewport 内旧 Ruler 删除。场景各仅剩 1 个 Probe/Ruler，父节点分别 = ProbeHome/RulerHome。
4. **C 区按钮**：普通/透视按钮 160x64（原 56px 不达标）；HelpControls 按钮同调 64 并改为上下布局避免与文字重叠。
5. **钢轨层次**：RailBase(深) + RailSurface(浅) + RailHighlight(顶部高光) + RailShadow(底部暗线) + WeldLine；交互坐标契约保持（钢轨 y 0.35~0.75、焊缝 x 0.62 不变）；未用用户临时正面图，纯 UGUI 色块（M1 简洁风格，无渐变发光）。
6. **幂等与隔离**：Setup 连跑两次 M2.unity 哈希一致 d3751393（TMP 重指向 1→0）；未打开/保存 M1（M1.unity diff 保持任务前 4 行不变）；M2Shot 三视口（1920x1080 / 1280x720 / 2436x1125）已更新 Logs/m2-shot_*.png。
7. **像素级验证**（PIL，非目视）：三视口 QAEntry 头像彩色像素存在、画布右边缘无内容（1280 边距 x1264..1280 干净）、基线绿线/目标黄线/轨面/高光/焊缝均在预期坐标渲染；2436 因逻辑高 974 需按 scale 重算坐标。
8. 日志注：退出 batchmode 时 TMP 包 `m_AtlasTextures` UnassignedReferenceException 为 TMP quit 回调无害告警（Setup 逻辑后发生）；`git diff --check` 仅剩 Unity YAML `m_Name: ` 尾随空格（生成格式）。

待用户审核截图 → 审核门槛 A 通过后进入里程碑二（4 个 runtime 组件骨架）。

## 2026-08-12 — trellis-implement: M2 功能总收口（08-12-m2-final-functional-closeout）

老板授权一次性定点解冻 M2 Scene（仅限正式尺子、完整 QA 子树/组件引用、Build Settings），完成后重新冻结；M1/M3 全程不碰。主 Unity Editor 占用项目，采用已验证的隔离副本模式执行。

实施（新增 1 个 Editor 工具 + 2 个场景/配置产物）：

1. `Assets/Editor/M2FinalCloseout.cs`（新增，一次性收口工具）：QALayer 下创建 ChatArea（右侧预留 336px）→ 复用冻结空根 QAPanel → 构建 Header/MessageList/InputRow 完整子树（结构同 M1，路径无虚构层）→ Blocker 本体挂透明 Image+Button（点击关闭）→ QALayer 挂 M1QAPanel+M1DeepSeekClient（apiKey 留空）→ 注入 Presenter.qaPanel → Ruler/bg 接入多功能尺子.png（Multiple 子资产加载）、Ruler rect 420x91 保比例、zeroAnchorLocal=(-210,0)、禁用占位 ScaleText → BuildScenesSetup（M1/M2 index 0/1）。幂等 + 自愈（清理旧残留双 QAPanel 空根）。
2. 副本 `E:/Project/UnityGame/Probe-jiaoxue-m2-review` 执行 batchmode：QA/尺子接入后继续吸收老板 Scene 手调，并将尺子 Scene/Game 初态统一为 `RulerHome/Ruler`；最终 M2 冻结哈希 `3ef75ced51304258b5bde9b43be8f354b247753801a708ae52b922b5829c990b`，M1 `10884e91…`、M3 `f5446de3…` 字节全程不变。
3. M2 最终自动验收：GPU batchmode 三视口非空且 Scene 哈希不变；核心 Play Mode 与 M1→M2 生产入口均 PASS。首次单帧 150→100mm 会先钳在 110mm 完成伤损/峰值/蜂鸣同步，下一次输入再到 100mm。`M2FinalCloseout` 已删除全部写 Scene 能力，仅保留 47 行只读哈希验收。
3. 回拷产物：M2.unity（重新冻结）+ EditorBuildSettings.asset（M1/M2 index 0/1，SampleScene 移除）。

验证：离线 Roslyn 编译 + 副本 batchmode 均零 `error CS`；场景 YAML 509 块头/0 孤立块体；QA 节点单实例；M1QAPanel 路径/cnFont/Presenter/DeepSeek/Blocker 引用完整；M2 runtime 5 文件均 ≤150 行；`git diff --check` 仅 LF/CRLF 警告。M2Setup/M3Setup 保持只读打开器未动，M1QASetup 未动（M1 零回归）。

人工门槛（未自动执行，已写入 implement.md §5）：主 Editor 打开 M2 后 Play Mode 跑四阶段/角度/手动+自动 110mm/QA/重置/数字人、M1 QA 回归、M1→M2 串联；三视口只读截图。

收尾补记：上述核心流程、三视口和 M1→M2 串联后续均已由隔离副本自动验收通过，任务已归档。工作提交后主 Editor 又将 M2 `FullBodyView.x` 从 `-124` 手调为 `-13`，该后续视觉改动保留在工作区，未混入 M2 收口提交，也未被 Agent 覆盖。
