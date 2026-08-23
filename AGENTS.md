# Probe 仿真交互动画项目 — AI 协作规范（低代码优先）

## 项目速览

钢轨探伤仿真交互动画教学演示（Unity URP 2D，含 AI 问答面板，当前为 M1 模块）。
技术栈：URP 2D、Input System、TextMesh Pro、Editor 一键搭建工具。
需求文档：`文档/`（技术规格书、功能文档、DeepSeek 接入方案）。

## 低代码优先（客户硬性要求）

任何代码任务，先问「能否不写代码」。默认禁止新增专用脚本，必须新增时先说明理由。

1. 配置化优先：数据/参数进 ScriptableObject 或 Inspector 字段，禁止硬编码。
2. 代码精简：新增 runtime 脚本默认 ≤150 行，超限先拆分或改为配置；拒绝样板代码与过度设计。
3. 复用优先：跨模块复用通用组件（M1 组件即 M2/M3 的起点），禁止复制粘贴式扩展。
4. Editor 工具化：重复性搭建用 Editor 工具自动化（如 M1Setup.cs）；Editor 脚本豁免行数上限，但仍须精简。
5. 禁止引入 Bolt/Visual Scripting 等可视化脚本包。

## 项目约定速查

- 未冻结场景的结构改动走 Setup 脚本：改 `Assets/Editor/M1Setup.cs` / `M1QASetup.cs` 后重新生成，保持幂等；纯视觉微调可直接改场景。
- **M2/M3 Scene 已冻结**：当前 `Assets/Settings/Scenes/M2.unity`、`M3.unity` 是视觉权威，后续 Setup/程序/Agent 不得修改、创建、重生成或保存覆盖；只有老板可在 Unity Scene 中手工改视觉。M2/M3 Setup 只能打开/检测现有 Scene 并跳过，缺失时报错；功能必须走 runtime 动态绑定、已有节点或非视觉组件，禁止重写 RectTransform、Graphic/TMP、文案、颜色、Sprite、active、sibling。
- 目录：runtime 脚本 `Assets/Scripts/`，Editor 工具 `Assets/Editor/`，素材 `Assets/交互动画素材/`（工具图 `Assets/InspectionToolMaterials/`、探头图 `Assets/probeFootage/`、音频 `Assets/Audio/`、数字人 `Assets/DigitalHuman/`），
  场景 `Assets/Settings/Scenes/`，文档 `文档/`。
- 存量代码不主动重构（规则只管新增代码）；任务涉及存量文件时才顺手精简。
- **引导/讲解视频复用 M1IntroVideo + UI-LumaKey**（首次记忆、暂停恢复、黑底抠像悬空人物都已封装好，
  新模块只换 VideoClip；调参走材质 Inspector）。H.264 `yuv420p` 黑底视频无 Alpha，仍需 LumaKey；视频 UI 节点只放一个 Graphic，点击由 RawImage 自身或独立子节点承载。常驻数字人（小尺寸）用独立 LumaKey 材质资产收窄 KeySmooth + RT 开 mipmap（关闭 autoGenerateMips 后在 VideoPlayer.frameReady 中显式 GenerateMips），不碰开场引导材质。详见 `.trellis/spec/unity/video-intro.md`。
