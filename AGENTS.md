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
