# Design: 低代码优先的项目系统提示词

## 产出物结构

```
AGENTS.md                        ← R1：Trellis 块外新增总纲（≤60 行，中文）
.trellis/spec/unity/
├── index.md                     ← R2：新增，登记 low-code.md
└── low-code.md                  ← R2：细节规范（决策树/脚本/配置/Editor/场景/约定）
```

同步规则（R3）：`low-code.md` 为权威版本，AGENTS.md 总纲为其摘要；改规范先改 `low-code.md`，再同步总纲。

## AGENTS.md 总纲草稿（待审阅，目标 ≤60 行）

```markdown
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
```

（约 38 行，预留余量。）

## spec 大纲：`low-code.md`

1. **适用范围与权威性** — 所有 AI 编码任务；本文档为权威，AGENTS.md 总纲为摘要。
2. **低代码决策树** — ① 能否纯配置/Inspector 实现？→ 写配置；② 能否复用现有组件？→ 复用；③ 能否用 Editor 工具生成？→ 写/改 Editor 工具；④ 以上皆否 → 新增 runtime 脚本并说明理由。
3. **runtime 脚本规范** — ≤150 行默认上限；超限流程（拆分或改配置）；命名（M1 前缀模块名 + 职责，如 M1QAPanel）；中文注释不强制。
4. **配置化规范** — 数据/参数进 ScriptableObject / Inspector / 场景配置；禁止硬编码魔数；配置项需有 Inspector 默认值。
5. **Editor 工具规范** — 豁免行数上限；必须幂等（重复执行不产生重复对象，参考 M1Setup 现状）；生成对象命名可预测；改动 Setup 需说明重新生成的影响。
6. **场景改动规则（方案 A）** — 结构改动（增删元素、布局参数）走 Setup；纯视觉微调直接改场景；禁止两者混合产生漂移。
7. **模块与目录约定** — 目录职责、M1 模块结构（QAPanel / ToolSelection / PressDetector）、场景文件位置。
8. **禁止事项** — Bolt/Visual Scripting、复制粘贴式扩展、无理由新增脚本、主动重构存量（除非任务涉及）。
9. **与 AGENTS.md 的同步契约** — 修改本文档后必须同步总纲；总纲仅摘要，不存放细节。

`index.md`：引用 `low-code.md`，说明其适用范围为 Unity 项目全部代码任务。

## 验收核对方式

- 总纲行数 ≤60（wc -l 校验块外部分）。
- 总纲五条规则与 spec 第 2/3/4/5/8 节一一对应，无遗漏无冲突。
- 所有路径引用与仓库实际路径核对一致。
