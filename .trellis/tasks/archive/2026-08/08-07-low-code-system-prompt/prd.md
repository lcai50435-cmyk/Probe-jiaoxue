# 低代码优先的项目系统提示词

## Goal

为本项目定制一份「低代码优先」的系统提示词，使所有 AI 编码任务的产出尽量精简：优先配置化、可视化方案，代码最小化、通用化、可维护化。客户要求低代码，本提示词将成为项目内 AI 助手（pi / Claude Code / Cursor 等）的统一行为约束。

## 背景（已确认事实）

- Unity URP 2D 项目：钢轨探伤仿真交互动画教学演示 + AI 问答面板（M1 模块）。
- 现有 runtime 代码仅 3 个脚本共 611 行（M1PressDetector.cs 47、M1QAPanel.cs 384、M1ToolSelection.cs 180）。
- `Assets/Editor/` 已有 3 个一键搭建工具（M1Setup.cs、M1QASetup.cs、GenerateChineseFont.cs），项目已有「Editor 工具生成 + 配置驱动」的倾向。
- 项目根 `AGENTS.md` 仅含 Trellis 托管块（TRELLIS:START/END），块外为空，可自由添加项目规范。
- `.trellis/spec/` 现有 backend/frontend 模板规范与 Unity 项目不匹配，无 Unity 专属规范。
- 旁证：并行任务 `08-07-m1-ui-visual-optimization` PRD 已引用「低代码优先倾向：优先场景配置化修复，尽量不改/少改运行时代码」，即本项目低代码实践基线。
- 全局 AGENTS.md 已有 KISS/YAGNI/DRY 原则，本项目提示词在其基础上强化「低代码」并补充 Unity 专属约定。

## Requirements

### 落地形态（方案 C）

- R1 项目根 `AGENTS.md` Trellis 块外新增「低代码优先」总纲：简体中文、≤60 行。
- R2 新增 `.trellis/spec/unity/low-code.md` 细节规范，并在 `.trellis/spec/unity/index.md` 登记。
- R3 分层同步约定：AGENTS.md 总纲是 spec 的摘要，改规范先改 spec 再同步总纲（写入 spec 文档）。

### 低代码规则（强约束 + 默认值，五条）

- R4 默认禁止新增专用脚本：新功能先问「能否用现有组件 + Inspector/配置实现」，能则写配置不写代码；必须新增时说明理由。
- R5 单个新增 runtime 脚本默认 ≤150 行，超限先拆分或改为配置。
- R6 数据一律配置化（ScriptableObject / Inspector 字段 / 场景配置），禁止硬编码。
- R7 跨模块复用通用组件（M1 即 M2/M3 的起点），禁止复制粘贴式扩展。
- R8 不引入 Bolt/Visual Scripting 等可视化脚本包（增加依赖、与 AI 协作冲突）。

### 规则边界

- R9 Editor 工具脚本（`Assets/Editor/`）豁免 150 行上限（低代码放大器），但其余规则（配置化、复用、禁硬编码）仍适用。
- R10 存量超标代码（如 M1QAPanel.cs 384 行）不主动重构；仅当任务本身涉及该文件时顺手精简。

### 内容范围（全含，分层控制篇幅）

- R11 总纲含：项目一句话定位 + 技术栈速览 + 五条低代码规则 + 关键项目约定速查。
- R12 细节规范含：低代码决策树、runtime 脚本规范、配置化规范、Editor 工具规范（含幂等要求）、场景改动规则、模块/命名约定、禁止事项。
- R13 场景改动规则（方案 A）：结构改动走 Setup 脚本（改 `Assets/Editor/*Setup.cs` 后重新生成，保持幂等）；纯视觉微调（颜色、文字等）可直接改场景。

## Acceptance Criteria

- [ ] `AGENTS.md` 块外新增简体中文低代码总纲，≤60 行，含项目速览、五条规则、约定速查；任何 AI 工具读取项目根即可见。
- [ ] `.trellis/spec/unity/low-code.md` 存在并登记于 `index.md`；细节与总纲一致，无冲突；引用真实路径（`Assets/Scripts/`、`Assets/Editor/`、`文档/`）。
- [ ] 五条强约束（R4-R8）、两条边界（R9-R10）、场景规则（R13）全部在规范中可执行、无空洞口号。
- [ ] 文档自身精简：总纲 ≤60 行；规范无重复段落。

## Out of Scope

- 编写/交付给客户看的低代码展示文档（用户已选方案 C，不选 D）。
- 重构现有存量代码。
- 修改 Trellis 托管块内容。

## Open Questions

（无——规划已完成，待用户审阅 design.md 后批准）