- **问答面板打开即全局暂停**（`M1QAPanel.pauseGameOnOpen` 默认开）：`Time.timeScale=0` 含模块计时/拖拽/动画，关闭时恢复打开前值；问答链路组件必须走 unscaled 计时（长按/滑入/逐字/请求/视频）。详见 `.trellis/spec/unity/low-code.md` 8.1。
- **探测模块统一采用 M3 验收的 UGUI 基线**：1920x1080/Match0.5，浅灰页面与白色教学面、左上局部工具架、右上全身数字人、右下 460x240 深色波形、左下 364x64 视图分段、底部 176px 浅色操作带；M2 仅做保留业务合同的视觉迁移，M4/M5 只替换模块专属流程/参数/素材，禁止复制其他模块状态机。详见 `.trellis/spec/unity/ugui-module-template.md`。
- **冻结 Scene 的运行时文案与几何合同**（详见 low-code.md 5.4）：旧文案数组（如 stepHints）用代码默认数组覆盖不写回；尺子 0→110 锚点必须位于同一可见刻度基线并作为唯一 mm 比例（二维欧氏距离）；入射点锚点偏置必须在扫描端点反推补偿，否则欧氏距离合同失效；M1→M2 链路烟测须轮询等待场景加载。
- **2026-08-14 PPT 四要点**：校角与测量统一尺子尺寸（420×91，ppm≈2.768）；测量尺水平放置（localRotation=0），扫描轨迹线与损伤点同线（scanLineY=damage.y）为前提；波形简化参考「焊筋轮廓波」——深底/网格/橙红基线+尖峰，隐藏 WaveStateText/CurrentDistanceText 等提示词，MeasurementBubble 的 110mm 字样改「测量完成」。
- **2026-08-14 视觉反馈二轮（探头遮挡/悬空）**：探头发射面锚点 `probeEntryLocal=(0.89,0.04)`（probeFootage 右下楔形），校角发射面卡槽、测量与尺子平行无遮挡；损伤点 `damageUv.y=0.711→0.63`（红椭圆下部/下缘）使扫描线/探头下移贴普通视图钢轨踏面；均为运行时覆盖 Scene 旧值不写回。详见 low-code.md 5.4。
- **2026-08-15 M2 波形探伤仪屏化（二轮定稿，Scene 直做）**：老板授权直接改 `M2.unity` 波形窗口区域——4:3（460×345，y=172.5）；删 WaveHeader 提示词与旧 M2WaveformGraphic；WaveGrid 全 stretch 挂 `M2WaveformFx`（序列化）；新增横轴 0~200mm/纵轴 0~100 刻度文字；`M2WaveformFx` 必须有 `RequireComponent(CanvasRenderer)`（否则 Play 不渲染）；点状"+"网格 + 常驻绿色始波（脉冲尖峰，不画竖线）+ 底部绿色锯齿噪声线；伤损波同形同色，150mm 短波（8%/X≈75%）→ 115mm 最高（78%/X≈57.5%）→ 110mm（X≈55%）检出锁定；纯状态驱动，暂停天然冻结。`M2WaveformFx` 代码默认值已更新为 160/123/120 供后续新场景，M2 Scene 仍序列化 150/115/110。M3 旧样式零改动。详见 low-code.md 5.4。
- **2026-08-16 M2 检出视觉反馈**：探测到损伤时报警蜂鸣的同时，射线由绿色变橙色（`M2ProbeDrag.beamDetectedColor` Inspector 可调；独立橙色渐变 Sprite），Reset 恢复绿色；烟测新增橙色纹理断言。详见 low-code.md 5.4。
- **2026-08-16 M3 按 PPT 对齐**：老板授权同步 M3 Scene/Play；扫描 160→120mm（Bind 运行时覆盖，Scene 旧 120.96 会使一放就检出）、到达 120 检出锁定；波形复用 `M2WaveformFx`（160 短波→123 最高→120 停止）；目标以伤损为主，测距 0/120 双点；射线绿→橙复用 M2；进入时不再播放自动耦合剂 Intro，直接定位避免开场延迟。射线：默认 200mm（Bind 覆盖）前 ~5° 长度不变，仅当射线会碰到/超出**红椭圆（伤损）下边缘**才缩到刚好碰下边缘（`min(默认, drop/sin)` 连续无突变）；**检出=射线末端实际到达/越过伤损**（`BeamLenPx ≥ 沿射线到伤损距离`）。详见 low-code.md 5.4 与 module-flow-contract.md §9。
- **2026-08-16 M3 拖动按钮样式同步 M2**：老板确认 M3 角度滑块视觉采用 M2 同款——`AngleTrack` 深灰圆角粗条、`Handle` 32×48 细长圆角条且初始左端对齐、`Fill`/`Handle` 圆角 Sprite 与 M2 一致。详见 low-code.md 5.4。
- **2026-08-16 M2 检出即测距**：M2 与 M3 一致——检出瞬间锁定探头并直接解锁尺子测量，无"下一步"按钮门控（`NextToMeasure` 已删，nextButton 不再激活）；`M2RulerDrag.Awake` 强制测量尺水平放置（measureAngleDeg=0/measureOffset=0，Scene 旧值 9.55/(19,28) 不写回）。尺子工作态 localScale 保持 1，禁止折算 PixelsPerMm（会改变探头初始放置位置）。详见 low-code.md 5.4 与 module-flow-contract.md §10。
- **2026-08-18 M5 擦拭耦合剂（复用 M2 UGUI 骨架，单步结束模块）**：起始（M2 轨顶视角 + 钢轨顶面涂蓝色耦合剂）→ 拖擦拭布（rag.png）至轨顶 → 左右拖动控制擦拭范围（进度跟手）→ 100% 通过；无探测流程/无下一模块（完成面板"M5 擦拭耦合剂完成"）。ToolShelf 三槽位（ProbeHome/RulerHome/RagHome，探头/尺子静态展示与 M2 同款，RagHome 紧邻 RulerHome 右侧）。耦合剂 = `M5CouplantFx` 从 `俯视角.png` 切 coverRect 子矩形薄膜，初始 fillAmount=1 铺满、擦拭 fillOrigin=1 + fillAmount=1-p（与 M2CouplantFx 状态相反）；`M5RagDrag` Home（RagHome 槽置灰）→ 工作态跟手；`M5FlowController` Stage { Wipe, Completed }；透视视图隐藏耦合剂层。Scene 由 `M5Setup.cs` 生成（未冻结幂等）；数字人/QA 复用 `M3DigitalHumanBootstrap`（场景名 M3/M4/M5）。浅色工具置灰坑：rag 需加深置灰 (0.45,0.47,0.5,0.9) + Outline 描边，否则像透明。详见 low-code.md 5.4。
- **2026-08-23 M5 波形窗口保留 + 钢轨 Scene 权威（老板定稿）**：M5 保留 M2 波形窗口（SupportArea 静态视觉，`M2WaveformFx` 程序化绘制与 M2 同款——深底/蓝条红条/网格/绿色始波/噪声线，参数 150/115/110；Setup 检测 WaveGrid 缺组件自动补回）。老板手工调整钢轨位置（RailBackground x=-236 左移避开波形窗口），**M5Setup 对 railBg 布局 Scene 权威**（sizeDelta 非 0 不覆盖，仅新建/空布局设默认），视觉微调不会被 Setup 重置。
- **2026-08-23 M5 工具架用 MainScene/Tool（方案 B）**：老板手工添加 Tool（M2 样式三槽位 ProbeHome/RulerHome/RagHome），M5Setup 在 Tool 存在时跳过 ToolShelf 创建；Probe/Ruler 仅静态展示（M2ProbeDrag/M2RulerDrag 移除），RagHome 槽位工具节点改名为 Rag + rag.png/M5RagDrag（擦拭功能，尺寸方形 80×80），EnsureAll 查找优先 Tool 树。
- **2026-08-23 耦合剂薄膜 CouplantMask 布局 Scene 权威**：老板手工对准钢轨（pos (-232,-32)），`M5CouplantFx.Init()` 仅空布局时按 coverRect 计算，非空不覆盖（与钢轨同约定）。
- **2026-08-23 M5 三修复**：① 数字人舞台 DigitalHumanStage 布局 Scene 权威（Setup 不覆盖，避免数字人裁切）；② 透视钢轨与普通钢轨同布局（以普通视图为准，不再 Stretch）；③ rag 初始可拖（unlocked=true，单步交互，置灰仅视觉）。
- **2026-08-23 CouplantOverlay 布局 Scene 权威**：老板调 position/scale 贴合钢轨，Setup 仅空布局才 Stretch（不重置已调布局）。
- **2026-08-23 完成面板不显示 + QA/数字人 Bootstrap 壳**：擦拭完成后不弹"M5 擦拭耦合剂完成"；M5 数字人/QA 由 M3DigitalHumanBootstrap 装配（Adapt 清理 M2 旧 M1 组件/节点，QAPanel 与 DigitalHumanStage 保持空壳）。
- **2026-08-23 M5 数字人 = M2 合同**：Bootstrap 对 M5 场景用 M2 参数（FullBodyView 底部全高 + HeightControlsWidth + pos(-13,-35)，AvatarView y=-40），M3/M4 不变。
- **2026-08-23 钢轨 preserveAspect=false**：与 M2 一致（拉伸填满容器，不按比例缩小留白）。
- **2026-08-23 RailViewport/bg 同步 M2 + 钢轨/透视/耦合剂 Scene 权威**：M5 补 M2 白底 bg（Stretch 底部内缩 99.828，最底）；RailBackground/RailPerspective/CouplantOverlay 的 scale/position 老板手调，Setup 仅空布局设默认不覆盖。详见 low-code.md 5.4。
- **Unity 6 伪 null 坑（2026-08-18）**：`GetComponent<T>()` 对缺失组件返回 Unity 伪 null，`??` 不触发——禁止 `GetComponent ?? AddComponent` 写法，必须 `if (x == null)` 分步；`TextAlignmentOptions` 无 `MiddleCenter`（用 `Center`）；EventSystem 必须用 `InputSystemUIInputModule`（项目 Input System 模式，旧 StandaloneInputModule 每帧抛 InvalidOperationException）。详见 low-code.md 7.1。
- 细节规范见 `.trellis/spec/unity/`（low-code.md / video-intro.md / ugui-module-template.md；改规范先改它，再同步本摘要）。

<!-- TRELLIS:START -->
# Trellis Instructions

These instructions are for AI assistants working in this project.

This project is managed by Trellis. The working knowledge you need lives under `.trellis/`:

- `.trellis/workflow.md` — development phases, when to create tasks, skill routing
- `.trellis/spec/` — package- and layer-scoped coding guidelines (read before writing code in a given layer)
- `.trellis/workspace/` — per-developer journals and session traces
- `.trellis/tasks/` — active and archived tasks (PRDs, research, jsonl context)

If a Trellis command is available on your platform (e.g. `/trellis:finish-work`, `/trellis:continue`), prefer it over manual steps. Not every platform exposes every command.

If you're using Codex or another agent-capable tool, additional project-scoped helpers may live in:
- `.agents/skills/` — reusable Trellis skills
- `.codex/agents/` — optional custom subagents

Managed by Trellis. Edits outside this block are preserved; edits inside may be overwritten by a future `trellis update`.

<!-- TRELLIS:END -->
