# 全模块字体放大第一批（审计可放心清单）

## Goal

实施 09-19 审计（`.trellis/tasks/09-19-font-size-audit/`）"✅ 可放心放大"清单：
模块标题、底部步骤条、各按钮字、QA 面板文字按建议值放大，最长文案已逐条验证不超框。

## Requirements

1. **范围**（严格按已确认清单，谨慎/不建议项一律不动）：
   - 模块标题 36→40；M1 主标题 50→56
   - 底部步骤条 26/27→30（M2/M3/M4 `StepProgress/Text`、M5 `StepProgressText`）；M2 指令行 26→28
   - 视图切换 24→26；重置流程 28→30；重置弹窗确认/取消 →32；M2 涂抹耦合剂 28→32
   - 下一模块按钮：M3 30→32、M4 30→34；**M2 保持 28**（"进入轨头侧面探测"8 字顶满 242px，审计❌项）
   - 开始探测/点击继续 36→40；跳过引导 32→36
   - QA 面板：标题 34→38（M1/M2 Scene + M3~M5 运行时 Bootstrap）、发送 32→36、语音 30→32、
     问答气泡 28→30（M1QAPanel 全模块共用）
2. **机制**：Scene 序列化值零改动（M2/M3 冻结；M1/M4/M5 也不写回，保持单一机制），
   全部走运行时覆盖——与 stepHints/重置弹窗既有模式一致。新建共享静态类
   `ModuleFontBump`（≤150 行，全局命名空间与 Module* 同风格），
   M2~M5 FlowController / M1ToolSelection 的 Awake 各加一行调用。
   新增理由：5 个模块 × 20+ 项的字号表集中一处，避免复制粘贴魔法数（低代码规范"复用优先"）。
3. 不动：谨慎项（M3/M4 指令长句、漂浮提示、云朵）、不建议项（波形刻度、计数、Placeholder、
   ×、M2 进入下一模块按钮、检出横幅、视频字幕）、任何 RectTransform/文案/颜色。
4. 运行时创建的 QA 文本直接改创建处常量：`M1QAPanel.AddMessage` 28→30、
   `M3DigitalHumanBootstrap` Title 34→38 / Send 32→36 / Voice 30→32。

## Acceptance Criteria

- [ ] `ModuleFontBump` ≤150 行；5 处 Awake 各一行接线。
- [ ] 覆盖目标在 5 个 Scene 中按名字可定位（唯一性已人工核对），节点缺失时静默跳过不报错。
- [ ] Unity 6000.3.21f1 batchmode 编译通过（exit 0、无 CS 错误）。
- [ ] `git status` 确认 5 个 Scene 文件零新增改动（M2.unity 维持会话前的既有脏状态）。
- [ ] 全部新值与审计"可放心"建议值一致；无谨慎/不建议项被放大。

## Notes

- 轻量任务：PRD-only。
- 效果验收：字号为运行时覆盖，Editor Scene 视图与 Shot 截图看不到变化，需 Play 模式或真机确认；
  数学验算已在审计报告逐条完成（最长文案 @ 新字号 vs 容器，均留 ≥2px 余量）。
- 云朵台词 26→28 已由 `.trellis/tasks/09-19-cloud-speech-font-bump/` 单独实施。
