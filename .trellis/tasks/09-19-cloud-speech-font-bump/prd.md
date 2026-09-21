# 云朵台词字号上限26→28（方案A）

## Goal

按 09-19 字体审计结论（`.trellis/tasks/09-19-font-size-audit/`），将 M2~M5 数字人云朵台词的
字号上限从 26 提到 28：短句（≤10 字）实际渲染变大，长句仍由自动缩字兜底、绝不超出云朵。

## Requirements

1. 仅改 4 个 FlowController 里运行时创建 `ModuleSpeechBubble` 的 `fontSize` 赋值：
   `Assets/Scripts/M2FlowController.cs`、`M3FlowController.cs`、`M4FlowController.cs`、`M5FlowController.cs`。
2. 不改：`ModuleSpeechBubble` 默认字段值、`minFontSize`（16/18 兜底下限）、`bubbleSize`、
   `anchorOffset`、云朵背景、台词文案与分段逻辑——即布局与台词合同零改动。
3. 改动处加日期注释，匹配代码库"老板/决策 + 日期"的注释习惯。
4. 不碰任何 Scene（M2/M3 冻结；云朵图为 Scene 资产）。

## Acceptance Criteria

- [ ] 4 处 `speechBubble.fontSize = 26f` → `28f`，其余参数逐项不变。
- [ ] Unity 6000.3.21f1 batchmode 打开工程编译通过（exit 0，无 CS 错误）。
- [ ] 全仓 grep 确认无遗漏的 26f 台词字号引用（ModuleSpeechBubble 默认值除外，运行时必被覆盖）。
- [ ] 宽高验算：M2 最长行 11 字 ×28=308 > 300 → 自动缩至 ≈27.2 装下；4 行最高 4×1.17×28 ≈ 131 ≤ 154 内高，均不超云朵。

## Notes

- 轻量任务：PRD-only。
- 方案 A 的已知取舍（已向老板侧说明并获采用）：长句受云朵宽度限制仍在 ~20px，不随上限变大；
  后续若要整体变大需换大云朵图（老板 Scene 操作，另起任务）。
