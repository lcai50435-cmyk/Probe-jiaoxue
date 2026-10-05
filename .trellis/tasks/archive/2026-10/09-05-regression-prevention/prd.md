# 反复回归根因分析与防再犯机制

父任务：`09-03-trial-check-regression-fix`（R4 的独立交付子任务）。

## Goal

老板反复遇到「明明修好的 bug，改其他问题后又出现」。本任务交付：① 根因解释（证据链）；② 轻量防再犯落地（可重复验收命令/断言 + spec 沉淀）。不建 CI/自动化跑批。

## Background（多源证据，父 prd 已查证）

- **冻结 M2.unity 当前有未提交改动**：新增 `M1DigitalHumanPresenter` 字段序列化（forceUrlPlayback/framePlayer/idleUrl/thinkingUrl/speakingUrl）与 `M2ProbeDrag` 字段（anglePromptColor/angleTitleGap/anglePromptGap）——疑似并行会话/编辑器打开保存时把运行时应由代码默认值覆盖的字段写回冻结 Scene；这是「冻结哈希漂移 → 旧值复活」的典型通道。
- 字体 asset `sarasa-gothic-sc-regular_cn.asset` 未提交改动约 2.5 万行（字距调整表），来源待确认（疑似 TMP 重导入/并行工作）。
- journal-1.md 多次记录「并行会话并发修改」「LF 也在改同一批文件」；AGENTS.md 也要求共享文件串行协调。
- 缺少「修复后必跑的验收命令」清单：M2/M3/M4 修复常靠人工 Play 验收，未沉淀为自动化断言（已有 M2RuntimeSmoke 等工具，需确认覆盖范围）。
- 冻结约定（AGENTS.md）：M2/M3/M4 Scene 视觉权威，程序不得改；运行时覆盖 Scene 旧值（代码默认数组/DontSave/运行时绑定）是主流修复方式——这本身放大了「场景被写回 → 旧值复活」的风险面。

## Requirements

### 1. 根因解释（证据链汇总）

整理成结构化根因文档（spec 章节或任务结论），至少覆盖：
- 冻结 Scene 被意外写回的发生通道（编辑器打开保存自动补序列化字段、并行会话写同一 Scene、Setup 与运行时覆盖并存）。
- 并行工作区冲突：多个 Agent/会话写同一批共享文件（FlowController/Setup/Bootstrap/ScriptableObject）。
- 验收缺口：修复无自动断言、无冻结哈希闸门，靠人工 Play，改 A 破 B 无感。
- 运行时覆盖双轨（代码默认 vs Scene 序列化值）带来的「修好又坏」放大器。

### 2. 轻量防再犯落地

- 为本次父任务四项修复各沉淀一条**可重复执行的验收命令/断言**（能在 Editor batchmode / Play 自动跑的最小集合）。
- 确认现有冒烟/断言工具覆盖面（M2RuntimeSmoke、M3/M4 是否有对应、Scene 哈希校验脚本），缺口列出并补最小可用项（不建 CI）。
- 经 `trellis-update-spec` 把约定写进 `.trellis/spec/unity/`（low-code.md 5.4 或对应章节）：① 冻结 Scene 哈希校验为每次修复后的必跑验收；② 全模块 Play 冒烟清单；③ 并行会话写共享文件前的串行协调规则（读 AGENTS.md 已有约定并强化为可操作清单）。

## Acceptance Criteria

- [ ] 根因解释文档完成：证据链完整、结论可执行，写入 spec 或任务收尾说明。
- [ ] 本次修复（R1/R2/R3）的验收命令/断言清单已产出并实际执行通过。
- [ ] 冻结哈希校验约定沉淀进 `.trellis/spec/unity/`，含一条可复现的命令。
- [ ] 全模块冒烟约定沉淀（M1-M5 或受影响模块范围 + 命令）。
- [ ] 共享文件并行修改规则在 spec/AGENTS.md 中可达（不新增漂移文档）。
- [ ] 无新增业务代码/Scene 改动；不触碰未授权历史未提交改动（M2.unity/字体 asset 只审查不改）。

## Out Of Scope

- 搭建 CI / 自动化跑批 / git hook（重落地，老板已定不做，可另立任务）。
- 实际修复 R1/R2/R3（兄弟子任务）。
- 清理/回退 M2.unity 与字体 asset 的未提交改动（先审查归属，交老板决策）。
