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

- 场景结构改动走 Setup 脚本：改 `Assets/Editor/M1Setup.cs` / `M1QASetup.cs` 后重新生成，保持幂等；
  纯视觉微调（颜色、文字、字号）可直接改场景。
- 目录：runtime 脚本 `Assets/Scripts/`，Editor 工具 `Assets/Editor/`，素材 `Assets/交互动画素材/`，
  场景 `Assets/Settings/Scenes/`，文档 `文档/`。
- 存量代码不主动重构（规则只管新增代码）；任务涉及存量文件时才顺手精简。
- 细节规范见 `.trellis/spec/unity/low-code.md`（改规范先改它，再同步本摘要）。

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
